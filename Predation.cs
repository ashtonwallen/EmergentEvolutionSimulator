namespace Tideglass;

// Persistent counters never affect decisions or draw random numbers. World totals
// include organisms that die between observations; per-organism values are lifetime totals.
public sealed class FeedingMetrics
{
    public long Encounters { get; set; }
    public long Bites { get; set; }
    public double ContactSeconds { get; set; }
    public double PreyNearbySeconds { get; set; }
    public double TissueVolume { get; set; }
    public double TissueEnergy { get; set; }
    public double ScavengedEnergy { get; set; }
    public double DigestionCost { get; set; }
    public double GripCost { get; set; }
    public double LocomotionEnergy { get; set; }
    public FeedingMetrics Copy() => (FeedingMetrics)MemberwiseClone();
}

public sealed class FeedingContact
{
    public int TargetId { get; set; } = -1;
    public double Seconds { get; set; }
    public double At { get; set; } = -100;
    public FeedingContact Copy() => (FeedingContact)MemberwiseClone();
}

public sealed partial class World
{
    public FeedingMetrics Feeding { get; set; } = new();
    public double? FeedingSince { get; set; }
    // Gradual protection: enzymes, pressure and wounds all matter without an
    // invulnerability threshold. Zero digestive investment still cannot eat tissue.
    internal static double TissueAccess(double enzyme, double armor, double integrity, double pressure, double toughness = 0)
    {
        double effort = Math.Max(0, enzyme) * (1 + Math.Clamp(pressure, 0, 3));
        double resistance = .08 + Math.Max(0, armor) * 2 * Math.Clamp(integrity, 0, 1) + Math.Max(0, toughness) * .5;
        return effort / (effort + resistance);
    }

    // Return the nearest *living* contacted section. Stale cached poses may exist
    // after severing, so check current growth/integrity before allowing a bite.
    private bool FeedingSite(Organism target, V point, double radius, out int cell, out int bud, out double gap)
    {
        cell = -1; bud = -1; gap = double.PositiveInfinity;
        for (int i = 0; i < target.Body.Count; i++)
        {
            if (target.Body[i].Integrity <= 0) continue;
            V d = point - target.Body[i].Position;
            if (d.Length > radius + BodyMechanics.OuterRadius(target, i) + .5) continue;
            double g = d.Length - BodyMechanics.Support(target, i, BodyMechanics.Unit(d)) - radius;
            if (g < gap) { gap = g; cell = i; bud = -1; }
        }
        if (point.X + radius + .5 >= target.BudMinimum.X && point.X - radius - .5 <= target.BudMaximum.X
            && point.Y + radius + .5 >= target.BudMinimum.Y && point.Y - radius - .5 <= target.BudMaximum.Y
            && point.Z + radius + .5 >= target.BudMinimum.Z && point.Z - radius - .5 <= target.BudMaximum.Z)
            for (int i = 0; i < target.PhysicalBuds.Length; i++)
            {
                var state = target.BudStates[i]; var p = target.PhysicalBuds[i];
                if (!p.Active || state.Growth <= 0 || state.Integrity <= 0) continue;
                double g = BranchGeometry.Gap(p, point) - radius;
                if (g < gap) { gap = g; bud = i; cell = -1; }
            }
        return gap <= .5;
    }

    internal double FeedAt(Organism eater, IReadOnlyList<Organism> neighbors, V point, double radius,
        double area, double enzyme, double pressure, double adhesion, FeedingContact contact, double dt)
    {
        if (dt <= 0 || enzyme <= 0 || area <= 0 || eater.Energy <= 0 || eater.Energy >= eater.Capacity * .98) return 0;
        Organism? victim = null; int cell = -1, bud = -1; double nearest = double.PositiveInfinity;
        foreach (var candidate in neighbors)
        {
            if (candidate == eater || candidate.Energy <= 0) continue;
            if (!FeedingSite(candidate, point, radius, out int c, out int b, out double gap)) continue;
            // Prefer an existing contact only while that contact is still physical.
            double score = gap - (candidate.Id == contact.TargetId ? .5 : 0);
            if (score < nearest) { nearest = score; victim = candidate; cell = c; bud = b; }
        }
        if (victim == null) { contact.TargetId = -1; contact.Seconds = 0; return 0; }
        bool continued = contact.TargetId == victim.Id && Time - contact.At <= dt * 1.5 + 1e-8;
        if (!continued) { eater.Feeding.Encounters++; Feeding.Encounters++; }
        contact.Seconds = continued ? Math.Min(30, contact.Seconds + dt) : dt;
        contact.TargetId = victim.Id; contact.At = Time;
        eater.Feeding.ContactSeconds += dt; Feeding.ContactSeconds += dt;

        double integrity = bud >= 0 ? victim.BudStates[bud].Integrity : victim.Body[cell].Integrity;
        double fullVolume = bud >= 0 ? TissueEconomy.BranchVolume(victim, bud, victim.BudStates[bud].Growth) : TissueEconomy.CoreVolume(victim.Genes, cell);
        double totalVolume = TissueEconomy.Volume(victim);
        double toughness = bud >= 0 ? victim.Genes.Buds![bud].Toughness : 0;
        double access = TissueAccess(enzyme, victim.Genes.Membrane, integrity, pressure, toughness);
        double handling = .3 + .7 * contact.Seconds / (1 + contact.Seconds);
        double efficiency = .6 + .2 * Math.Clamp(enzyme, 0, 1);
        double energyDensity = TissueEconomy.StoredEnergy + Math.Max(0, victim.Energy) / Math.Max(1e-12, totalVolume);
        double volume = Math.Min(fullVolume * integrity, dt * .12 * area * enzyme * access * handling);
        volume = Math.Min(volume, Math.Max(0, eater.Capacity - eater.Energy) / (energyDensity * efficiency));
        if (volume <= 1e-12) return 0;
        double cost = Math.Min(eater.Energy, dt * .015 * area * enzyme + volume * .08);
        eater.Energy -= cost; eater.Feeding.DigestionCost += cost; Feeding.DigestionCost += cost;
        if (eater.Energy <= 0) return 0;
        double reserves = Math.Max(0, victim.Energy) * Math.Min(1, volume / Math.Max(1e-12, totalVolume));
        victim.Energy -= reserves;
        double remaining = Math.Max(0, integrity - volume / Math.Max(1e-12, fullVolume));
        if (bud >= 0)
        {
            victim.BudStates[bud].Integrity = remaining; victim.BudStates[bud].LastDamage = Time;
            if (remaining < .08) Sever(victim, bud);
        }
        else
        {
            victim.Body[cell].Integrity = remaining; victim.Body[cell].LastDamage = Time;
            if (cell == 0 && remaining < .08) victim.Energy = 0;
        }
        victim.LastDamage = Time; Injuries++;
        double gained = Math.Min(eater.Capacity - eater.Energy, (volume * TissueEconomy.StoredEnergy + reserves) * efficiency);
        eater.Energy += gained; eater.TissueEnergy += gained;
        eater.Feeding.TissueVolume += volume; Feeding.TissueVolume += volume;
        eater.Feeding.TissueEnergy += gained; Feeding.TissueEnergy += gained;
        eater.Feeding.Bites++; Feeding.Bites++;
        eater.LastMeal = Time;
        if (Time - eater.LastAttack > 3) TissueMeals++;
        eater.LastAttack = victim.LastAttack = Time;
        victim.ThreatDirection = BodyMechanics.Unit(victim.Center - point);
        victim.Irritation = Math.Min(2, victim.Irritation + volume / Math.Max(.001, totalVolume) * 8);
        DrinkFood(eater, gained, victim.Hydration);
        ApplyFeedingGrip(eater, victim, area, adhesion, dt);
        return gained;
    }

    // Contact friction dissipates relative movement and is limited by grip strength
    // and paid work. Both bodies react; loss of contact immediately ends the grip.
    internal void ApplyFeedingGrip(Organism eater, Organism victim, double area, double adhesion, double dt)
    {
        if (adhesion <= 0 || eater.Energy <= 0 || victim.Energy <= 0) return;
        V relative = victim.Velocity - eater.Velocity;
        double a = BodyMechanics.Drag(eater), b = BodyMechanics.Drag(victim);
        double reducedDrag = a * b / (a + b);
        double force = Math.Min(relative.Length * reducedDrag * .8, area * adhesion * 1.5);
        double cost = dt * (.003 * area * adhesion + .02 * force * relative.Length);
        double affordable = Math.Min(1, eater.Energy / Math.Max(1e-12, cost));
        cost *= affordable; force *= affordable;
        eater.Energy -= cost; eater.Feeding.GripCost += cost; Feeding.GripCost += cost;
        V pull = BodyMechanics.Unit(relative) * force;
        V shiftA = pull * (dt / a), shiftB = pull * (-dt / b);
        foreach (var c in eater.Body) c.Position += shiftA;
        foreach (var c in victim.Body) c.Position += shiftB;
        eater.Velocity += pull * (1 / a); victim.Velocity -= pull * (1 / b);
    }

    internal void FeedCore(Organism eater, IReadOnlyList<Organism> neighbors, double dt)
    {
        if (eater.Energy <= 0 || eater.Energy >= eater.Capacity * .98) return;
        // One digestive surface per organism per tick: choose the closest actual
        // core contact, so adding neighbors cannot multiply its processing budget.
        int chosen = -1; V point = new(); double nearest = double.PositiveInfinity;
        for (int i = 0; i < eater.Body.Count; i++)
        {
            if (eater.LocalEnzymes(i) <= 0 || eater.Body[i].Integrity <= 0) continue;
            foreach (var other in neighbors)
            {
                if (other == eater || other.Energy <= 0) continue;
                if ((other.StepCenter - eater.Body[i].Position).Length > BodyMechanics.OuterRadius(eater, i) + other.PhysicalExtent + 1) continue;
                V direction = BodyMechanics.Unit(other.Center - eater.Body[i].Position);
                V surface = eater.Body[i].Position + direction * BodyMechanics.Support(eater, i, direction);
                if (FeedingSite(other, surface, .5, out _, out _, out double gap) && gap < nearest)
                { nearest = gap; chosen = i; point = surface; }
            }
        }
        if (chosen < 0) { eater.CoreFeeding.TargetId = -1; eater.CoreFeeding.Seconds = 0; return; }
        FeedAt(eater, neighbors, point, .5, TissueEconomy.CoreArea(eater.Genes, chosen) * eater.Body[chosen].Integrity,
            eater.LocalEnzymes(chosen), eater.Genes.Contract * .25, eater.Genes.SurfaceAdhesion, eater.CoreFeeding, dt);
    }

    internal double Scavenge(Organism eater, V point, double radius, double area, double enzyme, double mechanical, double dt, double aerobic = 1)
    {
        if (enzyme <= 0 || eater.Energy <= 0) return 0;
        double efficiency = (.6 + .2 * Math.Clamp(enzyme, 0, 1)) * aerobic;
        double room = Math.Max(0, eater.Capacity - eater.Energy);
        double budget = Math.Min(dt * area * enzyme * 1.5, room / Math.Max(1e-12, TissueEconomy.FoodConversion * efficiency));
        double raw = CaptureRemains(point, radius, budget, mechanical);
        if (raw <= 0) return 0;
        double cost = Math.Min(eater.Energy, raw * .02);
        eater.Energy -= cost; eater.Feeding.DigestionCost += cost; Feeding.DigestionCost += cost;
        double gained = Math.Min(eater.Capacity - eater.Energy, raw * efficiency);
        eater.Energy += gained; eater.GrazingEnergy += gained;
        eater.Feeding.ScavengedEnergy += gained; Feeding.ScavengedEnergy += gained;
        eater.LastMeal = Time; DrinkFood(eater, gained);
        return gained;
    }

    internal double TissueCue(Organism observer, V point, IReadOnlyList<Organism> neighbors)
    {
        double signal = 0;
        foreach (var other in neighbors)
        {
            if (other == observer || other.Energy <= 0) continue;
            double distance = (point - other.StepCenter).Length;
            if (distance < 65) signal += other.StepTissueCue * (1 - distance / 65);
        }
        return signal;
    }

    internal double RemainsCue(V point)
    {
        if (Detritus.Count == 0 || remainsBins.Length == 0) return 0;
        int x = Math.Clamp((int)(point.X / CellSize), 0, Cols - 1), y = Math.Clamp((int)(point.Y / CellSize), 0, Rows - 1);
        int reach = (int)Math.Ceiling(65 / CellSize); double signal = 0;
        for (int yy = Math.Max(0, y - reach); yy <= Math.Min(Rows - 1, y + reach); yy++)
        for (int xx = Math.Max(0, x - reach); xx <= Math.Min(Cols - 1, x + reach); xx++)
            if (remainsBins[yy * Cols + xx] is { } bin) foreach (var remains in bin)
            {
                double distance = (point - remains.Position).Length;
                if (distance < 65 && remains.Energy > 0) signal += Math.Min(2, remains.Energy) * (1 - distance / 65);
            }
        return signal;
    }
}
