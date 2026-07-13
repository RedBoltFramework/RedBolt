namespace RedBolt;

/// <summary>
/// Debug geometry rendering component. Should be added only to the root scene node.
/// </summary>
public sealed class DebugRenderer : Component
{
    private const int MaxLines = 1_000_000;
    private const int MaxTriangles = 1_000_000;

    private readonly List<DebugLine> _lines = [];
    private readonly List<DebugLine> _noDepthLines = [];
    private readonly List<DebugTriangle> _triangles = [];
    private readonly List<DebugTriangle> _noDepthTriangles = [];
    private Matrix3x4 _view;
    private Matrix4 _projection;
    private Matrix4 _gpuProjection;
    private Frustum _frustum;
    private Camera? _camera;
    private bool _lineAntiAlias;

    public DebugRenderer()
    {
        SubscribeToEvent<CoreEvents.EndFrameEvent>((ref CoreEvents.EndFrameEvent _) => HandleEndFrame());
    }

    /// <summary>
    /// Set line antialiasing on/off. Default false.
    /// </summary>
    public void SetLineAntiAlias(bool enable)
    {
        if (enable != _lineAntiAlias)
            _lineAntiAlias = enable;
    }

    /// <summary>
    /// Set the camera viewpoint. Call before rendering, or before adding geometry if you want to use culling.
    /// </summary>
    public void SetView(Camera? camera)
    {
        if (camera is null)
            return;

        _view = camera.GetView();
        _projection = camera.GetProjection();
        _gpuProjection = camera.GetGPUProjection();
        _frustum = camera.GetFrustum();
        _camera = camera;
    }

    /// <summary>
    /// Add a line.
    /// </summary>
    public void AddLine(Vector3f start, Vector3f end, Color color, bool depthTest = true)
    {
        AddLine(start, end, color.ToUInt(), depthTest);
    }

    /// <summary>
    /// Add a line with color already converted to unsigned.
    /// </summary>
    public void AddLine(Vector3f start, Vector3f end, uint color, bool depthTest = true)
    {
        if (_lines.Count + _noDepthLines.Count >= MaxLines)
            return;

        if (depthTest)
            _lines.Add(new DebugLine(start, end, color));
        else
            _noDepthLines.Add(new DebugLine(start, end, color));
    }

    /// <summary>
    /// Add a solid triangle.
    /// </summary>
    public void AddTriangle(Vector3f vertexA, Vector3f vertexB, Vector3f vertexC, Color color, bool depthTest = true)
    {
        AddTriangle(vertexA, vertexB, vertexC, color.ToUInt(), depthTest);
    }

    /// <summary>
    /// Add a solid triangle with color already converted to unsigned.
    /// </summary>
    public void AddTriangle(Vector3f vertexA, Vector3f vertexB, Vector3f vertexC, uint color, bool depthTest = true)
    {
        if (_triangles.Count + _noDepthTriangles.Count >= MaxTriangles)
            return;

        if (depthTest)
            _triangles.Add(new DebugTriangle(vertexA, vertexB, vertexC, color));
        else
            _noDepthTriangles.Add(new DebugTriangle(vertexA, vertexB, vertexC, color));
    }

    /// <summary>
    /// Add a solid quadrangular polygon.
    /// </summary>
    public void AddPolygon(Vector3f vertexA, Vector3f vertexB, Vector3f vertexC, Vector3f vertexD, Color color, bool depthTest = true)
    {
        AddTriangle(vertexA, vertexB, vertexC, color, depthTest);
        AddTriangle(vertexC, vertexD, vertexA, color, depthTest);
    }

    /// <summary>
    /// Add a solid quadrangular polygon with color already converted to unsigned.
    /// </summary>
    public void AddPolygon(Vector3f vertexA, Vector3f vertexB, Vector3f vertexC, Vector3f vertexD, uint color, bool depthTest = true)
    {
        AddTriangle(vertexA, vertexB, vertexC, color, depthTest);
        AddTriangle(vertexC, vertexD, vertexA, color, depthTest);
    }

    /// <summary>
    /// Return whether line antialiasing is enabled.
    /// </summary>
    public bool GetLineAntiAlias() => _lineAntiAlias;

    /// <summary>
    /// Return the view transform.
    /// </summary>
    public Matrix3x4 GetView() => _view;

    /// <summary>
    /// Return the projection transform.
    /// </summary>
    public Matrix4 GetProjection() => _projection;

    /// <summary>
    /// Return the view frustum.
    /// </summary>
    public Frustum GetFrustum() => _frustum;

    /// <summary>
    /// Check whether a bounding box is inside the view frustum.
    /// </summary>
    public bool IsInside(BoundingBox box) => _frustum.IsInsideFast(box) == Intersection.Inside;

    /// <summary>
    /// Return whether has something to render.
    /// </summary>
    public bool HasContent() => _lines.Count != 0 || _noDepthLines.Count != 0 || _triangles.Count != 0 || _noDepthTriangles.Count != 0;

    /// <summary>
    /// Clear debug geometry at the end of the frame.
    /// </summary>
    public void HandleEndFrame()
    {
        var linesSize = _lines.Count;
        var noDepthLinesSize = _noDepthLines.Count;
        var trianglesSize = _triangles.Count;
        var noDepthTrianglesSize = _noDepthTriangles.Count;

        _lines.Clear();
        _noDepthLines.Clear();
        _triangles.Clear();
        _noDepthTriangles.Clear();

        if (_lines.Capacity > linesSize * 2)
            _lines.Capacity = linesSize;
        if (_noDepthLines.Capacity > noDepthLinesSize * 2)
            _noDepthLines.Capacity = noDepthLinesSize;
        if (_triangles.Capacity > trianglesSize * 2)
            _triangles.Capacity = trianglesSize;
        if (_noDepthTriangles.Capacity > noDepthTrianglesSize * 2)
            _noDepthTriangles.Capacity = noDepthTrianglesSize;
    }
}
