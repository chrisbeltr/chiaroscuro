using System.Numerics;
using Chiaroscuro.Core.Geometry;
using Vector3 = Chiaroscuro.Core.Geometry.Vector3;

namespace Chiaroscuro.UI.Viewport;

/// <summary>A projected world-space point: where it lands on screen, plus its distance
/// from the camera (used by <see cref="Views.RoomViewport"/> for back-to-front draw
/// ordering, since Skia has no z-buffer).</summary>
public readonly record struct ProjectedPoint(double ScreenX, double ScreenY, double Depth);

/// <summary>Pure world-space-to-screen-space projection - no Skia/Avalonia dependency, so
/// it's unit-testable the same way Chiaroscuro.Core's math is.</summary>
public static class ViewportProjector
{
    public static ProjectedPoint? Project(Vector3 worldPoint, OrbitCamera camera, double viewportWidth, double viewportHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return null;
        }

        var viewProjection = camera.GetViewMatrix() * camera.GetProjectionMatrix(viewportWidth / viewportHeight);
        var clip = Vector4.Transform(new Vector4(OrbitCamera.ToNumerics(worldPoint), 1f), viewProjection);

        if (clip.W <= 0.0001f)
        {
            return null; // behind the camera - can't be meaningfully placed on screen
        }

        var ndcX = clip.X / clip.W;
        var ndcY = clip.Y / clip.W;

        var screenX = (ndcX + 1.0) / 2.0 * viewportWidth;
        var screenY = (1.0 - ndcY) / 2.0 * viewportHeight; // NDC's +Y is up; screen's +Y is down

        var depth = System.Numerics.Vector3.Distance(OrbitCamera.ToNumerics(camera.GetEyePosition()), OrbitCamera.ToNumerics(worldPoint));

        return new ProjectedPoint(screenX, screenY, depth);
    }
}
