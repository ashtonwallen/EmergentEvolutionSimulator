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
    private const int TextureWidth = World.Cols * 4, TextureHeight = World.Rows * 4;
    private readonly double[] heights = new double[TextureWidth * TextureHeight];
    private readonly WriteableBitmap texture = new(TextureWidth, TextureHeight, 96, 96, PixelFormats.Bgra32, null);
    public HabitatVisual(World w)
    {
        for (int y = 0; y < TextureHeight; y++) for (int x = 0; x < TextureWidth; x++) heights[y * TextureWidth + x] = w.Ground(x * World.CellSize / 4, y * World.CellSize / 4);
        var mesh = new MeshGeometry3D();
        for (int y = 0; y <= World.Rows; y++) for (int x = 0; x <= World.Cols; x++)
        {
            double xx = x * World.CellSize, yy = y * World.CellSize;
            mesh.Positions.Add(Aquarium.Point(new(xx, yy, w.Ground(xx, yy))));
            var normal = new Vector3D(w.Ground(xx - 1, yy) - w.Ground(xx + 1, yy), 2, w.Ground(xx, yy - 1) - w.Ground(xx, yy + 1)); normal.Normalize();
            mesh.Normals.Add(normal); mesh.TextureCoordinates.Add(new((double)x / World.Cols, (double)y / World.Rows));
        }
        for (int y = 0; y < World.Rows; y++) for (int x = 0; x < World.Cols; x++)
        {
            int a = y * (World.Cols + 1) + x, b = a + World.Cols + 1;
            foreach (int i in new[] { a, b, a + 1, a + 1, b, b + 1 }) mesh.TriangleIndices.Add(i);
        }
        mesh.Freeze();
        Model.Children.Add(new GeometryModel3D(mesh, new DiffuseMaterial(new ImageBrush(texture))));
        water.Material = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(40, 61, 167, 175))); water.BackMaterial = water.Material;
        foam.Material = new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(145, 161, 214, 192)));
        growth.Material = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(114, 158, 59))); growth.BackMaterial = growth.Material;
        puddles.Material = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(155, 66, 136, 141))); puddles.BackMaterial = puddles.Material;
        Model.Children.Add(foam); Model.Children.Add(growth); Model.Children.Add(puddles);
        Update(w);
    }
    public void Update(World w)
    {
        var pixels = new byte[TextureWidth * TextureHeight * 4];
        var surface = new MeshGeometry3D(); var plants = new MeshGeometry3D(); var edge = new MeshGeometry3D(); var pools = new MeshGeometry3D();
        double level = w.WaterLevel;
        for (int y = 0; y < World.Rows; y++) for (int x = 0; x < World.Cols; x++)
        {
            int index = y * World.Cols + x;
            double xx = x * World.CellSize, yy = y * World.CellSize, h = w.Ground(xx, yy), depth = level - h;
            double algae = w.Biofilm[index] / 12;
            V[] corners = [new(xx, yy, h), new(xx, yy + World.CellSize, w.Ground(xx, yy + World.CellSize)), new(xx + World.CellSize, yy + World.CellSize, w.Ground(xx + World.CellSize, yy + World.CellSize)), new(xx + World.CellSize, yy, w.Ground(xx + World.CellSize, yy))];
            if (w.SurfaceWater[index] > .15 && h > level)
            {
                double poolLevel = w.PuddleLevel(new V(xx + World.CellSize * .5, yy + World.CellSize * .5));
                var poolCorners = new List<V>(6);
                for (int k = 0; k < 4; k++)
                {
                    V a = corners[k], b = corners[(k + 1) % 4];
                    if (a.Z < poolLevel) poolCorners.Add(new(a.X, a.Y, poolLevel + .12));
                    if ((a.Z < poolLevel) != (b.Z < poolLevel))
                    {
                        double t = (poolLevel - a.Z) / (b.Z - a.Z);
                        poolCorners.Add(new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, poolLevel + .12));
                    }
                }
                for (int k = 1; k < poolCorners.Count - 1; k++) Triangle(pools, poolCorners[0], poolCorners[k], poolCorners[k + 1]);
            }
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
            double size = 2 + rock.Food * .9;
            for (int n = 0; n < 14; n++)
            {
                double angle = n * 2.39996, height = .15 + .8 * n / 14.0;
                V normal = new(Math.Cos(angle) * Math.Sqrt(1 - height * height), Math.Sin(angle) * Math.Sqrt(1 - height * height), height);
                V p = rock.Position + normal * (rock.Radius + .3);
                V side = new(-Math.Sin(angle), Math.Cos(angle));
                V flow = w.FlowAt(p) * .35;
                Triangle(plants, p - side * (size * .25), p + normal * size + flow, p + side * (size * .25));
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
    private static void Triangle(MeshGeometry3D mesh, V a, V b, V c)
    {
        int n = mesh.Positions.Count; mesh.Positions.Add(Aquarium.Point(a)); mesh.Positions.Add(Aquarium.Point(b)); mesh.Positions.Add(Aquarium.Point(c));
        mesh.TriangleIndices.Add(n); mesh.TriangleIndices.Add(n + 1); mesh.TriangleIndices.Add(n + 2);
    }
}
