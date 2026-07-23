namespace Chiaroscuro.Core.Geometry;

public readonly record struct Vector3(double X, double Y, double Z)
{
    public static readonly Vector3 Zero = new(0, 0, 0);

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    public Vector3 Normalized()
    {
        var length = Length;
        return length == 0 ? Zero : new Vector3(X / length, Y / length, Z / length);
    }

    public double Dot(Vector3 other) => X * other.X + Y * other.Y + Z * other.Z;

    public double AngleTo(Vector3 other)
    {
        var cosAngle = Normalized().Dot(other.Normalized());
        var clamped = Math.Clamp(cosAngle, -1.0, 1.0);
        return Math.Acos(clamped);
    }

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator -(Vector3 v) => new(-v.X, -v.Y, -v.Z);
    public static Vector3 operator *(Vector3 v, double scalar) => new(v.X * scalar, v.Y * scalar, v.Z * scalar);
    public static Vector3 operator *(double scalar, Vector3 v) => v * scalar;
}
