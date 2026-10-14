using System.Diagnostics;
using Tideglass;

static class RestartChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static async Task Until(Func<Task<bool>> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!await condition())
        {
            if (timer.Elapsed.TotalSeconds > 8) throw new Exception("Restart check timed out");
            await Task.Delay(20);
        }
    }

    public static async Task Run()
    {
        string path = Path.Combine(Path.GetTempPath(), "tideglass-restart-" + Guid.NewGuid() + ".json");
        try
        {
            foreach (bool noDisplay in new[] { false, true })
            {
                var empty = World.Create(195, 7); empty.Organisms.Clear(); empty.Time = 500;
                empty.MutationMode = MutationMode.Legacy;
                var runner = new SimulationRunner(empty, 0) { NoDisplay = noDisplay };
                try
                {
                    runner.Speed = 100;
                    await Until(async () => await runner.ReadTimeAsync() > 500.1);
                    runner.Speed = 0; await runner.ReadTimeAsync();
                    await runner.SaveAsync(path);
                    var disabled = World.Load(path);
                    Check(disabled.Organisms.Count == 0 && disabled.AutomaticRestarts == 0, "Disabled setting restarted an extinct population");
                    runner.RestartOnExtinction = true;
                    double paused = await runner.ReadTimeAsync(); await Task.Delay(60);
                    Check(await runner.ReadTimeAsync() == paused && runner.Status.AutomaticRestarts == 0, "Paused population restarted");
                    var oldView = runner.Snapshot;
                    runner.Speed = 100;
                    await Until(() => Task.FromResult(runner.Status.AutomaticRestarts == 1));
                    runner.Speed = 0; await runner.ReadTimeAsync();
                    Check(runner.Error == null && runner.Status.Population > 0 && runner.Status.Speed >= 0, "Restart failed or produced invalid throughput");
                    if (noDisplay) Check(ReferenceEquals(oldView, runner.Snapshot), "Display-off restart published a viewport");
                    await runner.SaveAsync(path);
                    var fresh = World.Load(path);
                    Check(fresh.PopulationCap == 7 && fresh.Organisms.Count <= 7, "Extinction restart lost the population cap");
                    Check(fresh.Time < 500 && fresh.Organisms.Count > 0 && fresh.AutomaticRestarts == 1
                        && fresh.RestartOnExtinction && fresh.MutationMode == MutationMode.Legacy, "Restart lost settings or failed to reset world");
                    Check(fresh.RandomState != disabled.RandomState && fresh.Events.Any(e => e.Contains("Restarted after extinction")), "Restart did not change seed or record an event");
                    runner.NoDisplay = false;
                    await Until(() => Task.FromResult(runner.Snapshot.AutomaticRestarts == 1));
                    Check(runner.Snapshot.Organisms.Count > 0 && runner.Snapshot.Biofilm.SequenceEqual(fresh.Biofilm), "Restored display retained old environment fields");
                    runner.RestartOnExtinction = false; await runner.SaveAsync(path);
                    Check(!World.Load(path).RestartOnExtinction, "Disabling restart was not saved");
                }
                finally { await runner.StopAsync(); }
            }

            // Last living organism dies during a tick, including after loading a previous restart.
            var dying = World.Create(63); dying.Organisms.RemoveRange(1, dying.Organisms.Count - 1);
            dying.Organisms[0].Energy = -1e6; dying.RestartOnExtinction = true; dying.AutomaticRestarts = 1;
            dying.Save(path);
            var resumed = new SimulationRunner(World.Load(path), 0) { NoDisplay = true };
            try
            {
                Check(resumed.RestartOnExtinction, "Loaded toggle was not restored");
                resumed.Speed = 100;
                await Until(() => Task.FromResult(resumed.Status.AutomaticRestarts == 2));
                resumed.Speed = 0; await resumed.ReadTimeAsync(); await resumed.SaveAsync(path);
                Check(World.Load(path).Organisms.Count > 0 && World.Load(path).AutomaticRestarts == 2, "Last death did not start a second ecosystem");
                Check(resumed.Error == null, "Restart worker failed");
            }
            finally { await resumed.StopAsync(); }
        }
        finally { File.Delete(path); }
        Console.WriteLine("PASS: extinction restart on/off, paused behavior, fresh seeds, repeated restarts, saved settings, no-display snapshots and restoration");
    }
}
