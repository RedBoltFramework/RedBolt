//
// Copyright (c) 2017-2020 the rbfx project.
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

namespace RedBolt.Sample.BakedLighting;

using System;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.DearImGui;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Navigation;
using RedBolt.Platform;
using RedBolt.Resource;
using RedBolt.Scene;

internal static class BakedLightingProgram
{
    private static int Main()
    {
        try
        {
            using var application = new BakedLighting();
            var exitCode = application.Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt BakedLighting sample finished.");
            return exitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

/// <summary>Baked lighting example.</summary>
public class BakedLighting : Application
{
    /// <summary>Construct.</summary>
    public BakedLighting()
    {
    }

    /// <summary>Setup after engine initialization and before running the main loop.</summary>
    protected override void Start()
    {
        // Execute base class startup
        base.Start();

        // Create scene content
        CreateScene();

        // Create the UI content
        CreateInstructions();

        // Subscribe to necessary events
        SubscribeToEvents();

        // Set the mouse mode to use in the sample
        Input.SetMouseMode(MouseMode.Relative);
        Input.SetMouseVisible(false);
    }

    /// <summary>Create scene content.</summary>
    private void CreateScene()
    {
        var cache = Resources;

        // Load scene
        _scene = new Scene();

        var xmlFile = cache.Get<XMLFile>("Scenes/BakedLightingExample.xml");
        using var root = xmlFile.GetRoot();
        _scene.LoadXML(root);

        var camera = _scene.FindComponent<Camera>()!;
        _cameraNode = camera.Node!;
        _cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -8.0f));
        _viewport = new Viewport(_scene, camera);
        Renderer.SetViewport(0, _viewport);

        var navMesh = _scene.FindComponent<NavigationMesh>()!;
        navMesh.Rebuild();

        _agent = _scene.FindComponent<CrowdAgent>()!;

        var animController = _agent.Node.FindComponent<AnimationController>()!;
        animController.PlayNewExclusive(new AnimationParameters(cache.Get<Animation>("Models/Mutant/Mutant_Idle0.ani")).Looped());

        var crowdManager = _scene.GetComponent<CrowdManager>()!;
        var parameters = crowdManager.GetObstacleAvoidanceParams(0);
        parameters.WeightToi = 0.0001f;
        crowdManager.SetObstacleAvoidanceParams(0, parameters);

        // Initialize yaw and pitch angles
        _cameraRotationPitch = _cameraNode.Parent!;
        _cameraRotationYaw = _cameraRotationPitch.Parent!;
        _yaw = _cameraRotationYaw.WorldRotation.YawAngle;
        _pitch = _cameraRotationPitch.WorldRotation.PitchAngle;
    }

    /// <summary>Construct an instruction text to the UI.</summary>
    private void CreateInstructions()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());

        // Construct new Text object, set string to display and font to use
        // The text has multiple rows. Center them in relation to each other
        // Position the text relative to the screen center
        // The rbfx Text control is replaced by the allowed Dear ImGui sample UI surface.
    }

    /// <summary>Subscribe to necessary events.</summary>
    private void SubscribeToEvents()
    {
        // Unsubscribe the SceneUpdate event from base class as the camera node is being controlled in HandlePostUpdate() in this sample
    }

    /// <summary>Handle application update. Set controls to character.</summary>
    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

        var cache = Resources;

        // Update camera rotation
        const float rotationSensitivity = 0.1f;
        var mouseMove = Input.GetMouseMove();
        _yaw = MathDefs.Mod(_yaw + mouseMove.X * rotationSensitivity, 360.0f);
        _pitch = MathDefs.Clamp(_pitch + mouseMove.Y * rotationSensitivity, -89.0f, 89.0f);

        _cameraRotationPitch!.SetRotation(new Quaternion(_pitch, 0.0f, 0.0f));
        _cameraRotationYaw!.SetRotation(new Quaternion(0.0f, _yaw, 0.0f));

        // Apply movement
        var rotation = _cameraRotationYaw!.WorldRotation;
        var controlDirection = Vector3f.Zero;
        if (Input.GetKeyDown(Key.W))
            controlDirection += Vector3f.Forward;
        if (Input.GetKeyDown(Key.S))
            controlDirection += Vector3f.Back;
        if (Input.GetKeyDown(Key.A))
            controlDirection += Vector3f.Left;
        if (Input.GetKeyDown(Key.D))
            controlDirection += Vector3f.Right;

        var movementDirection = rotation * controlDirection;
        var movementSpeed = Input.GetKeyDown(Key.Shift) ? 5.0f : 2.0f;
        _agent!.TargetVelocity = movementSpeed * movementDirection;

        // Animate model
        var runAnimation = cache.Get<Animation>("Models/Mutant/Mutant_Run.ani");
        var idleAnimation = cache.Get<Animation>("Models/Mutant/Mutant_Idle0.ani");

        var animController = _agent.Node.FindComponent<AnimationController>()!;
        var rotationNode = animController.Node.Parent!;
        var actualVelocityFlat = _agent.ActualVelocity * new Vector3f(1.0f, 0.0f, 1.0f);
        if (actualVelocityFlat.Length > MathDefs.LargeEpsilon)
        {
            var animationSpeed = actualVelocityFlat.Length * 0.3f;
            rotationNode.SetWorldDirection(actualVelocityFlat);
            animController.PlayExistingExclusive(new AnimationParameters(runAnimation).Looped().Speed(animationSpeed), 0.2f);
        }
        else
            animController.PlayExistingExclusive(new AnimationParameters(idleAnimation).Looped(), 0.2f);

        // Toggle textures
        if (Input.GetKeyPress(Key.Tab))
        {
            var animModel = animController.Node.GetComponent<AnimatedModel>()!;
            _texturesEnabled = !_texturesEnabled;
            animModel.SetMaterial(_texturesEnabled
                ? cache.Get<Material>("Models/Mutant/Materials/mutant_M.xml")
                : cache.Get<Material>("Materials/DefaultWhite.xml"));
        }
    }

    protected override void Stop()
    {
        Renderer.SetNumViewports(0);
        _viewport?.Dispose();
        _viewport = null;
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Stop();
    }

    private static void DrawInstructions()
    {
        var windowOpen = true;
        if (ImGui.Begin("Baked Lighting\0"u8, ref windowOpen,
            ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("Use WASD keys and the right mouse button to move\0"u8);
            ImGui.TextUnformatted("Shift to sprint\0"u8);
        }
        ImGui.End();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.BakedLighting";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
        if (ProcessUtils.GetPlatform() != PlatformId.Web)
        {
            parameters.WindowHeight = 720;
            parameters.WindowWidth = 1280;
        }
        parameters.WindowTitle = "RedBolt Baked Lighting";
    }

    public override void Dispose()
    {
        if (_scene is not null)
            Renderer.SetNumViewports(0);
        _viewport?.Dispose();
        _viewport = null;
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Dispose();
    }

    private Scene? _scene;
    private Node? _cameraNode;
    private Node? _cameraRotationPitch;
    private Node? _cameraRotationYaw;
    private Viewport? _viewport;
    private CrowdAgent? _agent;
    private float _yaw;
    private float _pitch;
    private bool _texturesEnabled = true;
}
