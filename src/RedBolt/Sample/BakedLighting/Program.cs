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

using System;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
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
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
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
        _cameraNode.SetParent(_scene);
        _cameraNode.SetPosition(new Vector3f(0.0f, 1.5f, -8.0f));
        _cameraNode.LookAt(new Vector3f(0.0f, 0.5f, -2.0f));
        var freeFlyController = _cameraNode.CreateComponent<FreeFlyController>();
        freeFlyController.SetSpeed(2.0f);
        freeFlyController.SetAcceleratedSpeed(5.0f);
        _viewport = new Viewport(_scene, camera);
        Renderer.SetViewport(0, _viewport);

        /* TODO: port when the Navigation/Crowd subsystem is available.
        auto navMesh = scene_->FindComponent<NavigationMesh>();
        navMesh->Rebuild();

        agent_ = scene_->FindComponent<CrowdAgent>();

        auto animController = agent_->GetNode()->FindComponent<AnimationController>();
        animController->PlayNewExclusive(AnimationParameters{context_, "Models/Mutant/Mutant_Idle0.ani"}.Looped());

        auto crowdManager = scene_->GetComponent<CrowdManager>();
        CrowdObstacleAvoidanceParams params = crowdManager->GetObstacleAvoidanceParams(0);
        params.weightToi = 0.0001f;
        crowdManager->SetObstacleAvoidanceParams(0, params);

        // Initialize yaw and pitch angles
        Node* cameraRotationPitch = cameraNode_->GetParent();
        Node* cameraRotationYaw = cameraRotationPitch->GetParent();
        yaw_ = cameraRotationYaw->GetWorldRotation().YawAngle();
        pitch_ = cameraRotationPitch->GetWorldRotation().PitchAngle();
        */
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
        // TODO: port when the shared managed Sample base subscribes to SceneUpdate.
    }

    /// <summary>Handle application update. Set controls to character.</summary>
    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

        /* TODO: port when the Navigation/Crowd subsystem is available.
        auto* input = GetSubsystem<Input>();
        auto* cache = GetSubsystem<ResourceCache>();

        // Update camera rotation
        const float rotationSensitivity = 0.1f;
        const IntVector2 mouseMove = input->GetMouseMove();
        yaw_ = Mod(yaw_ + mouseMove.x_ * rotationSensitivity, 360.0f);
        pitch_ = Clamp(pitch_ + mouseMove.y_ * rotationSensitivity, -89.0f, 89.0f);

        Node* cameraRotationPitch = cameraNode_->GetParent();
        Node* cameraRotationYaw = cameraRotationPitch->GetParent();
        cameraRotationPitch->SetRotation(Quaternion(pitch_, 0.0f, 0.0f));
        cameraRotationYaw->SetRotation(Quaternion(0.0f, yaw_, 0.0f));

        // Apply movement
        const Quaternion& rotation = cameraRotationYaw->GetWorldRotation();
        Vector3 controlDirection = Vector3::ZERO;
        if (input->GetKeyDown(KEY_W))
            controlDirection += Vector3::FORWARD;
        if (input->GetKeyDown(KEY_S))
            controlDirection += Vector3::BACK;
        if (input->GetKeyDown(KEY_A))
            controlDirection += Vector3::LEFT;
        if (input->GetKeyDown(KEY_D))
            controlDirection += Vector3::RIGHT;

        const Vector3 movementDirection = rotation * controlDirection;
        const float speed = input->GetKeyDown(KEY_SHIFT) ? 5.0f : 2.0f;
        agent_->SetTargetVelocity(speed * movementDirection);

        // Animate model
        auto* runAnimation = cache->GetResource<Animation>("Models/Mutant/Mutant_Run.ani");
        auto* idleAnimation = cache->GetResource<Animation>("Models/Mutant/Mutant_Idle0.ani");

        auto animController = agent_->GetNode()->FindComponent<AnimationController>();
        auto rotationNode = animController->GetNode()->GetParent();
        const Vector3 actualVelocityFlat = (agent_->GetActualVelocity() * Vector3(1, 0, 1));
        if (actualVelocityFlat.Length() > M_LARGE_EPSILON)
        {
            const float speed = actualVelocityFlat.Length() * 0.3f;
            rotationNode->SetWorldDirection(actualVelocityFlat);
            animController->PlayExistingExclusive(AnimationParameters{runAnimation}.Looped().Speed(speed), 0.2f);
        }
        else
        {
            animController->PlayExistingExclusive(AnimationParameters{idleAnimation}.Looped(), 0.2f);
        }

        // Toggle textures
        if (input->GetKeyPress(KEY_TAB))
        {
            auto cache = context_->GetSubsystem<ResourceCache>();
            auto animModel = animController->GetNode()->GetComponent<AnimatedModel>();

            texturesEnabled_ = !texturesEnabled_;
            if (texturesEnabled_)
                animModel->SetMaterial(cache->GetResource<Material>("Models/Mutant/Materials/mutant_M.xml"));
            else
                animModel->SetMaterial(cache->GetResource<Material>("Materials/DefaultWhite.xml"));
        }
        */
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
    private Viewport? _viewport;
}
