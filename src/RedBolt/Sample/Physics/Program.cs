//
// Copyright (c) 2008-2022 the Urho3D project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.Sample.Physics;

using System;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.DearImGui;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.Scene;

internal static class PhysicsProgram
{
    private static int Main()
    {
        try
        {
            using var application = new PhysicsSample();
            return application.Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

/// <summary>
/// Physics example.
/// This sample demonstrates:
///     - Creating both static and moving physics objects to a scene
///     - Displaying the scene using the Renderer subsystem
/// </summary>
public class PhysicsSample : Application
{
    protected override void Start()
    {
        base.Start();
        RedBolt.PhysicsJolt.Physics.RegisterPhysicsLibrary(RedBolt.Core.Context.Instance);
        CreateScene();
        CreateInstructions();
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
        Input.SetMouseMode(MouseMode.Relative);
        Input.SetMouseVisible(false);
    }

    private void CreateScene()
    {
        var cache = Resources;
        _scene = new Scene();
        _scene.CreateComponent<Octree>();
        _scene.CreateComponent<PhysicsWorld>();

        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.6f, -1.0f, 0.8f));
        lightNode.CreateComponent<Light>().SetLightType(LightType.Directional);

        var floorNode = _scene.CreateChild("Floor");
        floorNode.SetPosition(new Vector3f(0.0f, -0.5f, 0.0f));
        floorNode.SetScale(new Vector3f(50.0f, 1.0f, 50.0f));
        var floorObject = floorNode.CreateComponent<StaticModel>();
        floorObject.SetModel(cache.Get<Model>("Models/Box.mdl"));
        floorObject.SetMaterial(cache.Get<Material>("Materials/StoneTiled.xml"));
        floorNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        floorNode.CreateComponent<RigidBody>();

        for (var y = 0; y < 8; ++y)
        {
            for (var x = -y; x <= y; ++x)
            {
                var boxNode = _scene.CreateChild("Box");
                boxNode.SetPosition(new Vector3f(x, -y + 8.0f, 0.0f));
                var boxObject = boxNode.CreateComponent<StaticModel>();
                boxObject.SetModel(cache.Get<Model>("Models/Box.mdl"));
                boxObject.SetMaterial(cache.Get<Material>("Materials/StoneSmall.xml"));
                boxObject.SetCastShadows(true);
                boxNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
                var body = boxNode.CreateComponent<RigidBody>();
                body.Mass = 1.0f;
                body.Friction = 0.75f;
            }
        }

        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<FreeFlyController>().SetSpeed(20.0f);
        _cameraNode.CreateComponent<Camera>();
        _cameraNode.SetPosition(new Vector3f(0.0f, 5.0f, -20.0f));
        _cameraNode.LookAt(new Vector3f(0.0f, 4.0f, 0.0f));
    }

    private void CreateInstructions()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
    }

    private void SpawnObject()
    {
        var boxNode = _scene!.CreateChild("SmallBox");
        boxNode.SetPosition(_cameraNode!.GetPosition());
        boxNode.SetRotation(_cameraNode.GetRotation());
        var boxObject = boxNode.CreateComponent<StaticModel>();
        boxObject.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        boxObject.SetMaterial(Resources.Get<Material>("Materials/StoneSmall.xml"));
        boxObject.SetCastShadows(true);
        boxNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        var body = boxNode.CreateComponent<RigidBody>();
        body.Mass = 1.0f;
        body.Friction = 0.75f;
        body.LinearVelocity = _cameraNode.GetRotation() * new Vector3f(0.0f, 0.25f, 1.0f) * 10.0f;
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

        if (Input.GetMouseButtonPress(MouseButton.Left))
            SpawnObject();
    }

    protected override void Stop()
    {
        Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Stop();
    }

    private static void DrawInstructions()
    {
        var windowOpen = true;
        if (ImGui.Begin("Physics"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
            ImGui.TextUnformatted("WASD + mouse: move camera\nLeft mouse button: throw a box\nEscape: exit"u8);
        ImGui.End();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Physics";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowHeight = 720;
            parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Physics";
    }

    public override void Dispose()
    {
        if (_scene is not null)
            Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Dispose();
    }

    private Scene? _scene;
    private Node? _cameraNode;
}
