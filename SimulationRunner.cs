using System.Collections.Concurrent;
using System.Diagnostics;

namespace Tideglass;

/// <summary>Single owner of the live world. UI reads snapshots; never waits for a tick.</summary>
public sealed record SimulationStatus(double Time, int Population, int Generation, double Speed, int AutomaticRestarts, int? Seed = null);

public sealed class SimulationRunner
{
    private World live;
    private World snapshot;
    private readonly ConcurrentQueue<Action> commands = new();
    private readonly object commandGate = new();
    private bool acceptingCommands = true;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task worker;
    private int requestedSpeed = 1;
    private double achievedSpeed;
    private int reducedDisplay, noDisplay, restartOnExtinction, collectData;
    private int populationCap;
    public int PopulationCap
    {
        get => Volatile.Read(ref populationCap);
        set { ArgumentOutOfRangeException.ThrowIfNegative(value); Volatile.Write(ref populationCap, value); }
    }
    private readonly ResearchRecorder recorder;
    public bool CollectData { get => Volatile.Read(ref collectData) != 0; set => Volatile.Write(ref collectData, value ? 1 : 0); }
    public string? DataError => recorder.Error;
    private bool worldChanged;
    private SimulationStatus status;
    public SimulationStatus Status => Volatile.Read(ref status);
    public bool NoDisplay { get => Volatile.Read(ref noDisplay) != 0; set => Volatile.Write(ref noDisplay, value ? 1 : 0); }
    public bool ReducedDisplay { get => Volatile.Read(ref reducedDisplay) != 0; set => Volatile.Write(ref reducedDisplay, value ? 1 : 0); }
    public bool RestartOnExtinction { get => Volatile.Read(ref restartOnExtinction) != 0; set => Volatile.Write(ref restartOnExtinction, value ? 1 : 0); }
    public World Snapshot => Volatile.Read(ref snapshot);
    public int Speed { get => Volatile.Read(ref requestedSpeed); set => Volatile.Write(ref requestedSpeed, value); }
    public double AchievedSpeed => Volatile.Read(ref achievedSpeed);
    public string? Error { get; private set; }
    public SimulationRunner(World world, int initialSpeed = 1, string? dataDirectory = null)
    {
        recorder = new ResearchRecorder(dataDirectory); CollectData = world.CollectData;
        live = world; snapshot = live.Snapshot();
        requestedSpeed = initialSpeed; RestartOnExtinction = live.RestartOnExtinction; PopulationCap = live.PopulationCap;
        status = new(live.Time, live.Organisms.Count, live.RecordGeneration, 0, live.AutomaticRestarts, live.Seed);
        worker = Task.Run(Run);
    }
    private async Task Run()
    {
        var clock = Stopwatch.StartNew();
        double previous = 0, nextPublish = 0, nextFields = 0, lastRate = 0, lastTime = live.Time, accumulator = 0;
        int lastSpeed = Speed; bool lastReduced = ReducedDisplay, lastNoDisplay = NoDisplay;
        void ResetTiming(double now)
        {
            worldChanged = false; accumulator = 0; previous = now;
            nextPublish = nextFields = 0; lastTime = live.Time; lastRate = now;
            Volatile.Write(ref achievedSpeed, 0);
            Volatile.Write(ref status, new(live.Time, live.Organisms.Count, live.RecordGeneration, 0, live.AutomaticRestarts, live.Seed));
        }
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                while (commands.TryDequeue(out var command)) command();
                double now = clock.Elapsed.TotalSeconds;
                if (worldChanged) ResetTiming(now);
                live.RestartOnExtinction = RestartOnExtinction; live.CollectData = CollectData; live.PopulationCap = PopulationCap;
                recorder.Observe(live, CollectData);
                int speed = Speed;
                bool displayOff = NoDisplay;
                if (lastNoDisplay != displayOff)
                {
                    lastNoDisplay = displayOff; accumulator = 0; previous = now;
                    nextPublish = 0; nextFields = 0;
                }
                if (speed != lastSpeed) { accumulator = 0; previous = now; lastSpeed = speed; nextPublish = 0; nextFields = 0; }
                if (lastReduced != ReducedDisplay) { lastReduced = ReducedDisplay; nextPublish = 0; nextFields = 0; }
                accumulator = speed == 0 ? 0 : Math.Min(accumulator + Math.Min(.1, now - previous) * speed, Math.Max(1, speed * .5));
                previous = now;
                long budgetStart = Stopwatch.GetTimestamp();
                while ((displayOff || accumulator >= .1) && Stopwatch.GetElapsedTime(budgetStart).TotalMilliseconds < (displayOff || speed > 1 ? 24 : 8) && Speed > 0 && NoDisplay == displayOff)
                {
                    live.PopulationCap = PopulationCap;
                    live.Step(.1); if (CollectData) recorder.Observe(live, true); if (!displayOff) accumulator -= .1;
                    if (live.Organisms.Count == 0 && RestartOnExtinction && Speed > 0)
                    {
                        // Derive a fresh, repeatable seed from the finished run, without using
                        // the UI clock or changing any surviving organism's selection pressures.
                        int seed = unchecked((int)(live.RandomState + 0x9E3779B9u));
                        recorder.End(live, "extinction");
                        var fresh = World.Create(seed, PopulationCap); fresh.CollectData = CollectData;
                        fresh.RestartOnExtinction = true; fresh.MutationMode = live.MutationMode;
                        fresh.AutomaticRestarts = live.AutomaticRestarts + 1;
                        fresh.Note($"Restarted after extinction / restart {fresh.AutomaticRestarts} / previous run {live.Time / 60:F1} min, generation {live.RecordGeneration}");
                        live = fresh;
                        ResetTiming(clock.Elapsed.TotalSeconds);
                        break;
                    }
                }
                now = clock.Elapsed.TotalSeconds;
                if (!displayOff && now >= nextPublish)
                {
                    bool fields = now >= nextFields;
                    Volatile.Write(ref snapshot, live.Snapshot(snapshot, fields));
                    bool reduced = ReducedDisplay && speed > 1;
                    if (fields) nextFields = now + (reduced ? 1 : .25);
                    double interval = Math.Max(reduced ? .5 : speed >= 20 ? .10 : .05, Math.Min(.5, live.Organisms.Count / 20000.0));
                    nextPublish = speed == 0 ? double.PositiveInfinity : now + interval;
                }
                if (now - lastRate >= 1)
                {
                    Volatile.Write(ref achievedSpeed, (live.Time - lastTime) / (now - lastRate));
                    Volatile.Write(ref status, new(live.Time, live.Organisms.Count, live.RecordGeneration, AchievedSpeed, live.AutomaticRestarts, live.Seed));
                    lastTime = live.Time; lastRate = now;
                }
                if ((displayOff && Speed > 0) || (speed > 1 && accumulator >= .1)) await Task.Yield();
                else await Task.Delay(2).ConfigureAwait(false);
            }
        }
        catch (Exception ex) { Error = ex.ToString(); }
        finally
        {
            lock (commandGate) acceptingCommands = false;
            while (commands.TryDequeue(out var command)) command();
            recorder.End(live, "closed"); recorder.Dispose();
        }
    }
    internal async Task<double> ReadTimeAsync()
    {
        double result = 0; await Enqueue(() => result = live.Time); return result;
    }
    public Task SaveAsync(string? path = null) => Enqueue(() => { live.RestartOnExtinction = RestartOnExtinction; live.CollectData = CollectData; live.PopulationCap = PopulationCap; live.Save(path); });
    public Task NewAsync(int seed, string? backupPath = null) => Enqueue(() =>
    {
        live.RestartOnExtinction = RestartOnExtinction; live.CollectData = CollectData; live.PopulationCap = PopulationCap;
        live.Save(backupPath ?? World.SavePath + ".previous");
        recorder.End(live, "new-world");
        var fresh = World.Create(seed, PopulationCap); fresh.CollectData = CollectData;
        fresh.RestartOnExtinction = RestartOnExtinction; fresh.MutationMode = live.MutationMode;
        live = fresh; worldChanged = true;
        Volatile.Write(ref snapshot, live.Snapshot());
    });
    private Task Enqueue(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (commandGate)
        {
            if (!acceptingCommands) { completion.SetException(new InvalidOperationException(Error ?? "Simulation stopped")); return completion.Task; }
            commands.Enqueue(() => { try { action(); completion.SetResult(); } catch (Exception ex) { completion.SetException(ex); } });
        }
        return completion.Task;
    }
    public async Task StopAsync() { cancellation.Cancel(); await worker.ConfigureAwait(false); cancellation.Dispose(); }
}
