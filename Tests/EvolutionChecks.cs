using System.Text.Json;
using Tideglass;

static class EvolutionChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Run()
    {
        var parent = EvolutionDiagnostics.Fixture(64);
        string original = JsonSerializer.Serialize(parent);
        var scaled = EvolutionDiagnostics.SampleMutations(64, MutationMode.Scaled, 400);
        var legacy = EvolutionDiagnostics.SampleMutations(64, MutationMode.Legacy, 400);
        Check(scaled.MeanScalarEdits < legacy.MeanScalarEdits * .5, "Large genomes remain excessively disrupted");
        Check(scaled.MeanTopologyEdits > 0 && scaled.MeanMagnitude > 0, "Mutation scaling froze exploration");
        foreach (var mode in new[] { MutationMode.Legacy, MutationMode.Scaled })
        {
            var a = new Random(42); var b = new Random(42);
            for (int i = 0; i < 40; i++)
            {
                var child = parent.Offspring(a, null, mode);
                Check(JsonSerializer.Serialize(child) == JsonSerializer.Serialize(parent.Offspring(b, null, mode)), "Mutation is not deterministic");
                Check(Branching.Valid(child), "Mutation broke topology");
            }
        }
        Check(JsonSerializer.Serialize(parent) == original, "Offspring mutation changed parent");
        // Isolate scalar mutation from insertions/removals when checking size scaling.
        Check(Mutation.Scale(EvolutionDiagnostics.Fixture(64), MutationMode.Scaled)
            < Mutation.Scale(EvolutionDiagnostics.Fixture(1), MutationMode.Scaled), "Mutation budget does not account for genome size");

        var world = new World();
        var pose = new BudPose(new(200, 200, 100), new(240, 200, 100), new(280, 200, 100), 1, 1, 0, true);
        var rock = new Rock { Position = new(220, 201, 100), Radius = 3, Food = 1 };
        world.Rocks.Add(rock);
        Check(!world.SurfaceContact(pose.Tip, 1, out _, out _, out _)
            && !world.SurfaceContact(pose.Root, 1, out _, out _, out _)
            && !world.SurfaceContact(pose.Joint, 1, out _, out _, out _), "Rock fixture endpoints are not clear");
        Check(world.BranchSurfaceContact(pose, out var contact) && contact.Rock == rock
            && Math.Abs(contact.Fraction - .25) < 1e-9 && contact.Normal.Y < -.99, "Shaft missed rock between all endpoints");
        Check((BranchGeometry.Point(pose, contact.Fraction) - contact.Point).Length < 1e-9, "Contact velocity would sample wrong point");
        var clear = new World();
        Check(!clear.BranchSurfaceContact(pose, out _), "Suspended shaft has phantom contact");
        var curved = pose with { Root = new(200, 200, 10), Joint = new(240, 200, .5), Tip = new(280, 200, 10) };
        Check(clear.BranchSurfaceContact(curved, out var floor) && floor.Rock is null && floor.Fraction == .5, "Curved shaft missed ground");
        var longWorld = new World();
        longWorld.Rocks.Add(new Rock { Position = new(300, 201, 100), Radius = 2 });
        var longPose = pose with { Joint = new(500, 200, 100), Tip = new(800, 200, 100) };
        Check(longWorld.BranchSurfaceContact(longPose, out _), "Long shaft missed rock outside midpoint spatial margin");
        Check(!world.BranchSurfaceContact(pose with { Active = false }, out _), "Inactive tissue contacted ground");
        Check(double.IsFinite(BranchGeometry.DistanceSquared(pose with { Joint = pose.Root }, pose.Root)), "Degenerate segment is non-finite");

        var specimen = new Organism
        {
            Genes = new Genome { Cells = 1, Buds = [new BudGene { Length = 8, Width = .2, Muscle = 0, Photo = 0, Digestion = 0, Stiffness = 1 }] },
            Age = 100, Energy = 40, Body = [new Cell { Position = new(300, 300, 120) }], BudStates = [new BudState { Growth = 1, FormedAt = 0 }]
        };
        var shaft = new BudPose[1]; Branching.Poses(specimen, specimen.Age, shaft);
        V shaftPoint = BranchGeometry.Point(shaft[0], .3);
        var lab = new World(); lab.Rocks.Add(new Rock { Position = shaftPoint + new V(0, 1, 0), Radius = 2, Food = 1 });
        V reaction = Branching.Solve(lab, specimen, .1, []);
        Check(specimen.GroundContacts == 1 && reaction.Y < 0 && specimen.ShoreEnergy > 0, "Shaft contact did not produce support and local feeding");
        Check(specimen.LocomotionPower == 0, "Passive shaft was charged for active mechanical work");
        lab.CacheAnatomy(specimen);
        Check(lab.TissueContact(specimen, shaftPoint, .1), "Shaft cannot receive tissue contact");
        double coreIntegrity = specimen.Body[0].Integrity;
        lab.DamageTissue(specimen, shaftPoint, 1);
        Check(specimen.BudStates[0].Integrity < 1 && specimen.Body[0].Integrity == coreIntegrity, "Shaft damage hit the core");

        var assay = new Organism { Genes = EvolutionDiagnostics.Fixture(10), BudStates = Enumerable.Range(0, 10).Select(_ => new BudState { Growth = 1 }).ToList() };
        Check(EvolutionDiagnostics.RemoveSubtree(assay, 4) == 4 && assay.Genes.Buds!.Count == 6
            && assay.BudStates.Count == 6 && Branching.Valid(assay.Genes), "Ablation removed unrelated branches or corrupted wiring");

        string path = Path.Combine(Path.GetTempPath(), "tideglass-mutation-" + Guid.NewGuid() + ".json");
        try
        {
            var saved = World.Create(47); saved.MutationMode = MutationMode.Legacy;
            saved.Save(path); var loaded = World.Load(path);
            Check(loaded.MutationMode == MutationMode.Legacy && loaded.Snapshot().MutationMode == MutationMode.Legacy, "Mutation mode lost in persistence/snapshot");
            for (int i = 0; i < 20; i++) { saved.Step(.1); loaded.Step(.1); }
            Check(JsonSerializer.Serialize(saved) == JsonSerializer.Serialize(loaded), "Legacy-mode continuation changed after resume");
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!; json.AsObject().Remove("MutationMode");
            File.WriteAllText(path, json.ToJsonString());
            Check(World.Load(path).MutationMode == MutationMode.Scaled, "Existing saves do not get scaled offspring mutation");
        }
        finally { File.Delete(path); }
        Console.WriteLine($"PASS: scaled mutation ({scaled.MeanScalarEdits:F1} vs {legacy.MeanScalarEdits:F1} mean indexed edits), immutable parents, shaft contact/forces/feeding/damage, ablation, mode persistence");
    }
}
