namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Resource;
using RedBolt.Scene;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class VisualRegressionTextureTests
{
    [VisualFact]
    public void Texture_2d_array_layers_select_stable_native_images()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.02f, 0.02f, 0.02f));
        var node = scene.CreateChild("ArrayPreview");
        node.SetScale(1.7f);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(fixture.Resources.Get<Model>("Models/Box.mdl"));
        model.SetMaterial(fixture.Resources.Get<Material>("Materials/Showcase/PreviewTexture2DArray.xml"));
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(3.2f, 2.3f, -5.0f), Vector3f.Zero, 256.0f / 192.0f, fov: 40.0f);

        var firstLayer = fixture.Capture(scene, camera);
        node.SetPosition(new Vector3f(0.0f, 0.0f, 0.1f));
        var middleLayer = fixture.Capture(scene, camera);
        node.SetPosition(new Vector3f(0.0f, 0.0f, 0.2f));
        var lastLayer = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(firstLayer, "textures/array-layer-first.png"),
            new VisualRegressionImageCapture(middleLayer, "textures/array-layer-middle.png"),
            new VisualRegressionImageCapture(lastLayer, "textures/array-layer-last.png"));
    }

    [VisualFact]
    public void Texture_3d_volume_sampling_changes_with_world_slice()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.02f, 0.02f, 0.02f));
        var node = scene.CreateChild("VolumePreview");
        node.SetPosition(new Vector3f(0.5f, 0.5f, 0.2f));
        node.SetScale(1.2f);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(fixture.Resources.Get<Model>("Models/Box.mdl"));
        model.SetMaterial(fixture.Resources.Get<Material>("Materials/Showcase/PreviewTexture3D.xml"));
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(3.2f, 2.3f, -5.0f), new Vector3f(0.5f, 0.5f, 0.2f),
            256.0f / 192.0f, fov: 40.0f);

        var nearSlice = fixture.Capture(scene, camera);
        node.SetPosition(new Vector3f(0.5f, 0.5f, 0.55f));
        var middleSlice = fixture.Capture(scene, camera);
        node.SetPosition(new Vector3f(0.5f, 0.5f, 0.9f));
        var farSlice = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(nearSlice, "textures/volume-slice-near.png"),
            new VisualRegressionImageCapture(middleSlice, "textures/volume-slice-middle.png"),
            new VisualRegressionImageCapture(farSlice, "textures/volume-slice-far.png"));
    }

    [VisualFact]
    public void Texture_cube_faces_and_orientation_render_as_a_skybox()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(Color.Black);
        using var cube = CreateFaceColorCube();
        using var material = fixture.Resources.Get<Material>("Materials/DefaultSkybox.xml").Clone(
            "VisualRegression.FaceColorSkybox");
        material.SetTexture("Albedo", cube);
        var skyNode = scene.CreateChild("FaceColorSkybox");
        skyNode.SetScale(10.0f);
        skyNode.SetRotation(new Quaternion(10.0f, 25.0f, 0.0f));
        var skybox = skyNode.CreateComponent<Skybox>();
        skybox.SetModel(fixture.Resources.Get<Model>("Models/Box.mdl"));
        skybox.SetMaterial(material);

        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, 0.0f), Vector3f.Forward, 256.0f / 192.0f,
            fov: 70.0f);
        var firstOrientation = fixture.Capture(scene, camera);
        skyNode.SetRotation(new Quaternion(10.0f, 115.0f, 0.0f));
        var rotatedOrientation = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(firstOrientation, "textures/cube-faces.png"),
            new VisualRegressionImageCapture(rotatedOrientation, "textures/cube-rotated.png"));
    }

    [VisualFact]
    public void Texture_formats_render_representative_unorm_packed_float_and_compressed_data()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.02f, 0.02f, 0.02f));
        var node = scene.CreateChild("FormatPreview");
        node.SetScale(1.5f);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(fixture.Resources.Get<Model>("Models/Plane.mdl"));
        using var material = CreateUnlitTextureMaterial(fixture.Resources,
            "Textures/Formats/RGBA.dds");
        model.SetMaterial(material);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(3.4f, 2.8f, -5.5f), Vector3f.Zero, 256.0f / 192.0f, fov: 42.0f);

        var unorm = fixture.Capture(scene, camera);
        material.SetTexture("Albedo", fixture.Resources.Get<Texture2D>("Textures/Formats/DXT1.dds"));
        var compressed = fixture.Capture(scene, camera);
        material.SetTexture("Albedo", fixture.Resources.Get<Texture2D>("Textures/Formats/RGB10A2.dds"));
        var packed = fixture.Capture(scene, camera);
        material.SetTexture("Albedo", fixture.Resources.Get<Texture2D>("Textures/Formats/RGBA_F16.dds"));
        var floatingPoint = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(unorm, "textures/format-rgba8.png"),
            new VisualRegressionImageCapture(compressed, "textures/format-dxt1.png"),
            new VisualRegressionImageCapture(packed, "textures/format-rgb10a2.png"),
            new VisualRegressionImageCapture(floatingPoint, "textures/format-rgba-f16.png"));
    }

    private static Material CreateUnlitTextureMaterial(ResourceCache resources, string textureName)
    {
        var material = new Material { Name = "VisualRegression.TextureFormat" };
        material.SetTechnique(0, resources.Get<Technique>("Techniques/DiffUnlit.xml"));
        material.SetTexture("Albedo", resources.Get<Texture2D>(textureName));
        material.SetShaderParameter("MatDiffColor", Vector4f.One);
        return material;
    }

    private static TextureCube CreateFaceColorCube()
    {
        var cube = new TextureCube { Name = "VisualRegression.FaceColorCube" };
        foreach (var face in Enum.GetValues<CubeMapFace>())
        {
            if (face == CubeMapFace.MaxCubeMapFaces)
                continue;
            using var image = new Image();
            Assert.True(image.SetSize(16, 16, 4));
            var color = face switch
            {
                CubeMapFace.PositiveX => new Color(1.0f, 0.1f, 0.1f),
                CubeMapFace.NegativeX => new Color(0.1f, 1.0f, 0.1f),
                CubeMapFace.PositiveY => new Color(0.1f, 0.1f, 1.0f),
                CubeMapFace.NegativeY => new Color(1.0f, 1.0f, 0.1f),
                CubeMapFace.PositiveZ => new Color(1.0f, 0.1f, 1.0f),
                _ => new Color(0.1f, 1.0f, 1.0f)
            };
            var pixel = new byte[]
            {
                (byte)(color.R * 255.0f), (byte)(color.G * 255.0f),
                (byte)(color.B * 255.0f), 255
            };
            var data = new byte[16 * 16 * 4];
            for (var i = 0; i < data.Length; i += 4)
                pixel.CopyTo(data, i);
            image.SetData(data);
            Assert.True(cube.SetData(face, image));
        }
        return cube;
    }
}
