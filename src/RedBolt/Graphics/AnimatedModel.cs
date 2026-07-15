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
    // void DrawDebugGeometry(DebugRenderer* debug, bool depthTest) override;
    // TODO: port - C++ code:
    // void ApplyAnimation();
    // TODO: port - C++ code:
    // const ea::vector<SharedPtr<VertexBuffer>>& GetMorphVertexBuffers() const;
    // TODO: port - C++ code:
    // unsigned GetNumMorphs() const;
    // TODO: port - C++ code:
    // void SetModelAttr(const ResourceRef& value);
    // TODO: port - C++ code:
    // void SetBonesEnabledAttr(const VariantVector& value);
    // TODO: port - C++ code:
    // void SetMorphsAttr(const ea::vector<unsigned char>& value);
    // TODO: port - C++ code:
    // ResourceRef GetModelAttr() const;
    // TODO: port - C++ code:
    // VariantVector GetBonesEnabledAttr() const;
    // TODO: port - C++ code:
    // const ea::vector<unsigned char>& GetMorphsAttr() const;
    // TODO: port - C++ code:
    // const ea::vector<ea::vector<unsigned>>& GetGeometryBoneMappings() const;
    // TODO: port - C++ code:
    // const ea::vector<ea::vector<Matrix3x4>>& GetGeometrySkinMatrices() const;
    // TODO: port - C++ code:
    // void UpdateBoneBoundingBox();
    // TODO: port - C++ code:
    // protected:
    // void OnNodeSet(Node* previousNode, Node* currentNode) override;
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
    public bool IsMaster() => true;

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
}
