namespace Tideglass;

// Incoming synapses belong to their destination tissue. Indices refer to inherited buds.
public sealed class SignalWire
{
    public int Source { get; set; }
    public double Weight { get; set; }
    public double MemoryMix { get; set; }
    public SignalWire Copy() => (SignalWire)MemberwiseClone();
}

public static partial class Branching
{
    // A computation budget, shared by the entire organism, rather than a fixed brain layout.
    public const int WireBudget = Development.NodeBudget * 4;

    internal static void RemoveBud(Genome g, int removed)
    {
        var buds = g.Buds!;
        int parent = buds[removed].Parent;
        buds.RemoveAt(removed);
        foreach (var b in buds)
        {
            if (b.Parent == removed) b.Parent = parent;
            else if (b.Parent > removed) b.Parent--;
            b.Wires.RemoveAll(w => w.Source == removed);
            foreach (var wire in b.Wires) if (wire.Source > removed) wire.Source--;
        }
    }

    internal static bool DuplicateStructure(Genome g, int root, Random r)
    {
        var buds = g.Buds!;
        int originalCount = buds.Count;
        if (root < 0 || root >= originalCount) return false;
        Span<int> map = stackalloc int[originalCount]; map.Fill(-1);
        int count = 0, wireCount = buds.Sum(b => b.Wires.Count), addedWires = 0;
        for (int i = root; i < originalCount; i++)
            if (i == root || (buds[i].Parent >= 0 && map[buds[i].Parent] >= 0))
            {
                map[i] = originalCount + count++;
                addedWires += buds[i].Wires.Count;
            }
        // Preserve outgoing connections as well as the copied group's internal and incoming wiring.
        for (int i = 0; i < originalCount; i++)
            if (map[i] < 0) foreach (var wire in buds[i].Wires) if (map[wire.Source] >= 0) addedWires++;
        if (originalCount + count + g.Cells > Development.NodeBudget || wireCount + addedWires > WireBudget) return false;
        for (int i = root; i < originalCount; i++)
        {
            if (map[i] < 0) continue;
            var copy = buds[i].Copy();
            if (i != root) copy.Parent = map[copy.Parent];
            else if (copy.Parent < 0)
            {
                copy.AnchorCell = (int)(r.NextDouble() * g.Cells);
                copy.AnchorAngle = Math.IEEERemainder(copy.AnchorAngle + (r.NextDouble() * 2 - 1) * Math.PI, Math.Tau);
            }
            else copy.Turn = Math.Clamp(copy.Turn + (r.NextDouble() * 2 - 1) * 1.5, -Math.PI, Math.PI);
            foreach (var wire in copy.Wires) if (map[wire.Source] >= 0) wire.Source = map[wire.Source];
            buds.Add(copy);
        }
        for (int i = 0; i < originalCount; i++)
        {
            if (map[i] >= 0) continue;
            var wires = buds[i].Wires;
            int oldCount = wires.Count;
            for (int j = 0; j < oldCount; j++) if (map[wires[j].Source] >= 0)
            {
                var copy = wires[j].Copy(); copy.Source = map[copy.Source]; wires.Add(copy);
            }
        }
        return true;
    }

    private static void MutateWiring(Genome g, Random r)
    {
        var buds = g.Buds!;
        if (buds.Count == 0) return;
        foreach (var b in buds)
            for (int i = b.Wires.Count - 1; i >= 0; i--)
            {
                var wire = b.Wires[i];
                if (r.NextDouble() < .04) { b.Wires.RemoveAt(i); continue; }
                if (r.NextDouble() < .18) wire.Weight = Math.Clamp(wire.Weight + (r.NextDouble() * 2 - 1) * .35, -2, 2);
                if (r.NextDouble() < .12) wire.MemoryMix = Math.Clamp(wire.MemoryMix + (r.NextDouble() * 2 - 1) * .25, 0, 1);
                if (r.NextDouble() < .04) wire.Source = (int)(r.NextDouble() * buds.Count);
            }
        if (r.NextDouble() < .25 && buds.Sum(b => b.Wires.Count) < WireBudget)
            buds[(int)(r.NextDouble() * buds.Count)].Wires.Add(new SignalWire
            {
                Source = (int)(r.NextDouble() * buds.Count), Weight = (r.NextDouble() * 2 - 1) * .4,
                MemoryMix = r.NextDouble()
            });
    }
}
