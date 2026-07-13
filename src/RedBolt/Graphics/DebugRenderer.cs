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
    private Frustum _frustum = new();
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
    /// Add a scene node represented as its coordinate axes.
    /// </summary>
    public void AddNode(Node? node, float scale = 1.0f, bool depthTest = true)
    {
        if (node is null)
            return;

        var start = node.GetWorldPosition();
        var rotation = node.GetWorldRotation();

        AddLine(start, start + rotation * (scale * Vector3f.Right), new Color(1.0f, 0.0f, 0.0f).ToUInt(), depthTest);
        AddLine(start, start + rotation * (scale * Vector3f.Up), new Color(0.0f, 1.0f, 0.0f).ToUInt(), depthTest);
        AddLine(start, start + rotation * (scale * Vector3f.Forward), new Color(0.0f, 0.0f, 1.0f).ToUInt(), depthTest);
    }

    /// <summary>
    /// Add an XYZ frame at the given worldTransform with optional uniform scale.
    /// </summary>
    public void AddFrame(Matrix3x4 worldTransform, float scale = 1.0f,
        Color? colorX = null, Color? colorY = null, Color? colorZ = null, bool depthTest = true)
    {
        var origin = worldTransform * Vector3f.Zero;
        var x = worldTransform * (Vector3f.Right * scale);
        var y = worldTransform * (Vector3f.Up * scale);
        var z = worldTransform * (Vector3f.Forward * scale);

        AddLine(origin, x, colorX ?? new Color(1.0f, 0.0f, 0.0f), depthTest);
        AddLine(origin, y, colorY ?? new Color(0.0f, 1.0f, 0.0f), depthTest);
        AddLine(origin, z, colorZ ?? new Color(0.0f, 0.0f, 1.0f), depthTest);
    }

    /// <summary>
    /// Add a bounding box.
    /// </summary>
    public void AddBoundingBox(BoundingBox box, Color color, bool depthTest = true, bool solid = false)
    {
        AddBoundingBox(box, Matrix3x4.Identity, color, depthTest, solid);
    }

    /// <summary>
    /// Add a bounding box with transform.
    /// </summary>
    public void AddBoundingBox(BoundingBox box, Matrix3x4 transform, Color color, bool depthTest = true, bool solid = false)
    {
        var min = box.Min;
        var max = box.Max;
        var vertex0 = transform * min;
        var vertex1 = transform * new Vector3f(max.X, min.Y, min.Z);
        var vertex2 = transform * new Vector3f(max.X, max.Y, min.Z);
        var vertex3 = transform * new Vector3f(min.X, max.Y, min.Z);
        var vertex4 = transform * new Vector3f(min.X, min.Y, max.Z);
        var vertex5 = transform * new Vector3f(max.X, min.Y, max.Z);
        var vertex6 = transform * new Vector3f(min.X, max.Y, max.Z);
        var vertex7 = transform * max;
        var uintColor = color.ToUInt();

        if (!solid)
        {
            AddLine(vertex0, vertex1, uintColor, depthTest);
            AddLine(vertex1, vertex2, uintColor, depthTest);
            AddLine(vertex2, vertex3, uintColor, depthTest);
            AddLine(vertex3, vertex0, uintColor, depthTest);
            AddLine(vertex4, vertex5, uintColor, depthTest);
            AddLine(vertex5, vertex7, uintColor, depthTest);
            AddLine(vertex7, vertex6, uintColor, depthTest);
            AddLine(vertex6, vertex4, uintColor, depthTest);
            AddLine(vertex0, vertex4, uintColor, depthTest);
            AddLine(vertex1, vertex5, uintColor, depthTest);
            AddLine(vertex2, vertex7, uintColor, depthTest);
            AddLine(vertex3, vertex6, uintColor, depthTest);
        }
        else
        {
            AddPolygon(vertex0, vertex1, vertex2, vertex3, uintColor, depthTest);
            AddPolygon(vertex4, vertex5, vertex7, vertex6, uintColor, depthTest);
            AddPolygon(vertex0, vertex4, vertex6, vertex3, uintColor, depthTest);
            AddPolygon(vertex1, vertex5, vertex7, vertex2, uintColor, depthTest);
            AddPolygon(vertex3, vertex2, vertex7, vertex6, uintColor, depthTest);
            AddPolygon(vertex0, vertex1, vertex5, vertex4, uintColor, depthTest);
        }
    }

    /// <summary>
    /// Add a frustum.
    /// </summary>
    public void AddFrustum(Frustum frustum, Color color, bool depthTest = true)
    {
        var vertices = frustum.Vertices;
        var uintColor = color.ToUInt();

        AddLine(vertices[0], vertices[1], uintColor, depthTest);
        AddLine(vertices[1], vertices[2], uintColor, depthTest);
        AddLine(vertices[2], vertices[3], uintColor, depthTest);
        AddLine(vertices[3], vertices[0], uintColor, depthTest);
        AddLine(vertices[4], vertices[5], uintColor, depthTest);
        AddLine(vertices[5], vertices[6], uintColor, depthTest);
        AddLine(vertices[6], vertices[7], uintColor, depthTest);
        AddLine(vertices[7], vertices[4], uintColor, depthTest);
        AddLine(vertices[0], vertices[4], uintColor, depthTest);
        AddLine(vertices[1], vertices[5], uintColor, depthTest);
        AddLine(vertices[2], vertices[6], uintColor, depthTest);
        AddLine(vertices[3], vertices[7], uintColor, depthTest);
    }

    /// <summary>
    /// Add a polyhedron.
    /// </summary>
    public void AddPolyhedron(Polyhedron poly, Color color, bool depthTest = true)
    {
        var uintColor = color.ToUInt();

        foreach (var face in poly.Faces)
        {
            if (face.Count >= 3)
            {
                for (var i = 0; i < face.Count; ++i)
                    AddLine(face[i], face[(i + 1) % face.Count], uintColor, depthTest);
            }
        }
    }

    /// <summary>
    /// Add a sphere.
    /// </summary>
    public void AddSphere(Sphere sphere, Color color, bool depthTest = true)
    {
        var uintColor = color.ToUInt();

        for (var j = 0; j < 180; j += 45)
        {
            for (var i = 0; i < 360; i += 45)
            {
                var p1 = sphere.GetPoint(i, j);
                var p2 = sphere.GetPoint(i + 45, j);
                var p3 = sphere.GetPoint(i, j + 45);
                var p4 = sphere.GetPoint(i + 45, j + 45);

                AddLine(p1, p2, uintColor, depthTest);
                AddLine(p3, p4, uintColor, depthTest);
                AddLine(p1, p3, uintColor, depthTest);
                AddLine(p2, p4, uintColor, depthTest);
            }
        }
    }

    /// <summary>
    /// Add a sphere sector.
    /// </summary>
    public void AddSphereSector(Sphere sphere, Quaternion rotation, float angle,
        bool drawLines, Color color, bool depthTest = true)
    {
        if (angle <= 0.0f)
            return;
        else if (angle >= 360.0f)
        {
            AddSphere(sphere, color, depthTest);
            return;
        }

        const uint numCircleSegments = 8;
        const uint numLines = 4;
        const float arcStep = 45.0f;

        var uintColor = color.ToUInt();
        var halfAngle = 0.5f * angle;
        var numArcSegments = (uint)MathF.Ceiling(halfAngle / arcStep) + 1;

        for (uint j = 0; j < numCircleSegments; ++j)
        {
            AddLine(
                sphere.Center + rotation * sphere.GetLocalPoint(j * 360.0f / numCircleSegments, halfAngle),
                sphere.Center + rotation * sphere.GetLocalPoint((j + 1) * 360.0f / numCircleSegments, halfAngle),
                uintColor, depthTest);
        }

        var step = numCircleSegments / numLines;
        for (uint i = 0; i < numArcSegments - 1; ++i)
        {
            for (uint j = 0; j < numCircleSegments; j += step)
            {
                var nextPhi = i + 1 == numArcSegments - 1 ? halfAngle : (i + 1) * arcStep;
                AddLine(
                    sphere.Center + rotation * sphere.GetLocalPoint(j * 360.0f / numCircleSegments, i * arcStep),
                    sphere.Center + rotation * sphere.GetLocalPoint(j * 360.0f / numCircleSegments, nextPhi),
                    uintColor, depthTest);
            }
        }

        if (drawLines)
        {
            for (uint j = 0; j < numCircleSegments; j += step)
            {
                AddLine(sphere.Center,
                    sphere.Center + rotation * sphere.GetLocalPoint(j * 360.0f / numCircleSegments, halfAngle),
                    uintColor, depthTest);
            }
        }
    }

    /// <summary>
    /// Add a cylinder.
    /// </summary>
    public void AddCylinder(Vector3f position, float radius, float height, Color color, bool depthTest = true)
    {
        var sphere = new Sphere(position, radius);
        var heightVector = new Vector3f(0.0f, height, 0.0f);
        var offsetX = new Vector3f(radius, 0.0f, 0.0f);
        var offsetZ = new Vector3f(0.0f, 0.0f, radius);
        for (var i = 0; i < 360; i += 45)
        {
            var p1 = sphere.GetPoint(i, 90);
            var p2 = sphere.GetPoint(i + 45, 90);
            AddLine(p1, p2, color, depthTest);
            AddLine(p1 + heightVector, p2 + heightVector, color, depthTest);
        }
        AddLine(position + offsetX, position + heightVector + offsetX, color, depthTest);
        AddLine(position - offsetX, position + heightVector - offsetX, color, depthTest);
        AddLine(position + offsetZ, position + heightVector + offsetZ, color, depthTest);
        AddLine(position - offsetZ, position + heightVector - offsetZ, color, depthTest);
    }

    /// <summary>
    /// Add a skeleton.
    /// </summary>
    public void AddSkeleton(Skeleton skeleton, Color color, bool depthTest = true)
    {
        var bones = skeleton.GetBones();
        if (bones.Count == 0)
            return;

        var uintColor = color.ToUInt();
        for (var i = 0; i < bones.Count; ++i)
        {
            // Skip if bone contains no skinned geometry
            if (bones[i].Radius < MathDefs.Epsilon && bones[i].BoundingBox.Size.LengthSquared < MathDefs.Epsilon)
                continue;

            var boneNode = bones[i].Node;
            if (boneNode is null)
                continue;

            var start = boneNode.GetWorldPosition();
            Vector3f end;

            var parentIndex = bones[i].ParentIndex;
            var parentNode = boneNode.GetParent();
            // If bone has a parent defined, and it also skins geometry, draw a line to it. Else draw the bone as a point
            if (parentNode is not null && (bones[(int)parentIndex].Radius >= MathDefs.Epsilon ||
                bones[(int)parentIndex].BoundingBox.Size.LengthSquared >= MathDefs.Epsilon))
                end = parentNode.GetWorldPosition();
            else
                end = start;

            AddLine(start, end, uintColor, depthTest);
        }
    }

    /// <summary>
    /// Add a circle.
    /// </summary>
    public void AddCircle(Vector3f center, Vector3f normal, float radius, Color color, int steps = 64, bool depthTest = true, bool drawNormal = true)
    {
        var orientation = Quaternion.FromRotationTo(Vector3f.Up, normal.Normalized);
        var point = orientation * new Vector3f(radius, 0.0f, 0.0f) + center;
        var uintColor = color.ToUInt();

        for (var i = 1; i <= steps; ++i)
        {
            var angle = (float)i / steps * 360.0f;
            var vertex = new Vector3f(radius * MathDefs.Cos(angle), 0.0f, radius * MathDefs.Sin(angle));
            var current = orientation * vertex + center;
            AddLine(point, current, uintColor, depthTest);
            point = current;
        }

        if (drawNormal)
        {
            point = center + normal * (radius / 4.0f);
            AddLine(center, point, uintColor, depthTest);
        }
    }

    /// <summary>
    /// Add a cross.
    /// </summary>
    public void AddCross(Vector3f center, float size, Color color, bool depthTest = true)
    {
        var uintColor = color.ToUInt();
        var halfSize = size / 2.0f;
        for (var i = 0; i < 3; ++i)
        {
            var start = center;
            var end = center;
            switch (i)
            {
                case 0:
                    start.X -= halfSize;
                    end.X += halfSize;
                    break;
                case 1:
                    start.Y -= halfSize;
                    end.Y += halfSize;
                    break;
                default:
                    start.Z -= halfSize;
                    end.Z += halfSize;
                    break;
            }

            AddLine(start, end, uintColor, depthTest);
        }
    }

    /// <summary>
    /// Add a quad on the XZ plane.
    /// </summary>
    public void AddQuad(Vector3f center, float width, float height, Color color, bool depthTest = true)
    {
        var uintColor = color.ToUInt();
        var vertex0 = new Vector3f(center.X - width / 2.0f, center.Y, center.Z - height / 2.0f);
        var vertex1 = new Vector3f(center.X + width / 2.0f, center.Y, center.Z - height / 2.0f);
        var vertex2 = new Vector3f(center.X + width / 2.0f, center.Y, center.Z + height / 2.0f);
        var vertex3 = new Vector3f(center.X - width / 2.0f, center.Y, center.Z + height / 2.0f);
        AddLine(vertex0, vertex1, uintColor, depthTest);
        AddLine(vertex1, vertex2, uintColor, depthTest);
        AddLine(vertex2, vertex3, uintColor, depthTest);
        AddLine(vertex3, vertex0, uintColor, depthTest);
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
