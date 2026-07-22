using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderPathTests
{
    public RenderPathTests(HeadlessEngineFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
    }

    [Fact]
    public void Render_path_matches_native_constructor_shape()
    {
        Assert.Single(typeof(RenderPath).GetConstructors());
        using var renderPath = new RenderPath();
        Assert.Equal(string.Empty, renderPath.Name);
    }

    [Fact]
    public void Render_path_repairs_enabled_passes_by_name()
    {
        var renderPath = new RenderPath();
        renderPath.SetName("RenderPaths/Test.xml");
        var alpha = new TrackingRenderPass("Alpha") { EnabledByDefaultValue = false };
        var beta = new TrackingRenderPass("Beta");
        renderPath.AddPass(alpha);
        renderPath.AddPass(beta);

        var repaired = renderPath.RepairEnabledRenderPasses(
        [
            ("Beta", false),
            ("Alpha", true)
        ]);

        Assert.Collection(
            repaired,
            pass => Assert.Equal(("Alpha", true), pass),
            pass => Assert.Equal(("Beta", false), pass));
    }

    [Fact]
    public void Render_path_repair_returns_independent_matching_passes_copy()
    {
        var renderPath = new RenderPath();
        renderPath.SetName("RenderPaths/Test.xml");
        renderPath.AddPass(new TrackingRenderPass("Alpha"));
        renderPath.AddPass(new TrackingRenderPass("Beta"));

        (string Name, bool Enabled)[] matchingPasses =
        [
            ("Alpha", true),
            ("Beta", false)
        ];

        var repaired = renderPath.RepairEnabledRenderPasses(matchingPasses);

        Assert.NotSame(matchingPasses, repaired);
        Assert.Equal(matchingPasses, repaired);
    }

    [Fact]
    public void Render_path_clone_deep_copies_passes_without_copying_resource_state()
    {
        using var renderPath = new RenderPath();
        renderPath.SetName("RenderPaths/Test.renderpath");
        var pass = new TrackingRenderPass("Alpha");
        pass.SetComment("Comment");
        renderPath.AddPass(pass);
        using var parameters = new StringVariantMap();
        renderPath.UpdateParameters(
            new RenderPipelineSettings(),
            [("Alpha", true)],
            parameters);

        using var clone = renderPath.Clone();

        Assert.Equal(string.Empty, clone.Name);
        var clonedPass = Assert.IsType<TrackingRenderPass>(Assert.Single(clone.Passes));
        Assert.NotSame(pass, clonedPass);
        Assert.Equal("Alpha", clonedPass.GetPassName());
        Assert.Equal("Comment", clonedPass.GetComment());
        Assert.Equal(default, clone.AggregatedPassTraits);
    }

    [Fact]
    public void Render_path_omits_empty_optional_passes_and_clears_on_missing_input()
    {
        using var renderPath = new RenderPath();
        using var output = new TestJSONOutputArchive();
        using (output.OpenUnorderedBlock("resource"))
            renderPath.SerializeInBlock(output);

        Assert.DoesNotContain("\"passes\"", output.ToJsonString());

        renderPath.AddPass(new TrackingRenderPass("Alpha"));
        using var input = new TestJSONInputArchive("{}");
        using (input.OpenUnorderedBlock("resource"))
            renderPath.SerializeInBlock(input);

        Assert.Empty(renderPath.Passes);
    }

    [Fact]
    public void Render_path_deserialization_creates_registered_render_pass_type()
    {
        var context = Context.Instance;
        context.AddFactoryReflection<TrackingRenderPass>(ObjectCategory.Category_RenderPass);
        try
        {
            string json;
            using (var source = new RenderPath())
            using (var output = new TestJSONOutputArchive())
            {
                source.AddPass(new TrackingRenderPass("Custom"));
                using (output.OpenUnorderedBlock("resource"))
                    source.SerializeInBlock(output);
                json = output.ToJsonString();
            }

            using var restored = new RenderPath();
            using var input = new TestJSONInputArchive(json);
            using (input.OpenUnorderedBlock("resource"))
                restored.SerializeInBlock(input);

            Assert.IsType<TrackingRenderPass>(Assert.Single(restored.Passes));
            Assert.Equal("Custom", restored.Passes[0].GetPassName());
        }
        finally
        {
            context.RemoveReflection<TrackingRenderPass>();
        }
    }

    [Fact]
    public void Render_path_binary_serialization_uses_registered_render_pass_type_hash()
    {
        var context = Context.Instance;
        context.AddFactoryReflection<TrackingRenderPass>(ObjectCategory.Category_RenderPass);
        try
        {
            using var buffer = new VectorBuffer();
            using var source = new RenderPath();
            source.AddPass(new TrackingRenderPass("Custom"));
            using (var output = new BinaryOutputArchive(context, buffer))
            {
                using (output.OpenUnorderedBlock("resource"))
                    source.SerializeInBlock(output);
                output.Flush();
            }

            buffer.Seek(0);
            using var restored = new RenderPath();
            using (var input = new BinaryInputArchive(context, buffer))
            {
                using (input.OpenUnorderedBlock("resource"))
                    restored.SerializeInBlock(input);
                input.Flush();
            }

            Assert.IsType<TrackingRenderPass>(Assert.Single(restored.Passes));
            Assert.Equal("Custom", restored.Passes[0].GetPassName());
        }
        finally
        {
            context.RemoveReflection<TrackingRenderPass>();
        }
    }

    [Fact]
    public void Render_path_reorder_ignores_negative_index()
    {
        var renderPath = new RenderPath();
        renderPath.SetName("RenderPaths/Test.xml");
        var alpha = new TrackingRenderPass("Alpha");
        var beta = new TrackingRenderPass("Beta");
        renderPath.AddPass(alpha);
        renderPath.AddPass(beta);

        renderPath.ReorderPass(beta, -1);

        Assert.Equal([alpha, beta], renderPath.Passes);
    }

    [Fact]
    public void Tone_mapping_invalidates_pipeline_state_only_for_native_parameter_type_changes()
    {
        using var pass = new ToneMappingPass();
        var pipelineStates = typeof(ToneMappingPass).GetField(
            "_pipelineStates", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var pipelineStateCache = typeof(ToneMappingPass).GetNestedType(
            "PipelineStateCache", System.Reflection.BindingFlags.NonPublic)!;
        var settings = new RenderPipelineSettings();
        var renderBufferManager = settings.RenderBufferManager;
        renderBufferManager.ColorSpace = RenderPipelineColorSpace.LinearHDR;
        settings.RenderBufferManager = renderBufferManager;

        pipelineStates.SetValue(pass, Activator.CreateInstance(pipelineStateCache));
        using var mode = new Variant(1);
        using var parameters = new StringVariantMap { ["Tone Mapping: Mode"] = mode };
        pass.UpdateParameters(settings, parameters);
        Assert.Null(pipelineStates.GetValue(pass));

        pipelineStates.SetValue(pass, Activator.CreateInstance(pipelineStateCache));
        using var wrongTypeMode = new Variant(2.0f);
        using var wrongTypeParameters = new StringVariantMap { ["Tone Mapping: Mode"] = wrongTypeMode };
        pass.UpdateParameters(settings, wrongTypeParameters);
        Assert.NotNull(pipelineStates.GetValue(pass));

        renderBufferManager = settings.RenderBufferManager;
        renderBufferManager.ColorSpace = RenderPipelineColorSpace.GammaLDR;
        settings.RenderBufferManager = renderBufferManager;
        pass.UpdateParameters(settings, parameters);
        Assert.Null(pipelineStates.GetValue(pass));
    }

    [Fact]
    public void Render_path_updates_and_renders_only_enabled_passes()
    {
        var renderPath = new RenderPath();
        renderPath.SetName("RenderPaths/Test.xml");
        var first = new TrackingRenderPass("First");
        var second = new TrackingRenderPass("Second") { InternallyEnabledValue = false };
        renderPath.AddPass(first);
        renderPath.AddPass(second);

        var settings = new RenderPipelineSettings();
        using var parameters = new StringVariantMap();
        renderPath.UpdateParameters(
        settings,
        [
            ("First", true),
            ("Second", true)
        ],
        parameters);

        var sharedState = new SharedRenderPassState();
        renderPath.Update(sharedState);
        renderPath.Render(sharedState);

        Assert.Equal(1, first.UpdateCalls);
        Assert.Equal(1, first.RenderCalls);
        Assert.Equal(0, second.UpdateCalls);
        Assert.Equal(0, second.RenderCalls);
        Assert.True(renderPath.AggregatedPassTraits.NeedReadWriteColorBuffer);
        Assert.True(renderPath.AggregatedPassTraits.NeedBilinearColorSampler);
    }

    [Fact]
    public void Render_path_aggregates_readable_depth_trait_from_ssao()
    {
        var renderPath = new RenderPath();
        renderPath.SetName("RenderPaths/Test.xml");
        renderPath.AddPass(new AmbientOcclusionPass());

        using var parameters = new StringVariantMap();
        renderPath.UpdateParameters(new RenderPipelineSettings(), renderPath.RepairEnabledRenderPasses([]), parameters);

    }

    [Fact]
    public void Render_path_json_renderpath_loads_through_resource_cache()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "RedBolt.RenderPathTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "RenderPaths"));
        var resources = new ResourceCache();
        resources.RegisterDefaultResources();
        resources.AddResourceDir(tempRoot);

        try
        {
            System.IO.File.WriteAllText(Path.Combine(tempRoot, "RenderPaths", "Simple.renderpath"), """
                {
                  "passes": [
                    {
                      "type": "FullScreenShaderPass",
                      "value": {
                        "attributes": [
                          { "name": "Pass Name", "type": "String", "value": "Postprocess: Adjust Color" },
                          { "name": "Comment", "type": "String", "value": "Adjust output color RGB and HSV." },
                          { "name": "Read+Write Color Output", "type": "Bool", "value": true },
                          { "name": "Bilinear Color Sampler", "type": "Bool", "value": true },
                          { "name": "Parameters Prefix", "type": "String", "value": "Adjust Color: " },
                          {
                            "name": "Parameters",
                            "type": "StringVariantMap",
                            "value": [
                              { "key": "HSVParams", "type": "Vector4", "value": "1 1 1 1" }
                            ]
                          }
                        ]
                      }
                    }
                  ]
                }
                """);

            {
                var source = System.IO.File.ReadAllText(Path.Combine(tempRoot, "RenderPaths", "Simple.renderpath"));
                using var archive = new TestJSONInputArchive(source);
                using var rootBlock = archive.OpenUnorderedBlock("resource");
                using var directRenderPath = new RenderPath();
                directRenderPath.SerializeInBlock(archive);
            }

            var renderPath = resources.GetResource<RenderPath>("RenderPaths/Simple");

            using var collectedParameters = new StringVariantMap();
            renderPath.CollectParameters(collectedParameters);
            renderPath.UpdateParameters(new RenderPipelineSettings(), renderPath.RepairEnabledRenderPasses([]), collectedParameters);

            var pass = Assert.IsType<FullScreenShaderPass>(Assert.Single(renderPath.Passes));
            Assert.Equal("Postprocess: Adjust Color", pass.GetPassName());
            Assert.Equal("Adjust output color RGB and HSV.", pass.GetComment());
            Assert.Equal(new Vector4f(1.0f, 1.0f, 1.0f, 1.0f), collectedParameters["Adjust Color: HSVParams"].GetVector4());
            Assert.True(renderPath.AggregatedPassTraits.NeedReadWriteColorBuffer);
            Assert.True(renderPath.AggregatedPassTraits.NeedBilinearColorSampler);
        }
        finally
        {
            resources.RemoveResourceDir(tempRoot);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Render_path_loads_real_rbfx_default_renderpath_types()
    {
        var resources = new ResourceCache();
        resources.RegisterDefaultResources();
        var upstreamCoreData = GetCoreData();
        resources.AddResourceDir(upstreamCoreData);

        try
        {
            var renderPath = resources.GetResource<RenderPath>(RenderPipeline.DefaultRenderPathName);

            Assert.NotEmpty(renderPath.Passes);
            Assert.Collection(
                renderPath.Passes.Take(5),
                pass => Assert.IsType<AutoExposurePass>(pass),
                pass => Assert.IsType<AmbientOcclusionPass>(pass),
                pass => Assert.IsType<BloomPass>(pass),
                pass => Assert.IsType<OutlineRenderPass>(pass),
                pass => Assert.IsType<ToneMappingPass>(pass));
            Assert.Contains(renderPath.Passes, static pass => pass is FullScreenShaderPass);

            using var collectedParameters = new StringVariantMap();
            renderPath.CollectParameters(collectedParameters);
            renderPath.UpdateParameters(new RenderPipelineSettings(), renderPath.RepairEnabledRenderPasses([]), collectedParameters);

            Assert.Contains("Exposure: Automatic", collectedParameters.Keys);
            Assert.Contains("Bloom: Num Iterations", collectedParameters.Keys);
            Assert.Contains("Tone Mapping: Mode", collectedParameters.Keys);
            Assert.Equal(new Vector4f(1.0f, 1.0f, 1.0f, 1.0f), collectedParameters["Adjust Color: HSVParams"].GetVector4());
            Assert.True(renderPath.AggregatedPassTraits.NeedReadWriteColorBuffer);
            Assert.True(renderPath.AggregatedPassTraits.NeedBilinearColorSampler);
        }
        finally
        {
            resources.RemoveResourceDir(upstreamCoreData);
        }
    }

    private static string GetCoreData()
    {
        return RbfxTestData.CoreData;
    }

    [Fact]
    public void Bloom_pass_uses_native_pod_cache_shapes()
    {
        var parametersType = typeof(BloomPass).GetNestedType(
            "Parameters", System.Reflection.BindingFlags.NonPublic)!;
        var pipelineStateCacheType = typeof(BloomPass).GetNestedType(
            "PipelineStateCache", System.Reflection.BindingFlags.NonPublic)!;
        var textureCacheType = typeof(BloomPass).GetNestedType(
            "TextureCache", System.Reflection.BindingFlags.NonPublic)!;
        var parametersField = typeof(BloomPass).GetField(
            "_parameters", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var pipelineStatesField = typeof(BloomPass).GetField(
            "_pipelineStates", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var texturesField = typeof(BloomPass).GetField(
            "_textures", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        Assert.False(typeof(BloomPass).IsSealed);
        Assert.True(parametersType.IsValueType);
        Assert.True(pipelineStateCacheType.IsValueType);
        Assert.True(textureCacheType.IsValueType);
        Assert.Equal(parametersType, parametersField.FieldType);
        Assert.Equal(typeof(Nullable<>).MakeGenericType(pipelineStateCacheType), pipelineStatesField.FieldType);
        Assert.Equal(textureCacheType, Assert.Single(texturesField.FieldType.GenericTypeArguments));

        var left = Activator.CreateInstance(parametersType)!;
        var right = Activator.CreateInstance(parametersType)!;
        var equalityOperator = parametersType.GetMethod(
            "op_Equality", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
        Assert.True((bool)equalityOperator.Invoke(null, [left, right])!);
        parametersType.GetProperty("NumIterations")!.SetValue(right, 6);
        Assert.False((bool)equalityOperator.Invoke(null, [left, right])!);
    }

    [Fact]
    public void Full_screen_shader_pass_uses_native_pod_cache_shape()
    {
        var attributesType = typeof(FullScreenShaderPass).GetNestedType(
            "Attributes", System.Reflection.BindingFlags.NonPublic)!;
        var cacheType = typeof(FullScreenShaderPass).GetNestedType(
            "Cache", System.Reflection.BindingFlags.NonPublic)!;
        var attributesField = typeof(FullScreenShaderPass).GetField(
            "_attributes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var cacheField = typeof(FullScreenShaderPass).GetField(
            "_cache", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        Assert.True(attributesType.IsValueType);
        Assert.True(cacheType.IsValueType);
        Assert.Equal(attributesType, attributesField.FieldType);
        Assert.Equal(typeof(Nullable<>).MakeGenericType(cacheType), cacheField.FieldType);
    }

    [Fact]
    public void Full_screen_shader_pass_owns_independent_parameter_variants()
    {
        using var defaultSource = new CString(new string('d', 32));
        using var updatedSource = new CString(new string('u', 32));
        var pass = new FullScreenShaderPass();
        var setParameters = typeof(FullScreenShaderPass).GetMethod(
            "SetParameters", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        using var defaultVariant = new Variant(defaultSource);
        using var defaultParameters = new StringVariantMap { ["Value"] = defaultVariant };
        setParameters.Invoke(pass, [defaultParameters]);

        pass.InitializeView(null!);

        var defaults = Assert.IsType<List<Variant>>(typeof(FullScreenShaderPass).GetField(
            "_shaderParameterDefaults", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(pass));
        var parameters = Assert.IsType<List<ShaderParameterDesc>>(typeof(FullScreenShaderPass).GetField(
            "_shaderParameters", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(pass));
        var defaultValue = defaults[0].GetCString();
        var initialValue = parameters[0].Value.GetCString();

        Assert.NotSame(defaultSource, defaultValue);
        Assert.NotSame(defaultSource, initialValue);
        Assert.NotSame(defaultValue, initialValue);

        using var updatedVariant = new Variant(updatedSource);
        using var updatedParameters = new StringVariantMap { ["Value"] = updatedVariant };
        pass.UpdateParameters(new RenderPipelineSettings(), updatedParameters);

        var updatedValue = parameters[0].Value.GetCString();
        Assert.Equal(string.Empty, initialValue.ConvertToString());
        Assert.Equal(new string('d', 32), defaultValue.ConvertToString());
        Assert.NotSame(updatedSource, updatedValue);

        pass.InitializeView(null!);

        Assert.Equal(string.Empty, defaultValue.ConvertToString());
        Assert.Equal(string.Empty, updatedValue.ConvertToString());
        var currentDefaultValue = defaults[0].GetCString();
        var currentValue = parameters[0].Value.GetCString();

        pass.Dispose();

        Assert.Equal(string.Empty, currentDefaultValue.ConvertToString());
        Assert.Equal(string.Empty, currentValue.ConvertToString());
    }

    private sealed class TrackingRenderPass : RenderPass
    {
        public TrackingRenderPass()
            : this(string.Empty)
        {
        }

        public TrackingRenderPass(string passName)
        {
            _passName = passName;
        }

        public bool EnabledByDefaultValue
        {
            set => SetEnabledByDefault(value);
        }

        public bool InternallyEnabledValue
        {
            set => _isEnabledInternally = value;
        }

        public int UpdateCalls { get; private set; }

        public int RenderCalls { get; private set; }

        public override void UpdateParameters(RenderPipelineSettings settings, StringVariantMap parameters)
        {
            _traits = new RenderPassTraits(NeedReadWriteColorBuffer: true, NeedBilinearColorSampler: true);
        }

        public override void Update(SharedRenderPassState sharedState)
        {
            ++UpdateCalls;
        }

        public override void Render(SharedRenderPassState sharedState)
        {
            ++RenderCalls;
        }

        public override RenderPass Clone()
        {
            var clone = new TrackingRenderPass(GetPassName())
            {
                EnabledByDefaultValue = IsEnabledByDefault(),
                InternallyEnabledValue = true
            };
            clone.SetComment(GetComment());
            return clone;
        }
    }
}
