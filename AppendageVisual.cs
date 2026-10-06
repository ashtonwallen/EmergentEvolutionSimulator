using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Tideglass;

internal sealed class AppendageVisual(Material material)
{
    public GeometryModel3D Model { get; } = new() { Material = material, BackMaterial = material };
    private double last = -100;
    public bool Due(double now, bool close) => now - last >= (close ? .065 : 1.2);
    public void Update(World world, Organism o, double now, bool close, BodySurface? skin)
    {
        if (!Due(now, close)) return;
        last = now;
        if (o.Genes.Buds != null) { UpdateBranches(world, o, skin); return; }
        var mesh = new MeshGeometry3D();
        V f = V.Direction(o.Heading, o.Pitch), s = new(-Math.Sin(o.Heading), Math.Cos(o.Heading), 0);
        V u = new(-Math.Cos(o.Heading) * Math.Sin(o.Pitch), -Math.Sin(o.Heading) * Math.Sin(o.Pitch), Math.Cos(o.Pitch));
        V center = o.Center;
        V Local(V p) { p -= center; return new(Dot(p, f), Dot(p, s), Dot(p, u)); }
        for (int n = 0; n < Appendages.Sites(o); n++)
        {
            if (o.Genes.Rule(n).Outgrowth < .025) continue;
            for (int side = -1; side <= 1; side += 2)
            {
                var p = Appendages.Pose(o, n, side, o.Age);
                // The contact solver bends compressed ends at the substrate.
                V tip = p.Tip;
                if (world.Tidal) tip = world.VisibleContact(tip, p.Radius * .3);
                V anchor = Local(p.Root);
                if (skin != null && n < skin.Nodes.Length)
                {
                    // Attach to the same smoothed scaffold used by the continuous skin.
                    V deform = new(); double sum = 0;
                    for (int k = 0; k < skin.Nodes.Length; k++)
                    {
                        V d = Local(o.Body[k].Position) - skin.Nodes[k];
                        double limit = o.Genes.CellRadius(k) * .85;
                        if (d.Length > limit) d *= limit / d.Length;
                        double falloff = 1 + (skin.Nodes[n] - skin.Nodes[k]).LengthSquared / (o.Genes.CellRadius(k) * o.Genes.CellRadius(k) * .7);
                        double weight = 1 / (falloff * falloff);
                        deform += d * weight; sum += weight;
                    }
                    anchor = skin.Nodes[n] + deform * (1 / sum) + new V(0, side * o.Genes.CellRadius(n) * .25);
                }
                V[] path = [anchor, Local(p.Joint), Local(tip)];
                int start = mesh.Positions.Count;
                const int sides = 7;
                for (int ring = 0; ring < 3; ring++)
                {
                    V tangent = path[Math.Min(2, ring + 1)] - path[Math.Max(0, ring - 1)];
                    tangent *= 1 / Math.Max(.0001, tangent.Length);
                    V a = Cross(tangent, Math.Abs(tangent.Z) > .9 ? new V(0, 1) : new V(0, 0, 1)); a *= 1 / Math.Max(.0001, a.Length);
                    V b = Cross(tangent, a);
                    double radius = p.Radius * (ring == 0 ? 1.3 : ring == 1 ? .85 : .22);
                    for (int j = 0; j < sides; j++)
                    {
                        double angle = j * Math.Tau / sides;
                        V normal = a * Math.Cos(angle) + b * Math.Sin(angle);
                        V point = path[ring] + normal * radius;
                        mesh.Positions.Add(new(point.X, point.Z, point.Y));
                        mesh.Normals.Add(new(normal.X, normal.Z, normal.Y));
                        mesh.TextureCoordinates.Add(new System.Windows.Point(ring * .4, (double)j / sides));
                    }
                }
                for (int ring = 0; ring < 2; ring++) for (int j = 0; j < sides; j++)
                {
                    int a = start + ring * sides + j, b = start + ring * sides + (j + 1) % sides, c = a + sides, d = b + sides;
                    foreach (int index in new[] { a, c, b, b, c, d }) mesh.TriangleIndices.Add(index);
                }
                for (int j = 1; j < sides - 1; j++)
                { mesh.TriangleIndices.Add(start + 2 * sides); mesh.TriangleIndices.Add(start + 2 * sides + j); mesh.TriangleIndices.Add(start + 2 * sides + j + 1); }
            }
        }
        mesh.Freeze(); Model.Geometry = mesh;
    }
    private Material? branchMaterial;
    private void UpdateBranches(World world, Organism o, BodySurface? skin)
    {
        var buds = o.Genes.Buds!;
        Span<BudPose> poses = stackalloc BudPose[buds.Count];
        Branching.Poses(o, o.Age, poses, world.WaterLevel);
        var mesh = new MeshGeometry3D();
        V f = V.Direction(o.Heading, o.Pitch), s = new(-Math.Sin(o.Heading), Math.Cos(o.Heading), 0), u = new(-Math.Cos(o.Heading) * Math.Sin(o.Pitch), -Math.Sin(o.Heading) * Math.Sin(o.Pitch), Math.Cos(o.Pitch));
        V center = o.Center;
        V Local(V p) { p -= center; return new(Dot(p, f), Dot(p, s), Dot(p, u)); }
        Span<V> ends = stackalloc V[buds.Count];
        for (int i = 0; i < buds.Count; i++)
        {
            var p = poses[i]; var b = buds[i];
            if (!p.Active) continue;
            V anchor = b.Parent >= 0 ? ends[b.Parent] : Local(p.Root);
            if (b.Parent < 0 && skin != null && p.Anchor < skin.Nodes.Length)
            {
                V deform = new(); double sum = 0;
                for (int k = 0; k < skin.Nodes.Length; k++)
                {
                    V d = Local(o.Body[k].Position) - skin.Nodes[k];
                    double limit = o.Genes.CellRadius(k) * .85;
                    if (d.Length > limit) d *= limit / d.Length;
                    double falloff = 1 + (skin.Nodes[p.Anchor] - skin.Nodes[k]).LengthSquared / (o.Genes.CellRadius(k) * o.Genes.CellRadius(k) * .7);
                    double weight = 1 / (falloff * falloff); deform += d * weight; sum += weight;
                }
                V outward = Local(p.Root) - Local(o.Body[p.Anchor].Position);
                anchor = skin.Nodes[p.Anchor] + deform * (1 / sum) + outward * .95;
            }
            V tip = p.Tip, joint = p.Joint;
            if (world.Tidal)
            {
                tip = world.VisibleContact(tip, p.Radius * p.Flatness * .2);
                joint = world.VisibleContact(joint, p.Radius * p.Flatness * .3);
            }
            ends[i] = Local(tip);
            V[] path = [anchor, Local(joint), ends[i]];
            int start = mesh.Positions.Count;
            const int sides = 9, rings = 5;
            double tissue = o.BudStates[i].Integrity < .65 ? .875 : b.Photo > .35 && b.Photo > b.Digestion ? .625 : b.Digestion > .4 ? .875 : b.Stiffness > .6 ? .375 : .125;
            int onlyChild = -1, children = 0;
            for (int k = i + 1; k < buds.Count; k++) if (buds[k].Parent == i && poses[k].Active) { onlyChild = k; children++; }
            V startDirection = Unit(path[1] - path[0]), endDirection = Unit(path[2] - path[1]);
            double startRadius = p.Radius * 1.05, endRadius = p.Radius * b.Taper;
            double startFlatness = p.Flatness, endFlatness = p.Flatness;
            if (b.Parent >= 0)
            {
                int siblings = 0;
                for (int k = b.Parent + 1; k < buds.Count; k++) if (buds[k].Parent == b.Parent && poses[k].Active) siblings++;
                if (siblings == 1)
                {
                    startDirection = Unit(Unit(Local(poses[b.Parent].Tip) - Local(poses[b.Parent].Joint)) + startDirection);
                    startRadius = Math.Max(startRadius, poses[b.Parent].Radius * buds[b.Parent].Taper);
                    startFlatness = (p.Flatness + poses[b.Parent].Flatness) * .5;
                }
            }
            if (children == 1)
            {
                endDirection = Unit(endDirection + Unit(Local(poses[onlyChild].Joint) - Local(poses[onlyChild].Root)));
                endRadius = Math.Max(endRadius, poses[onlyChild].Radius * 1.05);
                endFlatness = (p.Flatness + poses[onlyChild].Flatness) * .5;
            }
            for (int ring = 0; ring < rings; ring++)
            {
                double t = (double)ring / (rings - 1);
                V control = path[1] * 2 - (path[0] + path[2]) * .5;
                V at = path[0] * ((1 - t) * (1 - t)) + control * (2 * t * (1 - t)) + path[2] * (t * t);
                V tangent = Unit((control - path[0]) * (1 - t) + (path[2] - control) * t);
                if (ring == 0) tangent = startDirection;
                if (ring == rings - 1) tangent = endDirection;
                V a = Cross(tangent, Math.Abs(tangent.Z) > .9 ? new V(0, 1) : new V(0, 0, 1)); a = Unit(a);
                V normalB = Cross(tangent, a);
                double radius = t < .5 ? startRadius * (1 - 2 * t) + p.Radius * 2 * t : p.Radius * (2 - 2 * t) + endRadius * (2 * t - 1);
                double flatness = t < .5 ? startFlatness * (1 - 2 * t) + p.Flatness * 2 * t : p.Flatness * (2 - 2 * t) + endFlatness * (2 * t - 1);
                for (int j = 0; j < sides; j++)
                {
                    double angle = j * Math.Tau / sides;
                    V point = at + a * (Math.Cos(angle) * radius) + normalB * (Math.Sin(angle) * radius * flatness);
                    V normal = Unit(a * Math.Cos(angle) + normalB * (Math.Sin(angle) / flatness));
                    mesh.Positions.Add(new(point.X, point.Z, point.Y)); mesh.Normals.Add(new(normal.X, normal.Z, normal.Y));
                    mesh.TextureCoordinates.Add(new System.Windows.Point(.15 + .7 * j / sides, tissue));
                }
            }
            for (int ring = 0; ring < rings - 1; ring++) for (int j = 0; j < sides; j++)
            {
                int a = start + ring * sides + j, b0 = start + ring * sides + (j + 1) % sides, c = a + sides, d = b0 + sides;
                foreach (int index in new[] { a, c, b0, b0, c, d }) mesh.TriangleIndices.Add(index);
            }
            int cap = mesh.Positions.Count; mesh.Positions.Add(new(ends[i].X, ends[i].Z, ends[i].Y)); mesh.Normals.Add(new(endDirection.X, endDirection.Z, endDirection.Y)); mesh.TextureCoordinates.Add(new(.5, tissue));
            for (int j = 0; j < sides; j++) foreach (int index in new[] { cap, start + sides * (rings - 1) + j, start + sides * (rings - 1) + (j + 1) % sides }) mesh.TriangleIndices.Add(index);
            int rootCap = mesh.Positions.Count; mesh.Positions.Add(new(anchor.X, anchor.Z, anchor.Y)); mesh.Normals.Add(new(-startDirection.X, -startDirection.Z, -startDirection.Y)); mesh.TextureCoordinates.Add(new(.5, tissue));
            for (int j = 0; j < sides; j++) foreach (int index in new[] { rootCap, start + (j + 1) % sides, start + j }) mesh.TriangleIndices.Add(index);
        }
        mesh.Freeze(); Model.Geometry = mesh;
        branchMaterial ??= TissueMaterial(o.Genes);
        Model.Material = branchMaterial; Model.BackMaterial = branchMaterial;
    }
    private static Material TissueMaterial(Genome g)
    {
        const int width = 32, height = 32; var pixels = new byte[width * height * 4];
        double angle = g.Hue * Math.Tau;
        double red = 169 + 62 * Math.Cos(angle), green = 143 + 48 * Math.Cos(angle - 2), blue = 137 + 57 * Math.Cos(angle + 2);
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            int row = y / 8; double shade = .72 + .25 * Math.Sin((x + .5) * Math.PI / width);
            double r = red, g0 = green, b = blue;
            if (row == 1) { r = r * .65 + 60; g0 = g0 * .65 + 48; b = b * .65 + 25; }
            if (row == 2) { r = r * .25 + 54; g0 = g0 * .25 + 122; b = b * .25 + 49; }
            if (row == 3) { r = r * .4 + 122; g0 = g0 * .4 + 46; b = b * .4 + 42; }
            int i = (y * width + x) * 4; pixels[i] = (byte)Math.Clamp(b * shade, 0, 255); pixels[i + 1] = (byte)Math.Clamp(g0 * shade, 0, 255); pixels[i + 2] = (byte)Math.Clamp(r * shade, 0, 255); pixels[i + 3] = 255;
        }
        var texture = System.Windows.Media.Imaging.BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4); texture.Freeze();
        var result = new MaterialGroup(); result.Children.Add(new DiffuseMaterial(new ImageBrush(texture))); result.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(45, 167, 187, 167)), 22)); result.Freeze(); return result;
    }
    private static V Unit(V p) => p * (1 / Math.Max(.00001, p.Length));
    private static double Dot(V a, V b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static V Cross(V a, V b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
}
