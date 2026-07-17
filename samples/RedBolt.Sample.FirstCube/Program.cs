using System;
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
    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.FirstCube";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowHeight = 540;
        parameters.WindowTitle = "RedBolt First Cube";
        parameters.WindowWidth = 960;
    }

    protected override void Start()
    {
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);

        var scene = new Scene();
        scene.CreateComponent<Octree>();

        var cameraNode = scene.CreateChild("Camera");
        cameraNode.Position = new Vector3f(0.0f, 2.0f, -6.0f);
        var camera = cameraNode.CreateComponent<Camera>();

        var lightNode = scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.4f, -1.0f, 0.6f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);

        var cubeNode = scene.CreateChild("Cube");
        cubeNode.Rotation = Quaternion.Identity;
        var cube = cubeNode.CreateComponent<StaticModel>();
        cube.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        cube.SetMaterial(Resources.Get<Material>("Materials/StoneTiled.xml"));

        Renderer.SetViewport(0, new Viewport(scene, camera));
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }
    }
}
