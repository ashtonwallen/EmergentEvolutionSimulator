namespace Tideglass;

// A reference unit is an ellipsoid with radius 6, elongation 1.3 and flatness .72.
// Each developmental piece owns its material; blended/overlapping surfaces do not erase it.
public static class TissueEconomy
{
    private static double Cube(double x) => x * x * x;
    public const double BuildCost = 5, StoredEnergy = 3.5, FoodConversion = 1.7;
    public static double GrowthReserve(Organism o) => Math.Min(8, o.Capacity * .15);
    public static double CoreArea(Genome g, int i) => Math.Pow(CoreVolume(g, i), 2.0 / 3);
    public static double MateContribution(double energy) => Math.Min(9, Math.Max(0, energy) * .2);
    public static double CoreVolume(Genome g, int i) => Cube(g.CellRadius(i) / 6) * g.Elongation * g.Flattening / (1.3 * .72);
    public static double BranchVolume(Organism o, int i, double growth)
    {
        var b = o.Genes.Buds![i];
        int anchor = b.AnchorCell;
        for (int p = b.Parent; p >= 0; p = o.Genes.Buds[p].Parent) anchor = o.Genes.Buds[p].AnchorCell;
        double radius = o.Genes.CellRadius(Math.Clamp(anchor, 0, o.Genes.Cells - 1));
        // Two equal frusta: root radius 1.05, joint radius 1, tip radius taper.
        double section = (1.05 * 1.05 + 1.05 + 1 + 1 + b.Taper + b.Taper * b.Taper) / 6;
        return Cube(radius / 6 * growth) * .75 * b.Length * b.Width * b.Width * b.Flatness * section / (1.3 * .95 * .72);
    }
    public static double CoreVolume(Organism o)
    { double result = 0; for (int i = 0; i < o.Body.Count; i++) result += CoreVolume(o.Genes, i) * o.Body[i].Integrity; return result; }
    public static double Volume(Organism o)
    {
        double result = CoreVolume(o);
        if (o.Genes.Buds != null) for (int i = 0; i < Math.Min(o.BudStates.Count, o.Genes.Buds.Count); i++) result += BranchVolume(o, i, o.BudStates[i].Growth) * o.BudStates[i].Integrity;
        return result;
    }
    public static double RemainsFood(double volume) => Math.Max(0, volume) * StoredEnergy / FoodConversion;
}
