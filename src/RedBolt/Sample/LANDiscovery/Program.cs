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

namespace RedBolt.Sample.LANDiscovery;

using System;
using System.Collections.Generic;
using System.Text;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.DearImGui;
using RedBolt.Engine;
using RedBolt.Input;
using RedBolt.Network;

internal static class LANDiscoveryProgram
{
    private static int Main()
    {
        try
        {
            using var application = new LANDiscovery();
            return application.Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

/// <summary>LAN discovery example.</summary>
public sealed class LANDiscovery : Application
{
    private const ushort ServerPort = 54654;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.LANDiscovery";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowWidth = 1280;
            parameters.WindowHeight = 720;
#endif
        parameters.WindowTitle = "RedBolt LAN Discovery";
    }

    protected override void Start()
    {
        base.Start();
        Context.RegisterSubsystem(new RedBolt.Network.Network());
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        _lanDiscovery = new LANDiscoveryManager();
        SubscribeToEvent<NetworkEvents.NetworkHostDiscoveredEvent>(HandleNetworkHostDiscovered);
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => Draw());
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    protected override void Update(float timeStep)
    {
        for (var iterator = _serverListItems.GetEnumerator(); iterator.MoveNext();)
        {
            var item = iterator.Current.Value;
            if (Time.GetTimeSinceEpoch() - item.LastSeen <= 10)
                continue;

            RedBolt.Logging.Log.GetLogger().Info("Server {} expired!", item.Name);
            _expiredServers.Add(iterator.Current.Key);
        }

        foreach (var name in _expiredServers)
            _serverListItems.Remove(name);
        _expiredServers.Clear();
    }

    protected override void Stop()
    {
        _lanDiscovery?.Dispose();
        _lanDiscovery = null;
        base.Stop();
    }

    private void Draw()
    {
        ImGui.SetNextWindowSize(new Vector2f(620.0f, 360.0f),
            ImGuiCond.ImGuiCondFirstUseEver);
        var open = true;
        if (ImGui.Begin("LAN Discovery"u8, ref open,
            ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("1. Start server"u8);
            if (!_serverRunning && ImGui.Button("Start server"u8))
                StartServer();
            if (_serverRunning && ImGui.Button("Stop server"u8))
                StopServer();

            ImGui.Spacing();
            ImGui.TextUnformatted("2. Discover LAN servers"u8);
            if (ImGui.Button("Search..."u8))
                DoNetworkDiscovery();

            ImGui.Spacing();
            ImGui.TextUnformatted("Local servers:"u8);
            foreach (var item in _serverListItems.Values)
            {
                var text = $"{item.Name} ({item.Players}) {item.Address}:{item.Port}";
                ImGui.TextUnformatted(Encoding.UTF8.GetBytes(text));
            }
        }
        ImGui.End();
    }

    private void HandleNetworkHostDiscovered(ref NetworkEvents.NetworkHostDiscoveredEvent eventData)
    {
        var data = eventData.Beacon;
        var name = data[new StringHash("Name")].GetString();
        if (_serverListItems.TryGetValue(name, out var item))
        {
            RedBolt.Logging.Log.GetLogger().Info("Server {} reannounced itself!", name);
            item.LastSeen = Time.GetTimeSinceEpoch();
            _serverListItems[name] = item;
        }
        else
        {
            RedBolt.Logging.Log.GetLogger().Info("Server {} discovered!", name);
            _serverListItems[name] = new ServerListEntry
            {
                Name = name,
                Players = data[new StringHash("Players")].GetInt(),
                Address = eventData.Address,
                Port = (ushort)eventData.Port,
                LastSeen = Time.GetTimeSinceEpoch()
            };
        }
    }

    private void StartServer()
    {
        using var data = new VariantMap
        {
            [new StringHash("Name")] = "Test server",
            [new StringHash("Players")] = 100
        };
        _lanDiscovery!.SetBroadcastData(data);
        _serverRunning = _lanDiscovery.Start(ServerPort);
    }

    private void StopServer()
    {
        _lanDiscovery!.Stop();
        _serverRunning = false;
    }

    private void DoNetworkDiscovery()
    {
        _lanDiscovery!.Start(ServerPort);
        _serverListItems.Clear();
    }

    private struct ServerListEntry
    {
        public string Name;
        public int Players;
        public string Address;
        public ushort Port;
        public uint LastSeen;
    }

    private readonly SortedDictionary<string, ServerListEntry> _serverListItems = [];
    private readonly List<string> _expiredServers = [];
    private LANDiscoveryManager? _lanDiscovery;
    private bool _serverRunning;
}
