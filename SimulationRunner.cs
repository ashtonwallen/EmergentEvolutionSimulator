using System.Collections.Concurrent;
using System.Diagnostics;

namespace Tideglass;

/// <summary>Single owner of the live world. UI reads snapshots; never waits for a tick.</summary>
public sealed class SimulationRunner
{
    private World live;
    private World snapshot;
    private readonly ConcurrentQueue<Action> commands = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task worker;
    private int requestedSpeed = 1;
    private double achievedSpeed;
    private int reducedDisplay;
    public bool ReducedDisplay { get => Volatile.Read(ref reducedDisplay) != 0; set => Volatile.Write(ref reducedDisplay, value ? 1 : 0); }
    public World Snapshot => Volatile.Read(ref snapshot);
    public int Speed { get => Volatile.Read(ref requestedSpeed); set => Volatile.Write(ref requestedSpeed, value); }
    public double AchievedSpeed => Volatile.Read(ref achievedSpeed);
    public string? Error { get; private set; }
    public SimulationRunner(World world)
    {
        live = world; snapshot = live.Snapshot();
        worker = Task.Run(Run);
    }
    private async Task Run()
    {
        var clock = Stopwatch.StartNew();
        double previous = 0, nextPublish = 0, nextFields = 0, lastRate = 0, lastTime = live.Time, accumulator = 0;
        int lastSpeed = Speed; bool lastReduced = ReducedDisplay;
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                while (commands.TryDequeue(out var command)) command();
                double now = clock.Elapsed.TotalSeconds;
                int speed = Speed;
                if (speed != lastSpeed) { accumulator = 0; previous = now; lastSpeed = speed; nextPublish = 0; nextFields = 0; }
                if (lastReduced != ReducedDisplay) { lastReduced = ReducedDisplay; nextPublish = 0; nextFields = 0; }
                accumulator = speed == 0 ? 0 : Math.Min(accumulator + Math.Min(.1, now - previous) * speed, Math.Max(1, speed * .5));
                previous = now;
                long budgetStart = Stopwatch.GetTimestamp();
                while (accumulator >= .1 && Stopwatch.GetElapsedTime(budgetStart).TotalMilliseconds < (speed > 1 ? 24 : 8) && Speed > 0)
                {
                    live.Step(.1); accumulator -= .1;
                }
                now = clock.Elapsed.TotalSeconds;
                if (now >= nextPublish)
                {
                    bool fields = now >= nextFields;
                    Volatile.Write(ref snapshot, live.Snapshot(snapshot, fields));
                    bool reduced = ReducedDisplay && speed > 1;
                    if (fields) nextFields = now + (reduced ? 1 : .25);
                    nextPublish = now + (reduced ? .5 : speed >= 20 ? .10 : .05);
                }
                if (now - lastRate >= 1)
                {
                    Volatile.Write(ref achievedSpeed, (live.Time - lastTime) / (now - lastRate));
                    lastTime = live.Time; lastRate = now;
                }
                if (speed > 1 && accumulator >= .1) await Task.Yield();
                else await Task.Delay(2).ConfigureAwait(false);
            }
        }
        catch (Exception ex) { Error = ex.ToString(); }
        finally { while (commands.TryDequeue(out var command)) command(); }
    }
    public Task SaveAsync(string? path = null) => Enqueue(() => live.Save(path));
    public Task NewAsync(int seed) => Enqueue(() => { live.Save(World.SavePath + ".previous"); live = World.Create(seed); Volatile.Write(ref snapshot, live.Snapshot()); });
    private Task Enqueue(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (worker.IsCompleted) { completion.SetException(new InvalidOperationException(Error ?? "Simulation stopped")); return completion.Task; }
        commands.Enqueue(() => { try { action(); completion.SetResult(); } catch (Exception ex) { completion.SetException(ex); } });
        return completion.Task;
    }
    public async Task StopAsync() { cancellation.Cancel(); await worker.ConfigureAwait(false); cancellation.Dispose(); }
}
