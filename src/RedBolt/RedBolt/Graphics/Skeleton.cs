using Microsoft.Extensions.Logging;

namespace RedBolt;

[Flags]
public enum AnimationChannel : byte
{
    None = 0,
    Position = 1 << 0,
    Rotation = 1 << 1,
    Scale = 1 << 2
}

[Flags]
public enum BoneCollisionShape : byte
{
    None = 0x0,
    Sphere = 0x1,
    Box = 0x2
}

/// <summary>Bone in a skeleton.</summary>
public sealed class Bone : ICloneable
{
    /// <summary>Construct with defaults.</summary>
    public Bone()
    {
        ParentIndex = 0;
        InitialPosition = Vector3f.Zero;
        InitialRotation = Quaternion.IDENTITY;
        InitialScale = Vector3f.One;
        Animated = true;
        Radius = 0.0f;
        CollisionMask = BoneCollisionShape.None;
        BoundingBox = new BoundingBox();
    }

    /// <summary>Bone name.</summary>
    public string Name = string.Empty;

    /// <summary>Bone name hash.</summary>
    public StringHash NameHash;

    /// <summary>Parent bone index.</summary>
    public int ParentIndex;

    /// <summary>Reset position.</summary>
    public Vector3f InitialPosition;

    /// <summary>Reset rotation.</summary>
    public Quaternion InitialRotation;

    /// <summary>Reset scale.</summary>
    public Vector3f InitialScale;

    /// <summary>Offset matrix.</summary>
    public Matrix3x4 OffsetMatrix;

    /// <summary>Animation enable flag.</summary>
    public bool Animated;

    /// <summary>Supported collision types.</summary>
    public BoneCollisionShape CollisionMask;

    /// <summary>Radius.</summary>
    public float Radius;

    /// <summary>Local-space bounding box.</summary>
    public BoundingBox BoundingBox;

    /// <summary>Scene node.</summary>
    public Node? Node;

    public object Clone()
    {
        return new Bone
        {
            Name = Name,
            NameHash = NameHash,
            ParentIndex = ParentIndex,
            InitialPosition = InitialPosition,
            InitialRotation = InitialRotation,
            InitialScale = InitialScale,
            OffsetMatrix = OffsetMatrix,
            Animated = Animated,
            CollisionMask = CollisionMask,
            Radius = Radius,
            BoundingBox = BoundingBox,
            Node = Node
        };
    }
}

/// <summary>Hierarchical collection of bones.</summary>
public sealed class Skeleton : ICloneable, IDisposable
{
    /// <summary>Construct an empty skeleton.</summary>
    public Skeleton()
    {
    }

    /// <summary>Destruct.</summary>
    public void Dispose()
    {
        _bones.Clear();
        _bonesOrder.Clear();
        _rootBoneIndex = -1;
    }

    public Skeleton Clone()
    {
        var clone = new Skeleton
        {
            _rootBoneIndex = _rootBoneIndex
        };
        foreach (var bone in _bones)
            clone._bones.Add((Bone)bone.Clone());
        clone._bonesOrder.AddRange(_bonesOrder);
        return clone;
    }

    object ICloneable.Clone() => Clone();

    /// <summary>Read from a stream. Return true if successful.</summary>
    public bool Load(Deserializer source)
    {
        ClearBones();

        if (source.IsEof())
            return false;

        int bones = checked((int)source.ReadUInt());
        _bones.Capacity = Math.Max(_bones.Capacity, bones);

        for (int i = 0; i < bones; ++i)
        {
            var newBone = new Bone
            {
                Name = source.ReadString()
            };
            newBone.NameHash = new StringHash(newBone.Name);
            newBone.ParentIndex = checked((int)source.ReadUInt());
            newBone.InitialPosition = source.ReadVector3();
            newBone.InitialRotation = source.ReadQuaternion();
            newBone.InitialScale = source.ReadVector3();
            newBone.OffsetMatrix = source.ReadMatrix3x4();

            // Read bone collision data
            newBone.CollisionMask = (BoneCollisionShape)source.ReadUByte();
            if ((newBone.CollisionMask & BoneCollisionShape.Sphere) != 0)
                newBone.Radius = source.ReadFloat();
            if ((newBone.CollisionMask & BoneCollisionShape.Box) != 0)
                newBone.BoundingBox = source.ReadBoundingBox();

            if (newBone.ParentIndex == i)
                _rootBoneIndex = i;

            _bones.Add(newBone);
        }

        UpdateBoneOrder();
        return true;
    }

    /// <summary>Write to a stream. Return true if successful.</summary>
    public bool Save(Serializer dest)
    {
        if (!dest.WriteUInt((uint)_bones.Count))
            return false;

        for (var i = 0; i < _bones.Count; ++i)
        {
            Bone bone = _bones[i];
            dest.WriteString(bone.Name);
            dest.WriteUInt(checked((uint)bone.ParentIndex));
            dest.WriteVector3(bone.InitialPosition);
            dest.WriteQuaternion(bone.InitialRotation);
            dest.WriteVector3(bone.InitialScale);
            _ = dest.WriteMatrix3x4(bone.OffsetMatrix);

            // Collision info
            dest.WriteUByte((byte)bone.CollisionMask);
            if ((bone.CollisionMask & BoneCollisionShape.Sphere) != 0)
                dest.WriteFloat(bone.Radius);
            if ((bone.CollisionMask & BoneCollisionShape.Box) != 0)
                dest.WriteBoundingBox(bone.BoundingBox);
        }

        return true;
    }

    /// <summary>Define from another skeleton.</summary>
    public void Define(Skeleton source)
    {
        ClearBones();

        _bones.Capacity = Math.Max(_bones.Capacity, source._bones.Count);
        foreach (var sourceBone in source._bones)
        {
            var bone = (Bone)sourceBone.Clone();
            // Make sure we clear node references, if they exist
            // (AnimatedModel will create new nodes on its own)
            bone.Node = null;
            _bones.Add(bone);
        }

        _rootBoneIndex = source._rootBoneIndex;
        UpdateBoneOrder();
    }

    /// <summary>Set root bone's index.</summary>
    public void SetRootBoneIndex(int index)
    {
        if (index >= 0 && index < _bones.Count)
            _rootBoneIndex = index;
        else
            Log.GetLogger().LogError("Root bone index out of bounds");
    }

    /// <summary>Set number of bones.</summary>
    public void SetNumBones(int numBones)
    {
        while (_bones.Count < numBones)
            _bones.Add(new Bone());
        if (_bones.Count > numBones)
            _bones.RemoveRange(numBones, _bones.Count - numBones);
    }

    /// <summary>Clear bones.</summary>
    public void ClearBones()
    {
        _bones.Clear();
        _rootBoneIndex = -1;
    }

    /// <summary>Recalculate order of bones in hierarchy, from parents to children.</summary>
    public void UpdateBoneOrder()
    {
        int numBones = _bones.Count;
        _bonesOrder.Capacity = Math.Max(_bonesOrder.Capacity, numBones);
        _bonesOrder.Clear();

        // Collect roots first
        for (int boneIndex = 0; boneIndex < numBones; ++boneIndex)
        {
            if (_bones[boneIndex].ParentIndex == boneIndex)
                _bonesOrder.Add(boneIndex);
        }

        // Collect layer by layer
        int rangeBegin = 0;
        int rangeEnd = _bonesOrder.Count;

        while (_bonesOrder.Count < numBones && rangeBegin != rangeEnd)
        {
            for (int boneIndex = 0; boneIndex < numBones; ++boneIndex)
            {
                int parentBoneIndex = _bones[boneIndex].ParentIndex;
                if (parentBoneIndex == boneIndex)
                    continue;

                var isDirectChild = false;
                for (var parentIndex = rangeBegin; parentIndex < rangeEnd; ++parentIndex)
                {
                    if (_bonesOrder[parentIndex] == parentBoneIndex)
                    {
                        isDirectChild = true;
                        break;
                    }
                }

                if (isDirectChild)
                    _bonesOrder.Add(boneIndex);
            }

            rangeBegin = rangeEnd;
            rangeEnd = _bonesOrder.Count;
        }
    }

    /// <summary>Reset all animating bones to initial positions.</summary>
    public void Reset()
    {
        foreach (var bone in _bones)
        {
            if (bone.Animated && bone.Node is not null)
                bone.Node.SetTransform(bone.InitialPosition, bone.InitialRotation, bone.InitialScale);
        }
    }

    /// <summary>Return all bones.</summary>
    public IReadOnlyList<Bone> GetBones() => _bones;

    /// <summary>Return order of bones from parents to children.</summary>
    public IReadOnlyList<int> GetBonesOrder() => _bonesOrder;

    /// <summary>Return modifiable bones.</summary>
    public IList<Bone> GetModifiableBones() => _bones;

    /// <summary>Return number of bones.</summary>
    public int GetNumBones() => _bones.Count;

    /// <summary>Return root bone.</summary>
    public Bone? GetRootBone() => GetBone(_rootBoneIndex);

    /// <summary>Return index of the bone by name. Return -1 if not found.</summary>
    public int GetBoneIndex(string boneName) => GetBoneIndex(new StringHash(boneName));

    /// <summary>Return index of the bone by name hash. Return -1 if not found.</summary>
    public int GetBoneIndex(StringHash boneNameHash)
    {
        for (int i = 0; i < _bones.Count; ++i)
        {
            if (_bones[i].NameHash == boneNameHash)
                return i;
        }

        return -1;
    }

    /// <summary>Return index of the bone by the bone pointer. Return -1 if not found.</summary>
    public int GetBoneIndex(Bone? bone)
    {
        if (bone is null)
            return -1;

        for (int i = 0; i < _bones.Count; ++i)
        {
            if (ReferenceEquals(_bones[i], bone))
                return i;
        }

        return -1;
    }

    /// <summary>Return parent of the given bone. Return null for root bones.</summary>
    public Bone? GetBoneParent(Bone? bone)
    {
        return GetBoneIndex(bone) == bone!.ParentIndex ? null : GetBone(bone.ParentIndex);
    }

    /// <summary>Return bone by index.</summary>
    public Bone? GetBone(int index)
    {
        return index >= 0 && index < _bones.Count ? _bones[index] : null;
    }

    /// <summary>Return bone by name.</summary>
    public Bone? GetBone(string name) => GetBone(new StringHash(name));

    /// <summary>Return bone by name hash.</summary>
    public Bone? GetBone(StringHash boneNameHash)
    {
        int index = GetBoneIndex(boneNameHash);
        return index >= 0 && index < _bones.Count ? _bones[index] : null;
    }

    /// <summary>Reset all animating bones to initial positions without marking the nodes dirty. Requires the node dirtying to be performed later.</summary>
    public void ResetSilent()
    {
        foreach (var bone in _bones)
        {
            if (bone.Animated && bone.Node is not null)
                bone.Node.SetTransformSilent(bone.InitialPosition, bone.InitialRotation, bone.InitialScale);
        }
    }

    /// <summary>Bones.</summary>
    private readonly List<Bone> _bones = [];
    /// <summary>Indices of bones ordered from root to children.</summary>
    private readonly List<int> _bonesOrder = [];
    /// <summary>Root bone index.</summary>
    private int _rootBoneIndex = -1;
}
