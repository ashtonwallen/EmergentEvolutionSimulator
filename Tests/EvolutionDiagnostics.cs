using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Tideglass;

static class EvolutionDiagnostics
{
    private static readonly PropertyInfo[] bodyFields = Scalars(typeof(Genome));
    private static readonly PropertyInfo[] patternFields = Scalars(typeof(GrowthRule));
    private static readonly PropertyInfo[] branchFields = Scalars(typeof(BudGene));
    private static readonly PropertyInfo[] wireFields = Scalars(typeof(SignalWire));
    private static PropertyInfo[] Scalars(Type type) => type.GetProperties()
        .Where(p => p.CanWrite && (p.PropertyType == typeof(double) || p.PropertyType == typeof(int))).ToArray();

    // Index-aligned comparison, not a homology or fitness measure. Structural mutations
    // and recombination can change the meaning of an index; report topology separately.
    public readonly record struct Difference(int ScalarEdits, double Magnitude, int TopologyEdits);
    public static Difference Compare(Genome a, Genome b)
    {
        int edits = 0, topology = Math.Abs(a.Cells - b.Cells) + Math.Abs(a.Pattern.Count - b.Pattern.Count);
        double magnitude = 0;
        void CompareFields(object x, object y, PropertyInfo[] fields)
        {
            foreach (var field in fields)
            {
                double oldValue = Convert.ToDouble(field.GetValue(x)), newValue = Convert.ToDouble(field.GetValue(y));
                if (oldValue == newValue) continue;
                edits++; magnitude += Math.Abs(newValue - oldValue) / (1 + Math.Abs(oldValue));
            }
        }
        CompareFields(a, b, bodyFields);
        for (int i = 0; i < Math.Min(a.Pattern.Count, b.Pattern.Count); i++) CompareFields(a.Pattern[i], b.Pattern[i], patternFields);
        var aa = a.Buds ?? []; var bb = b.Buds ?? [];
        topology += Math.Abs(aa.Count - bb.Count);
        for (int i = 0; i < Math.Min(aa.Count, bb.Count); i++)
        {
            CompareFields(aa[i], bb[i], branchFields);
            if (aa[i].Parent != bb[i].Parent || aa[i].AnchorCell != bb[i].AnchorCell) topology++;
            topology += Math.Abs(aa[i].Wires.Count - bb[i].Wires.Count);
            for (int j = 0; j < Math.Min(aa[i].Wires.Count, bb[i].Wires.Count); j++)
            {
                CompareFields(aa[i].Wires[j], bb[i].Wires[j], wireFields);
                if (aa[i].Wires[j].Source != bb[i].Wires[j].Source) topology++;
            }
        }
        return new(edits, magnitude, topology);
    }

    public static Genome Fixture(int branches) => new()
    {
        Cells = 3,
        Buds = Enumerable.Range(0, branches).Select(i => new BudGene
        {
            Parent = i % 4 == 0 ? -1 : i - 1, AnchorCell = i % 3, Length = 1, Width = .3,
            Wires = [new SignalWire { Source = i, Weight = .4, MemoryMix = .5 }]
        }).ToList()
    };

    public sealed record MutationSummary(int Branches, MutationMode Mode, int Samples, double MeanScalarEdits, double MeanMagnitude, double MeanTopologyEdits);
    public static MutationSummary SampleMutations(int branches, MutationMode mode, int samples = 400)
    {
        var parent = Fixture(branches); var random = new Random(1187);
        double edits = 0, magnitude = 0, topology = 0;
        for (int i = 0; i < samples; i++)
        {
            var child = parent.Offspring(random, null, mode);
            if (!Branching.Valid(child)) throw new Exception("Invalid offspring in mutation diagnostic");
            var d = Compare(parent, child); edits += d.ScalarEdits; magnitude += d.Magnitude; topology += d.TopologyEdits;
        }
        return new(branches, mode, samples, edits / samples, magnitude / samples, topology / samples);
    }

    private sealed class Totals
    {
        public double Path, RelativeWaterPath, MovementWork, Photo, Grazing, Shore, Tissue;
        public int BirthsObserved, SexualBirths, ScalarEdits, TopologyEdits;
        public double MutationMagnitude;
    }
    private readonly record struct Before(V Center, double Photo, double Grazing, double Shore, double Tissue);

    public static void Run(string[] args)
    {
        double seconds = args.Length > 1 ? double.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 300;
        if (!double.IsFinite(seconds) || seconds < .1 || seconds > 86400) throw new ArgumentException("Duration must be 0.1 to 86400 simulated seconds.");
        string output = Path.GetFullPath(args.Length > 2 ? args[2] : Path.Combine(Path.GetTempPath(), "tideglass-evolution-diagnostics.json"));
        if (string.Equals(output, Path.GetFullPath(World.SavePath), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Diagnostics cannot overwrite the ecosystem save.");
        int[] seeds = args.Length > 3 ? args[3].Split(',').Select(int.Parse).ToArray() : [149, 731, 2026];
        World.SimulationWorkers.MaxDegreeOfParallelism = 1;
        var mutations = new List<MutationSummary>();
        foreach (int count in new[] { 1, 10, 64 }) foreach (var mode in new[] { MutationMode.Legacy, MutationMode.Scaled })
            mutations.Add(SampleMutations(count, mode));
        var runs = new List<object>();
        foreach (int seed in seeds) foreach (var mode in new[] { MutationMode.Legacy, MutationMode.Scaled })
        {
            var world = World.Create(seed); world.MutationMode = mode;
            var totals = new Totals(); var samples = new List<object>();
            var previous = new Dictionary<int, Before>();
            var watch = Stopwatch.StartNew();
            samples.Add(Sample(world, totals));
            int steps = (int)Math.Ceiling(seconds / .1);
            for (int step = 0; step < steps; step++)
            {
                previous.Clear();
                foreach (var o in world.Organisms) previous[o.Id] = new(o.Center, o.LightEnergy, o.GrazingEnergy, o.ShoreEnergy, o.TissueEnergy);
                world.Step(.1);
                foreach (var o in world.Organisms)
                {
                    if (!double.IsFinite(o.Energy + o.Center.Length) || !Branching.Valid(o.Genes)) throw new Exception("Non-finite or invalid organism");
                    if (previous.TryGetValue(o.Id, out var old))
                    {
                        V travel = o.Center - old.Center;
                        totals.Path += travel.Length;
                        totals.RelativeWaterPath += (travel - world.FlowAt(old.Center) * .1).Length;
                        totals.MovementWork += o.LocomotionPower * .1;
                        totals.Photo += o.LightEnergy - old.Photo; totals.Grazing += o.GrazingEnergy - old.Grazing;
                        totals.Shore += o.ShoreEnergy - old.Shore; totals.Tissue += o.TissueEnergy - old.Tissue;
                    }
                    else if (o.ParentGenes is not null)
                    {
                        totals.BirthsObserved++; if (o.MateId > 0) totals.SexualBirths++;
                        var d = Compare(o.ParentGenes, o.Genes);
                        totals.ScalarEdits += d.ScalarEdits; totals.TopologyEdits += d.TopologyEdits; totals.MutationMagnitude += d.Magnitude;
                    }
                }
                if ((step + 1) % 300 == 0 || step + 1 == steps)
                {
                    samples.Add(Sample(world, totals));
                    Console.WriteLine($"EVOLUTION seed={seed} mode={mode} t={world.Time:F0} population={world.Organisms.Count} births={world.Births} generation={world.RecordGeneration}");
                }
            }
            watch.Stop();
            runs.Add(new { Seed = seed, Mode = mode.ToString(), SimulatedSeconds = world.Time, WallSeconds = watch.Elapsed.TotalSeconds, Samples = samples, Ablation = Ablate(world) });
            WriteReport();
        }
        void WriteReport()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output, JsonSerializer.Serialize(new
            {
                Schema = 1, StepSeconds = .1, Seeds = seeds,
                Notes = "Both modes use current contact physics. Scalar differences are index-aligned and include recombination; they are not homology or fitness measures. Path and intake totals exclude final ticks of organisms that died. Relative-water path is a proxy including settling and deformation; work includes core surface motors and branch actuation, not all metabolism. Ablation is a paired 10-second isolated-copy assay with one root subtree removed, without releasing food. No assay results affect selection.",
                Mutations = mutations, Runs = runs
            }, new JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }));
        }
        Console.WriteLine($"DIAGNOSTICS {output}");
    }

    private static object Sample(World w, Totals t)
    {
        double count = Math.Max(1, w.Organisms.Count), births = Math.Max(1, t.BirthsObserved);
        return new
        {
            w.Time, Population = w.Organisms.Count, w.Births, w.Deaths, Generation = w.RecordGeneration,
            MeanBodyCells = w.Organisms.Sum(o => o.Body.Count) / count,
            MeanBranchInstructions = w.Organisms.Sum(o => o.Genes.Buds?.Count ?? 0) / count,
            MeanDevelopedBranches = w.Organisms.Sum(o => o.BudStates.Count(b => b.Growth > .5 && b.Integrity > .3)) / count,
            MeanLifetimeOffspringOfSurvivors = w.Organisms.Sum(o => o.Children) / count,
            GroundContacting = w.Organisms.Count(o => o.GroundContacts > 0),
            t.BirthsObserved, t.SexualBirths,
            MeanBirthScalarDifference = t.ScalarEdits / births, MeanBirthMagnitude = t.MutationMagnitude / births, MeanBirthTopologyDifference = t.TopologyEdits / births,
            t.Path, t.RelativeWaterPath, t.MovementWork,
            MovementWorkPerRelativeWaterDistance = t.RelativeWaterPath > 1e-9 ? (double?)(t.MovementWork / t.RelativeWaterPath) : null,
            t.Photo, t.Grazing, t.Shore, t.Tissue
        };
    }

    public static int RemoveSubtree(Organism o, int root)
    {
        var buds = o.Genes.Buds!; var remove = new bool[buds.Count]; remove[root] = true;
        for (int i = root + 1; i < buds.Count; i++) remove[i] = buds[i].Parent >= 0 && remove[buds[i].Parent];
        int count = 0;
        for (int i = buds.Count - 1; i >= 0; i--) if (remove[i])
        {
            Branching.RemoveBud(o.Genes, i); o.BudStates.RemoveAt(i); count++;
        }
        o.PhysicalBuds = []; return count;
    }

    private static object? Ablate(World source)
    {
        var candidate = source.Organisms.Where(o => o.Genes.Buds is not null && o.Genes.Buds
            .Where((b, i) => b.Parent < 0 && i < o.BudStates.Count && o.BudStates[i].Growth > .5 && o.BudStates[i].Integrity > .5).Any())
            .OrderByDescending(o => Branching.Extent(o)).ThenBy(o => o.Id).FirstOrDefault();
        if (candidate is null) return null;
        int root = candidate.Genes.Buds!.FindIndex(b => b.Parent < 0 && candidate.BudStates[candidate.Genes.Buds.IndexOf(b)].Growth > .5
            && candidate.BudStates[candidate.Genes.Buds.IndexOf(b)].Integrity > .5);
        string copy = JsonSerializer.Serialize(source);
        object Trial(bool remove)
        {
            var w = JsonSerializer.Deserialize<World>(copy)!;
            w.Organisms.RemoveAll(o => o.Id != candidate.Id);
            var o = w.Organisms[0]; o.LastBirth = w.Time;
            int removed = remove ? RemoveSubtree(o, root) : 0;
            double energy = o.Energy, intake = o.LightEnergy + o.GrazingEnergy + o.ShoreEnergy + o.TissueEnergy;
            double work = 0, path = 0; V start = o.Center;
            for (int i = 0; i < 100 && w.Organisms.Count > 0; i++)
            {
                V old = o.Center; w.Step(.1); path += (o.Center - old).Length; work += o.LocomotionPower * .1;
            }
            return new { RemovedBranches = removed, Survived = w.Organisms.Count > 0, ElapsedSeconds = w.Time - source.Time,
                NetDisplacement = (o.Center - start).Length, Path = path, AppendageWork = work, EnergyChange = o.Energy - energy,
                Intake = o.LightEnergy + o.GrazingEnergy + o.ShoreEnergy + o.TissueEnergy - intake };
        }
        return new { OrganismId = candidate.Id, Root = root, Intact = Trial(false), Removed = Trial(true) };
    }
}
