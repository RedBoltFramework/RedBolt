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

namespace RedBolt.Sample.HttpRequestDemo;

using System;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.DearImGui;
using RedBolt.Engine;
using RedBolt.Input;
using RedBolt.Network;
using RedBolt.Resource;

internal static class HttpRequestDemoProgram
{
    private static int Main()
    {
        try
        {
            using var application = new HttpRequestDemo();
            return application.Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

/// <summary>HTTP request example.</summary>
public sealed class HttpRequestDemo : Application
{
    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.HttpRequestDemo";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowWidth = 1280;
            parameters.WindowHeight = 720;
#endif
        parameters.WindowTitle = "RedBolt HTTP Request Demo";
    }

    protected override void Start()
    {
        base.Start();
        Context.RegisterSubsystem(new RedBolt.Network.Network());
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => Draw());
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    protected override void Update(float timeStep)
    {
        if (_httpRequest is null)
        {
            const string verb = "GET";
            string[] headers = ["hello: world"];
            _httpRequest = new HttpRequest("https://httpbin.org/ip", verb, headers);
        }
        else if (_httpRequest.State == HttpRequestState.Initializing)
            return;
        else if (_httpRequest.State == HttpRequestState.Error)
        {
            SetText("An error has occurred: " + _httpRequest.Error);
            RedBolt.Logging.Log.GetLogger().Error("HttpRequest error: {} ({})",
                _httpRequest.Error, _httpRequest.StatusCode);
            _finished = true;
        }
        else if (_httpRequest.State == HttpRequestState.Open)
            SetText("Processing...");
        else if (_httpRequest.State == HttpRequestState.Closed && !_finished)
        {
            _message = _httpRequest.ReadString();
            RedBolt.Logging.Log.GetLogger().Info("HttpRequest success: {} ({})",
                _message, _httpRequest.StatusCode);

            using var json = new JSONFile();
            json.FromString(_message);
            var value = json.Root.Get("origin");
            SetText(value.IsNull ? "Invalid JSON response retrieved!" : "Your IP is: " + value.GetString());
            _finished = true;
        }
    }

    protected override void Stop()
    {
        _httpRequest?.Dispose();
        _httpRequest = null;
        _text.Dispose();
        base.Stop();
    }

    private void SetText(string text) => _text.FormatInPlace("{0}", text);

    private void Draw()
    {
        ImGui.SetNextWindowSize(new Vector2f(480.0f, 100.0f),
            ImGuiCond.ImGuiCondFirstUseEver);
        var open = true;
        if (ImGui.Begin("HTTP Request"u8, ref open,
            ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
            ImGui.TextUnformatted(_text.ContentBytes);
        ImGui.End();
    }

    private string _message = string.Empty;
    private readonly CString _text = new("Starting request...");
    private HttpRequest? _httpRequest;
    private bool _finished;
}
