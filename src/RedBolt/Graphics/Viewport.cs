namespace RedBolt;

public sealed class Viewport
{
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

    public Viewport(Scene scene, Camera leftEye, Camera rightEye)
        : this(scene, leftEye, IntRect.Zero)
    {
        SetEye(rightEye, 1);
    }

    public Camera Camera { get; private set; } = null!;

    public Camera? CullCamera { get; private set; }

    public bool DrawDebug { get; private set; } = true;

    public IntRect Rect { get; private set; }

    public Camera? RightEye { get; private set; }

    public Scene Scene { get; private set; } = null!;

    public Camera GetCamera() => Camera;

    public Camera GetCullCamera() => CullCamera ?? Camera;

    public bool GetDrawDebug() => DrawDebug;

    public Camera? GetEye(int eyeIndex) => eyeIndex switch
    {
        0 => Camera,
        1 => RightEye,
        _ => null
    };

    public IntRect GetRect() => Rect;

    public Scene GetScene() => Scene;

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
