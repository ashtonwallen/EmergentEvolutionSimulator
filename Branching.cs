namespace Tideglass;

// A shared computation budget, not a catalogue of anatomies or an appendage count.
public static class Development
{
    public const int NodeBudget = 128;
}

// One piece of growing tissue. Any piece can bear children, and roots attach independently.
public sealed class BudGene
{
    public int Parent { get; set; } = -1;
    public int AnchorCell { get; set; }
    public double AnchorAngle { get; set; }
    public double AnchorPitch { get; set; }
    public double Turn { get; set; }
    public double Elevation { get; set; }
    public double Length { get; set; } = .18;
    public double Width { get; set; } = .20;
    public double Flatness { get; set; } = .8;
    public double Taper { get; set; } = .55;
    public double Stiffness { get; set; } = .2;
    public double Muscle { get; set; } = .35;
    public double Phase { get; set; }
    public double Frequency { get; set; } = 1;
    public double Coupling { get; set; } = .4;
    public double ChildCoupling { get; set; } = .2;
    public double NeighborCoupling { get; set; }
    public double ContractionSense { get; set; }
    public double PhaseResponse { get; set; }
    public double MotorResponse { get; set; }
    public double BendAmplitude { get; set; }
    public double BendPhase { get; set; }
    public double MemoryResponse { get; set; }
    public double MemoryTime { get; set; } = 2;
    public double RecurrentResponse { get; set; }
    public double HungerResponse { get; set; }
    public double SlipResponse { get; set; }
    public double MoistureResponse { get; set; }
    public double Permeability { get; set; } = .5;
    public double StretchResponse { get; set; }
    public double SignalDelay { get; set; } = .25;
    public double Toughness { get; set; } = .1;
    public double Repair { get; set; } = .25;
    public double TouchResponse { get; set; }
    public double FoodResponse { get; set; }
    public double LightResponse { get; set; }
    public double Adhesion { get; set; } = .05;
    public double Digestion { get; set; } = .05;
    public double Photo { get; set; } = .05;
    public double Delay { get; set; }
    public double Maturation { get; set; } = 35;
    private sealed record AttachmentProjection(double Angle, double Pitch, V Direction);
    private volatile AttachmentProjection? cachedProjection;
    internal V AnchorDirection()
    {
        var projection = cachedProjection;
        if (projection == null || projection.Angle != AnchorAngle || projection.Pitch != AnchorPitch)
        {
            projection = new(AnchorAngle, AnchorPitch, new(Math.Cos(AnchorAngle) * Math.Cos(AnchorPitch),
                Math.Sin(AnchorAngle) * Math.Cos(AnchorPitch), Math.Sin(AnchorPitch)));
            cachedProjection = projection;
        }
        return projection.Direction;
    }
    public List<SignalWire> Wires { get; set; } = [];
    public BudGene Copy() { var b = (BudGene)MemberwiseClone(); b.Wires = Wires.Select(w => w.Copy()).ToList(); return b; }
}

// Development and local reflex state are separate from the inherited recipe.
public sealed class BudState
{
    public double? FormedAt { get; set; }
    public double Growth { get; set; }
    public double Activation { get; set; }
    public bool Contact { get; set; }
    public double Integrity { get; set; } = 1;
    public double LastDamage { get; set; } = -100;
    public double Signal { get; set; }
    public double Memory { get; set; }
    public double Stretch { get; set; }
    public double PhaseOffset { get; set; }
    public double PhaseRate { get; set; }
    public double Contraction { get; set; }
    public double Load { get; set; }
    public double Slip { get; set; }
    public BudState Copy() => (BudState)MemberwiseClone();
}

public readonly record struct BudPose(V Root, V Joint, V Tip, double Radius, double Flatness, int Anchor, bool Active);

public static partial class Branching
{
    private static double Dot(V a, V b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static V Cross(V a, V b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    private static V Unit(V p) => p * (1 / Math.Max(.000001, p.Length));

    public static List<BudGene> Primitive(Random r)
    {
        var result = new List<BudGene>();
        int count = 1 + (int)(r.NextDouble() * 3);
        for (int i = 0; i < count; i++) result.Add(new BudGene { AnchorAngle = r.NextDouble() * Math.Tau, AnchorPitch = (r.NextDouble() - .5) * 1.2, Length = .08 + r.NextDouble() * .18, Phase = r.NextDouble() * Math.Tau });
        return result;
    }

    // Existing adults retain their legacy recipe. Only their offspring are translated.
    public static List<BudGene> FromLegacy(Genome g)
    {
        var result = new List<BudGene>();
        for (int site = 0; site < Math.Min(8, g.Cells); site++) for (int side = -1; side <= 1; side += 2)
        {
            var rule = g.Rule(site);
            if (rule.Outgrowth < .025) continue;
            var root = new BudGene { AnchorCell = site, AnchorAngle = side * Math.PI / 2, Length = rule.Outgrowth * 2, Width = (.14 + rule.Paddle * .2) * Math.Min(1, rule.Outgrowth * 3), Flatness = .9, Elevation = -rule.Bend * .22, Stiffness = rule.Stiffness, Muscle = g.Contract, Phase = -site * rule.Phase + (side < 0 ? Math.PI : 0), Adhesion = g.SurfaceAdhesion, Digestion = g.Enzymes * .2, Photo = g.Photosynthesis * .2 };
            result.Add(root);
            var end = root.Copy(); end.Parent = result.Count - 1; end.Length *= .85; end.Width *= .7; end.Elevation = -rule.Bend * .75; end.AnchorAngle = 0; end.Phase += .4;
            result.Add(end);
        }
        return result;
    }

    public static bool Valid(Genome g)
    {
        if (g.Buds == null) return true;
        if (g.Buds.Count + g.Cells > Development.NodeBudget || g.Buds.Any(b => b.Wires == null) || g.Buds.Sum(b => b.Wires.Count) > WireBudget) return false;
        for (int i = 0; i < g.Buds.Count; i++)
        {
            var b = g.Buds[i];
            if (b.Wires.Any(w => w == null || w.Source < 0 || w.Source >= g.Buds.Count || !double.IsFinite(w.Weight + w.MemoryMix) || Math.Abs(w.Weight) > 2 || w.MemoryMix < 0 || w.MemoryMix > 1)) return false;
            if (b.Parent < -1 || b.Parent >= i || b.AnchorCell < 0 || b.AnchorCell >= g.Cells || !double.IsFinite(b.Length + b.Width + b.Flatness + b.Turn + b.Elevation + b.AnchorAngle + b.AnchorPitch + b.Muscle + b.Phase + b.Frequency + b.Coupling + b.Stiffness + b.TouchResponse + b.FoodResponse + b.LightResponse + b.Digestion + b.Photo + b.Adhesion + b.Delay + b.Maturation + b.Taper + b.ChildCoupling + b.StretchResponse + b.SignalDelay + b.Toughness + b.Repair + b.NeighborCoupling + b.ContractionSense + b.PhaseResponse + b.MotorResponse + b.BendAmplitude + b.BendPhase + b.MemoryResponse + b.MemoryTime + b.RecurrentResponse + b.HungerResponse + b.SlipResponse + b.MoistureResponse + b.Permeability) || b.MemoryTime <= 0 || b.Permeability < 0 || b.Permeability > 1 || b.Length <= 0 || b.Width <= 0 || b.Flatness <= 0 || b.Maturation <= 0) return false;
        }
        return true;
    }

    public static void Mutate(Genome g, Random r)
    {
        var buds = g.Buds!;
        double Mut(double value, double amount, double lo, double hi) => Math.Clamp(value + (r.NextDouble() < .18 ? (r.NextDouble() * 2 - 1) * amount : 0), lo, hi);
        foreach (var b in buds)
        {
            b.AnchorCell = Math.Clamp(b.AnchorCell, 0, g.Cells - 1);
            if (r.NextDouble() < .04) b.AnchorCell = (int)(r.NextDouble() * g.Cells);
            b.AnchorAngle = Mut(b.AnchorAngle, .7, -Math.Tau, Math.Tau);
            b.AnchorPitch = Mut(b.AnchorPitch, .4, -1.5, 1.5);
            b.Turn = Mut(b.Turn, .55, -Math.PI, Math.PI);
            b.Elevation = Mut(b.Elevation, .4, -1.5, 1.5);
            b.Length = Mut(b.Length, .22 + b.Length * .3, .025, 12);
            b.Width = Mut(b.Width, .12, .035, 2);
            b.Flatness = Mut(b.Flatness, .25, .12, 2);
            b.Taper = Mut(b.Taper, .22, .08, 1.5);
            b.Stiffness = Mut(b.Stiffness, .22, .02, 1);
            b.Muscle = Mut(b.Muscle, .22, 0, 1);
            b.Phase = Mut(b.Phase, .6, -Math.Tau, Math.Tau);
            b.Frequency = Mut(b.Frequency, .3, .1, 3);
            b.Coupling = Mut(b.Coupling, .25, -1, 1);
            b.ChildCoupling = Mut(b.ChildCoupling, .3, -1, 1);
            b.NeighborCoupling = Mut(b.NeighborCoupling, .25, -1, 1);
            b.ContractionSense = Mut(b.ContractionSense, .3, -1, 1);
            b.PhaseResponse = Mut(b.PhaseResponse, .25, -1, 1);
            b.MotorResponse = Mut(b.MotorResponse, .3, -1, 1);
            b.BendAmplitude = Mut(b.BendAmplitude, .25, -1, 1);
            b.BendPhase = Mut(b.BendPhase, .6, -Math.PI, Math.PI);
            b.MemoryResponse = Mut(b.MemoryResponse, .3, -1, 1);
            b.MemoryTime = Mut(b.MemoryTime, 1.5, .1, 20);
            b.RecurrentResponse = Mut(b.RecurrentResponse, .3, -1, 1);
            b.HungerResponse = Mut(b.HungerResponse, .3, -1, 1);
            b.SlipResponse = Mut(b.SlipResponse, .3, -1, 1);
            b.MoistureResponse = Mut(b.MoistureResponse, .3, -1, 1);
            b.Permeability = Mut(b.Permeability, .15, 0, 1);
            b.StretchResponse = Mut(b.StretchResponse, .3, -1, 1);
            b.SignalDelay = Mut(b.SignalDelay, .2, .05, 3);
            b.Toughness = Mut(b.Toughness, .2, 0, 1);
            b.Repair = Mut(b.Repair, .2, 0, 1);
            b.TouchResponse = Mut(b.TouchResponse, .35, -1, 1);
            b.FoodResponse = Mut(b.FoodResponse, .35, -1, 1);
            b.LightResponse = Mut(b.LightResponse, .35, -1, 1);
            b.Adhesion = Mut(b.Adhesion, .22, 0, 1);
            b.Digestion = Mut(b.Digestion, .22, 0, 1);
            b.Photo = Mut(b.Photo, .22, 0, 1);
            b.Delay = Mut(b.Delay, 5, 0, 100);
            b.Maturation = Mut(b.Maturation, 8, 10, 150);
        }
        // Roots, serial joints, and forks arise through the same insertion operation.
        if (r.NextDouble() < .22 && buds.Count + g.Cells < Development.NodeBudget)
        {
            int source = buds.Count == 0 ? -1 : (int)(r.NextDouble() * buds.Count);
            var b = source < 0 ? new BudGene() : buds[source].Copy();
            b.Parent = source >= 0 && r.NextDouble() < .58 ? source : -1;
            if (b.Parent < 0) { b.AnchorAngle = r.NextDouble() * Math.Tau; b.AnchorCell = (int)(r.NextDouble() * g.Cells); b.AnchorPitch = (r.NextDouble() - .5) * 2; }
            b.Turn += (r.NextDouble() - .5) * 1.6; b.Phase += (r.NextDouble() - .5) * 2;
            b.Length *= .5 + r.NextDouble() * .6;
            if (buds.Sum(part => part.Wires.Count) + b.Wires.Count <= WireBudget) buds.Add(b);
        }
        if (buds.Count > 0 && r.NextDouble() < .08)
        {
            RemoveBud(g, (int)(r.NextDouble() * buds.Count));
        }
        if (buds.Count > 1 && r.NextDouble() < .08)
        {
            int i = 1 + (int)(r.NextDouble() * (buds.Count - 1));
            buds[i].Parent = (int)(r.NextDouble() * (i + 1)) - 1;
        }
        if (buds.Count > 0 && r.NextDouble() < .07) DuplicateStructure(g, (int)(r.NextDouble() * buds.Count), r);
        MutateWiring(g, r);
    }

    public static void Develop(Organism o, double dt)
    {
        var buds = o.Genes.Buds!;
        while (o.BudStates.Count < buds.Count) o.BudStates.Add(new());
        for (int i = 0; i < buds.Count; i++)
        {
            var b = buds[i]; var state = o.BudStates[i];
            bool ready = b.Parent >= 0 ? o.BudStates[b.Parent].Growth > .35 && o.BudStates[b.Parent].Integrity > .3 : b.AnchorCell < o.Body.Count && o.Body[b.AnchorCell].Integrity > .15;
            if (!ready || state.Integrity <= .02) continue;
            state.FormedAt ??= o.Age;
            double t = Math.Clamp((o.Age - state.FormedAt.Value - b.Delay) / b.Maturation, 0, 1);
            double target = t * t * (3 - 2 * t);
            double cost = Math.Max(.1, b.Length * b.Width * b.Width * o.Genes.Radius * 2);
            double addition = Math.Min(Math.Min(dt * 1.5 / b.Maturation, Math.Max(0, target - state.Growth)), Math.Max(0, o.Energy - 8) / cost);
            state.Growth += addition; o.Energy -= addition * cost;
        }
    }

    private readonly record struct PoseBasis(double Radius, double Muscle, double Length, V Root, V Axis, V Sideways, V Vertical);

    public static void Poses(Organism o, double age, Span<BudPose> poses, double waterLevel = double.MaxValue)
        => Poses(o, age, poses, waterLevel, [], false);

    private static void Poses(Organism o, double age, Span<BudPose> poses, double waterLevel, Span<PoseBasis> basis, bool reuse)
    {
        var buds = o.Genes.Buds!;
        V forward = V.Direction(o.Heading, o.Pitch), side = new(-Math.Sin(o.Heading), Math.Cos(o.Heading), 0);
        V up = Cross(forward, side);
        double reserves = Math.Clamp(o.Energy / Math.Max(1, o.Capacity * .25), .05, 1);
        double contraction = .35 + o.Genes.Contract * .65, phaseBase = age * o.Genes.Rhythm * 2;
        for (int i = 0; i < buds.Count; i++)
        {
            var b = buds[i];
            if (i >= o.BudStates.Count || o.BudStates[i].Growth <= 0 || o.BudStates[i].Integrity <= .02 || (b.Parent >= 0 ? !poses[b.Parent].Active : b.AnchorCell >= o.Body.Count)) { poses[i] = default; continue; }
            var state = o.BudStates[i];
            int anchor = b.Parent >= 0 ? poses[b.Parent].Anchor : b.AnchorCell;
            double radius = reuse ? basis[i].Radius : o.Genes.CellRadius(anchor), growth = state.Growth;
            V axis, root;
            if (b.Parent >= 0) { var parent = poses[b.Parent]; root = parent.Tip; axis = Unit(parent.Tip - parent.Root); }
            else if (reuse) { axis = basis[i].Axis; root = basis[i].Root; }
            else
            {
                V localAxis = b.AnchorDirection();
                axis = forward * localAxis.X + side * localAxis.Y + up * localAxis.Z;
                double along = Dot(axis, forward) / o.Genes.Elongation, lateral = Dot(axis, side) / .95, height = Dot(axis, up) / o.Genes.Flattening;
                double skinRadius = radius / Math.Sqrt(along * along + lateral * lateral + height * height);
                root = o.Body[anchor].Position + axis * (skinRadius * .94);
            }
            V sideways, vertical;
            if (reuse && b.Parent < 0) { sideways = basis[i].Sideways; vertical = basis[i].Vertical; }
            else
            {
                sideways = Unit(Cross(new V(0, 0, 1), axis));
                if (sideways.Length < .5) sideways = side;
                vertical = Unit(Cross(axis, sideways));
            }
            double phase = phaseBase * b.Frequency + o.Phase + b.Phase + state.PhaseOffset + (age - o.Age) * state.PhaseRate;
            double muscle = reuse ? basis[i].Muscle : b.Muscle * contraction * state.Integrity * reserves
                * Math.Clamp(1 + b.MotorResponse * state.Activation, 0, 2) / (1 + state.Load * (1 - b.Stiffness) * .3);
            double turn = b.Turn + Math.Sin(phase) * muscle * .65 + state.Activation * muscle * .35;
            double pitch = b.Elevation + Math.Sin(phase + b.BendPhase) * muscle * b.BendAmplitude;
            double length = reuse ? basis[i].Length : radius * b.Length * growth;
            if (!reuse && basis.Length > 0) basis[i] = new(radius, muscle, length, root, axis, sideways, vertical);
            V direction = axis * (Math.Cos(turn) * Math.Cos(pitch)) + sideways * (Math.Sin(turn) * Math.Cos(pitch)) + vertical * Math.Sin(pitch);
            double dry = Math.Clamp((root.Z - waterLevel + radius) / (radius * 2), 0, 1);
            V sag = new(0, 0, -dry * (1 - b.Stiffness) * length * length / (radius * 8));
            V tip = root + direction * length + sag;
            V joint = root + (tip - root) * .5 + vertical * (Math.Sin(phase + b.BendPhase) * b.BendAmplitude * (1 - b.Stiffness) * length * muscle * .14);
            poses[i] = new(root, joint, tip, radius * b.Width * growth * (.4 + .6 * state.Integrity), b.Flatness, anchor, true);
        }
    }

    public static double Extent(Organism o)
    {
        if (o.Genes.Buds == null) return o.Genes.Radius * Appendages.Reach(o) * 4;
        Span<double> lengths = stackalloc double[o.Genes.Buds.Count]; double longest = 0;
        for (int i = 0; i < lengths.Length; i++) { var b = o.Genes.Buds[i]; lengths[i] = b.Length * (i < o.BudStates.Count ? o.BudStates[i].Growth : 0) + (b.Parent >= 0 ? lengths[b.Parent] : 0); longest = Math.Max(longest, lengths[i]); }
        return longest * o.Genes.Radius;
    }

    public static V Solve(World w, Organism o, double dt, IReadOnlyList<Organism>? neighbors, V? freeMotion = null)
    {
        var buds = o.Genes.Buds!;
        Develop(o, dt);
        Span<double> signals = stackalloc double[buds.Count];
        Span<double> childSignals = stackalloc double[buds.Count];
        Span<int> childCounts = stackalloc int[buds.Count];
        Span<double> anchorSignals = stackalloc double[o.Body.Count];
        Span<int> anchorCounts = stackalloc int[o.Body.Count];
        childSignals.Clear(); childCounts.Clear(); anchorSignals.Clear(); anchorCounts.Clear();
        V center = o.Center;
        double daylight = w.Daylight / (1 + (neighbors?.Count ?? 0) * .12);
        for (int n = 0; n < buds.Count; n++)
        {
            var state = o.BudStates[n];
            state.PhaseOffset = Math.IEEERemainder(state.PhaseOffset + dt * state.PhaseRate, Math.Tau);
            signals[n] = Math.Clamp(state.Signal + buds[n].ContractionSense * state.Contraction, -1, 1);
            if (state.Growth > 0 && state.Integrity > .02)
            {
                if (buds[n].Parent >= 0) { childSignals[buds[n].Parent] += signals[n]; childCounts[buds[n].Parent]++; }
                else if (buds[n].AnchorCell < o.Body.Count) { anchorSignals[buds[n].AnchorCell] += signals[n]; anchorCounts[buds[n].AnchorCell]++; }
            }
            if (state.Integrity < 1 && w.Time - state.LastDamage > 8 && o.Energy > o.Capacity * .4)
            {
                double repair = Math.Min(1 - state.Integrity, dt * (.003 + buds[n].Repair * .01));
                state.Integrity += repair; o.Energy -= repair * (4 + buds[n].Length * buds[n].Width * buds[n].Width * o.Genes.Radius * 2);
            }
        }
        Span<BudPose> poses = stackalloc BudPose[buds.Count];
        Span<BudPose> next = stackalloc BudPose[buds.Count];
        Span<PoseBasis> basis = stackalloc PoseBasis[buds.Count];
        Poses(o, o.Age, poses, w.WaterLevel, basis, false);
        Poses(o, o.Age + dt, next, w.WaterLevel, basis, true);
        // Read every source before updating any destination: recurrence has a real one-step delay.
        Span<double> wiredInputs = stackalloc double[buds.Count]; wiredInputs.Clear();
        for (int i = 0; i < buds.Count; i++)
        {
            if (!poses[i].Active) continue;
            foreach (var wire in buds[i].Wires)
            {
                if (!poses[wire.Source].Active) continue;
                var source = o.BudStates[wire.Source];
                wiredInputs[i] += wire.Weight * (signals[wire.Source] * (1 - wire.MemoryMix) + source.Memory * wire.MemoryMix)
                    * source.Integrity;
                double distance = (poses[i].Joint - poses[wire.Source].Joint).Length / Math.Max(1, o.Genes.Radius);
                o.Energy -= dt * (.001 + distance * .0003 + Math.Abs(wire.Weight) * .001);
            }
        }
        V free = freeMotion ?? new V();
        double bodyDrag = 1 + o.Body.Count * .65, torque = 0;
        Resistance resistance = Resistance.Isotropic(bodyDrag);
        V drive = free * bodyDrag;
        Span<Resistance> fluidDrags = stackalloc Resistance[buds.Count], groundDrags = stackalloc Resistance[buds.Count];
        Span<V> fluidVelocities = stackalloc V[buds.Count], tipVelocities = stackalloc V[buds.Count], flows = stackalloc V[buds.Count], supports = stackalloc V[buds.Count], contactNormals = stackalloc V[buds.Count];
        fluidDrags.Clear(); groundDrags.Clear(); fluidVelocities.Clear(); tipVelocities.Clear(); flows.Clear(); supports.Clear(); contactNormals.Clear();
        o.LocomotionPower = 0; o.ContactSlip = 0;
        o.GroundContacts = 0;
        for (int i = 0; i < buds.Count; i++)
        {
            var b = buds[i]; var p = poses[i]; var state = o.BudStates[i];
            if (!p.Active) continue;
            double length = (p.Tip - p.Root).Length, area = Math.Max(.001, length * p.Radius / (o.Genes.Radius * o.Genes.Radius));
            double volume = area * b.Width * b.Flatness;
            V velocity = tipVelocities[i] = (next[i].Tip - p.Tip) * (1 / dt);
            fluidVelocities[i] = (next[i].Joint - p.Joint) * (1 / dt);
            double immersion = (w.Immersion(p.Joint, Math.Max(.1, p.Radius)) + w.Immersion(p.Tip, Math.Max(.1, p.Radius))) * .5;
            fluidDrags[i] = TissueMechanics.WaterDrag(p, area, immersion);
            flows[i] = w.FlowAt(p.Joint);
            resistance += fluidDrags[i];
            drive += fluidDrags[i].Apply(flows[i] - fluidVelocities[i]);
            state.Contact = w.SurfaceContact(p.Tip, p.Radius * b.Flatness, out var normal, out double penetration, out var touchedRock);
            state.Stretch = Math.Clamp(Math.Max(0, penetration) / Math.Max(1, length) + state.Load * .2, 0, 1);
            if (state.Contact)
            {
                contactNormals[i] = normal;
                double strength = state.Integrity * Math.Clamp(b.Stiffness * b.Width * b.Width * 20 / Math.Max(.2, b.Length), 0, 1);
                double compression = Math.Clamp(penetration / Math.Max(.2, p.Radius), 0, 1);
                double grip = compression * strength * (.35 + b.Adhesion) * Math.Sqrt(area);
                groundDrags[i] = TissueMechanics.GroundDrag(normal, grip * 2);
                supports[i] = normal * (Math.Min(12, penetration * 2) * strength);
                resistance += groundDrags[i];
                drive += supports[i] - groundDrags[i].Apply(velocity);
                o.Anchoring += grip * b.Adhesion;
                o.GroundContacts++;
                // Tangential scraping and a firm narrow edge detach attached food.
                double scrape = Math.Min(2, state.Slip / 5) * strength / Math.Max(.25, b.Taper);
                double bite = w.GrazeContact(p.Tip, dt * .4 * area * (1 - b.Digestion * .4) * (.25 + scrape), touchedRock); o.ShoreEnergy += bite * 1.7; o.Energy += bite * 1.7;
                w.DrinkFood(o, bite * 1.7, w.MoistureAt(p.Tip));
                w.AbsorbContactWater(o, p.Tip, p.Radius * b.Flatness, area * (.2 + 1.6 * b.Permeability), dt);
                if (bite > .003) o.LastMeal = w.Time;
            }
            int tile = w.Tile(p.Tip);
            double wet = w.Immersion(p.Tip, Math.Max(.1, p.Radius));
            // Broad adhesive surfaces retain particles; rapid flow washes poorly held food past.
            double relativeFlow = (flows[i] - free - fluidVelocities[i]).Length;
            double retention = Math.Clamp((.2 + b.Adhesion * 1.6) / Math.Sqrt(b.Flatness)
                / (1 + relativeFlow * .08 * (1 - b.Adhesion)), .05, 2);
            double cutting = b.Stiffness * b.Muscle * state.Integrity * Math.Min(2, (velocity + free).Length / 5)
                / Math.Max(.25, b.Taper);
            double captured = w.Capture(p.Root, p.Joint, p.Radius, dt * area * .7 * retention, b.Digestion)
                + w.Capture(p.Joint, p.Tip, p.Radius, dt * area * .7 * retention, b.Digestion)
                + w.Capture(p.Tip, next[i].Tip, p.Radius, dt * area * .3 * retention, b.Digestion)
                + w.CaptureRemains(p.Tip, p.Radius, dt * area * b.Digestion, cutting + b.Adhesion);
            o.GrazingEnergy += captured; o.Energy += captured; w.DrinkFood(o, captured);
            if (captured > .003) o.LastMeal = w.Time;
            double light = w.LightAt(p.Tip) * daylight;
            double photo = Math.Min(w.Carbon[tile] * 4, dt * .12 * area * b.Photo * light / Math.Sqrt(b.Flatness));
            w.Carbon[tile] -= photo * .25; w.Oxygen[tile] = Math.Min(2, w.Oxygen[tile] + photo * .22);
            o.LightEnergy += photo; o.Energy += photo;
            if (neighbors != null && b.Digestion > .05)
                for (int neighbor = 0, countNeighbors = neighbors.Count; neighbor < countNeighbors; neighbor++)
                {
                    var other = neighbors[neighbor];
                    if (other.Energy <= 0 || other == o || b.Digestion * (1 + cutting) <= other.Genes.Membrane) continue;
                    bool touching = w.TissueContact(other, p.Tip, p.Radius);
                    if (!touching) continue;
                    double stolen = Math.Min(other.Energy, dt * .8 * area * Math.Max(0, b.Digestion - other.Genes.Membrane / (1 + cutting)) * (.25 + b.Adhesion));
                    w.DrinkFood(o, stolen * .7, other.Hydration);
                    other.Energy -= stolen; w.DamageTissue(other, p.Tip, stolen * 2); o.Energy += stolen * .7; o.TissueEnergy += stolen * .7;
                    if (stolen > .002)
                    {
                        other.ThreatDirection = Unit(other.Center - p.Tip); other.Irritation = Math.Min(2, other.Irritation + stolen * 4);
                        if (w.Time - o.LastAttack > 3) w.TissueMeals++;
                        o.LastAttack = w.Time; other.LastAttack = w.Time;
                    }
                }
            // Local reflex changes activation, never the genotype or number of parts.
            double food = 0;
            if (b.FoodResponse != 0)
            {
                food = Math.Clamp((w.FoodSignal(p.Tip, o.Genes) - w.FoodSignal(p.Root, o.Genes)) * .3, -1, 1);
                food = Math.Clamp(food + w.FoodContactSignal(p.Tip) * .3, -1, 1);
            }
            double children = childSignals[i]; int count = childCounts[i];
            double peers = 0;
            if (b.Parent < 0 && b.AnchorCell < o.Body.Count && anchorCounts[b.AnchorCell] > 1)
                peers = (anchorSignals[b.AnchorCell] - signals[i]) / (anchorCounts[b.AnchorCell] - 1);
            double input = wiredInputs[i] + b.TouchResponse * (state.Contact ? 1 : 0) + b.FoodResponse * food + b.LightResponse * light + b.StretchResponse * state.Stretch
                + (b.Parent >= 0 ? signals[b.Parent] * b.Coupling : 0) + (count > 0 ? children / count * b.ChildCoupling : 0) + peers * b.NeighborCoupling
                + b.MemoryResponse * state.Memory + b.RecurrentResponse * state.Signal
                + b.HungerResponse * (1 - Math.Clamp(o.Energy / o.Capacity, 0, 1))
                + b.SlipResponse * Math.Tanh(state.Slip * .2)
                + b.MoistureResponse * (wet - .5);
            state.Memory += (state.Signal - state.Memory) * (1 - Math.Exp(-dt / b.MemoryTime));
            state.Signal += (Math.Clamp(input + state.Activation * .15, -1, 1) - state.Signal) * Math.Min(1, dt / Math.Max(.05, b.SignalDelay));
            state.Activation += (state.Signal - state.Activation) * Math.Min(1, dt * 3);
            state.PhaseRate = b.PhaseResponse * state.Signal * 2;
            state.Contraction = Math.Sin(o.Age * o.Genes.Rhythm * 2 * b.Frequency + o.Phase + b.Phase + state.PhaseOffset) * b.Muscle * state.Integrity;
            double sensors = Math.Abs(b.TouchResponse) + Math.Abs(b.FoodResponse) + Math.Abs(b.LightResponse) + Math.Abs(b.StretchResponse) + Math.Abs(b.ContractionSense) + Math.Abs(b.NeighborCoupling) + Math.Abs(b.MemoryResponse) + Math.Abs(b.RecurrentResponse) + Math.Abs(b.HungerResponse) + Math.Abs(b.SlipResponse) + Math.Abs(b.MoistureResponse);
            o.Energy -= dt * (volume * (.035 + b.Stiffness * .02 + b.Toughness * .04 + b.Repair * .01 + Math.Abs(b.Permeability - .5) * .015) + area * (.025 * b.Muscle * b.Muscle + .012 * b.Photo + .012 * b.Digestion + .01 * b.Adhesion + sensors * .005));
        }
        V motion = resistance.Solve(drive);
        if (motion.Length > 40) motion *= 40 / motion.Length; // Numerical guard, not a fitness target.
        for (int i = 0; i < buds.Count; i++)
        {
            var p = poses[i]; if (!p.Active) continue;
            var state = o.BudStates[i]; var b = buds[i];
            V waterForce = fluidDrags[i].Apply(flows[i] - motion - fluidVelocities[i]);
            V groundForce = groundDrags[i].Apply((motion + tipVelocities[i]) * -1) + supports[i];
            V force = waterForce + groundForce;
            state.Load = Math.Clamp(force.Length / (.5 + b.Width * b.Width * 12), 0, 8);
            V contactVelocity = motion + tipVelocities[i];
            state.Slip = state.Contact ? (contactVelocity - contactNormals[i] * Dot(contactVelocity, contactNormals[i])).Length : 0;
            o.ContactSlip += state.Slip;
            double work = Math.Max(0, -Dot(waterForce, fluidVelocities[i])) + Math.Max(0, -Dot(groundForce, tipVelocities[i]));
            double power = work * .008 + state.Load * b.Muscle * .002;
            o.LocomotionPower += power; o.Energy -= power * dt;
            V lever = p.Joint - center;
            torque += (lever.X * force.Y - lever.Y * force.X) / (o.Genes.Radius * o.Genes.Radius * 12);
        }
        if (o.GroundContacts > 0) o.ContactSlip /= o.GroundContacts;
        o.Energy = Math.Min(o.Capacity, o.Energy);
        o.Heading += Math.Clamp(torque / bodyDrag, -.8, .8) * dt;
        return motion - free;
    }
}
