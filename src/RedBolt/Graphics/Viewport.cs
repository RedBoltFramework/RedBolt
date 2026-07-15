namespace RedBolt;

/// <summary>Viewport definition either for a render surface or the backbuffer.</summary>
/// <remarks>
// C++: Viewport inherits from Object. C# matches.
// C++: Has RegisterObject static method for factory registration. C# doesn't have it (not needed with C# reflection).
// C++: Has autoRenderPipeline_ flag. C# matches.
// C++: GetRenderPipelineView returns RenderPipelineView*. C# matches.
/// </remarks>
public sealed class Viewport : Object
{
    private bool _autoRenderPipeline = true;
    private RenderPipeline? _renderPipeline;
    private RenderPipelineView? _renderPipelineView;

    public Viewport()
    {
    }

    public Viewport(Scene scene, Camera camera)
        : this(scene, camera, IntRect.Zero)
    {
    }

    public Viewport(Scene scene, Camera camera, IntRect rect)
    {
        SetScene(scene);
        SetCamera(camera);
        SetRect(rect);
    }

    public Viewport(Scene scene, Camera camera, IntRect rect, RenderPipeline renderPipeline)
        : this(scene, camera, rect)
    {
        _autoRenderPipeline = false;
        _renderPipeline = renderPipeline ?? throw new ArgumentNullException(nameof(renderPipeline));
    }

    public Viewport(Scene scene, Camera leftEye, Camera rightEye)
        : this(scene, leftEye, IntRect.Zero)
    {
        SetEye(rightEye, 1);
    }

    public Viewport(Scene scene, Camera leftEye, Camera rightEye, RenderPipeline renderPipeline)
        : this(scene, leftEye, rightEye)
    {
        _autoRenderPipeline = false;
        _renderPipeline = renderPipeline ?? throw new ArgumentNullException(nameof(renderPipeline));
    }

    public Camera Camera { get; private set; } = null!;

    public Camera? CullCamera { get; private set; }

    public bool DrawDebug { get; private set; } = true;

    public IntRect Rect { get; private set; }

    public Camera? RightEye { get; private set; }

    public Scene Scene { get; private set; } = null!;

    public Camera GetCamera() => Camera;

    public Camera? GetCullCamera() => CullCamera;

    public bool GetDrawDebug() => DrawDebug;

    public Camera? GetEye(int eyeIndex) => eyeIndex switch
    {
        0 => Camera,
        1 => RightEye,
        _ => null
    };

    public IntRect GetRect() => Rect;

    public IntRect GetEffectiveRect(RenderSurface? renderTarget, bool compensateRenderTargetFlip = true)
    {
        var renderDevice = GetSubsystem<RenderDevice>();
        var graphics = GetSubsystem<Graphics>()!;
        var isOpenGl = renderDevice?.GetBackend() == RenderBackend.OpenGL;
        var renderTargetSize = RenderSurface.GetSize(graphics, renderTarget);

        if (Rect == IntRect.Zero)
            return new IntRect(Vector2i.Zero, renderTargetSize);

        var rect = new IntRect(
            Math.Clamp(Rect.Left, 0, renderTargetSize.X - 1),
            Math.Clamp(Rect.Top, 0, renderTargetSize.Y - 1),
            0,
            0);
        rect.Right = Math.Clamp(Rect.Right, rect.Left + 1, renderTargetSize.X);
        rect.Bottom = Math.Clamp(Rect.Bottom, rect.Top + 1, renderTargetSize.Y);

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

    public Scene GetScene() => Scene;

    public RenderPipelineView? GetRenderPipelineView()
    {
        if (_renderPipeline is null)
            return null;
        if (_autoRenderPipeline && !ReferenceEquals(_renderPipeline.Scene, Scene))
            return null;
        if (_renderPipelineView is null || !ReferenceEquals(_renderPipelineView.GetRenderPipeline(), _renderPipeline))
            return null;
        return _renderPipelineView;
    }

    public void AllocateView()
    {
        if (_autoRenderPipeline && _renderPipeline is not null && !ReferenceEquals(_renderPipeline.Scene, Scene))
            _renderPipeline = null;

        if (_renderPipeline is null && Scene is not null)
        {
            _renderPipeline = Scene.Root.GetComponent<RenderPipeline>() ?? Scene.Root.CreateComponent<RenderPipeline>();
        }

        if (_renderPipeline is not null && (_renderPipelineView is null || !ReferenceEquals(_renderPipelineView.GetRenderPipeline(), _renderPipeline)))
            _renderPipelineView = _renderPipeline.Instantiate();
    }

    public Ray GetScreenRay(int x, int y)
    {
        if (Camera is null)
            return new Ray();

        var graphics = GetSubsystem<Graphics>()!;
        var screenX = Rect == IntRect.Zero
            ? x / (float)graphics.Width
            : (x - Rect.Left) / (float)Rect.Width;
        var screenY = Rect == IntRect.Zero
            ? y / (float)graphics.Height
            : (y - Rect.Top) / (float)Rect.Height;
        return Camera.GetScreenRay(screenX, screenY);
    }

    public Vector2i WorldToScreenPoint(Vector3f worldPos)
    {
        if (Camera is null)
            return Vector2i.Zero;

        var screenPoint = Camera.WorldToScreenPoint(worldPos);
        if (Rect == IntRect.Zero)
        {
            var graphics = GetSubsystem<Graphics>()!;
            return new Vector2i((int)(screenPoint.X * graphics.Width), (int)(screenPoint.Y * graphics.Height));
        }

        return new Vector2i(
            (int)(Rect.Left + screenPoint.X * Rect.Width),
            (int)(Rect.Top + screenPoint.Y * Rect.Height));
    }

    public Vector3f ScreenToWorldPoint(int x, int y, float depth)
    {
        if (Camera is null)
            return Vector3f.Zero;

        var graphics = GetSubsystem<Graphics>()!;
        var screenX = Rect == IntRect.Zero
            ? x / (float)graphics.Width
            : (x - Rect.Left) / (float)Rect.Width;
        var screenY = Rect == IntRect.Zero
            ? y / (float)graphics.Height
            : (y - Rect.Top) / (float)Rect.Height;
        return Camera.ScreenToWorldPoint(new Vector3f(screenX, screenY, depth));
    }

    public bool IsStereo() => RightEye is not null;

    public void SetCamera(Camera camera)
    {
        Camera = camera ?? throw new ArgumentNullException(nameof(camera));
    }

    public void SetCullCamera(Camera? camera)
    {
        CullCamera = camera;
    }

    public void SetDrawDebug(bool enable)
    {
        DrawDebug = enable;
    }

    public void SetEye(Camera? camera, int eyeIndex)
    {
        switch (eyeIndex)
        {
            case 0:
                if (camera is null)
                    throw new ArgumentNullException(nameof(camera));
                SetCamera(camera);
                break;
            case 1:
                RightEye = camera;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(eyeIndex));
        }
    }

    public void SetRect(IntRect rect)
    {
        Rect = rect;
    }

    public void SetScene(Scene scene)
    {
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));
    }
}
