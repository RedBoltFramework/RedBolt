using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

/// <summary>One billboard in the billboard set.</summary>
public struct Billboard
{
    public Billboard()
    {
        Color = new Color();
    }

    /// <summary>Position.</summary>
    public Vector3f Position;
    /// <summary>Two-dimensional size. If BillboardSet has fixed screen size enabled, this is measured in pixels instead of world units.</summary>
    public Vector2f Size;
    /// <summary>UV coordinates.</summary>
    public Rect Uv;
    /// <summary>Color.</summary>
    public Color Color;
    /// <summary>Rotation.</summary>
    public float Rotation;
    /// <summary>Direction (For direction based billboard only).</summary>
    public Vector3f Direction;
    /// <summary>Enabled flag.</summary>
    public bool Enabled;
    /// <summary>Sort distance. Used internally.</summary>
    public float SortDistance;
    /// <summary>Scale factor for fixed screen size mode. Used internally.</summary>
    public float ScreenScaleFactor;
}

/// <summary>Billboard component.</summary>
public class BillboardSet : Drawable
{
    private static readonly float InvSqrtTwo = 1.0f / MathF.Sqrt(2.0f);

    private static readonly string[] FaceCameraModeNames =
    [
        "None",
        "Rotate XYZ",
        "Rotate Y",
        "LookAt XYZ",
        "LookAt Y",
        "LookAt Mixed",
        "Direction",
        "Axis Angle"
    ];

    /// <summary>Billboards.</summary>
    protected readonly List<Billboard> _billboards = [];
    /// <summary>Animation LOD bias.</summary>
    protected float _animationLodBias = 1.0f;
    /// <summary>Animation LOD timer.</summary>
    protected float _animationLodTimer;
    /// <summary>Billboards relative flag.</summary>
    protected bool _relative = true;
    /// <summary>Scale affects billboard scale flag.</summary>
    protected bool _scaled = true;
    /// <summary>Billboards sorted flag.</summary>
    protected bool _sorted;
    /// <summary>Billboards fixed screen size flag.</summary>
    protected bool _fixedScreenSize;
    /// <summary>Billboard rotation mode in relation to the camera.</summary>
    protected FaceCameraMode _faceCameraMode = FaceCameraMode.RotateXyz;
    /// <summary>Minimal angle between billboard normal and look-at direction.</summary>
    protected float _minAngle;

    /// <summary>Geometry.</summary>
    private readonly Geometry _geometry = new();
    /// <summary>Vertex buffer.</summary>
    private readonly VertexBuffer _vertexBuffer = new();
    /// <summary>Index buffer.</summary>
    private readonly IndexBuffer _indexBuffer = new();
    /// <summary>Transform matrices for position and billboard orientation.</summary>
    private readonly Matrix3x4[] _transforms = [Matrix3x4.Identity, Matrix3x4.Identity];
    /// <summary>Buffers need resize flag.</summary>
    private bool _bufferSizeDirty = true;
    /// <summary>Vertex buffer needs rewrite flag.</summary>
    private bool _bufferDirty = true;
    /// <summary>Force update flag (ignore animation LOD momentarily).</summary>
    private bool _forceUpdate;
    /// <summary>Update billboard geometry type.</summary>
    private bool _geometryTypeUpdate;
    /// <summary>Sorting flag. Triggers a vertex buffer rewrite for each view this billboard set is rendered from.</summary>
    private bool _sortThisFrame;
    /// <summary>Whether was last rendered from an ortho camera.</summary>
    private bool _hasOrthoCamera;
    /// <summary>Frame number on which was last sorted.</summary>
    private uint _sortFrameNumber;
    /// <summary>Previous offset to camera for determining whether sorting is necessary.</summary>
    private Vector3f _previousOffset;
    /// <summary>Billboard indices for sorting (C++ stores pointers into billboards_).</summary>
    private readonly List<int> _sortedBillboards = [];

    /// <summary>Construct.</summary>
    public BillboardSet()
        : base(DrawableFlags.Geometry)
    {
        RegisterAttribute("Is Enabled", typeof(bool), self => ((BillboardSet)self).Enabled,
            (self, value) => ((BillboardSet)self).SetEnabled(value is bool enabled && enabled), true);

        _geometry.SetVertexBuffer(0, _vertexBuffer);
        _geometry.SetIndexBuffer(_indexBuffer);

        _vertexBuffer.SetDebugName("BillboardSet Geometry");
        _indexBuffer.SetDebugName("BillboardSet Geometry");

        _batches.Add(new SourceBatch
        {
            Geometry = _geometry,
            GeometryType = GeometryType.Billboard,
            WorldTransform = _transforms
        });
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _sortedBillboards.Clear();
        _geometry.Dispose();
        _indexBuffer.Dispose();
        _vertexBuffer.Dispose();
        _billboards.Clear();
        base.Dispose();
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<BillboardSet>(ObjectCategory.Category_Geometry);
    }

    /// <summary>Process octree raycast. May be called from a worker thread.</summary>
    public override void ProcessRayQuery(RayOctreeQuery query, List<RayQueryResult> results)
    {
        // If no billboard-level testing, use the Drawable test
        if (query.Level < RayQueryLevel.RayTriangle)
        {
            base.ProcessRayQuery(query, results);
            return;
        }

        // Check ray hit distance to AABB before proceeding with billboard-level tests
        if (query.Ray.HitDistance(GetWorldBoundingBox()) >= query.MaxDistance)
            return;

        var worldTransform = Node.GetWorldTransform();
        var billboardTransform = _relative ? worldTransform : Matrix3x4.Identity;
        var billboardScale = _scaled ? worldTransform.Scale : Vector3f.One;

        for (var i = 0; i < _billboards.Count; ++i)
        {
            var billboard = _billboards[i];
            if (!billboard.Enabled)
                continue;

            // Approximate the billboards as spheres for raycasting
            var size = InvSqrtTwo * (billboard.Size.X * billboardScale.X + billboard.Size.Y * billboardScale.Y);
            if (_fixedScreenSize)
                size *= billboard.ScreenScaleFactor;
            var center = billboardTransform * billboard.Position;
            var billboardSphere = new Sphere(center, size);

            var distance = query.Ray.HitDistance(billboardSphere);
            if (distance < query.MaxDistance)
            {
                // If the code reaches here then we have a hit
                results.Add(new RayQueryResult
                {
                    Position = query.Ray.Origin + distance * query.Ray.Direction,
                    Normal = -query.Ray.Direction,
                    Distance = distance,
                    Drawable = this,
                    Node = Node,
                    SubObject = checked((uint)i)
                });
            }
        }
    }

    /// <summary>Return currently requested format of vertex buffer.</summary>
    private uint GetVertexBufferFormat()
    {
        if (_faceCameraMode == FaceCameraMode.AxisAngle)
            return (uint)(VertexMask.Position | VertexMask.Normal | VertexMask.Color | VertexMask.TexCoord1 | VertexMask.Tangent);
        if (_faceCameraMode == FaceCameraMode.Direction)
            return (uint)(VertexMask.Position | VertexMask.Normal | VertexMask.Color | VertexMask.TexCoord1 | VertexMask.TexCoord2);
        return (uint)(VertexMask.Position | VertexMask.Color | VertexMask.TexCoord1 | VertexMask.TexCoord2);
    }

    /// <summary>Calculate distance and prepare batches for rendering. May be called from worker thread(s), possibly re-entrantly.</summary>
    public override void UpdateBatches(CommonFrameInfo frame)
    {
        // If beginning a new frame, assume no sorting first
        if (frame.FrameNumber != _sortFrameNumber)
        {
            _sortThisFrame = false;
            _sortFrameNumber = frame.FrameNumber;
        }

        var worldPos = Node.GetWorldPosition();
        var offset = worldPos - frame.Camera!.Node!.GetWorldPosition();
        // Sort if position relative to camera has changed
        if (offset != _previousOffset || frame.Camera.IsOrthographic() != _hasOrthoCamera)
        {
            if (_sorted)
                _sortThisFrame = true;
            if (_faceCameraMode == FaceCameraMode.Direction)
                _bufferDirty = true;

            _hasOrthoCamera = frame.Camera.IsOrthographic();
        }

        // Calculate fixed screen size scale factor for billboards. Will not dirty the buffer unless actually changed
        if (_fixedScreenSize)
            CalculateFixedScreenSize(frame);

        SetDistance(frame.Camera.GetDistance(GetWorldBoundingBox().Center));

        // Calculate scaled distance for animation LOD
        var scale = GetWorldBoundingBox().Size.DotProduct(GraphicsDefs.DotScale);
        // If there are no billboards, the size becomes zero, and LOD'ed updates no longer happen. Disable LOD in that case
        if (scale > MathDefs.Epsilon)
            SetLodDistance(frame.Camera.GetLodDistance(GetDistance(), scale, LodBias));
        else
            SetLodDistance(0.0f);

        var batch = _batches[0];
        batch.Distance = GetDistance();
        batch.NumWorldTransforms = 2;
        // Billboard positioning
        _transforms[0] = _relative ? Node.GetWorldTransform() : Matrix3x4.Identity;
        // Billboard rotation
        Quaternion billboardRotation;
        if (_faceCameraMode != FaceCameraMode.None && _faceCameraMode != FaceCameraMode.AxisAngle)
            billboardRotation = frame.Camera.GetFaceCameraRotation(Node.GetWorldPosition(), Node.GetWorldRotation(), _faceCameraMode, _minAngle);
        else
            billboardRotation = Node.GetWorldRotation();
        _transforms[1] = new Matrix3x4(Vector3f.Zero, billboardRotation, Vector3f.One);
        batch.WorldTransform = _transforms;
        _batches[0] = batch;

        // Update buffer size and format
        if (_bufferSizeDirty || (_indexBuffer.GetRawBuffer()?.IsDataLost() ?? false))
            RequestUpdateBatchesDelayed(frame);
    }

    /// <summary>Batch update from main thread. Called on demand only if RequestUpdateBatchesDelayed() is called from UpdateBatches().</summary>
    public override void UpdateBatchesDelayed(CommonFrameInfo frame)
    {
        UpdateBufferSize();
    }

    /// <summary>Prepare geometry for rendering. Called from a worker thread if possible (no GPU update).</summary>
    public override void UpdateGeometry(CommonFrameInfo frame)
    {
        // If rendering from multiple views and fixed screen size is in use, re-update scale factors before each render
        if (_fixedScreenSize && _viewCameras.Count > 1)
            CalculateFixedScreenSize(frame);

        // If using camera facing, re-update the rotation for the current view now
        if (_faceCameraMode != FaceCameraMode.None && _faceCameraMode != FaceCameraMode.AxisAngle)
        {
            _transforms[1] = new Matrix3x4(Vector3f.Zero, frame.Camera!.GetFaceCameraRotation(Node.GetWorldPosition(),
                Node.GetWorldRotation(), _faceCameraMode, _minAngle), Vector3f.One);
        }

        if (_bufferDirty || _sortThisFrame || (_vertexBuffer.GetRawBuffer()?.IsDataLost() ?? false))
            UpdateVertexBuffer(frame);
    }

    /// <summary>Return whether a geometry update is necessary, and if it can happen in a worker thread.</summary>
    public override UpdateGeometryType GetUpdateGeometryType()
    {
        // If using camera facing, always need some kind of geometry update, in case the billboard set is rendered from several views
        if (_bufferDirty || _bufferSizeDirty || (_vertexBuffer.GetRawBuffer()?.IsDataLost() ?? false) ||
            (_indexBuffer.GetRawBuffer()?.IsDataLost() ?? false) || _sortThisFrame ||
            _faceCameraMode != FaceCameraMode.None || _fixedScreenSize)
            return UpdateGeometryType.MainThread;
        return UpdateGeometryType.None;
    }

    /// <summary>Set material.</summary>
    public void SetMaterial(Material? material)
    {
        var batch = _batches[0];
        batch.Material = material;
        _batches[0] = batch;
    }

    /// <summary>Set number of billboards.</summary>
    public void SetNumBillboards(uint num)
    {
        // Prevent negative value being assigned from the editor
        if (num > MathDefs.MaxInt)
            num = 0;

        var oldNum = (uint)_billboards.Count;
        if (num == oldNum)
            return;

        if (num < oldNum)
            _billboards.RemoveRange((int)num, (int)(oldNum - num));
        else
        {
            // Set default values to new billboards
            for (var i = oldNum; i < num; ++i)
            {
                _billboards.Add(new Billboard
                {
                    Position = Vector3f.Zero,
                    Size = Vector2f.One,
                    Uv = Rect.Positive,
                    Color = new Color(1.0f, 1.0f, 1.0f),
                    Rotation = 0.0f,
                    Direction = Vector3f.Up,
                    Enabled = false,
                    ScreenScaleFactor = 1.0f
                });
            }
        }

        _bufferSizeDirty = true;
        Commit();
    }

    /// <summary>Set whether billboards are relative to the scene node. Default true.</summary>
    public void SetRelative(bool enable)
    {
        _relative = enable;
        Commit();
    }

    /// <summary>Set whether scene node scale affects billboards' size. Default true.</summary>
    public void SetScaled(bool enable)
    {
        _scaled = enable;
        Commit();
    }

    /// <summary>Set whether billboards are sorted by distance. Default false.</summary>
    public void SetSorted(bool enable)
    {
        _sorted = enable;
        Commit();
    }

    /// <summary>Set whether billboards have fixed size on screen (measured in pixels) regardless of distance to camera. Default false.</summary>
    public void SetFixedScreenSize(bool enable)
    {
        _fixedScreenSize = enable;
        Commit();
    }

    /// <summary>Set how the billboards should rotate in relation to the camera. Default is to follow camera rotation on all axes (FC_ROTATE_XYZ).</summary>
    public void SetFaceCameraMode(FaceCameraMode mode)
    {
        if (mode == _faceCameraMode)
            return;

        if (_faceCameraMode == FaceCameraMode.Direction || mode == FaceCameraMode.Direction ||
            _faceCameraMode == FaceCameraMode.AxisAngle || mode == FaceCameraMode.AxisAngle)
        {
            _faceCameraMode = mode;
            var batch = _batches[0];
            batch.GeometryType = _faceCameraMode switch
            {
                FaceCameraMode.Direction => GeometryType.DirectionalBillboard,
                FaceCameraMode.AxisAngle => GeometryType.StaticNoInstancing,
                _ => GeometryType.Billboard
            };
            _batches[0] = batch;
            _geometryTypeUpdate = true;
            _bufferSizeDirty = true;
            Commit();
        }
        else
        {
            _faceCameraMode = mode;
        }
    }

    /// <summary>Set minimal angle between billboard normal and look-at direction.</summary>
    public void SetMinAngle(float angle)
    {
        _minAngle = angle;
    }

    /// <summary>Set animation LOD bias.</summary>
    public void SetAnimationLodBias(float bias)
    {
        _animationLodBias = Math.Max(bias, 0.0f);
    }

    /// <summary>Mark for bounding box and vertex buffer update. Call after modifying the billboards.</summary>
    public void Commit()
    {
        MarkPositionsDirty();
    }

    /// <summary>Return material.</summary>
    public Material? GetMaterial() => _batches[0].Material;

    /// <summary>Return number of billboards.</summary>
    public uint GetNumBillboards() => (uint)_billboards.Count;

    /// <summary>Return all billboards.</summary>
    public List<Billboard> GetBillboards() => _billboards;

    /// <summary>Return billboard by index.</summary>
    public ref Billboard GetBillboard(uint index)
    {
        if (index < (uint)_billboards.Count)
            return ref CollectionsMarshal.AsSpan(_billboards)[(int)index];
        return ref Unsafe.NullRef<Billboard>();
    }

    /// <summary>Return whether billboards are relative to the scene node.</summary>
    public bool IsRelative() => _relative;

    /// <summary>Return whether scene node scale affects billboards' size.</summary>
    public bool IsScaled() => _scaled;

    /// <summary>Return whether billboards are sorted.</summary>
    public bool IsSorted() => _sorted;

    /// <summary>Return whether billboards are fixed screen size.</summary>
    public bool IsFixedScreenSize() => _fixedScreenSize;

    /// <summary>Return how the billboards rotate in relation to the camera.</summary>
    public FaceCameraMode GetFaceCameraMode() => _faceCameraMode;

    /// <summary>Return minimal angle between billboard normal and look-at direction.</summary>
    public float GetMinAngle() => _minAngle;

    /// <summary>Return animation LOD bias.</summary>
    public float GetAnimationLodBias() => _animationLodBias;

    /// <summary>Set material attribute.</summary>
    public void SetMaterialAttr(ResourceRef value)
    {
        var cache = GetSubsystem<ResourceCache>();
        SetMaterial(cache?.GetResourceOrNull<Material>(value.Name));
    }

    /// <summary>Set billboards attribute.</summary>
    public void SetBillboardsAttr(VariantVector value)
    {
        var index = 0;
        var numBillboards = index < value.Count ? value[index++].GetUInt() : 0u;
        SetNumBillboards(numBillboards);

        // Dealing with old billboard format
        if (value.Count == _billboards.Count * 6 + 1)
        {
            for (var i = 0; i < _billboards.Count && index < value.Count; ++i)
            {
                var billboard = _billboards[i];
                billboard.Position = value[index++].GetVector3();
                billboard.Size = value[index++].GetVector2();
                var uv = value[index++].GetVector4();
                billboard.Uv = new Rect(uv.X, uv.Y, uv.Z, uv.W);
                billboard.Color = value[index++].GetColor();
                billboard.Rotation = value[index++].GetFloat();
                billboard.Enabled = value[index++].GetBool();
                _billboards[i] = billboard;
            }
        }
        // New billboard format
        else
        {
            for (var i = 0; i < _billboards.Count && index < value.Count; ++i)
            {
                var billboard = _billboards[i];
                billboard.Position = value[index++].GetVector3();
                billboard.Size = value[index++].GetVector2();
                var uv = value[index++].GetVector4();
                billboard.Uv = new Rect(uv.X, uv.Y, uv.Z, uv.W);
                billboard.Color = value[index++].GetColor();
                billboard.Rotation = value[index++].GetFloat();
                billboard.Direction = value[index++].GetVector3();
                billboard.Enabled = value[index++].GetBool();
                _billboards[i] = billboard;
            }
        }

        Commit();
    }

    /// <summary>Return material attribute.</summary>
    public ResourceRef GetMaterialAttr() =>
        Resource.GetResourceRef(_batches[0].Material, new StringHash(nameof(Material)));

    /// <summary>Return billboards attribute.</summary>
    public VariantVector GetBillboardsAttr()
    {
        var ret = new VariantVector
        {
            Capacity = _billboards.Count * 7 + 1
        };
        ret.Add(new Variant((int)_billboards.Count));

        foreach (var billboard in _billboards)
        {
            ret.Add(new Variant(billboard.Position));
            ret.Add(new Variant(billboard.Size));
            ret.Add(new Variant(new Vector4f(billboard.Uv.Min.X, billboard.Uv.Min.Y, billboard.Uv.Max.X, billboard.Uv.Max.Y)));
            ret.Add(new Variant(billboard.Color));
            ret.Add(new Variant(billboard.Rotation));
            ret.Add(new Variant(billboard.Direction));
            ret.Add(new Variant(billboard.Enabled));
        }

        return ret;
    }

    /// <summary>Return FaceCameraMode enum value names.</summary>
    public static string[] GetFaceCameraModeNames() => FaceCameraModeNames;

    /// <summary>Recalculate the world-space bounding box.</summary>
    protected override void OnWorldBoundingBoxUpdate()
    {
        var enabledBillboards = 0;
        var worldTransform = Node.GetWorldTransform();
        var billboardTransform = _relative ? worldTransform : Matrix3x4.Identity;
        var billboardScale = _scaled ? worldTransform.Scale : Vector3f.One;
        var worldBox = new BoundingBox();

        for (var i = 0; i < _billboards.Count; ++i)
        {
            var billboard = _billboards[i];
            if (!billboard.Enabled)
                continue;

            var size = InvSqrtTwo * (billboard.Size.X * billboardScale.X + billboard.Size.Y * billboardScale.Y);
            if (_fixedScreenSize)
                size *= billboard.ScreenScaleFactor;

            var center = billboardTransform * billboard.Position;
            var edge = Vector3f.One * size;
            worldBox.Merge(new BoundingBox(center - edge, center + edge));

            ++enabledBillboards;
        }

        // Always merge the node's own position to ensure particle emitter updates continue when the relative mode is switched
        worldBox.Merge(Node.GetWorldPosition());

        SetWorldBoundingBox(worldBox);
    }

    /// <summary>Mark billboard vertex buffer to need an update.</summary>
    protected void MarkPositionsDirty()
    {
        if (Node is not null)
            OnMarkedDirty(Node);
        _bufferDirty = true;
    }

    /// <summary>Resize billboard vertex and index buffers.</summary>
    private void UpdateBufferSize()
    {
        var numBillboards = (uint)_billboards.Count;

        if (_vertexBuffer.GetVertexCount() < numBillboards * 4 || _geometryTypeUpdate ||
            _vertexBuffer.GetElementMask() == (uint)VertexMask.None)
        {
            _vertexBuffer.SetSize(numBillboards * 4, GetVertexBufferFormat(), true);
            _geometry.SetVertexBuffer(0, _vertexBuffer);
            _geometryTypeUpdate = false;
        }

        var wasLargeIndices = _indexBuffer.GetIndexSize() == 4;
        var largeIndices = wasLargeIndices || (numBillboards * 4 >= 65536);

        if (_indexBuffer.GetIndexCount() < numBillboards * 6 || wasLargeIndices != largeIndices)
            _indexBuffer.SetSize(numBillboards * 6, largeIndices);

        _bufferSizeDirty = false;
        _bufferDirty = true;
        _forceUpdate = true;

        if (numBillboards == 0)
            return;

        // Indices do not change for a given billboard capacity
        var indices = new uint[numBillboards * 6];
        uint vertexIndex = 0;
        var dest = 0;
        var remaining = numBillboards;
        while (remaining-- > 0)
        {
            indices[dest + 0] = vertexIndex;
            indices[dest + 1] = vertexIndex + 1;
            indices[dest + 2] = vertexIndex + 2;
            indices[dest + 3] = vertexIndex + 2;
            indices[dest + 4] = vertexIndex + 3;
            indices[dest + 5] = vertexIndex;

            dest += 6;
            vertexIndex += 4;
        }

        _indexBuffer.SetUnpackedData(indices);
        _indexBuffer.GetRawBuffer()?.ClearDataLost();
    }

    private void BuildDefaultVertexBuffer(uint enabledBillboards, Span<float> dest, Vector3f billboardScale)
    {
        var offset = 0;
        for (var i = 0; i < enabledBillboards; ++i)
        {
            var billboard = _billboards[_sortedBillboards[i]];

            var size = new Vector2f(billboard.Size.X * billboardScale.X, billboard.Size.Y * billboardScale.Y);
            var color = billboard.Color.ToUInt();
            if (_fixedScreenSize)
                size *= billboard.ScreenScaleFactor;

            MathDefs.SinCos(billboard.Rotation, out var sin, out var cos);
            var rotationMatrix00 = cos;
            var rotationMatrix01 = sin;
            var rotationMatrix10 = -sin;
            var rotationMatrix11 = cos;

            dest[offset + 0] = billboard.Position.X;
            dest[offset + 1] = billboard.Position.Y;
            dest[offset + 2] = billboard.Position.Z;
            MemoryMarshal.Write(MemoryMarshal.AsBytes(dest.Slice(offset + 3, 1)), in color);
            dest[offset + 4] = billboard.Uv.Min.X;
            dest[offset + 5] = billboard.Uv.Min.Y;
            dest[offset + 6] = -size.X * rotationMatrix00 + size.Y * rotationMatrix01;
            dest[offset + 7] = -size.X * rotationMatrix10 + size.Y * rotationMatrix11;

            dest[offset + 8] = billboard.Position.X;
            dest[offset + 9] = billboard.Position.Y;
            dest[offset + 10] = billboard.Position.Z;
            MemoryMarshal.Write(MemoryMarshal.AsBytes(dest.Slice(offset + 11, 1)), in color);
            dest[offset + 12] = billboard.Uv.Max.X;
            dest[offset + 13] = billboard.Uv.Min.Y;
            dest[offset + 14] = size.X * rotationMatrix00 + size.Y * rotationMatrix01;
            dest[offset + 15] = size.X * rotationMatrix10 + size.Y * rotationMatrix11;

            dest[offset + 16] = billboard.Position.X;
            dest[offset + 17] = billboard.Position.Y;
            dest[offset + 18] = billboard.Position.Z;
            MemoryMarshal.Write(MemoryMarshal.AsBytes(dest.Slice(offset + 19, 1)), in color);
            dest[offset + 20] = billboard.Uv.Max.X;
            dest[offset + 21] = billboard.Uv.Max.Y;
            dest[offset + 22] = size.X * rotationMatrix00 - size.Y * rotationMatrix01;
            dest[offset + 23] = size.X * rotationMatrix10 - size.Y * rotationMatrix11;

            dest[offset + 24] = billboard.Position.X;
            dest[offset + 25] = billboard.Position.Y;
            dest[offset + 26] = billboard.Position.Z;
            MemoryMarshal.Write(MemoryMarshal.AsBytes(dest.Slice(offset + 27, 1)), in color);
            dest[offset + 28] = billboard.Uv.Min.X;
            dest[offset + 29] = billboard.Uv.Max.Y;
            dest[offset + 30] = -size.X * rotationMatrix00 - size.Y * rotationMatrix01;
            dest[offset + 31] = -size.X * rotationMatrix10 - size.Y * rotationMatrix11;

            offset += 32;
        }
    }

    private void BuildDirectionVertexBuffer(uint enabledBillboards, Span<float> dest, Vector3f billboardScale)
    {
        var offset = 0;
        for (var i = 0; i < enabledBillboards; ++i)
        {
            var billboard = _billboards[_sortedBillboards[i]];

            var size = new Vector2f(billboard.Size.X * billboardScale.X, billboard.Size.Y * billboardScale.Y);
            var color = billboard.Color.ToUInt();
            if (_fixedScreenSize)
                size *= billboard.ScreenScaleFactor;

            MathDefs.SinCos(billboard.Rotation, out var sin, out var cos);
            var rot2D00 = cos;
            var rot2D01 = sin;
            var rot2D10 = -sin;
            var rot2D11 = cos;

            dest[offset + 0] = billboard.Position.X;
            dest[offset + 1] = billboard.Position.Y;
            dest[offset + 2] = billboard.Position.Z;
            dest[offset + 3] = billboard.Direction.X;
            dest[offset + 4] = billboard.Direction.Y;
            dest[offset + 5] = billboard.Direction.Z;
            MemoryMarshal.Write(MemoryMarshal.AsBytes(dest.Slice(offset + 6, 1)), in color);
            dest[offset + 7] = billboard.Uv.Min.X;
            dest[offset + 8] = billboard.Uv.Min.Y;
            dest[offset + 9] = -size.X * rot2D00 + size.Y * rot2D01;
            dest[offset + 10] = -size.X * rot2D10 + size.Y * rot2D11;

            dest[offset + 11] = billboard.Position.X;
            dest[offset + 12] = billboard.Position.Y;
            dest[offset + 13] = billboard.Position.Z;
            dest[offset + 14] = billboard.Direction.X;
            dest[offset + 15] = billboard.Direction.Y;
            dest[offset + 16] = billboard.Direction.Z;
            MemoryMarshal.Write(MemoryMarshal.AsBytes(dest.Slice(offset + 17, 1)), in color);
            dest[offset + 18] = billboard.Uv.Max.X;
            dest[offset + 19] = billboard.Uv.Min.Y;
            dest[offset + 20] = size.X * rot2D00 + size.Y * rot2D01;
            dest[offset + 21] = size.X * rot2D10 + size.Y * rot2D11;

            dest[offset + 22] = billboard.Position.X;
            dest[offset + 23] = billboard.Position.Y;
            dest[offset + 24] = billboard.Position.Z;
            dest[offset + 25] = billboard.Direction.X;
            dest[offset + 26] = billboard.Direction.Y;
            dest[offset + 27] = billboard.Direction.Z;
            MemoryMarshal.Write(MemoryMarshal.AsBytes(dest.Slice(offset + 28, 1)), in color);
            dest[offset + 29] = billboard.Uv.Max.X;
            dest[offset + 30] = billboard.Uv.Max.Y;
            dest[offset + 31] = size.X * rot2D00 - size.Y * rot2D01;
            dest[offset + 32] = size.X * rot2D10 - size.Y * rot2D11;

            dest[offset + 33] = billboard.Position.X;
            dest[offset + 34] = billboard.Position.Y;
            dest[offset + 35] = billboard.Position.Z;
            dest[offset + 36] = billboard.Direction.X;
            dest[offset + 37] = billboard.Direction.Y;
            dest[offset + 38] = billboard.Direction.Z;
            MemoryMarshal.Write(MemoryMarshal.AsBytes(dest.Slice(offset + 39, 1)), in color);
            dest[offset + 40] = billboard.Uv.Min.X;
            dest[offset + 41] = billboard.Uv.Max.Y;
            dest[offset + 42] = -size.X * rot2D00 - size.Y * rot2D01;
            dest[offset + 43] = -size.X * rot2D10 - size.Y * rot2D11;

            offset += 44;
        }
    }

    private void BuildAxisAngleVertexBuffer(uint enabledBillboards, Span<float> dest, Vector3f billboardScale)
    {
        var offset = 0;
        for (var i = 0; i < enabledBillboards; ++i)
        {
            var billboard = _billboards[_sortedBillboards[i]];

            var size = new Vector2f(billboard.Size.X * billboardScale.X, billboard.Size.Y * billboardScale.Y);
            var color = billboard.Color.ToUInt();
            if (_fixedScreenSize)
                size *= billboard.ScreenScaleFactor;

            MathDefs.SinCos(billboard.Rotation, out _, out _);
            var rot = new Matrix3(billboard.Rotation, billboard.Direction);

            WriteAxisAngleVertex(dest, ref offset, billboard.Position + rot * new Vector3f(-size.X, size.Y, 0.0f),
                rot.Column(2), color, new Vector2f(billboard.Uv.Min.X, billboard.Uv.Max.Y), rot.Column(0));
            WriteAxisAngleVertex(dest, ref offset, billboard.Position + rot * new Vector3f(size.X, size.Y, 0.0f),
                rot.Column(2), color, billboard.Uv.Max, rot.Column(0));
            WriteAxisAngleVertex(dest, ref offset, billboard.Position + rot * new Vector3f(size.X, -size.Y, 0.0f),
                rot.Column(2), color, new Vector2f(billboard.Uv.Max.X, billboard.Uv.Min.Y), rot.Column(0));
            WriteAxisAngleVertex(dest, ref offset, billboard.Position + rot * new Vector3f(-size.X, -size.Y, 0.0f),
                rot.Column(2), color, billboard.Uv.Min, rot.Column(0));
        }
    }

    private static void WriteAxisAngleVertex(Span<float> dest, ref int offset, Vector3f position, Vector3f normal,
        uint color, Vector2f uv, Vector3f tangent)
    {
        dest[offset++] = position.X;
        dest[offset++] = position.Y;
        dest[offset++] = position.Z;
        dest[offset++] = normal.X;
        dest[offset++] = normal.Y;
        dest[offset++] = normal.Z;
        MemoryMarshal.Write(MemoryMarshal.AsBytes(dest.Slice(offset, 1)), in color);
        offset++;
        dest[offset++] = uv.X;
        dest[offset++] = uv.Y;
        dest[offset++] = tangent.X;
        dest[offset++] = tangent.Y;
        dest[offset++] = tangent.Z;
        dest[offset++] = 1.0f;
    }

    /// <summary>Rewrite billboard vertex buffer.</summary>
    private void UpdateVertexBuffer(CommonFrameInfo frame)
    {
        // If using animation LOD, accumulate time and see if it is time to update
        if (_animationLodBias > 0.0f && GetLodDistance() > 0.0f)
        {
            _animationLodTimer += _animationLodBias * frame.TimeStep * AnimationLodBaseScale;
            if (_animationLodTimer >= GetLodDistance())
                _animationLodTimer %= GetLodDistance();
            else
            {
                // No LOD if immediate update forced
                if (!_forceUpdate)
                    return;
            }
        }

        var numBillboards = (uint)_billboards.Count;
        uint enabledBillboards = 0;
        var worldTransform = Node.GetWorldTransform();
        var billboardTransform = _relative ? worldTransform : Matrix3x4.Identity;
        var billboardScale = _scaled ? worldTransform.Scale : Vector3f.One;

        // First check number of enabled billboards
        for (var i = 0; i < numBillboards; ++i)
        {
            if (_billboards[i].Enabled)
                ++enabledBillboards;
        }

        _sortedBillboards.Clear();
        if (_sortedBillboards.Capacity < enabledBillboards)
            _sortedBillboards.Capacity = (int)enabledBillboards;

        // Then set initial sort order and distances
        for (var i = 0; i < numBillboards; ++i)
        {
            var billboard = _billboards[i];
            if (!billboard.Enabled)
                continue;

            if (_sorted)
            {
                billboard.SortDistance = frame.Camera!.GetDistanceSquared(billboardTransform * billboard.Position);
                _billboards[i] = billboard;
            }

            _sortedBillboards.Add(i);
        }

        _geometry.SetDrawRange(PrimitiveType.TriangleList, 0, enabledBillboards * 6, false);

        _bufferDirty = false;
        _forceUpdate = false;
        if (enabledBillboards == 0)
            return;

        if (_sorted)
        {
            _sortedBillboards.Sort((lhs, rhs) =>
                _billboards[rhs].SortDistance.CompareTo(_billboards[lhs].SortDistance));
            var worldPos = Node.GetWorldPosition();
            // Store the "last sorted position" now
            _previousOffset = worldPos - frame.Camera!.Node!.GetWorldPosition();
        }

        var mapped = _vertexBuffer.Map();
        if (mapped.IsEmpty)
            return;

        var dest = MemoryMarshal.Cast<byte, float>(mapped);

        if (_faceCameraMode == FaceCameraMode.Direction)
            BuildDirectionVertexBuffer(enabledBillboards, dest, billboardScale);
        else if (_faceCameraMode == FaceCameraMode.AxisAngle)
            BuildAxisAngleVertexBuffer(enabledBillboards, dest, billboardScale);
        else
            BuildDefaultVertexBuffer(enabledBillboards, dest, billboardScale);

        _vertexBuffer.Unmap();
        _vertexBuffer.GetRawBuffer()?.ClearDataLost();
    }

    /// <summary>Calculate billboard scale factors in fixed screen size mode.</summary>
    private void CalculateFixedScreenSize(CommonFrameInfo frame)
    {
        var invViewHeight = 1.0f / frame.ViewportSize.Y;
        var halfViewWorldSize = frame.Camera!.GetHalfViewSize();
        var scaleFactorChanged = false;

        if (!frame.Camera.IsOrthographic())
        {
            var viewProj = frame.Camera.GetProjection() * frame.Camera.GetView().Matrix4;
            var worldTransform = Node.GetWorldTransform();
            var billboardTransform = _relative ? worldTransform : Matrix3x4.Identity;

            for (var i = 0; i < _billboards.Count; ++i)
            {
                var billboard = _billboards[i];
                var projPos = viewProj * billboardTransform * billboard.Position.ToVector4(1.0f);
                var newScaleFactor = invViewHeight * halfViewWorldSize * projPos.W;
                if (newScaleFactor != billboard.ScreenScaleFactor)
                {
                    billboard.ScreenScaleFactor = newScaleFactor;
                    _billboards[i] = billboard;
                    scaleFactorChanged = true;
                }
            }
        }
        else
        {
            for (var i = 0; i < _billboards.Count; ++i)
            {
                var billboard = _billboards[i];
                var newScaleFactor = invViewHeight * halfViewWorldSize;
                if (newScaleFactor != billboard.ScreenScaleFactor)
                {
                    billboard.ScreenScaleFactor = newScaleFactor;
                    _billboards[i] = billboard;
                    scaleFactorChanged = true;
                }
            }
        }

        if (scaleFactorChanged)
        {
            _bufferDirty = true;
            _forceUpdate = true;
            _worldBoundingBoxDirty = true;
        }
    }
}
