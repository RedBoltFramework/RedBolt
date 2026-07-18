using Urho3D.Core;
using Urho3D.Engine;
using Urho3D.Graphics;
using Urho3D.Scene;
using Urho3D.Input;
using Urho3D.Math;
using Urho3D.Resource;
using Ea;
using ImGuiNet;
using System.Globalization;

namespace SampleApplication;

partial class DemoApplication : Application
{
    private const float CubeRotationSpeed = 45.0f;
    private const string DemoDocumentMarkup = """
        <rml>
        <head>
            <title>SharpCpp RmlUI Demo</title>
            <style>
                body {
                    margin: 0;
                    font-family: "Noto Sans";
                    color: #f5f7fa;
                }

                .panel {
                    display: block;
                    position: absolute;
                    top: 28px;
                    left: 28px;
                    width: 360px;
                    padding: 20px 22px 18px;
                    background-color: #182430dd;
                }

                .eyebrow {
                    display: block;
                    color: #8fc7ea;
                    font-size: 13px;
                    text-transform: uppercase;
                    letter-spacing: 1px;
                    margin-bottom: 8px;
                }

                .title {
                    display: block;
                    margin-bottom: 12px;
                    font-size: 27px;
                    font-weight: bold;
                }

                .copy {
                    display: block;
                    margin-bottom: 16px;
                    color: #d0d8e2;
                    font-size: 16px;
                }

                .metrics {
                    display: block;
                    margin-bottom: 16px;
                }

                .metric {
                    display: block;
                    margin-bottom: 10px;
                    padding: 9px 14px 10px;
                    background-color: #223548;
                }

                .metric-label {
                    display: block;
                    color: #8ea9bf;
                    font-size: 12px;
                    margin-bottom: 3px;
                    text-transform: uppercase;
                    letter-spacing: 1px;
                }

                .metric-value {
                    display: block;
                    font-size: 21px;
                    font-weight: bold;
                }

                .editor-label {
                    display: block;
                    margin-bottom: 6px;
                    color: #8ea9bf;
                    font-size: 12px;
                    text-transform: uppercase;
                    letter-spacing: 1px;
                }

                .note-input {
                    display: block;
                    width: 100%;
                    margin-bottom: 10px;
                    padding: 8px 10px;
                    background-color: #0f1923;
                    border: 1px solid #39556d;
                    color: #f5f7fa;
                    font-size: 15px;
                }

                .note-preview {
                    display: block;
                    margin-bottom: 14px;
                    color: #d0d8e2;
                    font-size: 14px;
                }

                .actions {
                    display: block;
                    margin-bottom: 14px;
                }

                .action-button {
                    display: block;
                    width: 100%;
                    margin-bottom: 8px;
                    padding: 9px 10px;
                    background-color: #2b7ea1;
                    border: 1px solid #54a9cd;
                    color: #f5f7fa;
                    font-size: 14px;
                    font-weight: bold;
                }

                .action-button.secondary {
                    margin-bottom: 0;
                    background-color: #1b2c39;
                    border-color: #39556d;
                }

                .hint {
                    display: block;
                    color: #8fc7ea;
                    font-size: 14px;
                }
            </style>
        </head>
        <body>
            <div class="panel" data-model="dashboard">
                <div class="eyebrow">RmlUI Overlay</div>
                <div class="title">Rbfx.Application</div>
                <div class="copy">This document is created from memory and driven through Rml data-model callbacks implemented in C#.</div>

                <div class="metrics">
                    <div class="metric">
                        <div class="metric-label">Elapsed</div>
                        <div class="metric-value">{{elapsed}}</div>
                    </div>
                    <div class="metric">
                        <div class="metric-label">Cube rotation</div>
                        <div class="metric-value">{{rotation}}</div>
                    </div>
                    <div class="metric">
                        <div class="metric-label">Frame rate</div>
                        <div class="metric-value">{{fps}}</div>
                    </div>
                    <div class="metric">
                        <div class="metric-label">Status</div>
                        <div class="metric-value">{{status}}</div>
                    </div>
                </div>

                <div class="editor-label">Operator note</div>
                <input class="note-input" type="text" data-value="note"/>
                <div class="note-preview">Echo: {{note}}</div>

                <div class="actions">
                    <button class="action-button" data-event-click="apply_note('Pinned from event callback')">Apply preset note</button>
                    <button class="action-button secondary" data-event-click="apply_note('')">Clear note</button>
                </div>

                <div class="hint">Edit the note or press the buttons to exercise both `BindFunc` and `BindEventCallback` from C#.</div>
            </div>
        </body>
        </rml>
        """;

    private readonly Ea.String _documentSource = new("memory://rbfx.application/demo.rml");
    private readonly byte[] _imguiWindowTitle = "SharpCpp ImGui Demo"u8.ToArray();
    private readonly byte[] _imguiIntroHeader = "Dear ImGui"u8.ToArray();
    private readonly byte[] _imguiIntroText = "Generated from imgui.h through SharpCpp. This window uses source-driven Dear ImGui bindings."u8.ToArray();
    private readonly byte[] _imguiNoteHeader = "Operator note"u8.ToArray();
    private readonly byte[] _imguiToggleTooltip = "Toggles extra Dear ImGui details for this sample window."u8.ToArray();

    private Viewport _viewport;
    private Scene _scene;
    private Node _camera;
    private Node _cube;
    private Node _light;
    private Urho3D.RmlUI.RmlUI _ui;
    private Rml.Context _rmlContext;
    private Rml.DataModelConstructor _dataModelConstructor;
    private Rml.DataModelHandle _dataModelHandle;
    private Rml.ElementDocument _document;
    private float _elapsedTime;
    private float _cubeRotation;
    private readonly Ea.String _elapsedText = new("0.0 s");
    private readonly Ea.String _rotationText = new("0 deg");
    private readonly Ea.String _fpsText = new("--");
    private readonly Ea.String _statusText = new("Initializing");
    private readonly Ea.String _noteText = new("Bindings online");
    private float _framesPerSecond;
    private bool _hasFrameRate;
    private int _eventCount;
    private bool _showImGuiDetails = true;
    private readonly Ea.String _imguiElapsedLabel = new(string.Empty);
    private readonly Ea.String _imguiRotationLabel = new(string.Empty);
    private readonly Ea.String _imguiFpsLabel = new(string.Empty);
    private readonly Ea.String _imguiStatusLabel = new(string.Empty);
    private readonly Ea.String _imguiNoteValue = new(string.Empty);
    private readonly Ea.String _imguiToggleLabel = new(string.Empty);
    private readonly Ea.String _imguiDetailsText = new(string.Empty);

    public DemoApplication(Context context) : base(context)
    {
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _document?.Close();
            _document?.Dispose();
            _dataModelHandle?.Dispose();
            _dataModelConstructor?.Dispose();
            _viewport?.Dispose();
            _scene?.Dispose();
            _documentSource.Dispose();
            _elapsedText.Dispose();
            _rotationText.Dispose();
            _fpsText.Dispose();
            _statusText.Dispose();
            _noteText.Dispose();
            _imguiElapsedLabel.Dispose();
            _imguiRotationLabel.Dispose();
            _imguiFpsLabel.Dispose();
            _imguiStatusLabel.Dispose();
            _imguiNoteValue.Dispose();
            _imguiToggleLabel.Dispose();
            _imguiDetailsText.Dispose();
        }

        base.Dispose(disposing);
    }

    public override void Setup()
    {
        EngineParameters[EngineDefs.FullScreen] = false;
        EngineParameters[EngineDefs.Borderless] = false;
        EngineParameters[EngineDefs.WindowWidth] = 1280;
        EngineParameters[EngineDefs.WindowHeight] = 720;
        EngineParameters[EngineDefs.WindowTitle] = "SharpCpp RmlUI + ImGui Demo";
        EngineParameters[EngineDefs.EngineAutoLoadScripts] = false;
    }

    public override void Start()
    {
        var input = Context.GetSubsystem<Input>();
        input.SetMouseMode(MouseMode.MmFree);
        input.SetMouseVisible(true);

        _scene = new Scene(Context);
        _scene.CreateComponent<Octree>();

        _camera = _scene.CreateChild("Camera");
        _viewport = new Viewport(Context)
        {
            Scene = _scene,
            Camera = _camera.CreateComponent<Camera>()
        };

        var renderer = Context.GetSubsystem<Renderer>();
        renderer.SetViewport(0, _viewport);
        renderer.DefaultZone.FogColor = new Color(0.5f, 0.5f, 0.7f);

        _camera.Position = new Vector3(0, 2, -2);
        _camera.LookAt(Vector3.Zero);

        _cube = _scene.CreateChild("Cube");
        var model = _cube.CreateComponent<StaticModel>();
        var resourceCache = Context.GetSubsystem<ResourceCache>();
        model.SetModel(resourceCache.GetResource<Model>("Models/Box.mdl"));
        model.SetMaterial(0, resourceCache.GetResource<Material>("Materials/Stone.xml"));

        _light = _scene.CreateChild("Light");
        _light.CreateComponent<Light>();
        _light.Position = new Vector3(0, 2, -1);
        _light.LookAt(Vector3.Zero);

        InitializeRmlUi();

        SubscribeToEvent(CoreEvents.Update, OnUpdate);
    }

    private void OnUpdate(Urho3D.Core.Object sender, StringHash eventType, Ea.UnorderedMap<StringHash, Variant> eventData)
    {
        var timeStep = eventData.TryGetValue(CoreEvents.Update.TimeStep, out var value)
            ? value.Float
            : 0.0f;
        UpdateDashboard(timeStep);
    }

    private void InitializeRmlUi()
    {
        _ui = Context.GetSubsystem<Urho3D.RmlUI.RmlUI>()
            ?? throw new InvalidOperationException("Rbfx RmlUI subsystem is not available.");

        if (!_ui.LoadFont("Fonts/NotoSans-Condensed.ttf") || !_ui.LoadFont("Fonts/NotoSans-CondensedBold.ttf"))
        {
            throw new InvalidOperationException("Failed to load the fonts required by the RmlUI demo.");
        }

        _rmlContext = _ui.RmlContext;
        InitializeDataModel();

        using var documentMarkup = new Ea.String(DemoDocumentMarkup);
        _document = _rmlContext.LoadDocumentFromMemory(documentMarkup, _documentSource)
            ?? throw new InvalidOperationException("Failed to create the in-memory RmlUI document.");

        _document.Show();
        UpdateImGuiTextState();
    }

    private void InitializeDataModel()
    {
        using var modelName = new Ea.String("dashboard");
        using var elapsedName = new Ea.String("elapsed");
        using var rotationName = new Ea.String("rotation");
        using var fpsName = new Ea.String("fps");
        using var statusName = new Ea.String("status");
        using var noteName = new Ea.String("note");
        using var applyNoteName = new Ea.String("apply_note");

        _dataModelConstructor = _rmlContext.CreateDataModel(modelName)
            ?? throw new InvalidOperationException("Failed to create the RmlUI data model.");

        BindTextValue(elapsedName, _elapsedText);
        BindTextValue(rotationName, _rotationText);
        BindTextValue(fpsName, _fpsText);
        BindTextValue(statusName, _statusText);
        BindMutableTextValue(noteName, _noteText);
        BindRequired(applyNoteName, ApplyNoteFromEvent);

        _dataModelHandle = _dataModelConstructor.GetModelHandle()
            ?? throw new InvalidOperationException("Failed to retrieve the RmlUI data model handle.");
    }

    private void UpdateDashboard(float timeStep)
    {
        _elapsedTime += timeStep;
        _cubeRotation = (_cubeRotation + CubeRotationSpeed * timeStep) % 360.0f;
        _cube.Yaw(CubeRotationSpeed * timeStep);

        UpdateModelValue(_elapsedText, $"{_elapsedTime.ToString("0.0")} s", "elapsed");
        UpdateModelValue(_rotationText, $"{_cubeRotation.ToString("0")} deg", "rotation");
        _hasFrameRate = timeStep > 0.0f;
        _framesPerSecond = _hasFrameRate ? 1.0f / timeStep : 0.0f;

        UpdateModelValue(_fpsText, _hasFrameRate
            ? $"{_framesPerSecond.ToString("0")} fps"
            : "--",
            "fps");

        using var noteName = new Ea.String("note");
        var noteDirty = _dataModelHandle != null && _dataModelHandle.IsVariableDirty(noteName);
        var statusText = _eventCount > 0
            ? $"{_eventCount.ToString(CultureInfo.InvariantCulture)} event callback(s) handled"
            : noteDirty
                ? (IsNullOrWhiteSpace(_noteText) ? "Note cleared through the data model" : "Note updated through the data model")
                : (_ui.IsHovered ? "Pointer over UI" : "Pointer free for the 3D scene");

        UpdateModelValue(_statusText, statusText, "status");
        UpdateImGuiTextState();
        DrawImGui();
    }

    unsafe private void DrawImGui()
    {
        ImGui.SetNextWindowPos(new Vector2(430.0f, 28.0f), ImGuiCond.Once);
        ImGui.SetNextWindowSize(new Vector2(380.0f, 248.0f), ImGuiCond.Once);

        var renderBody = ImGui.Begin(_imguiWindowTitle, out _);

        try
        {
            if (!renderBody)
            {
                return;
            }

            ImGui.SeparatorText(_imguiIntroHeader);
            ImGui.PushTextWrapPos();
            try
            {
                ImGui.TextUnformatted(_imguiIntroText);
            }
            finally
            {
                ImGui.PopTextWrapPos();
            }

            ImGui.TextUnformatted(_imguiElapsedLabel);
            ImGui.TextUnformatted(_imguiRotationLabel);
            ImGui.TextUnformatted(_imguiFpsLabel);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.56f, 0.78f, 0.92f, 1.0f));
            try
            {
                ImGui.TextUnformatted(_imguiStatusLabel);
            }
            finally
            {
                ImGui.PopStyleColor();
            }

            ImGui.SeparatorText(_imguiNoteHeader);
            ImGui.PushTextWrapPos();
            try
            {
                ImGui.TextUnformatted(_imguiNoteValue);
            }
            finally
            {
                ImGui.PopTextWrapPos();
            }

            if (ImGui.Button(_imguiToggleLabel))
            {
                _showImGuiDetails = !_showImGuiDetails;
                UpdateImGuiTextState();
            }

            if (ImGui.BeginItemTooltip())
            {
                try
                {
                    ImGui.TextUnformatted(_imguiToggleTooltip);
                }
                finally
                {
                    ImGui.EndTooltip();
                }
            }

            if (_showImGuiDetails)
            {
                ImGui.Separator();
                ImGui.PushTextWrapPos();
                try
                {
                    ImGui.TextUnformatted(_imguiDetailsText);
                }
                finally
                {
                    ImGui.PopTextWrapPos();
                }
            }
        }
        finally
        {
            ImGui.End();
        }
    }

    private void UpdateImGuiTextState()
    {
        _imguiElapsedLabel.Format($"Elapsed: {_elapsedTime:F1} s");
        _imguiRotationLabel.Format($"Rotation: {_cubeRotation:F0} deg");
        if (_hasFrameRate)
        {
            _imguiFpsLabel.Format($"Frame rate: {_framesPerSecond:F0} fps");
        }
        else
        {
            _imguiFpsLabel.Format($"Frame rate: --");
        }

        _imguiStatusLabel.Format($"Status: {_statusText}");
        _imguiNoteValue.Format($"{(IsNullOrWhiteSpace(_noteText) ? "(empty)" : _noteText)}");
        _imguiToggleLabel.Format($"{(_showImGuiDetails ? "Hide extra ImGui details" : "Show extra ImGui details")}");
        _imguiDetailsText.Format(
            $"UI hovered: {(_ui != null && _ui.IsHovered ? "yes" : "no")} | Event callbacks: {_eventCount} | Note length: {_noteText.Count}");
    }

    private void BindRequired(Ea.String name, Action<Rml.Variant> getFunc)
    {
        if (!_dataModelConstructor.BindFunc(name, getFunc))
        {
            throw new InvalidOperationException($"Failed to bind RmlUI data-model variable '{name}'.");
        }
    }

    private void BindRequired(Ea.String name, Action<Rml.Variant> getFunc, Action<Rml.Variant> setFunc)
    {
        if (!_dataModelConstructor.BindFunc(name, getFunc, setFunc))
        {
            throw new InvalidOperationException($"Failed to bind RmlUI data-model variable '{name}'.");
        }
    }

    private void BindRequired(Ea.String name, Action<Rml.DataModelHandle, Rml.Event, Ea.Vector<Rml.Variant>> eventFunc)
    {
        if (!_dataModelConstructor.BindEventCallback(name, eventFunc))
        {
            throw new InvalidOperationException($"Failed to bind RmlUI data-model event '{name}'.");
        }
    }

    private void BindTextValue(Ea.String name, Ea.String text)
    {
        BindRequired(name, value =>
        {
            _ = value.Assign<Ea.String>(text);
        });
    }

    private void BindMutableTextValue(Ea.String name, Ea.String text)
    {
        BindRequired(
            name,
            value =>
            {
                _ = value.Assign<Ea.String>(text);
            },
            value => text.Assign(value.Get<Ea.String>()));
    }

    private void UpdateModelValue(Ea.String currentValue, string newValue, string variableName)
    {
        if (currentValue.Equals(newValue) || _dataModelHandle == null)
        {
            return;
        }

        currentValue.Assign(newValue);
        using var nativeVariableName = new Ea.String(variableName);
        _dataModelHandle.DirtyVariable(nativeVariableName);
    }

    private void ApplyNoteFromEvent(Rml.DataModelHandle handle, Rml.Event @event, Ea.Vector<Rml.Variant> arguments)
    {
        _ = @event;

        if (arguments.Count > 0)
        {
            _noteText.Assign(arguments[0].Get<Ea.String>());
        }
        else
        {
            _noteText.Assign(string.Empty);
        }

        _eventCount++;
        handle.DirtyVariable("note");
        handle.DirtyVariable("status");
    }

    private static bool IsNullOrWhiteSpace(Ea.String value)
        => string.IsNullOrWhiteSpace(value.ToString());
}

internal class Program
{
    static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

        using var context = new Context();

        using var app = new DemoApplication(context);
        return app.Run();
    }
}
