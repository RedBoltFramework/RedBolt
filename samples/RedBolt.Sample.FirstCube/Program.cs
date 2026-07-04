using RedBolt;

try
{
    var exitCode = new FirstCubeApplication().Run();
    if (exitCode == 0)
        Console.WriteLine("RedBolt first cube presented through generated SDL/Diligent bindings.");
    return exitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

sealed class FirstCubeApplication : Application
{
    private double elapsedTime;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.FirstCube";
        parameters.GraphicsBackend = GraphicsBackend.OpenGL;
        parameters.WindowHeight = 540;
        parameters.WindowTitle = "RedBolt First Cube";
        parameters.WindowWidth = 960;
    }

    protected override void Start()
    {
        var scene = new Scene();
        scene.CreateComponent<Octree>();

        var cameraNode = scene.CreateChild("Camera");
        cameraNode.Position = new Vector3(0.0f, 2.0f, -6.0f);
        var camera = cameraNode.CreateComponent<Camera>();

        var cubeNode = scene.CreateChild("Cube");
        cubeNode.Rotation = Quaternion.Identity;
        var cube = cubeNode.CreateComponent<StaticModel>();
        cube.Model = Resources.Get<Model>("Models/ProceduralBox");
        cube.Material = Resources.Get<Material>("Materials/FirstCube");

        Renderer.SetViewport(0, new Viewport(scene, camera));
    }

    protected override void Update(float timeStep)
    {
        elapsedTime += timeStep;
        if (elapsedTime >= 4.0 || Input.ExitRequested)
        {
            Exit();
            return;
        }

        Thread.Sleep(16);
    }
}
