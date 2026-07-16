using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Utility to process render camera (not cull camera!).</summary>
public sealed class CameraProcessor : Object
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
                    Log.Logger.Log(LogLevel.Error, default,
                        "All Cameras used in one SceneProcessor should use the same settings: "
                        + "Flip Vertical, Orthographic, Use Reflection, Use Clipping, Fill Mode",
                        null, static (state, _) => state);
                    System.Diagnostics.Debug.Assert(false);
                }
            }
        }
    }

    public uint GetPipelineStateHash()
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

    public bool IsCameraReversed() => _flipCameraForRendering ^ _isCameraFlippedByUser ^ _isReflectionCamera;

    public bool IsCameraOrthographic() => _isCameraOrthographic;

    public bool IsCameraClipped() => _isCameraClipped;

    public FillMode GetCameraFillMode() => _cameraFillMode;

    /// <summary>Callbacks from SceneProcessor</summary>
    /// <remarks>Called at the beginning of the update.</remarks>
    public void OnUpdateBegin(CommonFrameInfo frameInfo)
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

    public void OnRenderBegin(CommonFrameInfo frameInfo)
    {
        foreach (var camera in _cameras)
        {
            if (camera is not null && _flipCameraForRendering)
                camera.SetFlipVertical(!camera.GetFlipVertical());
        }
    }

    public void OnRenderEnd(CommonFrameInfo frameInfo)
    {
        foreach (var camera in _cameras)
        {
            if (_flipCameraForRendering && camera is not null)
                camera.SetFlipVertical(!camera.GetFlipVertical());
        }
    }

    private static void UpdateCamera(CommonFrameInfo frameInfo, Camera camera)
    {
        var cameraPosition = camera.Node!.GetWorldPosition();
        var cameraZone = frameInfo.Octree!.QueryZone(cameraPosition, camera.GetZoneMask()).Zone;
        camera.SetZone(cameraZone);

        if (camera.GetAutoAspectRatio())
            camera.SetAspectRatioInternal((float)frameInfo.ViewportSize.X / frameInfo.ViewportSize.Y);
    }

    private static void CombineHash(ref uint result, bool value) => CombineHash(ref result, value ? 1u : 0u);

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }
}
