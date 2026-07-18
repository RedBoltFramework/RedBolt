using SharpCpp.Model.Ast;
using SharpCpp.Rbfx.Generator;

using Xunit;

namespace SharpCpp.Tests.Model;

public sealed class RbfxManagedNamespaceProjectionPassTests
{
    [Fact]
    public void Apply_projects_urho_header_segments_without_reparenting_original_ast()
    {
        var compilation = new BindingCompilation("SharpCpp.Rbfx.Bindings", "Urho3D", "Bindings", "Native", ["Urho3D/Urho3DAll.h"]);
        var context = new RecordBinding("record:Context", "Context", "Context", "Urho3D::Context")
        {
            HeaderIncludePath = "Urho3D/Core/Context.h",
            ManagedNamespace = "Urho3D"
        };
        var scene = new RecordBinding("record:Scene", "Scene", "Scene", "Urho3D::Scene")
        {
            HeaderIncludePath = "Urho3D/Scene/Scene.h",
            ManagedNamespace = "Urho3D"
        };
        var rigidBody = new RecordBinding("record:RigidBody", "RigidBody", "RigidBody", "Urho3D::RigidBody")
        {
            HeaderIncludePath = "Physics/RigidBody.h",
            ManagedNamespace = "Urho3D"
        };
        var vector = new RecordBinding("record:Vector", "vector", "Vector", "eastl::vector")
        {
            HeaderIncludePath = "Urho3D/Container/Vector.h",
            ManagedNamespace = "Ea"
        };
        var mode = new EnumBinding("enum:Mode", "Mode", "Mode", cppQualifiedName: "Urho3D::Mode")
        {
            HeaderIncludePath = "Urho3D/Core/Mode.h",
            ManagedNamespace = "Urho3D"
        };
        compilation.Members.Add(context);
        compilation.Members.Add(scene);
        compilation.Members.Add(rigidBody);
        compilation.Members.Add(vector);
        compilation.Members.Add(mode);
        var alias = new BindingTypeAlias("Urho3D::RigidBodyPtr", "RigidBodyPtr", "RigidBodyPtr", rigidBody.HandleType)
        {
            ManagedNamespace = "Urho3D"
        };
        compilation.TypeAliases.Add(alias);

        var projection = RbfxManagedNamespaceProjectionPass.Apply(compilation);

        Assert.Same(compilation, context.Parent);
        Assert.Same(compilation, scene.Parent);
        Assert.Same(compilation, rigidBody.Parent);
        Assert.Same(compilation, mode.Parent);
        Assert.Equal(new[] { context, scene, rigidBody, vector }, compilation.Records);
        Assert.True(projection.TryGetManagedNamespace(context, out var contextNamespace));
        Assert.True(projection.TryGetManagedNamespace(scene, out var sceneNamespace));
        Assert.True(projection.TryGetManagedNamespace(rigidBody, out var rigidBodyNamespace));
        Assert.True(projection.TryGetManagedNamespace(vector, out var vectorNamespace));
        Assert.True(projection.TryGetManagedNamespace(mode, out var modeNamespace));
        Assert.Equal("Urho3D.Core", contextNamespace);
        Assert.Equal("Urho3D.Scene", sceneNamespace);
        Assert.Equal("Urho3D.Physics", rigidBodyNamespace);
        Assert.Equal("Ea", vectorNamespace);
        Assert.Equal("Urho3D.Core", modeNamespace);
        Assert.Contains(projection.Root.DescendantsAndSelf(), ns => ns.FullName == "Urho3D.Core");
        Assert.Contains(projection.Root.DescendantsAndSelf(), ns => ns.FullName == "Urho3D.Scene");
        Assert.Contains(projection.Root.DescendantsAndSelf(), ns => ns.FullName == "Urho3D.Physics");
    }

}
