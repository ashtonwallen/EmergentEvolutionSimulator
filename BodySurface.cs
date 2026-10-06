namespace Tideglass;

/// <summary>A continuous implicit tissue surface. The cell graph is a scaffold, not visible beads.</summary>
public sealed class BodySurface
{
    public V[] Vertices { get; private set; } = [];
    public V[] Normals { get; private set; } = [];
    public int[] Triangles { get; private set; } = [];
    public V[] Nodes { get; private set; } = [];
    public int[] BoneA { get; private set; } = [];
    public int[] BoneB { get; private set; } = [];
    public double[] WeightA { get; private set; } = [];
    public double[] Weights { get; private set; } = [];
    public V Minimum { get; private set; }
    public V Maximum { get; private set; }
    public double Extent => (Maximum - Minimum).Length * .5;
    private readonly record struct Kernel(V Center, V Along, V Side, V Up, double X, double Y, double Z, double Phase);
    private static double Dot(V a, V b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static V Cross(V a, V b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    private static V Unit(V p) => p * (1 / Math.Max(.000001, p.Length));

    public static BodySurface Create(Genome g, int cells, int resolution = 20)
    {
        cells = Math.Clamp(cells, 1, Development.NodeBudget);
        var data = new BodySurface();
        var nodes = new V[cells];
        var parents = new int[cells]; parents[0] = -1;
        var directions = new V[cells]; directions[0] = new V(1, 0, 0);
        for (int n = 1; n < cells; n++)
        {
            var rule = g.Rule(n);
            int parent = Math.Max(0, n - rule.ParentBack - (g.Branch > (Math.Sin(n * 12.3) + 1) / 2 ? 1 : 0));
            parents[n] = parent;
            directions[n] = V.Direction(n * g.Turn + rule.Turn, rule.Elevation);
            nodes[n] = nodes[parent] + directions[n] * ((g.CellRadius(n) + g.CellRadius(parent)) * .8);
        }
        V center = new(); foreach (var p in nodes) center += p; center *= 1.0 / cells;
        for (int n = 0; n < cells; n++) nodes[n] -= center;
        data.Nodes = nodes;
        var kernels = new List<Kernel>(cells * 2);
        V min = new(double.MaxValue, double.MaxValue, double.MaxValue), max = new(double.MinValue, double.MinValue, double.MinValue);
        for (int n = 0; n < cells; n++)
        {
            V along = directions[n], side = Unit(Cross(new V(0, 0, 1), along)), up = Unit(Cross(along, side));
            double radius = g.CellRadius(n), rx = radius * g.Elongation, ry = radius * .95, rz = radius * g.Flattening;
            kernels.Add(new(nodes[n], along, side, up, rx, ry, rz, n * g.Turn));
            if (n > 0)
            {
                double length = (nodes[n] - nodes[parents[n]]).Length;
                double neck = Math.Min(g.CellRadius(n), g.CellRadius(parents[n]));
                kernels.Add(new((nodes[n] + nodes[parents[n]]) * .5, along, side, up, length * .7, neck * .72, neck * .72 * g.Flattening, n * g.Turn));
            }
            V bounds = new(
                Math.Sqrt(along.X * along.X * rx * rx + side.X * side.X * ry * ry * 1.8 + up.X * up.X * rz * rz),
                Math.Sqrt(along.Y * along.Y * rx * rx + side.Y * side.Y * ry * ry * 1.8 + up.Y * up.Y * rz * rz),
                Math.Sqrt(along.Z * along.Z * rx * rx + side.Z * side.Z * ry * ry * 1.8 + up.Z * up.Z * rz * rz));
            bounds *= 2.5;
            var lo = nodes[n] - bounds; var hi = nodes[n] + bounds;
            min = new(Math.Min(min.X, lo.X), Math.Min(min.Y, lo.Y), Math.Min(min.Z, lo.Z));
            max = new(Math.Max(max.X, hi.X), Math.Max(max.Y, hi.Y), Math.Max(max.Z, hi.Z));
        }
        double Field(V p)
        {
            double result = 0;
            foreach (var k in kernels)
            {
                V d = p - k.Center;
                double x = Dot(d, k.Along) / k.X;
                double taper = Math.Clamp(1 + g.Taper * x * .28, .5, 1.5);
                double ripple = 1 + g.EdgeGrowth * .28 * Math.Sin(x * 5 + k.Phase);
                double y = Dot(d, k.Side) / (k.Y * taper * ripple);
                double z = Dot(d, k.Up) / (k.Z * taper);
                double q = 1 - (x * x + y * y + z * z) / 3.25;
                if (q > 0) result += q * q * q;
            }
            return result - .3;
        }
        V Normal(V p)
        {
            const double h = .06;
            return Unit(new V(Field(p - new V(h, 0)) - Field(p + new V(h, 0)), Field(p - new V(0, h)) - Field(p + new V(0, h)), Field(p - new V(0, 0, h)) - Field(p + new V(0, 0, h))));
        }
        V size = max - min;
        double longest = Math.Max(size.X, Math.Max(size.Y, size.Z));
        int nx = Math.Clamp((int)(resolution * size.X / longest), 9, resolution), ny = Math.Clamp((int)(resolution * size.Y / longest), 9, resolution), nz = Math.Clamp((int)(resolution * size.Z / longest), 9, resolution);
        var samples = new V[(nx + 1) * (ny + 1) * (nz + 1)]; var values = new double[samples.Length];
        int Index(int x, int y, int z) => (z * (ny + 1) + y) * (nx + 1) + x;
        for (int z = 0; z <= nz; z++) for (int y = 0; y <= ny; y++) for (int x = 0; x <= nx; x++)
        {
            int i = Index(x, y, z); samples[i] = new(min.X + size.X * x / nx, min.Y + size.Y * y / ny, min.Z + size.Z * z / nz); values[i] = Field(samples[i]);
        }
        var points = new List<V>(); var normals = new List<V>(); var triangles = new List<int>();
        var edges = new Dictionary<long, int>();
        int Edge(int a, int b)
        {
            if (a > b) (a, b) = (b, a);
            long key = ((long)a << 32) | (uint)b;
            if (edges.TryGetValue(key, out int index)) return index;
            double t = values[a] / (values[a] - values[b]);
            V p = samples[a] + (samples[b] - samples[a]) * t;
            index = points.Count; edges.Add(key, index); points.Add(p); normals.Add(Normal(p)); return index;
        }
        void Triangle(int a, int b, int c)
        {
            V normal = Cross(points[b] - points[a], points[c] - points[a]);
            if (normal.LengthSquared < 1e-12) return;
            if (Dot(normal, normals[a] + normals[b] + normals[c]) < 0) (b, c) = (c, b);
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
        }
        void Tetra(int a, int b, int c, int d)
        {
            Span<int> inside = stackalloc int[4], outside = stackalloc int[4];
            int ni = 0, no = 0;
            Span<int> tetra = stackalloc int[] { a, b, c, d };
            foreach (int i in tetra) { if (values[i] >= 0) inside[ni++] = i; else outside[no++] = i; }
            if (ni == 0 || ni == 4) return;
            if (ni == 1) Triangle(Edge(inside[0], outside[0]), Edge(inside[0], outside[1]), Edge(inside[0], outside[2]));
            else if (ni == 3) Triangle(Edge(outside[0], inside[0]), Edge(outside[0], inside[1]), Edge(outside[0], inside[2]));
            else
            {
                int p = Edge(inside[0], outside[0]), q = Edge(inside[0], outside[1]), r = Edge(inside[1], outside[0]), s = Edge(inside[1], outside[1]);
                Triangle(p, q, r); Triangle(q, s, r);
            }
        }
        for (int z = 0; z < nz; z++) for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++)
        {
            int a = Index(x, y, z), b = Index(x + 1, y, z), c = Index(x + 1, y + 1, z), d = Index(x, y + 1, z);
            int e = Index(x, y, z + 1), f = Index(x + 1, y, z + 1), h = Index(x, y + 1, z + 1), j = Index(x + 1, y + 1, z + 1);
            if ((values[a] > 0) == (values[b] > 0) && (values[a] > 0) == (values[c] > 0) && (values[a] > 0) == (values[d] > 0) && (values[a] > 0) == (values[e] > 0) && (values[a] > 0) == (values[f] > 0) && (values[a] > 0) == (values[h] > 0) && (values[a] > 0) == (values[j] > 0)) continue;
            Tetra(a, b, c, j); Tetra(a, c, d, j); Tetra(a, d, h, j); Tetra(a, h, e, j); Tetra(a, e, f, j); Tetra(a, f, b, j);
        }
        data.Vertices = points.ToArray(); data.Normals = normals.ToArray(); data.Triangles = triangles.ToArray();
        data.BoneA = new int[points.Count]; data.BoneB = new int[points.Count]; data.WeightA = new double[points.Count];
        data.Weights = new double[points.Count * cells];
        data.Minimum = new(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z));
        data.Maximum = new(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z));
        for (int i = 0; i < points.Count; i++)
        {
            int a = 0, b = 0; double da = double.MaxValue, db = double.MaxValue;
            for (int n = 0; n < nodes.Length; n++)
            {
                double distance = (points[i] - nodes[n]).LengthSquared;
                if (distance < da) { b = a; db = da; a = n; da = distance; } else if (distance < db) { b = n; db = distance; }
            }
            data.BoneA[i] = a; data.BoneB[i] = b; data.WeightA[i] = cells == 1 ? 1 : db / Math.Max(.0001, da + db);
            double sum = 0;
            for (int n = 0; n < cells; n++)
            {
                double falloff = 1 + (points[i] - nodes[n]).LengthSquared / (g.CellRadius(n) * g.CellRadius(n) * .7);
                double w = 1 / (falloff * falloff); data.Weights[i * cells + n] = w; sum += w;
            }
            for (int n = 0; n < cells; n++) data.Weights[i * cells + n] /= sum;
        }
        return data;
    }

    public V DeformVertex(int i, Genome g, double age, double phase)
    {
        var p = Vertices[i];
        double wavelength = Math.Max(5, g.Radius * (1.5 + g.Elongation));
        double wave = age * g.Rhythm * 2 + phase - p.X / wavelength * 3;
        double contraction = g.Contract * .16;
        double side = Math.Sin(wave) * contraction;
        double edge = Math.Clamp(Math.Abs(p.Y) / Math.Max(1, Math.Max(Math.Abs(Minimum.Y), Math.Abs(Maximum.Y))), 0, 1);
        return new(p.X * (1 - side * .35), p.Y * (1 + side) + Math.Sin(wave * .75) * g.Contract * g.Radius * .12, p.Z * (1 - side) + Math.Sin(wave + p.Y / wavelength * 2) * (g.EdgeGrowth + .15) * g.Contract * g.Radius * edge * .5);
    }
}
