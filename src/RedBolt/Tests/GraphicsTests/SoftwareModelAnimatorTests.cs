using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;
namespace RedBolt.Tests;

public sealed class SoftwareModelAnimatorTests
{
    [Fact]
    public void Class_shapes_registration_and_animation_data_copy_match_cpp()
    {
        Assert.True(typeof(VertexBufferAnimationData).IsValueType);
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(VertexBufferAnimationData)));
        Assert.False(typeof(SoftwareModelAnimator).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(SoftwareModelAnimator)));

        var animationData = new VertexBufferAnimationData
        {
            BlendWeights = [0.25f],
            BlendIndices = [3]
        };
        var clone = Assert.IsType<VertexBufferAnimationData>(animationData.Clone());
        Assert.NotSame(animationData.BlendWeights, clone.BlendWeights);
        Assert.NotSame(animationData.BlendIndices, clone.BlendIndices);

        SoftwareModelAnimator.RegisterObject(Context.Instance);
        using var reflected = Assert.IsType<SoftwareModelAnimator>(
            Context.Instance.CreateObject(ObjectReflection<SoftwareModelAnimator>.TypeId));
    }

    [Fact]
    public void Morph_animation_clones_resets_applies_and_commits_vertex_data()
    {
        var originalBuffer = CreateVertexBuffer(VertexMask.Position, new Vector3f(1.0f, 2.0f, 3.0f));
        var model = new Model { Name = "Morph" };
        Assert.True(model.SetVertexBuffers([originalBuffer], [0], [1]));
        var morphData = new byte[sizeof(uint) + 3 * sizeof(float)];
        Write(morphData, 0, 0u);
        Write(morphData, sizeof(uint), new Vector3f(2.0f, -1.0f, 4.0f));
        var morph = new ModelMorph { Weight = 0.5f };
        morph.Buffers[0] = new VertexBufferMorph
        {
            ElementMask = VertexMask.Position,
            VertexCount = 1,
            DataSize = (uint)morphData.Length,
            MorphData = morphData
        };
        model.SetMorphs([morph]);
        var animator = new SoftwareModelAnimator();

        animator.Initialize(model, false, SoftwareModelAnimator.MaxBones);
        animator.ResetAnimation();
        animator.ApplyMorphs([morph]);
        animator.Commit();

        var clonedBuffer = Assert.IsType<VertexBuffer>(Assert.Single(animator.GetVertexBuffers()));
        Assert.NotSame(originalBuffer, clonedBuffer);
        Assert.Equal(new Vector3f(2.0f, 1.5f, 5.0f), Read<Vector3f>(clonedBuffer.GetShadowData(), 0));

        animator.Dispose();
        Assert.Empty(animator.GetGeometries());
        Assert.Empty(animator.GetVertexBuffers());
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), Read<Vector3f>(originalBuffer.GetShadowData(), 0));
    }

    [Fact]
    public void Skeletal_animation_uses_blend_indices_and_weights()
    {
        var elements = VertexBuffer.GetElements((uint)(VertexMask.Position | VertexMask.BlendWeights | VertexMask.BlendIndices));
        var originalBuffer = new VertexBuffer();
        originalBuffer.SetShadowed(true);
        Assert.True(originalBuffer.SetSize(1, elements));
        var data = new byte[checked((int)originalBuffer.GetVertexSize())];
        Write(data, 0, new Vector3f(1.0f, 0.0f, 0.0f));
        var weightsOffset = checked((int)originalBuffer.GetElementOffset(VertexElementSemantic.BlendWeights));
        Write(data, weightsOffset, new Vector4f(1.0f, 0.0f, 0.0f, 0.0f));
        var indicesOffset = checked((int)originalBuffer.GetElementOffset(VertexElementSemantic.BlendIndices));
        data[indicesOffset] = 0;
        originalBuffer.Update(data);
        var model = new Model { Name = "Skinning" };
        Assert.True(model.SetVertexBuffers([originalBuffer]));
        using var animator = new SoftwareModelAnimator();

        animator.Initialize(model, true, SoftwareModelAnimator.MaxBones);
        animator.ResetAnimation();
        animator.ApplySkinning([
            Matrix3x4.FromTranslation(new Vector3f(2.0f, 0.0f, 0.0f)),
            Matrix3x4.Identity,
            Matrix3x4.Identity,
            Matrix3x4.Identity
        ]);
        animator.Commit();

        var clonedBuffer = Assert.IsType<VertexBuffer>(Assert.Single(animator.GetVertexBuffers()));
        Assert.Equal(new Vector3f(3.0f, 0.0f, 0.0f), Read<Vector3f>(clonedBuffer.GetShadowData(), 0));
    }

    [Fact]
    public void Reduced_bone_count_sorts_equal_weights_like_cpp_pair()
    {
        var elements = VertexBuffer.GetElements((uint)(VertexMask.Position | VertexMask.BlendWeights | VertexMask.BlendIndices));
        var originalBuffer = new VertexBuffer();
        originalBuffer.SetShadowed(true);
        Assert.True(originalBuffer.SetSize(1, elements));
        var data = new byte[checked((int)originalBuffer.GetVertexSize())];
        Write(data, 0, Vector3f.Zero);
        var weightsOffset = checked((int)originalBuffer.GetElementOffset(VertexElementSemantic.BlendWeights));
        Write(data, weightsOffset, new Vector4f(0.25f, 0.25f, 0.25f, 0.25f));
        var indicesOffset = checked((int)originalBuffer.GetElementOffset(VertexElementSemantic.BlendIndices));
        data[indicesOffset] = 0;
        data[indicesOffset + 1] = 1;
        data[indicesOffset + 2] = 2;
        data[indicesOffset + 3] = 3;
        originalBuffer.Update(data);
        var model = new Model { Name = "ReducedSkinning" };
        Assert.True(model.SetVertexBuffers([originalBuffer]));
        using var animator = new SoftwareModelAnimator();

        animator.Initialize(model, true, 2);
        animator.ResetAnimation();
        animator.ApplySkinning([
            Matrix3x4.Identity,
            Matrix3x4.FromTranslation(new Vector3f(10.0f, 0.0f, 0.0f)),
            Matrix3x4.FromTranslation(new Vector3f(20.0f, 0.0f, 0.0f)),
            Matrix3x4.FromTranslation(new Vector3f(30.0f, 0.0f, 0.0f))
        ]);
        animator.Commit();

        var clonedBuffer = Assert.IsType<VertexBuffer>(Assert.Single(animator.GetVertexBuffers()));
        Assert.Equal(new Vector3f(25.0f, 0.0f, 0.0f), Read<Vector3f>(clonedBuffer.GetShadowData(), 0));
    }

    [Fact]
    public void Initialization_clones_geometry_and_appends_animated_vertex_stream()
    {
        var originalBuffer = CreateVertexBuffer(VertexMask.Position, Vector3f.Zero);
        var indexBuffer = new IndexBuffer();
        indexBuffer.SetShadowed(true);
        Assert.True(indexBuffer.SetSize(1));
        indexBuffer.SetUnpackedData([0u]);
        var originalGeometry = new Geometry();
        originalGeometry.SetVertexBuffers([originalBuffer]);
        originalGeometry.SetIndexBuffer(indexBuffer);
        Assert.True(originalGeometry.SetDrawRange(PrimitiveType.PointList, 0, 1));
        originalGeometry.SetLodDistance(12.0f);
        var model = new Model { Name = "Geometry" };
        Assert.True(model.SetVertexBuffers([originalBuffer], [0], [1]));
        model.SetNumGeometries(1);
        Assert.True(model.SetGeometry(0, 0, originalGeometry));
        var morph = new ModelMorph();
        morph.Buffers[0] = new VertexBufferMorph { ElementMask = VertexMask.Position };
        model.SetMorphs([morph]);
        using var animator = new SoftwareModelAnimator();

        animator.Initialize(model, false, SoftwareModelAnimator.MaxBones);

        var clonedGeometry = Assert.IsType<Geometry>(Assert.Single(Assert.Single(animator.GetGeometries())));
        Assert.NotSame(originalGeometry, clonedGeometry);
        Assert.Same(indexBuffer, clonedGeometry.GetIndexBuffer());
        Assert.Equal(2u, clonedGeometry.GetNumVertexBuffers());
        Assert.Same(originalBuffer, clonedGeometry.GetVertexBuffer(0));
        Assert.Same(Assert.Single(animator.GetVertexBuffers()), clonedGeometry.GetVertexBuffer(1));
        Assert.Equal(PrimitiveType.PointList, clonedGeometry.GetPrimitiveType());
        Assert.Equal(12.0f, clonedGeometry.GetLodDistance());
    }

    private static VertexBuffer CreateVertexBuffer(VertexMask mask, Vector3f position)
    {
        var buffer = new VertexBuffer();
        buffer.SetShadowed(true);
        Assert.True(buffer.SetSize(1, mask));
        var data = new byte[checked((int)buffer.GetVertexSize())];
        Write(data, 0, position);
        buffer.Update(data);
        return buffer;
    }

    private static T Read<T>(IReadOnlyList<byte> data, int offset) where T : struct
    {
        var bytes = new byte[Unsafe.SizeOf<T>()];
        for (var i = 0; i < bytes.Length; ++i)
            bytes[i] = data[offset + i];
        return MemoryMarshal.Read<T>(bytes);
    }

    private static void Write<T>(Span<byte> data, int offset, T value) where T : struct =>
        MemoryMarshal.Write(data[offset..], in value);
}
