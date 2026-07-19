using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace RedBolt;

/// <summary>Animated model component.</summary>
public class AnimatedModel : StaticModel
{
    private bool _animationDirty;
    private bool _morphsDirty;
    private bool _skinningDirty = true;
    private bool _boneBoundingBoxDirty = true;
    private readonly Skeleton _skeleton = new();
    private readonly List<ModelAnimationOutput> _skeletonData = [];
    private WeakReference<AnimationStateSource>? _animationStateSource;
    private SoftwareModelAnimator? _modelAnimator;
    private readonly List<ModelMorph> _morphs = [];
    private Matrix3x4[] _skinMatrices = [];
    private readonly List<List<uint>> _geometryBoneMappings = [];
    private readonly List<Matrix3x4[]> _geometrySkinMatrices = [];
    private readonly List<List<(int GeometryIndex, int MatrixIndex)>> _geometrySkinMatrixPtrs = [];
    private BoundingBox _boneBoundingBox;
    private readonly VectorBuffer _attrBuffer = new();
    private uint _animationLodFrameNumber;
    private float _animationLodBias = 1.0f;
    private float _animationLodTimer = -1.0f;
    private float _animationLodDistance;
    private bool _updateInvisible;
    private bool _softwareSkinning;
    private uint _numSoftwareSkinningBones = 4;
    private bool _isMaster = true;
    private bool _assignBonesPending;
    private bool _forceAnimationUpdate;

    /// <summary>Construct.</summary>
    public AnimatedModel()
    {
        UpdateSoftwareSkinningState();
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        // When being destroyed, remove the bone hierarchy if appropriate (last AnimatedModel in the node)
        var rootBone = _skeleton.GetRootBone();
        if (rootBone?.Node is { } rootBoneNode)
        {
            var parent = rootBoneNode.Parent;
            if (parent is not null && parent.GetComponent<AnimatedModel>() is null)
                RemoveRootBone();
        }

        if (_model is not null)
            _model.ReloadFinished -= HandleModelReloadFinished;
        _attrBuffer.Clear();
        _geometrySkinMatrixPtrs.Clear();
        _geometrySkinMatrices.Clear();
        _geometryBoneMappings.Clear();
        _skinMatrices = [];
        _morphs.Clear();
        _modelAnimator?.Dispose();
        _modelAnimator = null;
        _animationStateSource = null;
        _skeletonData.Clear();
        _skeleton.Dispose();
        base.Dispose();
    }

    /// <summary>Register object factory. Drawable must be registered first.</summary>
    public static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<AnimatedModel>(ObjectCategory.Category_Geometry);
    }

    /// <summary>Apply attribute changes that can not be applied immediately. Called after scene load or a network update.</summary>
    public override void ApplyAttributes()
    {
        if (_assignBonesPending)
            AssignBoneNodes();
    }

    /// <summary>Process raycast with custom transform.</summary>
    public void ProcessCustomRayQuery(RayOctreeQuery query, BoundingBox worldBoundingBox,
        Matrix3x4 worldTransform, ReadOnlySpan<Matrix3x4> boneWorldTransforms, List<RayQueryResult> results)
    {
        var level = query.Level;
        if (level < RayQueryLevel.RayTriangle || _skeleton.GetNumBones() == 0)
        {
            base.ProcessCustomRayQuery(query, worldBoundingBox, worldTransform, results);
            return;
        }

        if (query.Ray.HitDistance(worldBoundingBox) >= query.MaxDistance)
            return;

        var bones = _skeleton.GetBones();

        for (var i = 0; i < bones.Count; ++i)
        {
            var bone = bones[i];
            if (bone.Node is null)
                continue;

            float distance;

            var transform = i < boneWorldTransforms.Length ? boneWorldTransforms[i] : bone.Node.GetWorldTransform();

            if ((bone.CollisionMask & BoneCollisionShape.Box) != 0)
            {
                var box = bone.BoundingBox;
                var distanceAndNormal = query.Ray.HitDistanceAndNormal(box.Transformed(transform));
                distance = distanceAndNormal.Distance;
                if (distance >= query.MaxDistance)
                    continue;
                if (level != RayQueryLevel.RayAabb)
                {
                    var inverse = transform.Inverse;
                    var localRay = query.Ray.Transformed(inverse);
                    distance = localRay.HitDistance(box);
                    if (distance >= query.MaxDistance)
                        continue;
                }
            }
            else if ((bone.CollisionMask & BoneCollisionShape.Sphere) != 0)
            {
                var boneSphere = new Sphere(transform.Translation, bone.Radius);
                distance = query.Ray.HitDistance(boneSphere);
                if (distance >= query.MaxDistance)
                    continue;
            }
            else
                continue;

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

    /// <summary>Process octree raycast. May be called from a worker thread.</summary>
    public override void ProcessRayQuery(RayOctreeQuery query, List<RayQueryResult> results)
    {
        ProcessCustomRayQuery(query, GetWorldBoundingBox(), Node.GetWorldTransform(), [], results);
    }

    /// <summary>Set model and create bone nodes.</summary>
    public void SetModel(Model? model, bool createBones)
    {
        if (ReferenceEquals(GetModel(), model))
            return;

        if (!IsAttached)
        {
            Log.GetLogger().LogError("Can not set model while model component is not attached to a scene node");
            return;
        }

        if (_model is not null)
            _model.ReloadFinished -= HandleModelReloadFinished;

        _model = model;
        _morphs.Clear();

        if (model is null)
        {
            RemoveRootBone();
            SetNumGeometries(0);
            _geometryBoneMappings.Clear();
            _modelAnimator?.Dispose();
            _modelAnimator = null;
            _skeletonData.Clear();
            SetBoundingBox(default);
            SetSkeleton(new Skeleton(), false);
            return;
        }

        model.ReloadFinished += HandleModelReloadFinished;

        SetNumGeometries(model.GetNumGeometries());
        var geometries = model.GetGeometries();
        for (var i = 0; i < geometries.Count; ++i)
        {
            _geometries[i].Clear();
            _geometries[i].AddRange(geometries[i]);
            _geometryData[i] = new StaticModelGeometryData { Center = model.GetGeometryCenter((uint)i) };
        }

        _geometryBoneMappings.Clear();
        foreach (var sourceMapping in model.GetGeometryBoneMappings())
            _geometryBoneMappings.Add([.. sourceMapping]);

        _modelAnimator?.Dispose();
        _modelAnimator = null;

        foreach (var sourceMorph in model.GetMorphs())
        {
            var morph = new ModelMorph
            {
                Name = sourceMorph.Name,
                NameHash = sourceMorph.NameHash,
                Weight = sourceMorph.Weight
            };
            foreach (var pair in sourceMorph.Buffers)
            {
                var sourceBuffer = pair.Value;
                morph.Buffers[pair.Key] = new VertexBufferMorph
                {
                    ElementMask = sourceBuffer.ElementMask,
                    VertexCount = sourceBuffer.VertexCount,
                    DataSize = sourceBuffer.DataSize,
                    MorphData = sourceBuffer.MorphData
                };
            }
            _morphs.Add(morph);
        }

        SetBoundingBox(model.GetBoundingBox());
        _boneBoundingBox = GetBoundingBox();
        _boneBoundingBoxDirty = true;
        SetSkeleton(model.GetSkeleton(), createBones);
        ResetLodLevels();
        _skinMatrices = new Matrix3x4[_skeleton.GetNumBones()];
        ResizeList(_skeletonData, checked((int)_skeleton.GetNumBones()));
        SetGeometryBoneMappings();
        UpdateSoftwareSkinningState();

        for (var i = 0; i < _batches.Count; ++i)
        {
            var batch = _batches[i];
            if (_skinMatrices.Length != 0 && !_softwareSkinning)
            {
                batch.GeometryType = GeometryType.Skinned;
                if (_geometrySkinMatrices.Count != 0 && _geometrySkinMatrices[i].Length != 0)
                    batch.WorldTransform = _geometrySkinMatrices[i];
                else
                    batch.WorldTransform = _skinMatrices;
                batch.NumWorldTransforms = checked((uint)batch.WorldTransform.Length);
            }
            else if (_softwareSkinning)
            {
                batch.GeometryType = GeometryType.Static;
                batch.WorldTransform = [Matrix3x4.Identity];
                batch.NumWorldTransforms = 1;
            }
            else
            {
                batch.GeometryType = GeometryType.Static;
                batch.WorldTransform = [Node.GetWorldTransform()];
                batch.NumWorldTransforms = 1;
            }
            _batches[i] = batch;
        }

        if (_softwareSkinning)
            CloneGeometries();
    }

    /// <summary>Set model and create bone nodes.</summary>
    public override void SetModel(Model? model) => SetModel(model, true);

    /// <summary>Set animation LOD bias.</summary>
    public void SetAnimationLodBias(float bias)
    {
        _animationLodBias = Math.Max(bias, 0.0f);
    }

    /// <summary>Set whether to update animation and the bounding box when not visible.</summary>
    public void SetUpdateInvisible(bool enable)
    {
        _updateInvisible = enable;
    }

    /// <summary>Set vertex morph weight by index.</summary>
    public void SetMorphWeight(uint index, float weight)
    {
        if (index >= _morphs.Count)
            return;

        // If morph vertex buffers have not been created yet, create now
        if (weight != 0.0f && _modelAnimator is null)
            CloneGeometries();

        if (weight != _morphs[(int)index].Weight)
        {
            _morphs[(int)index].Weight = weight;

            // For a master model, set the same morph weight on non-master models
            if (_isMaster)
            {
                var models = Node.GetComponents<AnimatedModel>();

                // Indexing might not be the same, so use the name hash instead
                for (var i = 1; i < models.Count; ++i)
                {
                    if (!models[i]._isMaster)
                        models[i].SetMorphWeight(_morphs[(int)index].NameHash, weight);
                }
            }

            MarkMorphsDirty();
        }
    }

    /// <summary>Set vertex morph weight by name.</summary>
    public void SetMorphWeight(string name, float weight)
    {
        for (var i = 0; i < _morphs.Count; ++i)
        {
            if (_morphs[i].Name == name)
            {
                SetMorphWeight((uint)i, weight);
                return;
            }
        }
    }

    /// <summary>Set vertex morph weight by name hash.</summary>
    public void SetMorphWeight(StringHash nameHash, float weight)
    {
        for (var i = 0; i < _morphs.Count; ++i)
        {
            if (_morphs[i].NameHash == nameHash)
            {
                SetMorphWeight((uint)i, weight);
                return;
            }
        }
    }

    /// <summary>Reset all vertex morphs to zero.</summary>
    public void ResetMorphWeights()
    {
        foreach (var morph in _morphs)
            morph.Weight = 0.0f;

        // For a master model, reset weights on non-master models
        if (_isMaster)
        {
            var models = Node.GetComponents<AnimatedModel>();

            for (var i = 1; i < models.Count; ++i)
            {
                if (!models[i]._isMaster)
                    models[i].ResetMorphWeights();
            }
        }

        MarkMorphsDirty();
    }

    /// <summary>Reset node transforms of skeleton bones.</summary>
    public void ResetBones()
    {
        _skeleton.Reset();
        MarkAnimationDirty();
    }

    /// <summary>Apply all animation states to nodes.</summary>
    public void ApplyAnimation()
    {
        // Reset skeleton, apply all animations, calculate bones' bounding box. Make sure this is only done for the master model
        // (first AnimatedModel in a node)
        if (_isMaster)
        {
            InitializeLocalBoneTransforms(false);
            CalculateAnimations();
            CalculateLocalBoundingBox();
            ApplyBoneTransformsToNodes();
        }
    }

    /// <summary>Update animation before octree reinsertion.</summary>
    public override void Update(CommonFrameInfo frame)
    {
        if (!PrepareForThreadedUpdate(frame.Camera, frame.FrameNumber))
            return;

        if (_isMaster)
        {
            // On main component, update animation and bounding box
            var transformsDirty = false;
            if (_animationDirty || _boneBoundingBoxDirty)
            {
                InitializeLocalBoneTransforms(false);

                if (_animationDirty)
                {
                    if (UpdateAndCheckAnimationTimers(frame.TimeStep))
                    {
                        CalculateAnimations();
                        transformsDirty = true;
                    }
                }

                if (_boneBoundingBoxDirty)
                    CalculateLocalBoundingBox();
            }

            if (transformsDirty)
            {
                var octree = GetOctant()!.GetOctree()!;
                for (var boneIndex = 0; boneIndex < _skeleton.GetNumBones(); ++boneIndex)
                {
                    var node = _skeleton.GetBone((uint)boneIndex)!.Node;
                    var transform = _skeletonData[boneIndex].LocalToParent;
                    if (node is not null)
                        octree.QueueNodeTransformUpdate(node, transform);
                }
            }
        }
        else
        {
            // On sibling components, just update bounding box.
            // Note that bounding box is delayed by one frame!
            if (_boneBoundingBoxDirty)
            {
                InitializeLocalBoneTransforms(false);
                CalculateLocalBoundingBox();
            }
        }
    }

    /// <summary>Calculate distance and prepare batches for rendering. May be called from worker thread(s), possibly re-entrantly.</summary>
    public override void UpdateBatches(CommonFrameInfo frame)
    {
        var camera = frame.Camera;
        if (camera is null)
            return;

        var worldTransform = Node.GetWorldTransform();
        var worldBoundingBox = GetWorldBoundingBox();
        SetDistance(camera.GetDistance(worldBoundingBox.Center));

        // Note: per-geometry distances do not take skinning into account. Especially in case of a ragdoll they may be
        // much off base if the node's own transform is not updated
        if (_batches.Count == 1)
        {
            var batch = _batches[0];
            batch.Distance = GetDistance();
            _batches[0] = batch;
        }
        else
        {
            for (var i = 0; i < _batches.Count; ++i)
            {
                var batch = _batches[i];
                batch.Distance = camera.GetDistance(worldTransform * (Model?.GetGeometryCenter((uint)i) ?? Vector3f.Zero));
                _batches[i] = batch;
            }
        }

        // Use a transformed version of the model's bounding box instead of world bounding box for LOD scale
        // determination so that animation does not change the scale
        var transformedBoundingBox = GetBoundingBox().Transformed(worldTransform);
        var size = transformedBoundingBox.Size;
        var scale = (size.X + size.Y + size.Z) / 3.0f;
        var newLodDistance = camera.GetLodDistance(GetDistance(), scale, LodBias);

        // If model is rendered from several views, use the minimum LOD distance for animation LOD
        if (frame.FrameNumber != _animationLodFrameNumber)
        {
            _animationLodDistance = newLodDistance;
            _animationLodFrameNumber = frame.FrameNumber;
        }
        else
            _animationLodDistance = Math.Min(_animationLodDistance, newLodDistance);

        if (newLodDistance != GetLodDistance())
        {
            SetLodDistance(newLodDistance);
            CalculateLodLevels();
        }
    }

    /// <summary>Prepare geometry for rendering.</summary>
    public override void UpdateGeometry(CommonFrameInfo frame)
    {
        // Late update in case the model came into view and animation was dirtied in the meanwhile
        if (_forceAnimationUpdate)
        {
            var needUpdate = UpdateAndCheckAnimationTimers(frame.TimeStep);
            if (!needUpdate)
                throw new InvalidOperationException("Animation update was forced but animation timers rejected it.");
            ApplyAnimation();
            _forceAnimationUpdate = false;
        }

        if (_skinningDirty)
            UpdateSkinning();

        if (_morphsDirty)
            UpdateMorphs();
    }

    /// <summary>Return whether a geometry update is necessary, and if it can happen in a worker thread.</summary>
    public override UpdateGeometryType GetUpdateGeometryType()
    {
        if (_morphsDirty || _forceAnimationUpdate || (_skinningDirty && _softwareSkinning))
            return UpdateGeometryType.MainThread;
        else if (_skinningDirty)
            return UpdateGeometryType.WorkerThread;
        else
            return UpdateGeometryType.None;
    }

    /// <summary>Connect to AnimationStateSource that provides animation states.</summary>
    public void ConnectToAnimationStateSource(AnimationStateSource? source)
    {
        _animationStateSource = source is not null ? new WeakReference<AnimationStateSource>(source) : null;
    }

    /// <summary>Return the skeleton.</summary>
    public Skeleton GetSkeleton() => _skeleton;

    /// <summary>Return all vertex morphs.</summary>
    public IReadOnlyList<ModelMorph> GetMorphs() => _morphs;

    /// <summary>Return all morph vertex buffers.</summary>
    public IReadOnlyList<VertexBuffer?> GetMorphVertexBuffers()
    {
        return _modelAnimator?.GetVertexBuffers() ?? Array.Empty<VertexBuffer?>();
    }

    /// <summary>Return per-geometry bone mappings.</summary>
    public IReadOnlyList<IReadOnlyList<uint>> GetGeometryBoneMappings()
        => _geometryBoneMappings;

    /// <summary>Return per-geometry skin matrices.</summary>
    public IReadOnlyList<IReadOnlyList<Matrix3x4>> GetGeometrySkinMatrices()
    {
        return _geometrySkinMatrices;
    }

    /// <summary>Return animation LOD bias.</summary>
    public float GetAnimationLodBias() => _animationLodBias;

    /// <summary>Return whether to update animation when not visible.</summary>
    public bool GetUpdateInvisible() => _updateInvisible;

    /// <summary>Return whether is the master animated model.</summary>
    public bool IsMaster() => _isMaster;

    /// <summary>Return vertex morph weight by index.</summary>
    public float GetMorphWeight(uint index) => index < _morphs.Count ? _morphs[(int)index].Weight : 0.0f;

    /// <summary>Return vertex morph weight by name.</summary>
    public float GetMorphWeight(string name)
    {
        foreach (var morph in _morphs)
        {
            if (morph.Name == name)
                return morph.Weight;
        }

        return 0.0f;
    }

    /// <summary>Return vertex morph weight by name hash.</summary>
    public float GetMorphWeight(StringHash nameHash)
    {
        foreach (var morph in _morphs)
        {
            if (morph.NameHash == nameHash)
                return morph.Weight;
        }

        return 0.0f;
    }

    /// <summary>Return number of vertex morphs.</summary>
    public uint GetNumMorphs() => checked((uint)_morphs.Count);

    /// <summary>Set model attribute.</summary>
    public new void SetModelAttr(ResourceRef value)
    {
        var cache = GetSubsystem<ResourceCache>()!;
        SetModel(string.IsNullOrEmpty(value.Name) ? null : cache.GetResourceOrNull<Model>(value.Name), false);
    }

    /// <summary>Set bones' animation enabled attribute.</summary>
    public void SetBonesEnabledAttr(VariantVector value)
    {
        var bones = _skeleton.GetModifiableBones();
        for (var i = 0; i < bones.Count && i < value.Count; ++i)
            bones[i].Animated = value[i].GetBool();
    }

    /// <summary>Set morphs attribute.</summary>
    public void SetMorphsAttr(IReadOnlyList<byte> value)
    {
        for (uint index = 0; index < value.Count; ++index)
            SetMorphWeight(index, value[(int)index] / 255.0f);
    }

    /// <summary>Return model attribute.</summary>
    public new ResourceRef GetModelAttr() => base.GetModelAttr();

    /// <summary>Return bones' animation enabled attribute.</summary>
    public VariantVector GetBonesEnabledAttr()
    {
        var result = new VariantVector();
        var bones = _skeleton.GetBones();
        foreach (var bone in bones)
            result.Add(new Variant(bone.Animated));
        return result;
    }

    /// <summary>Return morphs attribute.</summary>
    public IReadOnlyList<byte> GetMorphsAttr()
    {
        _attrBuffer.Clear();
        foreach (var morph in _morphs)
            _attrBuffer.WriteUByte((byte)(morph.Weight * 255.0f));
        return _attrBuffer.GetBuffer();
    }

    /// <summary>Set skeleton.</summary>
    public void SetSkeleton(Skeleton skeleton, bool createBones)
    {
        if (createBones && Node is null)
        {
            Log.GetLogger().LogError("AnimatedModel not attached to a scene node, can not create bone nodes");
            return;
        }

        if (_isMaster)
        {
            if (_skeleton.GetNumBones() == skeleton.GetNumBones())
            {
                var destBones = _skeleton.GetModifiableBones();
                var srcBones = skeleton.GetBones();
                var compatible = true;

                for (var i = 0; i < destBones.Count; ++i)
                {
                    if (destBones[i].Node is not null && destBones[i].Name == srcBones[i].Name &&
                        destBones[i].ParentIndex == srcBones[i].ParentIndex)
                    {
                        var boneNode = destBones[i].Node;
                        var animated = destBones[i].Animated;
                        destBones[i] = (Bone)srcBones[i].Clone();
                        destBones[i].Node = boneNode;
                        destBones[i].Animated = animated;
                    }
                    else
                    {
                        compatible = false;
                        break;
                    }
                }
                if (compatible)
                    return;
            }

            if (_animationStateSource is not null && _animationStateSource.TryGetTarget(out var animationStateSource))
                animationStateSource.MarkAnimationStateTracksDirty();

            if (createBones)
                RemoveRootBone();

            _skeleton.Define(skeleton);

            FinalizeBoneBoundingBoxes();

            if (createBones)
            {
                var bones = _skeleton.GetModifiableBones();
                for (var i = 0; i < bones.Count; ++i)
                {
                    var bone = bones[i];
                    var boneNode = Node!.CreateChild(bone.Name);
                    boneNode.AddListener(this);
                    boneNode.SetTransform(bone.InitialPosition, bone.InitialRotation, bone.InitialScale);
                    boneNode.SetTemporary(IsTemporary);
                    bone.Node = boneNode;
                }

                for (var i = 0; i < bones.Count; ++i)
                {
                    var parentIndex = bones[i].ParentIndex;
                    if (parentIndex != i && parentIndex < bones.Count)
                        bones[(int)parentIndex].Node!.AddChild(bones[i].Node!);
                }
            }

            var eventData = new DrawableEvents.BoneHierarchyCreatedEvent { Node = Node };
            SendEvent(ref eventData);
        }
        else
        {
            _skeleton.Define(skeleton);

            var master = Node!.GetComponent<AnimatedModel>();
            if (master is not null && !ReferenceEquals(master, this))
                master.FinalizeBoneBoundingBoxes();

            if (createBones)
            {
                var bones = _skeleton.GetModifiableBones();
                for (var i = 0; i < bones.Count; ++i)
                {
                    var boneNode = Node.GetChild(bones[i].Name, true);
                    boneNode?.AddListener(this);
                    bones[i].Node = boneNode;
                }
            }
        }

        _assignBonesPending = !createBones;
    }

    /// <summary>Visualize the component as debug geometry.</summary>
    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is not null && IsEnabledEffective)
        {
            debug.AddBoundingBox(GetWorldBoundingBox(), new Color(0.0f, 1.0f, 0.0f), depthTest);
            debug.AddSkeleton(_skeleton, new Color(0.75f, 0.75f, 0.75f), false);
        }
    }

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        base.OnNodeSet(previousNode, currentNode);

        if (currentNode is not null)
            _isMaster = currentNode.FindComponent<AnimatedModel>() is null;
    }

    /// <summary>Handle node transform being dirtied.</summary>
    protected internal override void OnMarkedDirty(Node node)
    {
        base.OnMarkedDirty(node);

        if (_skeleton.GetNumBones() != 0)
        {
            _skinningDirty = true;
            _boneBoundingBoxDirty = true;
        }
    }

    /// <summary>Recalculate the world-space bounding box.</summary>
    protected override void OnWorldBoundingBoxUpdate()
    {
        if (_isMaster)
            _worldBoundingBox = _boneBoundingBox.Transformed(Node.GetWorldTransform());
        else
        {
            var master = Node.GetComponent<AnimatedModel>();
            if (ReferenceEquals(master, this))
                _isMaster = true;
            if (master is not null)
                _worldBoundingBox = master.GetWorldBoundingBox();
        }
    }

    /// <summary>Recalculate the bone bounding box.</summary>
    public void UpdateBoneBoundingBox()
    {
        InitializeLocalBoneTransforms(false);
        CalculateLocalBoundingBox();
    }

    private void AssignBoneNodes()
    {
        _assignBonesPending = false;

        if (Node is null)
            return;

        var allBonesFound = true;
        var bones = _skeleton.GetModifiableBones();
        for (var i = 0; i < bones.Count; ++i)
        {
            var boneNode = Node.GetChild(bones[i].Name, true);
            if (boneNode is not null)
            {
                boneNode.AddListener(this);
                bones[i].Node = boneNode;
            }
            else
            {
                allBonesFound = false;
                break;
            }
        }

        if (!allBonesFound && GetModel() is { } model)
            SetSkeleton(model.GetSkeleton(), true);

        if (_animationStateSource is not null && _animationStateSource.TryGetTarget(out var animationStateSource))
            animationStateSource.MarkAnimationStateTracksDirty();
    }

    private void FinalizeBoneBoundingBoxes()
    {
        var bones = _skeleton.GetModifiableBones();
        var models = Node.GetComponents<AnimatedModel>();

        if (models.Count > 1)
        {
            if (GetModel() is { } model)
            {
                var modelBones = model.GetSkeleton().GetBones();
                for (var i = 0; i < bones.Count && i < modelBones.Count; ++i)
                {
                    bones[i].CollisionMask = modelBones[i].CollisionMask;
                    bones[i].Radius = modelBones[i].Radius;
                    bones[i].BoundingBox = modelBones[i].BoundingBox;
                }
            }

            for (var i = 0; i < models.Count; ++i)
            {
                if (ReferenceEquals(models[i], this))
                    continue;

                var otherSkeleton = models[i].GetSkeleton();
                for (var j = 0; j < bones.Count; ++j)
                {
                    var otherBone = otherSkeleton.GetBone(bones[j].NameHash);
                    if (otherBone is null)
                        continue;
                    if ((otherBone.CollisionMask & BoneCollisionShape.Sphere) != 0)
                    {
                        bones[j].CollisionMask |= BoneCollisionShape.Sphere;
                        bones[j].Radius = Math.Max(bones[j].Radius, otherBone.Radius);
                    }
                    if ((otherBone.CollisionMask & BoneCollisionShape.Box) != 0)
                    {
                        bones[j].CollisionMask |= BoneCollisionShape.Box;
                        if (bones[j].BoundingBox.Defined)
                            bones[j].BoundingBox.Merge(otherBone.BoundingBox);
                        else
                            bones[j].BoundingBox.Define(otherBone.BoundingBox);
                    }
                }
            }
        }

        for (var i = 0; i < bones.Count; ++i)
        {
            if ((bones[i].CollisionMask & BoneCollisionShape.Box) != 0 &&
                bones[i].BoundingBox.Size.Length < MathDefs.Epsilon)
                bones[i].CollisionMask &= ~BoneCollisionShape.Box;
            if ((bones[i].CollisionMask & BoneCollisionShape.Sphere) != 0 && bones[i].Radius < MathDefs.Epsilon)
                bones[i].CollisionMask &= ~BoneCollisionShape.Sphere;
        }
    }

    private void RemoveRootBone()
    {
        var rootBone = _skeleton.GetRootBone();
        rootBone?.Node?.Remove();
    }

    private void InitializeLocalBoneTransforms(bool reset)
    {
        if (_skeleton.GetNumBones() != _skeletonData.Count)
            throw new InvalidOperationException("Skeleton data size does not match bone count.");

        for (var i = 0; i < _skeleton.GetNumBones(); ++i)
        {
            var bone = _skeleton.GetBone(checked((uint)i))!;
            var output = _skeletonData[(int)i];
            output.Dirty = AnimationChannel.None;
            output.LocalToParent = !reset && bone.Node is not null
                ? new Transform(bone.Node.Position, bone.Node.Rotation, bone.Node.Scale)
                : new Transform(bone.InitialPosition, bone.InitialRotation, bone.InitialScale);
            _skeletonData[(int)i] = output;
        }
    }

    private void CalculateFinalBoneTransforms()
    {
        var bonesOrder = _skeleton.GetBonesOrder();
        for (var i = 0; i < bonesOrder.Count; ++i)
        {
            var boneIndex = bonesOrder[i];
            var bone = _skeleton.GetBone(boneIndex)!;
            var output = _skeletonData[(int)boneIndex];
            output.LocalToComponent = bone.ParentIndex == boneIndex
                ? output.LocalToParent.ToMatrix3x4()
                : _skeletonData[(int)bone.ParentIndex].LocalToComponent * output.LocalToParent.ToMatrix3x4();
            _skeletonData[(int)boneIndex] = output;
        }
    }

    private void CalculateLocalBoundingBox()
    {
        CalculateFinalBoneTransforms();

        _boneBoundingBox.Clear();

        var bones = _skeleton.GetBones();
        if (bones.Count == 0)
            _boneBoundingBox.Merge(Vector3f.Zero);
        else
        {
            for (var boneIndex = 0; boneIndex < bones.Count; ++boneIndex)
            {
                var bone = bones[boneIndex];
                var transform = _skeletonData[boneIndex].LocalToComponent;
                if ((bone.CollisionMask & BoneCollisionShape.Box) != 0)
                    _boneBoundingBox.Merge(bone.BoundingBox.Transformed(transform));
                else if ((bone.CollisionMask & BoneCollisionShape.Sphere) != 0)
                    _boneBoundingBox.Merge(new Sphere(transform.Translation, bone.Radius * 0.5f));
            }
            if (!_boneBoundingBox.Defined)
                _boneBoundingBox.Merge(Vector3f.Zero);
        }

        _boneBoundingBoxDirty = false;
        _worldBoundingBoxDirty = true;
    }

    private void MarkMorphsDirty()
    {
        _morphsDirty = true;
    }

    private void CloneGeometries()
    {
        _modelAnimator?.Dispose();
        _modelAnimator = new SoftwareModelAnimator();
        _modelAnimator.Initialize(_model!, _softwareSkinning, _numSoftwareSkinningBones);
        var animatedGeometries = _modelAnimator.GetGeometries();
        _geometries.Clear();
        foreach (var sourceLods in animatedGeometries)
            _geometries.Add([.. sourceLods]);

        // Make sure the rendering batches use the new cloned geometries
        ResetLodLevels();
        MarkMorphsDirty();
    }

    private void SetGeometryBoneMappings()
    {
        _geometrySkinMatrices.Clear();
        _geometrySkinMatrixPtrs.Clear();

        if (_geometryBoneMappings.Count == 0)
            return;

        // Check if all mappings are empty, then we do not need to use mapped skinning
        var allEmpty = true;
        for (var i = 0; i < _geometryBoneMappings.Count; ++i)
        {
            if (_geometryBoneMappings[i].Count != 0)
                allEmpty = false;
        }

        if (allEmpty)
            return;

        if (_softwareSkinning)
        {
            Log.GetLogger().LogWarning("Geometry bone mappings are ignored in software skinning");
            return;
        }

        // Reserve space for per-geometry skinning matrices
        for (var i = 0; i < _geometryBoneMappings.Count; ++i)
            _geometrySkinMatrices.Add(new Matrix3x4[_geometryBoneMappings[i].Count]);

        // Build original-to-skinindex matrix pointer mapping for fast copying
        // Note: at this point layout of geometrySkinMatrices_ cannot be modified or pointers become invalid
        for (var i = 0; i < _skeleton.GetNumBones(); ++i)
            _geometrySkinMatrixPtrs.Add([]);
        for (var i = 0; i < _geometryBoneMappings.Count; ++i)
        {
            for (var j = 0; j < _geometryBoneMappings[i].Count; ++j)
                _geometrySkinMatrixPtrs[(int)_geometryBoneMappings[i][j]].Add((i, j));
        }
    }

    private bool PrepareForThreadedUpdate(Camera? camera, uint frameNumber)
    {
        // If node was invisible last frame, need to decide animation LOD distance here
        // If headless, retain the current animation distance (should be 0)
        if (camera is not null && Math.Abs(unchecked((int)(frameNumber - _viewFrameNumber))) > 1)
        {
            // First check for no update at all when invisible, except on first update. In that case reset LOD timer to ensure update
            // next time the model is in view
            if (_viewFrameNumber != 0 && !_updateInvisible)
            {
                if (_animationDirty)
                {
                    _animationLodTimer = -1.0f;
                    _forceAnimationUpdate = true;
                }
                return false;
            }

            // Force view frame number to be valid
            _viewFrameNumber = Math.Max(1u, _viewFrameNumber);

            var distance = camera.GetDistance(Node.GetWorldPosition());
            // If distance is greater than draw distance, no need to update at all
            if (DrawDistance > 0.0f && distance > DrawDistance)
                return false;

            var size = GetWorldBoundingBox().Size;
            var scale = (size.X + size.Y + size.Z) / 3.0f;
            _animationLodDistance = camera.GetLodDistance(distance, scale, LodBias);
        }

        return true;
    }

    private bool UpdateAndCheckAnimationTimers(float timeStep)
    {
        // If using animation LOD, accumulate time and see if it is time to update
        var throttlingAllowed = _animationStateSource is null ||
            !_animationStateSource.TryGetTarget(out var animationStateSource) ||
            animationStateSource.IsAnimationThrottlingAllowed();
        if (throttlingAllowed && _animationLodBias > 0.0f && _animationLodDistance > 0.0f)
        {
            // Perform the first update always regardless of LOD timer
            if (_animationLodTimer >= 0.0f)
            {
                _animationLodTimer += _animationLodBias * timeStep * AnimationLodBaseScale;
                if (_animationLodTimer >= _animationLodDistance)
                    _animationLodTimer %= _animationLodDistance;
                else
                    return false;
            }
            else
                _animationLodTimer = 0.0f;
        }
        return true;
    }

    private void CalculateAnimations()
    {
        if (!_isMaster)
            throw new InvalidOperationException("Only master AnimatedModel may calculate animations.");

        // AnimationStateSource is a weak pointer which may or may not be an issue
        if (_animationStateSource is not null && _animationStateSource.TryGetTarget(out var animationStateSource))
        {
            foreach (var state in animationStateSource.GetAnimationStates())
                state.CalculateModelTracks(_skeletonData);
        }

        _animationDirty = false;
        _boneBoundingBoxDirty = true;
    }

    private void ApplyBoneTransformsToNodes()
    {
        for (var boneIndex = 0; boneIndex < _skeleton.GetNumBones(); ++boneIndex)
        {
            var bone = _skeleton.GetBone((uint)boneIndex)!;
            var transform = _skeletonData[boneIndex].LocalToParent;
            if (bone.Node is { } node)
                node.SetTransformSilent(transform.Position, transform.Rotation, transform.Scale);
        }

        // Skeleton reset and animations apply the node transforms "silently" to avoid repeated marking dirty. Mark dirty now
        Node.MarkDirty();
    }

    internal void MarkAnimationDirty()
    {
        if (_isMaster)
        {
            _animationDirty = true;
            MarkForUpdate();
        }
    }

    private void UpdateSkinning()
    {
        // Note: the model's world transform will be baked in the skin matrices
        var bones = _skeleton.GetBones();
        // Use model's world transform in case a bone is missing
        var worldTransform = Node.GetWorldTransform();

        // Skinning with global matrices only
        if (_geometrySkinMatrices.Count == 0)
        {
            for (var i = 0; i < bones.Count; ++i)
            {
                var bone = bones[i];
                _skinMatrices[i] = bone.Node is not null
                    ? bone.Node.GetWorldTransform() * bone.OffsetMatrix
                    : worldTransform;
            }
        }
        // Skinning with per-geometry matrices
        else
        {
            for (var i = 0; i < bones.Count; ++i)
            {
                var bone = bones[i];
                _skinMatrices[i] = bone.Node is not null
                    ? bone.Node.GetWorldTransform() * bone.OffsetMatrix
                    : worldTransform;

                // Copy the skin matrix to per-geometry matrices as needed
                for (var j = 0; j < _geometrySkinMatrixPtrs[i].Count; ++j)
                {
                    var target = _geometrySkinMatrixPtrs[i][j];
                    _geometrySkinMatrices[target.GeometryIndex][target.MatrixIndex] = _skinMatrices[i];
                }
            }
        }

        _skinningDirty = false;

        // If software skinning is enabled, force update
        if (_softwareSkinning)
            _morphsDirty = true;
    }

    private void UpdateMorphs()
    {
        var graphics = GetSubsystem<Graphics>();
        if (graphics is null)
            return;

        if (_modelAnimator is not null)
        {
            _modelAnimator.ResetAnimation();
            _modelAnimator.ApplyMorphs(CollectionsMarshal.AsSpan(_morphs));
            if (_softwareSkinning)
                _modelAnimator.ApplySkinning(_skinMatrices);
            _modelAnimator.Commit();
        }

        _morphsDirty = false;
    }

    private void HandleModelReloadFinished(Resource resource)
    {
        var currentModel = _model;
        _model = null;
        SetModel(currentModel);
    }

    private void UpdateSoftwareSkinningState()
    {
        var renderer = GetSubsystem<Renderer>();
        if (renderer is null)
            return;

        _softwareSkinning = !renderer.GetUseHardwareSkinning();
        _numSoftwareSkinningBones = renderer.GetNumSoftwareSkinningBones();

        if (renderer.GetSkinningMode() == SkinningMode.Auto && _model is not null)
        {
            // Fallback to software skinning if too many bones affect the model
            if (_geometrySkinMatrices.Count == 0 && _model.GetSkeleton().GetNumBones() > Graphics.GetMaxBones())
            {
                Log.GetLogger().LogWarning(
                    "Model {Name} have {NumBones} bones, but only {MaxBones} can be skinned on GPU. Falling back to software skinning.",
                    _model.Name, _model.GetSkeleton().GetNumBones(), Graphics.GetMaxBones());

                _softwareSkinning = true;
            }
        }
    }

    private static void ResizeList<T>(List<T> values, int size)
    {
        if (values.Count > size)
            values.RemoveRange(size, values.Count - size);
        while (values.Count < size)
            values.Add(default!);
    }
}
