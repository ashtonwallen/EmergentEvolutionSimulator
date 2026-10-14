using Tideglass;

static class PopulationCapChecks
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static World Fertile(int cap)
    {
        var world = new World { PopulationCap = cap, Time = 100, NextId = 5, Habitat = new LabHabitat(0) };
        for (int i = 0; i < 4; i++)
        {
            var o = new Organism { Id = i + 1, Age = 100,
                Genes = new Genome { Cells = 1, Radius = 5, Contract = 0, Photosynthesis = 0,
                    Pattern = [new GrowthRule { Size = 1 }], Buds = [] },
                Body = [new Cell { Position = new(300 + i * 200, 300, 120) }] };
            o.Energy = o.Capacity;
            world.Organisms.Add(o);
        }
        return world;
    }
    public static async Task Run()
    {
        var unlimited = Fertile(0); unlimited.Step(.1);
        Check(unlimited.Births == 4 && unlimited.Organisms.Count == 8, "Unlimited reproduction did not exercise all parents");
        var capped = Fertile(5); capped.Step(.1);
        Check(capped.Births == 1 && capped.Organisms.Count == 5, "Simultaneous births overshot cap");
        var lowered = Fertile(2); uint random = lowered.RandomState; lowered.Step(.1);
        Check(lowered.Organisms.Count == 4 && lowered.Births == 0 && lowered.Deaths == 0, "Lowering cap killed organisms or allowed births");
        Check(lowered.NextId == 5 && lowered.RandomState == random && lowered.Organisms.All(o => o.Children == 0 && o.LastBirth == 0), "Blocked births mutated or updated parents");
        lowered.PopulationCap = 5; lowered.Step(.1);
        Check(lowered.Births == 1 && lowered.Organisms.Count == 5, "Raising cap did not resume births");
        var refill = Fertile(4); refill.Organisms[3].Energy = -1e6; refill.Step(.1);
        Check(refill.Organisms.Count == 3 && refill.Deaths == 1, "Natural death failed at cap");
        refill.Step(.1);
        Check(refill.Organisms.Count == 4 && refill.Births == 1, "Death did not free a birth slot");
        Check(World.Create(731, 7).Organisms.Count == 7 && World.Create(731, 0).Organisms.Count == 100, "Founder count ignores cap");
        Check(capped.Snapshot().PopulationCap == 5, "Snapshot lost cap");
        string path = Path.Combine(Path.GetTempPath(), "tideglass-cap-" + Guid.NewGuid() + ".json");
        try
        {
            capped.Save(path);
            Check(World.Load(path).PopulationCap == 5, "Save/load lost cap");
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"PopulationCap\":5,", ""));
            Check(World.Load(path).PopulationCap == World.DefaultPopulationCap, "Older save has no safe default cap");
            var runner = new SimulationRunner(capped, 0);
            try
            {
                runner.PopulationCap = 7; await runner.SaveAsync(path);
                Check(World.Load(path).PopulationCap == 7, "Paused setting was not saved");
                await runner.NewAsync(196, path);
                Check(runner.Snapshot.PopulationCap == 7 && runner.Snapshot.Organisms.Count == 7, "New run lost cap");
                runner.PopulationCap = 0; await runner.NewAsync(197, path);
                Check(runner.Snapshot.PopulationCap == 0 && runner.Snapshot.Organisms.Count == 100, "Unlimited new run is capped");
            }
            finally { await runner.StopAsync(); }
        }
        finally { File.Delete(path); }
        Console.WriteLine("PASS: birth-slot reservation, cap lowered/raised, natural turnover, unlimited, founders, snapshots, old saves, persistence and new runs");
    }
}
