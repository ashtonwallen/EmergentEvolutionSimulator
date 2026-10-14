namespace Tideglass;

public static class BodyMechanics
{
    public static double Dot(V a, V b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static V Cross(V a, V b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    public static V Unit(V a) => a * (1 / Math.Max(1e-9, a.Length));
    // Conservative sphere for cheap rejection before the oriented ellipsoid calculation.
    public static double OuterRadius(Organism o, int i) => o.Genes.CellRadius(i)
        * Math.Max(.95, Math.Max(o.Genes.Elongation, o.Genes.Flattening)) + 1e-9;
    public static double Support(Organism o, int i, V direction)
        => new CellShape(o, i).Support(direction);

    public readonly struct CellShape
    {
        private readonly V axis, side, up;
        private readonly double radius, elongation, flattening;
        public CellShape(Organism o, int i)
        {
            axis = V.Direction(o.Heading + o.Body[i].Angle, o.Pitch + o.Body[i].Elevation);
            side = Unit(Cross(new V(0, 0, 1), axis)); up = Unit(Cross(axis, side));
            radius = o.Genes.CellRadius(i); elongation = o.Genes.Elongation; flattening = o.Genes.Flattening;
        }
        public double Support(V direction) => radius * Math.Sqrt(Math.Pow(Dot(direction, axis) * elongation, 2)
            + Math.Pow(Dot(direction, side) * .95, 2) + Math.Pow(Dot(direction, up) * flattening, 2));
    }
    public static double Drag(Organism o) => Math.Max(.2, 1.65 * Math.Pow(TissueEconomy.CoreVolume(o), 2.0 / 3));

    // Overdamped active-surface model. Cortex motors push against water/contact;
    // local tangential forces supply propulsion and turning, with finite power.
    // This leaves simple founders mobile without imposing limbs or a body plan.
    public static V Solve(World w, Organism o, V sensory, double dt)
    {
        var g = o.Genes; V center = o.Center, forward = V.Direction(o.Heading, o.Pitch);
        V side = new(-Math.Sin(o.Heading), Math.Cos(o.Heading)), up = Cross(forward, side);
        V desiredTorque = Cross(forward, sensory) * .7;
        Span<V> normals = [forward, forward * -1, side, side * -1, up, up * -1];
        double reserves = Math.Clamp(o.Energy / Math.Max(1, o.Capacity * .25), 0, 1), drag = Drag(o);
        V force = new(), torque = new(); double power = 0;
        for (int i = 0; i < o.Body.Count; i++)
        {
            var cell = o.Body[i]; double r = g.CellRadius(i);
            var shape = new CellShape(o, i);
            double area = Math.Pow(TissueEconomy.CoreVolume(g, i), 2.0 / 3);
            double pulse = 1 + .4 * Math.Sin(o.Age * g.Rhythm * 2 + o.Phase - i * g.Rule(i).Phase);
            double strength = g.Contract * cell.Integrity * reserves * o.ThermalEfficiency * pulse * area;
            foreach (V normal in normals)
            {
                V lever = normal * shape.Support(normal), at = cell.Position + lever;
                double wet = w.Tidal ? w.Immersion(at, r * .2) : 1;
                double contact = w.Tidal && w.SurfaceContact(at, .5, out _, out _, out _) ? (1 - wet) * (.2 + g.SurfaceAdhesion) : 0;
                V tangent = forward - normal * Dot(forward, normal) + Cross(desiredTorque, normal);
                V traction = tangent * (strength * (wet + contact) * .7);
                force += traction; torque += Cross(cell.Position - center + lever, traction);
                power += traction.LengthSquared * .008;
            }
        }
        double budget = Math.Min(1, Math.Max(0, o.Energy) / Math.Max(1e-9, power * dt));
        force *= budget; torque *= budget; power *= budget;
        o.LocomotionPower = power; o.Energy -= power * dt;
        double rotationalDrag = drag * g.Radius * g.Radius * 2;
        o.Heading += Math.Clamp(torque.Z / rotationalDrag, -.8, .8) * dt;
        o.Pitch = Math.Clamp(o.Pitch - Math.Clamp(Dot(torque, side) / rotationalDrag, -.8, .8) * dt, -1.35, 1.35);
        double immersion = w.Tidal ? w.Immersion(center, g.Radius) : 1;
        return w.FlowAt(center) * immersion + force * (1 / drag);
    }
}
