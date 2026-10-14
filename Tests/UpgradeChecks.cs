using System.Text.Json;
using System.IO.Compression;
using Tideglass;

static class UpgradeChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static string[] RecordedLines(string path)
    {
        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }
    public static async Task Run()
    {
        var o = new Organism { Genes = new Genome { Cells = 1, Buds = [new BudGene { Length = 4, Width = .5, Maturation = 10 }] },
            Energy = 100, Age = 5, Body = [new Cell { Position = new(300, 300, 120) }], BudStates = [new BudState { FormedAt = 0 }] };
        double capacity = o.Capacity; o.Genes.Cells = 30;
        Check(o.Capacity == capacity, "Unbuilt tissue supplies storage"); o.Genes.Cells = 1;
        Check(Math.Abs(TissueEconomy.CoreVolume(o) - 1) < 1e-10, "Reference volume changed");
        o.Genes.Radius = 12; Check(Math.Abs(TissueEconomy.CoreVolume(o) - 8) < 1e-10, "Volume is not cubic in radius"); o.Genes.Radius = 6;
        double full = TissueEconomy.BranchVolume(o, 0, 1);
        var gene = o.Genes.Buds![0];
        double frustumVolume = Math.PI * 216 * gene.Length * gene.Width * gene.Width * gene.Flatness
            * (1.05 * 1.05 + 1.05 + 2 + gene.Taper + gene.Taper * gene.Taper) / 6;
        double referenceVolume = 4 * Math.PI / 3 * 216 * 1.3 * .95 * .72;
        Check(Math.Abs(full - frustumVolume / referenceVolume) < 1e-10, "Core and branch volume units disagree");
        Check(Math.Abs(TissueEconomy.BranchVolume(o, 0, .5) / full - .125) < 1e-10, "Branch growth is not cubic");
        double before = TissueEconomy.Volume(o), energy = o.Energy;
        Branching.Develop(o, 1);
        Check(Math.Abs(energy - o.Energy - (TissueEconomy.Volume(o) - before) * TissueEconomy.BuildCost) < 1e-9, "Growth did not pay for tissue");
        var w = new World { Habitat = new LabHabitat(0) }; w.CacheAnatomy(o);
        double branch = TissueEconomy.BranchVolume(o, 0, o.BudStates[0].Growth);
        w.Sever(o, 0); double remains = w.Detritus.Sum(d => d.Energy);
        Check(Math.Abs(remains * TissueEconomy.FoodConversion - branch * TissueEconomy.StoredEnergy) < 1e-9, "Severing energy disagrees with construction");
        w.Sever(o, 0); Check(w.Detritus.Sum(d => d.Energy) == remains, "Repeated sever creates energy");
        Check(TissueEconomy.StoredEnergy < TissueEconomy.BuildCost, "Recycling can fund perpetual growth");

        o.Genes.Buds = []; o.BudStates.Clear(); o.Genes.Contract = 0; o.ThermalEfficiency = 1;
        V flow = w.FlowAt(o.Center); double heading = o.Heading, pitch = o.Pitch;
        V movement = BodyMechanics.Solve(w, o, new V(0, 10, 10), .1);
        Check((movement - flow).Length < 1e-10 && o.Heading == heading && o.Pitch == pitch && o.LocomotionPower == 0, "Inactive cortex steers or propels");
        o.Genes.Contract = .8;
        movement = BodyMechanics.Solve(w, o, new V(0, 1, 0), .1);
        Check((movement - flow).Length > .01 && o.Heading > heading && o.LocomotionPower > 0, "Active forces lack propulsion/torque/work");
        var tapered = new BudPose(new(0,0,100), new(10,0,100), new(20,0,100), 3, .2, 0, true, .1);
        Check(BranchGeometry.Gap(tapered, new(19,2,100)) > 0 && BranchGeometry.Gap(tapered, new(10,2,100)) < 0, "Taper does not affect collision");
        Check(BranchGeometry.Gap(tapered, new(10,0,101)) > 0, "Flatness does not affect collision");
        // Discovery must include appendages reaching beyond the former 65-unit center cutoff.
        var longBody = new Organism { Id = 1, Genes = new Genome { Cells = 1, Buds = [new BudGene { Length = 20, Muscle = 0, Width = .1 }] },
            Body = [new Cell { Position = new(300,300,120) }], BudStates = [new BudState { Growth = 1 }] };
        var neighbor = new Organism { Id = 2, Body = [new Cell { Position = new(430,300,120) }] };
        w.Organisms = [longBody, neighbor]; w.Step(.1);
        Check(longBody.StepNeighbors.Contains(neighbor) && neighbor.StepNeighbors.Contains(longBody), "Long tissue lost its neighbors");

        string folder = Path.Combine(Path.GetTempPath(), "tideglass-recording-" + Guid.NewGuid().ToString("N"));
        try
        {
            var a = World.Create(125); var b = World.Create(125);
            using (var recorder = new ResearchRecorder(folder))
            {
                recorder.Observe(a, false); Check(!Directory.Exists(folder), "Disabled recorder writes files");
                recorder.Observe(a, true);
                for (int i = 0; i < 310; i++) { a.Step(.1); b.Step(.1); recorder.Observe(a, true); }
                recorder.End(a, "test");
                string? id = a.RunId; a.RunId = null;
                Check(JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b), "Observing changes simulation evolution"); a.RunId = id;
                string recording = Directory.GetFiles(folder, "*.jsonl.gz").Single();
                string[] lines = RecordedLines(recording);
                Check(lines.Length == 4 && JsonDocument.Parse(lines[1]).RootElement.GetProperty("organisms").GetArrayLength() == 100, "Recorder cadence or samples invalid");
                Check(new FileInfo(recording).Length < lines.Sum(line => line.Length) / 2, "Recordings are not effectively compressed");
            }
            string rotationFolder = Path.Combine(folder, "rotation");
            var rotationWorld = World.Create(522);
            using (var recorder = new ResearchRecorder(rotationFolder, 128 * 1024))
            {
                for (int sample = 0; sample < 1000; sample++)
                {
                    rotationWorld.Time = sample * 30; recorder.Observe(rotationWorld, true);
                    Check(recorder.Error == null, "Compressed recording failed during rotation");
                    if (sample % 25 == 0 && Directory.GetFiles(rotationFolder).Length > 1) break;
                }
                recorder.End(rotationWorld, "test");
            }
            var segments = Directory.GetFiles(rotationFolder, "*.jsonl.gz");
            Check(segments.Length > 1 && segments.SelectMany(RecordedLines).Any(line => line.Contains("\"reason\":\"rotation\"")),
                "Compressed segments did not rotate or finish with readable gzip trailers");
            // A single sample larger than the segment limit used to rotate on every
            // Observe call, even while paused. Exercise the actual failing case.
            string oversizedFolder = Path.Combine(folder, "oversized");
            using (var recorder = new ResearchRecorder(oversizedFolder, 1))
            {
                rotationWorld.Time = 0; recorder.Observe(rotationWorld, true);
                for (int i = 0; i < 100; i++) recorder.Observe(rotationWorld, true);
                rotationWorld.Time = 29.9; recorder.Observe(rotationWorld, true);
                Check(Directory.GetFiles(oversizedFolder).Length == 1, "Rotation bypassed cadence while paused or before 30 seconds");
                rotationWorld.Time = 30; recorder.Observe(rotationWorld, true);
                Check(Directory.GetFiles(oversizedFolder).Length == 2, "Recording failed to continue after rotation");
            }
            var empty = World.Create(3); empty.Organisms.Clear(); empty.CollectData = true; empty.RestartOnExtinction = true;
            var runner = new SimulationRunner(empty, 0, folder) { NoDisplay = true };
            try
            {
                runner.Speed = 100;
                for (int i = 0; i < 150 && runner.Status.AutomaticRestarts == 0; i++) await Task.Delay(20);
                runner.Speed = 0; await runner.ReadTimeAsync();
                Check(runner.Status.AutomaticRestarts == 1 && runner.CollectData && runner.DataError == null, "Recorder failed across extinction");
                string save = Path.Combine(folder, "save.json"); await runner.SaveAsync(save);
                Check(World.Load(save).CollectData, "Recording preference not saved");
            }
            finally { await runner.StopAsync(); }
            Check(Directory.GetFiles(folder, "*.jsonl.gz").SelectMany(RecordedLines).Any(l => l.Contains("\"reason\":\"extinction\"")), "Extinction not recorded");
            string blocked = Path.Combine(folder, "blocked"); File.WriteAllText(blocked, "file");
            using var broken = new ResearchRecorder(blocked); broken.Observe(a, true);
            Check(broken.Error != null, "Recording failure not surfaced");
            broken.Observe(a, false); Check(broken.Error == null, "Cannot reset recorder error");
            using var lowDisk = new ResearchRecorder(Path.Combine(folder, "low-disk"), 1024, () => 1024);
            lowDisk.Observe(a, true); Check(lowDisk.Error?.Contains("2 GB") == true, "Recorder did not reserve space for saves");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        Console.WriteLine("PASS: tissue ledger, actual storage, cubic growth, recycling, powered cortex/steering, tapered contact, long-body discovery, passive recording, restart persistence and disk failure");
    }
}
