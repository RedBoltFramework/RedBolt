//
// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2023-2023 the rbfx project.
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

namespace RedBolt.Sample.CharacterDemo;

using System;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.DearImGui;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.PhysicsJolt;
using RedBolt.Resource;
using RedBolt.Scene;

internal static class CharacterDemoProgram
{
    private static int Main()
    {
        try
        {
            using var application = new CharacterDemo();
            return application.Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

/// <summary>
/// Moving character example.
/// This sample demonstrates:
///     - Controlling a humanoid character through physics
///     - Driving animations using the AnimationController component
///     - Manual control of a bone scene node
///     - Implementing 1st and 3rd person cameras, using raycasts to avoid the 3rd person camera clipping into scenery
///     - Defining attributes of a custom component so that it can be saved and loaded
///     - Using touch inputs/gyroscope for iOS/Android (implemented through an external file)
/// </summary>
public class CharacterDemo : Application
{
    private const float CameraMinDist = 1.0f;
    private const float CameraInitialDist = 5.0f;
    private const float CameraMaxDist = 20.0f;

    /// <summary>Construct.</summary>
    public CharacterDemo()
    {
        _firstPerson = false;
    }

    /// <summary>Setup after engine initialization and before running the main loop.</summary>
    protected override void Start()
    {
        // Execute base class startup
        base.Start();

        RedBolt.PhysicsJolt.Physics.RegisterPhysicsLibrary(Context);
        _dPadAdapter = new DirectionalPadAdapter();
        _dPadAdapter.SetEnabled(true);

        // Create static scene content
        CreateScene();

        // Create the controllable character
        CreateCharacter();

        // Create the UI content
        CreateInstructions();

        // Subscribe to necessary events
        SubscribeToEvents();

        // Set the mouse mode to use in the sample
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    /// <summary>Create static scene content.</summary>
    private void CreateScene()
    {
        var cache = Resources;

        _scene = new Scene();

        // Create scene subsystem components
        _scene.CreateComponent<Octree>();
        _scene.CreateComponent<PhysicsWorld>();

        // Create camera and define viewport. We will be doing load / save, so it's convenient to create the camera outside the scene,
        // so that it won't be destroyed and recreated, and we don't have to redefine the viewport on load
        _cameraNode = _scene.CreateChild("Camera");
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(300.0f);
        _viewport = new Viewport(_scene, camera);
        Renderer.SetViewport(0, _viewport);

        // Create static scene content. First create a zone for ambient lighting and fog control
        var zoneNode = _scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetAmbientColor(new Color(0.15f, 0.15f, 0.15f));
        zone.SetFogColor(new Color(0.5f, 0.5f, 0.7f));
        zone.SetFogStart(100.0f);
        zone.SetFogEnd(300.0f);
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));

        // Create a directional light with cascaded shadow mapping
        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.3f, -0.5f, 0.425f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetCastShadows(true);
        light.SetShadowBias(new BiasParameters(0.00025f, 0.5f));
        light.SetShadowCascade(new CascadeParameters(10.0f, 50.0f, 200.0f, 0.0f, 0.8f));
        light.SetSpecularIntensity(0.5f);

        // Create the floor object
        var floorNode = _scene.CreateChild("Floor");
        floorNode.SetPosition(new Vector3f(0.0f, -0.5f, 0.0f));
        floorNode.SetScale(new Vector3f(200.0f, 1.0f, 200.0f));
        var floorObject = floorNode.CreateComponent<StaticModel>();
        floorObject.SetModel(cache.Get<Model>("Models/Box.mdl"));
        floorObject.SetMaterial(cache.Get<Material>("Materials/Stone.xml"));

        var floorBody = floorNode.CreateComponent<RigidBody>();
        // Use collision layer bit 2 to mark world scenery. This is what we will raycast against to prevent camera from going
        // inside geometry
        floorBody.CollisionLayer = 2;
        floorNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);

        // Create swing doors based on physics
        {
            var doorPrefab = cache.Get<PrefabResource>("Prefabs/Door.prefab");
            var objectNode = _scene.CreateChild("Door");
            objectNode.SetPosition(new Vector3f(2.0f, 0.5f, -2.0f));
            objectNode.CreateComponent<PrefabReference>().SetPrefab(doorPrefab);

            objectNode = _scene.CreateChild("Door");
            objectNode.SetPosition(new Vector3f(2.0f, 0.5f, 2.0f));
            objectNode.CreateComponent<PrefabReference>().SetPrefab(doorPrefab);
        }
        // Create sliding door
        {
            var doorPrefab = cache.Get<PrefabResource>("Prefabs/SlidingDoor.prefab");
            var objectNode = _scene.CreateChild("SlidingDoor");
            objectNode.SetPosition(new Vector3f(-3.0f, 0.0f, -3.0f));
            var prefabReference = objectNode.CreateComponent<PrefabReference>();
            prefabReference.SetPrefab(doorPrefab);
            prefabReference.Inline(PrefabInlineFlag.None);
        }
        // Create mushrooms of varying sizes
        const int NumMushrooms = 60;
        var mushroomPrefab = cache.Get<PrefabResource>("Prefabs/Mushroom.prefab");
        for (var i = 0; i < NumMushrooms; ++i)
        {
            var objectNode = _scene.CreateChild("Mushroom");
            objectNode.SetPosition(new Vector3f(MathDefs.Random(180.0f) - 90.0f, 0.0f, MathDefs.Random(180.0f) - 90.0f));
            objectNode.SetRotation(new Quaternion(0.0f, MathDefs.Random(360.0f), 0.0f));
            objectNode.SetScale(2.0f + MathDefs.Random(5.0f));
            objectNode.CreateComponent<PrefabReference>().SetPrefab(mushroomPrefab);
        }

        // Create movable boxes. Let them fall from the sky at first
        const int NumBoxes = 100;
        for (var i = 0; i < NumBoxes; ++i)
        {
            var scale = MathDefs.Random(2.0f) + 0.5f;

            var objectNode = _scene.CreateChild("Box");
            objectNode.SetPosition(new Vector3f(
                MathDefs.Random(180.0f) - 90.0f,
                MathDefs.Random(10.0f) + 10.0f,
                MathDefs.Random(180.0f) - 90.0f));
            objectNode.SetRotation(new Quaternion(
                MathDefs.Random(360.0f), MathDefs.Random(360.0f), MathDefs.Random(360.0f)));
            objectNode.SetScale(scale);
            var boxObject = objectNode.CreateComponent<StaticModel>();
            boxObject.SetModel(cache.Get<Model>("Models/Box.mdl"));
            boxObject.SetMaterial(cache.Get<Material>("Materials/Stone.xml"));
            boxObject.SetCastShadows(true);

            var body = objectNode.CreateComponent<RigidBody>();
            body.CollisionLayer = 2;
            // Bigger boxes will be heavier and harder to move
            body.Mass = scale * 2.0f;
            objectNode.CreateComponent<CollisionShape>().SetBox(Vector3f.One);
        }
    }

    /// <summary>Create controllable character.</summary>
    private void CreateCharacter()
    {
        var cache = Resources;

        var objectNode = _scene!.CreateChild("Jack");
        objectNode.SetPosition(new Vector3f(0.0f, 1.0f, 0.0f));

        // spin node
        var adjustNode = objectNode.CreateChild("AdjNode");
        adjustNode.SetRotation(new Quaternion(180.0f, new Vector3f(0.0f, 1.0f, 0.0f)));

        // Create the rendering component + animation controller
        var model = adjustNode.CreateComponent<AnimatedModel>();
        model.SetModel(cache.Get<Model>("Models/Mutant/Mutant.mdl"));
        model.SetMaterial(cache.Get<Material>("Models/Mutant/Materials/mutant_M.xml"));
        model.SetCastShadows(true);
        adjustNode.CreateComponent<AnimationController>();

        // Set the head bone for manual control
        model.GetSkeleton().GetBone("Mutant:Head")!.Animated = false;

        // Create rigidbody, and set non-zero mass so that the body becomes dynamic
        var body = objectNode.CreateComponent<RigidBody>();
        body.CollisionLayer = 1;
        body.Mass = 1.0f;

        // Set zero angular factor so that physics doesn't turn the character on its own.
        // Instead we will control the character yaw manually
        body.AngularFactor = Vector3f.Zero;

        // Set the rigidbody to signal collision also when in rest, so that we get ground collisions properly
        body.CollisionEventMode = CollisionEventMode.Always;

        // Set a capsule shape for collision
        objectNode.CreateComponent<CollisionShape>().SetCapsule(0.7f, 1.8f, new Vector3f(0.0f, 0.9f, 0.0f));

        // Create the character logic component, which takes care of steering the rigidbody
        // Remember it so that we can set the controls. Use a ea::weak_ptr because the scene hierarchy already owns it
        // and keeps it alive as long as it's not removed from the hierarchy
        var character = objectNode.CreateComponent<Character>();
        _character = new WeakReference<Character>(character);
        var moveAndOrbit = objectNode.CreateComponent<MoveAndOrbitController>();
        moveAndOrbit.LoadInputMap("Input/MoveAndOrbit.inputmap");
        character.SetInputMap(moveAndOrbit.GetInputMap());
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
        // Subscribe to PostUpdate event for updating the camera position after physics simulation
        SubscribeToEvent<CoreEvents.PostUpdateEvent>((ref CoreEvents.PostUpdateEvent _) => HandlePostUpdate());

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

        var rotating = Input.GetMouseButtonDown(MouseButton.Right) && !ImGui.GetIO().WantCaptureMouse;
        Input.SetMouseMode(rotating ? MouseMode.Relative : MouseMode.Free);
        Input.SetMouseVisible(!rotating);

        if (_character is not null && _character.TryGetTarget(out var character))
        {
            // Update controls using keys
            if (!ImGui.GetIO().WantCaptureKeyboard)
            {
                // Set rotation already here so that it's updated every rendering frame instead of every physics frame
                character.Node.SetRotation(new Quaternion(character.GetYaw(), Vector3f.Up));

                // Switch between 1st and 3rd person
                if (Input.GetKeyPress(Key.F))
                    _firstPerson = !_firstPerson;
            }
        }
    }

    /// <summary>Handle application post-update. Update camera position after character has moved.</summary>
    private void HandlePostUpdate()
    {
        if (_character is null || !_character.TryGetTarget(out var character))
            return;

        var characterNode = character.Node;

        // Get camera lookat dir from character yaw + pitch
        var rot = characterNode.Rotation;
        var dir = rot * new Quaternion(character.GetPitch(), Vector3f.Right);

        // Turn head to camera pitch, but limit to avoid unnatural animation
        var headNode = characterNode.GetChild("Mutant:Head", true)!;
        var limitPitch = MathDefs.Clamp(character.GetPitch(), -45.0f, 45.0f);
        var headDir = rot * new Quaternion(limitPitch, new Vector3f(1.0f, 0.0f, 0.0f));
        // This could be expanded to look at an arbitrary target, now just look at a point in front
        var headWorldTarget = headNode.WorldPosition + headDir * new Vector3f(0.0f, 0.0f, -1.0f);
        headNode.LookAt(headWorldTarget, new Vector3f(0.0f, 1.0f, 0.0f));

        if (_firstPerson)
        {
            _cameraNode!.SetPosition(headNode.WorldPosition + rot * new Vector3f(0.0f, 0.15f, 0.2f));
            _cameraNode.SetRotation(dir);
        }
        else
        {
            // Third person camera: position behind the character
            var aimPoint = characterNode.Position + rot * new Vector3f(0.0f, 1.7f, 0.0f);

            // Collide camera ray with static physics objects (layer bitmask 2) to ensure we see the character properly
            var rayDir = dir * Vector3f.Back;
            var rayDistance = CameraInitialDist;
            var result = default(PhysicsRaycastResult);
            _scene!.GetComponent<PhysicsWorld>()!.RaycastSingle(ref result, new Ray(aimPoint, rayDir), rayDistance, 2);
            if (result.Body is not null)
                rayDistance = MathF.Min(rayDistance, result.Distance);
            rayDistance = MathDefs.Clamp(rayDistance, CameraMinDist, CameraMaxDist);

            _cameraNode!.SetPosition(aimPoint + rayDir * rayDistance);
            _cameraNode.SetRotation(dir);
        }
    }

    private static void DrawInstructions()
    {
        var windowOpen = true;
        if (ImGui.Begin("Character Demo"u8, ref windowOpen,
                ImGuiWindowFlags.ImGuiWindowFlagsNoResize |
                ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted(
                "Use WASD keys and hold right mouse button/touch to move\nSpace to jump, F to toggle 1st/3rd person\nEscape to exit"u8);
        }
        ImGui.End();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.CharacterDemo";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Character Demo";
    }

    protected override void Stop()
    {
        Cleanup();
        base.Stop();
    }

    public override void Dispose()
    {
        Cleanup();
        _dPadAdapter?.SetEnabled(false);
        _dPadAdapter?.Dispose();
        _dPadAdapter = null;
        base.Dispose();
    }

    private void Cleanup()
    {
        if (_scene is not null)
            Renderer.SetNumViewports(0);
        _character = null;
        _cameraNode = null;
        _viewport?.Dispose();
        _viewport = null;
        _scene?.Dispose();
        _scene = null;
    }

    private Scene? _scene;
    private Node? _cameraNode;
    private Viewport? _viewport;
    /// <summary>The controllable character component.</summary>
    private WeakReference<Character>? _character;
    /// <summary>First person camera flag.</summary>
    private bool _firstPerson;
    private DirectionalPadAdapter? _dPadAdapter;
}
