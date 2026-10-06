using System.Windows;
using System.Windows.Media;

namespace Tideglass;

// A small orthographic anatomy diagram; the habitat itself is rendered in 3D.
public sealed class SpecimenView(Aquarium aquarium) : FrameworkElement
{
    private static readonly Brush fill = new SolidColorBrush(Color.FromRgb(78, 143, 130));
    private static readonly Pen edge = new(new SolidColorBrush(Color.FromRgb(173, 206, 166)), .5);
    protected override void OnRender(DrawingContext dc)
    {
        var o = aquarium.Selected; if (o == null) return;
        var surface = aquarium.SelectedSurface;
        if (surface != null)
        {
            double appendageExtent = Branching.Extent(o) * 2;
            double zoom = Math.Min((ActualWidth - 12) / Math.Max(1, surface.Maximum.X - surface.Minimum.X + appendageExtent), (ActualHeight - 12) / Math.Max(1, surface.Maximum.Y - surface.Minimum.Y + (surface.Maximum.Z - surface.Minimum.Z) * .4 + appendageExtent));
            var shape = new StreamGeometry { FillRule = FillRule.Nonzero };
            Point Project(int i) { var p = surface.DeformVertex(i, o.Genes, o.Age, o.Phase); return new Point(p.X, p.Y * .85 - p.Z * .35); }
            using (var context = shape.Open())
            {
                for (int t = 0; t < surface.Triangles.Length; t += 3)
                {
                    Point a = Project(surface.Triangles[t]), b = Project(surface.Triangles[t + 1]), c = Project(surface.Triangles[t + 2]);
                    if ((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X) < 0) continue;
                    context.BeginFigure(a, true, true); context.LineTo(b, true, false); context.LineTo(c, true, false);
                }
            }
            dc.PushTransform(new TranslateTransform(ActualWidth / 2, ActualHeight / 2)); dc.PushTransform(new ScaleTransform(zoom, zoom));
            if (o.Genes.Buds == null) for (int n = 0; n < Appendages.Sites(o); n++) for (int side = -1; side <= 1; side += 2)
            {
                var limb = Appendages.Pose(o, n, side, o.Age);
                Point P(V v)
                {
                    v -= o.Center;
                    double x = v.X * Math.Cos(o.Heading) + v.Y * Math.Sin(o.Heading);
                    double y = -v.X * Math.Sin(o.Heading) + v.Y * Math.Cos(o.Heading);
                    return new(x, y * .85 - v.Z * .35);
                }
                var pen = new Pen(fill, limb.Radius * 1.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                dc.DrawLine(pen, P(limb.Root), P(limb.Joint)); dc.DrawLine(pen, P(limb.Joint), P(limb.Tip));
            }
            if (o.Genes.Buds != null)
            {
                Span<BudPose> poses = stackalloc BudPose[o.Genes.Buds.Count];
                Branching.Poses(o, o.Age, poses, aquarium.World.WaterLevel);
                Point P(V v)
                {
                    v -= o.Center;
                    double x = v.X * Math.Cos(o.Heading) + v.Y * Math.Sin(o.Heading);
                    double y = -v.X * Math.Sin(o.Heading) + v.Y * Math.Cos(o.Heading);
                    return new(x, y * .85 - v.Z * .35);
                }
                foreach (var limb in poses)
                {
                    if (!limb.Active) continue;
                    var pen = new Pen(fill, limb.Radius * 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                    dc.DrawLine(pen, P(limb.Root), P(limb.Joint)); dc.DrawLine(pen, P(limb.Joint), P(limb.Tip));
                }
            }
            dc.DrawGeometry(fill, null, shape); dc.Pop(); dc.Pop(); return;
        }
        V center = o.Center;
        double extent = o.Body.Max(c => (c.Position - center).Length) + o.Genes.Radius * 1.7;
        double scale = Math.Min(4, Math.Min(ActualHeight, ActualWidth) / (extent * 2));
        dc.PushTransform(new TranslateTransform(ActualWidth / 2, ActualHeight / 2)); dc.PushTransform(new ScaleTransform(scale, scale));
        foreach (var c in o.Body)
        {
            V p = c.Position - center;
            if (c.Parent >= 0) { V q = o.Body[c.Parent].Position - center; dc.DrawLine(new Pen(fill, o.Genes.Radius), new(p.X, p.Y * .7 - p.Z * .5), new(q.X, q.Y * .7 - q.Z * .5)); }
        }
        for (int i = 0; i < o.Body.Count; i++)
        {
            V p = o.Body[i].Position - center; double r = o.Genes.CellRadius(i);
            dc.DrawEllipse(fill, edge, new(p.X, p.Y * .7 - p.Z * .5), r, r * .9);
            dc.DrawEllipse(edge.Brush, null, new(p.X - r * .2, p.Y * .7 - p.Z * .5 - r * .2), r * .23, r * .23);
        }
        dc.Pop(); dc.Pop();
    }
}
