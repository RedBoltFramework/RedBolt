//
// Copyright (c) 2008-2020 the Urho3D project.
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

namespace RedBolt.Sample.HelloRmlUI;

using System;
using System.Runtime.CompilerServices;

using RedBolt;
using RedBolt.Bindings.RmlUi;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Platform;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.RmlUI;
using RedBolt.Scene;
using CoreVariant = RedBolt.Core.Variant;
using RmlUISubsystem = RedBolt.RmlUI.RmlUI;

internal static class HelloRmlUIProgram
{
    private static int Main()
    {
        try
        {
            using var application = new HelloRmlUI();
            var exitCode = application.Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt HelloRmlUI sample finished.");
            return exitCode;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

/// <summary>A 2D UI window, managed by main UI instance returned by GetSubsystem&lt;RmlUI&gt;().</summary>
[Reflected]
public partial class SimpleWindow : RmlUIComponent
{
    /// <summary>Construct.</summary>
    public SimpleWindow()
    {
        SetResource("UI/HelloRmlUI.rml");
        _variant.Value = new CoreVariant(new ResourceRef("Model", "Models/Box.mdl"));
        _variantVector.Add(new CoreVariant(42));
        _variantVector.Add(new CoreVariant("vector text"));
        _variantMap["int"] = new CoreVariant(24);
        _variantMap["str"] = new CoreVariant("map text");
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _variant.Value.Dispose();
        _variantVector.Dispose();
        _variantMap.Dispose();
        base.Dispose();
    }

    /// <summary>Callback function invoked from rml template.</summary>
    public void CountClicks(VariantVector arguments)
    {
        // Increase counter and notify model of it's update.
        ++_counter;
        DirtyVariable("counter");
    }

    /// <summary>Process 'CloseWindow' event.</summary>
    public void OnCloseWindow(ref RmlCustomEvent args)
    {
        if (args.EventType != new StringHash("CloseWindow"))
            return;

        var element = Element.CreateManagedWrapper(args.Data["_Element"].GetVoidPtr(), false);
        var ownerDocument = element.GetOwnerDocument();
        if (ownerDocument is not null && GetDocument() is { } document && ownerDocument.Handle == document.Handle)
            Remove();
    }

    /// <summary>Update model and animate progressbars.</summary>
    public override void Update(float timeStep)
    {
        base.Update(timeStep);

        // Animate progressbars
        _progress = (MathDefs.Sin(GetSubsystem<Time>()!.GetElapsedTime() * 50.0f) + 1.0f) / 2.0f;
        DirtyVariable("progress");
    }

    /// <summary>Initialize document model.</summary>
    protected override void OnDataModelInitialized()
    {
        // Create a data model for connecting UI with state kept in this class.
        // Important: there can only be one data model with given name per unique RmlUI subsystem!
        BindDataModelProperty("font_size",
            (ref CoreVariant value) => value = new CoreVariant(GetEmSize()),
            (in CoreVariant value) => SetEmSize(value.GetFloat()));
        BindDataModelProperty("slider_value",
            (ref CoreVariant value) => value = new CoreVariant(_sliderValue),
            (in CoreVariant value) => _sliderValue = value.GetInt());
        BindDataModelProperty("counter",
            (ref CoreVariant value) => value = new CoreVariant(_counter),
            (in CoreVariant value) => _counter = value.GetInt());
        BindDataModelProperty("progress",
            (ref CoreVariant value) => value = new CoreVariant(_progress),
            (in CoreVariant value) => _progress = value.GetFloat());
        BindDataModelVariant("variant", _variant);
        BindDataModelVariantVector("variant_vector", _variantVector);
        BindDataModelVariantMap("variant_map", _variantMap);

        BindDataModelEvent("count", CountClicks);

        // Act on pressing window close button.
        SubscribeToEvent<RmlCustomEvent>(GetUI(), OnCloseWindow);
    }

    /// <summary>Value of UI slider.</summary>
    protected int _sliderValue;
    /// <summary>Value of button click counter.</summary>
    protected int _counter;
    /// <summary>Value of progressbar progress.</summary>
    protected float _progress;
    /// <summary>Value of Urho3D::Variant type.</summary>
    protected readonly StrongBox<CoreVariant> _variant = new();
    /// <summary>Value of Urho3D::VariantVector type.</summary>
    protected readonly VariantVector _variantVector = [];
    /// <summary>Value of Urho3D::VariantMap type.</summary>
    protected readonly VariantMap _variantMap = [];
}

/// <summary>A RmlUI demonstration.</summary>
public sealed class HelloRmlUI : Application
{
    /// <summary>Construct.</summary>
    public HelloRmlUI()
    {
    }

    /// <summary>Setup after engine initialization and before running main loop.</summary>
    protected override void Start()
    {
        // Register custom components.
        RmlUISubsystem.RegisterRmlUILibrary(Context);
        Context.RegisterAssembly(typeof(SimpleWindow).Assembly);
        _ui = new RmlUISubsystem();
        Context.RegisterSubsystem(_ui);
        if (Engine.Parameters.LoadFonts)
            _ui.ReloadFonts();
        _ui.SetScale(GetSubsystem<RenderDevice>()!.GetDpiScale());

        // Execute base class startup
        base.Start();

        // Initialize Scene
        InitScene();

        // Initialize Window
        InitWindow();

        // Set the mouse mode to use in the sample
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    /// <summary>Initialize 3D scene.</summary>
    private void InitScene()
    {
        var cache = Resources;

        _scene = new Scene();
        _scene.CreateComponent<Octree>();
        var zone = _scene.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetFogColor(Color.Gray);
        zone.SetFogStart(100.0f);
        zone.SetFogEnd(300.0f);

        // Create a child scene node (at world origin) and a StaticModel component into it.
        var boxNode = _scene.CreateChild("Box");
        boxNode.SetScale(new Vector3f(5.0f, 5.0f, 5.0f));
        boxNode.SetRotation(new Quaternion(90.0f, Vector3f.Left));

        // Create a box model and hide it initially.
        var boxModel = boxNode.CreateComponent<StaticModel>();
        boxModel.SetModel(cache.Get<Model>("Models/Box.mdl"));

        // Create a camera.
        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<Camera>();

        // Set an initial position for the camera scene node.
        _cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -10.0f));

        // Set up a viewport so 3D scene can be visible.
        Renderer.SetViewport(0, new Viewport(_scene, _cameraNode.GetComponent<Camera>()!));
    }

    /// <summary>Initialize UI subsystems, backbuffer and cube windows.</summary>
    private void InitWindow()
    {
        // Create a window rendered into backbuffer.
        var window = _scene!.CreateComponent<SimpleWindow>();
        _window = new WeakReference<SimpleWindow>(window);

        // Node that will get UI rendered on it.
        var boxNode = _scene.GetChild("Box")!;

        // Create a texture we will render to.
        _texture = new Texture2D();

        // Create a material that will display UI texture on a cube.
        _material = new Material();
        _material.SetTechnique(0, Resources.Get<Technique>("Techniques/DiffUnlit.xml"));
        _material.SetTexture("Albedo", _texture);

        // Create a component that sets up UI rendering.
        var canvas = boxNode.CreateComponent<RmlCanvasComponent>();
        canvas.SetTexture(_texture);
        canvas.SetUISize(new Vector2i(512, 512));
        canvas.SetRemapMousePos(true);

        // Create a window that will be rendered on a cube.
        var windowOnCube = boxNode.CreateComponent<SimpleWindow>();
        _windowOnCube = new WeakReference<SimpleWindow>(windowOnCube);

        // Create a StaticModel for our cube.
        var model = boxNode.GetComponent<StaticModel>()!;

        // And set a material to it so UI would be rendered on to cube.
        model.SetMaterial(_material);
    }

    /// <summary>Animate cube, handle keys.</summary>
    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

        // Animate cube.
        var node = _scene!.GetChild("Box")!;
        node.Yaw(6.0f * timeStep * 1.5f);
        node.Roll(-6.0f * timeStep * 1.5f);
        node.Pitch(-6.0f * timeStep * 1.5f);

        if (Input.GetKeyPress(Key.F9))
            _ui!.SetDebuggerVisible(!_ui.IsDebuggerVisible());
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.HelloRmlUI";
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
        parameters.WindowTitle = "RedBolt Hello RmlUI";
    }

    protected override void Stop()
    {
        Renderer.SetNumViewports(0);
        _window = null;
        _windowOnCube = null;
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        _material?.Dispose();
        _material = null;
        _texture?.Dispose();
        _texture = null;
        Context.RemoveSubsystem<RmlUISubsystem>();
        _ui?.Dispose();
        _ui = null;
        base.Stop();
    }

    public override void Dispose()
    {
        if (_scene is not null)
            Renderer.SetNumViewports(0);
        _window = null;
        _windowOnCube = null;
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        _material?.Dispose();
        _material = null;
        _texture?.Dispose();
        _texture = null;
        Context.RemoveSubsystem<RmlUISubsystem>();
        _ui?.Dispose();
        _ui = null;
        base.Dispose();
    }

    /// <summary>Window which will be rendered into backbuffer.</summary>
    private WeakReference<SimpleWindow>? _window;
    /// <summary>Window which will be rendered onto a side of a cube.</summary>
    private WeakReference<SimpleWindow>? _windowOnCube;
    /// <summary>Texture to which windowOnCube_ will render.</summary>
    private Texture2D? _texture;
    /// <summary>Material which will apply windowOnCube_ on to a cube.</summary>
    private Material? _material;
    private RmlUISubsystem? _ui;
    private Scene? _scene;
    private Node? _cameraNode;
}
