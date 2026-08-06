//
// Copyright (c) 2017-2020 the rbfx project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.Replica;

using System.Diagnostics;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

/// <summary>NetworkObject replicated from a client prefab and not updated afterwards.</summary>
[Reflected(ObjectCategory.Category_Network)]
public partial class StaticNetworkObject : NetworkObject
{
    [Serialized("Is Origin For Distance Filtering")]
    public bool IsOriginForDistanceFiltering { get; set; } = true;

    public void SetClientPrefab(PrefabResource? prefab)
    {
        if (prefab is not null && prefab.GetName().Length == 0)
        {
            Debug.Fail($"StaticNetworkObject.SetClientPrefab is called with unnamed resource for object "
                + TrackedComponent.ToString(NetworkId));
            return;
        }

        if (!IsStandalone)
        {
            Logging.Log.GetLogger().Error(
                "StaticNetworkObject.SetClientPrefab is called for object {} which is already replicated",
                TrackedComponent.ToString(NetworkId));
            return;
        }

        _clientPrefab = prefab;
    }

    public PrefabResource? ClientPrefab => _clientPrefab;

    public override float? CalculateDistanceForFiltering(NetworkObject otherNetworkObject)
    {
        if (!IsOriginForDistanceFiltering)
            return null;
        return base.CalculateDistanceForFiltering(otherNetworkObject);
    }

    public override void WriteSnapshot(NetworkFrame frame, Serializer destination)
    {
        destination.WriteUInt((uint)ParentNetworkId);
        destination.WriteString(_clientPrefab?.GetName() ?? string.Empty);
        destination.WriteString(Node!.Name);

        var absolutePosition = Scene!.ToAbsoluteWorldPosition(Node.GetWorldPosition());
        destination.WritePackedVector3(absolutePosition, VectorBinaryEncoding.Double);
        destination.WritePackedQuaternion(Node.GetWorldRotation(), VectorBinaryEncoding.Int16);
        destination.WriteVector3(Node.GetSignedWorldScale());
    }

    public override void InitializeFromSnapshot(NetworkFrame frame, Deserializer source, bool isOwned)
    {
        SetParentNetworkObject((NetworkId)source.ReadUInt());

        var clientPrefabName = source.ReadString();
        SetClientPrefabAttribute(new ResourceRef(typeof(PrefabResource).GetClassName(), clientPrefabName));

        if (_clientPrefab is not null)
        {
            using var reader = new PrefabReaderFromMemory(_clientPrefab.GetNodePrefab());
            Node!.Load(reader, PrefabLoadFlag.KeepExistingComponents);
        }

        Node!.SetName(source.ReadString());

        var absolutePosition = source.ReadPackedVector3(VectorBinaryEncoding.Double);
        var worldPosition = Scene!.ToRelativeWorldPosition(absolutePosition);
        var worldRotation = source.ReadPackedQuaternion(VectorBinaryEncoding.Int16);
        var worldScale = source.ReadVector3();
        var worldTransform = new Matrix3x4(worldPosition, worldRotation, worldScale);
        var localTransform = Node.IsTransformHierarchyRoot()
            ? worldTransform
            : Node.Parent!.GetWorldTransform().Inverse * worldTransform;
        Node.SetTransformMatrix(localTransform);
    }

    [Serialized("Client Prefab", typeof(ResourceRef),
        DefaultSource = nameof(GetClientPrefabAttributeDefault), Setter = nameof(SetClientPrefabAttribute))]
    private ResourceRef GetClientPrefabAttribute() => new(
        typeof(PrefabResource).GetClassName(), _clientPrefab?.GetName() ?? string.Empty);

    private static ResourceRef GetClientPrefabAttributeDefault() =>
        new(typeof(PrefabResource).GetClassName());

    private void SetClientPrefabAttribute(ResourceRef value)
    {
        var cache = Context.GetSubsystem<ResourceCache>()!;
        SetClientPrefab(value.Name.Length != 0
            ? cache.GetResourceOrNull<PrefabResource>(value.Name)
            : null);
    }

    private PrefabResource? _clientPrefab;
}
