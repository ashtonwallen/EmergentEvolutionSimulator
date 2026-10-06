namespace Tideglass;

// Shared by collision, resources, and the renderer. Heights are simulation units.
public static class Shoreline
{
    public static double WaterLevel(double time) => 218 + 34 * Math.Sin(time * Math.Tau / 360);
    public static double Daylight(double time) => .3 + .7 * Math.Pow(.5 + .5 * Math.Cos(time * Math.Tau / 780), .7);
    public static double Sediment(double x, double y) => Math.Exp(-Math.Pow((x - 1810) / 240, 2) - Math.Pow((y - 1140) / 210, 2));
    private const int TerrainSpacing = 5;
    private const int TerrainCols = (int)World.Width / TerrainSpacing + 1;
    private const int TerrainRows = (int)World.Height / TerrainSpacing + 1;
    private static readonly Lazy<double[]> terrain = new(() =>
    {
        var heights = new double[TerrainCols * TerrainRows];
        for (int y = 0; y < TerrainRows; y++) for (int x = 0; x < TerrainCols; x++)
            heights[y * TerrainCols + x] = GroundAnalytic(x * TerrainSpacing, y * TerrainSpacing);
        return heights;
    });
    public static double Ground(double x, double y)
    {
        x = Math.Clamp(x / TerrainSpacing, 0, TerrainCols - 1);
        y = Math.Clamp(y / TerrainSpacing, 0, TerrainRows - 1);
        int ix = Math.Min((int)x, TerrainCols - 2), iy = Math.Min((int)y, TerrainRows - 2);
        double fx = x - ix, fy = y - iy; int i = iy * TerrainCols + ix;
        var h = terrain.Value;
        return (h[i] * (1 - fx) + h[i + 1] * fx) * (1 - fy)
            + (h[i + TerrainCols] * (1 - fx) + h[i + TerrainCols + 1] * fx) * fy;
    }
    private static double GroundAnalytic(double x, double y)
    {
        x /= World.HabitatScale; y /= World.HabitatScale;
        double ramp = Math.Clamp((x - 450) / 1220, 0, 1);
        double coast = 12 + 278 * ramp + 15 * Math.Sin(y / 145 + x / 260) + 7 * Math.Sin(x / 63) * Math.Cos(y / 85);
        double pool = 58 * Math.Exp(-Math.Pow((x - 1370) / 135, 2) - Math.Pow((y - 460) / 190, 2));
        double ridge = 35 * Math.Exp(-Math.Pow((x - 1020) / 160, 2) - Math.Pow((y - 820) / 150, 2));
        double channel = 24 * Math.Exp(-Math.Pow((y - (480 + 60 * Math.Sin(x / 167))) / 38, 2)) * Math.Exp(-Math.Pow((x - 1050) / 430, 4));
        double pools = 20 * Math.Exp(-Math.Pow((x - 1510) / 70, 2) - Math.Pow((y - 780) / 85, 2));
        return Math.Clamp(coast - pool + ridge - channel - pools, 4, 310);
    }
}

public readonly record struct OutgrowthPose(V Root, V Joint, V Tip, double Radius);

// Two-segment tissue outgrowths. The same articulated geometry drives contact and rendering.
// Overdamped contact/fluid forces keep this inexpensive; this is not a rigid-body solver.
public static class Appendages
{
    public static double Reach(Organism o)
    {
        if (o.Genes.Buds != null) return Branching.Extent(o) / (o.Genes.Radius * 4);
        double reach = 0;
        for (int n = 0; n < Sites(o); n++) reach = Math.Max(reach, o.Genes.Rule(n).Outgrowth);
        return reach;
    }
    public static int Sites(Organism o) => Math.Min(8, o.Body.Count);
    public static OutgrowthPose Pose(Organism o, int site, int side, double age)
    {
        var g = o.Genes; var rule = g.Rule(site);
        double r = g.CellRadius(site), growth = Math.Clamp((age - (o.Body[site].FormedAt ?? 0)) / 35, 0, 1);
        double length = r * rule.Outgrowth * 4 * growth;
        double phase = age * g.Rhythm * 2 + o.Phase - site * rule.Phase + (side < 0 ? Math.PI : 0);
        double stroke = Math.Sin(phase) * g.Contract * length * .65;
        double lift = Math.Max(0, Math.Cos(phase)) * g.Contract * length * .55;
        V f = V.Direction(o.Heading, o.Pitch), s = new(-Math.Sin(o.Heading), Math.Cos(o.Heading), 0);
        V u = new(-Math.Cos(o.Heading) * Math.Sin(o.Pitch), -Math.Sin(o.Heading) * Math.Sin(o.Pitch), Math.Cos(o.Pitch));
        V root = o.Body[site].Position + s * (side * r * .65);
        V joint = root + s * (side * length * .50) + f * (stroke * .4) - u * (length * rule.Bend * .22);
        V tip = root + s * (side * length * .78) + f * stroke - u * (length * rule.Bend - lift);
        return new(root, joint, tip, r * (.14 + rule.Paddle * .20) * Math.Min(1, rule.Outgrowth * 3) * growth);
    }

    public static V Solve(World w, Organism o, double dt, IReadOnlyList<Organism>? neighbors = null, V? freeMotion = null)
    {
        if (o.Genes.Buds != null) return Branching.Solve(w, o, dt, neighbors, freeMotion);
        V reaction = new(); double support = 0, load = 1 + o.Body.Count * .65;
        o.GroundContacts = 0;
        for (int n = 0; n < Sites(o); n++)
        {
            var rule = o.Genes.Rule(n);
            if (rule.Outgrowth < .025) continue;
            for (int side = -1; side <= 1; side += 2)
            {
                var p = Pose(o, n, side, o.Age);
                var next = Pose(o, n, side, o.Age + dt);
                V velocity = (next.Tip - p.Tip) * (1 / dt);
                double floor = w.Ground(p.Tip.X, p.Tip.Y);
                double penetration = floor + p.Radius - p.Tip.Z;
                if (penetration > 0)
                {
                    double grip = Math.Min(1, penetration / Math.Max(1, p.Radius)) * rule.Stiffness;
                    reaction += new V(-velocity.X, -velocity.Y) * (grip * (.4 + o.Genes.SurfaceAdhesion));
                    support += Math.Min(15, penetration * 5) * rule.Stiffness;
                    o.GroundContacts++;
                    int tile = w.SurfaceTile(p.Tip);
                    double bite = Math.Min(w.Biofilm[tile], dt * .10 * rule.Outgrowth * (1 - o.Genes.Enzymes * .4));
                    w.Biofilm[tile] -= bite;
                    o.Energy = Math.Min(o.Capacity, o.Energy + bite * 1.7); o.ShoreEnergy += bite * 1.7;
                    w.DrinkFood(o, bite * 1.7, w.MoistureAt(p.Tip));
                    w.AbsorbContactWater(o, p.Tip, p.Radius, rule.Outgrowth, dt);
                    if (bite > .003) o.LastMeal = w.Time;
                }
                else if (p.Tip.Z < w.WaterLevel)
                {
                    // Lifted recovery strokes present less area than the extended power stroke.
                    double spread = Math.Clamp((p.Joint.Z - p.Tip.Z) / Math.Max(1, o.Genes.Radius * rule.Outgrowth * 4), .05, 1);
                    double area = p.Radius * rule.Outgrowth * spread * .06;
                    reaction -= velocity * area;
                }
            }
            o.Energy -= dt * rule.Outgrowth * (.018 + rule.Stiffness * .02 + o.Genes.Contract * o.Genes.Contract * .045);
        }
        reaction *= 1 / load;
        if (reaction.Length > 12) reaction *= 12 / reaction.Length;
        return reaction + new V(0, 0, Math.Min(16, support / load));
    }
}
