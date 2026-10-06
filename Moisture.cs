namespace Tideglass;

public sealed partial class World
{
    public double[] SoilMoisture { get; set; } = new double[Cols * Rows];
    public double[] SurfaceWater { get; set; } = new double[Cols * Rows];
    [System.Text.Json.Serialization.JsonIgnore] public double Rain => Math.Clamp((Math.Sin((Time + 90) * Math.Tau / 900) - .78) / .22, 0, 1);
    private double[]? waterCapacity, moistureShelter, runoff;

    private void PrepareMoistureTerrain()
    {
        if (waterCapacity != null) return;
        int count = Cols * Rows;
        waterCapacity = new double[count]; moistureShelter = new double[count]; runoff = new double[count];
        var heights = new double[count]; var visited = new bool[count];
        var frontier = new PriorityQueue<int, double>();
        for (int y = 0; y < Rows; y++) for (int x = 0; x < Cols; x++)
        {
            int i = y * Cols + x;
            V p = new((x + .5) * CellSize, (y + .5) * CellSize);
            heights[i] = Ground(p.X, p.Y);
            foreach (var rock in Rocks)
            {
                double gap = Math.Sqrt(Math.Pow(p.X - rock.Position.X, 2) + Math.Pow(p.Y - rock.Position.Y, 2)) - rock.Radius;
                moistureShelter[i] = Math.Max(moistureShelter[i], Math.Clamp(1 - Math.Max(0, gap) / 45, 0, 1) * .7);
            }
            if (x == 0 || y == 0 || x == Cols - 1 || y == Rows - 1) { visited[i] = true; frontier.Enqueue(i, heights[i]); }
        }
        // Flood from the boundary to discover closed depressions and their spill heights.
        while (frontier.TryDequeue(out int i, out double spill))
        {
            int x = i % Cols, y = i / Cols;
            foreach (int n in new[] { x > 0 ? i - 1 : -1, x < Cols - 1 ? i + 1 : -1, y > 0 ? i - Cols : -1, y < Rows - 1 ? i + Cols : -1 })
            {
                if (n < 0 || visited[n]) continue;
                visited[n] = true;
                double level = Math.Max(heights[n], spill);
                waterCapacity[n] = Math.Clamp(level - heights[n], 0, 12);
                frontier.Enqueue(n, level);
            }
        }
    }

    public void InitializeMoisture()
    {
        PrepareMoistureTerrain();
        for (int i = 0; i < SoilMoisture.Length; i++)
        {
            double h = Ground((i % Cols + .5) * CellSize, (i / Cols + .5) * CellSize);
            SoilMoisture[i] = Math.Clamp(.12 + .88 * Math.Exp(-Math.Max(0, h - WaterLevel) / 28) + waterCapacity![i] * .03, 0, 1);
            SurfaceWater[i] = waterCapacity![i] * .45;
        }
    }

    private void UpdateMoisture(double dt)
    {
        PrepareMoistureTerrain();
        double rain = Rain, sunlight = Shoreline.Daylight(Time);
        Array.Clear(runoff!);
        for (int y = 0; y < Rows; y++) for (int x = 0; x < Cols; x++)
        {
            int i = y * Cols + x; double h = Ground((x + .5) * CellSize, (y + .5) * CellSize);
            if (h < WaterLevel) { SoilMoisture[i] = 1; SurfaceWater[i] = Math.Min(12, WaterLevel - h); continue; }
            double shelter = moistureShelter![i];
            double evaporation = (.00035 + sunlight * .001) * (1 - shelter * .7);
            double infiltration = Math.Min(SurfaceWater[i], dt * .015 * (1 - SoilMoisture[i]));
            SurfaceWater[i] -= infiltration;
            SoilMoisture[i] = Math.Clamp(SoilMoisture[i] + dt * rain * .004 + infiltration * .12 - dt * evaporation, 0, 1);
            SurfaceWater[i] = Math.Clamp(SurfaceWater[i] + dt * (rain * .06 * SoilMoisture[i] - evaporation * 5), 0, 12);
            double excess = Math.Max(0, SurfaceWater[i] - waterCapacity![i]);
            if (excess <= 0) continue;
            int downhill = i; double lowest = h;
            foreach (int n in new[] { x > 0 ? i - 1 : -1, x < Cols - 1 ? i + 1 : -1, y > 0 ? i - Cols : -1, y < Rows - 1 ? i + Cols : -1 })
            {
                if (n < 0) continue;
                double nh = Ground((n % Cols + .5) * CellSize, (n / Cols + .5) * CellSize);
                if (nh < lowest) { lowest = nh; downhill = n; }
            }
            double drained = Math.Min(excess, dt * .15);
            SurfaceWater[i] -= drained;
            if (downhill != i) runoff![downhill] += drained;
        }
        for (int i = 0; i < SurfaceWater.Length; i++) SurfaceWater[i] = Math.Min(12, SurfaceWater[i] + runoff![i]);
    }

    public double MoistureAt(V p) => Math.Max(SoilMoisture[SurfaceTile(p)], Math.Clamp(SurfaceWater[SurfaceTile(p)], 0, 1));
    public double PuddleLevel(V p) => Ground((Math.Clamp((int)(p.X / CellSize), 0, Cols - 1) + .5) * CellSize,
        (Math.Clamp((int)(p.Y / CellSize), 0, Rows - 1) + .5) * CellSize) + SurfaceWater[SurfaceTile(p)];
    public double WaterStorage(Organism o) => Math.Max(.5, o.Body.Count * Math.Pow(o.Genes.Radius / 6, 3) * o.Genes.Elongation * o.Genes.Flattening);
    public void DrinkFood(Organism o, double energy, double waterFraction = .8)
    {
        if (energy <= 0 || waterFraction <= 0 || o.Hydration >= 1) return;
        o.Hydration = Math.Min(1, o.Hydration + energy * waterFraction * .07 / WaterStorage(o));
    }

    public void AbsorbContactWater(Organism o, V point, double radius, double area, double dt)
    {
        if (o.Hydration >= 1 || area <= 0) return;
        if (!SurfaceContact(point, radius + .25, out _, out _, out var rock)) return;
        int tile = SurfaceTile(point);
        double damp = rock == null ? MoistureAt(point) : SoilMoisture[tile] * .35 + Rain * .4;
        double uptake = dt * .065 * area * damp * (1 - o.Genes.Waterproofing * .85);
        double available = rock == null ? SoilMoisture[tile] * 20 + SurfaceWater[tile] * 8 : SoilMoisture[tile] * 7;
        uptake = Math.Min(Math.Min(uptake, available), Math.Max(0, 1 - o.Hydration) * WaterStorage(o));
        double fromPool = rock == null ? Math.Min(SurfaceWater[tile], uptake / 8) : 0;
        SurfaceWater[tile] -= fromPool;
        SoilMoisture[tile] = Math.Max(0, SoilMoisture[tile] - (uptake - fromPool * 8) / 20);
        o.Hydration = Math.Min(1, o.Hydration + uptake / WaterStorage(o));
    }

    public void UpdateHydration(Organism o, double dt, double wet)
    {
        PrepareMoistureTerrain();
        var g = o.Genes; V p = o.StepCenter;
        // Sea water is effectively unlimited; puddle/soil uptake is deducted by contact absorption.
        wet = Math.Clamp((WaterLevel - p.Z + g.Radius) / (2 * g.Radius), 0, 1);
        double storage = WaterStorage(o);
        double area = o.Body.Count * Math.Pow(g.Radius / 6, 2) * Math.Pow(g.Elongation * g.Flattening, 2.0 / 3)
            * (1 + Math.Abs(Math.Log(g.Flattening)) * .35 + Math.Abs(Math.Log(g.Elongation)) * .2);
        if (g.Buds != null) for (int i = 0; i < o.BudStates.Count; i++)
        {
            var b = g.Buds[i]; double growth = o.BudStates[i].Growth;
            area += b.Length * b.Width * growth * growth * .3;
        }
        double shelter = moistureShelter![SurfaceTile(p)] * Math.Exp(-Math.Max(0, p.Z - Ground(p.X, p.Y)) / 30);
        double humidity = SoilMoisture[SurfaceTile(p)] * Math.Exp(-Math.Max(0, p.Z - Ground(p.X, p.Y)) / 22);
        double drying = .010 * area / storage * (1 - g.Waterproofing * .92) * (1 - shelter * .65) * (1 - humidity * .5)
            * (.65 + Shoreline.Daylight(Time) * .35) * (1 - Rain * .8);
        o.Hydration = Math.Clamp(o.Hydration + dt * (wet * .12 * (1 - g.Waterproofing * .65) - (1 - wet) * drying), 0, 1);
    }
}
