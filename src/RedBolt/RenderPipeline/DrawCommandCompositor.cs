using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace RedBolt;

internal static class BatchRendererHelpers
{
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

    public static Vector4f GetFogParameter(Camera camera)
    {
        var farClip = camera.GetFarClip();
        var fogStart = Math.Min(camera.GetEffectiveFogStart(), farClip);
        var fogEnd = Math.Min(camera.GetEffectiveFogEnd(), farClip);
        if (fogStart >= fogEnd * (1.0f - MathDefs.LargeEpsilon))
            fogStart = fogEnd * (1.0f - MathDefs.LargeEpsilon);
        var fogRange = Math.Max(fogEnd - fogStart, MathDefs.Epsilon);
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

internal sealed class ObjectParameterBuilder
{
    private readonly bool _instancingEnabled;
    private readonly bool _ambientEnabled;
    private readonly DrawableAmbientMode _ambientMode;
    private readonly bool _linearColorSpace;
    private Vector4f _ambientValueFlat;
    private SphericalHarmonicsDot9? _ambientValueSh;

    public ObjectParameterBuilder(BatchRendererSettings settings, BatchRenderFlag flags)
    {
        _instancingEnabled = (flags & BatchRenderFlag.EnableInstancingForStaticGeometry) != 0;
        _ambientEnabled = (flags & BatchRenderFlag.EnableAmbientLighting) != 0;
        _ambientMode = settings.AmbientMode;
        _linearColorSpace = (flags & BatchRenderFlag.LinearColorSpace) != 0;
    }

    public bool IsInstancingSupported() => _instancingEnabled;
    public bool IsAmbientEnabled() => _ambientEnabled;

    public bool IsBatchInstanced(PipelineBatch pipelineBatch)
        => _instancingEnabled && pipelineBatch.Geometry is not null
           && pipelineBatch.Geometry.IsInstanced(pipelineBatch.GeometryType);

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

    public void AddBatchesToInstancingBuffer(InstancingBuffer instancingBuffer, SourceBatch sourceBatch, int instanceIndex)
    {
        instancingBuffer.AddInstance();
        instancingBuffer.SetElements(in sourceBatch.WorldTransform[instanceIndex], 0, 3);
        if (_ambientEnabled)
        {
            if (_ambientMode == DrawableAmbientMode.Flat)
                instancingBuffer.SetElements(in _ambientValueFlat, 3, 1);
            else if (_ambientMode == DrawableAmbientMode.Directional && _ambientValueSh is { } sh)
                instancingBuffer.SetElements(in sh, 3, 7);
        }
    }

    public void AddBatchUniformsToDrawQueue(DrawCommandQueue drawQueue, Node cameraNode, SourceBatch sourceBatch, int instanceIndex)
    {
        if (!drawQueue.BeginShaderParameterGroup(ShaderParameterGroup.Object, true))
            return;

        if (_ambientEnabled)
        {
            if (_ambientMode == DrawableAmbientMode.Flat)
                drawQueue.AddShaderParameter(ShaderConsts.Object_Ambient, _ambientValueFlat);
            else if (_ambientMode == DrawableAmbientMode.Directional && _ambientValueSh is { } sh)
            {
                drawQueue.AddShaderParameter(ShaderConsts.Object_SHAr, sh.Ar);
                drawQueue.AddShaderParameter(ShaderConsts.Object_SHAg, sh.Ag);
                drawQueue.AddShaderParameter(ShaderConsts.Object_SHAb, sh.Ab);
                drawQueue.AddShaderParameter(ShaderConsts.Object_SHBr, sh.Br);
                drawQueue.AddShaderParameter(ShaderConsts.Object_SHBg, sh.Bg);
                drawQueue.AddShaderParameter(ShaderConsts.Object_SHBb, sh.Bb);
                drawQueue.AddShaderParameter(ShaderConsts.Object_SHC, sh.C);
            }
        }

        switch (sourceBatch.GeometryType)
        {
            case GeometryType.Skinned:
            {
                var count = (int)Math.Min(sourceBatch.NumWorldTransforms, Graphics.GetMaxBones());
                drawQueue.AddShaderParameter(ShaderConsts.Object_SkinMatrices, sourceBatch.WorldTransform.AsSpan(0, count));
                break;
            }
            case GeometryType.Billboard:
                drawQueue.AddShaderParameter(ShaderConsts.Object_Model, sourceBatch.WorldTransform[0]);
                if (sourceBatch.NumWorldTransforms > 1)
                    drawQueue.AddShaderParameter(ShaderConsts.Object_BillboardRot, sourceBatch.WorldTransform[1].RotationMatrix);
                else
                    drawQueue.AddShaderParameter(ShaderConsts.Object_BillboardRot, cameraNode.GetWorldRotation().RotationMatrix);
                break;
            default:
                drawQueue.AddShaderParameter(ShaderConsts.Object_Model, sourceBatch.WorldTransform[instanceIndex]);
                break;
        }

        drawQueue.CommitShaderParameterGroup(ShaderParameterGroup.Object);
    }
}

/// <summary>Batch renderer to command queue. Port of C++ DrawCommandCompositor.</summary>
internal sealed class DrawCommandCompositor
{
    public const uint VertexLightStride = 3;
    private const uint PortableLightMask = 0xff;
    private static readonly CookedLightParams NullVertexLight;

    private readonly BatchRenderingContext _ctx;
    private readonly BatchRendererSettings _settings;
    private readonly RenderBackend _backend;
    private readonly uint _numVertexLights;
    private readonly RenderPipelineDebugger? _debugger;
    private readonly bool _debuggerEnabled;
    private readonly DrawableProcessor _drawableProcessor;
    private readonly InstancingBuffer? _instancingBuffer;
    private readonly CommonFrameInfo _frameInfo;
    private readonly Scene _scene;
    private readonly IReadOnlyList<LightProcessor?> _lights;
    private readonly Node _cameraNode;
    private readonly float _depthRange;
    private readonly Vector4f _clipPlane;
    private readonly EnabledFeatureFlags _enabled;
    private readonly ObjectParameterBuilder _objectParameterBuilder;
    private DirtyStateFlags _dirty;
    private CachedSharedState _current;
    private InstancingGroupState _instancingGroup;
    private LightVolumeBatchHelpers _lightVolumeHelpers;
    private int _instanceIndex;
    private readonly Vector4f[] _vertexLightsData = new Vector4f[LightAccumulator.MaxVertexLights * VertexLightStride];

    public DrawCommandCompositor(BatchRenderingContext ctx, BatchRendererSettings settings,
        RenderPipelineDebugger? debugger, DrawableProcessor drawableProcessor, InstancingBuffer? instancingBuffer,
        BatchRenderFlag flags, int startInstance, bool debuggerEnabled)
    {
        _ctx = ctx;
        _settings = settings;
        _backend = ctx.Camera.GetSubsystem<RenderDevice>()?.GetBackend() ?? RenderBackend.OpenGL;
        _numVertexLights = drawableProcessor.GetSettings().MaxVertexLights;
        _debugger = debugger;
        _debuggerEnabled = debuggerEnabled;
        _drawableProcessor = drawableProcessor;
        _instancingBuffer = instancingBuffer;
        _frameInfo = drawableProcessor.GetFrameInfo();
        _scene = _frameInfo.Scene ?? throw new InvalidOperationException("DrawableProcessor frame has no scene.");
        _lights = drawableProcessor.GetLightProcessors();
        _cameraNode = ctx.Camera.Node ?? throw new InvalidOperationException("Camera has no node.");
        _depthRange = ctx.Camera.GetFarClip();
        _clipPlane = BatchRendererHelpers.GetClipPlane(ctx.Camera);
        _enabled = new EnabledFeatureFlags(flags);
        _objectParameterBuilder = new ObjectParameterBuilder(settings, flags);
        _instanceIndex = startInstance;
        _dirty = DirtyStateFlags.CreateInitial();
        _current = new CachedSharedState();
    }

    public void ProcessSceneBatch(PipelineBatch pipelineBatch)
        => ProcessBatch(pipelineBatch, pipelineBatch.GetSourceBatch());

    public void ProcessLightVolumeBatch(PipelineBatch pipelineBatch)
    {
        if (pipelineBatch.PixelLightIndex >= _lights.Count)
            return;
        var light = _lights[pipelineBatch.PixelLightIndex]?.Light;
        if (light is null)
            return;
        _lightVolumeHelpers.Transform = light.GetVolumeTransform(_ctx.Camera);
        _lightVolumeHelpers.SourceBatch = new SourceBatch
        {
            WorldTransform = [_lightVolumeHelpers.Transform],
            NumWorldTransforms = 1
        };
        ProcessBatch(pipelineBatch, _lightVolumeHelpers.SourceBatch);
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

    private void ProcessBatch(PipelineBatch pipelineBatch, SourceBatch sourceBatch)
    {
        if (pipelineBatch.Geometry is null || pipelineBatch.Geometry.GetEffectiveIndexCount() == 0)
            return;

        ref readonly var lightAccumulator = ref Unsafe.NullRef<LightAccumulator>();
        if (_enabled.AmbientLighting || _enabled.VertexLighting)
            lightAccumulator = ref _drawableProcessor.GetGeometryLighting(checked((uint)pipelineBatch.DrawableIndex));

        CheckDirtyCommonState(pipelineBatch);
        if (_enabled.PixelLighting)
            CheckDirtyPixelLight(pipelineBatch);
        if (_enabled.VertexLighting)
            CheckDirtyVertexLight(in lightAccumulator);
        if (_enabled.AmbientLighting)
        {
            CheckDirtyReflectionProbe(in lightAccumulator);
            CheckDirtyLightmap(sourceBatch);
        }

        var numBatchInstances = pipelineBatch.GeometryType == GeometryType.Static
            ? checked((int)sourceBatch.NumWorldTransforms) : 1;
        var resetInstancingGroup = _instancingGroup.Count == 0 || _dirty.IsAnythingDirty();

        if (_debuggerEnabled && _debugger is not null)
            _debugger.ReportSceneBatch(new DebugFrameSnapshotBatch(_drawableProcessor, pipelineBatch, resetInstancingGroup));

        if (resetInstancingGroup)
        {
            if (_instancingGroup.Count > 0)
                CommitInstancedDrawCalls();

            if (_dirty.PipelineState && pipelineBatch.PipelineState is not null)
                _ctx.DrawQueue.SetPipelineState(pipelineBatch.PipelineState);

            if (_enabled.LightMaskToStencil && pipelineBatch.Drawable is not null)
                _ctx.DrawQueue.SetStencilRef(pipelineBatch.Drawable.GetLightMaskInZone() & PortableLightMask);

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
                CommitDrawCalls(numBatchInstances, sourceBatch);
            }
        }
        else
        {
            _instancingGroup.Count += numBatchInstances;
            _instanceIndex += numBatchInstances;
        }
    }

    private void CheckDirtyCommonState(PipelineBatch pipelineBatch)
    {
        _dirty.PipelineState = !ReferenceEquals(_current.PipelineState, pipelineBatch.PipelineState);
        _current.PipelineState = pipelineBatch.PipelineState;

        var constantDepthBias = 0.0f;
        if (_current.PipelineState?.GetDesc().AsGraphics() is { } graphicsDesc)
            constantDepthBias = graphicsDesc.ConstantDepthBias;
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
        _dirty.ReflectionProbe = !ReferenceEquals(_current.ReflectionProbes[0], p0)
            || !ReferenceEquals(_current.ReflectionProbes[1], p1)
            || _current.ReflectionProbesBlendFactor != lightAccumulator.ReflectionProbesBlendFactor;
        if (!_dirty.ReflectionProbe)
            return;
        _current.ReflectionProbes[0] = p0;
        _current.ReflectionProbes[1] = p1;
        _current.ReflectionProbeTextures[0] = p0?.Value.ReflectionMap;
        _current.ReflectionProbeTextures[1] = p1?.Value.ReflectionMap;
        _current.ReflectionProbesBlendFactor = lightAccumulator.ReflectionProbesBlendFactor;
    }

    private void CheckDirtyPixelLight(PipelineBatch pipelineBatch)
    {
        _dirty.PixelLightConstants = _current.PixelLightIndex != pipelineBatch.PixelLightIndex;
        if (!_dirty.PixelLightConstants)
            return;

        _current.PixelLightIndex = pipelineBatch.PixelLightIndex;
        _current.PixelLightEnabled = _current.PixelLightIndex != int.MaxValue
            && _current.PixelLightIndex < _lights.Count
            && _lights[_current.PixelLightIndex] is not null;
        if (_current.PixelLightEnabled)
        {
            _current.PixelLightProcessor = _lights[_current.PixelLightIndex];
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
            var lightProcessor = lightIndex != int.MaxValue && lightIndex < _lights.Count
                ? _lights[lightIndex]
                : null;
            ref readonly var params_ = ref (lightProcessor is not null
                ? ref lightProcessor.Params
                : ref NullVertexLight);
            var color = params_.GetColor(_enabled.LinearColorSpace);
            _vertexLightsData[i * VertexLightStride] = new Vector4f(color, params_.InverseRange);
            _vertexLightsData[i * VertexLightStride + 1] = new Vector4f(params_.Direction, params_.SpotCutoff);
            _vertexLightsData[i * VertexLightStride + 2] = new Vector4f(params_.Position, params_.InverseSpotCutoff);
        }
    }

    private void CheckDirtyLightmap(SourceBatch sourceBatch)
    {
        var prev = _current.LightmapScaleOffset;
        var lightmapChanged = !Nullable.Equals(prev, sourceBatch.LightmapScaleOffset);
        _dirty.LightmapConstants = lightmapChanged;
        if (!lightmapChanged)
            return;

        _current.LightmapScaleOffset = sourceBatch.LightmapScaleOffset;
        Texture2D? lightmapTexture = sourceBatch.LightmapScaleOffset is not null
            ? _scene.GetLightmapTexture(sourceBatch.LightmapIndex)
            : null;
        _dirty.LightmapTextures = !ReferenceEquals(_current.LightmapTexture, lightmapTexture);
        _current.LightmapTexture = lightmapTexture;
    }

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

        if (_current.Material is not null
            && drawQueue.BeginShaderParameterGroup(ShaderParameterGroup.Material, _dirty.Material || _dirty.LightmapConstants))
        {
            foreach (var (name, parameter) in _current.Material.GetShaderParameters())
            {
                if (parameter.IsCustom)
                    continue;
                if (name == (string)ShaderConsts.Material_FadeOffsetScale && parameter.Value is Vector2f param)
                {
                    var paramAdjusted = new Vector2f(param.X / _depthRange, _depthRange / param.Y);
                    drawQueue.AddShaderParameter(new StringHash(name), paramAdjusted);
                }
                else if (parameter.Value is not null)
                    AddMaterialParameter(drawQueue, name, parameter.Value);
            }

            if (_enabled.AmbientLighting && _current.LightmapScaleOffset is { } lmOffset)
                drawQueue.AddShaderParameter(ShaderConsts.Material_LMOffset, lmOffset);

            drawQueue.CommitShaderParameterGroup(ShaderParameterGroup.Material);
        }
        _dirty.LightmapConstants = false;

        if (_current.Material is not null && drawQueue.BeginShaderParameterGroup(ShaderParameterGroup.Custom, _dirty.Material))
        {
            foreach (var (name, parameter) in _current.Material.GetShaderParameters())
            {
                if (parameter.IsCustom && parameter.Value is not null)
                    AddMaterialParameter(drawQueue, name, parameter.Value);
            }
            drawQueue.CommitShaderParameterGroup(ShaderParameterGroup.Custom);
        }
    }

    private static void AddMaterialParameter(DrawCommandQueue drawQueue, string name, object value)
    {
        var hash = new StringHash(name);
        switch (value)
        {
            case float f: drawQueue.AddShaderParameter(hash, f); break;
            case Vector2f v2: drawQueue.AddShaderParameter(hash, v2); break;
            case Vector3f v3: drawQueue.AddShaderParameter(hash, v3); break;
            case Vector4f v4: drawQueue.AddShaderParameter(hash, v4); break;
            case Color c: drawQueue.AddShaderParameter(hash, c.ToVector4()); break;
            case Matrix3x4 m34: drawQueue.AddShaderParameter(hash, m34); break;
            case Matrix4 m4: drawQueue.AddShaderParameter(hash, m4); break;
            case Variant variant: drawQueue.AddShaderParameter(hash, variant); break;
            case int i: drawQueue.AddShaderParameter(hash, i); break;
            case bool b: drawQueue.AddShaderParameter(hash, b ? 1.0f : 0.0f); break;
        }
    }

    private void UpdateDirtyResources()
    {
        var resourcesDirty = _dirty.PipelineState || _dirty.Material || _dirty.ReflectionProbe || _dirty.IsResourcesDirty();
        if (!resourcesDirty)
            return;

        var drawQueue = _ctx.DrawQueue;
        foreach (var desc in _ctx.GlobalResources.Span)
        {
            if (desc.Texture is not null)
                GraphicsUtils.AddShaderResource(drawQueue, desc.Name, desc.Texture);
        }

        var materialHasEnvironmentMap = false;
        if (_current.Material is not null)
        {
            foreach (var (nameHash, texture) in _current.Material.GetTextures())
            {
                if (nameHash == (StringHash)ShaderResources.Reflection0)
                    materialHasEnvironmentMap = true;
                if (nameHash == (StringHash)ShaderResources.Emission && _current.LightmapTexture is not null)
                    continue;
                GraphicsUtils.AddShaderResource(drawQueue, nameHash, texture.Texture);
            }
        }

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
                _current.ReflectionProbeTextures[0]);
            GraphicsUtils.AddNullableShaderResource(drawQueue, ShaderResources.Reflection1, TextureType.TextureCube,
                _current.ReflectionProbeTextures[1]);
        }

        drawQueue.CommitShaderResources();
        _dirty.LightmapTextures = false;
        _dirty.PixelLightTextures = false;
    }

    private void AddFrameConstants()
    {
        var drawQueue = _ctx.DrawQueue;
        foreach (var shaderParameter in _ctx.FrameParameters.Span)
            drawQueue.AddShaderParameter(shaderParameter.Name, shaderParameter.Value);

        drawQueue.AddShaderParameter(ShaderConsts.Frame_DeltaTime, _frameInfo.TimeStep);
        var elapsedTimes = _scene.GetElapsedTimes();
        drawQueue.AddShaderParameter(ShaderConsts.Frame_ElapsedTime, new Vector3f((float)elapsedTimes.X, (float)elapsedTimes.Y, (float)elapsedTimes.Z));
    }

    private void AddCameraConstants(float constantDepthBias)
    {
        var drawQueue = _ctx.DrawQueue;
        var camera = _ctx.Camera;
        foreach (var shaderParameter in _ctx.CameraParameters.Span)
            drawQueue.AddShaderParameter(shaderParameter.Name, shaderParameter.Value);

        if (_ctx.InstanceMultiplier > 1 && _frameInfo.Cameras.First == camera)
        {
            var cameras = new[] { _frameInfo.Cameras.First!, _frameInfo.Cameras.Second! };
            var cameraTransform = new[] { cameras[0].GetEffectiveWorldTransform().Matrix4, cameras[1].GetEffectiveWorldTransform().Matrix4 };
            var cameraPosition = new[] { new Vector4f(cameraTransform[0].Translation, 0.0f), new Vector4f(cameraTransform[1].Translation, 0.0f) };
            var cameraView = new[] { cameras[0].GetView().Matrix4, cameras[1].GetView().Matrix4 };
            var cameraViewProj = new[] { cameras[0].GetEffectiveGPUViewProjection(constantDepthBias), cameras[1].GetEffectiveGPUViewProjection(constantDepthBias) };
            var depthModes = new[] { BatchRendererHelpers.GetCameraDepthModeParameter(cameras[0], _backend), BatchRendererHelpers.GetCameraDepthModeParameter(cameras[1], _backend) };
            var depthReconstructions = new[] { BatchRendererHelpers.GetCameraDepthReconstructParameter(cameras[0]), BatchRendererHelpers.GetCameraDepthReconstructParameter(cameras[1]) };

            drawQueue.AddShaderParameter(ShaderConsts.Camera_CameraPos, cameraPosition.AsSpan());
            drawQueue.AddShaderParameter(ShaderConsts.Camera_ViewInv, cameraTransform.AsSpan());
            drawQueue.AddShaderParameter(ShaderConsts.Camera_View, cameraView.AsSpan());
            drawQueue.AddShaderParameter(ShaderConsts.Camera_DepthMode, depthModes.AsSpan());
            drawQueue.AddShaderParameter(ShaderConsts.Camera_DepthReconstruct, depthReconstructions.AsSpan());

            cameras[0].GetFrustumSize(out _, out var far0);
            cameras[1].GetFrustumSize(out _, out var far1);
            var farVectors = new[] { new Vector4f(far0, 0.0f), new Vector4f(far1, 0.0f) };
            drawQueue.AddShaderParameter(ShaderConsts.Camera_FrustumSize, farVectors.AsSpan());
            drawQueue.AddShaderParameter(ShaderConsts.Camera_ViewProj, cameraViewProj.AsSpan());
        }
        else
        {
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

        drawQueue.AddShaderParameter(ShaderConsts.Camera_ClipPlane, _clipPlane);
        drawQueue.AddShaderParameter(ShaderConsts.Camera_NearClip, camera.GetNearClip());
        drawQueue.AddShaderParameter(ShaderConsts.Camera_FarClip, camera.GetFarClip());

        if (_ctx.OutputShadowSplit is not null)
        {
            ref readonly var lightParams = ref _ctx.OutputShadowSplit.LightProcessor.Params;
            drawQueue.AddShaderParameter(ShaderConsts.Camera_NormalOffsetScale,
                lightParams.ShadowNormalBias[_ctx.OutputShadowSplit.SplitIndex]);
        }

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
        if (!_enabled.ColorOutput)
            return;
        var drawQueue = _ctx.DrawQueue;
        var p0 = _current.ReflectionProbes[0];
        var p1 = _current.ReflectionProbes[1];
        if (p0 is null || p1 is null)
            return;

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
        _ctx.DrawQueue.AddShaderParameter(ShaderConsts.Light_VertexLights,
            _vertexLightsData.AsSpan(0, (int)(_numVertexLights * VertexLightStride)));
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

    private void CommitDrawCalls(int numInstances, SourceBatch sourceBatch)
    {
        if (_current.Geometry is null)
            return;
        var drawQueue = _ctx.DrawQueue;
        var hasIndexBuffer = _current.Geometry.GetIndexBuffer() is not null;

        if (_dirty.Geometry)
        {
            GraphicsUtils.SetBuffersFromGeometry(drawQueue, _current.Geometry);
            if (_current.Geometry.IsHeadless())
            {
                Log.GetLogger().LogError("Cannot render Geometry that uses headless buffers: {Name}",
                    _current.Drawable?.GetFullNameDebug() ?? "unknown Drawable");
                return;
            }
        }

        for (var i = 0; i < numInstances; ++i)
        {
            var cameraNode = _frameInfo.ViewReferenceNode ?? _cameraNode;
            _objectParameterBuilder.AddBatchUniformsToDrawQueue(drawQueue, cameraNode, sourceBatch, i);

            if (_ctx.InstanceMultiplier == 1)
            {
                if (hasIndexBuffer)
                    drawQueue.DrawIndexed(checked((int)_current.Geometry.GetIndexStart()),
                        checked((int)_current.Geometry.GetIndexCount()));
                else
                    drawQueue.Draw(checked((int)_current.Geometry.GetVertexStart()),
                        checked((int)_current.Geometry.GetVertexCount()));
            }
            else
            {
                if (hasIndexBuffer)
                    drawQueue.DrawIndexedInstanced(checked((int)_current.Geometry.GetIndexStart()),
                        checked((int)_current.Geometry.GetIndexCount()), 0, checked((int)_ctx.InstanceMultiplier));
                else
                    drawQueue.DrawInstanced(checked((int)_current.Geometry.GetVertexStart()),
                        checked((int)_current.Geometry.GetVertexCount()), 0, checked((int)_ctx.InstanceMultiplier));
            }
        }
    }

    private void CommitInstancedDrawCalls()
    {
        System.Diagnostics.Debug.Assert(_instancingGroup.Count > 0);
        var geometry = _instancingGroup.Geometry;
        if (geometry is null)
            return;
        if (geometry.IsHeadless())
        {
            Log.GetLogger().LogError("Cannot render Geometry that uses headless buffers: {Name}",
                _instancingGroup.FirstDrawable?.GetFullNameDebug() ?? "unknown Drawable");
            return;
        }

        GraphicsUtils.SetBuffersFromGeometry(_ctx.DrawQueue, geometry, _instancingBuffer?.VertexBuffer);
        _ctx.DrawQueue.DrawIndexedInstanced(checked((int)geometry.GetIndexStart()),
            checked((int)geometry.GetIndexCount()), checked((int)_instancingGroup.Start),
            checked((int)(_instancingGroup.Count * _ctx.InstanceMultiplier)));
        _instancingGroup.Count = 0;
    }

    private readonly struct EnabledFeatureFlags
    {
        public EnabledFeatureFlags(BatchRenderFlag flags)
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

        public static DirtyStateFlags CreateInitial() => new() { CameraConstants = true };

        public readonly bool IsStateDirty() => PipelineState || Material || Geometry || ReflectionProbe;
        public readonly bool IsConstantsDirty() => CameraConstants || PixelLightConstants || VertexLightConstants || LightmapConstants;
        public readonly bool IsResourcesDirty() => PixelLightTextures || LightmapTextures;
        public readonly bool IsAnythingDirty() => IsStateDirty() || IsConstantsDirty() || IsResourcesDirty();
    }

    private struct CachedSharedState
    {
        public PipelineState? PipelineState;
        public float ConstantDepthBias;
        public StrongBox<ReflectionProbeData>?[] ReflectionProbes;
        public TextureCube?[] ReflectionProbeTextures;
        public float ReflectionProbesBlendFactor;
        public int PixelLightIndex;
        public bool PixelLightEnabled;
        public LightProcessor? PixelLightProcessor;
        public Texture? PixelLightRamp;
        public Texture? PixelLightShape;
        public Texture2D? PixelLightShadowMap;
        public LightAccumulator.VertexLightContainer VertexLights;
        public Texture2D? LightmapTexture;
        public Vector4f? LightmapScaleOffset;
        public Material? Material;
        public Geometry? Geometry;
        public Drawable? Drawable;

        public CachedSharedState()
        {
            ReflectionProbes = new StrongBox<ReflectionProbeData>?[2];
            ReflectionProbeTextures = new TextureCube?[2];
            PixelLightIndex = int.MaxValue;
        }
    }

    private struct InstancingGroupState
    {
        public Geometry? Geometry;
        public Drawable? FirstDrawable;
        public int Start;
        public int Count;
    }

    private struct LightVolumeBatchHelpers
    {
        public Matrix3x4 Transform;
        public SourceBatch SourceBatch;
    }
}
