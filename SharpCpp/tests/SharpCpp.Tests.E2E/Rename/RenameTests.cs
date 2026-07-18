using SharpCpp.Tests.E2E.Generated;

using Xunit;

namespace SharpCpp.Tests.E2E.Rename;

public sealed class RenameTests
{
    [Fact]
    public void Record_renamed_type_emits_with_new_name()
    {
        var type = typeof(RenamedStruct);
        Assert.Equal("RenamedStruct", type.Name);

        var instance = new RenamedStruct();
        Assert.NotNull(instance);
    }

    [Fact]
    public void Original_struct_name_is_not_emitted()
    {
        var exportedTypes = typeof(E2EBindings).Assembly.GetExportedTypes();
        Assert.DoesNotContain(exportedTypes, type => type.Name == "OriginalStruct");
    }

    [Fact]
    public void Generic_record_renamed_with_format_placeholder()
    {
        var type = typeof(ContainerOf<int>);
        Assert.Equal("ContainerOf`1", type.Name);

        var holder = E2EBindings.MakeIntHolder(42);
        Assert.Equal(42, holder.GetItem());
    }

    [Fact]
    public void Enum_renamed_type_emits_with_new_name()
    {
        var type = typeof(SharpCpp.Tests.E2E.Generated.DayOfWeek);
        Assert.True(type.IsEnum);
        Assert.Equal("DayOfWeek", type.Name);
    }

    [Fact]
    public void Free_function_renamed_emits_with_new_name()
    {
        var method = typeof(E2EBindings).GetMethod("AddValues");
        Assert.NotNull(method);

        var result = (int)method!.Invoke(null, new object[] { 3, 4 })!;
        Assert.Equal(7, result);
    }

    [Fact]
    public void Instance_method_renamed_emits_with_new_name()
    {
        var type = typeof(ApiObject);
        var method = type.GetMethod("GetCount");
        Assert.NotNull(method);
        Assert.DoesNotContain(type.GetMethods(), m => m.Name == "FetchCount");

        var instance = new ApiObject();
        Assert.Equal(0, instance.GetCount());
    }

    [Fact]
    public void Static_method_renamed_emits_with_new_name()
    {
        var type = typeof(ApiObject);
        var method = type.GetMethod("MakeDefault");
        Assert.NotNull(method);
        Assert.DoesNotContain(type.GetMethods(), m => m.Name == "CreateDefault");
    }

    [Fact]
    public void Field_renamed_emits_with_new_name()
    {
        var type = typeof(ApiObject);
        var property = type.GetProperty("ItemCount");
        Assert.NotNull(property);
        Assert.Null(type.GetProperty("Count_"));
    }

    [Fact]
    public void Record_renamed_with_name_placeholder_uses_pascal_case_substitution()
    {
        var type = typeof(DataStoreWrapper);
        Assert.Equal("DataStoreWrapper", type.Name);
    }
}
