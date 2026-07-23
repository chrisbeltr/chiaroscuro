namespace Chiaroscuro.Core.Geometry;

/// <summary>
/// A rectangular window aperture set into one of a <see cref="Room"/>'s walls.
/// </summary>
/// <param name="Wall">Which wall the window is set into.</param>
/// <param name="HorizontalOffset">
/// Distance from the wall's own center, measured along the wall's horizontal axis
/// (East-West for North/South walls, North-South for East/West walls). 0 = centered.
/// </param>
/// <param name="SillHeight">Height of the window's bottom edge above the floor.</param>
/// <param name="Width">Width of the window opening, along the wall's horizontal axis.</param>
/// <param name="Height">Height of the window opening, vertically.</param>
public readonly record struct Window(WallOrientation Wall, double HorizontalOffset, double SillHeight, double Width, double Height)
{
    /// <summary>The window's center point in room-world space - this is W_center in spec §3.2's ray equation.</summary>
    public Vector3 GetCenter(Room room) => Wall switch
    {
        WallOrientation.North => new Vector3(HorizontalOffset, room.Length / 2, SillHeight + Height / 2),
        WallOrientation.South => new Vector3(HorizontalOffset, -room.Length / 2, SillHeight + Height / 2),
        WallOrientation.East => new Vector3(room.Width / 2, HorizontalOffset, SillHeight + Height / 2),
        WallOrientation.West => new Vector3(-room.Width / 2, HorizontalOffset, SillHeight + Height / 2),
        _ => throw new ArgumentOutOfRangeException(nameof(Wall)),
    };

    /// <summary>
    /// The window's four corners in room-world space, ordered bottom-left, bottom-right,
    /// top-right, top-left (as viewed from inside the room, facing the wall). This is the
    /// 2D polygon spec §3.2 refers to as the "window frame" to be projected into the room.
    /// </summary>
    public Vector3[] GetCorners(Room room)
    {
        var center = GetCenter(room);
        var halfWidth = Width / 2;
        var halfHeight = Height / 2;

        // A wall's "horizontal" in-plane axis depends on which wall it is: North/South
        // walls run along X (East-West), East/West walls run along Y (North-South).
        var horizontalAxis = Wall is WallOrientation.North or WallOrientation.South
            ? new Vector3(1, 0, 0)
            : new Vector3(0, 1, 0);
        var verticalAxis = new Vector3(0, 0, 1); // every wall's vertical (sill-to-head) axis is simply Up

        return
        [
            center - horizontalAxis * halfWidth - verticalAxis * halfHeight, // bottom-left
            center + horizontalAxis * halfWidth - verticalAxis * halfHeight, // bottom-right
            center + horizontalAxis * halfWidth + verticalAxis * halfHeight, // top-right
            center - horizontalAxis * halfWidth + verticalAxis * halfHeight, // top-left
        ];
    }
}
