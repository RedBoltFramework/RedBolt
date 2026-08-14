// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.

namespace RedBolt.PhysicsBox2D;

using RedBolt.Bindings.Box2D;
using RedBolt.Core;
using RedBolt.Math;

/// <summary>2D box collision component.</summary>
[Reflected(ObjectCategory.Category_Physics2D, InheritAttributes = true)]
public partial class CollisionBox2D : CollisionShape2D
{
    private Vector2f _size = Vector2f.One;
    private Vector2f _center;
    private float _angle;

    [Serialized("Size")]
    public Vector2f Size { get => _size; set { _size = value; RecreateFixture(); } }
    [Serialized("Center")]
    public Vector2f Center { get => _center; set { _center = value; RecreateFixture(); } }
    [Serialized("Angle")]
    public float Angle { get => _angle; set { _angle = value; RecreateFixture(); } }

    public void SetSize(float width, float height) => Size = new Vector2f(width, height);
    public void SetCenter(float x, float y) => Center = new Vector2f(x, y);

    protected override IEnumerable<B2ShapeId> CreateNativeShapes(B2BodyId bodyId, B2ShapeDef def)
    {
        var scale = WorldScale2D;
        using var center = PhysicsUtils2D.ToB2(_center * scale);
        using var rotation = Box2DNativeBindings.B2MakeRot(_angle * PhysicsUtils2D.DegreesToRadians);
        using var polygon = Box2DNativeBindings.B2MakeOffsetBox(
            MathF.Abs(_size.X * scale.X) * 0.5f, MathF.Abs(_size.Y * scale.Y) * 0.5f, center, rotation);
        yield return Box2DNativeBindings.B2CreatePolygonShape(bodyId, def, polygon);
    }
}

/// <summary>2D circle collision component.</summary>
[Reflected(ObjectCategory.Category_Physics2D, InheritAttributes = true)]
public partial class CollisionCircle2D : CollisionShape2D
{
    private float _radius = 1.0f;
    private Vector2f _center;

    [Serialized("Radius")]
    public float Radius { get => _radius; set { _radius = MathF.Max(value, 0.0f); RecreateFixture(); } }
    [Serialized("Center")]
    public Vector2f Center { get => _center; set { _center = value; RecreateFixture(); } }
    public void SetCenter(float x, float y) => Center = new Vector2f(x, y);

    protected override IEnumerable<B2ShapeId> CreateNativeShapes(B2BodyId bodyId, B2ShapeDef def)
    {
        var scale = WorldScale2D.X;
        using var circle = new B2Circle
        {
            CenterX = _center.X * scale,
            CenterY = _center.Y * scale,
            Radius = _radius * scale,
        };
        yield return Box2DNativeBindings.B2CreateCircleShape(bodyId, def, circle);
    }
}

/// <summary>2D edge collision component.</summary>
[Reflected(ObjectCategory.Category_Physics2D, InheritAttributes = true)]
public partial class CollisionEdge2D : CollisionShape2D
{
    private Vector2f _vertex1 = new(-0.01f, 0.0f);
    private Vector2f _vertex2 = new(0.01f, 0.0f);

    [Serialized("Vertex 1")]
    public Vector2f Vertex1 { get => _vertex1; set { _vertex1 = value; RecreateFixture(); } }
    [Serialized("Vertex 2")]
    public Vector2f Vertex2 { get => _vertex2; set { _vertex2 = value; RecreateFixture(); } }
    public void SetVertices(Vector2f vertex1, Vector2f vertex2)
    {
        _vertex1 = vertex1;
        _vertex2 = vertex2;
        RecreateFixture();
    }

    protected override IEnumerable<B2ShapeId> CreateNativeShapes(B2BodyId bodyId, B2ShapeDef def)
    {
        var scale = WorldScale2D;
        using var segment = new B2Segment
        {
            Point1X = _vertex1.X * scale.X,
            Point1Y = _vertex1.Y * scale.Y,
            Point2X = _vertex2.X * scale.X,
            Point2Y = _vertex2.Y * scale.Y,
        };
        yield return Box2DNativeBindings.B2CreateSegmentShape(bodyId, def, segment);
    }
}

/// <summary>2D polygon collision component.</summary>
[Reflected(ObjectCategory.Category_Physics2D, InheritAttributes = true)]
public partial class CollisionPolygon2D : CollisionShape2D
{
    private readonly List<Vector2f> _vertices = [];

    public uint VertexCount
    {
        get => (uint)_vertices.Count;
        set
        {
            while (_vertices.Count < value)
                _vertices.Add(default);
            if (_vertices.Count > value)
                _vertices.RemoveRange((int)value, _vertices.Count - (int)value);
        }
    }

    public IReadOnlyList<Vector2f> Vertices => _vertices;

    [Serialized("Vertices")]
    public byte[] VerticesAttr
    {
        get => ShapeVertexSerializer.Write(_vertices);
        set => SetVertices(ShapeVertexSerializer.Read(value));
    }

    public Vector2f GetVertex(uint index) => index < _vertices.Count ? _vertices[(int)index] : default;
    public void SetVertex(uint index, Vector2f vertex)
    {
        if (index >= _vertices.Count)
            return;
        _vertices[(int)index] = vertex;
        if (index == _vertices.Count - 1)
            RecreateFixture();
    }
    public void SetVertices(IEnumerable<Vector2f> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        _vertices.Clear();
        _vertices.AddRange(vertices);
        RecreateFixture();
    }

    protected override IEnumerable<B2ShapeId> CreateNativeShapes(B2BodyId bodyId, B2ShapeDef def)
    {
        if (_vertices.Count is < 3 or > 8)
            yield break;
        var scaled = _vertices.Select(value => value * WorldScale2D).ToArray();
        if (SignedArea(scaled) < 0.0f)
            Array.Reverse(scaled);

        using var polygon = new B2Polygon { Count = scaled.Length, Radius = 0.0f };
        var center = ComputeCentroid(scaled);
        polygon.CentroidX = center.X;
        polygon.CentroidY = center.Y;
        for (var i = 0; i < scaled.Length; ++i)
        {
            polygon.Vertices[i].X = scaled[i].X;
            polygon.Vertices[i].Y = scaled[i].Y;
            var edge = scaled[(i + 1) % scaled.Length] - scaled[i];
            var normal = new Vector2f(edge.Y, -edge.X).NormalizedOrDefault();
            polygon.Normals[i].X = normal.X;
            polygon.Normals[i].Y = normal.Y;
        }
        yield return Box2DNativeBindings.B2CreatePolygonShape(bodyId, def, polygon);
    }

    private static float SignedArea(IReadOnlyList<Vector2f> vertices)
    {
        var twiceArea = 0.0f;
        for (var i = 0; i < vertices.Count; ++i)
            twiceArea += vertices[i].CrossProduct(vertices[(i + 1) % vertices.Count]);
        return twiceArea * 0.5f;
    }

    private static Vector2f ComputeCentroid(IReadOnlyList<Vector2f> vertices)
    {
        var center = Vector2f.Zero;
        var area6 = 0.0f;
        for (var i = 0; i < vertices.Count; ++i)
        {
            var current = vertices[i];
            var next = vertices[(i + 1) % vertices.Count];
            var cross = current.CrossProduct(next);
            center += (current + next) * cross;
            area6 += cross * 3.0f;
        }
        return MathF.Abs(area6) > MathDefs.Epsilon ? center / area6 : vertices.Aggregate(Vector2f.Zero, (sum, value) => sum + value) / vertices.Count;
    }
}

/// <summary>2D chain collision component.</summary>
[Reflected(ObjectCategory.Category_Physics2D, InheritAttributes = true)]
public partial class CollisionChain2D : CollisionShape2D
{
    private readonly List<Vector2f> _vertices = [];
    private bool _loop;

    [Serialized("Loop")]
    public bool Loop { get => _loop; set { _loop = value; RecreateFixture(); } }
    public uint VertexCount
    {
        get => (uint)_vertices.Count;
        set
        {
            while (_vertices.Count < value)
                _vertices.Add(default);
            if (_vertices.Count > value)
                _vertices.RemoveRange((int)value, _vertices.Count - (int)value);
        }
    }
    public IReadOnlyList<Vector2f> Vertices => _vertices;
    [Serialized("Vertices")]
    public byte[] VerticesAttr
    {
        get => ShapeVertexSerializer.Write(_vertices);
        set => SetVertices(ShapeVertexSerializer.Read(value));
    }
    public Vector2f GetVertex(uint index) => index < _vertices.Count ? _vertices[(int)index] : default;
    public void SetVertex(uint index, Vector2f vertex)
    {
        if (index >= _vertices.Count)
            return;
        _vertices[(int)index] = vertex;
        if (index == _vertices.Count - 1)
            RecreateFixture();
    }
    public void SetVertices(IEnumerable<Vector2f> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        _vertices.Clear();
        _vertices.AddRange(vertices);
        RecreateFixture();
    }

    protected override IEnumerable<B2ShapeId> CreateNativeShapes(B2BodyId bodyId, B2ShapeDef def)
    {
        if (_vertices.Count < 2)
            yield break;
        var segmentCount = _loop ? _vertices.Count : _vertices.Count - 1;
        var scale = WorldScale2D;
        for (var i = 0; i < segmentCount; ++i)
        {
            var a = _vertices[i] * scale;
            var b = _vertices[(i + 1) % _vertices.Count] * scale;
            using var segment = new B2Segment { Point1X = a.X, Point1Y = a.Y, Point2X = b.X, Point2Y = b.Y };
            yield return Box2DNativeBindings.B2CreateSegmentShape(bodyId, def, segment);
        }
    }
}

internal static class ShapeVertexSerializer
{
    public static byte[] Write(IReadOnlyList<Vector2f> vertices)
    {
        using var stream = new MemoryStream(vertices.Count * sizeof(float) * 2);
        using var writer = new BinaryWriter(stream);
        foreach (var vertex in vertices)
        {
            writer.Write(vertex.X);
            writer.Write(vertex.Y);
        }
        return stream.ToArray();
    }

    public static IReadOnlyList<Vector2f> Read(byte[]? data)
    {
        if (data is null || data.Length == 0)
            return [];
        var result = new List<Vector2f>(data.Length / (sizeof(float) * 2));
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream);
        while (stream.Position + sizeof(float) * 2 <= stream.Length)
            result.Add(new Vector2f(reader.ReadSingle(), reader.ReadSingle()));
        return result;
    }
}
