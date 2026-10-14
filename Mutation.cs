namespace Tideglass;

public enum MutationMode { Scaled, Legacy }

public static class Mutation
{
    // Budget parameter edits across body, growth pattern, branches and wiring together.
    // Structural operations retain their separate, per-offspring probabilities.
    public static double Scale(Genome g, MutationMode mode)
    {
        if (mode == MutationMode.Legacy) return 1;
        int branches = g.Buds?.Count ?? 0, wires = g.Buds?.Sum(b => b.Wires.Count) ?? 0;
        double expected = 26 * .3 + g.Pattern.Count * 3.08 + branches * 7.06 + wires * .38;
        double budget = 4 + .5 * Math.Sqrt(1 + g.Pattern.Count + branches + wires * .25);
        return Math.Min(1, budget / expected);
    }

    public static double Adjust(Random r, double value, double amount, double lo, double hi,
        double probability, MutationMode mode)
    {
        if (r.NextDouble() >= probability) return Math.Clamp(value, lo, hi);
        // Triangular variation favors small edits; occasional large edits remain possible.
        double change = mode == MutationMode.Legacy ? r.NextDouble() * 2 - 1 : r.NextDouble() - r.NextDouble();
        return Math.Clamp(value + change * amount, lo, hi);
    }
}
