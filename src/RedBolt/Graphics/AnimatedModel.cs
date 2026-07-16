using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Animated model component.</summary>
public class AnimatedModel : StaticModel
{
    // TODO: port - C++ code:
    // void UpdateBatches(const FrameInfo& frame) override;
    // TODO: port - C++ code:
    // void UpdateGeometry(const FrameInfo& frame) override;
    // TODO: port - C++ code:
    // UpdateGeometryType GetUpdateGeometryType() override;
    // TODO: port - C++ code:
    // const ea::vector<SharedPtr<VertexBuffer>>& GetMorphVertexBuffers() const;
    // TODO: port - C++ fields:
    // ea::vector<ModelAnimationOutput> skeletonData_;
    // SharedPtr<SoftwareModelAnimator> modelAnimator_;
    // ea::vector<Matrix3x4> skinMatrices_;
    // ea::vector<ea::vector<unsigned>> geometryBoneMappings_;
    // ea::vector<ea::vector<Matrix3x4>> geometrySkinMatrices_;
    // ea::vector<ea::vector<Matrix3x4*>> geometrySkinMatrixPtrs_;
    // BoundingBox boneBoundingBox_;
    // mutable VectorBuffer attrBuffer_;
    // unsigned animationLodFrameNumber_;
    // float animationLodTimer_;
    // float animationLodDistance_;
    // bool softwareSkinning_;
    // unsigned numSoftwareSkinningBones_;
    // bool isMaster_;
    // bool assignBonesPending_;
    // bool forceAnimationUpdate_;
    // bool animationDirty_, morphsDirty_, skinningDirty_, boneBoundingBoxDirty_;

    private bool _animationDirty;
    private bool _morphsDirty;
    private bool _skinningDirty = true;
    private bool _boneBoundingBoxDirty = true;
    private readonly Skeleton _skeleton = new();
    private readonly List<ModelAnimationOutput> _skeletonData = [];
    private AnimationStateSource? _animationStateSource;
    private readonly List<ModelMorph> _morphs = [];
    private readonly List<Matrix3x4> _skinMatrices = [];
    private BoundingBox _boneBoundingBox;
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
    private readonly List<byte> _morphsAttribute = [];

    /// <summary>Construct.</summary>
    public AnimatedModel()
    {
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
            Log.Logger.LogError("Can not set model while model component is not attached to a scene node");
            return;
        }

        base.SetModel(model);
        _morphs.Clear();

        if (model is null)
        {
            SetSkeleton(new Skeleton(), false);
            return;
        }

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

        _boneBoundingBox = GetBoundingBox();
        _boneBoundingBoxDirty = true;
        SetSkeleton(model.GetSkeleton(), createBones);
        ResizeList(_skinMatrices, checked((int)_skeleton.GetNumBones()));
        ResizeList(_skeletonData, checked((int)_skeleton.GetNumBones()));
    }

    /// <summary>Set model and create bone nodes.</summary>
    public new void SetModel(Model? model) => SetModel(model, true);

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

        _morphs[(int)index].Weight = weight;
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
    }

    /// <summary>Reset node transforms of skeleton bones.</summary>
    public void ResetBones()
    {
        _skeleton.Reset();
    }

    /// <summary>Apply all animation states to nodes.</summary>
    public void ApplyAnimation()
    {
        if (!_isMaster)
            return;

        _skeleton.ResetSilent();
        var outputs = new List<ModelAnimationOutput>(checked((int)_skeleton.GetNumBones()));
        foreach (var bone in _skeleton.GetBones())
        {
            outputs.Add(new ModelAnimationOutput
            {
                LocalToParent = new Transform(bone.InitialPosition, bone.InitialRotation, bone.InitialScale)
            });
        }

        if (_animationStateSource is not null)
        {
            foreach (var state in _animationStateSource.GetAnimationStates())
                state.CalculateModelTracks(outputs);
        }

        var bones = _skeleton.GetBones();
        for (var i = 0; i < bones.Count; ++i)
        {
            var boneNode = bones[i].Node;
            if (boneNode is null)
                continue;

            var transform = outputs[i].LocalToParent;
            boneNode.SetTransformSilent(transform.Position, transform.Rotation, transform.Scale);
        }

        Node?.MarkDirty();
    }

    /// <summary>Update animation before octree reinsertion.</summary>
    public override void Update(CommonFrameInfo frame)
    {
        base.Update(frame);
        ApplyAnimation();
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
        _animationStateSource = source;
    }

    /// <summary>Return the skeleton.</summary>
    public Skeleton GetSkeleton() => _skeleton;

    /// <summary>Return all vertex morphs.</summary>
    public IReadOnlyList<ModelMorph> GetMorphs() => _morphs;

    /// <summary>Return per-geometry bone mappings.</summary>
    public IReadOnlyList<IReadOnlyList<uint>> GetGeometryBoneMappings()
        => GetModel()?.GetGeometryBoneMappings() ?? [];

    /// <summary>Return per-geometry skin matrices.</summary>
    public IReadOnlyList<IReadOnlyList<Matrix3x4>> GetGeometrySkinMatrices()
    {
        var mappings = GetModel()?.GetGeometryBoneMappings();
        if (mappings is null)
            return [];

        var worldTransform = IsAttached ? Node.GetWorldTransform() : Matrix3x4.IDENTITY;
        var result = new List<IReadOnlyList<Matrix3x4>>(mappings.Count);
        foreach (var mapping in mappings)
        {
            var matrices = new List<Matrix3x4>(mapping.Count);
            foreach (var boneIndex in mapping)
            {
                var bone = _skeleton.GetBone(boneIndex);
                matrices.Add(bone?.Node is { } boneNode
                    ? boneNode.GetWorldTransform() * bone.OffsetMatrix
                    : worldTransform);
            }
            result.Add(matrices);
        }

        return result;
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
        _morphsAttribute.Clear();
        foreach (var morph in _morphs)
            _morphsAttribute.Add((byte)(morph.Weight * 255.0f));
        return _morphsAttribute;
    }

    /// <summary>Set skeleton.</summary>
    public void SetSkeleton(Skeleton skeleton, bool createBones)
    {
        if (createBones && Node is null)
        {
            Log.Logger.LogError("AnimatedModel not attached to a scene node, can not create bone nodes");
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

            _animationStateSource?.MarkAnimationStateTracksDirty();

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

        _animationStateSource?.MarkAnimationStateTracksDirty();
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

    private static void ResizeList<T>(List<T> values, int size)
    {
        if (values.Count > size)
            values.RemoveRange(size, values.Count - size);
        while (values.Count < size)
            values.Add(default!);
    }
}
