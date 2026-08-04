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
using RedBolt.Platform;

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

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.VirtualFileSystem";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
        if (ProcessUtils.GetPlatform() != PlatformId.Web)
        {
            parameters.WindowWidth = 1280;
            parameters.WindowHeight = 720;
        }
        parameters.WindowTitle = "RedBolt Virtual File System";
    }

    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
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

        _mountPointLines.Add(ToUtf8("  MountPoints:"));
        for (var i = 0u; i < vfs.NumMountPoints(); ++i)
            _mountPointLines.Add(ToUtf8(vfs.GetMountPoint(i)!.GetName()));
    }

    private void RenderUi()
    {
        var vfs = Context.GetSubsystem<VirtualFileSystem>()!;

        ImGui.SetNextWindowSize(new Vector2f(550.0f, 500.0f), ImGuiCond.ImGuiCondFirstUseEver);
        ImGui.SetNextWindowPos(new Vector2f(350.0f, 50.0f), ImGuiCond.ImGuiCondFirstUseEver);
        var queryWindowOpen = true;
        if (ImGui.Begin("VFS Query Interface"u8, ref queryWindowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("URI: "u8);
            ImGui.SameLine();
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

            RenderValue("scheme: "u8, _schemeLine);
            RenderValue("path: "u8, _pathLine);
            RenderValue("canonical: "u8, _canonicalLine);
            RenderValue("exists: "u8, _exists ? "yes"u8 : "no"u8);
            RenderOptionalValue(
                "absolute path: "u8, _absoluteFileNameLine, _absoluteFileName.Length == 0, "[not found]"u8);
            RenderOptionalValue("file size: "u8, _fileSizeLine, _readOnlyFile is null, "[not found]"u8);
            RenderOptionalValue(
                "modification time: "u8, _modificationTimeLine, _modificationTime == 0, "[unknown]"u8);
            RenderOptionalValue(
                "reversed URI: "u8, _reversedUriLine, _reversedUri.Length == 0, "[not found]"u8);

            ImGui.Separator();

            var needScan = ImGui.IsWindowAppearing();

            ImGui.TextUnformatted("scheme:"u8);
            ImGui.SameLine();
            if (ImGui.InputText("##scanscheme"u8, _scanSchemeBuffer, InputBufferSize))
            {
                _scanPath.Scheme = ReadInputBuffer(_scanSchemeBuffer);
                needScan = true;
            }

            ImGui.TextUnformatted("path:"u8);
            ImGui.SameLine();
            if (ImGui.InputText("##scanpath"u8, _scanPathBuffer, InputBufferSize))
            {
                _scanPath.FileName = ReadInputBuffer(_scanPathBuffer);
                needScan = true;
            }

            ImGui.TextUnformatted("filter:"u8);
            ImGui.SameLine();
            if (ImGui.InputText("##scanfilter"u8, _scanFilterBuffer, InputBufferSize))
            {
                _scanFilter = ReadInputBuffer(_scanFilterBuffer);
                needScan = true;
            }

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

            RenderYellowText("scan results:"u8);
            if (ImGui.BeginListBox("##results"u8))
            {
                foreach (var result in _scanResultLines)
                    ImGui.Selectable(result, false);
                ImGui.EndListBox();
            }
        }
        ImGui.End();

        ImGui.SetNextWindowPos(new Vector2f(20.0f, 250.0f), ImGuiCond.ImGuiCondFirstUseEver);
        var mountPointsWindowOpen = true;
        if (ImGui.Begin("Mount Points"u8, ref mountPointsWindowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText, new Vector4f(0.0f, 1.0f, 0.0f, 1.0f));
            foreach (var line in _mountPointLines)
                ImGui.TextUnformatted(line);
            ImGui.PopStyleColor();
        }
        ImGui.End();
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
            Exit();
    }

    protected override void Stop()
    {
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
        RenderYellowText(label);
        ImGui.SameLine();
        ImGui.TextUnformatted(value);
    }

    private static void RenderOptionalValue(
        ReadOnlySpan<byte> label, ReadOnlySpan<byte> value, bool missing, ReadOnlySpan<byte> missingValue)
    {
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, new Vector4f(1.0f, 1.0f, 0.0f, 1.0f));
        ImGui.TextUnformatted(label);
        ImGui.SameLine();
        if (missing)
            ImGui.TextUnformatted(missingValue);
        ImGui.PopStyleColor();
        if (!missing)
            ImGui.TextUnformatted(value);
    }

    private static void RenderYellowText(ReadOnlySpan<byte> text)
    {
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, new Vector4f(1.0f, 1.0f, 0.0f, 1.0f));
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
}
