namespace Chiaroscuro.Core.Geometry;

/// <summary>
/// An infinite flat plane in 3D space, described by any point that lies on it
/// and a normal vector (a vector perpendicular to the plane's surface).
/// </summary>
/// <param name="Point">Any single point known to lie on the plane.</param>
/// <param name="Normal">
/// A vector perpendicular to the plane. Does NOT need to be pre-normalized to
/// length 1 — the intersection math below divides through by the normal's own
/// dot products, so any non-zero scaling of it produces the same result.
/// </param>
public readonly record struct Plane(Vector3 Point, Vector3 Normal);
