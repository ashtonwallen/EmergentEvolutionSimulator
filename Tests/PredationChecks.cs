using System.IO.Compression;
using System.Text.Json;
using Tideglass;

static class PredationChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static Organism Body(int id, double x, double energy = 3) => new()
    {
        Id = id, Age = 20, Energy = energy,
        Genes = new Genome { Cells = 1, Enzymes = 0, Photosynthesis = 0, Contract = 0, Membrane = .5, Buds = [] },
        Body = [new Cell { Position = new(x, 300, 120) }]
    };
    static World Lab(params Organism[] organisms)
    {
        var w = new World { Habitat = new LabHabitat(0), Organisms = organisms.ToList() };
        foreach (var o in organisms) { o.StepCenter = o.Center; w.CacheAnatomy(o); }
        return w;
    }
    static double Budget(World w) => w.Organisms.Sum(o => Math.Max(0, o.Energy) + TissueEconomy.Volume(o) * TissueEconomy.StoredEnergy)
        + w.Detritus.Sum(d => d.Energy) * TissueEconomy.FoodConversion;
    public static void Run()
    {
        var eater = Body(1, 300); var prey = Body(2, 315, .001); var world = Lab(eater, prey);
        var contact = new FeedingContact(); string genes = JsonSerializer.Serialize(prey.Genes);
        double initial = Budget(world), initialVolume = TissueEconomy.Volume(prey);
        double gain = world.FeedAt(eater, [prey], new(308, 300, 120), .5, 1, 1, 0, 0, contact, .5);
        Check(gain > .001 && prey.Body[0].Integrity < 1, "Nearly empty reserves make the physical body inedible");
        Check(Budget(world) < initial, "Tissue feeding creates energy");
        Check(Math.Abs(initialVolume - TissueEconomy.Volume(prey) - world.Feeding.TissueVolume) < 1e-10, "Bite credit differs from removed material");
        Check(genes == JsonSerializer.Serialize(prey.Genes), "A bite changes inherited anatomy");
        Check(world.Feeding.Bites == 1 && world.Feeding.Encounters == 1 && world.Feeding.DigestionCost > 0, "Feeding counters miss a paid bite");

        double low = World.TissueAccess(.05, .8, 1, 0), high = World.TissueAccess(.8, .8, 1, 0);
        Check(low > 0 && high > low && low < World.TissueAccess(.05, .1, 1, 0), "Armor is binary or higher investment has no benefit");
        Check(World.TissueAccess(.05, .8, .2, 0) > low && World.TissueAccess(.05, .8, 1, 1) > low, "Wounds/pressure do not improve access");
        Check(World.TissueAccess(0, 0, 0, 3) == 0, "Nondigestive tissue can eat animals");
        double before = Budget(world);
        Check(world.FeedAt(eater, [prey], new(308, 300, 200), .5, 1, 1, 0, 1, contact, .1) == 0 && Budget(world) == before,
            "Feeding reaches across depth or charges for a nonexistent contact");
        Check(contact.TargetId == -1 && contact.Seconds == 0, "A lost contact keeps its handling bonus");

        double Trial(int count, double handling)
        {
            var a = Body(1, 300); var neighbors = Enumerable.Range(0, count).Select(i => Body(i + 2, 315)).ToArray();
            var w = Lab([a, .. neighbors]); w.Time = 1;
            var c = new FeedingContact { TargetId = 2, At = .9, Seconds = handling };
            return w.FeedAt(a, neighbors, new(308, 300, 120), .5, 1, .8, 0, 0, c, .1);
        }
        Check(Math.Abs(Trial(1, 0) - Trial(20, 0)) < 1e-12, "Adding nearby prey multiplies one surface's bite budget");
        Check(Trial(1, 3) > Trial(1, 0), "Sustained contact does not improve handling");

        var branched = Body(2, 330, 10);
        branched.Genes.Buds = [new BudGene { Length = 4, Width = .3, Muscle = 0, Photo = 0, Digestion = 0 }, new BudGene { Parent = 0, Length = 2, Width = .2 }];
        branched.BudStates = [new BudState { Growth = 1, Integrity = .081 }, new BudState { Growth = 1 }];
        eater = Body(1, 300); world = Lab(eater, branched);
        var pose = branched.PhysicalBuds[0]; V shaft = BranchGeometry.Point(pose, .3);
        before = Budget(world);
        world.FeedAt(eater, [branched], shaft, .01, 100, 1, 1, 0, new(), .1);
        Check(branched.BudStates[0].Growth == 0 && world.Detritus.Count == 2, "Feeding did not sever exhausted tissue and its descendants");
        Check(Budget(world) <= before && branched.Body[0].Integrity == 1, "Severing duplicates consumed material or eats unrelated core");
        double afterSever = Budget(world);
        Check(world.FeedAt(eater, [branched], shaft, .01, 100, 1, 1, 0, new(), .1) == 0 && Budget(world) == afterSever,
            "Stale branch geometry can be eaten twice");

        var gripA = Body(1, 300); var gripB = Body(2, 315); world = Lab(gripA, gripB);
        gripA.Velocity = new(0, 0); gripB.Velocity = new(4, 0);
        V center = (gripA.Center + gripB.Center) * .5; double relative = (gripA.Velocity - gripB.Velocity).Length;
        world.ApplyFeedingGrip(gripA, gripB, 1, .8, .1);
        Check((gripA.Velocity - gripB.Velocity).Length < relative && (gripA.Velocity - gripB.Velocity).Length > 0,
            "Grip either fails to resist escape or locks prey unconditionally");
        Check(((gripA.Center + gripB.Center) * .5 - center).Length < 1e-10 && world.Feeding.GripCost > 0 && gripA.Energy < 3,
            "Grip lacks opposite reactions or paid work");

        // A true core bite, through the simulation call site, uses oriented surface contact.
        eater = Body(1, 300); eater.Genes.Enzymes = 1; prey = Body(2, 315); world = Lab(eater, prey);
        world.FeedCore(eater, [prey], .1);
        Check(eater.TissueEnergy > 0 && eater.CoreFeeding.TargetId == prey.Id, "Core feeding is disconnected from contact geometry");
        world.CacheAnatomy(prey); double intactCue = world.TissueCue(eater, prey.Center, [prey]);
        prey.Body[0].Integrity = .5; world.CacheAnatomy(prey);
        Check(world.TissueCue(eater, prey.Center, [prey]) > intactCue && world.TissueCue(eater, prey.Center + new V(0, 0, 100), [prey]) == 0,
            "Local wounds do not produce a stronger, distance-limited tissue cue");

        eater = Body(1, 300);
        eater.Genes.Buds = [new BudGene { Length = 4, Width = .3, Muscle = 0, Photo = 0, Digestion = .5, FoodResponse = 1 }];
        eater.BudStates = [new BudState { Growth = 1, FormedAt = 0 }];
        var branchPoses = new BudPose[1]; Branching.Poses(eater, eater.Age, branchPoses);
        prey = Body(2, 350); prey.Body[0].Position = branchPoses[0].Tip;
        world = Lab(eater, prey);
        Branching.Solve(world, eater, .1, [prey]);
        Check(eater.TissueEnergy > 0 && eater.BudStates[0].Feeding.TargetId == prey.Id && prey.Body[0].Integrity < 1,
            "Appendage solve does not route physical digestion through tissue feeding");
        var independent = eater.CopyForView(); independent.BudStates[0].Feeding.Seconds++;
        Check(independent.BudStates[0].Feeding.Seconds != eater.BudStates[0].Feeding.Seconds, "Appendage snapshots share contact duration");

        eater = Body(1, 300); world = Lab(eater); world.Tidal = true;
        world.Detritus.Add(new Remains { Position = eater.Center, Radius = 3, Energy = 4 });
        world.Step(.1); // Build the normal spatial food indexes.
        var remains = world.Detritus[0]; double materialBefore = remains.Energy;
        double reserveBefore = eater.Energy;
        double scavenged = world.Scavenge(eater, remains.Position, 3, 1, .1, 0, .1);
        Check(scavenged > 0 && eater.Energy > reserveBefore && remains.Energy < materialBefore, "Low digestion cannot benefit from scavenging");
        Check(scavenged < (materialBefore - remains.Energy) * TissueEconomy.FoodConversion && world.Feeding.ScavengedEnergy == scavenged,
            "Scavenging duplicates carcass energy or does not record it");
        Check(world.RemainsCue(remains.Position) > world.RemainsCue(remains.Position + new V(60, 0)), "Carcasses have no local sensory gradient");

        string folder = Path.Combine(Path.GetTempPath(), "tideglass-feeding-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            world.FeedingSince = 12; eater.CoreFeeding.TargetId = 88; eater.CoreFeeding.Seconds = 2; eater.CoreFeeding.At = world.Time;
            string path = Path.Combine(folder, "world.json"); world.Save(path); var restored = World.Load(path);
            Check(restored.FeedingSince == 12 && restored.Feeding.ScavengedEnergy == scavenged && restored.Organisms[0].CoreFeeding.Seconds == 2,
                "Save drops cumulative metrics or contact duration");
            var snapshot = world.Snapshot(); snapshot.Feeding.Bites++; snapshot.Organisms[0].Feeding.Bites++; snapshot.Organisms[0].CoreFeeding.Seconds++;
            Check(world.Feeding.Bites == 0 && eater.Feeding.Bites == 0 && eater.CoreFeeding.Seconds == 2, "Display snapshot shares mutable feeding state");
            using (var recorder = new ResearchRecorder(folder)) { recorder.Observe(world, true); recorder.End(world, "test"); }
            using var file = File.OpenRead(Directory.GetFiles(folder, "*.gz").Single());
            using var gzip = new GZipStream(file, CompressionMode.Decompress); using var reader = new StreamReader(gzip);
            using var header = JsonDocument.Parse(reader.ReadLine()!); using var sample = JsonDocument.Parse(reader.ReadLine()!);
            Check(header.RootElement.GetProperty("schema").GetInt32() == 2 && header.RootElement.GetProperty("physics").GetString() == "tissue-feeding-v3",
                "New recording cannot be separated from old physics");
            Check(sample.RootElement.GetProperty("Feeding").GetProperty("ScavengedEnergy").GetDouble() == scavenged
                && sample.RootElement.GetProperty("organisms")[0].GetProperty("Feeding").GetProperty("ScavengedEnergy").GetDouble() == scavenged,
                "Recording omits world or individual feeding metrics");
            world.Organisms.Clear(); Check(world.Feeding.ScavengedEnergy == scavenged, "Deaths erase world feeding totals");
        }
        finally { Directory.Delete(folder, true); }
        Console.WriteLine("PASS: conserved tissue bites/reserves/carcasses, gradual armor and wounds, bounded handling, paid grip/escape, scavenging/cues, feeding state and recording");
    }
}
