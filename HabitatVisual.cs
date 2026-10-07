using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace Tideglass;

internal sealed class HabitatVisual
{
    public Model3DGroup Model { get; } = new();
    public GeometryModel3D Water => water;
    private readonly GeometryModel3D water = new(), growth = new(), foam = new(), puddles = new();
    private const int TerrainCols = World.Cols * 2, TerrainRows = World.Rows * 2;
    private const double TerrainStep = World.CellSize / 2;
    private const int TextureWidth = World.Cols * 4, TextureHeight = World.Rows * 4;
    private readonly double[] heights = new double[TextureWidth * TextureHeight];
    private readonly WriteableBitmap texture = new(TextureWidth, TextureHeight, 96, 96, PixelFormats.Bgra32, null);
    public HabitatVisual(World w)
    {
        for (int y = 0; y < TextureHeight; y++) for (int x = 0; x < TextureWidth; x++) heights[y * TextureWidth + x] = w.Ground(x * World.CellSize / 4, y * World.CellSize / 4);
        var mesh = new MeshGeometry3D();
        for (int y = 0; y <= TerrainRows; y++) for (int x = 0; x <= TerrainCols; x++)
        {
            double xx = x * TerrainStep, yy = y * TerrainStep;
            mesh.Positions.Add(Aquarium.Point(new(xx, yy, w.Ground(xx, yy))));
            var normal = new Vector3D(w.Ground(xx - 1, yy) - w.Ground(xx + 1, yy), 2, w.Ground(xx, yy - 1) - w.Ground(xx, yy + 1)); normal.Normalize();
            mesh.Normals.Add(normal); mesh.TextureCoordinates.Add(new((double)x / TerrainCols, (double)y / TerrainRows));
        }
        for (int y = 0; y < TerrainRows; y++) for (int x = 0; x < TerrainCols; x++)
        {
            int a = y * (TerrainCols + 1) + x, b = a + TerrainCols + 1;
            foreach (int i in new[] { a, b, a + 1, a + 1, b, b + 1 }) mesh.TriangleIndices.Add(i);
        }
        mesh.Freeze();
        Model.Children.Add(new GeometryModel3D(mesh, new DiffuseMaterial(new ImageBrush(texture))));
        water.Material = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(40, 61, 167, 175))); water.BackMaterial = water.Material;
        foam.Material = new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(145, 161, 214, 192)));
        var foliage = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(0, 1) };
        foliage.GradientStops.Add(new(Color.FromRgb(43, 77, 53), 0));
        foliage.GradientStops.Add(new(Color.FromRgb(91, 130, 65), .55));
        foliage.GradientStops.Add(new(Color.FromRgb(157, 171, 94), 1));
        growth.Material = new DiffuseMaterial(foliage); growth.BackMaterial = growth.Material;
        var poolTint = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(0, 1) };
        poolTint.GradientStops.Add(new(Color.FromArgb(0, 66, 136, 141), 0));
        poolTint.GradientStops.Add(new(Color.FromArgb(65, 66, 136, 141), .25));
        poolTint.GradientStops.Add(new(Color.FromArgb(115, 66, 136, 141), 1));
        puddles.Material = new DiffuseMaterial(poolTint); puddles.BackMaterial = puddles.Material;
        Model.Children.Add(foam); Model.Children.Add(growth); Model.Children.Add(puddles);
        Update(w);
    }
    public void Update(World w)
    {
        var pixels = new byte[TextureWidth * TextureHeight * 4];
        var surface = new MeshGeometry3D(); var plants = new MeshGeometry3D(); var edge = new MeshGeometry3D(); var pools = BuildPools(w);
        double level = w.WaterLevel;
        for (int y = 0; y < World.Rows; y++) for (int x = 0; x < World.Cols; x++)
        {
            int index = y * World.Cols + x;
            double xx = x * World.CellSize, yy = y * World.CellSize, h = w.Ground(xx, yy), depth = level - h;
            double algae = w.Biofilm[index] / 12;
            V[] corners = [new(xx, yy, h), new(xx, yy + World.CellSize, w.Ground(xx, yy + World.CellSize)), new(xx + World.CellSize, yy + World.CellSize, w.Ground(xx + World.CellSize, yy + World.CellSize)), new(xx + World.CellSize, yy, w.Ground(xx + World.CellSize, yy))];
            var clipped = new List<V>(6); var crossings = new List<V>(2);
            for (int k = 0; k < 4; k++)
            {
                V a = corners[k], b = corners[(k + 1) % 4];
                if (a.Z < level) clipped.Add(new(a.X, a.Y, level));
                if ((a.Z < level) != (b.Z < level))
                { double t = (level - a.Z) / (b.Z - a.Z); V crossing = new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, level + .3); clipped.Add(crossing); crossings.Add(crossing); }
            }
            int start = surface.Positions.Count;
            foreach (var p in clipped) surface.Positions.Add(Aquarium.Point(p));
            for (int k = 1; k < clipped.Count - 1; k++) foreach (int i in new[] { start, start + k, start + k + 1 }) surface.TriangleIndices.Add(i);
            if (crossings.Count == 2)
            {
                V d = crossings[1] - crossings[0]; V across = new(-d.Y, d.X, 0); across *= .7 / Math.Max(.01, across.Length);
                Triangle(edge, crossings[0] - across, crossings[1] - across, crossings[0] + across);
                Triangle(edge, crossings[1] - across, crossings[1] + across, crossings[0] + across);
            }
            if (algae > .16 && (index * 17 % 7 < 3))
            {
                uint hash = (uint)index * 2654435761u;
                double size = 2 + algae * 8, px = xx + 2 + hash % 15, py = yy + 2 + (hash >> 8) % 15;
                V p = new(px, py, w.Ground(px, py) + .4);
                for (int blade = 0; blade < 3; blade++)
                {
                    double angle = hash * .01 + blade * 2.1;
                    V along = new(Math.Cos(angle), Math.Sin(angle)), across = new(-Math.Sin(angle), Math.Cos(angle));
                    V mid = p + along * (size * .35) + new V(0, 0, size * .6);
                    V tip = p + along * size + new V(0, 0, size * .35);
                    Triangle(plants, p - across * (size * .18), mid - across * (size * .22), p + across * (size * .18));
                    Triangle(plants, p + across * (size * .18), mid - across * (size * .22), mid + across * (size * .22));
                    Triangle(plants, mid - across * (size * .22), tip, mid + across * (size * .22));
                }
            }
        }
        foreach (var rock in w.Rocks)
        {
            if (rock.Food < .15) continue;
            double size = 2 + Math.Sqrt(rock.Food) * 2.2;
            for (int n = 0; n < 18; n++)
            {
                double angle = n * 2.39996 + rock.Position.X * .13, height = .12 + .83 * n / 18.0;
                V normal = new(Math.Cos(angle) * Math.Sqrt(1 - height * height), Math.Sin(angle) * Math.Sqrt(1 - height * height), height);
                V p = rock.Position + normal * (rock.Radius * RockRadius(rock, normal) + .15);
                V side = new(-Math.Sin(angle), Math.Cos(angle));
                V flow = w.FlowAt(p) * .12;
                double length = size * (.65 + .35 * Math.Sin(n * 7.13 + rock.Position.Y));
                V bend = side * (length * .45) + flow;
                // Broad, curved ribbons grow out of the actual stone skin, rather than floating spikes.
                for (int segment = 0; segment < 4; segment++)
                {
                    double a = segment / 4.0, b = (segment + 1) / 4.0;
                    V ca = p + normal * (length * a * (1 - .25 * a)) + bend * (a * a);
                    V cb = p + normal * (length * b * (1 - .25 * b)) + bend * (b * b);
                    double wa = length * .16 * (1 - a) * (.55 + Math.Sin(a * Math.PI));
                    double wb = length * .16 * (1 - b) * (.55 + Math.Sin(b * Math.PI));
                    int first = plants.Positions.Count;
                    Triangle(plants, ca - side * wa, cb - side * wb, ca + side * wa);
                    Triangle(plants, ca + side * wa, cb - side * wb, cb + side * wb);
                    plants.TextureCoordinates[first] = new(0, a); plants.TextureCoordinates[first + 1] = new(0, b);
                    plants.TextureCoordinates[first + 2] = new(1, a); plants.TextureCoordinates[first + 3] = new(1, a);
                    plants.TextureCoordinates[first + 4] = new(0, b); plants.TextureCoordinates[first + 5] = new(1, b);
                }
            }
        }
        for (int y = 0; y < TextureHeight; y++) for (int x = 0; x < TextureWidth; x++)
        {
            int i = y * TextureWidth + x, tile = (y / 4) * World.Cols + x / 4;
            uint hash = unchecked((uint)i * 2246822519u); hash ^= hash >> 13;
            double grain = (.82 + (hash & 255) / 255.0 * .27) * (1 - Shoreline.Sediment(x * World.CellSize / 4, y * World.CellSize / 4) * .3);
            double wet = Math.Max(Math.Clamp((level - heights[i]) / 35, 0, 1), w.SoilMoisture[tile] * .8);
            double algae = w.Biofilm[tile] / 12;
            pixels[i * 4] = (byte)((107 * (1 - wet) + 83 * wet) * grain);
            pixels[i * 4 + 1] = (byte)((149 * (1 - wet) + 100 * wet) * grain);
            pixels[i * 4 + 2] = (byte)((174 * (1 - wet) + 67 * wet) * grain * (1 - algae * .18)); pixels[i * 4 + 3] = 255;
        }
        texture.WritePixels(new Int32Rect(0, 0, TextureWidth, TextureHeight), pixels, TextureWidth * 4, 0);
        pools.Freeze(); puddles.Geometry = pools;
        surface.Freeze(); plants.Freeze(); edge.Freeze(); water.Geometry = surface; growth.Geometry = plants; foam.Geometry = edge;
    }
    private readonly record struct PoolVertex(V Position, double Depth);
    private static MeshGeometry3D BuildPools(World w)
    {
        var mesh = new MeshGeometry3D();
        var nodes = new PoolVertex[(TerrainCols + 1) * (TerrainRows + 1)];
        double Sample(double[] values, double x, double y)
        {
            x = Math.Clamp(x / World.CellSize - .5, 0, World.Cols - 1);
            y = Math.Clamp(y / World.CellSize - .5, 0, World.Rows - 1);
            int ix = Math.Min((int)x, World.Cols - 2), iy = Math.Min((int)y, World.Rows - 2);
            double fx = x - ix, fy = y - iy; int i = iy * World.Cols + ix;
            return (values[i] * (1 - fx) + values[i + 1] * fx) * (1 - fy)
                + (values[i + World.Cols] * (1 - fx) + values[i + World.Cols + 1] * fx) * fy;
        }
        var levels = new double[World.Cols * World.Rows];
        for (int i = 0; i < levels.Length; i++)
            levels[i] = w.Ground((i % World.Cols + .5) * World.CellSize, (i / World.Cols + .5) * World.CellSize) + w.SurfaceWater[i];
        for (int y = 0; y <= TerrainRows; y++) for (int x = 0; x <= TerrainCols; x++)
        {
            double xx = x * TerrainStep, yy = y * TerrainStep, ground = w.Ground(xx, yy);
            double level = Sample(levels, xx, yy);
            double depth = Math.Min(Sample(w.SurfaceWater, xx, yy), level - ground) - .2;
            if (ground < w.WaterLevel + .2) depth = -1;
            nodes[y * (TerrainCols + 1) + x] = new(new(xx, yy, Math.Max(ground, level) + .10), depth);
        }
        void Clip(PoolVertex a, PoolVertex b, PoolVertex c)
        {
            if (a.Depth <= 0 && b.Depth <= 0 && c.Depth <= 0) return;
            Span<PoolVertex> triangle = [a, b, c];
            Span<PoolVertex> polygon = stackalloc PoolVertex[4]; int count = 0;
            for (int k = 0; k < 3; k++)
            {
                var from = triangle[k]; var to = triangle[(k + 1) % 3];
                if (from.Depth > 0) polygon[count++] = from;
                if ((from.Depth > 0) != (to.Depth > 0))
                {
                    double t = from.Depth / (from.Depth - to.Depth);
                    polygon[count++] = new(from.Position + (to.Position - from.Position) * t, 0);
                }
            }
            int start = mesh.Positions.Count;
            for (int k = 0; k < count; k++)
            {
                mesh.Positions.Add(Aquarium.Point(polygon[k].Position));
                mesh.TextureCoordinates.Add(new(.5, Math.Clamp(polygon[k].Depth / 4, 0, 1)));
            }
            for (int k = 1; k < count - 1; k++)
            { mesh.TriangleIndices.Add(start); mesh.TriangleIndices.Add(start + k); mesh.TriangleIndices.Add(start + k + 1); }
        }
        for (int y = 0; y < TerrainRows; y++) for (int x = 0; x < TerrainCols; x++)
        {
            int a = y * (TerrainCols + 1) + x, b = a + TerrainCols + 1;
            Clip(nodes[a], nodes[b], nodes[a + 1]); Clip(nodes[a + 1], nodes[b], nodes[b + 1]);
        }
        return mesh;
    }

    private static void Triangle(MeshGeometry3D mesh, V a, V b, V c)
    {
        int n = mesh.Positions.Count; mesh.Positions.Add(Aquarium.Point(a)); mesh.Positions.Add(Aquarium.Point(b)); mesh.Positions.Add(Aquarium.Point(c));
        mesh.TriangleIndices.Add(n); mesh.TriangleIndices.Add(n + 1); mesh.TriangleIndices.Add(n + 2);
        mesh.TextureCoordinates.Add(new(0, 0)); mesh.TextureCoordinates.Add(new(.5, 1)); mesh.TextureCoordinates.Add(new(1, 0));
    }

    internal static double RockRadius(Rock rock, V n)
        => .94 + .035 * Math.Sin(n.X * 4 + rock.Position.X) * Math.Cos(n.Y * 5 + n.Z * 3)
            + .025 * Math.Sin(n.Z * 11 + n.X * 3 + rock.Position.Y);

    internal static Material StoneMaterial()
    {
        const int width = 192, height = 96;
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            double angle = x * Math.PI * 2 / (width - 1), latitude = y * Math.PI / (height - 1);
            double nx = Math.Cos(angle) * Math.Sin(latitude), ny = Math.Sin(angle) * Math.Sin(latitude), nz = Math.Cos(latitude);
            double mottling = Math.Sin(nx * 13 + nz * 9) * Math.Sin(ny * 17 - nz * 7);
            double vein = Math.Pow(Math.Max(0, Math.Cos(nz * 35 + Math.Sin(nx * 6) + ny * 3)), 12);
            double grain = Math.Sin(nx * 211 + ny * 97 + nz * 137) * 3;
            double shade = 1 + mottling * .12 + vein * .13;
            int i = (y * width + x) * 4;
            pixels[i] = (byte)(79 * shade + grain); pixels[i + 1] = (byte)(84 * shade + grain);
            pixels[i + 2] = (byte)(80 * shade + grain); pixels[i + 3] = 255;
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4); bitmap.Freeze();
        var material = new DiffuseMaterial(new ImageBrush(bitmap)); material.Freeze(); return material;
    }
}
