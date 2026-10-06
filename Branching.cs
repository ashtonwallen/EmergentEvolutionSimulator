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
    public BudGene Copy() => (BudGene)MemberwiseClone();
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
    public double Stretch { get; set; }
    public BudState Copy() => (BudState)MemberwiseClone();
}

public readonly record struct BudPose(V Root, V Joint, V Tip, double Radius, double Flatness, int Anchor, bool Active);

public static class Branching
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
        if (g.Buds.Count + g.Cells > Development.NodeBudget) return false;
        for (int i = 0; i < g.Buds.Count; i++)
        {
            var b = g.Buds[i];
            if (b.Parent < -1 || b.Parent >= i || b.AnchorCell < 0 || b.AnchorCell >= g.Cells || !double.IsFinite(b.Length + b.Width + b.Flatness + b.Turn + b.Elevation + b.AnchorAngle + b.AnchorPitch + b.Muscle + b.Phase + b.Frequency + b.Coupling + b.Stiffness + b.TouchResponse + b.FoodResponse + b.LightResponse + b.Digestion + b.Photo + b.Adhesion + b.Delay + b.Maturation + b.Taper + b.ChildCoupling + b.StretchResponse + b.SignalDelay + b.Toughness + b.Repair) || b.Length <= 0 || b.Width <= 0 || b.Flatness <= 0 || b.Maturation <= 0) return false;
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
            buds.Add(b);
        }
        if (buds.Count > 0 && r.NextDouble() < .08)
        {
            int removed = (int)(r.NextDouble() * buds.Count), parent = buds[removed].Parent;
            buds.RemoveAt(removed);
            foreach (var b in buds) { if (b.Parent == removed) b.Parent = parent; else if (b.Parent > removed) b.Parent--; }
        }
        if (buds.Count > 1 && r.NextDouble() < .08)
        {
            int i = 1 + (int)(r.NextDouble() * (buds.Count - 1));
            buds[i].Parent = (int)(r.NextDouble() * (i + 1)) - 1;
        }
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

    public static void Poses(Organism o, double age, Span<BudPose> poses, double waterLevel = double.MaxValue)
    {
        var buds = o.Genes.Buds!;
        V forward = V.Direction(o.Heading, o.Pitch), side = new(-Math.Sin(o.Heading), Math.Cos(o.Heading), 0);
        V up = Cross(forward, side);
        for (int i = 0; i < buds.Count; i++)
        {
            var b = buds[i];
            if (i >= o.BudStates.Count || o.BudStates[i].Growth <= 0 || o.BudStates[i].Integrity <= .02 || (b.Parent >= 0 ? !poses[b.Parent].Active : b.AnchorCell >= o.Body.Count)) { poses[i] = default; continue; }
            var state = o.BudStates[i];
            int anchor = b.Parent >= 0 ? poses[b.Parent].Anchor : b.AnchorCell;
            double radius = o.Genes.CellRadius(anchor), growth = state.Growth;
            V axis, root;
            if (b.Parent >= 0) { var parent = poses[b.Parent]; root = parent.Tip; axis = Unit(parent.Tip - parent.Root); }
            else
            {
                axis = forward * (Math.Cos(b.AnchorAngle) * Math.Cos(b.AnchorPitch)) + side * (Math.Sin(b.AnchorAngle) * Math.Cos(b.AnchorPitch)) + up * Math.Sin(b.AnchorPitch);
                double along = Dot(axis, forward) / o.Genes.Elongation, lateral = Dot(axis, side) / .95, height = Dot(axis, up) / o.Genes.Flattening;
                double skinRadius = radius / Math.Sqrt(along * along + lateral * lateral + height * height);
                root = o.Body[anchor].Position + axis * (skinRadius * .94);
            }
            V sideways = Unit(Cross(new V(0, 0, 1), axis));
            if (sideways.Length < .5) sideways = side;
            V vertical = Unit(Cross(axis, sideways));
            double parentSignal = b.Parent < 0 ? 0 : Math.Sin(age * o.Genes.Rhythm * 2 * buds[b.Parent].Frequency + o.Phase + buds[b.Parent].Phase);
            double phase = age * o.Genes.Rhythm * 2 * b.Frequency + o.Phase + b.Phase + b.Coupling * parentSignal;
            double muscle = b.Muscle * (.35 + o.Genes.Contract * .65) * state.Integrity;
            double turn = b.Turn + Math.Sin(phase) * muscle * .65 + state.Activation * .35;
            double lift = Math.Max(0, Math.Cos(phase)) * muscle * .65;
            double pitch = b.Elevation + lift + state.Activation * .2;
            double length = radius * b.Length * growth;
            V direction = axis * (Math.Cos(turn) * Math.Cos(pitch)) + sideways * (Math.Sin(turn) * Math.Cos(pitch)) + vertical * Math.Sin(pitch);
            double dry = Math.Clamp((root.Z - waterLevel + radius) / (radius * 2), 0, 1);
            V sag = new(0, 0, -dry * (1 - b.Stiffness) * length * length / (radius * 8));
            V tip = root + direction * length + sag;
            V joint = root + (tip - root) * .5 + vertical * (Math.Sin(phase + .7) * (1 - b.Stiffness) * length * .14);
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

    public static V Solve(World w, Organism o, double dt, IReadOnlyList<Organism>? neighbors)
    {
        var buds = o.Genes.Buds!;
        Develop(o, dt);
        Span<double> signals = stackalloc double[buds.Count];
        Span<double> childSignals = stackalloc double[buds.Count];
        Span<int> childCounts = stackalloc int[buds.Count];
        childSignals.Clear(); childCounts.Clear();
        V center = o.Center;
        double daylight = Shoreline.Daylight(w.Time) / (1 + (neighbors?.Count ?? 0) * .12);
        for (int n = 0; n < buds.Count; n++)
        {
            var state = o.BudStates[n]; signals[n] = state.Signal;
            if (buds[n].Parent >= 0) { childSignals[buds[n].Parent] += state.Signal; childCounts[buds[n].Parent]++; }
            if (state.Integrity < 1 && w.Time - state.LastDamage > 8 && o.Energy > o.Capacity * .4)
            {
                double repair = Math.Min(1 - state.Integrity, dt * (.003 + buds[n].Repair * .01));
                state.Integrity += repair; o.Energy -= repair * (4 + buds[n].Length * buds[n].Width * buds[n].Width * o.Genes.Radius * 2);
            }
        }
        Span<BudPose> poses = stackalloc BudPose[buds.Count];
        Span<BudPose> next = stackalloc BudPose[buds.Count];
        Poses(o, o.Age, poses, w.WaterLevel); Poses(o, o.Age + dt, next, w.WaterLevel);
        V reaction = new(); double support = 0, torque = 0, mass = 1 + o.Body.Count * .65;
        o.GroundContacts = 0;
        for (int i = 0; i < buds.Count; i++)
        {
            var b = buds[i]; var p = poses[i]; var state = o.BudStates[i];
            if (!p.Active) continue;
            double length = (p.Tip - p.Root).Length, area = Math.Max(.001, length * p.Radius / (o.Genes.Radius * o.Genes.Radius));
            double volume = area * b.Width * b.Flatness; mass += volume * .3;
            V velocity = (next[i].Tip - p.Tip) * (1 / dt);
            V force = new(); state.Contact = w.SurfaceContact(p.Tip, p.Radius * b.Flatness, out var normal, out double penetration, out _);
            state.Stretch = Math.Clamp(penetration / Math.Max(1, length), 0, 1);
            if (state.Contact)
            {
                double strength = state.Integrity * Math.Clamp(b.Stiffness * b.Width * b.Width * 20 / Math.Max(.2, b.Length), 0, 1);
                double grip = Math.Clamp(penetration / Math.Max(.2, p.Radius), 0, 1) * strength;
                V tangent = velocity - normal * Dot(velocity, normal);
                force = tangent * (-grip * (.35 + b.Adhesion));
                force += normal * (Math.Min(12, penetration * 2) * strength);
                support += Math.Min(8, penetration * 2) * strength * Math.Max(0, normal.Z);
                o.Anchoring += grip * b.Adhesion;
                o.GroundContacts++;
                int surface = w.SurfaceTile(p.Tip);
                double bite = w.GrazeSurface(p.Tip, p.Radius * b.Flatness, dt * .4 * area * (1 - b.Digestion * .4)); o.ShoreEnergy += bite * 1.7; o.Energy += bite * 1.7;
                if (bite > .003) o.LastMeal = w.Time;
            }
            else if (p.Tip.Z < w.WaterLevel)
            {
                V relative = velocity + o.Velocity - w.FlowAt(p.Tip);
                V axis = Unit(p.Tip - p.Root);
                V along = axis * Dot(relative, axis), across = relative - along;
                double stroke = .25 + .75 * Math.Max(0, Math.Sin(o.Age * o.Genes.Rhythm * 2 * b.Frequency + o.Phase + b.Phase));
                force -= (along * .08 + across * (stroke / Math.Max(.25, b.Flatness))) * (area * .35 * state.Integrity);
            }
            reaction += force;
            V lever = p.Tip - center;
            torque += (lever.X * force.Y - lever.Y * force.X) / (o.Genes.Radius * o.Genes.Radius * 12);
            int tile = w.Tile(p.Tip);
            double wet = w.Immersion(p.Tip, Math.Max(.1, p.Radius));
            double captured = w.Capture(p.Root, p.Joint, p.Radius, dt * area * .7, b.Digestion)
                + w.Capture(p.Joint, p.Tip, p.Radius, dt * area * .7, b.Digestion)
                + w.Capture(p.Tip, next[i].Tip, p.Radius, dt * area * .3, b.Digestion)
                + w.CaptureRemains(p.Tip, p.Radius, dt * area * b.Digestion);
            o.GrazingEnergy += captured; o.Energy += captured;
            if (captured > .003) o.LastMeal = w.Time;
            double light = w.LightAt(p.Tip) * daylight;
            double photo = Math.Min(w.Carbon[tile] * 4, dt * .12 * area * b.Photo * light / Math.Sqrt(b.Flatness));
            w.Carbon[tile] -= photo * .25; w.Oxygen[tile] = Math.Min(2, w.Oxygen[tile] + photo * .22);
            o.LightEnergy += photo; o.Energy += photo;
            if (neighbors != null && b.Digestion > .05)
                foreach (var other in neighbors)
                {
                    if (other.Energy <= 0) continue;
                    bool touching = w.TissueContact(other, p.Tip, p.Radius);
                    if (!touching) continue;
                    double stolen = Math.Min(other.Energy, dt * .8 * area * Math.Max(0, b.Digestion - other.Genes.Membrane));
                    other.Energy -= stolen; w.DamageTissue(other, p.Tip, stolen * 2); o.Energy += stolen * .7; o.TissueEnergy += stolen * .7;
                    if (stolen > .002)
                    {
                        other.ThreatDirection = Unit(other.Center - p.Tip); other.Irritation = Math.Min(2, other.Irritation + stolen * 4);
                        if (w.Time - o.LastAttack > 3) w.TissueMeals++;
                        o.LastAttack = w.Time; other.LastAttack = w.Time;
                    }
                }
            // Local reflex changes activation, never the genotype or number of parts.
            double food = Math.Clamp((w.FoodSignal(p.Tip, o.Genes) - w.FoodSignal(p.Root, o.Genes)) * .3, -1, 1);
            food = Math.Clamp(food + w.FoodContactSignal(p.Tip) * .3, -1, 1);
            double children = childSignals[i]; int count = childCounts[i];
            double input = b.TouchResponse * (state.Contact ? 1 : 0) + b.FoodResponse * food + b.LightResponse * light + b.StretchResponse * state.Stretch
                + (b.Parent >= 0 ? signals[b.Parent] * b.Coupling : 0) + (count > 0 ? children / count * b.ChildCoupling : 0);
            state.Signal += (Math.Clamp(input + state.Activation * .15, -1, 1) - state.Signal) * Math.Min(1, dt / Math.Max(.05, b.SignalDelay));
            state.Activation += (state.Signal - state.Activation) * Math.Min(1, dt * 3);
            double sensors = Math.Abs(b.TouchResponse) + Math.Abs(b.FoodResponse) + Math.Abs(b.LightResponse);
            o.Energy -= dt * (volume * (.035 + b.Stiffness * .02 + b.Toughness * .04 + b.Repair * .01) + area * (.025 * b.Muscle * b.Muscle + .012 * b.Photo + .012 * b.Digestion + .01 * b.Adhesion + sensors * .005));
        }
        o.Energy = Math.Min(o.Capacity, o.Energy);
        o.Heading += Math.Clamp(torque / mass, -.8, .8) * dt;
        reaction *= 1 / mass;
        if (reaction.Length > 18) reaction *= 18 / reaction.Length;
        return reaction + new V(0, 0, Math.Min(18, support / mass));
    }
}
