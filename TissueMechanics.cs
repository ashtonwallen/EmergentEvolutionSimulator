namespace Tideglass;

// Symmetric drag tensor: no preferred travel direction or power-stroke multiplier.
public readonly record struct Resistance(double XX, double YY, double ZZ, double XY = 0, double XZ = 0, double YZ = 0)
{
    public static Resistance Isotropic(double drag) => new(drag, drag, drag);
    public static Resistance Along(V direction, double drag)
        => new(direction.X * direction.X * drag, direction.Y * direction.Y * drag, direction.Z * direction.Z * drag,
            direction.X * direction.Y * drag, direction.X * direction.Z * drag, direction.Y * direction.Z * drag);
    public static Resistance operator +(Resistance a, Resistance b)
        => new(a.XX + b.XX, a.YY + b.YY, a.ZZ + b.ZZ, a.XY + b.XY, a.XZ + b.XZ, a.YZ + b.YZ);
    public static Resistance operator *(Resistance a, double scale)
        => new(a.XX * scale, a.YY * scale, a.ZZ * scale, a.XY * scale, a.XZ * scale, a.YZ * scale);
    public V Apply(V velocity) => new(XX * velocity.X + XY * velocity.Y + XZ * velocity.Z,
        XY * velocity.X + YY * velocity.Y + YZ * velocity.Z, XZ * velocity.X + YZ * velocity.Y + ZZ * velocity.Z);
    public V Solve(V force)
    {
        double a = YY * ZZ - YZ * YZ, b = XZ * YZ - XY * ZZ, c = XY * YZ - XZ * YY;
        double determinant = XX * a + XY * b + XZ * c;
        if (!double.IsFinite(determinant) || determinant <= 1e-12) return new();
        return new((a * force.X + b * force.Y + c * force.Z) / determinant,
            (b * force.X + (XX * ZZ - XZ * XZ) * force.Y + (XY * XZ - XX * YZ) * force.Z) / determinant,
            (c * force.X + (XY * XZ - XX * YZ) * force.Y + (XX * YY - XY * XY) * force.Z) / determinant);
    }
}

public static class TissueMechanics
{
    private static V Unit(V p) => p * (1 / Math.Max(.000001, p.Length));
    private static V Cross(V a, V b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    public static Resistance WaterDrag(BudPose p, double area, double immersion)
    {
        V axis = Unit(p.Tip - p.Root);
        V side = Unit(Cross(Math.Abs(axis.Z) > .9 ? new V(0, 1, 0) : new V(0, 0, 1), axis));
        V broadside = Unit(Cross(axis, side));
        double flatness = Math.Max(.12, p.Flatness);
        return (Resistance.Along(axis, .07) + Resistance.Along(side, .18 * flatness)
            + Resistance.Along(broadside, .30 / Math.Sqrt(flatness))) * (area * immersion);
    }
    public static Resistance GroundDrag(V normal, double grip)
        => (Resistance.Isotropic(1) + Resistance.Along(normal, -1)) * grip;
}
