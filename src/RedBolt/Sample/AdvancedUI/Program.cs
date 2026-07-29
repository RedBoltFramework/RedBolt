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

namespace RedBolt.Sample.AdvancedUI;

using System;

using RedBolt;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Platform;
using RedBolt.RenderAPI;
using RedBolt.Resource;
using RedBolt.RmlUI;
using RedBolt.Scene;
using CoreVariant = RedBolt.Core.Variant;
using RmlUISubsystem = RedBolt.RmlUI.RmlUI;

internal static class AdvancedUIProgram
{
    private static int Main()
    {
        try
        {
            using var application = new AdvancedUIApplication();
            var exitCode = application.Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt AdvancedUI sample finished.");
            return exitCode;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

public struct SampleExitRequestedEvent
{
}

/// <summary>A 2D UI window, managed by main UI instance returned by GetSubsystem&lt;RmlUI&gt;().</summary>
[Reflected]
public partial class AdvancedUIWindow : RmlUIComponent
{
    public AdvancedUIWindow()
    {
        SetResource("UI/AdvancedUI.rml");

        // Fill sample data
        const int numSavedGames = 25;
        for (var i = 1; i <= numSavedGames; ++i)
            _savedGames.Add($"Saved Game {i}<br/>2022-08-14 16:00");
        _savedGames.Reverse();
        _nextGameIndex = (uint)_savedGames.Count + 1;
    }

    private enum PageIndex : uint
    {
        MainMenu = 0,
        LoadGame,
    }

    private AdvancedUI? GetSample()
    {
        var stateManager = GetSubsystem<StateManager>();
        return stateManager!.State as AdvancedUI;
    }

    /// <summary>Implement RmlUIComponent.</summary>
    public override void Update(float timeStep)
    {
        base.Update(timeStep);
    }

    protected override void OnDataModelInitialized()
    {
        BindDataModelProperty("current_page",
            (ref CoreVariant value) => value = new CoreVariant((uint)_currentPage),
            (in CoreVariant value) => _currentPage = (PageIndex)value.GetUInt());
        BindDataModelProperty("selected_game_index",
            (ref CoreVariant value) => value = new CoreVariant(_selectedGameIndex),
            (in CoreVariant value) => _selectedGameIndex = value.GetUInt());

        BindDataModelStringVector("saved_games", _savedGames);
        BindDataModelProperty("is_game_played",
            (ref CoreVariant value) => value = new CoreVariant(_isGamePlayed),
            (in CoreVariant value) => _isGamePlayed = value.GetBool());

        var constructor = GetDataModelConstructor()!;
        using var onContinueName = new Std.String();
        onContinueName.Assign("on_continue");
        constructor.BindEventCallback(onContinueName, WrapCallback(OnContinue));
        using var onNewGameName = new Std.String();
        onNewGameName.Assign("on_new_game");
        constructor.BindEventCallback(onNewGameName, WrapCallback(OnNewGame));
        using var onLoadGameName = new Std.String();
        onLoadGameName.Assign("on_load_game");
        constructor.BindEventCallback(onLoadGameName, WrapCallback(OnLoadGame));
        using var onDeleteGameName = new Std.String();
        onDeleteGameName.Assign("on_delete_game");
        constructor.BindEventCallback(onDeleteGameName, WrapCallback(OnDeleteGame));
        using var onSettingsName = new Std.String();
        onSettingsName.Assign("on_settings");
        constructor.BindEventCallback(onSettingsName, WrapCallback(OnSettings));
        using var onExitName = new Std.String();
        onExitName.Assign("on_exit");
        constructor.BindEventCallback(onExitName, WrapCallback(OnExit));
        using var goBackName = new Std.String();
        goBackName.Assign("go_back");
        constructor.BindEventCallback(goBackName, WrapCallback(GoBack));
    }

    protected override void OnDocumentPostLoad()
    {
        RestoreFocus();
    }

    private void OnContinue()
    {
    }

    private void OnNewGame()
    {
        _isGamePlayed = true;
        _playedGameName = $"New Game {_nextGameIndex}<br/>2022-08-14 16:00";
        ++_nextGameIndex;
        _savedGames.Insert(0, _playedGameName);

        if (GetSample() is { } sample)
            sample.InitGame(_isGamePlayed, _playedGameName);

        DirtyAllVariables();
    }

    private void OnLoadGame()
    {
        if (_selectedGameIndex >= (uint)_savedGames.Count)
            return;

        _isGamePlayed = true;
        _playedGameName = _savedGames[(int)_selectedGameIndex];

        if (GetSample() is { } sample)
            sample.InitGame(_isGamePlayed, _playedGameName);

        DirtyAllVariables();
    }

    private void OnDeleteGame()
    {
        if (_selectedGameIndex >= (uint)_savedGames.Count)
            return;

        _savedGames.RemoveAt((int)_selectedGameIndex);

        DirtyAllVariables();
    }

    private void OnSettings()
    {
    }

    private void OnExit()
    {
        if (GetSample() is { } sample)
            sample.CloseSample();
    }

    private void GoBack()
    {
        _currentPage = PageIndex.MainMenu;

        DirtyAllVariables();
    }

    /// <summary>UI state.</summary>
    private PageIndex _currentPage;
    private uint _selectedGameIndex;
    private readonly StringVector _savedGames = [];
    private bool _isGamePlayed;

    private uint _nextGameIndex;
    private string _playedGameName = string.Empty;
}

/// <summary>A RmlUI demonstration.</summary>
[Reflected]
public partial class AdvancedUI : ApplicationState
{
    /// <summary>Construct.</summary>
    public AdvancedUI()
    {
    }

    public override void Activate(StringVariantMap bundle)
    {
        base.Activate(bundle);
        Start();
    }

    public override void Deactivate()
    {
        Stop();
        base.Deactivate();
    }

    /// <summary>Setup after engine initialization and before running the main loop.</summary>
    public void Start()
    {
        // Register custom components.
        Context.RegisterAssembly(typeof(AdvancedUIWindow).Assembly);

        // Execute base class startup

        // Initialize Scene
        InitScene();

        // Initialize Window
        InitWindow();

        // Set the mouse mode to use in the sample
        SetMouseMode(MouseMode.Free);
        SetMouseVisible(true);
    }

    public void Stop()
    {
    }

    /// <summary>Disable exit on Esc.</summary>
    public bool IsEscapeEnabled() => false;

    /// <summary>Initialize 3D scene.</summary>
    private void InitScene()
    {
        var cache = GetSubsystem<ResourceCache>()!;

        _scene = new Scene();
        _scene.CreateComponent<Octree>();
        var zone = _scene.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetFogColor(Color.Gray);
        zone.SetFogStart(100.0f);
        zone.SetFogEnd(300.0f);

        // Create skybox.
        var skyboxNode = _scene.CreateChild("Sky");
        var skybox = skyboxNode.CreateComponent<Skybox>();
        skybox.SetModel(cache.Get<Model>("Models/Box.mdl"));
        skybox.SetMaterial(cache.Get<Material>("Materials/Skybox.xml"));

        // Create 3D text.
        // TODO: port Text3D when the excluded built-in rbfx UI surface is available.
        // auto* textNode = scene_->CreateChild("Text");
        // textNode->SetPosition({-2.0f, 1.0f, 0.0f});
        // text3D_ = textNode->CreateComponent<Text3D>();
        // text3D_->SetFont(cache->GetResource<Font>("Fonts/BlueHighway.sdf"), 24);
        // text3D_->SetText("This text should never be visible");
        // text3D_->SetColor(0x483d8b_rgb);
        // text3D_->SetEnabled(false);

        // Create a camera.
        _cameraNode = _scene.CreateChild("Camera");
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetFov(90.0f);

        // Set an initial position for the camera scene node.
        _cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -2.0f));

        // Set up a viewport so 3D scene can be visible.
        _viewport = new Viewport(_scene, camera);
        SetViewport(0, _viewport);
    }

    /// <summary>Initialize UI subsystems, backbuffer and cube windows.</summary>
    private void InitWindow()
    {
        var ui = GetSubsystem<RmlUISubsystem>();

        // Create a window rendered into backbuffer.
        var window = _scene!.CreateComponent<AdvancedUIWindow>();
        _window = new WeakReference<AdvancedUIWindow>(window);

        // Subscribe to update event for handling keys and animating cube.
        SubscribeToEvent<CoreEvents.UpdateEvent>(OnUpdate);
    }

    /// <summary>Initialize currently played game.</summary>
    public void InitGame(bool gamePlayed, string text = "")
    {
        // TODO: port Text3D when the excluded built-in rbfx UI surface is available.
        // text3D_->SetEnabled(gamePlayed);
        // text3D_->SetText(text.replaced("<br/>", "\n"));
    }

    /// <summary>Handle keys.</summary>
    private void OnUpdate(ref CoreEvents.UpdateEvent eventData)
    {
        var timeStep = eventData.TimeStep;

        var input = GetSubsystem<Input>()!;

        if (input.GetKeyPress(Key.F9))
        {
            var ui = GetSubsystem<RmlUISubsystem>()!;
            ui.SetDebuggerVisible(!ui.IsDebuggerVisible());
        }
    }

    public void CloseSample()
    {
        SendEvent<SampleExitRequestedEvent>();
    }

    public override void Dispose()
    {
        _window = null;
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        _viewport?.Dispose();
        _viewport = null;
        base.Dispose();
    }

    /// <summary>Window which will be rendered into backbuffer.</summary>
    private WeakReference<AdvancedUIWindow>? _window;
    /// <summary>3D text that acts as indicator of played game.</summary>
    // TODO: port Text3D when the excluded built-in rbfx UI surface is available.
    // WeakPtr<Text3D> text3D_;

    private Scene? _scene;
    private Node? _cameraNode;
    private Viewport? _viewport;
}

internal sealed class AdvancedUIApplication : Application
{
    protected override void Start()
    {
        RmlUISubsystem.RegisterRmlUILibrary(Context);
        _ui = new RmlUISubsystem();
        Context.RegisterSubsystem(_ui);
        if (Engine.Parameters.LoadFonts)
            _ui.ReloadFonts();
        _ui.SetScale(GetSubsystem<RenderDevice>()!.GetDpiScale());

        base.Start();

        SubscribeToEvent<SampleExitRequestedEvent>((ref SampleExitRequestedEvent _) => Exit());

        _sample = new AdvancedUI();
        GetSubsystem<StateManager>()!.EnqueueState(_sample);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested)
            Exit();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.AdvancedUI";
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
        parameters.WindowTitle = "RedBolt Advanced UI";
    }

    protected override void Stop()
    {
        GetSubsystem<StateManager>()?.Reset();
        _sample?.Dispose();
        _sample = null;
        Context.RemoveSubsystem<RmlUISubsystem>();
        _ui?.Dispose();
        _ui = null;
        base.Stop();
    }

    public override void Dispose()
    {
        GetSubsystem<StateManager>()?.Reset();
        _sample?.Dispose();
        _sample = null;
        Context.RemoveSubsystem<RmlUISubsystem>();
        _ui?.Dispose();
        _ui = null;
        base.Dispose();
    }

    private AdvancedUI? _sample;
    private RmlUISubsystem? _ui;
}
