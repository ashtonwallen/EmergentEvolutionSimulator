using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Tideglass;

/// <summary>Builds tissue meshes off the UI thread; only nearby bodies receive vertex deformation.</summary>
internal sealed class OrganicVisual
{
    private sealed record Asset(BodySurface Surface, MeshGeometry3D Mesh, int Cells, int Resolution);
    private static readonly SemaphoreSlim builders = new(2);
    private static readonly Lazy<Asset> placeholder = new(() => Build(new Genome { Radius = 1 }, 1, 16));
    public GeometryModel3D Model { get; }
    public double Extent => asset?.Surface.Extent ?? 15;
    public BodySurface? Surface => asset?.Surface;
    public string? Error { get; private set; }
    private Asset? asset;
    private Task<Asset>? pending;
    private MeshGeometry3D? deforming;
    private double lastDeform = -1;
    private V[] positions = [], normals = [], displacement = [];
    private Point3D[] wpfPoints = [];
    private Vector3D[] wpfNormals = [];

    public OrganicVisual(Organism o, Material material)
    {
        Model = new GeometryModel3D(placeholder.Value.Mesh, material) { Transform = new ScaleTransform3D(o.Genes.Radius, o.Genes.Radius, o.Genes.Radius) };
    }
    public bool NeedsWork(Organism o, bool close) => pending?.IsCompleted == true || (pending == null && Error == null && (asset == null || asset.Cells != o.Body.Count || asset.Resolution < (close ? 30 : 17)));
    public void Prepare(Organism o, bool close)
    {
        if (pending?.IsCompleted == true)
        {
            if (pending.IsCompletedSuccessfully)
            {
                asset = pending.Result; Model.Geometry = asset.Mesh; Model.Transform = Transform3D.Identity; deforming = null;
                int n = asset.Surface.Vertices.Length;
                positions = new V[n]; normals = new V[n]; wpfPoints = new Point3D[n]; wpfNormals = new Vector3D[n]; displacement = new V[asset.Cells];
            }
            else Error = pending.Exception?.GetBaseException().Message ?? "Tissue mesh generation failed";
            pending = null;
        }
        int quality = close ? 30 : 17;
        if (pending != null || Error != null || (asset != null && asset.Cells == o.Body.Count && asset.Resolution >= quality)) return;
        Genome genes = o.Genes; int count = o.Body.Count;
        pending = Task.Run(async () =>
        {
            // A selected creature should not wait behind a whole ecosystem's low-detail work.
            if (!close) await builders.WaitAsync().ConfigureAwait(false);
            try { return Build(genes, count, quality); }
            finally { if (!close) builders.Release(); }
        });
    }
    public void Animate(Organism o, double now, bool animate)
    {
        if (asset == null) return;
        if (!animate)
        {
            if (deforming != null) { Model.Geometry = asset.Mesh; deforming = null; }
            return;
        }
        if (now - lastDeform < .075) return;
        lastDeform = now;
        if (deforming == null) { deforming = asset.Mesh.Clone(); Model.Geometry = deforming; }
        var data = asset.Surface;
        V center = o.Center, forward = V.Direction(o.Heading, o.Pitch), side = new(-Math.Sin(o.Heading), Math.Cos(o.Heading), 0), up = new(-Math.Cos(o.Heading) * Math.Sin(o.Pitch), -Math.Sin(o.Heading) * Math.Sin(o.Pitch), Math.Cos(o.Pitch));
        for (int n = 0; n < asset.Cells; n++)
        {
            var d = o.Body[n].Position - center;
            displacement[n] = new V(Dot(d, forward), Dot(d, side), Dot(d, up)) - data.Nodes[n];
            double maximum = o.Genes.CellRadius(n) * .85;
            if (displacement[n].Length > maximum) displacement[n] *= maximum / displacement[n].Length;
        }
        Array.Clear(normals);
        for (int i = 0; i < positions.Length; i++)
        {
            V deform = new();
            for (int n = 0; n < asset.Cells; n++)
            {
                double weight = data.Weights[i * asset.Cells + n];
                deform += displacement[n] * weight;
                deform -= (data.Vertices[i] - data.Nodes[n]) * ((1 - o.Body[n].Integrity) * .3 * weight);
            }
            positions[i] = data.DeformVertex(i, o.Genes, o.Age, o.Phase) + deform;
            var p = positions[i]; wpfPoints[i] = new(p.X, p.Z, p.Y);
        }
        for (int t = 0; t < data.Triangles.Length; t += 3)
        {
            int a = data.Triangles[t], b = data.Triangles[t + 1], c = data.Triangles[t + 2];
            var normal = Cross(positions[b] - positions[a], positions[c] - positions[a]);
            normals[a] += normal; normals[b] += normal; normals[c] += normal;
        }
        for (int i = 0; i < normals.Length; i++)
        {
            var n = normals[i] * (1 / Math.Max(.00001, normals[i].Length)); wpfNormals[i] = new(n.X, n.Z, n.Y);
        }
        var points = new Point3DCollection(wpfPoints); points.Freeze();
        var ns = new Vector3DCollection(wpfNormals); ns.Freeze();
        deforming.Positions = points; deforming.Normals = ns;
    }
    private static double Dot(V a, V b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static V Cross(V a, V b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    private static Asset Build(Genome genes, int count, int resolution)
    {
        var surface = BodySurface.Create(genes, count, resolution);
        var mesh = new MeshGeometry3D();
        for (int i = 0; i < surface.Vertices.Length; i++)
        {
            var p = surface.Vertices[i]; var n = surface.Normals[i];
            mesh.Positions.Add(new Point3D(p.X, p.Z, p.Y)); mesh.Normals.Add(new Vector3D(n.X, n.Z, n.Y));
            mesh.TextureCoordinates.Add(new Point((p.X - surface.Minimum.X) / Math.Max(.001, surface.Maximum.X - surface.Minimum.X), .5 + Math.Atan2(p.Z, p.Y) / Math.Tau));
        }
        for (int i = 0; i < surface.Triangles.Length; i += 3)
        {
            mesh.TriangleIndices.Add(surface.Triangles[i]); mesh.TriangleIndices.Add(surface.Triangles[i + 2]); mesh.TriangleIndices.Add(surface.Triangles[i + 1]);
        }
        mesh.Freeze(); return new(surface, mesh, count, resolution);
    }
}
