namespace RedBolt.RenderPipeline;

using Microsoft.Extensions.Logging;
using SystemMath = global::System.Math;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Container;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Scene;

internal static class BatchRendererHelpers
{
    /// <summary>Return shader parameter for camera depth mode.</summary>
    public static Vector4f GetCameraDepthModeParameter(Camera camera, RenderBackend backend)
    {
        var depthMode = Vector4f.Zero;
        if (camera.IsOrthographic())
        {
            depthMode.X = 1.0f;
            if (backend == RenderBackend.OpenGL)
            {
                depthMode.Z = 0.5f;
                depthMode.W = 0.5f;
            }
            else
            {
                depthMode.Z = 1.0f;
                depthMode.W = 0.0f;
            }
        }
        else
            depthMode.W = 1.0f / camera.GetFarClip();
        return depthMode;
    }

    /// <summary>Return shader parameter for camera depth reconstruction.</summary>
    public static Vector4f GetCameraDepthReconstructParameter(Camera camera)
    {
        var nearClip = camera.GetNearClip();
        var farClip = camera.GetFarClip();
        return new Vector4f(
            farClip / (farClip - nearClip),
            -nearClip / (farClip - nearClip),
            camera.IsOrthographic() ? 1.0f : 0.0f,
            camera.IsOrthographic() ? 0.0f : 1.0f);
    }

    /// <summary>Return shader parameter for zone fog.</summary>
    public static Vector4f GetFogParameter(Camera camera)
    {
        var farClip = camera.GetFarClip();
        var fogStart = SystemMath.Min(camera.GetEffectiveFogStart(), farClip);
        var fogEnd = SystemMath.Min(camera.GetEffectiveFogEnd(), farClip);
        if (fogStart >= fogEnd * (1.0f - MathDefs.LargeEpsilon))
            fogStart = fogEnd * (1.0f - MathDefs.LargeEpsilon);
        var fogRange = SystemMath.Max(fogEnd - fogStart, MathDefs.Epsilon);
        return new Vector4f(fogEnd / farClip, farClip / fogRange, 0.0f, 0.0f);
    }

    public static Vector4f GetClipPlane(Camera camera)
    {
        if (!camera.GetUseClipping())
            return new Vector4f(0, 0, 0, 1);
        var viewProj = camera.GetGPUProjection() * camera.GetView().Matrix4;
        return camera.GetClipPlane().Transformed(viewProj).ToVector4();
    }
}

/// <summary>Helper class to process per-object parameters.</summary>
internal struct ObjectParameterBuilder
{
    private readonly bool _instancingEnabled;
    private readonly bool _ambientEnabled;
    private readonly DrawableAmbientMode _ambientMode;
    private readonly bool _linearColorSpace;
    private Vector4f _ambientValueFlat;
    private SphericalHarmonicsDot9 _ambientValueSh;

    public ObjectParameterBuilder(BatchRendererSettings settings, BatchRenderFlag flags)
    {
        _instancingEnabled = (flags & BatchRenderFlag.EnableInstancingForStaticGeometry) != 0;
        _ambientEnabled = (flags & BatchRenderFlag.EnableAmbientLighting) != 0;
        _ambientMode = settings.AmbientMode;
        _linearColorSpace = (flags & BatchRenderFlag.LinearColorSpace) != 0;
    }

    public bool IsInstancingSupported() => _instancingEnabled;
    public bool IsAmbientEnabled() => _ambientEnabled;

    /// <summary>Whether the batch should be processed using instancing.</summary>
    public bool IsBatchInstanced(in PipelineBatch pipelineBatch)
        => _instancingEnabled && pipelineBatch.Geometry!.IsInstanced(pipelineBatch.GeometryType);

    /// <summary>Set batch ambient lighting.</summary>
    public void SetBatchAmbient(in LightAccumulator lightAccumulator)
    {
        if (_ambientMode == DrawableAmbientMode.Flat)
        {
            var ambient = lightAccumulator.SphericalHarmonics.EvaluateAverage();
            _ambientValueFlat = _linearColorSpace
                ? new Vector4f(ambient, 1.0f)
                : new Color(ambient).LinearToGamma().ToVector4();
        }
        else if (_ambientMode == DrawableAmbientMode.Directional)
            _ambientValueSh = lightAccumulator.SphericalHarmonics;
    }

    /// <summary>Add uniforms to instancing buffer for instanced batches.</summary>
    public void AddBatchesToInstancingBuffer(InstancingBuffer instancingBuffer, in SourceBatch sourceBatch,
        ReadOnlySpan<Matrix3x4> worldTransform, int instanceIndex)
    {
        instancingBuffer.AddInstance();
        instancingBuffer.SetElements(in worldTransform[instanceIndex], 0, 3);
        if (_ambientEnabled)
        {
            if (_ambientMode == DrawableAmbientMode.Flat)
                instancingBuffer.SetElements(in _ambientValueFlat, 3, 1);
            else if (_ambientMode == DrawableAmbientMode.Directional)
                instancingBuffer.SetElements(in _ambientValueSh, 3, 7);
        }
    }

    /// <summary>Add uniforms to draw queue for non-instanced batch.</summary>
    public void AddBatchUniformsToDrawQueue(DrawCommandQueue drawQueue, Node cameraNode, in SourceBatch sourceBatch,
        ReadOnlySpan<Matrix3x4> worldTransform, int instanceIndex)
    {
        if (!drawQueue.BeginShaderParameterGroup(ShaderParameterGroup.Object, true))
            return;

        if (_ambientEnabled)
        {
            if (_ambientMode == DrawableAmbientMode.Flat)
                drawQueue.AddShaderParameter(ShaderConsts.Object_Ambient, _ambientValueFlat);
            else if (_ambientMode == DrawableAmbientMode.Directional)
            {
                drawQueue.AddShaderParameter(ShaderConsts.Object_SHAr, _ambientValueSh.Ar);
                drawQueue.AddShaderParameter(ShaderConsts.Object_SHAg, _ambientValueSh.Ag);
                drawQueue.AddShaderParameter(ShaderConsts.Object_SHAb, _ambientValueSh.Ab);
                drawQueue.AddShaderParameter(ShaderConsts.Object_SHBr, _ambientValueSh.Br);
                drawQueue.AddShaderParameter(ShaderConsts.Object_SHBg, _ambientValueSh.Bg);
                drawQueue.AddShaderParameter(ShaderConsts.Object_SHBb, _ambientValueSh.Bb);
                drawQueue.AddShaderParameter(ShaderConsts.Object_SHC, _ambientValueSh.C);
            }
        }

        switch (sourceBatch.GeometryType)
        {
            case GeometryType.Skinned:
            {
                var count = (int)SystemMath.Min(sourceBatch.NumWorldTransforms, Graphics.GetMaxBones());
                drawQueue.AddShaderParameter(ShaderConsts.Object_SkinMatrices, worldTransform[..count]);
                break;
            }
            case GeometryType.Billboard:
                drawQueue.AddShaderParameter(ShaderConsts.Object_Model, worldTransform[0]);
                if (sourceBatch.NumWorldTransforms > 1)
                    drawQueue.AddShaderParameter(ShaderConsts.Object_BillboardRot, worldTransform[1].RotationMatrix);
                else
                    drawQueue.AddShaderParameter(ShaderConsts.Object_BillboardRot, cameraNode.GetWorldRotation().RotationMatrix);
                break;
            default:
                drawQueue.AddShaderParameter(ShaderConsts.Object_Model, worldTransform[instanceIndex]);
                break;
        }

        drawQueue.CommitShaderParameterGroup(ShaderParameterGroup.Object);
    }
}

/// <summary>Batch renderer to command queue. Port of C++ DrawCommandCompositor.</summary>
internal ref struct DrawCommandCompositor
{
    /// <summary>Number of vec4 elements per vertex light.</summary>
    public const int VertexLightStride = 3;
    private const uint PortableLightMask = 0xff;
    private static readonly CookedLightParams NullVertexLight;

    private readonly BatchRenderingContext _ctx;

    public DrawCommandCompositor(BatchRenderingContext ctx, BatchRendererSettings settings,
        RenderPipelineDebugger? debugger, DrawableProcessor drawableProcessor, InstancingBuffer instancingBuffer,
        BatchRenderFlag flags, int startInstance, bool debuggerEnabled)
    {
        _ctx = ctx;
        _settings = settings;
        _backend = ctx.Camera.GetSubsystem<RenderDevice>()!.GetBackend();
        _numVertexLights = checked((int)drawableProcessor.Settings.MaxVertexLights);
        _debugger = debugger;
        _debuggerEnabled = debuggerEnabled;
        _drawableProcessor = drawableProcessor;
        _instancingBuffer = instancingBuffer;
        _frameInfo = drawableProcessor.FrameInfo;
        _scene = _frameInfo.Scene!;
        _lights = drawableProcessor.LightProcessors;
        _cameraNode = ctx.Camera.Node!;
        _depthRange = ctx.Camera.GetFarClip();
        _clipPlane = BatchRendererHelpers.GetClipPlane(ctx.Camera);
        _enabled = new EnabledFeatureFlags(flags, instancingBuffer);
        _dirty = new DirtyStateFlags();
        _current = new CachedSharedState();
        _lightVolumeHelpers = new LightVolumeBatchHelpers();
        _objectParameterBuilder = new ObjectParameterBuilder(settings, flags);
        _instanceIndex = startInstance;
    }

    /// <summary>Process batches.</summary>
    public void ProcessSceneBatch(in PipelineBatch pipelineBatch)
    {
        ref readonly var sourceBatch = ref pipelineBatch.GetSourceBatch();
        ProcessBatch(in pipelineBatch, in sourceBatch, sourceBatch.WorldTransform);
    }

    public void ProcessLightVolumeBatch(in PipelineBatch pipelineBatch)
    {
        var light = _lights[pipelineBatch.PixelLightIndex]!.Light;
        _lightVolumeHelpers.Transform = light.GetVolumeTransform(_ctx.Camera);
        var worldTransform = MemoryMarshal.CreateReadOnlySpan(ref _lightVolumeHelpers.Transform, 1);
        ProcessBatch(in pipelineBatch, in _lightVolumeHelpers.SourceBatch, worldTransform);
    }

    public void FlushDrawCommands(int nextInstanceIndex)
    {
        if (_instancingGroup.Count > 0)
            CommitInstancedDrawCalls();
        if (nextInstanceIndex != _instanceIndex)
        {
            Log.GetLogger().LogError("Instancing buffer is malformed");
            System.Diagnostics.Debug.Assert(false);
        }
    }

    public void Dispose()
    {
        _lightVolumeHelpers.SourceBatch.Dispose();
    }

    // Extract and process batch state without any changes in DrawQueue.
    private void CheckDirtyCommonState(in PipelineBatch pipelineBatch)
    {
        _dirty.PipelineState = !ReferenceEquals(_current.PipelineState, pipelineBatch.PipelineState);
        _current.PipelineState = pipelineBatch.PipelineState;

        var constantDepthBias = _current.PipelineState!.GetDesc().AsGraphics()!.Value.ConstantDepthBias;
        _dirty.CameraConstants = _current.ConstantDepthBias != constantDepthBias;
        _current.ConstantDepthBias = constantDepthBias;

        _dirty.Material = !ReferenceEquals(_current.Material, pipelineBatch.Material);
        _current.Material = pipelineBatch.Material;

        _dirty.Geometry = !ReferenceEquals(_current.Geometry, pipelineBatch.Geometry);
        _current.Geometry = pipelineBatch.Geometry;

        _current.Drawable = pipelineBatch.Drawable;
    }

    private void CheckDirtyReflectionProbe(in LightAccumulator lightAccumulator)
    {
        var p0 = lightAccumulator.ReflectionProbes[0];
        var p1 = lightAccumulator.ReflectionProbes[1];
        _dirty.ReflectionProbe = !ReferenceEquals(_current.ReflectionProbe0, p0)
            || !ReferenceEquals(_current.ReflectionProbe1, p1)
            || _current.ReflectionProbesBlendFactor != lightAccumulator.ReflectionProbesBlendFactor;
        if (!_dirty.ReflectionProbe)
            return;
        _current.ReflectionProbe0 = p0;
        _current.ReflectionProbe1 = p1;
        _current.ReflectionProbeTexture0 = p0!.Value.ReflectionMap;
        _current.ReflectionProbeTexture1 = p1!.Value.ReflectionMap;
        _current.ReflectionProbesBlendFactor = lightAccumulator.ReflectionProbesBlendFactor;
    }

    private void CheckDirtyPixelLight(in PipelineBatch pipelineBatch)
    {
        _dirty.PixelLightConstants = _current.PixelLightIndex != pipelineBatch.PixelLightIndex;
        if (!_dirty.PixelLightConstants)
            return;

        _current.PixelLightIndex = pipelineBatch.PixelLightIndex;
        _current.PixelLightEnabled = _current.PixelLightIndex != int.MaxValue;
        if (_current.PixelLightEnabled)
        {
            _current.PixelLightProcessor = _lights[_current.PixelLightIndex]!;
            ref readonly var params_ = ref _current.PixelLightProcessor!.Params;
            _dirty.PixelLightTextures = !ReferenceEquals(_current.PixelLightRamp, params_.LightRamp)
                || !ReferenceEquals(_current.PixelLightShape, params_.LightShape)
                || !ReferenceEquals(_current.PixelLightShadowMap, params_.ShadowMap);
            if (_dirty.PixelLightTextures)
            {
                _current.PixelLightRamp = params_.LightRamp;
                _current.PixelLightShape = params_.LightShape;
                _current.PixelLightShadowMap = params_.ShadowMap;
            }
        }
    }

    private void CheckDirtyVertexLight(in LightAccumulator lightAccumulator)
    {
        var previous = _current.VertexLights;
        _current.VertexLights = lightAccumulator.VertexLights;
        var changed = false;
        for (var i = 0; i < LightAccumulator.MaxVertexLights; ++i)
        {
            if (previous[i] != _current.VertexLights[i])
            {
                changed = true;
                break;
            }
        }
        _dirty.VertexLightConstants = changed;
        if (!_dirty.VertexLightConstants)
            return;

        for (var i = 0; i < LightAccumulator.MaxVertexLights; ++i)
        {
            var lightIndex = _current.VertexLights[i];
            var lightProcessor = lightIndex != int.MaxValue ? _lights[lightIndex] : null;
            ref readonly var params_ = ref (lightProcessor is not null
                ? ref lightProcessor.Params
                : ref NullVertexLight);
            var color = params_.GetColor(_enabled.LinearColorSpace);
            _current.VertexLightsData[i * VertexLightStride] = new Vector4f(color, params_.InverseRange);
            _current.VertexLightsData[i * VertexLightStride + 1] = new Vector4f(params_.Direction, params_.SpotCutoff);
            _current.VertexLightsData[i * VertexLightStride + 2] = new Vector4f(params_.Position, params_.InverseSpotCutoff);
        }
    }

    private void CheckDirtyLightmap(in SourceBatch sourceBatch)
    {
        var prev = _current.LightmapScaleOffset;
        var lightmapChanged = !Nullable.Equals(prev, sourceBatch.LightmapScaleOffset);
        _dirty.LightmapConstants = lightmapChanged;
        if (!lightmapChanged)
            return;

        _current.LightmapScaleOffset = sourceBatch.LightmapScaleOffset;
        Texture2D? lightmapTexture = sourceBatch.LightmapScaleOffset is not null
            ? _scene.GetLightmapTexture(checked((int)sourceBatch.LightmapIndex))
            : null;
        _dirty.LightmapTextures = !ReferenceEquals(_current.LightmapTexture, lightmapTexture);
        _current.LightmapTexture = lightmapTexture;
    }

    // Commit changes to DrawQueue.
    private void UpdateDirtyConstants()
    {
        var drawQueue = _ctx.DrawQueue;
        if (drawQueue.BeginShaderParameterGroup(ShaderParameterGroup.Frame, false))
        {
            AddFrameConstants();
            drawQueue.CommitShaderParameterGroup(ShaderParameterGroup.Frame);
        }

        if (drawQueue.BeginShaderParameterGroup(ShaderParameterGroup.Camera, _dirty.CameraConstants))
        {
            AddCameraConstants(_current.ConstantDepthBias);
            drawQueue.CommitShaderParameterGroup(ShaderParameterGroup.Camera);
        }

        if (_enabled.AmbientLighting)
        {
            if (drawQueue.BeginShaderParameterGroup(ShaderParameterGroup.Zone, _dirty.ReflectionProbe))
            {
                AddReflectionProbeConstants();
                drawQueue.CommitShaderParameterGroup(ShaderParameterGroup.Zone);
            }
        }

        if (_ctx.OutputShadowSplit is not null)
        {
            if (drawQueue.BeginShaderParameterGroup(ShaderParameterGroup.Light, false))
            {
                AddPixelLightConstants(_ctx.OutputShadowSplit.LightProcessor.Params);
                drawQueue.CommitShaderParameterGroup(ShaderParameterGroup.Light);
            }
        }
        else if (_enabled.AnyLighting)
        {
            var lightConstantsDirty = _dirty.PixelLightConstants || _dirty.VertexLightConstants;
            if (drawQueue.BeginShaderParameterGroup(ShaderParameterGroup.Light, lightConstantsDirty))
            {
                if (_enabled.VertexLighting)
                    AddVertexLightConstants();
                if (_current.PixelLightEnabled)
                    AddPixelLightConstants(_current.PixelLightProcessor!.Params);
                drawQueue.CommitShaderParameterGroup(ShaderParameterGroup.Light);
            }
            _dirty.PixelLightConstants = false;
            _dirty.VertexLightConstants = false;
        }

        if (drawQueue.BeginShaderParameterGroup(ShaderParameterGroup.Material, _dirty.Material || _dirty.LightmapConstants))
        {
            // TODO: This block may be cached for some APIs
            var materialParameters = _current.Material!.EnumerateShaderParameters();
            while (materialParameters.MoveNext())
            {
                var name = materialParameters.Current.Key;
                var parameter = materialParameters.Current.Value;
                if (parameter.IsCustom)
                    continue;
                if (name == (string)ShaderConsts.Material_FadeOffsetScale)
                {
                    var param = parameter.VariantValue.GetVector2();
                    var paramAdjusted = new Vector2f(param.X / _depthRange, _depthRange / param.Y);
                    drawQueue.AddShaderParameter(new StringHash(name), paramAdjusted);
                }
                else
                    drawQueue.AddShaderParameter(new StringHash(name), parameter.VariantValue);
            }

            if (_enabled.AmbientLighting && _current.LightmapScaleOffset is { } lmOffset)
                drawQueue.AddShaderParameter(ShaderConsts.Material_LMOffset, in lmOffset);

            drawQueue.CommitShaderParameterGroup(ShaderParameterGroup.Material);
        }
        _dirty.LightmapConstants = false;

        if (drawQueue.BeginShaderParameterGroup(ShaderParameterGroup.Custom, _dirty.Material))
        {
            // TODO: This block may be cached for some APIs
            var materialParameters = _current.Material!.EnumerateShaderParameters();
            while (materialParameters.MoveNext())
            {
                var name = materialParameters.Current.Key;
                var parameter = materialParameters.Current.Value;
                if (parameter.IsCustom)
                    drawQueue.AddShaderParameter(new StringHash(name), parameter.VariantValue);
            }
            drawQueue.CommitShaderParameterGroup(ShaderParameterGroup.Custom);
        }
    }

    private void UpdateDirtyResources()
    {
        var resourcesDirty = _dirty.PipelineState || _dirty.Material || _dirty.ReflectionProbe || _dirty.IsResourcesDirty();
        if (!resourcesDirty)
            return;

        var drawQueue = _ctx.DrawQueue;
        foreach (var desc in _ctx.GlobalResources)
        {
            if (desc.Texture is not null)
                drawQueue.AddShaderResource(desc.Name, desc.Texture);
        }

        var materialHasEnvironmentMap = false;
        var materialTextures = _current.Material!.EnumerateTextures();
        while (materialTextures.MoveNext())
        {
            var nameHash = materialTextures.Current.Key;
            var texture = materialTextures.Current.Value;
            if (nameHash == (StringHash)ShaderResources.Reflection0)
                materialHasEnvironmentMap = true;
            // Emissive texture is used for lightmaps and refraction background, skip if necessary
            if (nameHash == (StringHash)ShaderResources.Emission && _current.LightmapTexture is not null)
                continue;
            GraphicsUtils.AddShaderResource(drawQueue, nameHash, texture.Texture);
        }

        // A material pass may still reference a texture sampler after the material texture
        // has been removed. Bind the device fallback in that case so Vulkan does not leave
        // an active shader resource variable unbound.
        var renderDevice = _ctx.Camera.GetSubsystem<RenderDevice>()!;
        AddDefaultMaterialTexture(drawQueue, ShaderResources.Albedo, renderDevice.GetDefaultAlbedoTexture());
        AddDefaultMaterialTexture(drawQueue, ShaderResources.Normal, renderDevice.GetDefaultNormalTexture());
        AddDefaultMaterialTexture(drawQueue, ShaderResources.Properties, renderDevice.GetDefaultPropertiesTexture());
        AddDefaultMaterialTexture(drawQueue, ShaderResources.Emission, renderDevice.GetDefaultEmissionTexture());

        if (_current.LightmapTexture is not null)
            GraphicsUtils.AddShaderResource(drawQueue, ShaderResources.Emission, _current.LightmapTexture);
        if (_current.PixelLightRamp is not null)
            GraphicsUtils.AddShaderResource(drawQueue, ShaderResources.LightRamp, _current.PixelLightRamp);
        if (_current.PixelLightShape is not null)
            GraphicsUtils.AddShaderResource(drawQueue, ShaderResources.LightShape, _current.PixelLightShape);
        if (_current.PixelLightShadowMap is not null)
            GraphicsUtils.AddShaderResource(drawQueue, ShaderResources.ShadowMap, _current.PixelLightShadowMap);
        if (_enabled.AmbientLighting && !materialHasEnvironmentMap)
        {
            GraphicsUtils.AddNullableShaderResource(drawQueue, ShaderResources.Reflection0, TextureType.TextureCube,
                _current.ReflectionProbeTexture0);
            GraphicsUtils.AddNullableShaderResource(drawQueue, ShaderResources.Reflection1, TextureType.TextureCube,
                _current.ReflectionProbeTexture1);
        }

        drawQueue.CommitShaderResources();
        _dirty.LightmapTextures = false;
        _dirty.PixelLightTextures = false;
    }

    private void AddDefaultMaterialTexture(DrawCommandQueue drawQueue, StringHash name, RawTexture? defaultTexture)
    {
        if (_current.Material!.GetTexture(name) is null
            && !HasGlobalResource(name)
            && defaultTexture is not null)
            drawQueue.AddShaderResource(name, defaultTexture);
    }

    private bool HasGlobalResource(StringHash name)
    {
        foreach (var resource in _ctx.GlobalResources)
        {
            if (resource.Name == name && resource.Texture is not null)
                return true;
        }

        return false;
    }

    private void AddFrameConstants()
    {
        var drawQueue = _ctx.DrawQueue;
        foreach (var shaderParameter in _ctx.FrameParameters)
            drawQueue.AddShaderParameter(shaderParameter.Name, shaderParameter.Value);

        drawQueue.AddShaderParameter(ShaderConsts.Frame_DeltaTime, _frameInfo.TimeStep);
        var elapsedTimes = _scene.GetElapsedTimes();
        drawQueue.AddShaderParameter(ShaderConsts.Frame_ElapsedTime, new Vector3f((float)elapsedTimes.X, (float)elapsedTimes.Y, (float)elapsedTimes.Z));
    }

    private void AddCameraConstants(float constantDepthBias)
    {
        var drawQueue = _ctx.DrawQueue;
        var camera = _ctx.Camera;
        foreach (var shaderParameter in _ctx.CameraParameters)
            drawQueue.AddShaderParameter(shaderParameter.Name, shaderParameter.Value);

        // XR: we can tell we're not in a shadowmap pass by the InstanceMultiplier at present and
        // for good measure check the camera matches the first camera of additional cameras.
        // Would need to change if shadows ever used multiplexed instancing to write
        // to layers / viewports / or manually-clip.
        if (_ctx.InstanceMultiplier > 1 && _frameInfo.AdditionalCameras.First == camera)
        {
            // Stereoscopic, pull parameters for both eyes.
            // Assumption: quad-views eye-focus targets would be best done as a second stereo target pass
            // because of the difference in frustum sizes (large for far field, narrow for focus).
            var camera0 = _frameInfo.AdditionalCameras.First!;
            var camera1 = _frameInfo.AdditionalCameras.Second!;
            Span<Matrix4> cameraTransform = stackalloc Matrix4[2]
                { camera0.GetEffectiveWorldTransform().Matrix4, camera1.GetEffectiveWorldTransform().Matrix4 };
            Span<Vector4f> cameraPosition = stackalloc Vector4f[2]
                { new(cameraTransform[0].Translation, 0.0f), new(cameraTransform[1].Translation, 0.0f) };
            Span<Matrix4> cameraView = stackalloc Matrix4[2] { camera0.GetView().Matrix4, camera1.GetView().Matrix4 };
            Span<Matrix4> cameraViewProj = stackalloc Matrix4[2]
                { camera0.GetEffectiveGPUViewProjection(constantDepthBias), camera1.GetEffectiveGPUViewProjection(constantDepthBias) };
            Span<Vector4f> depthModes = stackalloc Vector4f[2]
                { BatchRendererHelpers.GetCameraDepthModeParameter(camera0, _backend), BatchRendererHelpers.GetCameraDepthModeParameter(camera1, _backend) };
            Span<Vector4f> depthReconstructions = stackalloc Vector4f[2]
                { BatchRendererHelpers.GetCameraDepthReconstructParameter(camera0), BatchRendererHelpers.GetCameraDepthReconstructParameter(camera1) };

            drawQueue.AddShaderParameter(ShaderConsts.Camera_CameraPos, cameraPosition);
            drawQueue.AddShaderParameter(ShaderConsts.Camera_ViewInv, cameraTransform);
            drawQueue.AddShaderParameter(ShaderConsts.Camera_View, cameraView);
            drawQueue.AddShaderParameter(ShaderConsts.Camera_DepthMode, depthModes);
            drawQueue.AddShaderParameter(ShaderConsts.Camera_DepthReconstruct, depthReconstructions);

            camera0.GetFrustumSize(out _, out var far0);
            camera1.GetFrustumSize(out _, out var far1);
            Span<Vector4f> farVectors = stackalloc Vector4f[2] { new(far0, 0.0f), new(far1, 0.0f) };
            drawQueue.AddShaderParameter(ShaderConsts.Camera_FrustumSize, farVectors);
            drawQueue.AddShaderParameter(ShaderConsts.Camera_ViewProj, cameraViewProj);
        }
        else
        {
            // Not stereoscopic rendering, single eye.
            var cameraTransform = camera.GetEffectiveWorldTransform();
            drawQueue.AddShaderParameter(ShaderConsts.Camera_CameraPos, new Vector4f(cameraTransform.Translation, 0.0f));
            drawQueue.AddShaderParameter(ShaderConsts.Camera_ViewInv, cameraTransform);
            drawQueue.AddShaderParameter(ShaderConsts.Camera_View, camera.GetView());
            drawQueue.AddShaderParameter(ShaderConsts.Camera_DepthMode, BatchRendererHelpers.GetCameraDepthModeParameter(camera, _backend));
            drawQueue.AddShaderParameter(ShaderConsts.Camera_DepthReconstruct, BatchRendererHelpers.GetCameraDepthReconstructParameter(camera));
            camera.GetFrustumSize(out _, out var farVector);
            drawQueue.AddShaderParameter(ShaderConsts.Camera_FrustumSize, new Vector4f(farVector, 0.0f));
            drawQueue.AddShaderParameter(ShaderConsts.Camera_ViewProj, camera.GetEffectiveGPUViewProjection(constantDepthBias));
        }

        // Fill non-stereo parameters.
        drawQueue.AddShaderParameter(ShaderConsts.Camera_ClipPlane, _clipPlane);
        drawQueue.AddShaderParameter(ShaderConsts.Camera_NearClip, camera.GetNearClip());
        drawQueue.AddShaderParameter(ShaderConsts.Camera_FarClip, camera.GetFarClip());

        // Fill shadow split only parameters.
        if (_ctx.OutputShadowSplit is not null)
        {
            ref readonly var lightParams = ref _ctx.OutputShadowSplit.LightProcessor.Params;
            drawQueue.AddShaderParameter(ShaderConsts.Camera_NormalOffsetScale,
                lightParams.ShadowNormalBias[_ctx.OutputShadowSplit.SplitIndex]);
        }

        // Fill color output only parameters.
        if (_enabled.ColorOutput)
        {
            var ambientColorGamma = camera.GetEffectiveAmbientColor() * camera.GetEffectiveAmbientBrightness();
            var fogColorGamma = camera.GetEffectiveFogColor();
            drawQueue.AddShaderParameter(ShaderConsts.Camera_AmbientColor,
                _enabled.LinearColorSpace ? ambientColorGamma.GammaToLinear() : ambientColorGamma);
            drawQueue.AddShaderParameter(ShaderConsts.Camera_FogColor,
                _enabled.LinearColorSpace ? fogColorGamma.GammaToLinear() : fogColorGamma);
            drawQueue.AddShaderParameter(ShaderConsts.Camera_FogParams, BatchRendererHelpers.GetFogParameter(camera));
        }
    }

    private void AddReflectionProbeConstants()
    {
        // Shadow maps need only light position and direction for normal bias.
        if (!_enabled.ColorOutput)
            return;
        var drawQueue = _ctx.DrawQueue;
        var p0 = _current.ReflectionProbe0!;
        var p1 = _current.ReflectionProbe1!;

        if (_settings.CubemapBoxProjection)
        {
            drawQueue.AddShaderParameter(ShaderConsts.Zone_CubemapCenter0, p0.Value.CubemapCenter);
            drawQueue.AddShaderParameter(ShaderConsts.Zone_ProjectionBoxMin0, p0.Value.ProjectionBox.Min.ToVector4());
            drawQueue.AddShaderParameter(ShaderConsts.Zone_ProjectionBoxMax0, p0.Value.ProjectionBox.Max.ToVector4());
        }
        drawQueue.AddShaderParameter(ShaderConsts.Zone_RoughnessToLODFactor0, p0.Value.RoughnessToLodFactor);

        if (_settings.CubemapBoxProjection)
        {
            drawQueue.AddShaderParameter(ShaderConsts.Zone_CubemapCenter1, p1.Value.CubemapCenter);
            drawQueue.AddShaderParameter(ShaderConsts.Zone_ProjectionBoxMin1, p1.Value.ProjectionBox.Min.ToVector4());
            drawQueue.AddShaderParameter(ShaderConsts.Zone_ProjectionBoxMax1, p1.Value.ProjectionBox.Max.ToVector4());
        }
        drawQueue.AddShaderParameter(ShaderConsts.Zone_RoughnessToLODFactor1, p1.Value.RoughnessToLodFactor);
        drawQueue.AddShaderParameter(ShaderConsts.Zone_ReflectionBlendFactor, _current.ReflectionProbesBlendFactor);
    }

    private void AddVertexLightConstants()
    {
        ReadOnlySpan<Vector4f> vertexLightsData = _current.VertexLightsData;
        _ctx.DrawQueue.AddShaderParameter(ShaderConsts.Light_VertexLights,
            vertexLightsData[..(_numVertexLights * VertexLightStride)]);
    }

    private void AddPixelLightConstants(in CookedLightParams params_)
    {
        var drawQueue = _ctx.DrawQueue;
        drawQueue.AddShaderParameter(ShaderConsts.Light_LightDir, params_.Direction);
        drawQueue.AddShaderParameter(ShaderConsts.Light_LightPos, new Vector4f(params_.Position, params_.InverseRange));

        if (!_enabled.ColorOutput)
            return;

        drawQueue.AddShaderParameter(ShaderConsts.Light_LightColor,
            new Vector4f(params_.GetColor(_enabled.LinearColorSpace), params_.EffectiveSpecularIntensity));
        drawQueue.AddShaderParameter(ShaderConsts.Light_LightRad, params_.VolumetricRadius);
        drawQueue.AddShaderParameter(ShaderConsts.Light_LightLength, params_.VolumetricLength);
        drawQueue.AddShaderParameter(ShaderConsts.Light_SpotAngle, new Vector2f(params_.SpotCutoff, params_.InverseSpotCutoff));
        if (params_.LightShape is not null)
            drawQueue.AddShaderParameter(ShaderConsts.Light_LightShapeMatrix, params_.LightShapeMatrix);

        if (params_.NumLightMatrices > 0)
        {
            ReadOnlySpan<Matrix4> lightMatrices = params_.LightMatrices;
            drawQueue.AddShaderParameter(ShaderConsts.Light_LightMatrices, lightMatrices[..params_.NumLightMatrices]);
        }

        if (params_.ShadowMap is not null)
        {
            drawQueue.AddShaderParameter(ShaderConsts.Light_ShadowDepthFade, params_.ShadowDepthFade);
            drawQueue.AddShaderParameter(ShaderConsts.Light_ShadowIntensity, params_.ShadowIntensity);
            drawQueue.AddShaderParameter(ShaderConsts.Light_ShadowMapInvSize, params_.ShadowMapInvSize);
            drawQueue.AddShaderParameter(ShaderConsts.Light_ShadowSplits, params_.ShadowSplitDistances);
            drawQueue.AddShaderParameter(ShaderConsts.Light_ShadowCubeUVBias, params_.ShadowCubeUVBias);
            drawQueue.AddShaderParameter(ShaderConsts.Light_ShadowCubeAdjust, params_.ShadowCubeAdjust);
            drawQueue.AddShaderParameter(ShaderConsts.Light_VSMShadowParams, _settings.VarianceShadowMapParams);
        }
    }

    // Draw operations.
    private void LogHeadlessBuffersError(Drawable? drawable)
    {
        Log.GetLogger().LogError("Cannot render Geometry that uses headless buffers: {Name}",
            drawable?.GetFullNameDebug() ?? "unknown Drawable");
    }

    private void CommitDrawCalls(int numInstances, in SourceBatch sourceBatch, ReadOnlySpan<Matrix3x4> worldTransform)
    {
        var drawQueue = _ctx.DrawQueue;
        var hasIndexBuffer = _current.Geometry!.GetIndexBuffer() is not null;

        if (_dirty.Geometry)
        {
            GraphicsUtils.SetBuffersFromGeometry(drawQueue, _current.Geometry);
            if (_current.Geometry.IsHeadless())
            {
                LogHeadlessBuffersError(_current.Drawable);
                return;
            }
        }

        for (var i = 0; i < numInstances; ++i)
        {
            var cameraNode = _frameInfo.ViewReferenceNode ?? _cameraNode;
            _objectParameterBuilder.AddBatchUniformsToDrawQueue(drawQueue, cameraNode, in sourceBatch, worldTransform, i);

            if (_ctx.InstanceMultiplier == 1)
            {
                if (hasIndexBuffer)
                    drawQueue.DrawIndexed(checked((int)_current.Geometry.GetIndexStart()),
                        checked((int)_current.Geometry.GetIndexCount()));
                else
                    drawQueue.Draw(checked((int)_current.Geometry.GetVertexStart()),
                        checked((int)_current.Geometry.GetVertexCount()));
            }
            else // If multiplying instance counts for a purpose such as stereo, replace uninstanced draws with instanced draws.
            {
                if (hasIndexBuffer)
                    drawQueue.DrawIndexedInstanced(checked((int)_current.Geometry.GetIndexStart()),
                        checked((int)_current.Geometry.GetIndexCount()), 0, _ctx.InstanceMultiplier);
                else
                    drawQueue.DrawInstanced(checked((int)_current.Geometry.GetVertexStart()),
                        checked((int)_current.Geometry.GetVertexCount()), 0, _ctx.InstanceMultiplier);
            }
        }
    }

    private void CommitInstancedDrawCalls()
    {
        System.Diagnostics.Debug.Assert(_instancingGroup.Count > 0);
        var geometry = _instancingGroup.Geometry!;
        if (geometry.IsHeadless())
        {
            LogHeadlessBuffersError(_instancingGroup.FirstDrawable);
            return;
        }

        GraphicsUtils.SetBuffersFromGeometry(_ctx.DrawQueue, geometry, _instancingBuffer.VertexBuffer);
        _ctx.DrawQueue.DrawIndexedInstanced(checked((int)geometry.GetIndexStart()),
            checked((int)geometry.GetIndexCount()), checked((int)_instancingGroup.Start),
            checked(_instancingGroup.Count * _ctx.InstanceMultiplier));
        _instancingGroup.Count = 0;
    }

    private void ProcessBatch(in PipelineBatch pipelineBatch, in SourceBatch sourceBatch,
        ReadOnlySpan<Matrix3x4> worldTransform)
    {
        if (pipelineBatch.Geometry!.GetEffectiveIndexCount() == 0)
            return;

        ref readonly var lightAccumulator = ref Unsafe.NullRef<LightAccumulator>();
        if (_enabled.AmbientLighting || _enabled.VertexLighting)
            lightAccumulator = ref _drawableProcessor.GetGeometryLighting(pipelineBatch.DrawableIndex);

        // Update dirty flags and cached state.
        CheckDirtyCommonState(pipelineBatch);
        if (_enabled.PixelLighting)
            CheckDirtyPixelLight(pipelineBatch);
        if (_enabled.VertexLighting)
            CheckDirtyVertexLight(in lightAccumulator);
        if (_enabled.AmbientLighting)
        {
            CheckDirtyReflectionProbe(in lightAccumulator);
            CheckDirtyLightmap(in sourceBatch);
        }

        var numBatchInstances = pipelineBatch.GeometryType == GeometryType.Static
            ? checked((int)sourceBatch.NumWorldTransforms) : 1;
        var resetInstancingGroup = _instancingGroup.Count == 0 || _dirty.IsAnythingDirty();

        if (_debuggerEnabled)
            _debugger!.ReportSceneBatch(new DebugFrameSnapshotBatch(_drawableProcessor, pipelineBatch, resetInstancingGroup));

        if (resetInstancingGroup)
        {
            if (_instancingGroup.Count > 0)
                CommitInstancedDrawCalls();

            if (_dirty.PipelineState)
                _ctx.DrawQueue.SetPipelineState(_current.PipelineState!);

            if (_enabled.LightMaskToStencil)
                _ctx.DrawQueue.SetStencilRef(_current.Drawable!.GetLightMaskInZone() & PortableLightMask);

            UpdateDirtyConstants();
            UpdateDirtyResources();

            if (_objectParameterBuilder.IsBatchInstanced(pipelineBatch))
            {
                _instancingGroup.Count = numBatchInstances;
                _instancingGroup.Start = _instanceIndex;
                _instancingGroup.Geometry = _current.Geometry;
                _instancingGroup.FirstDrawable = _current.Drawable;
                _instanceIndex += numBatchInstances;
            }
            else
            {
                if (_objectParameterBuilder.IsAmbientEnabled())
                    _objectParameterBuilder.SetBatchAmbient(in lightAccumulator);
                CommitDrawCalls(numBatchInstances, in sourceBatch, worldTransform);
            }
        }
        else
        {
            _instancingGroup.Count += numBatchInstances;
            _instanceIndex += numBatchInstances;
        }
    }

    // External state (required).
    private readonly BatchRendererSettings _settings;
    private readonly RenderBackend _backend;
    private readonly int _numVertexLights;
    private readonly RenderPipelineDebugger? _debugger;
    private readonly bool _debuggerEnabled;
    private readonly DrawableProcessor _drawableProcessor;
    private readonly InstancingBuffer _instancingBuffer;
    private readonly FrameInfo _frameInfo;
    private readonly Scene _scene;
    private readonly IReadOnlyList<LightProcessor?> _lights;
    private readonly Node _cameraNode;
    private readonly float _depthRange;
    private readonly Vector4f _clipPlane;

    private readonly EnabledFeatureFlags _enabled;

    private readonly struct EnabledFeatureFlags
    {
        public EnabledFeatureFlags(BatchRenderFlag flags, InstancingBuffer instancingBuffer)
        {
            AmbientLighting = (flags & BatchRenderFlag.EnableAmbientLighting) != 0;
            VertexLighting = (flags & BatchRenderFlag.EnableVertexLights) != 0;
            PixelLighting = (flags & BatchRenderFlag.EnablePixelLights) != 0;
            AnyLighting = AmbientLighting || VertexLighting || PixelLighting;
            ColorOutput = (flags & BatchRenderFlag.DisableColorOutput) == 0;
            LightMaskToStencil = (flags & BatchRenderFlag.LightMaskToStencil) != 0;
            LinearColorSpace = (flags & BatchRenderFlag.LinearColorSpace) != 0;
        }

        public bool AmbientLighting { get; }
        public bool VertexLighting { get; }
        public bool PixelLighting { get; }
        public bool AnyLighting { get; }
        public bool ColorOutput { get; }
        public bool LightMaskToStencil { get; }
        public bool LinearColorSpace { get; }
    }

    private struct DirtyStateFlags
    {
        public bool PipelineState;
        public bool Material;
        public bool Geometry;
        public bool ReflectionProbe;
        public bool CameraConstants;
        public bool PixelLightConstants;
        public bool VertexLightConstants;
        public bool LightmapConstants;
        public bool PixelLightTextures;
        public bool LightmapTextures;

        public DirtyStateFlags()
        {
            CameraConstants = true;
        }

        public readonly bool IsStateDirty() => PipelineState || Material || Geometry || ReflectionProbe;
        public readonly bool IsConstantsDirty() => CameraConstants || PixelLightConstants || VertexLightConstants || LightmapConstants;
        public readonly bool IsResourcesDirty() => PixelLightTextures || LightmapTextures;
        public readonly bool IsAnythingDirty() => IsStateDirty() || IsConstantsDirty() || IsResourcesDirty();
    }

    private DirtyStateFlags _dirty;

    [InlineArray(LightAccumulator.MaxVertexLights * VertexLightStride)]
    private struct VertexLightDataContainer
    {
        private Vector4f _element0;
    }

    private struct CachedSharedState
    {
        public PipelineState? PipelineState;
        public float ConstantDepthBias;
        public StrongBox<ReflectionProbeData>? ReflectionProbe0;
        public StrongBox<ReflectionProbeData>? ReflectionProbe1;
        public TextureCube? ReflectionProbeTexture0;
        public TextureCube? ReflectionProbeTexture1;
        public float ReflectionProbesBlendFactor;
        public int PixelLightIndex;
        public bool PixelLightEnabled;
        public LightProcessor? PixelLightProcessor;
        public Texture? PixelLightRamp;
        public Texture? PixelLightShape;
        public Texture2D? PixelLightShadowMap;
        public LightAccumulator.VertexLightContainer VertexLights;
        public VertexLightDataContainer VertexLightsData;
        public Texture2D? LightmapTexture;
        public Vector4f? LightmapScaleOffset;
        public Material? Material;
        public Geometry? Geometry;
        public Drawable? Drawable;

        public CachedSharedState()
        {
            PixelLightIndex = int.MaxValue;
        }
    }

    private CachedSharedState _current;

    private struct InstancingGroupState
    {
        public Geometry? Geometry;
        public Drawable? FirstDrawable;
        public int Start;
        public int Count;
    }

    private InstancingGroupState _instancingGroup;

    private struct LightVolumeBatchHelpers
    {
        public Matrix3x4 Transform;
        public SourceBatch SourceBatch;

        public LightVolumeBatchHelpers()
        {
            SourceBatch = new SourceBatch();
        }
    }

    private LightVolumeBatchHelpers _lightVolumeHelpers;
    private ObjectParameterBuilder _objectParameterBuilder;
    private int _instanceIndex;
}
