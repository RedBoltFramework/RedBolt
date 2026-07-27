using RedBolt.Math;

namespace RedBolt.Graphics;

/// <summary>
/// Debug rendering line.
/// </summary>
public struct DebugLine
{
    /// <summary>
    /// Construct undefined.
    /// </summary>
    public DebugLine()
    {
        Start = default;
        End = default;
        Color = 0;
    }

    /// <summary>
    /// Construct with start and end positions and color.
    /// </summary>
    public DebugLine(Vector3f start, Vector3f end, uint color)
    {
        Start = start;
        End = end;
        Color = color;
    }

    /// <summary>
    /// Start position.
    /// </summary>
    public Vector3f Start;

    /// <summary>
    /// End position.
    /// </summary>
    public Vector3f End;

    /// <summary>
    /// Color.
    /// </summary>
    public uint Color;
}

/// <summary>
/// Debug render triangle.
/// </summary>
public struct DebugTriangle
{
    /// <summary>
    /// Construct undefined.
    /// </summary>
    public DebugTriangle()
    {
        VertexA = default;
        VertexB = default;
        VertexC = default;
        Color = 0;
    }

    /// <summary>
    /// Construct with start and end positions and color.
    /// </summary>
    public DebugTriangle(Vector3f vertexA, Vector3f vertexB, Vector3f vertexC, uint color)
    {
        VertexA = vertexA;
        VertexB = vertexB;
        VertexC = vertexC;
        Color = color;
    }

    /// <summary>
    /// Vertex a.
    /// </summary>
    public Vector3f VertexA;

    /// <summary>
    /// Vertex b.
    /// </summary>
    public Vector3f VertexB;

    /// <summary>
    /// Vertex c.
    /// </summary>
    public Vector3f VertexC;

    /// <summary>
    /// Color.
    /// </summary>
    public uint Color;
}
