// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.RenderPipeline;

using Microsoft.Extensions.Logging;

using RedBolt.Core;
using RedBolt.Graphics;
using FillMode = global::RedBolt.Graphics.FillMode;
using RedBolt.Logging;
using RedBolt.RenderAPI;
using RedBolt.Scene;

/// <summary>Utility to process render camera (not cull camera!).</summary>
public class CameraProcessor : RedObject
{
    private bool _isCameraOrthographic;
    private bool _isCameraFlippedByUser;
    private bool _isReflectionCamera;
    private bool _isCameraClipped;
    private bool _flipCameraForRendering;
    private FillMode _cameraFillMode;
    private readonly List<Camera?> _cameras = [];

    /// <summary>Graphics instance</summary>
    private readonly Graphics? _graphics;

    public CameraProcessor()
    {
        _graphics = GetSubsystem<Graphics>();
    }

    public override void Dispose()
    {
        _cameras.Clear();
        base.Dispose();
    }

    public void SetCameras(ReadOnlySpan<Camera?> cameras)
    {
        var numCameras = cameras.Length;
        _cameras.Clear();
        for (var i = 0; i < numCameras; ++i)
            _cameras.Add(cameras[i]);

        _isCameraOrthographic = false;
        _isCameraFlippedByUser = false;
        _isReflectionCamera = false;
        _isCameraClipped = false;

        if (numCameras > 0)
        {
            var firstCamera = cameras[0]!;
            _isCameraOrthographic = firstCamera.IsOrthographic();
            _isCameraFlippedByUser = firstCamera.GetFlipVertical();
            _isReflectionCamera = firstCamera.GetUseReflection();
            _isCameraClipped = firstCamera.GetUseClipping();
            _cameraFillMode = firstCamera.GetFillMode();
            for (var i = 1; i < numCameras; ++i)
            {
                var camera = cameras[i]!;
                if (_isCameraFlippedByUser != camera.GetFlipVertical()
                    || _isCameraOrthographic != camera.IsOrthographic()
                    || _isReflectionCamera != camera.GetUseReflection()
                    || _isCameraClipped != camera.GetUseClipping()
                    || _cameraFillMode != camera.GetFillMode())
                {
                    Log.GetLogger().Log(Microsoft.Extensions.Logging.LogLevel.Error, default,
                        "All Cameras used in one SceneProcessor should use the same settings: "
                        + "Flip Vertical, Orthographic, Use Reflection, Use Clipping, Fill Mode",
                        null, static (state, _) => state);
                    System.Diagnostics.Debug.Assert(false);
                }
            }
        }
    }

    public uint PipelineStateHash
    {
        get
        {
            var hash = 0u;
            CombineHash(ref hash, _isCameraOrthographic);
            CombineHash(ref hash, _isCameraFlippedByUser);
            CombineHash(ref hash, _isReflectionCamera);
            CombineHash(ref hash, _isCameraClipped);
            CombineHash(ref hash, _flipCameraForRendering);
            CombineHash(ref hash, (uint)_cameraFillMode);
            return hash;
        }
    }

    public bool IsCameraReversed => _flipCameraForRendering ^ _isCameraFlippedByUser ^ _isReflectionCamera;

    public bool IsCameraOrthographic => _isCameraOrthographic;

    public bool IsCameraClipped => _isCameraClipped;

    public FillMode CameraFillMode => _cameraFillMode;

    /// <summary>Callbacks from SceneProcessor</summary>
    /// <remarks>Called at the beginning of the update.</remarks>
    public void OnUpdateBegin(FrameInfo frameInfo)
    {
        // On OpenGL, flip the projection if rendering to a texture so that the texture can be addressed in the same way
        // as a render texture produced on Direct3D
        var isOpenGL = _graphics!.GetRenderBackend() == RenderBackend.OpenGL;
        _flipCameraForRendering = frameInfo.RenderTarget is not null && isOpenGL;

        foreach (var camera in _cameras)
        {
            if (camera is not null)
                UpdateCamera(frameInfo, camera);
        }
    }

    public void OnRenderBegin(FrameInfo frameInfo)
    {
        foreach (var camera in _cameras)
        {
            if (camera is not null && _flipCameraForRendering)
                camera.SetFlipVertical(!camera.GetFlipVertical());
        }
    }

    public void OnRenderEnd(FrameInfo frameInfo)
    {
        foreach (var camera in _cameras)
        {
            if (_flipCameraForRendering && camera is not null)
                camera.SetFlipVertical(!camera.GetFlipVertical());
        }
    }

    private void UpdateCamera(FrameInfo frameInfo, Camera camera)
    {
        var cameraPosition = camera.Node!.GetWorldPosition();
        var cameraZone = frameInfo.Octree!.QueryZone(cameraPosition, camera.GetZoneMask()).Zone;
        camera.SetZone(cameraZone);

        if (camera.GetAutoAspectRatio())
            camera.SetAspectRatioInternal((float)frameInfo.ViewSize.X / frameInfo.ViewSize.Y);
    }

    private static void CombineHash(ref uint result, bool value) => CombineHash(ref result, value ? 1u : 0u);

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }
}
