using Tideglass;

static class PerformanceChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    // Independent, unpruned narrow-phase formulas are the oracle for fast rejection.
    static double CoreSupport(Organism o, int i, V normal)
    {
        V axis = V.Direction(o.Heading + o.Body[i].Angle, o.Pitch + o.Body[i].Elevation);
        V side = BodyMechanics.Unit(BodyMechanics.Cross(new V(0, 0, 1), axis));
        V up = BodyMechanics.Unit(BodyMechanics.Cross(axis, side));
        return o.Genes.CellRadius(i) * Math.Sqrt(Math.Pow(BodyMechanics.Dot(normal, axis) * o.Genes.Elongation, 2)
            + Math.Pow(BodyMechanics.Dot(normal, side) * .95, 2) + Math.Pow(BodyMechanics.Dot(normal, up) * o.Genes.Flattening, 2));
    }

    static double BranchSupport(BudPose p, double fraction, V normal)
    {
        V axis = BodyMechanics.Unit(fraction <= .5 ? p.Joint - p.Root : p.Tip - p.Joint);
        V side = BodyMechanics.Unit(BodyMechanics.Cross(axis, Math.Abs(axis.Z) > .9 ? new V(0, 1) : new V(0, 0, 1)));
        V up = BodyMechanics.Cross(axis, side);
        return BranchGeometry.RadiusAt(p, fraction) * Math.Sqrt(Math.Pow(BodyMechanics.Dot(normal, side), 2)
            + Math.Pow(BodyMechanics.Dot(normal, up) * p.Flatness, 2));
    }

    static BranchContact ReferenceContact(World w, BudPose pose)
    {
        var best = new BranchContact(pose.Tip, new V(0, 0, 1), w.Ground(pose.Tip.X, pose.Tip.Y)
            + BranchSupport(pose, 1, new V(0, 0, 1)) - pose.Tip.Z, 1, null);
        for (int half = 0; half < 2; half++)
        {
            V a = half == 0 ? pose.Root : pose.Joint, b = half == 0 ? pose.Joint : pose.Tip, d = b - a;
            int intervals = Math.Clamp((int)Math.Ceiling(d.Length / 5), 1, 32);
            for (int n = 0; n <= intervals; n++)
            {
                double t = (double)n / intervals; V point = a + d * t;
                double penetration = w.Ground(point.X, point.Y) + BranchSupport(pose, (half + t) * .5, new V(0, 0, 1)) - point.Z;
                if (penetration > best.Penetration) best = new(point, new V(0, 0, 1), penetration, (half + t) * .5, null);
            }
            foreach (var rock in w.Rocks)
            {
                double t = BranchGeometry.ClosestFraction(a, b, rock.Position);
                V point = a + d * t, offset = point - rock.Position;
                double radius = BranchSupport(pose, (half + t) * .5, BodyMechanics.Unit(offset));
                double reach = rock.Radius + radius - Math.Max(0, best.Penetration);
                if (reach <= 0 || offset.LengthSquared >= reach * reach) continue;
                double distance = offset.Length;
                best = new(point, distance > 1e-9 ? offset * (1 / distance) : new V(0, 0, 1),
                    rock.Radius + radius - distance, (half + t) * .5, rock);
            }
        }
        return best;
    }

    public static void Run()
    {
        var random = new Random(90217);
        double Range(double min, double max) => min + random.NextDouble() * (max - min);
        V Offset(double span) => new(Range(-span, span), Range(-span, span), Range(-span, span));
        var world = new World();
        for (int trial = 0; trial < 1000; trial++)
        {
            var o = new Organism { Heading = Range(-Math.PI, Math.PI), Pitch = Range(-Math.PI, Math.PI),
                Genes = new Genome { Cells = 1, Radius = Range(1, 12), Elongation = Range(.1, 4), Flattening = Range(.05, 2), Buds = [] },
                Body = [new Cell { Position = Offset(10), Angle = Range(-3, 3), Elevation = Range(-2, 2) }] };
            world.CacheAnatomy(o);
            var shape = new BodyMechanics.CellShape(o, 0);
            for (int query = 0; query < 8; query++)
            {
                V point = o.Center + Offset(query < 4 ? 8 : 100), direction = BodyMechanics.Unit(point - o.Center);
                double radius = Range(.01, 12), reference = CoreSupport(o, 0, direction);
                Check(shape.Support(direction) == reference, "Cached core geometry changed support");
                bool touching = (o.Center - point).Length < radius + reference;
                Check(world.TissueContact(o, point, radius) == touching, "Core broad phase rejected actual contact");
            }
        }
        var coast = new World { Tidal = true };
        for (int i = 0; i < 35; i++)
        {
            double x = Range(1150, 1450), y = Range(600, 900);
            coast.Rocks.Add(new Rock { Position = new(x, y, coast.Ground(x, y) + Range(-10, 20)), Radius = Range(4, 40) });
        }
        for (int trial = 0; trial < 1200; trial++)
        {
            double x = Range(1100, 1500), y = Range(550, 950);
            V root = new(x, y, coast.Ground(x, y) + Range(-15, 70)), joint = root + Offset(60);
            var p = new BudPose(root, joint, joint + Offset(60), Range(.05, 20), Range(.05, 2), 0, true, Range(.1, 2));
            bool hit = coast.BranchSurfaceContact(p, out var contact);
            var expected = ReferenceContact(coast, p);
            Check(contact == expected && hit == (expected.Penetration > 0), "Branch broad phase or frame reuse changed contact");
        }
        // Include bin edges, clamped coordinates, long bodies, and the parallel path.
        for (int i = 0; i < 180; i++)
        {
            var o = new Organism { Id = i + 1, Genes = new Genome { Cells = 1, Radius = Range(2, 12),
                Buds = [new BudGene { Length = i % 9 == 0 ? 30 : .5, Width = .5 }] },
                Body = [new Cell { Position = new(i < 30 ? i * 70 : Range(-40, World.Width + 40), Range(-40, World.Height + 40), Range(-20, World.Depth + 20)) }],
                BudStates = [new BudState { Growth = 1 }] };
            world.Organisms.Add(o);
        }
        var originals = world.Organisms.ToArray(); world.Step(.1);
        int Bin(Organism o) => (Math.Clamp((int)(o.StepCenter.Z / 70), 0, 5) * 24 + Math.Clamp((int)(o.StepCenter.Y / 70), 0, 23)) * 39 + Math.Clamp((int)(o.StepCenter.X / 70), 0, 38);
        foreach (var o in originals)
        {
            var expected = originals.Where(other => other.Id != o.Id && (other.StepCenter - o.StepCenter).LengthSquared
                < Math.Pow(65 + o.PhysicalExtent + other.PhysicalExtent, 2)).OrderBy(Bin);
            Check(o.StepNeighbors.SequenceEqual(expected), "Bounded neighbor query changed membership or order");
        }
        Console.WriteLine("PASS: 8,000 core contacts, 1,200 terrain/rock contacts, and ordered neighbor searches match unpruned references");
    }
}
