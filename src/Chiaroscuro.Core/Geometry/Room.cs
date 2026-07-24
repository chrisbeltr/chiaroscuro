namespace Chiaroscuro.Core.Geometry;

/// <summary>
/// A rectangular room, origin-aligned per spec §3: O = (0,0,0).
/// <para>
/// Coordinate convention used throughout Chiaroscuro.Core (chosen to match the
/// sun unit vector formula in <see cref="Solar.SolarPosition.ToUnitVector"/>,
/// where azimuth 0°/North gives a +Y vector and azimuth 90°/East gives +X):
/// </para>
/// <list type="bullet">
/// <item>+X = East, -X = West</item>
/// <item>+Y = North, -Y = South</item>
/// <item>+Z = Up</item>
/// <item>Origin (0,0,0) is the CENTER OF THE FLOOR, not the room's volumetric
/// center - so the floor plane is Z=0 and the ceiling plane is Z=Height.
/// This is a deliberate design choice, not something the spec pins down.</item>
/// </list>
/// </summary>
public readonly record struct Room(double Width, double Length, double Height)
{
    /// <summary>The infinite plane containing the given surface (not clipped to the room's bounds).</summary>
    public Plane GetPlane(RoomSurface surface) => surface switch
    {
        // Normals point INTO the room interior by convention. This doesn't affect
        // the ray-plane intersection math (Ray.IntersectParameter works either way),
        // it's just a consistent convention should the normal direction matter later
        // (e.g. for shading/rendering in a later phase).
        RoomSurface.Floor => new Plane(Vector3.Zero, new Vector3(0, 0, 1)),
        RoomSurface.NorthWall => new Plane(new Vector3(0, Length / 2, 0), new Vector3(0, -1, 0)),
        RoomSurface.SouthWall => new Plane(new Vector3(0, -Length / 2, 0), new Vector3(0, 1, 0)),
        RoomSurface.EastWall => new Plane(new Vector3(Width / 2, 0, 0), new Vector3(-1, 0, 0)),
        RoomSurface.WestWall => new Plane(new Vector3(-Width / 2, 0, 0), new Vector3(1, 0, 0)),
        _ => throw new ArgumentOutOfRangeException(nameof(surface)),
    };

    /// <summary>
    /// True if <paramref name="point"/> falls within the actual rectangular extent of
    /// <paramref name="surface"/> - NOT just on its infinite plane. A ray can mathematically
    /// cross a wall's plane far outside where the physical wall actually is, so every
    /// candidate hit must pass this check before it counts as a real intersection.
    /// </summary>
    public bool IsWithinSurfaceBounds(RoomSurface surface, Vector3 point) => surface switch
    {
        RoomSurface.Floor => IsWithinExtent(point.X, Width) && IsWithinExtent(point.Y, Length),
        // The `or` pattern combinator: this arm matches either North or South wall, since
        // both share the same X/Z bounds (only their fixed Y coordinate differs).
        RoomSurface.NorthWall or RoomSurface.SouthWall => IsWithinExtent(point.X, Width) && IsWithinHeight(point.Z),
        RoomSurface.EastWall or RoomSurface.WestWall => IsWithinExtent(point.Y, Length) && IsWithinHeight(point.Z),
        _ => throw new ArgumentOutOfRangeException(nameof(surface)),
    };

    /// <summary>
    /// The surfaces a light ray entering through the wall <paramref name="windowWall"/>
    /// could plausibly land on: the floor and the three walls other than the window's own
    /// wall (per spec §3.2, "floor or interior wall planes").
    /// </summary>
    public IEnumerable<RoomSurface> GetCandidateSurfaces(WallOrientation windowWall)
    {
        var windowSurface = ToRoomSurface(windowWall);
        return Enum.GetValues<RoomSurface>().Where(surface => surface != windowSurface);
    }

    private bool IsWithinHeight(double z) => z >= 0 && z <= Height;

    private static bool IsWithinExtent(double coordinate, double extent) => Math.Abs(coordinate) <= extent / 2.0;

    /// <summary>Maps a window's wall to the corresponding <see cref="RoomSurface"/> - e.g. for
    /// excluding a window's own wall from candidate landing surfaces. Internal (not private)
    /// so <see cref="IlluminationPatchClipper"/> can reuse it too.</summary>
    internal static RoomSurface ToRoomSurface(WallOrientation wall) => wall switch
    {
        WallOrientation.North => RoomSurface.NorthWall,
        WallOrientation.South => RoomSurface.SouthWall,
        WallOrientation.East => RoomSurface.EastWall,
        WallOrientation.West => RoomSurface.WestWall,
        _ => throw new ArgumentOutOfRangeException(nameof(wall)),
    };
}
