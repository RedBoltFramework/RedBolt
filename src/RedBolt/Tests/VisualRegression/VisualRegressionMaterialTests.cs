namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;
using RedBolt.Scene;
using RedBolt.Core;

[Collection(nameof(GraphicsLifecycleCollection))]
public sealed class VisualRegressionMaterialTests
{
    [VisualFact]
    public void Material_lighting_modes_render_unlit_vertex_and_pixel_variants()
    {
        using var fixture = new VisualRegressionFixture();

        var pixel = CaptureMaterial(fixture, "Materials/Constant/MattWhite.xml");
        var unlit = CaptureMaterial(fixture, "Materials/Constant/UnlitWhite.xml");
        var vertex = CaptureMaterial(fixture, "Materials/VColUnlit.xml");

        fixture.AssertMatches(
            new VisualRegressionImageCapture(pixel, "materials/lighting-per-pixel.png"),
            new VisualRegressionImageCapture(unlit, "materials/lighting-unlit.png"),
            new VisualRegressionImageCapture(vertex, "materials/lighting-per-vertex.png"));
    }

    [VisualFact]
    public void Material_quality_and_technique_lod_select_the_expected_entries()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = CreateLitScene(fixture, out var camera);
        using var pipeline = scene.CreateComponent<RenderPipeline>();
        var model = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Sphere.mdl", "Materials/Stone.xml", Vector3f.Zero, 1.4f);

        SetMaterialQuality(pipeline, MaterialQuality.Low);
        var low = fixture.Capture(scene, camera);
        SetMaterialQuality(pipeline, MaterialQuality.Medium);
        var medium = fixture.Capture(scene, camera);
        SetMaterialQuality(pipeline, MaterialQuality.High);
        var high = fixture.Capture(scene, camera);

        using var lodMaterial = CreateTechniqueLodMaterial(fixture.Resources);
        model.SetMaterial(lodMaterial);
        var lod = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(low, "materials/quality-low.png"),
            new VisualRegressionImageCapture(medium, "materials/quality-medium.png"),
            new VisualRegressionImageCapture(high, "materials/quality-high.png"),
            new VisualRegressionImageCapture(lod, "materials/technique-lod.png"));
    }

    [VisualFact]
    public void Renderer_texture_quality_matrix_changes_native_texture_sampling()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.08f, 0.08f, 0.08f));
        var material = fixture.Resources.Get<Material>("Materials/StoneTiled");
        var node = scene.CreateChild("TextureQualitySurface");
        node.SetPosition(new Vector3f(0.0f, -1.0f, 2.0f));
        node.SetScale(6.0f);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(fixture.Resources.Get<Model>("Models/Plane.mdl"));
        model.SetMaterial(material);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(4.5f, 3.2f, -5.0f), new Vector3f(0.0f, -1.0f, 2.0f),
            256.0f / 192.0f, fov: 42.0f);

        fixture.Renderer.SetTextureQuality(MaterialQuality.Low);
        var low = fixture.Capture(scene, camera);
        fixture.Renderer.SetTextureQuality(MaterialQuality.Medium);
        var medium = fixture.Capture(scene, camera);
        fixture.Renderer.SetTextureQuality(MaterialQuality.High);
        var high = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(low, "materials/texture-quality-low.png"),
            new VisualRegressionImageCapture(medium, "materials/texture-quality-medium.png"),
            new VisualRegressionImageCapture(high, "materials/texture-quality-high.png"));
    }

    [VisualFact]
    public void Material_shader_parameters_and_animation_change_surface_response()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = CreateLitScene(fixture, out var camera);
        using var material = fixture.Resources.Get<Material>("Materials/PBRMushroom.xml").Clone(
            "VisualRegression.MaterialParameters");
        var model = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Sphere.mdl", "Materials/PBRMushroom.xml", Vector3f.Zero, 1.35f);
        model.SetMaterial(material);

        material.SetShaderParameter("MatDiffColor", new Vector4f(0.25f, 0.7f, 1.0f, 1.0f));
        material.SetShaderParameter("Metallic", 0.0f);
        material.SetShaderParameter("Roughness", 0.9f);
        var matte = fixture.Capture(scene, camera);

        material.SetShaderParameter("MatDiffColor", new Vector4f(1.0f, 0.25f, 0.1f, 1.0f));
        material.SetShaderParameter("Metallic", 1.0f);
        material.SetShaderParameter("Roughness", 0.15f);
        material.SetShaderParameter("MatEmissiveColor", new Vector3f(0.2f, 0.02f, 0.0f));
        var metallic = fixture.Capture(scene, camera);

        var animation = new ValueAnimation { Name = "VisualRegression.MaterialColorAnimation" };
        animation.ValueType = VariantType.Vector4;
        animation.SetKeyFrame(0.0f, new Vector4f(0.05f, 0.2f, 1.0f, 1.0f));
        animation.SetKeyFrame(1.0f, new Vector4f(1.0f, 0.8f, 0.05f, 1.0f));
        material.SetShaderParameter("MatDiffColor", Vector4f.One);
        material.SetShaderParameterAnimation("MatDiffColor", animation, WrapMode.Once, 1.0f);
        material.GetShaderParameterAnimationInfo("MatDiffColor")!.SetTime(0.5f);
        var animated = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(matte, "materials/parameters-matte.png"),
            new VisualRegressionImageCapture(metallic, "materials/parameters-metallic-emissive.png"),
            new VisualRegressionImageCapture(animated, "materials/parameters-animated.png"));
    }

    [VisualFact]
    public void Material_texture_slots_and_normal_tangent_mapping_render_distinct_variants()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = CreateLitScene(fixture, out var camera);
        using var normal = fixture.Resources.Get<Material>("Materials/Showcase/Stone.xml").Clone(
            "VisualRegression.NormalMapped");
        using var flat = normal.Clone("VisualRegression.FlatMapped");
        flat.SetTexture("Normal", null);
        using var pbr = fixture.Resources.Get<Material>("Materials/CheckboardProperties.xml").Clone(
            "VisualRegression.PropertiesMapped");

        var node = scene.CreateChild("Surface");
        node.SetScale(1.5f);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(fixture.Resources.Get<Model>("Models/Sphere.mdl"));
        model.SetMaterial(normal);
        var normalImage = fixture.Capture(scene, camera);

        model.SetMaterial(flat);
        var flatImage = fixture.Capture(scene, camera);

        model.SetMaterial(pbr);
        var propertiesImage = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(normalImage, "materials/texture-slots-normal.png"),
            new VisualRegressionImageCapture(flatImage, "materials/texture-slots-flat.png"),
            new VisualRegressionImageCapture(propertiesImage, "materials/texture-slots-properties.png"));
    }

    [VisualFact]
    public void Material_color_space_hints_change_texture_decode()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = CreateLitScene(fixture, out var camera);
        using var material = fixture.Resources.Get<Material>("Materials/Stone.xml")
            .Clone("VisualRegression.TextureColorSpace");
        using var sourceImage = CreateColorSpaceSourceImage();
        using var linearTexture = CreateColorSpaceTexture(sourceImage, linear: true, srgb: false);
        using var srgbTexture = CreateColorSpaceTexture(sourceImage, linear: false, srgb: true);
        var model = VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Sphere.mdl", "Materials/Stone.xml", Vector3f.Zero, 1.35f);
        model.SetMaterial(material);

        material.SetTexture("Albedo", linearTexture);
        var linear = fixture.Capture(scene, camera);
        material.SetTexture("Albedo", srgbTexture);
        var srgb = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(linear, "materials/color-space-linear-texture.png"),
            new VisualRegressionImageCapture(srgb, "materials/color-space-srgb-texture.png"));
    }

    [VisualFact]
    public void Texture_filter_mip_lod_and_addressing_controls_render_at_native_resolution()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.08f, 0.08f, 0.08f));
        using var material = fixture.Resources.Get<Material>("Materials/StoneTiled.xml").Clone(
            "VisualRegression.TextureSampling");
        var albedo = Assert.IsType<Texture2D>(material.GetTexture("Albedo"));
        var normal = Assert.IsType<Texture2D>(material.GetTexture("Normal"));
        albedo.SetAddressMode(TextureCoordinate.U, TextureAddressMode.Mirror);
        albedo.SetAddressMode(TextureCoordinate.V, TextureAddressMode.Wrap);
        normal.SetAddressMode(TextureCoordinate.U, TextureAddressMode.Mirror);
        normal.SetAddressMode(TextureCoordinate.V, TextureAddressMode.Wrap);

        var node = scene.CreateChild("TiledSurface");
        node.SetPosition(new Vector3f(0.0f, -1.0f, 2.0f));
        node.SetScale(6.0f);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(fixture.Resources.Get<Model>("Models/Plane.mdl"));
        model.SetMaterial(material);
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(4.5f, 3.2f, -5.0f), new Vector3f(0.0f, -1.0f, 2.0f),
            256.0f / 192.0f, fov: 42.0f);

        var nearest = CaptureWithSampling(fixture, scene, camera, albedo, normal,
            TextureFilterMode.Nearest, 0.0f, 8.0f);
        var bilinear = CaptureWithSampling(fixture, scene, camera, albedo, normal,
            TextureFilterMode.Bilinear, 0.0f, 8.0f);
        var trilinear = CaptureWithSampling(fixture, scene, camera, albedo, normal,
            TextureFilterMode.Trilinear, 1.0f, 8.0f);
        var anisotropic = CaptureWithSampling(fixture, scene, camera, albedo, normal,
            TextureFilterMode.Anisotropic, 0.0f, 8.0f);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(nearest, "materials/filter-nearest.png"),
            new VisualRegressionImageCapture(bilinear, "materials/filter-bilinear.png"),
            new VisualRegressionImageCapture(trilinear, "materials/filter-trilinear-mip1.png"),
            new VisualRegressionImageCapture(anisotropic, "materials/filter-anisotropic.png"));
    }

    [VisualFact]
    public void Material_cull_fill_depth_and_color_write_states_render_expected_surfaces()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = CreateLitScene(fixture, out var camera);
        using var baseMaterial = fixture.Resources.Get<Material>("Materials/Constant/MattWhiteTwoSided.xml").Clone(
            "VisualRegression.RenderStates");
        var node = scene.CreateChild("StateMesh");
        node.SetScale(1.5f);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(fixture.Resources.Get<Model>("Models/Box.mdl"));
        model.SetMaterial(baseMaterial);

        baseMaterial.SetCullMode(CullMode.None);
        baseMaterial.SetFillMode(FillMode.Solid);
        var solid = fixture.Capture(scene, camera);

        baseMaterial.SetFillMode(FillMode.Wireframe);
        var wire = fixture.Capture(scene, camera);

        baseMaterial.SetFillMode(FillMode.Point);
        var points = fixture.Capture(scene, camera);

        baseMaterial.SetFillMode(FillMode.Solid);
        baseMaterial.SetCullMode(CullMode.Clockwise);
        var clockwise = fixture.Capture(scene, camera);

        var pass = Assert.IsType<Pass>(baseMaterial.GetDefaultPass());
        pass.SetDepthTestMode(CompareMode.Always);
        pass.SetDepthWrite(false);
        pass.SetColorWrite(false);
        var depthOnly = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(solid, "materials/state-solid-two-sided.png"),
            new VisualRegressionImageCapture(wire, "materials/state-wireframe.png"),
            new VisualRegressionImageCapture(points, "materials/state-points.png"),
            new VisualRegressionImageCapture(clockwise, "materials/state-cull-clockwise.png"),
            new VisualRegressionImageCapture(depthOnly, "materials/state-depth-no-color.png"));
    }

    [VisualFact]
    public void Material_depth_compare_modes_render_against_a_known_depth_buffer()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = CreateLitScene(fixture, out var camera);
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/Constant/MattWhite.xml",
            new Vector3f(0.0f, 0.0f, 0.9f), 1.8f);
        using var material = fixture.Resources.Get<Material>("Materials/Constant/MattTransparent.xml")
            .Clone("VisualRegression.DepthCompareModes");
        material.SetShaderParameter("MatDiffColor", new Vector4f(1.0f, 0.15f, 0.05f, 0.55f));
        var node = scene.CreateChild("DepthCompareBox");
        node.SetPosition(new Vector3f(0.0f, 0.0f, -0.1f));
        node.SetScale(1.4f);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(fixture.Resources.Get<Model>("Models/Box.mdl"));
        model.SetMaterial(material);
        var pass = Assert.IsType<Pass>(material.GetTechnique(0)?.GetPass("alpha"));

        var captures = new List<VisualRegressionImageCapture>(8);
        foreach (var (mode, name) in new[]
        {
            (CompareMode.Always, "always"),
            (CompareMode.Equal, "equal"),
            (CompareMode.NotEqual, "not-equal"),
            (CompareMode.Less, "less"),
            (CompareMode.LessEqual, "less-equal"),
            (CompareMode.Greater, "greater"),
            (CompareMode.GreaterEqual, "greater-equal")
        })
        {
            pass.SetDepthTestMode(mode);
            pass.SetDepthWrite(true);
            captures.Add(new VisualRegressionImageCapture(
                fixture.Capture(scene, camera), $"materials/depth-{name}.png"));
        }

        pass.SetDepthTestMode(CompareMode.Less);
        pass.SetDepthWrite(false);
        captures.Add(new VisualRegressionImageCapture(
            fixture.Capture(scene, camera), "materials/depth-less-no-write.png"));

        fixture.AssertMatches(captures.ToArray());
    }

    [VisualFact]
    public void Material_blend_modes_and_transparency_render_in_a_fixed_depth_order()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.03f, 0.03f, 0.03f));
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/Constant/MattWhite.xml",
            new Vector3f(0.0f, 0.0f, 1.0f), 1.8f);
        using var material = fixture.Resources.Get<Material>("Materials/Constant/MattTransparent.xml").Clone(
            "VisualRegression.BlendModes");
        material.SetShaderParameter("MatDiffColor", new Vector4f(1.0f, 0.15f, 0.05f, 0.55f));
        var node = scene.CreateChild("TransparentBox");
        node.SetPosition(new Vector3f(0.0f, 0.0f, -0.1f));
        node.SetScale(1.4f);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(fixture.Resources.Get<Model>("Models/Box.mdl"));
        model.SetMaterial(material);
        var pass = Assert.IsType<Pass>(material.GetTechnique(0)?.GetPass("alpha"));
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(3.5f, 2.5f, -6.0f), Vector3f.Zero, 256.0f / 192.0f, fov: 40.0f);

        var captures = new List<VisualRegressionImageCapture>(10);
        foreach (var (mode, name) in new[]
        {
            (BlendMode.Replace, "replace"),
            (BlendMode.Add, "add"),
            (BlendMode.Multiply, "multiply"),
            (BlendMode.Alpha, "alpha"),
            (BlendMode.AddAlpha, "add-alpha"),
            (BlendMode.PremulAlpha, "premul-alpha"),
            (BlendMode.InvDestAlpha, "inverse-destination-alpha"),
            (BlendMode.Subtract, "subtract"),
            (BlendMode.SubtractAlpha, "subtract-alpha"),
            (BlendMode.Decal, "decal")
        })
        {
            pass.SetBlendMode(mode);
            captures.Add(new VisualRegressionImageCapture(
                fixture.Capture(scene, camera), $"materials/blend-{name}.png"));
        }

        fixture.AssertMatches(captures.ToArray());
    }

    [VisualFact]
    public void Material_alpha_cutout_translucency_and_coverage_render_stably()
    {
        using var fixture = new VisualRegressionFixture(256, 192, multiSample: 4);
        using var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.04f, 0.04f, 0.04f));
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Box.mdl", "Materials/Constant/MattWhite.xml",
            new Vector3f(0.0f, 0.0f, 0.9f), 1.8f);
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-3.0f, 4.0f, -4.0f),
            Vector3f.Zero, Color.White, brightness: 1.4f);

        using var cutout = fixture.Resources.Get<Material>("Materials/UrhoDecalAlphaMaskTwoSided.xml")
            .Clone("VisualRegression.AlphaCutout");
        var node = scene.CreateChild("Cutout");
        node.SetPosition(new Vector3f(0.0f, 0.0f, -0.15f));
        node.SetScale(2.2f);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(fixture.Resources.Get<Model>("Models/Plane.mdl"));
        model.SetMaterial(cutout);
        var pass = Assert.IsType<Pass>(cutout.GetTechnique(0)?.GetPass("base"));
        var camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(0.0f, 0.0f, -5.5f), Vector3f.Zero, 256.0f / 192.0f,
            orthographic: true, orthoSize: 3.4f);

        pass.SetAlphaToCoverage(false);
        pass.SetColorWrite(true);
        var cutoutImage = fixture.Capture(scene, camera);
        pass.SetAlphaToCoverage(true);
        var coverageImage = fixture.Capture(scene, camera);
        pass.SetColorWrite(false);
        var noColorImage = fixture.Capture(scene, camera);

        using var transparent = fixture.Resources.Get<Material>("Materials/Constant/MattTransparent.xml")
            .Clone("VisualRegression.Translucency");
        transparent.SetShaderParameter("MatDiffColor", new Vector4f(0.1f, 0.75f, 1.0f, 0.45f));
        model.SetMaterial(transparent);
        var transparentPass = Assert.IsType<Pass>(transparent.GetTechnique(0)?.GetPass("alpha"));
        transparentPass.SetDepthWrite(false);
        var translucentImage = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(cutoutImage, "materials/alpha-cutout.png"),
            new VisualRegressionImageCapture(coverageImage, "materials/alpha-coverage.png"),
            new VisualRegressionImageCapture(noColorImage, "materials/alpha-color-write-off.png"),
            new VisualRegressionImageCapture(translucentImage, "materials/translucency-two-sided.png"));
    }

    [VisualFact]
    public void Material_shader_define_variants_and_invalid_shader_fallback_are_visible()
    {
        using var fixture = new VisualRegressionFixture(256, 192);
        using var scene = CreateLitScene(fixture, out var camera);
        var node = scene.CreateChild("ShaderVariant");
        node.SetScale(1.4f);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(fixture.Resources.Get<Model>("Models/Sphere.mdl"));

        model.SetMaterial(fixture.Resources.Get<Material>("Materials/Stone.xml"));
        var normal = fixture.Capture(scene, camera);
        model.SetMaterial(fixture.Resources.Get<Material>("Materials/CheckboardProperties.xml"));
        var pbr = fixture.Capture(scene, camera);
        model.SetMaterial(fixture.Resources.Get<Material>("Materials/Constant/GlowingRed.xml"));
        var emissive = fixture.Capture(scene, camera);
        model.SetMaterial(fixture.Resources.Get<Material>("Materials/Showcase/InvalidShader.xml"));
        var invalid = fixture.Capture(scene, camera);

        fixture.AssertMatches(
            new VisualRegressionImageCapture(normal, "materials/defines-normal-map.png"),
            new VisualRegressionImageCapture(pbr, "materials/defines-pbr-properties.png"),
            new VisualRegressionImageCapture(emissive, "materials/defines-emissive.png"),
            new VisualRegressionImageCapture(invalid, "materials/defines-invalid-fallback.png"));
    }

    private static VisualRegressionImage CaptureMaterial(
        VisualRegressionFixture fixture, string materialName)
    {
        using var scene = CreateLitScene(fixture, out var camera);
        VisualRegressionSceneFactory.AddModel(
            scene, fixture.Resources, "Models/Sphere.mdl", materialName, Vector3f.Zero, 1.35f);
        return fixture.Capture(scene, camera);
    }

    private static Scene CreateLitScene(VisualRegressionFixture fixture, out Camera camera)
    {
        var scene = VisualRegressionSceneFactory.CreateScene(new Color(0.04f, 0.04f, 0.04f));
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Directional, new Vector3f(-3.0f, 5.0f, -4.0f),
            Vector3f.Zero, Color.White, brightness: 1.6f);
        VisualRegressionSceneFactory.AddLight(
            scene, LightType.Point, new Vector3f(2.0f, 2.0f, -3.0f),
            Vector3f.Zero, new Color(0.25f, 0.4f, 1.0f), range: 7.0f, brightness: 1.4f);
        camera = VisualRegressionSceneFactory.AddCamera(
            scene, new Vector3f(3.6f, 2.5f, -6.0f), Vector3f.Zero,
            256.0f / 192.0f, fov: 40.0f);
        return scene;
    }

    private static Material CreateTechniqueLodMaterial(ResourceCache resources)
    {
        var material = new Material { Name = "VisualRegression.TechniqueLod" };
        material.SetTexture("Albedo", resources.Get<Texture2D>("Textures/StoneDiffuse.dds"));
        material.SetTexture("Normal", resources.Get<Texture2D>("Textures/StoneNormal.dds"));
        material.SetPixelShaderDefines("PACKEDNORMAL");
        material.SetShaderParameter("MatSpecColor", new Vector4f(0.25f, 0.25f, 0.25f, 16.0f));
        material.SetTechniques([
            new TechniqueEntry(resources.Get<Technique>("Techniques/DiffNormal.xml"), MaterialQuality.High, 6.0f),
            new TechniqueEntry(resources.Get<Technique>("Techniques/Diff.xml"), MaterialQuality.Low, 0.0f)
        ]);
        return material;
    }

    private static void SetMaterialQuality(RenderPipeline pipeline, MaterialQuality quality)
    {
        var settings = pipeline.Settings;
        var sceneProcessor = settings.SceneProcessor;
        sceneProcessor.MaterialQuality = quality;
        settings.SceneProcessor = sceneProcessor;
        pipeline.SetSettings(settings);
    }

    private static VisualRegressionImage CaptureWithSampling(
        VisualRegressionFixture fixture,
        Scene scene,
        Camera camera,
        Texture2D albedo,
        Texture2D normal,
        TextureFilterMode filter,
        float minLod,
        float maxLod)
    {
        albedo.SetFilterMode(filter);
        normal.SetFilterMode(filter);
        albedo.SetAnisotropy(8);
        normal.SetAnisotropy(8);
        albedo.SetMinLod(minLod);
        normal.SetMinLod(minLod);
        albedo.SetMaxLod(maxLod);
        normal.SetMaxLod(maxLod);
        return fixture.Capture(scene, camera);
    }

    private static Texture2D CreateColorSpaceTexture(Image source, bool linear, bool srgb)
    {
        var texture = new Texture2D { Name = $"VisualRegression.ColorSpace.{linear}.{srgb}" };
        texture.SetLinear(linear);
        texture.SetSRGB(srgb);
        Assert.True(texture.SetData(source));
        if (srgb)
            Assert.True(texture.GetSRGB());
        return texture;
    }

    private static Image CreateColorSpaceSourceImage()
    {
        var image = new Image();
        Assert.True(image.SetSize(32, 32, 4));
        var data = image.GetData();
        for (var y = 0; y < image.Height; ++y)
        {
            for (var x = 0; x < image.Width; ++x)
            {
                var index = (y * image.Width + x) * 4;
                var value = checked((byte)(32 + (x * 192 / (image.Width - 1))));
                data[index] = value;
                data[index + 1] = checked((byte)(32 + (y * 192 / (image.Height - 1))));
                data[index + 2] = (byte)(x == y ? 255 : 64);
                data[index + 3] = byte.MaxValue;
            }
        }
        return image;
    }
}
