using System.Text.Json;
using System.IO;

namespace Tideglass;

public struct V(double x, double y, double z = 0)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
    public double Z { get; set; } = z;
    public readonly double LengthSquared => X * X + Y * Y + Z * Z;
    public readonly double Length => Math.Sqrt(LengthSquared);
    public static V operator +(V a, V b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static V operator -(V a, V b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static V operator *(V a, double b) => new(a.X * b, a.Y * b, a.Z * b);
    public static V Direction(double heading, double pitch) => new(Math.Cos(heading) * Math.Cos(pitch), Math.Sin(heading) * Math.Cos(pitch), Math.Sin(pitch));
}

public sealed class GrowthRule
{
    public double Turn { get; set; }
    public double Elevation { get; set; }
    public int ParentBack { get; set; } = 1;
    public double Size { get; set; } = 1;
    public double Phase { get; set; } = .85;
    public double EnzymeBias { get; set; }
    public double PhotoBias { get; set; }
    public double Outgrowth { get; set; } = .06;
    public double Stiffness { get; set; } = .22;
    public double Bend { get; set; } = .55;
    public double Paddle { get; set; } = .25;
    public GrowthRule Copy() => (GrowthRule)MemberwiseClone();
}

// A developmental recipe, not a catalogue of body parts. Each value is heritable.
public sealed class Genome
{
    public int Cells { get; set; } = 3;
    public double Radius { get; set; } = 6;
    public double Branch { get; set; } = .35;
    public double Turn { get; set; } = 1.5;
    public double Adhesion { get; set; } = .5;
    public double Contract { get; set; } = .4;
    public double Rhythm { get; set; } = 1;
    public double Sense { get; set; } = .4;
    public double Hue { get; set; } = .45;
    public double Enzymes { get; set; } = .04;
    public double Membrane { get; set; } = .2;
    public double Differentiation { get; set; } = .15;
    public double Photosynthesis { get; set; } = .1;
    public double ThermalOptimum { get; set; } = 23;
    public double KinAffinity { get; set; }
    public double TissueSense { get; set; }
    public double LightSense { get; set; }
    public double MoistureSense { get; set; }
    public double Elongation { get; set; } = 1.3;
    public double Flattening { get; set; } = .72;
    public double Taper { get; set; } = .25;
    public double EdgeGrowth { get; set; } = .04;
    public double SurfaceAdhesion { get; set; } = .08;
    public double Waterproofing { get; set; } = .08;
    public double Density { get; set; } = .52;
    public List<GrowthRule> Pattern { get; set; } = [new()];
    public List<BudGene>? Buds { get; set; }
    public GrowthRule Rule(int cell) => Pattern[cell % Pattern.Count];
    public double CellRadius(int cell) => Radius * Rule(cell).Size;
    public Genome Copy() { var g = (Genome)MemberwiseClone(); g.Pattern = Pattern.Select(r => r.Copy()).ToList(); g.Buds = Buds?.Select(b => b.Copy()).ToList(); return g; }
    public Genome Offspring(Random r, Genome? mate, MutationMode mutationMode = MutationMode.Scaled)
    {
        var g = Copy();
        if (mate != null)
        {
            if (r.NextDouble() < .5) g.Cells = mate.Cells;
            if (r.NextDouble() < .5) g.Branch = mate.Branch;
            if (r.NextDouble() < .5) g.Turn = mate.Turn;
            if (r.NextDouble() < .5) g.Contract = mate.Contract;
            if (r.NextDouble() < .5) g.Sense = mate.Sense;
            if (r.NextDouble() < .5) g.Radius = mate.Radius;
            if (r.NextDouble() < .5) g.Rhythm = mate.Rhythm;
            if (r.NextDouble() < .5) g.Adhesion = mate.Adhesion;
            if (r.NextDouble() < .5) g.Enzymes = mate.Enzymes;
            if (r.NextDouble() < .5) g.Membrane = mate.Membrane;
            if (r.NextDouble() < .5) g.Differentiation = mate.Differentiation;
            if (r.NextDouble() < .5) g.Photosynthesis = mate.Photosynthesis;
            if (r.NextDouble() < .5) g.ThermalOptimum = mate.ThermalOptimum;
            if (r.NextDouble() < .5) g.KinAffinity = mate.KinAffinity;
            if (r.NextDouble() < .5) g.TissueSense = mate.TissueSense;
            if (r.NextDouble() < .5) g.LightSense = mate.LightSense;
            if (r.NextDouble() < .5) g.MoistureSense = mate.MoistureSense;
            if (r.NextDouble() < .5) g.Elongation = mate.Elongation;
            if (r.NextDouble() < .5) g.Flattening = mate.Flattening;
            if (r.NextDouble() < .5) g.Taper = mate.Taper;
            if (r.NextDouble() < .5) g.EdgeGrowth = mate.EdgeGrowth;
            if (r.NextDouble() < .5) g.SurfaceAdhesion = mate.SurfaceAdhesion;
            if (r.NextDouble() < .5) g.Waterproofing = mate.Waterproofing;
            if (r.NextDouble() < .5) g.Density = mate.Density;
            if (r.NextDouble() < .5) g.Pattern = mate.Pattern.Select(p => p.Copy()).ToList();
            if (r.NextDouble() < .5) g.Buds = mate.Buds?.Select(b => b.Copy()).ToList() ?? Branching.FromLegacy(mate);
        }
        g.Buds ??= Branching.FromLegacy(g);
        double mutationScale = Mutation.Scale(g, mutationMode);
        double Mut(double n, double step, double lo, double hi) => Mutation.Adjust(r, n, step, lo, hi, .3 * mutationScale, mutationMode);
        if (r.NextDouble() < .2) g.Cells = Math.Clamp(g.Cells + (r.NextDouble() < .5 ? -1 : 1), 1, Development.NodeBudget);
        g.Cells = Math.Min(g.Cells, Development.NodeBudget - g.Buds.Count);
        g.Radius = Mut(g.Radius, 1, 3.5, 11);
        g.Branch = Mut(g.Branch, .2, 0, 1);
        g.Turn = Mut(g.Turn, .45, -.8, 3);
        g.Adhesion = Mut(g.Adhesion, .15, .05, 1);
        g.Contract = Mut(g.Contract, .16, .02, 1);
        g.Rhythm = Mut(g.Rhythm, .25, .2, 2.5);
        g.Sense = Mut(g.Sense, .16, 0, 1);
        g.Enzymes = Mut(g.Enzymes, .12, 0, 1);
        g.Membrane = Mut(g.Membrane, .12, 0, 1);
        g.Differentiation = Mut(g.Differentiation, .15, 0, 1);
        g.Photosynthesis = Mut(g.Photosynthesis, .15, 0, 1);
        g.ThermalOptimum = Mut(g.ThermalOptimum, 1.8, 8, 36);
        g.KinAffinity = Mut(g.KinAffinity, .2, -1, 1);
        g.TissueSense = Mut(g.TissueSense, .2, -1, 1);
        g.LightSense = Mut(g.LightSense, .2, -1, 1);
        g.MoistureSense = Mut(g.MoistureSense, .2, -1, 1);
        g.Elongation = Mut(g.Elongation, .3, .7, 3.8);
        g.Flattening = Mut(g.Flattening, .14, .18, 1.25);
        g.Taper = Mut(g.Taper, .2, -.85, .85);
        g.EdgeGrowth = Mut(g.EdgeGrowth, .12, 0, 1);
        g.SurfaceAdhesion = Mut(g.SurfaceAdhesion, .15, 0, 1);
        g.Waterproofing = Mut(g.Waterproofing, .18, 0, 1);
        g.Density = Mut(g.Density, .16, 0, 1);
        foreach (var rule in g.Pattern)
        {
            rule.Turn = Mut(rule.Turn, .35, -Math.PI, Math.PI);
            rule.Elevation = Mut(rule.Elevation, .35, -1.4, 1.4);
            rule.Size = Mut(rule.Size, .14, .55, 1.6);
            rule.Phase = Mut(rule.Phase, .3, -3, 3);
            rule.EnzymeBias = Mut(rule.EnzymeBias, .12, -.5, .5);
            rule.PhotoBias = Mut(rule.PhotoBias, .12, -.5, .5);
            rule.Outgrowth = Mut(rule.Outgrowth, .24, 0, 1.5);
            rule.Stiffness = Mut(rule.Stiffness, .22, .02, 1);
            rule.Bend = Mut(rule.Bend, .25, -.3, 1.3);
            rule.Paddle = Mut(rule.Paddle, .22, .05, 1);
            if (r.NextDouble() < .08 * mutationScale) rule.ParentBack = Math.Clamp(rule.ParentBack + (r.NextDouble() < .5 ? -1 : 1), 1, Development.NodeBudget);
        }
        if (r.NextDouble() < .08 && g.Pattern.Count < Development.NodeBudget) g.Pattern.Add(g.Pattern[(int)(r.NextDouble() * g.Pattern.Count)].Copy());
        if (r.NextDouble() < .04 && g.Pattern.Count > 1) g.Pattern.RemoveAt((int)(r.NextDouble() * g.Pattern.Count));
        Branching.Mutate(g, r, mutationMode, mutationScale);
        g.Hue = (Mut(g.Hue, .055, 0, 1) + 1) % 1;
        return g;
    }
}

public sealed class Cell
{
    public double Integrity { get; set; } = 1;
    public double LastDamage { get; set; } = -100;
    public double? FormedAt { get; set; }
    public V Position { get; set; }
    public int Parent { get; set; } = -1;
    public double Angle { get; set; }
    public double Elevation { get; set; }
}

public sealed class Organism
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public int MateId { get; set; }
    public int Generation { get; set; }
    public Genome Genes { get; set; } = new();
    public Genome? ParentGenes { get; set; }
    public List<Cell> Body { get; set; } = [];
    public List<BudState> BudStates { get; set; } = [];
    public double Energy { get; set; } = 32;
    public double Age { get; set; }
    public double Heading { get; set; }
    public double Pitch { get; set; }
    public double BirthTime { get; set; }
    public int Children { get; set; }
    public double LastBirth { get; set; }
    public double Phase { get; set; }
    public double LastMeal { get; set; } = -10;
    public double LightEnergy { get; set; }
    public double GrazingEnergy { get; set; }
    public double TissueEnergy { get; set; }
    public FeedingMetrics Feeding { get; set; } = new();
    public FeedingContact CoreFeeding { get; set; } = new();
    [System.Text.Json.Serialization.JsonIgnore] internal double StepTissueCue;
    public double LastAttack { get; set; } = -10;
    public double Irritation { get; set; }
    public V ThreatDirection { get; set; }
    public double LastContact { get; set; } = -10;
    public double Hydration { get; set; } = 1;
    public int GroundContacts { get; set; }
    public double ShoreEnergy { get; set; }
    public double VerticalSpeed { get; set; }
    public V Velocity { get; set; }
    public double Anchoring { get; set; }
    public double LocomotionPower { get; set; }
    public double ContactSlip { get; set; }
    public double LastDamage { get; set; } = -100;
    [System.Text.Json.Serialization.JsonIgnore] public BudPose[] PhysicalBuds = [];
    [System.Text.Json.Serialization.JsonIgnore] internal BranchContact[] StepBranchContacts = [];
    [System.Text.Json.Serialization.JsonIgnore] public V BudMinimum, BudMaximum;
    [System.Text.Json.Serialization.JsonIgnore] public double PhysicalExtent;
    public V Center { get { V p = new(); foreach (var c in Body) p += c.Position; return Body.Count == 0 ? p : p * (1.0 / Body.Count); } }
    [System.Text.Json.Serialization.JsonIgnore] public V StepCenter;
    [System.Text.Json.Serialization.JsonIgnore] internal List<Organism> StepNeighbors = [];
    [System.Text.Json.Serialization.JsonIgnore] internal double ThermalEfficiency, MetabolicRate;
    public Organism CopyForView() { var o = (Organism)MemberwiseClone(); o.Feeding = Feeding.Copy(); o.CoreFeeding = CoreFeeding.Copy(); o.PhysicalBuds = []; o.StepBranchContacts = []; o.StepNeighbors = []; o.BudStates = new List<BudState>(BudStates.Count); foreach (var b in BudStates) o.BudStates.Add(b.Copy()); o.Body = new List<Cell>(Body.Count); foreach (var c in Body) o.Body.Add(new Cell { Position = c.Position, Parent = c.Parent, Angle = c.Angle, Elevation = c.Elevation, FormedAt = c.FormedAt, Integrity = c.Integrity, LastDamage = c.LastDamage }); return o; }
    public double Capacity => Math.Max(.001, 44 * TissueEconomy.Volume(this));
    // A continuous developmental signal permits regional differences in tissue allocation.
    public double LocalEnzymes(int cell) => Math.Clamp(Genes.Enzymes + Genes.Differentiation * Math.Sin(cell * Genes.Turn) * .35 + Genes.Rule(cell).EnzymeBias, 0, 1);
    public double LocalPhotosynthesis(int cell) => Math.Clamp(Genes.Photosynthesis + Genes.Rule(cell).PhotoBias, 0, 1);
}

public sealed class Rock
{
    internal int CollisionOrder;
    public double Food { get; set; } = 4;
    public V Position { get; set; }
    public double Radius { get; set; }
}

public sealed class Remains
{
    public V Position { get; set; }
    public double Energy { get; set; }
    public double Radius { get; set; }
}

public interface IHabitat
{
    V Flow(V p, double time);
    double Production(V p, double time);
    double Temperature(V p, double time);
    double Light(V p, double time);
}

public sealed class Shallows : IHabitat
{
    public V Flow(V p, double time) => new(Math.Sin(p.Y / 180 + time / 110) * 2.1 + .7, Math.Cos(p.X / 240 + time / 150) * 1.6, Math.Sin(p.X / 220 + p.Y / 350 + time / 90) * .55);
    public double Production(V p, double time) => .025 + .085 * Math.Pow((1 + Math.Sin(p.X / 190 + Math.Cos(p.Y / 140) + time / 700)) / 2, 2);
    public double Temperature(V p, double time) => 23 + Math.Sin(p.X / 380 + p.Y / 720) * 3.5 + Math.Sin(time / 900) * 2;
    public double Light(V p, double time) => (.7 + .3 * Math.Sin(time / 480)) * (.3 + .7 * p.Z / World.Depth);
}

public sealed partial class World
{
    // Independent rows/organisms are parallel; energy transfers and births stay ordered.
    internal static readonly ParallelOptions SimulationWorkers = new()
    {
        MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount - 2, 1, 8)
    };
    public const double HabitatScale = 1.5, CellSize = 20 * HabitatScale;
    public const double Width = 1800 * HabitatScale, Height = 1120 * HabitatScale, Depth = 360;
    public const int Cols = 90, Rows = 56, Layers = 4, FieldCount = Cols * Rows * Layers;
    public int FormatVersion { get; set; }
    public MutationMode MutationMode { get; set; } = MutationMode.Scaled;
    public bool CollectData { get; set; }
    public int? Seed { get; set; }
    public string? RunId { get; set; }
    public bool RestartOnExtinction { get; set; }
    public const int DefaultPopulationCap = 1000;
    public int PopulationCap { get; set; } = DefaultPopulationCap;
    public int AutomaticRestarts { get; set; }
    public bool Tidal { get; set; }
    public double[] Biofilm { get; set; } = [];
    [System.Text.Json.Serialization.JsonIgnore] public double WaterLevel => Tidal ? tideLevel : Depth;
    [System.Text.Json.Serialization.JsonIgnore] public double Daylight => daylight;
    private double time, tideLevel = Shoreline.WaterLevel(0), daylight = Shoreline.Daylight(0);
    public double Ground(double x, double y) => Tidal ? Shoreline.Ground(x, y) : 0;
    public int SurfaceTile(V p) => Math.Clamp((int)(p.Y / CellSize), 0, Rows - 1) * Cols + Math.Clamp((int)(p.X / CellSize), 0, Cols - 1);
    public double Immersion(V p, double radius)
    {
        double level = WaterLevel;
        if (p.Z + radius <= level) return 1;
        if (Tidal && SurfaceWater[SurfaceTile(p)] > .05) level = Math.Max(level, PuddleLevel(p));
        return Math.Clamp((level - p.Z + radius) / (2 * radius), 0, 1);
    }
    public double Time { get => time; set { time = value; tideLevel = Shoreline.WaterLevel(value); daylight = Shoreline.Daylight(value); } }
    public int NextId { get; set; } = 1;
    public int Births { get; set; }
    public int Deaths { get; set; }
    public int Exchanges { get; set; }
    public int RecordGeneration { get; set; }
    public int TissueMeals { get; set; }
    public int ObservedOutgrowthBand { get; set; }
    public bool ObservedGroundPush { get; set; }
    public bool ObservedShoreBirth { get; set; }
    public double[] Food { get; set; } = [];
    public double[] OrganicMatter { get; set; } = [];
    public double[] Oxygen { get; set; } = [];
    public double[] Carbon { get; set; } = [];
    public double EnvironmentAccumulator { get; set; }
    public List<Organism> Organisms { get; set; } = [];
    public List<Rock> Rocks { get; set; } = [];
    public List<Remains> Detritus { get; set; } = [];
    public List<string> Events { get; set; } = [];
    [System.Text.Json.Serialization.JsonIgnore] public IHabitat Habitat { get; set; } = new Shallows();
    private sealed class EvolutionRandom : Random
    {
        public uint State = 731;
        public override double NextDouble() { uint x = State; x ^= x << 13; x ^= x >> 17; x ^= x << 5; State = x; return x / 4294967296.0; }
    }
    private readonly EvolutionRandom random = new();
    public uint RandomState { get => random.State; set => random.State = value == 0 ? 731u : value; }
    private const int BinX = 39, BinY = 24, BinZ = 6;
    // Candidate geometry stays contiguous, avoiding organism pointer reads for misses.
    private readonly record struct NeighborEntry(Organism Organism, V Center, double Extent);
    private readonly List<NeighborEntry>?[] bins = [];
    private readonly List<Organism> newborns = [];
    private double[]? oxygenBuffer, carbonBuffer;
    private static int Bin(int x, int y, int z) => (z * BinY + y) * BinX + x;
    public World() : this(true) { }
    private World(bool fields)
    {
        if (!fields) return;
        bins = new List<NeighborEntry>?[BinX * BinY * BinZ];
        Biofilm = new double[Cols * Rows]; SoilMoisture = new double[Cols * Rows]; SurfaceWater = new double[Cols * Rows];
        Food = new double[FieldCount]; OrganicMatter = new double[FieldCount];
        Oxygen = new double[FieldCount]; Carbon = new double[FieldCount];
        Array.Fill(Oxygen, 1); Array.Fill(Carbon, 1);
    }

    public static World Create(int seed = 731, int populationCap = DefaultPopulationCap)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(populationCap);
        var w = new World { RandomState = (uint)seed, Seed = seed, FormatVersion = 9, Tidal = true, PopulationCap = populationCap };
        var r = new Random(seed);
        for (int i = 0; i < w.Biofilm.Length; i++) w.Biofilm[i] = 3 + r.NextDouble() * 5;
        for (int i = 0; i < w.Food.Length; i++) { w.Food[i] = (1.5 + r.NextDouble() * 5) / Layers; w.OrganicMatter[i] = r.NextDouble() * 3 / Layers; }
        for (int i = 0; i < 18; i++) w.Rocks.Add(new Rock { Position = new(70 + r.NextDouble() * (Width - 140), 70 + r.NextDouble() * (Height - 140)), Radius = 18 + r.NextDouble() * 45 });
        foreach (var rock in w.Rocks) rock.Position = new(rock.Position.X, rock.Position.Y, w.Ground(rock.Position.X, rock.Position.Y) - rock.Radius * .35);
        w.AddHabitatDetails(); w.AddHabitatPockets();
        w.InitializeMoisture();
        int founders = populationCap == 0 ? 100 : Math.Min(100, populationCap);
        for (int i = 0; i < founders; i++)
        {
            V p;
            do { p = new(40 + r.NextDouble() * (Width - 80), 40 + r.NextDouble() * (Height - 80), 30 + r.NextDouble() * (Depth - 60)); } while (p.Z < w.Ground(p.X, p.Y) + 12 || p.Z > w.WaterLevel - 12 || w.Rocks.Any(k => (k.Position - p).Length < k.Radius + 25));
            var g = new Genome { Cells = r.Next(1, 5), Radius = 5 + r.NextDouble() * 2, Branch = r.NextDouble(), Turn = .7 + r.NextDouble() * 1.8, Contract = .15 + r.NextDouble() * .5, Hue = .29 + r.NextDouble() * .55, Sense = r.NextDouble() * .6, Enzymes = r.NextDouble() * .25, Membrane = .04 + r.NextDouble() * .22, Photosynthesis = r.NextDouble() * .5, ThermalOptimum = 21 + r.NextDouble() * 5 };
            var o = w.Make(g, p);
            g.Density = .52 + r.NextDouble() * .24;
            g.KinAffinity = (r.NextDouble() - .5) * .1;
            g.TissueSense = (r.NextDouble() - .5) * .1;
            g.LightSense = (r.NextDouble() - .5) * .2;
            g.Pattern[0].Outgrowth = .02 + r.NextDouble() * .10;
            g.Pattern[0].Size = .9 + r.NextDouble() * .2;
            g.Pattern[0].Turn = (r.NextDouble() - .5) * .3;
            g.Pattern[0].Elevation = (r.NextDouble() - .5) * .6;
            g.Elongation = 1.05 + r.NextDouble() * .5;
            g.Flattening = .55 + r.NextDouble() * .4;
            g.Taper = (r.NextDouble() - .25) * .55;
            g.Buds = Branching.Primitive(r);
            o.Age = r.NextDouble() * 40;
            w.Organisms.Add(o);
        }
        w.Note($"{founders} primitive founders");
        return w;
    }

    private Organism Make(Genome g, V p)
    {
        double radius = g.CellRadius(0);
        p = new(Math.Clamp(p.X, radius, Width - radius), Math.Clamp(p.Y, radius, Height - radius), Math.Clamp(p.Z, radius, Depth - radius));
        var o = new Organism { Id = NextId++, Genes = g, Heading = random.NextDouble() * Math.Tau, Pitch = (random.NextDouble() - .5) * .6, BirthTime = Time, Phase = random.NextDouble() * Math.Tau, Energy = 23 + g.Cells * 6 };
        o.Body.Add(new Cell { Position = p });
        return o;
    }

    public int Tile(V p) => (Math.Clamp((int)(p.Z / (Depth / Layers)), 0, Layers - 1) * Rows + Math.Clamp((int)(p.Y / CellSize), 0, Rows - 1)) * Cols + Math.Clamp((int)(p.X / CellSize), 0, Cols - 1);
    public double Sample(V p) => Food[Tile(p)];
    public double FoodSignal(V p, Genome g)
    {
        int tile = Tile(p);
        return Food[tile] * (1 - g.Enzymes * .5) + OrganicMatter[tile] * g.Enzymes + RemainsCue(p) * g.Enzymes * .3
            + (Tidal ? Biofilm[SurfaceTile(p)] * Math.Exp(-Math.Abs(p.Z - Ground(p.X, p.Y)) / 32) * .8 : 0);
    }
    public void Note(string text)
    {
        Events.Insert(0, $"{(int)(Time / 60):00}:{(int)(Time % 60):00}  {text}");
        if (Events.Count > 30) Events.RemoveAt(30);
    }

    private double maximumExtent;
    private void PrepareNeighbors(Organism o)
    {
        var center = o.StepCenter; var neighbors = o.StepNeighbors; neighbors.Clear();
        // Bound the query itself, rather than rounding its radius up around a whole bin.
        // Keep z/y/x traversal so interactions retain their original ordering.
        double reach = 65 + o.PhysicalExtent + maximumExtent + 1e-9;
        int x0 = Math.Clamp((int)((center.X - reach) / 70), 0, BinX - 1), x1 = Math.Clamp((int)((center.X + reach) / 70), 0, BinX - 1);
        int y0 = Math.Clamp((int)((center.Y - reach) / 70), 0, BinY - 1), y1 = Math.Clamp((int)((center.Y + reach) / 70), 0, BinY - 1);
        int z0 = Math.Clamp((int)((center.Z - reach) / 70), 0, BinZ - 1), z1 = Math.Clamp((int)((center.Z + reach) / 70), 0, BinZ - 1);
        for (int z = z0; z <= z1; z++)
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            var bucket = bins[Bin(x, y, z)]; if (bucket == null) continue;
            foreach (var other in bucket)
                if ((other.Center - center).LengthSquared < Math.Pow(65 + o.PhysicalExtent + other.Extent, 2) && other.Organism.Id != o.Id)
                    neighbors.Add(other.Organism);
        }
        double temp = Habitat.Temperature(o.StepCenter, Time);
        o.ThermalEfficiency = Math.Exp(-Math.Pow((temp - o.Genes.ThermalOptimum) / 9, 2));
        o.MetabolicRate = Math.Pow(1.6, (temp - 23) / 10);
    }

    public void Step(double dt)
    {
        FeedingSince ??= Time;
        Time += dt;
        EnvironmentAccumulator += dt;
        if (EnvironmentAccumulator >= .5)
        {
            UpdateEnvironment(EnvironmentAccumulator);
            EnvironmentAccumulator = 0;
        }
        if (Tidal) PreparePhysicalFood(dt);
        for (int i = Detritus.Count - 1; i >= 0; i--)
        {
            var d = Detritus[i];
            if (Tidal) d.Position = VisibleContact(d.Position + (FlowAt(d.Position) + new V(0, 0, -2)) * dt, d.Radius * .3);
            double release = Math.Min(d.Energy, dt * (Tidal ? .025 : .7));
            OrganicMatter[Tile(d.Position)] = Math.Min(20, OrganicMatter[Tile(d.Position)] + release);
            d.Energy -= release;
            if (d.Energy <= 0) Detritus.RemoveAt(i);
        }
        newborns.Clear();
        // Reuse 3D buckets and cache each center once. Neighbors use the same tick snapshot.
        maximumExtent = 0;
        foreach (var bucket in bins) bucket?.Clear();
        foreach (var o in Organisms)
        {
            var p = o.StepCenter = o.Center;
            CacheAnatomy(o);
            maximumExtent = Math.Max(maximumExtent, o.PhysicalExtent);
            int key = Bin(Math.Clamp((int)(p.X / 70), 0, BinX - 1), Math.Clamp((int)(p.Y / 70), 0, BinY - 1), Math.Clamp((int)(p.Z / 70), 0, BinZ - 1));
            (bins[key] ??= []).Add(new(o, p, o.PhysicalExtent));
        }
        if (Organisms.Count >= 128) Parallel.For(0, Organisms.Count, SimulationWorkers, i => PrepareNeighbors(Organisms[i]));
        else foreach (var organism in Organisms) PrepareNeighbors(organism);
        foreach (var o in Organisms)
        {
            if (o.Energy <= 0) continue;
            o.Age += dt;
            RepairBody(o, dt);
            var g = o.Genes;
            var center = o.StepCenter;
            var neighbors = o.StepNeighbors;
            int nearbyCount = 0;
            foreach (var other in neighbors) if ((other.StepCenter - center).LengthSquared < 65 * 65) nearbyCount++;
            double thermalEfficiency = o.ThermalEfficiency, metabolicRate = o.MetabolicRate;
            if (o.Body.Count < g.Cells && o.Age > o.Body.Count * 8 && o.Energy > TissueEconomy.GrowthReserve(o) + TissueEconomy.BuildCost * TissueEconomy.CoreVolume(g, o.Body.Count))
            {
                int n = o.Body.Count;
                var rule = g.Rule(n);
                int parent = Math.Max(0, n - rule.ParentBack - (g.Branch > (Math.Sin(n * 12.3) + 1) / 2 ? 1 : 0));
                double angle = o.Heading + n * g.Turn + rule.Turn;
                o.Body.Add(new Cell { FormedAt = o.Age, Parent = parent, Angle = n * g.Turn + rule.Turn, Elevation = rule.Elevation, Position = o.Body[parent].Position + V.Direction(angle, o.Pitch + rule.Elevation) * g.CellRadius(n) * 1.55 });
                o.Energy -= TissueEconomy.BuildCost * TissueEconomy.CoreVolume(g, n);
            }
            V gradient = new(FoodSignal(center + new V(24, 0), g) - FoodSignal(center - new V(24, 0), g), FoodSignal(center + new V(0, 24), g) - FoodSignal(center - new V(0, 24), g), FoodSignal(center + new V(0, 0, 48), g) - FoodSignal(center - new V(0, 0, 48), g));
            gradient *= g.Sense;
            gradient += new V(0, 0, g.LightSense * LightAt(center));
            if (Tidal)
            {
                double sensitivity = g.MoistureSense * (1 - o.Hydration);
                gradient += new V(MoistureAt(center + new V(30, 0)) - MoistureAt(center - new V(30, 0)),
                    MoistureAt(center + new V(0, 30)) - MoistureAt(center - new V(0, 30))) * sensitivity;
            }
            o.Irritation *= Math.Exp(-dt * 1.2);
            gradient += o.ThreatDirection * (o.Irritation * (g.Sense + .15) * 4);
            double hunger = 1 - Math.Clamp(o.Energy / o.Capacity, 0, 1);
            bool preyNearby = false;
            foreach (var other in neighbors)
            {
                    if (other.Id == o.Id || other.Energy <= 0) continue;
                    V toward = other.StepCenter - center; double distance = toward.Length;
                    if (distance < 1 || distance > 65) continue;
                    preyNearby = true;
                    double kin = Math.Max(0, 1 - Math.Abs(g.Hue - other.Genes.Hue) * 8);
                    double weight = g.KinAffinity * kin + g.TissueSense * other.StepTissueCue * hunger;
                    gradient += toward * (weight * (1 - distance / 65) / distance);
            }
            if (preyNearby) { o.Feeding.PreyNearbySeconds += dt; Feeding.PreyNearbySeconds += dt; }
            double wet = Tidal ? Immersion(center, g.Radius) : 1;
            V freeMotion = BodyMechanics.Solve(this, o, gradient, dt);
            double corePower = o.LocomotionPower;
            V appendageMotion = Tidal ? Appendages.Solve(this, o, dt, neighbors, freeMotion) : new V();
            // No more growth or repair occurs in this organism's turn. Damage below
            // affects its neighbors, whose capacities must still be read live.
            double capacity = o.Capacity;
            if (Tidal) o.LocomotionPower += corePower;
            o.Feeding.LocomotionEnergy += o.LocomotionPower * dt; Feeding.LocomotionEnergy += o.LocomotionPower * dt;
            V motion = freeMotion + appendageMotion;
            if (Tidal && center.Z < Ground(center.X, center.Y) + g.Radius * 1.5)
                motion *= 1 / (1 + Shoreline.Sediment(center.X, center.Y) * .8 / (1 + o.GroundContacts));
            if (Tidal)
            {
                UpdateHydration(o, dt, wet);
                o.Energy -= dt * o.Body.Count * (Math.Max(0, .55 - o.Hydration) * 2 + g.Waterproofing * .025);
                o.VerticalSpeed = (o.VerticalSpeed + dt * (-16 * (1 - wet) + (0.5 - g.Density) * 8 * wet)) * Math.Exp(-dt * (1 + wet * 3));
                motion += new V(0, 0, o.VerticalSpeed);

            }
            o.Velocity = motion;
            foreach (var c in o.Body) c.Position += motion * dt;
            for (int n = 1; n < o.Body.Count; n++)
            {
                var c = o.Body[n]; var parent = o.Body[c.Parent];
                double localPulse = Math.Sin(o.Age * g.Rhythm * 2 + o.Phase - n * g.Rule(n).Phase);
                double rest = (g.CellRadius(n) + g.CellRadius(c.Parent)) * (.8 + g.Contract * .15 * localPulse);
                V diff = c.Position - parent.Position;
                double length = Math.Max(.001, diff.Length);
                V correction = diff * ((length - rest) / length * Math.Min(.45, dt * (2 + g.Adhesion * 8)));
                c.Position -= correction;
                parent.Position += correction;
                V wanted = V.Direction(o.Heading + c.Angle, o.Pitch + c.Elevation);
                V bend = (wanted * rest - diff) * (.11 * dt * g.Adhesion);
                c.Position += bend; parent.Position -= bend;
            }
            for (int a = 0; a < o.Body.Count; a++) for (int b = a + 1; b < o.Body.Count; b++)
            {
                V diff = o.Body[a].Position - o.Body[b].Position;
                double length = diff.Length;
                V normal = BodyMechanics.Unit(diff);
                double separation = (BodyMechanics.Support(o, a, normal) + BodyMechanics.Support(o, b, normal)) * .625;
                if (length < separation && length > .001)
                {
                    V push = diff * ((separation - length) / length * .2);
                    o.Body[a].Position += push; o.Body[b].Position -= push;
                }
            }
            for (int ci = 0; ci < o.Body.Count; ci++)
            {
                var c = o.Body[ci];
                double cellRadius = g.CellRadius(ci);
                double exchangeArea = TissueEconomy.CoreArea(g, ci) * c.Integrity;
                foreach (var rock in NearbyRocks(c.Position))
                {
                    V offset = c.Position - rock.Position;
                    double outerReach = rock.Radius + BodyMechanics.OuterRadius(o, ci);
                    if (offset.LengthSquared > outerReach * outerReach) continue;
                    double distance = offset.Length;
                    V normal = distance > 1e-9 ? offset * (1 / distance) : new V(0, 0, 1);
                    double reach = rock.Radius + BodyMechanics.Support(o, ci, normal);
                    if (distance < reach) c.Position = rock.Position + normal * reach;
                }
                c.Position = new(Math.Clamp(c.Position.X, cellRadius, Width - cellRadius), Math.Clamp(c.Position.Y, cellRadius, Height - cellRadius), Math.Clamp(c.Position.Z, cellRadius, Depth - cellRadius));
                if (Tidal)
                {
                    double floor = Ground(c.Position.X, c.Position.Y) + BodyMechanics.Support(o, ci, new V(0, 0, 1));
                    if (c.Position.Z < floor) { c.Position = new(c.Position.X, c.Position.Y, floor); o.VerticalSpeed = Math.Max(0, o.VerticalSpeed); }
                    if (SurfaceContact(c.Position, cellRadius * g.Flattening + .2, out _, out _, out var grazingRock))
                    {
                        double bite = GrazeContact(c.Position, dt * .9 * exchangeArea * (1 - g.Enzymes * .4) * (1 - g.Membrane * .3), grazingRock); o.Energy = Math.Min(capacity, o.Energy + bite * 1.7); o.ShoreEnergy += bite * 1.7;
                        DrinkFood(o, bite * 1.7, MoistureAt(c.Position));
                        if (bite > .01) o.LastMeal = Time;
                    }
                }
                if (Tidal) AbsorbContactWater(o, c.Position, cellRadius * g.Flattening, 1, dt);
                int tile = Tile(c.Position);
                double enzyme = o.LocalEnzymes(ci);
                double photoInvestment = o.LocalPhotosynthesis(ci);
                double eaten = Math.Min(Food[tile], (Tidal ? .18 * Immersion(c.Position, cellRadius) : 1) * dt * .45 * exchangeArea * (1 - enzyme * .5) * (1 - g.Membrane * .28) * (1 - photoInvestment * .45) * thermalEfficiency);
                Food[tile] -= eaten;
                double digested = Math.Min(OrganicMatter[tile], dt * enzyme * .9 * exchangeArea * (Tidal ? .18 : 1));
                OrganicMatter[tile] -= digested;
                double light = LightAt(c.Position);
                if (Tidal) light *= Daylight / (1 + nearbyCount * .12);
                double exposedArea = Math.Clamp(Math.Sqrt(g.Elongation / g.Flattening), .75, 1.5);
                double photo = Photosynthesize(c.Position, dt * photoInvestment * light * .6 * thermalEfficiency * exposedArea * exchangeArea, dt);
                double aerobic = .2 + .8 * Math.Min(1, Oxygen[tile] / .45);
                double captured = Tidal ? Capture(c.Position - motion * dt, c.Position, cellRadius, dt * 1.2 * exchangeArea, enzyme) : 0;
                if (Tidal) Scavenge(o, c.Position, cellRadius, exchangeArea, enzyme, g.SurfaceAdhesion + g.Contract * .25, dt, aerobic);
                double grazing = (eaten * 1.3 + digested * 1.7 + captured) * aerobic;
                o.Energy = Math.Min(capacity, o.Energy + grazing + photo);
                o.LightEnergy += photo; o.GrazingEnergy += grazing;
                if (Tidal) DrinkFood(o, grazing);
                double respiration = dt * metabolicRate * .045;
                Oxygen[tile] = Math.Max(0, Oxygen[tile] - respiration);
                Carbon[tile] = Math.Min(3, Carbon[tile] + respiration * .8);
                if (Oxygen[tile] < .12) o.Energy -= dt * (.12 - Oxygen[tile]) * 2;
                if (eaten + digested > dt * .2) o.LastMeal = Time;
            }
            o.Energy -= dt * metabolicRate * TissueEconomy.CoreVolume(o) * (.13 + g.Radius * .014 + g.Contract * g.Contract * .13 + g.Sense * .025 + Math.Abs(g.MoistureSense) * .008 + g.Enzymes * .055 + g.Membrane * .035 + g.Photosynthesis * .04);
            o.Energy -= dt * o.Body.Count * (.025 * Math.Abs(g.Elongation - 1) + .035 * (1 - Math.Min(1, g.Flattening)) + .025 * g.EdgeGrowth + .015 * g.SurfaceAdhesion + .04 * o.Irritation * g.Contract);
            o.Energy -= dt * Math.Max(0, o.Age - 700) * .0007;
            Organism? mate = null;
            foreach (var other in neighbors)
            {
                    if (other.Id == o.Id) continue;
                    V diff = center - other.StepCenter; double length = diff.Length;
                    double contact = g.Radius + other.Genes.Radius + 4;
                    if (length < contact && length > .001)
                        foreach (var c in o.Body) c.Position += diff * ((contact - length) / length * dt * .8);
                    if (length >= contact && length < contact + 8 && length > .001)
                    {
                        double adhesion = g.SurfaceAdhesion * other.Genes.SurfaceAdhesion;
                        foreach (var c in o.Body) c.Position -= diff * (adhesion * (length - contact) * dt / length);
                        if (adhesion > .08) o.LastContact = Time;
                    }
                    if (length < contact + 8 && other.Age > 70 && Math.Abs(other.Genes.Cells - g.Cells) <= 3 && Time - other.LastBirth > 40 && other.Energy > other.Capacity * .65) mate = other;
            }
            FeedCore(o, neighbors, dt);
            // Reserve slots for all births this tick before spending parent energy or mutating.
            // Existing organisms above a lowered cap survive; deaths free slots next tick.
            if ((PopulationCap == 0 || Organisms.Count + newborns.Count < PopulationCap)
                && o.Energy > capacity * .82 && o.Age > 70 && Time - o.LastBirth > 45 && o.Body.Count == g.Cells && o.Energy * .36 > TissueEconomy.BuildCost * TissueEconomy.CoreVolume(g, 0) * 2)
            {
                var baby = Make(g.Offspring(random, mate?.Genes, MutationMode), center + V.Direction(o.Heading + 2, o.Pitch) * (g.Radius * 3));
                if (o.Energy * .36 <= TissueEconomy.BuildCost * TissueEconomy.CoreVolume(baby.Genes, 0)) continue;
                baby.ParentId = o.Id; baby.ParentGenes = g.Copy(); baby.Generation = o.Generation + 1;
                baby.Energy = Math.Max(0, o.Energy * .36 - TissueEconomy.BuildCost * TissueEconomy.CoreVolume(baby.Genes, 0)); o.Energy *= .59;
                if (mate != null)
                {
                    double contribution = TissueEconomy.MateContribution(mate.Energy);
                    baby.MateId = mate.Id; baby.Energy += contribution * (7.0 / 9); mate.Energy -= contribution; mate.LastBirth = Time; mate.Children++; Exchanges++;
                }
                o.LastBirth = Time; o.Children++; Births++;
                if (Tidal && !ObservedShoreBirth && Immersion(center, g.Radius) < .3)
                { ObservedShoreBirth = true; Note($"First exposed-shore birth / offspring #{baby.Id}"); }
                newborns.Add(baby);
                if (baby.Generation > RecordGeneration)
                {
                    RecordGeneration = baby.Generation;
                    Note($"Generation {RecordGeneration} · offspring #{baby.Id}");
                }
            }
        }
        for (int i = Organisms.Count - 1; i >= 0; i--)
        {
            var o = Organisms[i];
            if (o.Energy > 0) continue;
            for (int c = 0; c < o.Body.Count; c++) Detritus.Add(new Remains { Position = o.Body[c].Position, Energy = TissueEconomy.RemainsFood(TissueEconomy.CoreVolume(o.Genes, c) * o.Body[c].Integrity), Radius = o.Genes.CellRadius(c) });
            if (o.Genes.Buds != null) for (int b = 0; b < o.BudStates.Count; b++)
                if (o.BudStates[b].Growth > 0) Detritus.Add(new Remains { Position = o.Center, Energy = TissueEconomy.RemainsFood(TissueEconomy.BranchVolume(o, b, o.BudStates[b].Growth) * o.BudStates[b].Integrity), Radius = o.Genes.Radius * o.Genes.Buds[b].Width });
            Organisms.RemoveAt(i); Deaths++;
        }
        Organisms.AddRange(newborns);
        if (Tidal)
        {
            foreach (var o in Organisms)
            {
                double reach = Appendages.Reach(o);
                int band = (int)(reach / .4);
                if (band > ObservedOutgrowthBand)
                { ObservedOutgrowthBand = band; Note($"Longer outgrowths / organism #{o.Id}, generation {o.Generation}"); }
                if (!ObservedGroundPush && o.GroundContacts > 0 && reach > .35)
                { ObservedGroundPush = true; Note($"Articulated tissue pushes against the bottom / #{o.Id}"); }
            }
        }
        if (Organisms.Count == 0 && (Events.Count == 0 || !Events[0].Contains("extinction"))) Note("Local extinction · the water continues");
    }

    private void UpdateEnvironment(double dt)
    {
        if (Tidal) UpdateMoisture(dt);
        CurrentTime = Time; currentMap = null;
        foreach (var rock in Rocks)
        {
            int tile = SurfaceTile(rock.Position);
            double growth = Math.Min(Biofilm[tile], dt * .12 * Math.Max(0, 1 - rock.Food / 8));
            rock.Food += growth; Biofilm[tile] -= growth;
        }
        var nextOxygen = oxygenBuffer ??= new double[FieldCount];
        var nextCarbon = carbonBuffer ??= new double[FieldCount];
        double daylight = Tidal ? Daylight : 1, waterLevel = WaterLevel;
        Parallel.For(0, Layers * Rows, SimulationWorkers, row =>
        {
            int z = row / Rows, y = row % Rows;
            for (int x = 0; x < Cols; x++)
            {
                int offset = z * Cols * Rows, i = offset + y * Cols + x;
                var p = new V(x * CellSize, y * CellSize, (z + .5) * Depth / Layers);
                double production = Habitat.Production(p, Time);
                if (Tidal)
                {
                    double floor = Ground(p.X, p.Y);
                    if (p.Z < floor || p.Z > waterLevel) { Food[i] *= Math.Exp(-dt * .15); OrganicMatter[i] *= Math.Exp(-dt * .05); production = 0; }
                    else production *= .55;
                }
                double light = Habitat.Light(p, Time) * daylight;
                double algae = production * light * Math.Min(1, Carbon[i]) * dt / Layers;
                Food[i] = Math.Min(8.0 / Layers, Food[i] + algae);
                OrganicMatter[i] = Math.Min(10.0 / Layers, OrganicMatter[i] + (Tidal ? .013 : .085 - production * .65) * dt / Layers);
                int left = offset + y * Cols + Math.Max(0, x - 1), right = offset + y * Cols + Math.Min(Cols - 1, x + 1);
                int up = offset + Math.Max(0, y - 1) * Cols + x, down = offset + Math.Min(Rows - 1, y + 1) * Cols + x;
                int below = Math.Max(0, z - 1) * Cols * Rows + y * Cols + x, above = Math.Min(Layers - 1, z + 1) * Cols * Rows + y * Cols + x;
                double saturation = 1 - (Habitat.Temperature(p, Time) - 20) * .012;
                double air = z == Layers - 1 ? .05 : .006;
                // Microbial breakdown of accumulated waste consumes local oxygen.
                double decay = Math.Min(OrganicMatter[i], dt * .012 * OrganicMatter[i] * Oxygen[i] / (.2 + Oxygen[i]));
                OrganicMatter[i] -= decay;
                nextOxygen[i] = Math.Clamp(Oxygen[i] + (Oxygen[left] + Oxygen[right] + Oxygen[up] + Oxygen[down] - 4 * Oxygen[i]) * dt * .18 + (Oxygen[above] + Oxygen[below] - 2 * Oxygen[i]) * dt * .045 + (saturation - Oxygen[i]) * dt * air + algae * .12 - decay * .18, 0, 2);
                nextCarbon[i] = Math.Clamp(Carbon[i] + (Carbon[left] + Carbon[right] + Carbon[up] + Carbon[down] - 4 * Carbon[i]) * dt * .16 + (Carbon[above] + Carbon[below] - 2 * Carbon[i]) * dt * .04 + (1 - Carbon[i]) * dt * air * .5 - algae * .1 + decay * .12, 0, 3);
            }
        });
        if (Tidal)
        {
            for (int y = 0; y < Rows; y++) for (int x = 0; x < Cols; x++)
            {
                int i = y * Cols + x;
                double h = Ground(x * CellSize, y * CellSize);
                double patch = .15 + .85 * Math.Pow(.5 + .5 * Math.Sin(x * .23 + Math.Cos(y * .31)), 4);
                double moisture = SoilMoisture[i];
                double light = Math.Exp(-Math.Max(0, WaterLevel - h) / 100) * Daylight;
                int chemistry = Tile(new V(x * CellSize, y * CellSize, h));
                double growth = Math.Min(nextCarbon[chemistry] * 10, dt * .25 * patch * moisture * light * Math.Min(1, nextCarbon[chemistry]) * (1 - Biofilm[i] / 12));
                nextCarbon[chemistry] -= growth * .1; nextOxygen[chemistry] = Math.Min(2, nextOxygen[chemistry] + growth * .08);
                Biofilm[i] = Math.Clamp(Biofilm[i] + growth - dt * (.001 + (1 - moisture) * .003) * Biofilm[i], 0, 12);
            }
        }
        oxygenBuffer = Oxygen; carbonBuffer = Carbon;
        Oxygen = nextOxygen; Carbon = nextCarbon;
    }

    // Only the worker owns mutable simulation state. Renderers receive independent snapshots.
    public World Snapshot(World? previous = null, bool refreshFields = true)
    {
        var view = new World(false) { Feeding = Feeding.Copy(), FeedingSince = FeedingSince, Organisms = new List<Organism>(Organisms.Count), Detritus = new List<Remains>(Detritus.Count), FormatVersion = FormatVersion, MutationMode = MutationMode, CollectData = CollectData, Seed = Seed, RunId = RunId, PopulationCap = PopulationCap, RestartOnExtinction = RestartOnExtinction, AutomaticRestarts = AutomaticRestarts, Tidal = Tidal, Time = Time, Births = Births, Deaths = Deaths, Exchanges = Exchanges, RecordGeneration = RecordGeneration, TissueMeals = TissueMeals, NextId = NextId, Habitat = Habitat, RandomState = RandomState };
        bool copy = refreshFields || previous == null;
        view.SoilMoisture = copy ? (double[])SoilMoisture.Clone() : previous!.SoilMoisture;
        view.SurfaceWater = copy ? (double[])SurfaceWater.Clone() : previous!.SurfaceWater;
        view.Biofilm = copy ? (double[])Biofilm.Clone() : previous!.Biofilm;
        view.Food = copy ? (double[])Food.Clone() : previous!.Food;
        view.OrganicMatter = copy ? (double[])OrganicMatter.Clone() : previous!.OrganicMatter;
        view.Oxygen = copy ? (double[])Oxygen.Clone() : previous!.Oxygen;
        view.Carbon = copy ? (double[])Carbon.Clone() : previous!.Carbon;
        view.Rocks = Rocks.Select(r => new Rock { Position = r.Position, Radius = r.Radius, Food = r.Food }).ToList();
        view.CurrentTime = CurrentTime; view.currentMap = currentMap; // Replaced, never mutated, after construction.
        view.Injuries = Injuries; view.LostBranches = LostBranches;
        view.Clumps = Clumps.Select(c => c.Copy()).ToList();
        view.Events = new List<string>(Events);
        foreach (var o in Organisms) view.Organisms.Add(o.CopyForView());
        foreach (var d in Detritus) view.Detritus.Add(new Remains { Position = d.Position, Energy = d.Energy, Radius = d.Radius });
        return view;
    }

    public static string SavePath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tideglass", "ecosystem.json");
    public void Save(string? path = null)
    {
        path ??= SavePath;
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        using (var file = System.IO.File.Create(temp)) JsonSerializer.Serialize(file, this);
        System.IO.File.Move(temp, path, true);
    }
    public static World Load(string? path = null)
    {
        using var file = System.IO.File.OpenRead(path ?? SavePath);
        var w = JsonSerializer.Deserialize<World>(file) ?? throw new InvalidDataException("Empty ecosystem save.");
        if (w.FormatVersion < 2 && w.Food.Length == Cols * Rows)
        {
            double[] Expand(double[] source, bool nutrient)
            {
                if (source.Length != Cols * Rows) throw new InvalidDataException("Invalid legacy field.");
                var result = new double[FieldCount];
                for (int z = 0; z < Layers; z++) for (int i = 0; i < source.Length; i++) result[z * source.Length + i] = source[i] / (nutrient ? Layers : 1);
                return result;
            }
            w.Food = Expand(w.Food, true); w.OrganicMatter = Expand(w.OrganicMatter, true); w.Oxygen = Expand(w.Oxygen, false); w.Carbon = Expand(w.Carbon, false);
            foreach (var o in w.Organisms)
            {
                double altitude = 35 + ((uint)o.Id * 2654435761u % 280);
                foreach (var c in o.Body) c.Position = new(c.Position.X, c.Position.Y, altitude);
            }
            w.FormatVersion = 2; w.Note("The habitat opens into three dimensions");
        }
        if (w.FormatVersion < 3)
        {
            w.Tidal = true; w.FormatVersion = 3;
            for (int i = 0; i < w.Biofilm.Length; i++) w.Biofilm[i] = 5;
            foreach (var o in w.Organisms) foreach (var c in o.Body)
                c.Position = new(c.Position.X, c.Position.Y, Math.Max(c.Position.Z, w.Ground(c.Position.X * HabitatScale, c.Position.Y * HabitatScale) + o.Genes.Radius));
            foreach (var rock in w.Rocks) rock.Position = new(rock.Position.X, rock.Position.Y, w.Ground(rock.Position.X * HabitatScale, rock.Position.Y * HabitatScale) - rock.Radius * .35);
            w.Note("Shoreline habitat enabled");
        }
        if (w.FormatVersion < 4)
        {
            // Spread centers, preserving each body's dimensions and developmental state.
            foreach (var o in w.Organisms)
            {
                V center = o.Center;
                V offset = new(center.X * (HabitatScale - 1), center.Y * (HabitatScale - 1));
                foreach (var cell in o.Body) cell.Position += offset;
            }
            foreach (var rock in w.Rocks) rock.Position = new(rock.Position.X * HabitatScale, rock.Position.Y * HabitatScale, rock.Position.Z);
            foreach (var remains in w.Detritus) remains.Position = new(remains.Position.X * HabitatScale, remains.Position.Y * HabitatScale, remains.Position.Z);
            w.FormatVersion = 4;
            w.Note("The habitat expands / 2.25 times the ground area");
        }
        if (w.FormatVersion < 5)
        {
            w.FormatVersion = 5;
            w.Note("Branching development becomes available to descendants");
        }
        if (w.FormatVersion < 6)
        {
            w.AddHabitatDetails(); w.CurrentTime = w.Time; w.FormatVersion = 6;
            w.Note("Currents, sheltered crevices, contact feeding, and tissue repair");
        }
        if (w.FormatVersion < 7)
        {
            w.InitializeMoisture(); w.FormatVersion = 7;
            w.Note("Rain, damp soil, and retained pools open new shore habitats");
        }
        if (w.FormatVersion < 8)
        {
            w.AddHabitatPockets(); w.FormatVersion = 8;
        }
        if (w.FormatVersion < 9) { w.FormatVersion = 9; w.Note("Tissue accounting and force-driven movement enabled"); }
        if (w.SoilMoisture.Length != Cols * Rows || w.SurfaceWater.Length != Cols * Rows || w.SoilMoisture.Any(m => !double.IsFinite(m) || m < 0 || m > 1) || w.SurfaceWater.Any(m => !double.IsFinite(m) || m < 0 || m > 12)) throw new InvalidDataException("Invalid moisture fields.");
        if (!Enum.IsDefined(w.MutationMode) || w.Biofilm.Length != Cols * Rows || w.Food.Length != FieldCount || w.OrganicMatter.Length != FieldCount || w.Oxygen.Length != FieldCount || w.Carbon.Length != FieldCount || !double.IsFinite(w.Time) || w.Organisms.Any(o => o.Body.Count == 0 || o.Body.Count > Development.NodeBudget || o.Genes.Cells < 1 || o.Genes.Cells > Development.NodeBudget || o.Genes.Pattern.Count == 0 || !Branching.Valid(o.Genes) || o.BudStates.Any(b => !double.IsFinite(b.Growth + b.Activation + b.PhaseOffset + b.PhaseRate + b.Contraction + b.Load + b.Slip + b.Memory + b.Signal + b.MotorTurn + b.MotorPitch + b.Deflection.Length) || b.Growth < 0 || b.Growth > 1) || o.Body.Where((c, i) => c.Parent >= i || c.Parent < -1).Any())) throw new InvalidDataException("Invalid ecosystem data.");
        return w;
    }
}
