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

namespace RedBolt.Sample.Chat;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.DearImGui;
using RedBolt.Engine;
using RedBolt.IO;
using RedBolt.Input;
using RedBolt.Network;

internal static class ChatProgram
{
    private static int Main()
    {
        try
        {
            using var application = new Chat();
            return application.Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

/// <summary>Chat example.</summary>
public sealed class Chat : Application
{
    private const ushort ChatServerPort = 2345;
    private const int InputBufferSize = 1024;
    private static readonly NetworkMessageId ChatMessage =
        (NetworkMessageId)((uint)NetworkMessageId.User + 0);

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Chat";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowWidth = 1280;
            parameters.WindowHeight = 720;
#endif
        parameters.WindowTitle = "RedBolt Chat";
    }

    protected override void SetupCommandLine(CommandLineParser parser, EngineParameters parameters)
    {
        parser.AddFlag(["--start-server"], "Start the chat server on launch.",
            () => _startServerOnLaunch = true);
        parser.AddOption(["--connect"], "address", "Connect to a chat server on launch.",
            address => _connectOnLaunch = address);
    }

    protected override void Start()
    {
        base.Start();
        Context.RegisterSubsystem(new RedBolt.Network.Network());
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        _textBuffer = Marshal.AllocHGlobal(InputBufferSize);
        Marshal.WriteByte(_textBuffer, 0, 0);
        for (var i = 0; i < 30; ++i)
            _chatHistory.Add(string.Empty);
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => Draw());
        SubscribeToEvent<IOEvents.LogMessage>(HandleSampleLogMessage);
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);

        if (_startServerOnLaunch)
            StartServer();
        else if (!string.IsNullOrWhiteSpace(_connectOnLaunch))
            Connect(_connectOnLaunch);
    }

    protected override void Stop()
    {
        Disconnect();
        _clientConnection?.Dispose();
        _clientConnection = null;
        _server?.Dispose();
        _server = null;
        if (_textBuffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_textBuffer);
            _textBuffer = IntPtr.Zero;
        }
        base.Stop();
    }

    private void Draw()
    {
        ImGui.SetNextWindowSize(new Vector2f(800.0f, 500.0f),
            ImGuiCond.ImGuiCondFirstUseEver);
        var open = true;
        if (ImGui.Begin("Chat"u8, ref open, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            if (ImGui.BeginChild("Chat history"u8, new Vector2f(0.0f, 400.0f)))
            {
                foreach (var row in _chatHistory)
                    ImGui.TextUnformatted(Encoding.UTF8.GetBytes(row));
            }
            ImGui.EndChild();

            ImGui.InputText("Address / message"u8, _textBuffer, InputBufferSize);
            var clientConnected = _clientConnection?.IsConnected == true;
            var serverRunning = _server?.IsListening == true;
            if (clientConnected && ImGui.Button("Send"u8))
                Send();
            if (!clientConnected && !serverRunning && ImGui.Button("Connect"u8))
                Connect();
            if (!clientConnected && !serverRunning)
            {
#if !REDBOLT_PLATFORM_BROWSER
                    ImGui.SameLine();
                    if (ImGui.Button("Start Server"u8))
                        StartServer();
#endif
            }
            if ((clientConnected || serverRunning) && ImGui.Button("Disconnect"u8))
                Disconnect();
        }
        ImGui.End();
    }

    private void ShowChatText(string row)
    {
        if (_chatHistory.Count != 0)
            _chatHistory.RemoveAt(0);
        _chatHistory.Add(row);
    }

    private void HandleSampleLogMessage(ref IOEvents.LogMessage eventData) =>
        ShowChatText(eventData.Message);

    private void Send()
    {
        var text = ReadText();
        if (text.Length == 0)
            return;
        if (_clientConnection?.IsConnected == true)
        {
            using var data = new VectorBuffer();
            data.WriteString(text);
            using var message = new MemoryBuffer(data);
            _clientConnection.SendMessage(ChatMessage, message);
            ClearText();
        }
    }

    private void Connect(string? address = null)
    {
        address ??= ReadText().Trim();
        if (address.Length == 0)
            address = "localhost";
        ClearText();

        if (_clientConnection is null)
        {
            _clientConnection = new DataChannelConnection();
            _clientConnection.Connected += HandleClientConnected;
            _clientConnection.Disconnected += HandleClientDisconnected;
            _clientConnection.MessageReceived += HandleNetworkMessage;
        }
        if (!_clientConnection.IsDisconnected)
            return;
        _clientConnection.Connect(new URL(address.Contains("://", StringComparison.Ordinal)
            ? address
            : $"ws://{address}:{ChatServerPort}"));
    }

    private void Disconnect()
    {
        if (_clientConnection is not null && !_clientConnection.IsDisconnected)
            _clientConnection.Disconnect();
        else if (_server?.IsListening == true)
        {
            foreach (var weakConnection in _serverConnections)
            {
                if (weakConnection.TryGetTarget(out var connection))
                    connection.Disconnect();
            }
            _serverConnections.Clear();
            _server.Stop();
        }
    }

    private void StartServer()
    {
        if (_server is null)
        {
            _server = new DataChannelServer();
            _server.Connected += HandleServerConnected;
            _server.Disconnected += HandleServerDisconnected;
        }
        _server.Listen(new URL($"ws://0.0.0.0:{ChatServerPort}"));
    }

    private void HandleServerConnected(NetworkServer server, NetworkConnection connection)
    {
        _serverConnections.Add(new WeakReference<NetworkConnection>(connection));
        connection.MessageReceived += HandleNetworkMessage;
    }

    private void HandleServerDisconnected(NetworkServer server, NetworkConnection connection)
    {
        _serverConnections.RemoveAll(weak =>
            !weak.TryGetTarget(out var value) || ReferenceEquals(value, connection));
    }

    private void HandleClientConnected(NetworkConnection connection)
    {
    }

    private void HandleClientDisconnected(NetworkConnection connection)
    {
    }

    private void HandleNetworkMessage(NetworkConnection connection,
        NetworkMessageId messageId, MemoryBuffer message, ref bool handled)
    {
        if (messageId != ChatMessage)
            return;
        var text = message.ReadString();
        if (_server?.IsListening == true && IsServerConnection(connection))
        {
            var senderLabel = connection.Address.Length != 0 ? connection.Address : "Unknown";
            if (connection.Port != 0)
                senderLabel = $"{senderLabel}:{connection.Port}";
            text = senderLabel + " " + text;

            using var data = new VectorBuffer();
            data.WriteString(text);
            using var outgoing = new MemoryBuffer(data);
            foreach (var weakConnection in _serverConnections)
            {
                if (weakConnection.TryGetTarget(out var serverConnection))
                    serverConnection.SendMessage(ChatMessage, outgoing);
            }
        }
        ShowChatText(text);
        handled = true;
    }

    private bool IsServerConnection(NetworkConnection connection) =>
        _serverConnections.Any(weak =>
            weak.TryGetTarget(out var value) && ReferenceEquals(value, connection));

    private string ReadText() => Marshal.PtrToStringUTF8(_textBuffer) ?? string.Empty;
    private void ClearText() => Marshal.WriteByte(_textBuffer, 0, 0);

    private readonly List<string> _chatHistory = [];
    private IntPtr _textBuffer;
    private DataChannelServer? _server;
    private DataChannelConnection? _clientConnection;
    private readonly List<WeakReference<NetworkConnection>> _serverConnections = [];
    private bool _startServerOnLaunch;
    private string? _connectOnLaunch;
}
