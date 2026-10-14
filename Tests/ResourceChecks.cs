using Tideglass;

static class ResourceChecks
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static async Task Run()
    {
        Organism Tiny(int cells = 1) => new() { Age = 100, Genes = new Genome { Cells = cells, Radius = 3.5,
            Contract = 0, Photosynthesis = 0, Pattern = [new GrowthRule { Size = .45 }],
            Buds = [new BudGene { Length = 1, Width = .3, Maturation = 10, Muscle = 0 }] },
            Body = [new Cell { Position = new(300, 300, 120) }], BudStates = [new BudState { FormedAt = 0 }] };
        var tiny = Tiny(); tiny.Energy = tiny.Capacity * .9;
        Check(tiny.Capacity < 1, "Tiny tissue still has an artificial storage floor");
        double energy = tiny.Energy, tissue = TissueEconomy.Volume(tiny);
        Branching.Develop(tiny, 1);
        Check(tiny.BudStates[0].Growth > .01, "A small organism cannot grow below eight energy");
        Check(Math.Abs(energy - tiny.Energy - TissueEconomy.BuildCost * (TissueEconomy.Volume(tiny) - tissue)) < 1e-9, "Small growth creates free material");
        var multi = Tiny(2); multi.Energy = multi.Capacity * .9;
        var growthWorld = new World { Habitat = new LabHabitat(0), Organisms = [multi] }; growthWorld.Step(.1);
        Check(multi.Body.Count == 2, "A small organism cannot afford a second cell");
        var g = new Genome(); double largeArea = TissueEconomy.CoreArea(g, 0); g.Radius *= .5;
        Check(Math.Abs(TissueEconomy.CoreArea(g, 0) / largeArea - .25) < 1e-12, "Exchange area is independent of physical size");

        var light = new World { Habitat = new LabHabitat(1) }; V p = new(300, 300, 100);
        double first = light.Photosynthesize(p, .1, .1), crowded = first;
        for (int i = 1; i < 100; i++) crowded += light.Photosynthesize(p, .1, .1);
        double limit = .1 * .6 * World.CellSize * World.CellSize / (Math.PI * 36);
        Check(Math.Abs(crowded - limit) < 1e-10 && crowded / 100 < first / 10, "A crowded column has unlimited per-capita light");
        Check(light.Photosynthesize(p + new V(0, 0, 100), 1, .1) == 0, "Depth layers duplicate incident light");
        Check(light.Photosynthesize(p + new V(100, 0), .1, .1) > 0, "Local crowding penalizes unrelated habitat");
        light.Time += .1; Check(light.Photosynthesize(p, .1, .1) > 0, "Light budget never renews");
        Array.Clear(light.Carbon); Check(light.Photosynthesize(p, 1, .1) == 0, "Photosynthesis bypasses carbon depletion");

        var clean = new World { Habitat = new LabHabitat(0) }; var waste = new World { Habitat = new LabHabitat(0) };
        Array.Fill(waste.OrganicMatter, 2); clean.Step(.5); waste.Step(.5);
        Check(waste.Oxygen.Sum() < clean.Oxygen.Sum() && waste.Carbon.Sum() > clean.Carbon.Sum(), "Waste decomposition has no oxygen consequence");

        var a = Tiny(); var b = Tiny(); a.Id = 1; b.Id = 2;
        a.Genes.Buds = []; b.Genes.Buds = []; a.BudStates.Clear(); b.BudStates.Clear();
        a.Energy = a.Capacity; b.Energy = b.Capacity;
        var mating = new World { Time = 100, NextId = 3, Habitat = new LabHabitat(0), Organisms = [a, b] };
        double beforeEnergy = a.Energy + b.Energy, beforeVolume = TissueEconomy.Volume(a) + TissueEconomy.Volume(b);
        mating.Step(.1);
        Check(mating.Exchanges > 0, "Small-mate regression never exercised reproduction");
        Check(mating.Organisms.Sum(o => o.Energy) + TissueEconomy.BuildCost * (mating.Organisms.Sum(TissueEconomy.Volume) - beforeVolume)
            <= beforeEnergy + 1e-9, "Mating creates energy from a low-energy parent");

        var runner = new SimulationRunner(World.Create(25), 0);
        try
        {
            await Task.Delay(150); var snapshot = runner.Snapshot;
            await Task.Delay(200); Check(ReferenceEquals(snapshot, runner.Snapshot), "Paused world keeps allocating population snapshots");
            runner.Speed = 1; await Task.Delay(250);
            Check(!ReferenceEquals(snapshot, runner.Snapshot), "Snapshot publishing did not resume");
        }
        finally { await runner.StopAsync(); }
        Console.WriteLine("PASS: affordable small-body growth, area-scaled intake, finite shared light, carbon limits, waste oxygen demand, conserved mating energy, idle paused snapshots");
    }
}
