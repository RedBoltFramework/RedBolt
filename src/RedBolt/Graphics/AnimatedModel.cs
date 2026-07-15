using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Animated model component.</summary>
public class AnimatedModel : StaticModel
{
    // TODO: port - C++ code:
    // friend class AnimationState;
    //
    // TODO: port - C++ code:
    // void ApplyAttributes() override;
    // TODO: port - C++ code:
    // void ProcessCustomRayQuery(const RayOctreeQuery& query, const BoundingBox& worldBoundingBox,
    //     const Matrix3x4& worldTransform, ea::span<const Matrix3x4> boneWorldTransforms,
    //     ea::vector<RayQueryResult>& results);
    // TODO: port - C++ code:
    // void ProcessRayQuery(const RayOctreeQuery& query, ea::vector<RayQueryResult>& results) override;
    // TODO: port - C++ code:
    // void Update(const FrameInfo& frame) override;
    // TODO: port - C++ code:
    // void UpdateBatches(const FrameInfo& frame) override;
    // TODO: port - C++ code:
    // void UpdateGeometry(const FrameInfo& frame) override;
    // TODO: port - C++ code:
    // UpdateGeometryType GetUpdateGeometryType() override;
    // TODO: port - C++ code:
    // const ea::vector<SharedPtr<VertexBuffer>>& GetMorphVertexBuffers() const;
    // TODO: port - C++ code:
    // const ea::vector<ea::vector<unsigned>>& GetGeometryBoneMappings() const;
    // TODO: port - C++ code:
    // const ea::vector<ea::vector<Matrix3x4>>& GetGeometrySkinMatrices() const;
    // TODO: port - C++ code:
    // void UpdateBoneBoundingBox();
    // TODO: port - C++ code:
    // protected:
    // void OnMarkedDirty(Node* node) override;
    // void OnWorldBoundingBoxUpdate() override;
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

    private readonly Skeleton _skeleton = new();
    private AnimationStateSource? _animationStateSource;
    private readonly List<ModelMorph> _morphs = [];
    private float _animationLodBias = 1.0f;
    private bool _updateInvisible;
    private bool _isMaster;
    private readonly List<byte> _morphsAttribute = [];

    /// <summary>Construct.</summary>
    public AnimatedModel()
    {
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

        SetSkeleton(model.GetSkeleton(), createBones);
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

    /// <summary>Connect to AnimationStateSource that provides animation states.</summary>
    public void ConnectToAnimationStateSource(AnimationStateSource? source)
    {
        _animationStateSource = source;
    }

    /// <summary>Return the skeleton.</summary>
    public Skeleton GetSkeleton() => _skeleton;

    /// <summary>Return all vertex morphs.</summary>
    public IReadOnlyList<ModelMorph> GetMorphs() => _morphs;

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

        _skeleton.Define(skeleton);
        if (!createBones)
            return;

        var bones = _skeleton.GetModifiableBones();
        for (var i = 0; i < bones.Count; ++i)
        {
            var bone = bones[i];
            var boneNode = Node!.CreateChild(bone.Name);
            boneNode.SetTransform(bone.InitialPosition, bone.InitialRotation, bone.InitialScale);
            bone.Node = boneNode;
        }

        for (var i = 0; i < bones.Count; ++i)
        {
            var parentIndex = bones[i].ParentIndex;
            if (parentIndex != i && parentIndex < bones.Count)
                bones[(int)parentIndex].Node!.AddChild(bones[i].Node!);
        }
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
}
