namespace RedBolt.Graphics;

using RedBolt.Core;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;
using RedBolt.Scene;

/// <summary>Viewport definition either for a render surface or the backbuffer.</summary>
[Reflected]
public partial class Viewport : RedObject
{
    /// <summary>Scene pointer.</summary>
    private readonly WeakReference<Scene> _scene = new(null!);
    /// <summary>Camera pointer.</summary>
    private readonly WeakReference<Camera> _camera = new(null!);
    /// <summary>Right eye camera pointer.</summary>
    private readonly WeakReference<Camera> _rightEye = new(null!);
    /// <summary>Culling camera pointer.</summary>
    private readonly WeakReference<Camera> _cullCamera = new(null!);
    /// <summary>Viewport rectangle.</summary>
    private IntRect _rect = IntRect.Zero;
    /// <summary>Debug draw flag.</summary>
    private bool _drawDebug = true;

    /// <summary>Whether to search for render pipeline automatically.</summary>
    private bool _autoRenderPipeline = true;
    /// <summary>Render pipeline component from scene.</summary>
    private readonly WeakReference<RenderPipelineComponent> _renderPipeline = new(null!);
    /// <summary>Instance of render pipeline connected to render pipeline.</summary>
    private RenderPipelineView? _renderPipelineView;

    /// <summary>Construct with defaults.</summary>
    public Viewport()
    {
    }

    /// <summary>Construct with a full rectangle.</summary>
    public Viewport(Scene? scene, Camera? camera)
    {
        _scene.SetTarget(scene!);
        _camera.SetTarget(camera!);
    }

    /// <summary>Construct with a specified rectangle.</summary>
    public Viewport(Scene? scene, Camera? camera, IntRect rect)
    {
        _scene.SetTarget(scene!);
        _camera.SetTarget(camera!);
        _rect = rect;
    }

    /// <summary>Construct with a specified rectangle and render pipeline.</summary>
    public Viewport(Scene? scene, Camera? camera, IntRect rect, RenderPipelineComponent? renderPipeline)
    {
        _scene.SetTarget(scene!);
        _camera.SetTarget(camera!);
        _rect = rect;
        _autoRenderPipeline = false;
        _renderPipeline.SetTarget(renderPipeline!);
    }

    /// <summary>Construct for stereo with a render pipeline.</summary>
    public Viewport(Scene? scene, Camera? leftEye, Camera? rightEye, RenderPipelineComponent? renderPipeline)
    {
        _scene.SetTarget(scene!);
        _camera.SetTarget(leftEye!);
        _rightEye.SetTarget(rightEye!);
        _autoRenderPipeline = false;
        _renderPipeline.SetTarget(renderPipeline!);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _renderPipelineView?.Dispose();
        _renderPipelineView = null;
        _renderPipeline.SetTarget(null!);
        _cullCamera.SetTarget(null!);
        _rightEye.SetTarget(null!);
        _camera.SetTarget(null!);
        _scene.SetTarget(null!);
        base.Dispose();
    }


    /// <summary>Set scene.</summary>
    public void SetScene(Scene? scene)
    {
        var hasScene = _scene.TryGetTarget(out var currentScene) && currentScene is not null;
        if (hasScene != (scene is not null))
        {
            _renderPipelineView?.Dispose();
            _renderPipelineView = null;
        }

        _scene.SetTarget(scene!);
    }

    /// <summary>Set viewport camera.</summary>
    public void SetCamera(Camera? camera)
    {
        _camera.SetTarget(camera!);
    }

    /// <summary>Set view rectangle. A zero rectangle (0 0 0 0) means to use the render target's full dimensions.</summary>
    public void SetRect(IntRect rect)
    {
        _rect = rect;
    }

    /// <summary>Set whether to render debug geometry. Default true.</summary>
    public void SetDrawDebug(bool enable)
    {
        _drawDebug = enable;
    }

    /// <summary>Set separate camera to use for culling. Sharing a culling camera between several viewports allows to prepare the view only once, saving in CPU use. The culling camera's frustum should cover all the viewport cameras' frusta or else objects may be missing from the rendered view.</summary>
    public void SetCullCamera(Camera? camera)
    {
        _cullCamera.SetTarget(camera!);
    }

    /// <summary>Return scene.</summary>
    public Scene? GetScene() => _scene.TryGetTarget(out var scene) ? scene : null;

    /// <summary>Return viewport camera.</summary>
    public Camera? GetCamera() => _camera.TryGetTarget(out var camera) ? camera : null;

    /// <summary>Return render pipeline.</summary>
    public RenderPipelineView? GetRenderPipelineView()
    {
        _renderPipeline.TryGetTarget(out var renderPipeline);
        var scene = GetScene();

        // Render pipeline is null or expired
        if (renderPipeline is null)
            return null;

        // Automatic pipeline is not from the scene
        if (_autoRenderPipeline && !ReferenceEquals(renderPipeline.Scene, scene))
            return null;

        // View is expired or outdated
        if (_renderPipelineView is null || !ReferenceEquals(_renderPipelineView.GetRenderPipeline(), renderPipeline))
            return null;

        return _renderPipelineView;
    }

    /// <summary>Return view rectangle. A zero rectangle (0 0 0 0) means to use the render target's full dimensions. In this case you could fetch the actual view rectangle from View object, though it will be valid only after the first frame.</summary>
    public IntRect GetRect() => _rect;

    /// <summary>Return effective view rectangle. By default, this function compensates for render target flip on OpenGL. It may be disabled.</summary>
    public IntRect GetEffectiveRect(RenderSurface? renderTarget, bool compensateRenderTargetFlip = true)
    {
        var renderDevice = GetSubsystem<RenderDevice>();
        var isOpenGl = renderDevice is not null && renderDevice.GetBackend() == RenderBackend.OpenGL;

        var renderTargetSize = RenderSurface.GetSize(GetSubsystem<GraphicsSystem>()!, renderTarget);

        if (_rect == IntRect.Zero)
        {
            // Return render target dimensions if viewport rectangle is not defined
            return new IntRect(Vector2i.Zero, renderTargetSize);
        }
        else
        {
            // Validate and return viewport rectangle
            var rect = new IntRect();
            rect.Left = MathDefs.Clamp(_rect.Left, 0, renderTargetSize.X - 1);
            rect.Top = MathDefs.Clamp(_rect.Top, 0, renderTargetSize.Y - 1);
            rect.Right = MathDefs.Clamp(_rect.Right, rect.Left + 1, renderTargetSize.X);
            rect.Bottom = MathDefs.Clamp(_rect.Bottom, rect.Top + 1, renderTargetSize.Y);

            if (isOpenGl && renderTarget is not null && compensateRenderTargetFlip)
            {
                // On OpenGL the render to texture is flipped vertically.
                // Flip the viewport rectangle to compensate.
                rect.Top = renderTargetSize.Y - rect.Top;
                rect.Bottom = renderTargetSize.Y - rect.Bottom;
                (rect.Top, rect.Bottom) = (rect.Bottom, rect.Top);
            }

            return rect;
        }
    }

    /// <summary>Return whether to draw debug geometry.</summary>
    public bool GetDrawDebug() => _drawDebug;

    /// <summary>Return the culling camera. If null, the viewport camera will be used for culling (normal case).</summary>
    public Camera? GetCullCamera() => _cullCamera.TryGetTarget(out var camera) ? camera : null;

    /// <summary>Return ray corresponding to normalized screen coordinates.</summary>
    public Ray GetScreenRay(int x, int y)
    {
        var camera = GetCamera();
        if (camera is null)
            return new Ray();

        float screenX;
        float screenY;

        if (_rect == IntRect.Zero)
        {
            var graphics = GetSubsystem<GraphicsSystem>()!;
            screenX = x / (float)graphics.Width;
            screenY = y / (float)graphics.Height;
        }
        else
        {
            screenX = (x - _rect.Left) / (float)_rect.Width;
            screenY = (y - _rect.Top) / (float)_rect.Height;
        }

        return camera.GetScreenRay(screenX, screenY);
    }

    /// <summary>Convert a world space point to normalized screen coordinates.</summary>
    public Vector2i WorldToScreenPoint(Vector3f worldPos)
    {
        var camera = GetCamera();
        if (camera is null)
            return Vector2i.Zero;

        var screenPoint = camera.WorldToScreenPoint(worldPos);

        int x;
        int y;
        if (_rect == IntRect.Zero)
        {
            /// <todo>This is incorrect if the viewport is used on a texture render target instead of the backbuffer, as it may have different dimensions.</todo>
            var graphics = GetSubsystem<GraphicsSystem>()!;
            x = (int)(screenPoint.X * graphics.Width);
            y = (int)(screenPoint.Y * graphics.Height);
        }
        else
        {
            x = (int)(_rect.Left + screenPoint.X * _rect.Width);
            y = (int)(_rect.Top + screenPoint.Y * _rect.Height);
        }

        return new Vector2i(x, y);
    }

    /// <summary>Convert screen coordinates and depth to a world space point.</summary>
    public Vector3f ScreenToWorldPoint(int x, int y, float depth)
    {
        var camera = GetCamera();
        if (camera is null)
            return Vector3f.Zero;

        float screenX;
        float screenY;

        if (_rect == IntRect.Zero)
        {
            /// <todo>This is incorrect if the viewport is used on a texture render target instead of the backbuffer, as it may have different dimensions.</todo>
            var graphics = GetSubsystem<GraphicsSystem>()!;
            screenX = x / (float)graphics.Width;
            screenY = y / (float)graphics.Height;
        }
        else
        {
            screenX = (x - _rect.Left) / (float)_rect.Width;
            screenY = (y - _rect.Top) / (float)_rect.Height;
        }

        return camera.ScreenToWorldPoint(new Vector3f(screenX, screenY, depth));
    }

    /// <summary>Allocate the view structure. Called by Renderer.</summary>
    public void AllocateView()
    {
        var scene = GetScene();
        _renderPipeline.TryGetTarget(out var renderPipeline);

        // If automatic render pipeline, expire it on scene mismatch
        if (_autoRenderPipeline && renderPipeline is not null && !ReferenceEquals(renderPipeline.Scene, scene))
        {
            _renderPipeline.SetTarget(null!);
            renderPipeline = null;
        }

        if (renderPipeline is null && scene is not null)
        {
            renderPipeline = scene.GetDerivedComponent<RenderPipelineComponent>();
            if (renderPipeline is null)
                renderPipeline = scene.CreateComponent<RenderPipelineComponent>();
            _renderPipeline.SetTarget(renderPipeline);
        }

        // Expire view on pipeline mismatch
        if (renderPipeline is not null && (_renderPipelineView is null ||
            !ReferenceEquals(_renderPipelineView.GetRenderPipeline(), renderPipeline)))
        {
            var renderPipelineView = renderPipeline.Instantiate();
            _renderPipelineView?.Dispose();
            _renderPipelineView = renderPipelineView;
        }
    }

    /// <summary>Get the given eye by index, starting from the left.</summary>
    public Camera? GetEye(int index)
    {
        System.Diagnostics.Debug.Assert(IsStereo());
        return index == 0
            ? GetCamera()
            : (_rightEye.TryGetTarget(out var rightEye) ? rightEye : null);
    }

    /// <summary>Set a camera based on eye-index, starting from the left.</summary>
    public void SetEye(Camera? camera, int eyeIndex)
    {
        System.Diagnostics.Debug.Assert(eyeIndex >= 0 && eyeIndex <= 1);
        if (eyeIndex == 0)
            _camera.SetTarget(camera!);
        else
            _rightEye.SetTarget(camera!);
    }

    /// <summary>Returns true if this viewport has eyes for stereo.</summary>
    public bool IsStereo() => _rightEye.TryGetTarget(out var rightEye) && rightEye is not null;
}
