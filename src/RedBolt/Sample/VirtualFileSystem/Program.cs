// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.VirtualFileSystem;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.DearImGui;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.IO;
using RedBolt.Input;

internal static class VirtualFileSystemProgram
{
    private static int Main(string[] args)
    {
        return new VirtualFileSystemApplication().Run(args);
    }
}

/// <summary>Virtual file system example.</summary>
sealed class VirtualFileSystemApplication : Application
{
    private const int InputBufferSize = 512;

    private string _uri = "Models/Box.mdl";
    private FileIdentifier _fileIdentifier;
    private FileIdentifier _canonicalForm;
    private bool _exists;
    private string _absoluteFileName = string.Empty;
    private AbstractFile? _readOnlyFile;
    private uint _modificationTime;
    private string _reversedUri = string.Empty;
    private FileIdentifier _scanPath = new(string.Empty, "Materials");
    private string _scanFilter = "*.*";
    private bool _scanRecursive = true;
    private bool _scanFiles = true;
    private bool _scanDirectories;
    private readonly List<string> _scanResults = [];

    private readonly List<byte[]> _mountPointLines = [];
    private readonly List<byte[]> _scanResultLines = [];
    private IntPtr _uriBuffer;
    private IntPtr _scanSchemeBuffer;
    private IntPtr _scanPathBuffer;
    private IntPtr _scanFilterBuffer;
    private byte[] _schemeLine = [];
    private byte[] _pathLine = [];
    private byte[] _canonicalLine = [];
    private byte[] _absoluteFileNameLine = [];
    private byte[] _fileSizeLine = [];
    private byte[] _modificationTimeLine = [];
    private byte[] _reversedUriLine = [];
    private ImFont? _headingFont;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.VirtualFileSystem";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowWidth = 1280;
            parameters.WindowHeight = 720;
#endif
        parameters.WindowTitle = "RedBolt Virtual File System";
    }

    protected override void Start()
    {
        _sampleInput = new SampleInputMap();
        var dearImGui = new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags);
        Context.RegisterSubsystem(dearImGui);
        _headingFont = dearImGui.AddFont("Fonts/Anonymous Pro.ttf", size: 28.0f);
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => RenderUi());
        AllocateInputBuffers();
        CreateText();
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    private void CreateText()
    {
        var vfs = Context.GetSubsystem<VirtualFileSystem>()!;

        for (var i = 0u; i < vfs.NumMountPoints(); ++i)
            _mountPointLines.Add(ToUtf8(vfs.GetMountPoint(i)!.GetName()));
    }

    private void RenderUi()
    {
        var vfs = Context.GetSubsystem<VirtualFileSystem>()!;

        Vector2f viewportPosition;
        Vector2f viewportSize;
        uint viewportId;
        using (var viewport = ImGui.GetMainViewport())
        {
            viewportPosition = viewport.Pos;
            viewportSize = viewport.Size;
            viewportId = viewport.Id;
        }

        ImGui.SetNextWindowPos(viewportPosition);
        ImGui.SetNextWindowSize(viewportSize);
        ImGui.SetNextWindowViewport(viewportId);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowRounding, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowBorderSize, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowPadding, Vector2f.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarFrameRounding, 6.0f);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColWindowBg, DashboardBackground);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColFrameBg, ControlBackground);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColFrameBgHovered, ControlHovered);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColFrameBgActive, ControlActive);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColHeader, ControlBackground);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColHeaderHovered, ControlHovered);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColHeaderActive, ControlActive);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColCheckMark, AccentCyan);
        var queryWindowOpen = true;
        if (ImGui.Begin(
                "Virtual File System"u8,
                ref queryWindowOpen,
                ImGuiWindowFlags.ImGuiWindowFlagsNoDecoration |
                ImGuiWindowFlags.ImGuiWindowFlagsNoMove |
                ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings |
                ImGuiWindowFlags.ImGuiWindowFlagsNoBringToFrontOnFocus))
        {
            using (var drawList = ImGui.GetWindowDrawList())
                drawList.AddRectFilledMultiColor(viewportPosition, viewportPosition + viewportSize,
                    ImGui.GetColorU32(DashboardBackground),
                    ImGui.GetColorU32(new Vector4f(0.025f, 0.115f, 0.15f, 1.0f)),
                    ImGui.GetColorU32(new Vector4f(0.018f, 0.035f, 0.07f, 1.0f)),
                    ImGui.GetColorU32(new Vector4f(0.035f, 0.025f, 0.105f, 1.0f)));

            var compact = viewportSize.X < 900.0f;
            var margin = compact ? 14.0f : 26.0f;
            var headerHeight = compact ? 58.0f : 68.0f;
            var contentWidth = Math.Max(1.0f, viewportSize.X - margin * 2.0f);
            DrawHeader(viewportPosition + new Vector2f(margin, margin), contentWidth, compact);
            BeginPanel(
                "##Workspace"u8,
                viewportPosition + new Vector2f(margin, margin + headerHeight),
                new Vector2f(contentWidth, Math.Max(1.0f, viewportSize.Y - margin * 2.0f - headerHeight)));

            ImGui.SeparatorText("Mount points"u8);
            if (ImGui.BeginChild("##MountPoints"u8, new Vector2f(0.0f, 54.0f), ImGuiChildFlags.ImGuiChildFlagsBorders))
            {
                foreach (var mountPoint in _mountPointLines)
                    ImGui.TextUnformatted(mountPoint);
            }
            ImGui.EndChild();

            ImGui.SeparatorText("File query"u8);
            ImGui.SetNextItemWidth(-1.0f);
            if (ImGui.InputText("##uri"u8, _uriBuffer, InputBufferSize) || ImGui.IsWindowAppearing())
            {
                _uri = ReadInputBuffer(_uriBuffer);
                _fileIdentifier = FileIdentifier.FromUri(_uri);
                _canonicalForm = vfs.GetCanonicalIdentifier(_fileIdentifier);
                _exists = vfs.Exists(_fileIdentifier);
                _absoluteFileName = vfs.GetAbsoluteNameFromIdentifier(_fileIdentifier);
                _readOnlyFile?.Close();
                _readOnlyFile = vfs.OpenFile(_fileIdentifier, FileMode.Read);
                _modificationTime = vfs.GetLastModifiedTime(_fileIdentifier, true);
                _reversedUri = vfs.GetIdentifierFromAbsoluteName(_absoluteFileName).ToUri();
                UpdateQueryLines();
            }

            ImGui.PushStyleColor(ImGuiCol.ImGuiColChildBg, new Vector4f(0.055f, 0.065f, 0.085f, 1.0f));
            if (ImGui.BeginChild("##QueryDetails"u8, new Vector2f(0.0f, 172.0f), ImGuiChildFlags.ImGuiChildFlagsBorders))
            {
                RenderValue("Scheme"u8, _schemeLine);
                RenderValue("Path"u8, _pathLine);
                RenderValue("Canonical URI"u8, _canonicalLine);
                RenderStatusValue("Status"u8, _exists, "Available"u8, "Not found"u8);
                RenderOptionalValue(
                    "Absolute path"u8, _absoluteFileNameLine, _absoluteFileName.Length == 0, "Not available"u8);
                RenderOptionalValue("File size"u8, _fileSizeLine, _readOnlyFile is null, "Not available"u8);
                RenderOptionalValue(
                    "Modified"u8, _modificationTimeLine, _modificationTime == 0, "Unknown"u8);
                RenderOptionalValue(
                    "Reverse lookup"u8, _reversedUriLine, _reversedUri.Length == 0, "Not available"u8);
            }
            ImGui.EndChild();
            ImGui.PopStyleColor();

            var needScan = ImGui.IsWindowAppearing();

            ImGui.SeparatorText("Resource scan"u8);
            ImGui.SetNextItemWidth(165.0f);
            if (ImGui.InputText("##scanscheme"u8, _scanSchemeBuffer, InputBufferSize))
            {
                _scanPath.Scheme = ReadInputBuffer(_scanSchemeBuffer);
                needScan = true;
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(285.0f);
            if (ImGui.InputText("##scanpath"u8, _scanPathBuffer, InputBufferSize))
            {
                _scanPath.FileName = ReadInputBuffer(_scanPathBuffer);
                needScan = true;
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1.0f);
            if (ImGui.InputText("##scanfilter"u8, _scanFilterBuffer, InputBufferSize))
            {
                _scanFilter = ReadInputBuffer(_scanFilterBuffer);
                needScan = true;
            }
            RenderInputCaptions();

            if (ImGui.Checkbox("Recursive"u8, ref _scanRecursive))
                needScan = true;
            ImGui.SameLine();
            if (ImGui.Checkbox("Files"u8, ref _scanFiles))
                needScan = true;
            ImGui.SameLine();
            if (ImGui.Checkbox("Directories"u8, ref _scanDirectories))
                needScan = true;

            if (needScan)
            {
                var scanFlags = (ScanFlags)0;
                if (_scanRecursive)
                    scanFlags |= ScanFlags.ScanRecursive;
                if (_scanFiles)
                    scanFlags |= ScanFlags.ScanFiles;
                if (_scanDirectories)
                    scanFlags |= ScanFlags.ScanDirs;
                vfs.Scan(_scanResults, _scanPath, _scanFilter, scanFlags);
                UpdateScanResultLines();
            }

            ImGui.SameLine();
            if (_scanResultLines.Count == 0)
                RenderColoredText(new Vector4f(1.0f, 0.65f, 0.3f, 1.0f), "No matches"u8);
            else
                RenderColoredText(
                    new Vector4f(0.35f, 0.9f, 0.55f, 1.0f),
                    ToUtf8($"{_scanResultLines.Count} result{(_scanResultLines.Count == 1 ? string.Empty : "s")}"));

            if (ImGui.BeginChild("##ScanResults"u8, new Vector2f(0.0f, 0.0f), ImGuiChildFlags.ImGuiChildFlagsBorders))
            {
                foreach (var result in _scanResultLines)
                    ImGui.Selectable(result, false);
            }
            ImGui.EndChild();
            EndPanel();
        }
        ImGui.End();
        ImGui.PopStyleColor(8);
        ImGui.PopStyleVar(4);
    }

    private void DrawHeader(Vector2f position, float width, bool compact)
    {
        ImGui.SetCursorScreenPos(position);
        if (_headingFont is not null)
            ImGui.PushFont(_headingFont, compact ? 24.0f : 30.0f);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, PrimaryText);
        ImGui.TextUnformatted("VIRTUAL FILE EXPLORER"u8);
        ImGui.PopStyleColor();
        if (_headingFont is not null)
            ImGui.PopFont();
        ImGui.SetCursorScreenPos(position + new Vector2f(0.0f, compact ? 29.0f : 35.0f));
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, MutedText);
        ImGui.TextUnformatted("Resolve identifiers, inspect canonical paths, and scan every mounted source."u8);
        ImGui.PopStyleColor();
        if (!compact)
        {
            var badge = position + new Vector2f(width - 168.0f, 4.0f);
            using (var drawList = ImGui.GetWindowDrawList())
            {
                drawList.AddRectFilled(badge, badge + new Vector2f(168.0f, 34.0f),
                    ImGui.GetColorU32(new Vector4f(0.035f, 0.18f, 0.18f, 0.9f)), 17.0f);
                drawList.AddCircleFilled(badge + new Vector2f(17.0f, 17.0f), 4.0f, ImGui.GetColorU32(AccentCyan));
            }
            ImGui.SetCursorScreenPos(badge + new Vector2f(30.0f, 8.0f));
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText, AccentCyan);
            ImGui.TextUnformatted("MOUNTS ONLINE"u8);
            ImGui.PopStyleColor();
        }
    }

    private static void BeginPanel(ReadOnlySpan<byte> id, Vector2f position, Vector2f size)
    {
        ImGui.SetCursorScreenPos(position);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColChildBg, PanelBackground);
        ImGui.PushStyleColor(ImGuiCol.ImGuiColBorder, PanelBorder);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarChildRounding, 12.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarChildBorderSize, 1.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ImGuiStyleVarWindowPadding, new Vector2f(16.0f, 12.0f));
        ImGui.BeginChild(id, size, ImGuiChildFlags.ImGuiChildFlagsBorders);
    }

    private static void EndPanel()
    {
        ImGui.EndChild();
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(2);
    }

    private void RenderInputCaptions()
    {
        RenderColoredText(new Vector4f(0.55f, 0.6f, 0.68f, 1.0f), "Scheme"u8);
        ImGui.SameLine(173.0f);
        RenderColoredText(new Vector4f(0.55f, 0.6f, 0.68f, 1.0f), "Path"u8);
        ImGui.SameLine(466.0f);
        RenderColoredText(new Vector4f(0.55f, 0.6f, 0.68f, 1.0f), "Filter"u8);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _sampleInput!.Pressed("Exit"))
            Exit();
    }

    protected override void Stop()
    {
        _sampleInput?.Dispose();
        _sampleInput = null;
        _readOnlyFile?.Close();
        FreeInputBuffer(ref _uriBuffer);
        FreeInputBuffer(ref _scanSchemeBuffer);
        FreeInputBuffer(ref _scanPathBuffer);
        FreeInputBuffer(ref _scanFilterBuffer);
    }

    private void AllocateInputBuffers()
    {
        _uriBuffer = AllocateInputBuffer(_uri);
        _scanSchemeBuffer = AllocateInputBuffer(_scanPath.Scheme);
        _scanPathBuffer = AllocateInputBuffer(_scanPath.FileName);
        _scanFilterBuffer = AllocateInputBuffer(_scanFilter);
    }

    private void UpdateQueryLines()
    {
        _schemeLine = ToUtf8(_fileIdentifier.Scheme);
        _pathLine = ToUtf8(_fileIdentifier.FileName);
        _canonicalLine = ToUtf8(_canonicalForm.ToUri());
        _absoluteFileNameLine = ToUtf8(_absoluteFileName);
        _fileSizeLine = ToUtf8(_readOnlyFile?.Size.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        if (_modificationTime != 0)
        {
            // TODO: move to common place?
            _modificationTimeLine = ToUtf8(
                DateTimeOffset.FromUnixTimeSeconds(_modificationTime).LocalDateTime
                    .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }
        else
            _modificationTimeLine = [];
        _reversedUriLine = ToUtf8(_reversedUri);
    }

    private void UpdateScanResultLines()
    {
        _scanResultLines.Clear();
        foreach (var scanResult in _scanResults)
        {
            var result = _scanPath + scanResult;
            _scanResultLines.Add(ToUtf8(result.FileName));
        }
    }

    private static void RenderValue(ReadOnlySpan<byte> label, ReadOnlySpan<byte> value)
    {
        RenderLabel(label);
        ImGui.SameLine();
        ImGui.TextUnformatted(value);
    }

    private static void RenderOptionalValue(
        ReadOnlySpan<byte> label, ReadOnlySpan<byte> value, bool missing, ReadOnlySpan<byte> missingValue)
    {
        RenderLabel(label);
        ImGui.SameLine();
        if (missing)
        {
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText, new Vector4f(0.95f, 0.4f, 0.4f, 1.0f));
            ImGui.TextUnformatted(missingValue);
            ImGui.PopStyleColor();
        }
        else
            ImGui.TextUnformatted(value);
    }

    private static void RenderStatusValue(
        ReadOnlySpan<byte> label,
        bool success,
        ReadOnlySpan<byte> successValue,
        ReadOnlySpan<byte> failureValue)
    {
        RenderLabel(label);
        ImGui.SameLine();
        RenderColoredText(
            success ? new Vector4f(0.35f, 0.9f, 0.55f, 1.0f) : new Vector4f(0.95f, 0.4f, 0.4f, 1.0f),
            success ? successValue : failureValue);
    }

    private static void RenderLabel(ReadOnlySpan<byte> text)
    {
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, new Vector4f(0.4f, 0.75f, 1.0f, 1.0f));
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    private static void RenderColoredText(Vector4f color, ReadOnlySpan<byte> text)
    {
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    private static IntPtr AllocateInputBuffer(string value)
    {
        var buffer = Marshal.AllocHGlobal(InputBufferSize);
        var bytes = Encoding.UTF8.GetBytes(value);
        Marshal.Copy(bytes, 0, buffer, Math.Min(bytes.Length, InputBufferSize - 1));
        Marshal.WriteByte(buffer, Math.Min(bytes.Length, InputBufferSize - 1), 0);
        return buffer;
    }

    private static string ReadInputBuffer(IntPtr buffer)
    {
        return Marshal.PtrToStringUTF8(buffer) ?? string.Empty;
    }

    private static void FreeInputBuffer(ref IntPtr buffer)
    {
        if (buffer == IntPtr.Zero)
            return;
        Marshal.FreeHGlobal(buffer);
        buffer = IntPtr.Zero;
    }

    private static byte[] ToUtf8(string value)
    {
        return Encoding.UTF8.GetBytes(value);
    }

    private static readonly Vector4f DashboardBackground = new(0.014f, 0.025f, 0.055f, 1.0f);
    private static readonly Vector4f PanelBackground = new(0.043f, 0.06f, 0.105f, 0.96f);
    private static readonly Vector4f PanelBorder = new(0.16f, 0.27f, 0.4f, 0.85f);
    private static readonly Vector4f ControlBackground = new(0.075f, 0.12f, 0.18f, 1.0f);
    private static readonly Vector4f ControlHovered = new(0.095f, 0.22f, 0.28f, 1.0f);
    private static readonly Vector4f ControlActive = new(0.08f, 0.34f, 0.4f, 1.0f);
    private static readonly Vector4f PrimaryText = new(0.92f, 0.97f, 1.0f, 1.0f);
    private static readonly Vector4f MutedText = new(0.52f, 0.62f, 0.72f, 1.0f);
    private static readonly Vector4f AccentCyan = new(0.15f, 0.93f, 1.0f, 1.0f);
    private SampleInputMap? _sampleInput;
}
