using RedBolt.Scene;

namespace RedBolt.Tests;

public sealed class PrefabTypesTests
{
    [Fact]
    public void Prefab_ids_use_the_empty_value()
    {
        Assert.Equal(0u, (uint)AttributeId.None);
        Assert.Equal(0u, (uint)SerializableId.None);
    }

    [Fact]
    public void To_node_flags_requires_type_and_keeps_id()
    {
        var result = PrefabTypes.ToNodeFlags(PrefabArchiveFlag.None);

        Assert.True((result & PrefabArchiveFlag.IgnoreSerializableType) != 0);
        Assert.False((result & PrefabArchiveFlag.IgnoreSerializableId) != 0);
    }

    [Fact]
    public void To_component_flags_preserves_the_source_expression()
    {
        var flags = PrefabArchiveFlag.IgnoreSerializableId | PrefabArchiveFlag.IgnoreSerializableType;

        var result = PrefabTypes.ToComponentFlags(flags);

        Assert.Equal(PrefabArchiveFlag.IgnoreSerializableId, result);
        Assert.False((result & PrefabArchiveFlag.IgnoreSerializableType) != 0);
    }
}
