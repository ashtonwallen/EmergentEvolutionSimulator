namespace Tideglass;

public sealed class FoodClump
{
    public V Position { get; set; }
    public double Energy { get; set; }
    public double Radius { get; set; } = 3;
    public bool Organic { get; set; }
    public FoodClump Copy() => (FoodClump)MemberwiseClone();
}

public sealed partial class World
{
    public List<FoodClump> Clumps { get; set; } = [];
    public double CurrentTime { get; set; }
    public double ClumpAccumulator { get; set; }
    public int Injuries { get; set; }
    public int LostBranches { get; set; }
    private V[]? currentMap;
    private List<Rock>[]? rockMap;
    private readonly Dictionary<int, List<Remains>> remainsBins = [];
    private IEnumerable<Rock> NearbyRocks(V p)
    {
        if (rockMap == null)
        {
            rockMap = new List<Rock>[Cols * Rows];
            for (int i = 0; i < rockMap.Length; i++) rockMap[i] = [];
            foreach (var rock in Rocks)
            {
                double reach = rock.Radius + 64;
                int x0 = Math.Clamp((int)((rock.Position.X - reach) / CellSize), 0, Cols - 1), x1 = Math.Clamp((int)((rock.Position.X + reach) / CellSize), 0, Cols - 1);
                int y0 = Math.Clamp((int)((rock.Position.Y - reach) / CellSize), 0, Rows - 1), y1 = Math.Clamp((int)((rock.Position.Y + reach) / CellSize), 0, Rows - 1);
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) rockMap[y * Cols + x].Add(rock);
            }
        }
        return rockMap[SurfaceTile(p)];
    }
    private readonly Dictionary<int, List<FoodClump>> clumpBins = [];

    public void AddHabitatDetails()
    {
        rockMap = null; currentMap = null;
        // Two broken rock ridges create narrow gaps, sheltered pockets, and exposed faces.
        for (int bank = 0; bank < 2; bank++) for (int i = 0; i < 12; i++)
        {
            double x = 1320 + i * 43, y = 650 + bank * 96 + Math.Sin(i * .7) * 22;
            double radius = 16 + (i * 13 % 11);
            Rocks.Add(new Rock { Position = new(x, y, Ground(x, y) + radius * .35), Radius = radius });
        }
    }

    public V FlowAt(V p)
    {
        if (!Tidal) return Habitat.Flow(p, Time);
        currentMap ??= BuildCurrents();
        double wet = Immersion(p, 2);
        double boundary = .3 + .7 * Math.Clamp((p.Z - Ground(p.X, p.Y)) / 25, 0, 1);
        return currentMap[SurfaceTile(p)] * (wet * boundary);
    }

    private V[] BuildCurrents()
    {
        var map = new V[Cols * Rows];
        double tidal = Math.Cos(CurrentTime * Math.Tau / 360);
        Parallel.For(0, Rows, SimulationWorkers, y =>
        {
            for (int x = 0; x < Cols; x++)
            {
                V p = new((x + .5) * CellSize, (y + .5) * CellSize, 150);
                double channel = Math.Exp(-Math.Pow((p.Y - (720 + 90 * Math.Sin(p.X / 250))) / 80, 2));
                V flow = Habitat.Flow(p, CurrentTime) * 1.5 + new V(tidal * (2 + channel * 5), Math.Sin(p.X / 180 + CurrentTime / 65) * 1.4);
                double speed = Math.Max(.001, flow.Length); V direction = flow * (1 / speed);
                foreach (var rock in Rocks)
                {
                    V delta = p - new V(rock.Position.X, rock.Position.Y, 150);
                    if (Math.Abs(delta.X) > rock.Radius * 5 || Math.Abs(delta.Y) > rock.Radius * 5) continue;
                    double along = delta.X * direction.X + delta.Y * direction.Y;
                    if (along < 0 || along > rock.Radius * 5) continue;
                    double across = Math.Abs(delta.X * direction.Y - delta.Y * direction.X);
                    double shadow = Math.Exp(-Math.Pow(across / (rock.Radius + along * .2), 2)) * Math.Exp(-along / (rock.Radius * 3));
                    flow *= 1 - shadow * .8;
                }
                map[y * Cols + x] = flow;
            }
        });
        return map;
    }

    public double LightAt(V p)
    {
        double light = Habitat.Light(p, Time);
        if (!Tidal) return light;
        foreach (var rock in NearbyRocks(p))
        {
            double dx = p.X - rock.Position.X, dy = p.Y - rock.Position.Y;
            double q = rock.Radius * rock.Radius - dx * dx - dy * dy;
            if (q > 0 && p.Z < rock.Position.Z + Math.Sqrt(q)) light *= .3;
        }
        return light;
    }

    public bool SurfaceContact(V p, double radius, out V normal, out double penetration, out Rock? touchedRock)
    {
        penetration = Ground(p.X, p.Y) + radius - p.Z;
        normal = new(0, 0, 1); touchedRock = null;
        foreach (var rock in radius <= 64 ? NearbyRocks(p) : Rocks)
        {
            V d = p - rock.Position; double distance = d.Length;
            double overlap = rock.Radius + radius - distance;
            if (overlap > penetration)
            {
                penetration = overlap; normal = distance > .001 ? d * (1 / distance) : new V(0, 0, 1); touchedRock = rock;
            }
        }
        return penetration > 0;
    }

    public V VisibleContact(V p, double radius)
    {
        if (!Tidal) return p;
        return SurfaceContact(p, radius, out var normal, out double depth, out _) ? p + normal * depth : p;
    }

    public double GrazeSurface(V p, double radius, double wanted)
    {
        if (!SurfaceContact(p, radius, out _, out _, out var rock)) return 0;
        if (rock != null) { double bite = Math.Min(rock.Food, wanted); rock.Food -= bite; return bite; }
        int tile = SurfaceTile(p); double eaten = Math.Min(Biofilm[tile], wanted); Biofilm[tile] -= eaten; return eaten;
    }

    public void PreparePhysicalFood(double dt)
    {
        foreach (var list in clumpBins.Values) list.Clear();
        foreach (var list in remainsBins.Values) list.Clear();
        foreach (var remains in Detritus)
        {
            int key = SurfaceTile(remains.Position);
            if (!remainsBins.TryGetValue(key, out var bin)) remainsBins[key] = bin = [];
            bin.Add(remains);
        }
        for (int i = Clumps.Count - 1; i >= 0; i--)
        {
            var c = Clumps[i];
            c.Position += (FlowAt(c.Position) + new V(0, 0, c.Organic ? -1.4 : -.10)) * dt;
            c.Position = new(Math.Clamp(c.Position.X, 3, Width - 3), Math.Clamp(c.Position.Y, 3, Height - 3), Math.Min(Depth - 3, c.Position.Z));
            c.Position = VisibleContact(c.Position, c.Radius * .3);
            double decay = Math.Min(c.Energy, dt * .002);
            c.Energy -= decay; OrganicMatter[Tile(c.Position)] += decay;
            if (c.Energy <= .005) { Clumps.RemoveAt(i); continue; }
            int key = Tile(c.Position);
            if (!clumpBins.TryGetValue(key, out var bin)) clumpBins[key] = bin = [];
            bin.Add(c);
        }
        // Nutrient fields assemble into physical parcels; energy is transferred, not created.
        ClumpAccumulator += dt;
        if (ClumpAccumulator < .5) return;
        ClumpAccumulator = 0;
        for (int n = 0; n < 24 && Clumps.Count < 1800; n++)
        {
            V p = new(random.NextDouble() * Width, random.NextDouble() * Height, 10 + random.NextDouble() * (WaterLevel - 15));
            if (p.Z < Ground(p.X, p.Y) + 4) continue;
            int tile = Tile(p); bool organic = random.NextDouble() < .3;
            var field = organic ? OrganicMatter : Food;
            double amount = Math.Min(field[tile], .6 + random.NextDouble() * .8);
            if (amount < .2) continue;
            field[tile] -= amount;
            Clumps.Add(new FoodClump { Position = p, Energy = amount, Radius = 2 + Math.Sqrt(amount) * 1.7, Organic = organic });
        }
    }

    public double Capture(V from, V to, double radius, double capacity, double enzyme)
    {
        if (capacity <= 0 || Clumps.Count == 0) return 0;
        V d = to - from; double length2 = d.LengthSquared, result = 0;
        double margin = radius + 5;
        int x0 = Math.Clamp((int)((Math.Min(from.X, to.X) - margin) / CellSize), 0, Cols - 1);
        int x1 = Math.Clamp((int)((Math.Max(from.X, to.X) + margin) / CellSize), 0, Cols - 1);
        int y0 = Math.Clamp((int)((Math.Min(from.Y, to.Y) - margin) / CellSize), 0, Rows - 1);
        int y1 = Math.Clamp((int)((Math.Max(from.Y, to.Y) + margin) / CellSize), 0, Rows - 1);
        int z0 = Math.Clamp((int)((Math.Min(from.Z, to.Z) - margin) / (Depth / Layers)), 0, Layers - 1);
        int z1 = Math.Clamp((int)((Math.Max(from.Z, to.Z) + margin) / (Depth / Layers)), 0, Layers - 1);
        // Each parcel is visited once, using only bins intersecting the swept tissue bounds.
        for (int z = z0; z <= z1 && capacity > 0; z++)
        for (int y = y0; y <= y1 && capacity > 0; y++)
        for (int x = x0; x <= x1 && capacity > 0; x++)
        {
            if (!clumpBins.TryGetValue((z * Rows + y) * Cols + x, out var bin)) continue;
            foreach (var c in bin)
            {
                if (capacity <= 0) break;
                double t = length2 < .001 ? 0 : Math.Clamp(((c.Position.X - from.X) * d.X + (c.Position.Y - from.Y) * d.Y + (c.Position.Z - from.Z) * d.Z) / length2, 0, 1);
                if ((c.Position - (from + d * t)).LengthSquared > Math.Pow(radius + c.Radius, 2)) continue;
                double efficiency = c.Organic ? .15 + enzyme * .85 : 1 - enzyme * .35;
                double bite = Math.Min(c.Energy, capacity * efficiency); c.Energy -= bite; capacity -= bite;
                result += bite * (c.Organic ? 1.7 : 1.3);
            }
        }
        return result;
    }

    public double CaptureRemains(V p, double radius, double capacity)
    {
        if (capacity <= 0 || Detritus.Count == 0) return 0;
        double result = 0;
        int x = Math.Clamp((int)(p.X / CellSize), 0, Cols - 1), y = Math.Clamp((int)(p.Y / CellSize), 0, Rows - 1);
        int reach = (int)Math.Ceiling((radius + 30) / CellSize);
        for (int yy = Math.Max(0, y - reach); yy <= Math.Min(Rows - 1, y + reach); yy++)
        for (int xx = Math.Max(0, x - reach); xx <= Math.Min(Cols - 1, x + reach); xx++)
        {
            if (!remainsBins.TryGetValue(yy * Cols + xx, out var bin)) continue;
            foreach (var remains in bin)
            {
                if (capacity <= 0) break;
                if ((remains.Position - p).LengthSquared > Math.Pow(radius + remains.Radius, 2)) continue;
                double bite = Math.Min(remains.Energy, capacity); remains.Energy -= bite; capacity -= bite; result += bite * 1.7;
            }
        }
        return result;
    }

    public double FoodContactSignal(V p)
    {
        double signal = 0;
        if (clumpBins.TryGetValue(Tile(p), out var bin)) foreach (var c in bin) signal += c.Energy / (1 + (c.Position - p).Length * .15);
        return signal;
    }

    public void CacheAnatomy(Organism o)
    {
        if (o.Genes.Buds == null) return;
        if (o.PhysicalBuds.Length != o.Genes.Buds.Count) o.PhysicalBuds = new BudPose[o.Genes.Buds.Count];
        Branching.Poses(o, o.Age, o.PhysicalBuds, WaterLevel);
        V min = new(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
        V max = new(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);
        foreach (var part in o.PhysicalBuds)
        {
            if (!part.Active) continue;
            min = new(Math.Min(min.X, part.Tip.X - part.Radius), Math.Min(min.Y, part.Tip.Y - part.Radius), Math.Min(min.Z, part.Tip.Z - part.Radius));
            max = new(Math.Max(max.X, part.Tip.X + part.Radius), Math.Max(max.Y, part.Tip.Y + part.Radius), Math.Max(max.Z, part.Tip.Z + part.Radius));
        }
        o.BudMinimum = min; o.BudMaximum = max;
    }

    public bool TissueContact(Organism target, V point, double radius)
    {
        for (int i = 0; i < target.Body.Count; i++)
            if ((target.Body[i].Position - point).LengthSquared < Math.Pow(radius + target.Genes.CellRadius(i), 2)) return true;
        if (point.X + radius < target.BudMinimum.X || point.X - radius > target.BudMaximum.X
            || point.Y + radius < target.BudMinimum.Y || point.Y - radius > target.BudMaximum.Y
            || point.Z + radius < target.BudMinimum.Z || point.Z - radius > target.BudMaximum.Z) return false;
        foreach (var part in target.PhysicalBuds)
            if (part.Active && (part.Tip - point).LengthSquared < Math.Pow(radius + part.Radius, 2)) return true;
        return false;
    }

    public void DamageTissue(Organism victim, V point, double amount)
    {
        if (amount <= .001) return;
        int cell = 0, bud = -1; double nearest = double.MaxValue;
        for (int i = 0; i < victim.Body.Count; i++)
        {
            double gap = (victim.Body[i].Position - point).Length - victim.Genes.CellRadius(i);
            if (gap < nearest) { nearest = gap; cell = i; }
        }
        for (int i = 0; i < victim.PhysicalBuds.Length; i++)
        {
            var p = victim.PhysicalBuds[i]; if (!p.Active) continue;
            double gap = (p.Tip - point).Length - p.Radius;
            if (gap < nearest) { nearest = gap; bud = i; }
        }
        if (bud >= 0)
        {
            var gene = victim.Genes.Buds![bud]; var tissue = victim.BudStates[bud];
            tissue.Integrity = Math.Max(0, tissue.Integrity - amount / (4 + gene.Width * gene.Length * 8) / (1 + gene.Toughness * 3));
            tissue.LastDamage = Time;
            if (tissue.Integrity < .08) Sever(victim, bud);
        }
        else
        {
            var tissue = victim.Body[cell];
            tissue.Integrity = Math.Max(0, tissue.Integrity - amount / (18 + victim.Genes.Radius)); tissue.LastDamage = Time;
            if (cell == 0 && tissue.Integrity < .08) victim.Energy = 0;
        }
        Injuries++; victim.LastDamage = Time;
    }

    public void Sever(Organism o, int part)
    {
        if (o.BudStates[part].Growth < .01) return;
        Span<bool> removed = stackalloc bool[o.BudStates.Count]; removed.Clear(); removed[part] = true;
        for (int i = part; i < o.BudStates.Count; i++)
        {
            var b = o.Genes.Buds![i]; if (b.Parent >= 0 && removed[b.Parent]) removed[i] = true;
            if (!removed[i]) continue;
            var state = o.BudStates[i];
            if (i < o.PhysicalBuds.Length && o.PhysicalBuds[i].Active)
                Detritus.Add(new Remains { Position = o.PhysicalBuds[i].Tip, Energy = state.Growth * Math.Max(.05, b.Length * b.Width * b.Width * o.Genes.Radius), Radius = Math.Max(.3, o.PhysicalBuds[i].Radius) });
            state.Integrity = 0; state.Growth = 0; state.FormedAt = o.Age; state.LastDamage = Time; state.Signal = 0;
        }
        LostBranches++;
    }

    public void RepairBody(Organism o, double dt)
    {
        foreach (var cell in o.Body)
        {
            if (cell.Integrity >= 1 || Time - cell.LastDamage < 8 || o.Energy < o.Capacity * .4) continue;
            double repair = Math.Min(1 - cell.Integrity, dt * .004);
            cell.Integrity += repair; o.Energy -= repair * 22;
        }
        o.Anchoring = 0;
    }
}
