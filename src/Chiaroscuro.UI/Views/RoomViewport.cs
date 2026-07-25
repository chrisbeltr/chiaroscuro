using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Chiaroscuro.Core.Geometry;
using Chiaroscuro.UI.Viewport;
using SkiaSharp;
using Window = Chiaroscuro.Core.Geometry.Window; // disambiguate from Avalonia.Controls.Window

namespace Chiaroscuro.UI.Views;

/// <summary>Renders the room/window/light-cone 3D scene via a hand-rolled Skia draw
/// operation, and drives the camera from mouse drag (orbit) and wheel (zoom). See
/// docs/superpowers/specs/2026-07-23-3d-viewport-design.md for the overall design.</summary>
public sealed class RoomViewport : Control
{
    public static readonly StyledProperty<Room> RoomProperty =
        AvaloniaProperty.Register<RoomViewport, Room>(nameof(Room), new Room(6, 5, 3));

    public static readonly StyledProperty<Window> WindowProperty =
        AvaloniaProperty.Register<RoomViewport, Window>(nameof(Window));

    public static readonly StyledProperty<IlluminationResult?> IlluminationProperty =
        AvaloniaProperty.Register<RoomViewport, IlluminationResult?>(nameof(Illumination));

    public static readonly StyledProperty<Vector3?> TargetPointProperty =
        AvaloniaProperty.Register<RoomViewport, Vector3?>(nameof(TargetPoint));

    public static readonly StyledProperty<decimal?> ToleranceDegreesProperty =
        AvaloniaProperty.Register<RoomViewport, decimal?>(nameof(ToleranceDegrees));

    public Room Room
    {
        get => GetValue(RoomProperty);
        set => SetValue(RoomProperty, value);
    }

    public Window Window
    {
        get => GetValue(WindowProperty);
        set => SetValue(WindowProperty, value);
    }

    public IlluminationResult? Illumination
    {
        get => GetValue(IlluminationProperty);
        set => SetValue(IlluminationProperty, value);
    }

    public Vector3? TargetPoint
    {
        get => GetValue(TargetPointProperty);
        set => SetValue(TargetPointProperty, value);
    }

    public decimal? ToleranceDegrees
    {
        get => GetValue(ToleranceDegreesProperty);
        set => SetValue(ToleranceDegreesProperty, value);
    }

    private readonly OrbitCamera _camera;
    private Point? _lastPointerPosition;

    public RoomViewport()
    {
        _camera = new OrbitCamera(GetRoomCenter(Room));
        ClipToBounds = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == RoomProperty)
        {
            _camera.Target = GetRoomCenter(Room);
        }

        if (change.Property == RoomProperty || change.Property == WindowProperty || change.Property == IlluminationProperty
            || change.Property == TargetPointProperty || change.Property == ToleranceDegreesProperty)
        {
            InvalidateVisual();
        }
    }

    // Room's origin is the center of the FLOOR (see Room.cs), so the room's volumetric
    // center - what the camera should orbit - is straight up from there by half the height.
    private static Vector3 GetRoomCenter(Room room) => new(0, 0, room.Height / 2);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _lastPointerPosition = e.GetPosition(this);
            e.Pointer.Capture(this);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_lastPointerPosition is not { } last)
        {
            return;
        }

        var current = e.GetPosition(this);
        var delta = current - last;
        _lastPointerPosition = current;

        // Half a degree of orbit per pixel dragged feels reasonable at typical window sizes;
        // flip the signs below if orbiting feels backwards once you try it.
        const double radiansPerPixel = Math.PI / 360.0;
        _camera.Orbit(-delta.X * radiansPerPixel, delta.Y * radiansPerPixel);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _lastPointerPosition = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _camera.Zoom(-e.Delta.Y);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // Snapshot the camera on the UI thread: Render() on the draw operation below runs on
        // Avalonia's render thread, while pointer/wheel handlers mutate _camera on this (UI)
        // thread. Passing a frozen copy instead of the live instance avoids reading a
        // torn/half-updated camera state (e.g. new yaw with old pitch) from another thread.
        var cameraSnapshot = new OrbitCamera(_camera.Target, _camera.Yaw, _camera.Pitch, _camera.Distance);
        context.Custom(new ViewportDrawOperation(
            new Rect(Bounds.Size), cameraSnapshot, Room, Window, Illumination, TargetPoint, (double?)ToleranceDegrees));
    }

    private sealed class ViewportDrawOperation(
        Rect bounds, OrbitCamera camera, Room room, Window window, IlluminationResult? illumination,
        Vector3? target, double? toleranceDegrees)
        : ICustomDrawOperation
    {
        public Rect Bounds { get; } = bounds;

        public bool HitTest(Point p) => Bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose()
        {
        }

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null)
            {
                return;
            }

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            var primitives = SceneBuilder.Build(room, window, illumination, target, toleranceDegrees);
            var projected = ProjectAndSort(primitives, camera, Bounds.Width, Bounds.Height);

            using var paint = new SKPaint { IsAntialias = true };

            foreach (var (primitive, points) in projected)
            {
                switch (primitive)
                {
                    case SceneLine line:
                        paint.Style = SKPaintStyle.Stroke;
                        paint.StrokeWidth = 1.5f;
                        paint.Color = ToSkColor(line.Color);
                        canvas.DrawLine((float)points[0].X, (float)points[0].Y, (float)points[1].X, (float)points[1].Y, paint);
                        break;

                    case ScenePolygon:
                        paint.Style = SKPaintStyle.Fill;
                        paint.Color = ToSkColor(primitive.Color);
                        using (var path = new SKPath())
                        {
                            path.MoveTo((float)points[0].X, (float)points[0].Y);
                            for (var i = 1; i < points.Length; i++)
                            {
                                path.LineTo((float)points[i].X, (float)points[i].Y);
                            }
                            path.Close();
                            canvas.DrawPath(path, paint);
                        }
                        break;
                }
            }
        }

        /// <summary>Projects every primitive's world-space points to screen space, drops any
        /// primitive with a point behind the camera (unprojectable), then orders lines before
        /// polygons and polygons back-to-front by average depth (painter's algorithm - Skia
        /// has no z-buffer here) so the light cone/patch always paints over the wireframe.</summary>
        private static List<(ScenePrimitive Primitive, Point[] Points)> ProjectAndSort(
            IReadOnlyList<ScenePrimitive> primitives, OrbitCamera camera, double width, double height)
        {
            var result = new List<(ScenePrimitive Primitive, Point[] Points, double Depth)>();

            foreach (var primitive in primitives)
            {
                var worldPoints = primitive switch
                {
                    SceneLine line => new[] { line.Start, line.End },
                    ScenePolygon polygon => polygon.Corners,
                    _ => [],
                };

                var screenPoints = new Point[worldPoints.Length];
                var totalDepth = 0.0;
                var projectable = true;

                for (var i = 0; i < worldPoints.Length; i++)
                {
                    if (ViewportProjector.Project(worldPoints[i], camera, width, height) is not { } projected)
                    {
                        projectable = false;
                        break;
                    }

                    screenPoints[i] = new Point(projected.ScreenX, projected.ScreenY);
                    totalDepth += projected.Depth;
                }

                if (projectable)
                {
                    result.Add((primitive, screenPoints, totalDepth / worldPoints.Length));
                }
            }

            return result
                .OrderBy(entry => entry.Primitive is SceneLine ? 0 : 1)
                .ThenByDescending(entry => entry.Depth)
                .Select(entry => (entry.Primitive, entry.Points))
                .ToList();
        }

        private static SKColor ToSkColor(SceneColor color) => new(color.R, color.G, color.B, color.A);
    }
}
