namespace Tideglass;

public readonly record struct BranchContact(V Point, V Normal, double Penetration, double Fraction, Rock? Rock);

public static class BranchGeometry
{
    public static double RadiusAt(BudPose p, double t) => p.Radius * (t <= .5 ? 1.05 - .1 * t : 1 + (p.Taper - 1) * (2 * t - 1));
    public static V Nearest(BudPose p, V point, out double fraction)
    {
        double a = ClosestFraction(p.Root, p.Joint, point), b = ClosestFraction(p.Joint, p.Tip, point);
        V pa = Point(p, a * .5), pb = Point(p, (1 + b) * .5);
        if ((point - pa).LengthSquared < (point - pb).LengthSquared) { fraction = a * .5; return pa; }
        fraction = (1 + b) * .5; return pb;
    }
    public static double Support(BudPose p, double fraction, V normal)
        => new SectionFrame(fraction <= .5 ? p.Joint - p.Root : p.Tip - p.Joint).Support(p, fraction, normal);

    public readonly struct SectionFrame
    {
        private readonly V side, up;
        public SectionFrame(V direction)
        {
            V axis = BodyMechanics.Unit(direction);
            side = BodyMechanics.Unit(BodyMechanics.Cross(axis, Math.Abs(axis.Z) > .9 ? new V(0, 1) : new V(0, 0, 1)));
            up = BodyMechanics.Cross(axis, side);
        }
        public double Support(BudPose p, double fraction, V normal) => RadiusAt(p, fraction) * Math.Sqrt(Math.Pow(BodyMechanics.Dot(normal, side), 2)
            + Math.Pow(BodyMechanics.Dot(normal, up) * p.Flatness, 2));
    }
    public static double Gap(BudPose p, V point)
    {
        V nearest = Nearest(p, point, out double fraction), offset = point - nearest;
        return offset.Length - Support(p, fraction, BodyMechanics.Unit(offset));
    }
    public static V Point(BudPose p, double fraction) => fraction <= .5
        ? p.Root + (p.Joint - p.Root) * (fraction * 2)
        : p.Joint + (p.Tip - p.Joint) * (fraction * 2 - 1);

    public static double ClosestFraction(V a, V b, V point)
    {
        V d = b - a, q = point - a;
        return Math.Clamp((d.X * q.X + d.Y * q.Y + d.Z * q.Z) / Math.Max(1e-12, d.LengthSquared), 0, 1);
    }

    public static double DistanceSquared(BudPose p, V point)
    {
        V a = p.Root + (p.Joint - p.Root) * ClosestFraction(p.Root, p.Joint, point);
        V b = p.Joint + (p.Tip - p.Joint) * ClosestFraction(p.Joint, p.Tip, point);
        return Math.Min((point - a).LengthSquared, (point - b).LengthSquared);
    }
}

public sealed partial class World
{
    // One representative contact per branch. Sampling density cannot multiply grip or meals.
    // Rocks use exact segment/sphere proximity; terrain samples are at most 5 units apart
    // except at the 32-interval per-segment computation limit for extreme deformations.
    public bool BranchSurfaceContact(BudPose pose, out BranchContact contact)
    {
        contact = default;
        if (!pose.Active) return false;
        var first = new BranchGeometry.SectionFrame(pose.Joint - pose.Root);
        var second = new BranchGeometry.SectionFrame(pose.Tip - pose.Joint);
        double radius = pose.Radius * Math.Max(1.05, pose.Taper) * Math.Max(1, pose.Flatness);
        var best = new BranchContact(pose.Tip, new V(0, 0, 1), Ground(pose.Tip.X, pose.Tip.Y) + second.Support(pose, 1, new V(0, 0, 1)) - pose.Tip.Z, 1, null);
        for (int half = 0; half < 2; half++)
        {
            V a = half == 0 ? pose.Root : pose.Joint, b = half == 0 ? pose.Joint : pose.Tip;
            V d = b - a;
            int intervals = Math.Clamp((int)Math.Ceiling(d.Length / 5), 1, 32);
            for (int n = 0; n <= intervals; n++)
            {
                double t = (double)n / intervals;
                V point = a + d * t;
                double fraction = (half + t) * .5;
                double penetration = Ground(point.X, point.Y) + (fraction <= .5 ? first : second).Support(pose, fraction, new V(0, 0, 1)) - point.Z;
                if (penetration > best.Penetration)
                    best = new(point, new V(0, 0, 1), penetration, (half + t) * .5, null);
            }
            // The spatial map already includes a 64-unit margin around each rock.
            var rocks = d.Length * .5 + radius <= 64 ? NearbyRocks((a + b) * .5) : Rocks;
            foreach (var rock in rocks)
            {
                double t = BranchGeometry.ClosestFraction(a, b, rock.Position);
                V point = a + d * t, offset = point - rock.Position;
                double outerReach = rock.Radius + radius + 1e-9;
                if (offset.LengthSquared > outerReach * outerReach) continue;
                double fraction = (half + t) * .5;
                double contactRadius = (fraction <= .5 ? first : second).Support(pose, fraction, BodyMechanics.Unit(offset));
                double reach = rock.Radius + contactRadius - Math.Max(0, best.Penetration);
                if (reach <= 0 || offset.LengthSquared >= reach * reach) continue;
                double distance = offset.Length;
                best = new(point, distance > 1e-9 ? offset * (1 / distance) : new V(0, 0, 1),
                    rock.Radius + contactRadius - distance, (half + t) * .5, rock);
            }
        }
        contact = best;
        return best.Penetration > 0;
    }
}
