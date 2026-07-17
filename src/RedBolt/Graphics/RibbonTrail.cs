using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace RedBolt;

public enum TrailType
{
    FaceCamera = 0,
    Bone
}

/// <summary>Trail is consisting of series of tails. Two connected points make a tail.</summary>
public struct TrailPoint
{
    /// <summary>Construct a zero-initialized TrailPoint.</summary>
    public TrailPoint()
    {
    }

    /// <summary>Construct a TrailPoint with the given position and forward vector.</summary>
    public TrailPoint(Vector3f position, Vector3f forward)
    {
        Position = position;
        Forward = forward;
    }

    /// <summary>Position.</summary>
    public Vector3f Position;
    /// <summary>Forward vector.</summary>
    public Vector3f Forward;
    /// <summary>Parent position. Trail bone type uses this.</summary>
    public Vector3f ParentPos;
    /// <summary>Elapsed length inside the trail.</summary>
    public float ElapsedLength;
    /// <summary>Next point index to make a tail (-1 if none). C++ uses TrailPoint*.</summary>
    public int NextIndex = -1;
    /// <summary>Tail time to live.</summary>
    public float Lifetime;
    /// <summary>Distance for sorting.</summary>
    public float SortDistance;
}

/// <summary>Drawable component that creates a tail.</summary>
public class RibbonTrail : Drawable
{
    private const uint MaxTailColumn = 16;

    /// <summary>Tails.</summary>
    protected readonly List<TrailPoint> _points = [];
    /// <summary>Tails sorted flag.</summary>
    protected bool _sorted;
    /// <summary>Animation LOD bias.</summary>
    protected float _animationLodBias = 1.0f;
    /// <summary>Animation LOD timer.</summary>
    protected float _animationLodTimer;
    /// <summary>Trail type.</summary>
    protected TrailType _trailType = TrailType.FaceCamera;
    /// <summary>Base velocity applied to the trail.</summary>
    protected Vector3f _baseVelocity;

    private readonly Geometry _geometry = new();
    private readonly VertexBuffer _vertexBuffer = new();
    private readonly IndexBuffer _indexBuffer = new();
    private readonly Matrix3x4[] _transforms = [Matrix3x4.Identity];
    private bool _bufferSizeDirty;
    private bool _bufferDirty = true;
    private Vector3f _previousPosition;
    private float _vertexDistance = 0.1f;
    private float _width = 0.2f;
    private uint _numPoints;
    private Color _startColor = Color.White;
    private Color _endColor = new(1.0f, 1.0f, 1.0f, 0.0f);
    private float _startScale = 1.0f;
    private float _endScale = 1.0f;
    private float _lastTimeStep;
    private float _lifetime = 1.0f;
    private uint _tailColumn = 1;
    private uint _lastUpdateFrameNumber = MathDefs.MaxUnsigned;
    private bool _needUpdate;
    private Vector3f _previousOffset;
    private readonly List<int> _sortedPoints = [];
    private bool _forceUpdate;
    private bool _emitting = true;
    private bool _updateInvisible;
    private TrailPoint _endTail;
    private float _startEndTailTime;

    public RibbonTrail()
        : base(DrawableFlags.Geometry)
    {
        _vertexBuffer.SetShadowed(true);
        _indexBuffer.SetShadowed(true);
        _geometry.SetVertexBuffer(0, _vertexBuffer);
        _geometry.SetIndexBuffer(_indexBuffer);

        _batches.Add(new SourceBatch
        {
            Geometry = _geometry,
            GeometryType = GeometryType.TrailFaceCamera,
            WorldTransform = _transforms,
            NumWorldTransforms = 1
        });
    }

    public static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<RibbonTrail>(ObjectCategory.Category_Geometry);
    }

    public override void ProcessRayQuery(RayOctreeQuery query, List<RayQueryResult> results)
    {
        if (query.Level < RayQueryLevel.RayTriangle)
        {
            base.ProcessRayQuery(query, results);
            return;
        }

        if (query.Ray.HitDistance(GetWorldBoundingBox()) >= query.MaxDistance)
            return;

        for (var i = 0; i < _points.Count - 1; ++i)
        {
            var center = (_points[i].Position + _points[i + 1].Position) * 0.5f;
            var scale = _width * Vector3f.One;
            var distanceAndNormal = query.Ray.HitDistanceAndNormal(new BoundingBox(center - scale, center + scale));
            if (distanceAndNormal.Distance < query.MaxDistance)
            {
                results.Add(new RayQueryResult
                {
                    Position = query.Ray.Origin + distanceAndNormal.Distance * query.Ray.Direction,
                    Normal = distanceAndNormal.Normal,
                    Distance = distanceAndNormal.Distance,
                    Drawable = this,
                    Node = Node,
                    SubObject = checked((uint)i)
                });
            }
        }
    }

    public override void OnSetEnabled()
    {
        base.OnSetEnabled();

        if (IsAttached)
            _previousPosition = Node.GetWorldPosition();

        var scene = Scene;
        if (scene is not null)
        {
            if (IsEnabledEffective)
                SubscribeToEvent<SceneEvents.ScenePostUpdateEvent>(scene, HandleScenePostUpdate);
            else
                UnsubscribeFromEvent<SceneEvents.ScenePostUpdateEvent>(scene);
        }
    }

    private void HandleScenePostUpdate(ref SceneEvents.ScenePostUpdateEvent eventData)
    {
        _lastTimeStep = eventData.TimeStep;

        if (_updateInvisible || _viewFrameNumber != _lastUpdateFrameNumber)
        {
            if (_points.Count < 3 && _viewFrameNumber - _lastUpdateFrameNumber > 1)
            {
                _previousPosition = Node.GetWorldPosition();
                _points.Clear();
            }

            _lastUpdateFrameNumber = _viewFrameNumber;
            _needUpdate = true;
            MarkForUpdate();
        }
    }

    public override void Update(CommonFrameInfo frame)
    {
        base.Update(frame);

        if (!_needUpdate)
            return;

        UpdateTail(frame.TimeStep);
        if (IsAttached)
            OnMarkedDirty(Node);
        _needUpdate = false;
    }

    private void UpdateTail(float timeStep)
    {
        if (_baseVelocity != Vector3f.Zero)
        {
            for (var i = 0; i < _points.Count; ++i)
            {
                var point = _points[i];
                point.Position += _baseVelocity * timeStep;
                _points[i] = point;
            }
            _previousPosition += _baseVelocity * timeStep;
        }

        var worldPosition = Node.GetWorldPosition();
        var path = (_previousPosition - worldPosition).Length;

        var expiredIndex = -1;
        if (_points.Count > 0)
        {
            for (var i = 0; i < _points.Count - 1; ++i)
            {
                var point = _points[i];
                point.Lifetime += _lastTimeStep;
                _points[i] = point;

                if (point.Lifetime > _lifetime)
                    expiredIndex = i;
            }
        }

        if (expiredIndex != -1)
        {
            _points.RemoveRange(0, expiredIndex + 1);

            if (_points.Count > 1)
            {
                _endTail.Position = _points[0].Position;
                _startEndTailTime = _points[0].Lifetime;
            }
        }

        if (_points.Count == 0)
        {
            _previousPosition = worldPosition;
        }
        else if (_points.Count == 1)
        {
            _points.RemoveAt(0);
            _previousPosition = worldPosition;
        }
        else if (_points.Count > 1 && _points[0].Lifetime < _lifetime)
        {
            var step = MathDefs.SmoothStep(_startEndTailTime, _lifetime, _points[0].Lifetime);
            var p0 = _points[0];
            p0.Position = new Vector3f(
                MathDefs.Lerp(_endTail.Position.X, _points[1].Position.X, step),
                MathDefs.Lerp(_endTail.Position.Y, _points[1].Position.Y, step),
                MathDefs.Lerp(_endTail.Position.Z, _points[1].Position.Z, step));
            _points[0] = p0;
            _bufferDirty = true;
        }

        if (_points.Count == 0 && path > MathDefs.LargeEpsilon && _emitting)
        {
            var forwardMotion = (_previousPosition - worldPosition).Normalized;

            var startPoint = new TrailPoint(_previousPosition, forwardMotion);
            var nextPoint = new TrailPoint(worldPosition, forwardMotion);

            if (Node.Parent is not null)
            {
                startPoint.ParentPos = Node.Parent.GetWorldPosition();
                nextPoint.ParentPos = Node.Parent.GetWorldPosition();
            }

            _points.Add(startPoint);
            _points.Add(nextPoint);

            _endTail.Position = startPoint.Position;
            _startEndTailTime = 0.0f;
        }

        if (_points.Count > 1 && _emitting)
        {
            var forwardMotion = (_previousPosition - worldPosition).Normalized;

            if (path > _vertexDistance)
            {
                var newPoint = new TrailPoint(worldPosition, forwardMotion);
                if (Node.Parent is not null)
                    newPoint.ParentPos = Node.Parent.GetWorldPosition();

                _points.Add(newPoint);
                _previousPosition = worldPosition;
            }
            else
            {
                var back = _points[^1];
                back.Position = worldPosition;
                if (forwardMotion != Vector3f.Zero)
                    back.Forward = forwardMotion;
                _points[^1] = back;
            }
        }

        if ((uint)_points.Count != _numPoints)
            _bufferSizeDirty = true;
    }

    public void SetEndScale(float endScale) { _endScale = endScale; Commit(); }
    public void SetStartScale(float startScale) { _startScale = startScale; Commit(); }

    public void SetEmitting(bool emitting)
    {
        if (emitting == _emitting)
            return;

        _emitting = emitting;

        if (emitting && _points.Count > 0)
        {
            _points.Clear();
            _bufferSizeDirty = true;
        }

        if (IsAttached)
            OnMarkedDirty(Node);
    }

    public void SetTailColumn(uint tailColumn)
    {
        if (tailColumn > MaxTailColumn)
        {
            Log.Logger.LogWarning("Max ribbon trail tail column is {Column}", MaxTailColumn);
            _tailColumn = MaxTailColumn;
        }
        else if (tailColumn < 1)
            _tailColumn = 1;
        else
            _tailColumn = tailColumn;

        if (IsAttached)
            OnMarkedDirty(Node);
        _bufferSizeDirty = true;
    }

    public override void UpdateBatches(CommonFrameInfo frame)
    {
        SetDistance(frame.Camera!.GetDistance(GetWorldBoundingBox().Center));
        var batch = _batches[0];
        batch.Distance = GetDistance();
        _batches[0] = batch;

        var scale = GetWorldBoundingBox().Size.DotProduct(GraphicsDefs.DotScale);
        if (scale > MathDefs.Epsilon)
            SetLodDistance(frame.Camera.GetLodDistance(GetDistance(), scale, LodBias));
        else
            SetLodDistance(0.0f);

        var worldPos = Node.GetWorldPosition();
        var offset = worldPos - frame.Camera.Node!.GetWorldPosition();
        if (_sorted && offset != _previousOffset)
        {
            _bufferDirty = true;
            _previousOffset = offset;
        }

        if (_bufferSizeDirty || (_indexBuffer.GetRawBuffer()?.IsDataLost() ?? false))
            RequestUpdateBatchesDelayed(frame);
    }

    public override void UpdateBatchesDelayed(CommonFrameInfo frame) => UpdateBufferSize();

    public override void UpdateGeometry(CommonFrameInfo frame)
    {
        if (_bufferDirty || (_vertexBuffer.GetRawBuffer()?.IsDataLost() ?? false))
            UpdateVertexBuffer(frame);
    }

    public override UpdateGeometryType GetUpdateGeometryType()
    {
        if (_bufferDirty || (_vertexBuffer.GetRawBuffer()?.IsDataLost() ?? false))
            return UpdateGeometryType.MainThread;
        return UpdateGeometryType.None;
    }

    public void SetMaterial(Material? material)
    {
        var batch = _batches[0];
        batch.Material = material;
        _batches[0] = batch;
    }

    protected internal override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);

        if (scene is not null && IsEnabledEffective)
            SubscribeToEvent<SceneEvents.ScenePostUpdateEvent>(scene, HandleScenePostUpdate);
        else if (scene is null)
            UnsubscribeFromEvent<SceneEvents.ScenePostUpdateEvent>();
    }

    protected override void OnWorldBoundingBoxUpdate()
    {
        var worldBox = default(BoundingBox);

        for (var i = 0; i < _points.Count; ++i)
        {
            var p = _points[i].Position;
            var scale = _width * Vector3f.One;
            worldBox.Merge(new BoundingBox(p - scale, p + scale));
        }

        if (_points.Count == 0)
            worldBox.Merge(Node.GetWorldPosition());

        SetWorldBoundingBox(worldBox);
    }

    public override void PostUpdateWorldOrigin(IntVector3 oldOrigin, IntVector3 newOrigin, IntVector3 delta)
    {
        var offset = delta.ToVector3();
        for (var i = 0; i < _points.Count; ++i)
        {
            var point = _points[i];
            point.Position -= offset;
            point.ParentPos -= offset;
            _points[i] = point;
        }
        _previousPosition -= offset;
        _previousOffset -= offset;
        _endTail.Position -= offset;
        _endTail.ParentPos -= offset;
        _forceUpdate = true;
    }

    private void UpdateBufferSize()
    {
        _numPoints = (uint)_points.Count;

        var indexPerSegment = 6 + (_tailColumn - 1) * 6;
        var vertexPerSegment = 4 + (_tailColumn - 1) * 2;

        uint mask;
        var batch = _batches[0];
        if (_trailType == TrailType.FaceCamera)
        {
            batch.GeometryType = GeometryType.TrailFaceCamera;
            mask = (uint)(VertexMask.Position | VertexMask.Color | VertexMask.TexCoord1 | VertexMask.Tangent);
        }
        else
        {
            batch.GeometryType = GeometryType.TrailBone;
            mask = (uint)(VertexMask.Position | VertexMask.Normal | VertexMask.Color | VertexMask.TexCoord1 | VertexMask.Tangent);
        }

        _bufferSizeDirty = false;
        _bufferDirty = true;
        _forceUpdate = true;

        _indexBuffer.SetDebugName("RibbonTrail Geometry");
        _vertexBuffer.SetDebugName("RibbonTrail Geometry");

        if (_numPoints < 2)
        {
            _indexBuffer.SetSize(0, false);
            _vertexBuffer.SetSize(0, mask, true);
            _geometry.SetDrawRange(PrimitiveType.TriangleList, 0, 0, false);
            batch.Geometry = _geometry;
            _batches[0] = batch;
            return;
        }

        _indexBuffer.SetSize((_numPoints - 1) * indexPerSegment, false);
        _vertexBuffer.SetSize(_numPoints * vertexPerSegment, mask, true);
        _geometry.SetDrawRange(PrimitiveType.TriangleList, 0, (_numPoints - 1) * indexPerSegment, false);
        batch.Geometry = _geometry;
        _batches[0] = batch;

        var indices = new uint[(_numPoints - 1) * indexPerSegment];
        uint vertexIndex = 0;
        var dest = 0;
        var stripsLen = _numPoints - 1;

        while (stripsLen-- > 0)
        {
            indices[dest + 0] = vertexIndex;
            indices[dest + 1] = vertexIndex + 2;
            indices[dest + 2] = vertexIndex + 1;
            indices[dest + 3] = vertexIndex + 1;
            indices[dest + 4] = vertexIndex + 2;
            indices[dest + 5] = vertexIndex + 3;
            dest += 6;
            vertexIndex += 2;

            for (var i = 0u; i < (_tailColumn - 1); ++i)
            {
                indices[dest + 0] = vertexIndex;
                indices[dest + 1] = vertexIndex + 2;
                indices[dest + 2] = vertexIndex + 1;
                indices[dest + 3] = vertexIndex + 1;
                indices[dest + 4] = vertexIndex + 2;
                indices[dest + 5] = vertexIndex + 3;
                dest += 6;
                vertexIndex += 2;
            }

            vertexIndex += 2;
        }

        _indexBuffer.SetUnpackedData(indices);
        _indexBuffer.GetRawBuffer()?.ClearDataLost();
    }

    private void UpdateVertexBuffer(CommonFrameInfo frame)
    {
        if (_animationLodBias > 0.0f && GetLodDistance() > 0.0f)
        {
            _animationLodTimer += _animationLodBias * frame.TimeStep * AnimationLodBaseScale;
            if (_animationLodTimer >= GetLodDistance())
                _animationLodTimer = MathF.IEEERemainder(_animationLodTimer, GetLodDistance());
            else
            {
                if (!_forceUpdate)
                    return;
            }
        }

        if (_numPoints < 2)
            return;

        _sortedPoints.Clear();
        for (var i = 0; i < _numPoints; ++i)
        {
            var point = _points[i];
            if (_sorted)
            {
                point.SortDistance = frame.Camera!.GetDistanceSquared(point.Position);
                _points[i] = point;
            }
            _sortedPoints.Add(i);
        }

        if (_sorted)
            _sortedPoints.Sort((lhs, rhs) => _points[rhs].SortDistance.CompareTo(_points[lhs].SortDistance));

        var trailLength = 0.0f;
        for (var i = 0; i < _numPoints; ++i)
        {
            var length = i == 0 ? 0.0f : (_points[i].Position - _points[i - 1].Position).Length;
            trailLength += length;
            var point = _points[i];
            point.ElapsedLength = trailLength;
            point.NextIndex = i < _numPoints - 1 ? i + 1 : -1;
            _points[i] = point;
        }

        _bufferDirty = false;
        _forceUpdate = false;

        var mapped = _vertexBuffer.Map();
        if (mapped.IsEmpty)
            return;
        var dest = MemoryMarshal.Cast<byte, float>(mapped);
        var offset = 0;

        if (_trailType == TrailType.FaceCamera)
        {
            for (var i = 0; i < _numPoints; ++i)
            {
                var pointIndex = _sortedPoints[i];
                if (pointIndex == _points.Count - 1)
                    continue;
                var point = _points[pointIndex];
                if (point.NextIndex < 0)
                    continue;
                var next = _points[point.NextIndex];

                var factor = MathDefs.SmoothStep(0.0f, trailLength, point.ElapsedLength);
                var c = _endColor.Lerp(_startColor, factor).ToUInt();
                var width = MathDefs.Lerp(_width * _endScale, _width * _startScale, factor);

                var nextFactor = MathDefs.SmoothStep(0.0f, trailLength, next.ElapsedLength);
                var nextC = _endColor.Lerp(_startColor, nextFactor).ToUInt();
                var nextWidth = MathDefs.Lerp(_width * _endScale, _width * _startScale, nextFactor);

                WriteFaceCameraRow(dest, ref offset, point, c, factor, 0.0f, width, next, nextC, nextFactor, 0.0f, nextWidth);

                for (var j = 0u; j < (_tailColumn - 1); ++j)
                {
                    var elapsed = 1.0f / _tailColumn * (j + 1);
                    var midWidth = width - elapsed * 2.0f * width;
                    var nextMidWidth = nextWidth - elapsed * 2.0f * nextWidth;
                    WriteFaceCameraRow(dest, ref offset, point, c, factor, elapsed, midWidth, next, nextC, nextFactor, elapsed, nextMidWidth);
                }

                WriteFaceCameraRow(dest, ref offset, point, c, factor, 1.0f, -width, next, nextC, nextFactor, 1.0f, -nextWidth);
            }
        }
        else if (_trailType == TrailType.Bone)
        {
            for (var i = 0; i < _numPoints; ++i)
            {
                var pointIndex = _sortedPoints[i];
                if (pointIndex == _points.Count - 1)
                    continue;
                var point = _points[pointIndex];
                if (point.NextIndex < 0)
                    continue;
                var next = _points[point.NextIndex];

                var factor = MathDefs.SmoothStep(0.0f, trailLength, point.ElapsedLength);
                var c = _endColor.Lerp(_startColor, factor).ToUInt();
                var rightScale = MathDefs.Lerp(_endScale, _startScale, factor);
                var shift = (rightScale - 1.0f) / 2.0f;
                var leftScale = 0.0f - shift;

                var nextFactor = MathDefs.SmoothStep(0.0f, trailLength, next.ElapsedLength);
                var nextC = _endColor.Lerp(_startColor, nextFactor).ToUInt();
                var nextRightScale = MathDefs.Lerp(_endScale, _startScale, nextFactor);
                var nextShift = (nextRightScale - 1.0f) / 2.0f;
                var nextLeftScale = 0.0f - nextShift;

                WriteBoneRow(dest, ref offset, point, c, factor, 0.0f, leftScale, next, nextC, nextFactor, 0.0f, nextLeftScale);

                for (var j = 0u; j < (_tailColumn - 1); ++j)
                {
                    var elapsed = 1.0f / _tailColumn * (j + 1);
                    WriteBoneRow(dest, ref offset, point, c, factor, elapsed, MathDefs.Lerp(leftScale, rightScale, elapsed),
                        next, nextC, nextFactor, elapsed, MathDefs.Lerp(nextLeftScale, nextRightScale, elapsed));
                }

                WriteBoneRow(dest, ref offset, point, c, factor, 1.0f, rightScale, next, nextC, nextFactor, 1.0f, nextRightScale);
            }
        }

        _vertexBuffer.Unmap();
        _vertexBuffer.GetRawBuffer()?.ClearDataLost();
    }

    private static void WriteFaceCameraRow(Span<float> dest, ref int offset, TrailPoint point, uint c, float factor, float v, float width,
        TrailPoint next, uint nextC, float nextFactor, float nextV, float nextWidth)
    {
        dest[offset++] = point.Position.X;
        dest[offset++] = point.Position.Y;
        dest[offset++] = point.Position.Z;
        MemoryMarshal.Write(MemoryMarshal.AsBytes(dest.Slice(offset, 1)), in c);
        offset++;
        dest[offset++] = factor;
        dest[offset++] = v;
        dest[offset++] = point.Forward.X;
        dest[offset++] = point.Forward.Y;
        dest[offset++] = point.Forward.Z;
        dest[offset++] = width;

        dest[offset++] = next.Position.X;
        dest[offset++] = next.Position.Y;
        dest[offset++] = next.Position.Z;
        MemoryMarshal.Write(MemoryMarshal.AsBytes(dest.Slice(offset, 1)), in nextC);
        offset++;
        dest[offset++] = nextFactor;
        dest[offset++] = nextV;
        dest[offset++] = next.Forward.X;
        dest[offset++] = next.Forward.Y;
        dest[offset++] = next.Forward.Z;
        dest[offset++] = nextWidth;
    }

    private static void WriteBoneRow(Span<float> dest, ref int offset, TrailPoint point, uint c, float factor, float v, float scale,
        TrailPoint next, uint nextC, float nextFactor, float nextV, float nextScale)
    {
        dest[offset++] = point.Position.X;
        dest[offset++] = point.Position.Y;
        dest[offset++] = point.Position.Z;
        dest[offset++] = point.Forward.X;
        dest[offset++] = point.Forward.Y;
        dest[offset++] = point.Forward.Z;
        MemoryMarshal.Write(MemoryMarshal.AsBytes(dest.Slice(offset, 1)), in c);
        offset++;
        dest[offset++] = factor;
        dest[offset++] = v;
        dest[offset++] = point.ParentPos.X;
        dest[offset++] = point.ParentPos.Y;
        dest[offset++] = point.ParentPos.Z;
        dest[offset++] = scale;

        dest[offset++] = next.Position.X;
        dest[offset++] = next.Position.Y;
        dest[offset++] = next.Position.Z;
        dest[offset++] = next.Forward.X;
        dest[offset++] = next.Forward.Y;
        dest[offset++] = next.Forward.Z;
        MemoryMarshal.Write(MemoryMarshal.AsBytes(dest.Slice(offset, 1)), in nextC);
        offset++;
        dest[offset++] = nextFactor;
        dest[offset++] = nextV;
        dest[offset++] = next.ParentPos.X;
        dest[offset++] = next.ParentPos.Y;
        dest[offset++] = next.ParentPos.Z;
        dest[offset++] = nextScale;
    }

    public void SetLifetime(float time) { _lifetime = time; Commit(); }
    public void SetVertexDistance(float length) { _vertexDistance = length; Commit(); }
    public void SetEndColor(Color color) { _endColor = color; Commit(); }
    public void SetStartColor(Color color) { _startColor = color; Commit(); }
    public void SetSorted(bool enable) { _sorted = enable; Commit(); }

    public void SetTrailType(TrailType type)
    {
        if (_trailType == type)
            return;

        if (type == TrailType.Bone && (Node.Parent is null || ReferenceEquals(Node.Parent, Node.Scene)))
        {
            Log.Logger.LogWarning("No parent node found, revert back to Face Camera type");
            return;
        }

        _trailType = type;
        if (IsAttached)
            OnMarkedDirty(Node);
        _bufferSizeDirty = true;
    }

    public void SetBaseVelocity(Vector3f baseVelocity) => _baseVelocity = baseVelocity;

    public void SetMaterialAttr(ResourceRef value)
    {
        var cache = GetSubsystem<ResourceCache>();
        SetMaterial(cache?.GetResourceOrNull<Material>(value.Name));
        Commit();
    }

    public void SetWidth(float width) { _width = width; Commit(); }
    public void SetAnimationLodBias(float bias) => _animationLodBias = Math.Max(bias, 0.0f);
    public void SetUpdateInvisible(bool enable) => _updateInvisible = enable;
    public void Commit() => MarkPositionsDirty();

    protected void MarkPositionsDirty()
    {
        if (IsAttached)
            OnMarkedDirty(Node);
        _bufferDirty = true;
    }

    public Material? GetMaterial() => _batches[0].Material;
    public ResourceRef GetMaterialAttr() => Resource.GetResourceRef(_batches[0].Material, new StringHash(nameof(Material)));
    public float GetVertexDistance() => _vertexDistance;
    public float GetWidth() => _width;
    public Color GetStartColor() => _startColor;
    public Color GetEndColor() => _endColor;
    public float GetStartScale() => _startScale;
    public float GetEndScale() => _endScale;
    public bool IsSorted() => _sorted;
    public float GetLifetime() => _lifetime;
    public float GetAnimationLodBias() => _animationLodBias;
    public TrailType GetTrailType() => _trailType;
    public Vector3f GetBaseVelocity() => _baseVelocity;
    public uint GetTailColumn() => _tailColumn;
    public bool IsEmitting() => _emitting;
    public bool GetUpdateInvisible() => _updateInvisible;
    public int GetNumPoints() => _points.Count;
}
