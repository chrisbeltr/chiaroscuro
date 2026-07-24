using Chiaroscuro.Core.Geometry;

namespace Chiaroscuro.UI.Viewport;

/// <summary>A single drawable element of the 3D scene, expressed in room-world space.
/// <see cref="Views.RoomViewport"/> projects these to screen space and paints them - nothing
/// in this file or <see cref="SceneBuilder"/> knows about pixels, Skia, or Avalonia.</summary>
public abstract record ScenePrimitive(SceneColor Color);

/// <summary>A straight line between two world-space points - used for the room wireframe
/// and the window frame.</summary>
public sealed record SceneLine(Vector3 Start, Vector3 End, SceneColor Color) : ScenePrimitive(Color);

/// <summary>A filled, typically-translucent world-space polygon - used for the light-cone's
/// side faces and the illuminated landing patch.</summary>
public sealed record ScenePolygon(Vector3[] Corners, SceneColor Color) : ScenePrimitive(Color);

/// <summary>A plain RGBA color, 0-255 per channel. Deliberately not
/// <c>Avalonia.Media.Color</c> or <c>SkiaSharp.SKColor</c> - this file has no UI-framework
/// dependency, so it stays unit-testable without pulling in Avalonia or Skia.</summary>
public readonly record struct SceneColor(byte R, byte G, byte B, byte A = 255);
