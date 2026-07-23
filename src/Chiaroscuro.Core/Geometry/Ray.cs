namespace Chiaroscuro.Core.Geometry;

/// <summary>
/// A ray in 3D space: all points of the form Origin + t * Direction for t >= 0.
/// Corresponds to spec §3.2's ray equation P(t) = W_center + t * (-S_v).
/// </summary>
public readonly record struct Ray(Vector3 Origin, Vector3 Direction)
{
    /// <summary>The point reached by walking distance <paramref name="t"/> along the ray's direction.</summary>
    public Vector3 PointAt(double t) => Origin + Direction * t;

    /// <summary>
    /// Solves for the ray parameter t at which this ray crosses <paramref name="plane"/>.
    /// Returns null if the ray is parallel to the plane (never crosses it), or if the
    /// crossing point would lie behind the ray's origin (t is negative), which is not
    /// a physically meaningful hit for a ray of light travelling forward.
    /// </summary>
    public double? IntersectParameter(Plane plane)
    {
        // How much the ray's direction "faces into" the plane. If this is ~0, the ray
        // runs parallel to the plane's surface and can never cross it at a single point.
        var denominator = plane.Normal.Dot(Direction);
        const double parallelThreshold = 1e-9;
        if (Math.Abs(denominator) < parallelThreshold)
        {
            return null;
        }

        // Derived from substituting P(t) = Origin + t*Direction into the plane equation
        // Normal . (P - Point) = 0 and solving for t. See the walkthrough above.
        var t = plane.Normal.Dot(plane.Point - Origin) / denominator;

        // A negative t means the plane is behind where the ray starts - not a valid hit
        // for light travelling forward from the window into the room.
        return t >= 0 ? t : null;
    }
}
