namespace RedBolt;

/// <summary>
/// Frustum planes.
/// </summary>
public enum FrustumPlane
{
    Near = 0,
    Left,
    Right,
    Up,
    Down,
    Far,
}

/// <summary>
/// Convex constructed of 6 planes.
/// </summary>
public class Frustum : ICloneable
{
    public const int NumFrustumPlanes = 6;
    public const int NumFrustumVertices = 8;

    /// <summary>
    /// Construct a degenerate frustum with all points at origin.
    /// </summary>
    public Frustum()
    {
        Planes = new Plane[NumFrustumPlanes];
        Vertices = new Vector3f[NumFrustumVertices];
    }

    /// <summary>
    /// Copy-construct from another frustum.
    /// </summary>
    public Frustum(Frustum frustum)
    {
        Planes = new Plane[NumFrustumPlanes];
        Vertices = new Vector3f[NumFrustumVertices];
        Array.Copy(frustum.Planes, Planes, NumFrustumPlanes);
        Array.Copy(frustum.Vertices, Vertices, NumFrustumVertices);
    }

    public void CopyFrom(Frustum frustum)
    {
        Array.Copy(frustum.Planes, Planes, NumFrustumPlanes);
        Array.Copy(frustum.Vertices, Vertices, NumFrustumVertices);
    }

    /// <summary>
    /// Define with projection parameters and a transform matrix.
    /// </summary>
    public void Define(float fov, float aspectRatio, float zoom, float nearZ, float farZ)
    {
        Define(fov, aspectRatio, zoom, nearZ, farZ, Matrix3x4.Identity);
    }

    /// <summary>
    /// Define with projection parameters and a transform matrix.
    /// </summary>
    public void Define(float fov, float aspectRatio, float zoom, float nearZ, float farZ, Matrix3x4 transform)
    {
        nearZ = MathF.Max(nearZ, 0.0f);
        farZ = MathF.Max(farZ, nearZ);
        var halfViewSize = MathF.Tan(fov * MathDefs.DegToRad2) / zoom;
        var nearSize = new Vector3f
        {
            Z = nearZ,
            Y = nearZ * halfViewSize,
            X = nearZ * halfViewSize * aspectRatio
        };
        var farSize = new Vector3f
        {
            Z = farZ,
            Y = farZ * halfViewSize,
            X = farZ * halfViewSize * aspectRatio
        };

        Define(nearSize, farSize, transform);
    }

    /// <summary>
    /// Define with near and far dimension vectors and a transform matrix.
    /// </summary>
    public void Define(Vector3f nearSize, Vector3f farSize)
    {
        Define(nearSize, farSize, Matrix3x4.Identity);
    }

    /// <summary>
    /// Define with near and far dimension vectors and a transform matrix.
    /// </summary>
    public void Define(Vector3f nearSize, Vector3f farSize, Matrix3x4 transform)
    {
        Vertices[0] = transform * nearSize;
        Vertices[1] = transform * new Vector3f(nearSize.X, -nearSize.Y, nearSize.Z);
        Vertices[2] = transform * new Vector3f(-nearSize.X, -nearSize.Y, nearSize.Z);
        Vertices[3] = transform * new Vector3f(-nearSize.X, nearSize.Y, nearSize.Z);
        Vertices[4] = transform * farSize;
        Vertices[5] = transform * new Vector3f(farSize.X, -farSize.Y, farSize.Z);
        Vertices[6] = transform * new Vector3f(-farSize.X, -farSize.Y, farSize.Z);
        Vertices[7] = transform * new Vector3f(-farSize.X, farSize.Y, farSize.Z);

        UpdatePlanes();
    }

    /// <summary>
    /// Define with a bounding box and a transform matrix.
    /// </summary>
    public void Define(BoundingBox box)
    {
        Define(box, Matrix3x4.Identity);
    }

    /// <summary>
    /// Define with a bounding box and a transform matrix.
    /// </summary>
    public void Define(BoundingBox box, Matrix3x4 transform)
    {
        Vertices[0] = transform * new Vector3f(box.Max.X, box.Max.Y, box.Min.Z);
        Vertices[1] = transform * new Vector3f(box.Max.X, box.Min.Y, box.Min.Z);
        Vertices[2] = transform * new Vector3f(box.Min.X, box.Min.Y, box.Min.Z);
        Vertices[3] = transform * new Vector3f(box.Min.X, box.Max.Y, box.Min.Z);
        Vertices[4] = transform * new Vector3f(box.Max.X, box.Max.Y, box.Max.Z);
        Vertices[5] = transform * new Vector3f(box.Max.X, box.Min.Y, box.Max.Z);
        Vertices[6] = transform * new Vector3f(box.Min.X, box.Min.Y, box.Max.Z);
        Vertices[7] = transform * new Vector3f(box.Min.X, box.Max.Y, box.Max.Z);

        UpdatePlanes();
    }

    /// <summary>
    /// Define from a projection or view-projection matrix.
    /// </summary>
    public void Define(Matrix4 projection)
    {
        var projectionInverse = projection.Inverse;

        Vertices[0] = projectionInverse * new Vector3f(1.0f, 1.0f, 0.0f);
        Vertices[1] = projectionInverse * new Vector3f(1.0f, -1.0f, 0.0f);
        Vertices[2] = projectionInverse * new Vector3f(-1.0f, -1.0f, 0.0f);
        Vertices[3] = projectionInverse * new Vector3f(-1.0f, 1.0f, 0.0f);
        Vertices[4] = projectionInverse * new Vector3f(1.0f, 1.0f, 1.0f);
        Vertices[5] = projectionInverse * new Vector3f(1.0f, -1.0f, 1.0f);
        Vertices[6] = projectionInverse * new Vector3f(-1.0f, -1.0f, 1.0f);
        Vertices[7] = projectionInverse * new Vector3f(-1.0f, 1.0f, 1.0f);

        UpdatePlanes();
    }

    /// <summary>
    /// Define with orthographic projection parameters and a transform matrix.
    /// </summary>
    public void DefineOrtho(float orthoSize, float aspectRatio, float zoom, float nearZ, float farZ)
    {
        DefineOrtho(orthoSize, aspectRatio, zoom, nearZ, farZ, Matrix3x4.Identity);
    }

    /// <summary>
    /// Define with orthographic projection parameters and a transform matrix.
    /// </summary>
    public void DefineOrtho(float orthoSize, float aspectRatio, float zoom, float nearZ, float farZ, Matrix3x4 transform)
    {
        nearZ = MathF.Max(nearZ, 0.0f);
        farZ = MathF.Max(farZ, nearZ);
        var halfViewSize = orthoSize * 0.5f / zoom;
        var nearSize = new Vector3f(halfViewSize * aspectRatio, halfViewSize, nearZ);
        var farSize = new Vector3f(halfViewSize * aspectRatio, halfViewSize, farZ);

        Define(nearSize, farSize, transform);
    }

    /// <summary>
    /// Define a split (limited) frustum from a projection matrix, with near &amp; far distances specified.
    /// </summary>
    public void DefineSplit(Matrix4 projection, float nearZ, float farZ)
    {
        var projectionInverse = projection.Inverse;

        // Figure out depth values for near & far
        var nearTemp = projection * new Vector4f(0.0f, 0.0f, nearZ, 1.0f);
        var farTemp = projection * new Vector4f(0.0f, 0.0f, farZ, 1.0f);
        var nearDepth = nearTemp.Z / nearTemp.W;
        var farDepth = farTemp.Z / farTemp.W;

        Vertices[0] = projectionInverse * new Vector3f(1.0f, 1.0f, nearDepth);
        Vertices[1] = projectionInverse * new Vector3f(1.0f, -1.0f, nearDepth);
        Vertices[2] = projectionInverse * new Vector3f(-1.0f, -1.0f, nearDepth);
        Vertices[3] = projectionInverse * new Vector3f(-1.0f, 1.0f, nearDepth);
        Vertices[4] = projectionInverse * new Vector3f(1.0f, 1.0f, farDepth);
        Vertices[5] = projectionInverse * new Vector3f(1.0f, -1.0f, farDepth);
        Vertices[6] = projectionInverse * new Vector3f(-1.0f, -1.0f, farDepth);
        Vertices[7] = projectionInverse * new Vector3f(-1.0f, 1.0f, farDepth);

        UpdatePlanes();
    }

    /// <summary>
    /// Transform by a 3x3 matrix.
    /// </summary>
    public void Transform(Matrix3 transform)
    {
        for (var i = 0; i < NumFrustumVertices; ++i)
            Vertices[i] = transform * Vertices[i];

        UpdatePlanes();
    }

    /// <summary>
    /// Transform by a 3x4 matrix.
    /// </summary>
    public void Transform(Matrix3x4 transform)
    {
        for (var i = 0; i < NumFrustumVertices; ++i)
            Vertices[i] = transform * Vertices[i];

        UpdatePlanes();
    }

    /// <summary>
    /// Test if a point is inside or outside.
    /// </summary>
    public Intersection IsInside(Vector3f point)
    {
        for (var i = 0; i < NumFrustumPlanes; ++i)
        {
            if (Planes[i].Distance(point) < 0.0f)
                return Intersection.Outside;
        }

        return Intersection.Inside;
    }

    /// <summary>
    /// Test if a sphere is inside, outside or intersects.
    /// </summary>
    public Intersection IsInside(Sphere sphere)
    {
        var allInside = true;
        for (var i = 0; i < NumFrustumPlanes; ++i)
        {
            var distance = Planes[i].Distance(sphere.Center);
            if (distance < -sphere.Radius)
                return Intersection.Outside;
            else if (distance < sphere.Radius)
                allInside = false;
        }

        return allInside ? Intersection.Inside : Intersection.Intersects;
    }

    /// <summary>
    /// Test if a sphere if (partially) inside or outside.
    /// </summary>
    public Intersection IsInsideFast(Sphere sphere)
    {
        for (var i = 0; i < NumFrustumPlanes; ++i)
        {
            if (Planes[i].Distance(sphere.Center) < -sphere.Radius)
                return Intersection.Outside;
        }

        return Intersection.Inside;
    }

    /// <summary>
    /// Test if a bounding box is inside, outside or intersects.
    /// </summary>
    public Intersection IsInside(BoundingBox box)
    {
        var center = box.Center;
        var edge = center - box.Min;
        var allInside = true;

        for (var i = 0; i < NumFrustumPlanes; ++i)
        {
            var distance = Planes[i].Normal.DotProduct(center) + Planes[i].D;
            var absoluteDistance = Planes[i].AbsNormal.DotProduct(edge);
            if (distance < -absoluteDistance)
                return Intersection.Outside;
            else if (distance < absoluteDistance)
                allInside = false;
        }

        return allInside ? Intersection.Inside : Intersection.Intersects;
    }

    /// <summary>
    /// Test if a bounding box is (partially) inside or outside.
    /// </summary>
    public Intersection IsInsideFast(BoundingBox box)
    {
        var center = box.Center;
        var edge = center - box.Min;

        for (var i = 0; i < NumFrustumPlanes; ++i)
        {
            var distance = Planes[i].Normal.DotProduct(center) + Planes[i].D;
            var absoluteDistance = Planes[i].AbsNormal.DotProduct(edge);
            if (distance < -absoluteDistance)
                return Intersection.Outside;
        }

        return Intersection.Inside;
    }

    /// <summary>
    /// Return distance of a point to the frustum, or 0 if inside.
    /// </summary>
    public float Distance(Vector3f point)
    {
        var distance = 0.0f;
        for (var i = 0; i < NumFrustumPlanes; ++i)
            distance = MathF.Max(-Planes[i].Distance(point), distance);

        return distance;
    }

    /// <summary>
    /// Return transformed by a 3x3 matrix.
    /// </summary>
    public Frustum Transformed(Matrix3 transform)
    {
        var transformed = new Frustum();
        for (var i = 0; i < NumFrustumVertices; ++i)
            transformed.Vertices[i] = transform * Vertices[i];

        transformed.UpdatePlanes();
        return transformed;
    }

    /// <summary>
    /// Return transformed by a 3x4 matrix.
    /// </summary>
    public Frustum Transformed(Matrix3x4 transform)
    {
        var transformed = new Frustum();
        for (var i = 0; i < NumFrustumVertices; ++i)
            transformed.Vertices[i] = transform * Vertices[i];

        transformed.UpdatePlanes();
        return transformed;
    }

    /// <summary>
    /// Return projected by a 4x4 projection matrix.
    /// </summary>
    public Rect Projected(Matrix4 projection)
    {
        var rect = new Rect();

        ProjectAndMergeEdge(Vertices[0], Vertices[4], ref rect, projection);
        ProjectAndMergeEdge(Vertices[1], Vertices[5], ref rect, projection);
        ProjectAndMergeEdge(Vertices[2], Vertices[6], ref rect, projection);
        ProjectAndMergeEdge(Vertices[3], Vertices[7], ref rect, projection);
        ProjectAndMergeEdge(Vertices[4], Vertices[5], ref rect, projection);
        ProjectAndMergeEdge(Vertices[5], Vertices[6], ref rect, projection);
        ProjectAndMergeEdge(Vertices[6], Vertices[7], ref rect, projection);
        ProjectAndMergeEdge(Vertices[7], Vertices[4], ref rect, projection);

        return rect;
    }

    /// <summary>
    /// Update the planes. Called internally.
    /// </summary>
    public void UpdatePlanes()
    {
        Planes[(int)FrustumPlane.Near].Define(Vertices[2], Vertices[1], Vertices[0]);
        Planes[(int)FrustumPlane.Left].Define(Vertices[3], Vertices[7], Vertices[6]);
        Planes[(int)FrustumPlane.Right].Define(Vertices[1], Vertices[5], Vertices[4]);
        Planes[(int)FrustumPlane.Up].Define(Vertices[0], Vertices[4], Vertices[7]);
        Planes[(int)FrustumPlane.Down].Define(Vertices[6], Vertices[5], Vertices[1]);
        Planes[(int)FrustumPlane.Far].Define(Vertices[5], Vertices[6], Vertices[7]);

        // Check if we ended up with inverted planes (reflected transform) and flip in that case
        if (Planes[(int)FrustumPlane.Near].Distance(Vertices[5]) < 0.0f)
        {
            for (var i = 0; i < NumFrustumPlanes; ++i)
            {
                Planes[i].Normal = -Planes[i].Normal;
                Planes[i].D = -Planes[i].D;
            }
        }
    }

    public object Clone() => new Frustum(this);

    private static Vector3f ClipEdgeZ(Vector3f v0, Vector3f v1, float clipZ)
    {
        return new Vector3f(
            v1.X + (v0.X - v1.X) * ((clipZ - v1.Z) / (v0.Z - v1.Z)),
            v1.Y + (v0.Y - v1.Y) * ((clipZ - v1.Z) / (v0.Z - v1.Z)),
            clipZ);
    }

    private static void ProjectAndMergeEdge(Vector3f v0, Vector3f v1, ref Rect rect, Matrix4 projection)
    {
        // Check if both vertices behind near plane
        if (v0.Z < MathDefs.MinNearClip && v1.Z < MathDefs.MinNearClip)
            return;

        // Check if need to clip one of the vertices
        if (v1.Z < MathDefs.MinNearClip)
            v1 = ClipEdgeZ(v1, v0, MathDefs.MinNearClip);
        else if (v0.Z < MathDefs.MinNearClip)
            v0 = ClipEdgeZ(v0, v1, MathDefs.MinNearClip);

        // Project, perspective divide and merge
        var transformedV0 = projection * v0;
        var transformedV1 = projection * v1;
        rect.Merge(new Vector2f(transformedV0.X, transformedV0.Y));
        rect.Merge(new Vector2f(transformedV1.X, transformedV1.Y));
    }

    public Plane[] Planes { get; }
    public Vector3f[] Vertices { get; }
}
