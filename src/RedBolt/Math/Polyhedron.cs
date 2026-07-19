using System.Collections.Generic;

namespace RedBolt;

/// <summary>A convex volume built from polygon faces.</summary>
public class Polyhedron : ICloneable
{
    /// <summary>Construct empty.</summary>
    public Polyhedron()
    {
    }

    /// <summary>Copy-construct from another polyhedron.</summary>
    public Polyhedron(Polyhedron polyhedron)
    {
        Faces = new List<List<Vector3f>>(polyhedron.Faces.Count);
        for (var i = 0; i < polyhedron.Faces.Count; ++i)
            Faces.Add(new List<Vector3f>(polyhedron.Faces[i]));
    }

    public object Clone() => new Polyhedron(this);

    /// <summary>Construct from a list of faces.</summary>
    public Polyhedron(IReadOnlyList<IReadOnlyList<Vector3f>> faces)
    {
        Faces = new List<List<Vector3f>>(faces.Count);
        for (var i = 0; i < faces.Count; ++i)
            Faces.Add(new List<Vector3f>(faces[i]));
    }

    /// <summary>Construct from a bounding box.</summary>
    public Polyhedron(BoundingBox box)
    {
        Define(box);
    }

    /// <summary>Construct from a frustum.</summary>
    public Polyhedron(Frustum frustum)
    {
        Define(frustum);
    }

    /// <summary>Assign from another polyhedron.</summary>
    public Polyhedron CopyAssign(Polyhedron rhs)
    {
        var faces = new List<List<Vector3f>>(rhs.Faces.Count);
        for (var i = 0; i < rhs.Faces.Count; ++i)
            faces.Add(new List<Vector3f>(rhs.Faces[i]));
        Faces = faces;
        return this;
    }

    /// <summary>Define from a bounding box.</summary>
    public void Define(BoundingBox box)
    {
        Span<Vector3f> vertices = stackalloc Vector3f[8];
        vertices[0] = box.Min;
        vertices[1] = new Vector3f(box.Max.X, box.Min.Y, box.Min.Z);
        vertices[2] = new Vector3f(box.Min.X, box.Max.Y, box.Min.Z);
        vertices[3] = new Vector3f(box.Max.X, box.Max.Y, box.Min.Z);
        vertices[4] = new Vector3f(box.Min.X, box.Min.Y, box.Max.Z);
        vertices[5] = new Vector3f(box.Max.X, box.Min.Y, box.Max.Z);
        vertices[6] = new Vector3f(box.Min.X, box.Max.Y, box.Max.Z);
        vertices[7] = box.Max;

        while (Faces.Count > 6)
            Faces.RemoveAt(Faces.Count - 1);
        while (Faces.Count < 6)
            Faces.Add(new List<Vector3f>(4));
        SetFace(0, vertices[3], vertices[7], vertices[5], vertices[1]);
        SetFace(1, vertices[6], vertices[2], vertices[0], vertices[4]);
        SetFace(2, vertices[6], vertices[7], vertices[3], vertices[2]);
        SetFace(3, vertices[1], vertices[5], vertices[4], vertices[0]);
        SetFace(4, vertices[7], vertices[6], vertices[4], vertices[5]);
        SetFace(5, vertices[2], vertices[3], vertices[1], vertices[0]);
    }

    /// <summary>Define from a frustum.</summary>
    public void Define(Frustum frustum)
    {
        var vertices = frustum.Vertices;

        while (Faces.Count > 6)
            Faces.RemoveAt(Faces.Count - 1);
        while (Faces.Count < 6)
            Faces.Add(new List<Vector3f>(4));
        SetFace(0, vertices[0], vertices[4], vertices[5], vertices[1]);
        SetFace(1, vertices[7], vertices[3], vertices[2], vertices[6]);
        SetFace(2, vertices[7], vertices[4], vertices[0], vertices[3]);
        SetFace(3, vertices[1], vertices[5], vertices[6], vertices[2]);
        SetFace(4, vertices[4], vertices[7], vertices[6], vertices[5]);
        SetFace(5, vertices[3], vertices[0], vertices[1], vertices[2]);
    }

    /// <summary>Add a triangle face.</summary>
    public void AddFace(Vector3f v0, Vector3f v1, Vector3f v2)
    {
        Faces.Add(new List<Vector3f> { v0, v1, v2 });
    }

    /// <summary>Add a quadrilateral face.</summary>
    public void AddFace(Vector3f v0, Vector3f v1, Vector3f v2, Vector3f v3)
    {
        Faces.Add(new List<Vector3f> { v0, v1, v2, v3 });
    }

    /// <summary>Add an arbitrary face.</summary>
    public void AddFace(IReadOnlyList<Vector3f> face)
    {
        Faces.Add(new List<Vector3f>(face));
    }

    /// <summary>Clip with a plane.</summary>
    public void Clip(Plane plane)
    {
        _clippedVertices.Clear();
        foreach (var face in Faces)
        {
            var lastVertex = default(Vector3f);
            var lastDistance = 0.0f;
            _outFace.Clear();

            for (var j = 0; j < face.Count; ++j)
            {
                var distance = plane.Distance(face[j]);
                if (distance >= 0.0f)
                {
                    if (lastDistance < 0.0f)
                    {
                        var t = lastDistance / (lastDistance - distance);
                        var clippedVertex = lastVertex + t * (face[j] - lastVertex);
                        _outFace.Add(clippedVertex);
                        _clippedVertices.Add(clippedVertex);
                    }
                    _outFace.Add(face[j]);
                }
                else
                {
                    if (lastDistance >= 0.0f && j != 0)
                    {
                        var t = lastDistance / (lastDistance - distance);
                        var clippedVertex = lastVertex + t * (face[j] - lastVertex);
                        _outFace.Add(clippedVertex);
                        _clippedVertices.Add(clippedVertex);
                    }
                }

                lastVertex = face[j];
                lastDistance = distance;
            }

            // Recheck the distances of the last and first vertices and add the final clipped vertex if applicable
            var firstDistance = plane.Distance(face[0]);
            if ((lastDistance < 0.0f && firstDistance >= 0.0f) || (lastDistance >= 0.0f && firstDistance < 0.0f))
            {
                var t = lastDistance / (lastDistance - firstDistance);
                var clippedVertex = lastVertex + t * (face[0] - lastVertex);
                _outFace.Add(clippedVertex);
                _clippedVertices.Add(clippedVertex);
            }

            // Do not keep faces which are less than triangles
            if (_outFace.Count < 3)
                _outFace.Clear();
            face.Clear();
            face.AddRange(_outFace);
        }

        // Remove empty faces
        for (var i = Faces.Count - 1; i >= 0; --i)
        {
            if (Faces[i].Count == 0)
                Faces.RemoveAt(i);
        }

        // Create a new face from the clipped vertices. First remove duplicates
        for (var i = 0; i < _clippedVertices.Count; ++i)
        {
            for (var j = _clippedVertices.Count - 1; j > i; --j)
            {
                if (_clippedVertices[j].ApproximatelyEquivalent(_clippedVertices[i]))
                    _clippedVertices.RemoveAt(j);
            }
        }

        if (_clippedVertices.Count > 3)
        {
            _outFace.Clear();
            // Start with the first vertex
            _outFace.Add(_clippedVertices[0]);
            _clippedVertices.RemoveAt(0);

            while (_clippedVertices.Count > 0)
            {
                // Then add the vertex which is closest to the last added
                var lastAdded = _outFace[^1];
                var bestDistance = float.PositiveInfinity;
                var bestIndex = 0;
                for (var i = 0; i < _clippedVertices.Count; ++i)
                {
                    var distance = (_clippedVertices[i] - lastAdded).LengthSquared;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestIndex = i;
                    }
                }

                _outFace.Add(_clippedVertices[bestIndex]);
                _clippedVertices.RemoveAt(bestIndex);
            }

            Faces.Add(new List<Vector3f>(_outFace));
        }
    }

    /// <summary>Clip with a bounding box.</summary>
    public void Clip(BoundingBox box)
    {
        Span<Vector3f> vertices = stackalloc Vector3f[8];
        vertices[0] = box.Min;
        vertices[1] = new Vector3f(box.Max.X, box.Min.Y, box.Min.Z);
        vertices[2] = new Vector3f(box.Min.X, box.Max.Y, box.Min.Z);
        vertices[3] = new Vector3f(box.Max.X, box.Max.Y, box.Min.Z);
        vertices[4] = new Vector3f(box.Min.X, box.Min.Y, box.Max.Z);
        vertices[5] = new Vector3f(box.Max.X, box.Min.Y, box.Max.Z);
        vertices[6] = new Vector3f(box.Min.X, box.Max.Y, box.Max.Z);
        vertices[7] = box.Max;

        Clip(new Plane(vertices[5], vertices[7], vertices[3]));
        Clip(new Plane(vertices[0], vertices[2], vertices[6]));
        Clip(new Plane(vertices[3], vertices[7], vertices[6]));
        Clip(new Plane(vertices[4], vertices[5], vertices[1]));
        Clip(new Plane(vertices[4], vertices[6], vertices[7]));
        Clip(new Plane(vertices[1], vertices[3], vertices[2]));
    }

    /// <summary>Clip with a frustum.</summary>
    public void Clip(Frustum frustum)
    {
        foreach (var plane in frustum.Planes)
            Clip(plane);
    }

    /// <summary>Clear all faces.</summary>
    public void Clear()
    {
        Faces.Clear();
    }

    /// <summary>Transform with a 3x3 matrix.</summary>
    public void Transform(Matrix3 transform)
    {
        for (var i = 0; i < Faces.Count; ++i)
        {
            for (var j = 0; j < Faces[i].Count; ++j)
                Faces[i][j] = transform * Faces[i][j];
        }
    }

    /// <summary>Transform with a 3x4 matrix.</summary>
    public void Transform(Matrix3x4 transform)
    {
        for (var i = 0; i < Faces.Count; ++i)
        {
            for (var j = 0; j < Faces[i].Count; ++j)
                Faces[i][j] = transform * Faces[i][j];
        }
    }

    /// <summary>Return transformed with a 3x3 matrix.</summary>
    public Polyhedron Transformed(Matrix3 transform)
    {
        var ret = new Polyhedron();
        ret.Faces = new List<List<Vector3f>>(Faces.Count);
        for (var i = 0; i < Faces.Count; ++i)
        {
            var face = new List<Vector3f>(Faces[i].Count);
            for (var j = 0; j < Faces[i].Count; ++j)
                face.Add(transform * Faces[i][j]);
            ret.Faces.Add(face);
        }

        return ret;
    }

    /// <summary>Return transformed with a 3x4 matrix.</summary>
    public Polyhedron Transformed(Matrix3x4 transform)
    {
        var ret = new Polyhedron();
        ret.Faces = new List<List<Vector3f>>(Faces.Count);
        for (var i = 0; i < Faces.Count; ++i)
        {
            var face = new List<Vector3f>(Faces[i].Count);
            for (var j = 0; j < Faces[i].Count; ++j)
                face.Add(transform * Faces[i][j]);
            ret.Faces.Add(face);
        }

        return ret;
    }

    /// <summary>Return whether is empty.</summary>
    public bool Empty() => Faces.Count == 0;

    /// <summary>Polygon faces.</summary>
    public List<List<Vector3f>> Faces = [];

    /// <summary>Set a triangle face by index.</summary>
    private void SetFace(uint index, Vector3f v0, Vector3f v1, Vector3f v2)
    {
        var face = Faces[(int)index];
        face.Clear();
        face.Add(v0);
        face.Add(v1);
        face.Add(v2);
    }

    /// <summary>Set a quadrilateral face by index.</summary>
    private void SetFace(uint index, Vector3f v0, Vector3f v1, Vector3f v2, Vector3f v3)
    {
        var face = Faces[(int)index];
        face.Clear();
        face.Add(v0);
        face.Add(v1);
        face.Add(v2);
        face.Add(v3);
    }

    /// <summary>Internal vector for clipped vertices.</summary>
    private readonly List<Vector3f> _clippedVertices = [];

    /// <summary>Internal vector for the new face being constructed.</summary>
    private readonly List<Vector3f> _outFace = [];

}
