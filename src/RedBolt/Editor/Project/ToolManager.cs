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

namespace RedBolt.Editor.Project;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.SystemUI;

/// <summary>Manages third-party tools. Make it SettingsPage for simplicity.</summary>
public class ToolManager : SettingsPage
{
    public ToolManager()
    {
        _blenderPathHandle = GCHandle.Alloc(_blender.Path, GCHandleType.Normal);
        _fbx2glTFPathHandle = GCHandle.Alloc(_fbx2glTF.Path, GCHandleType.Normal);
        SubscribeToEvent<CoreEvents.UpdateEvent>((ref CoreEvents.UpdateEvent _) => Update());
    }

    public override void Dispose()
    {
        _fbx2glTFPathHandle.Free();
        _blenderPathHandle.Free();
        _notFoundPrompt.Dispose();
        _fbx2glTF.Dispose();
        _blender.Dispose();
        base.Dispose();
    }

    public void Update()
    {
        if (_blender.ScanPending || _blender.FirstScan)
            ScanBlender(_blender.FirstScan);
        if (_fbx2glTF.ScanPending || _fbx2glTF.FirstScan)
            ScanFBX2glTF(_fbx2glTF.FirstScan);
    }

    public bool HasBlender => _blender.Found;

    public string Blender => _blender.Path.ByteLength == 0 ? "blender" : _blender.Path.ConvertToString();

    public bool HasFBX2glTF => _fbx2glTF.Found;

    public string FBX2glTF => _fbx2glTF.Path.ByteLength == 0 ? "FBX2glTF" : _fbx2glTF.Path.ConvertToString();

    /// <summary>Implement SettingsPage.</summary>
    /// <inheritdoc />
    public override string UniqueName => "Editor.ExternalTools";

    public override bool IsSerializable => true;

    public override void SerializeInBlock(Archive archive)
    {
        if (!archive.IsInput)
        {
            if (_blender.FirstScan)
                ScanBlender(true);
            if (_fbx2glTF.FirstScan)
                ScanFBX2glTF(true);
        }

        ArchiveSerialization.SerializeOptionalValue(
            archive, "BlenderFound", ref _blender.Found, false, ArchiveSerialization.SerializeValue);
        var blenderPath = _blender.Path.ConvertToString();
        ArchiveSerialization.SerializeOptionalValue(
            archive, "BlenderPath", ref blenderPath, string.Empty, ArchiveSerialization.SerializeValue);
        if (archive.IsInput)
            _blender.Path.Set(blenderPath);
        ArchiveSerialization.SerializeOptionalValue(
            archive, "FBX2glTFFound", ref _fbx2glTF.Found, false, ArchiveSerialization.SerializeValue);
        var fbx2glTFPath = _fbx2glTF.Path.ConvertToString();
        ArchiveSerialization.SerializeOptionalValue(
            archive, "FBX2glTFPath", ref fbx2glTFPath, string.Empty, ArchiveSerialization.SerializeValue);
        if (archive.IsInput)
            _fbx2glTF.Path.Set(fbx2glTFPath);

        if (archive.IsInput)
        {
            _blender.FirstScan = false;
            _fbx2glTF.FirstScan = false;
        }
    }

    public override unsafe void RenderSettings()
    {
        ImGui.Text("Path to Blender executable (use system PATH if empty):"u8);
        RenderStatus(_blender.Found, _blender.Path, BlenderDownloadUrl);
        if (InputText("##BlenderPath"u8, _blender.Path, _blenderPathHandle))
            ScanBlender();

        ImGui.Separator();

        ImGui.Text("Path to FBX2glTF executable (use system PATH if empty):"u8);
        RenderStatus(_fbx2glTF.Found, _fbx2glTF.Path, FBX2glTFDownloadUrl);
        if (InputText("##FBX2glTFPath"u8, _fbx2glTF.Path, _fbx2glTFPathHandle))
            ScanFBX2glTF();

        ImGui.Separator();

        if (ImGui.Button("\uf021 Refresh All"u8))
        {
            ScanBlender();
            ScanFBX2glTF();
        }
    }

    private const uint ScanCooldownMs = 3000;

    private void ScanBlender(bool force = false)
    {
        _blender.ScanPending = true;
        if (!force && _blender.ScanTimer.GetMSec(false) < ScanCooldownMs)
            return;

        _blender.Found = ProcessUtils.SystemRun(Blender, BlenderArguments, out _) >= 0;

        _blender.ScanTimer.Reset();
        _blender.ScanPending = false;
        _blender.FirstScan = false;
    }

    private void ScanFBX2glTF(bool force = false)
    {
        _fbx2glTF.ScanPending = true;
        if (!force && _fbx2glTF.ScanTimer.GetMSec(false) < ScanCooldownMs)
            return;

        _fbx2glTF.Found = ProcessUtils.SystemRun(FBX2glTF, FBX2glTFArguments, out _) >= 0;

        _fbx2glTF.ScanTimer.Reset();
        _fbx2glTF.ScanPending = false;
        _fbx2glTF.FirstScan = false;
    }

    private void RenderStatus(bool found, CString path, ReadOnlySpan<byte> hint)
    {
        if (found)
        {
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText, new Vector4f(0.0f, 1.0f, 0.0f, 1.0f));
            try
            {
                ImGui.Text("\uf14a Tool is found and available"u8);
            }
            finally
            {
                ImGui.PopStyleColor();
            }
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.ImGuiColText, new Vector4f(1.0f, 0.0f, 0.0f, 1.0f));
            try
            {
                if (path.ByteLength != 0)
                {
                    _notFoundPrompt.FormatInPlace($"\uf071 Tool is not found by the path '{path}'");
                    ImGui.TextUnformatted(_notFoundPrompt.ContentBytes);
                }
                else
                    ImGui.Text("\uf071 Tool is not found in system PATH"u8);
            }
            finally
            {
                ImGui.PopStyleColor();
            }

            Widgets.TextURL("Download 3rdParty tool..."u8, hint);
        }
    }

    private static unsafe bool InputText(ReadOnlySpan<byte> label, CString value, GCHandle valueHandle)
    {
        var callback = (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, int>)&InputTextCallback;
        return ImGui.InputText(
            label,
            value.Pointer,
            (nuint)value.Capacity,
            ImGuiInputTextFlags.ImGuiInputTextFlagsCallbackResize,
            callback,
            GCHandle.ToIntPtr(valueHandle));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int InputTextCallback(IntPtr dataPointer)
    {
        using var data = ImGuiInputTextCallbackData.CreateManagedWrapper(dataPointer, ownsHandle: false);
        if (data.EventFlag == ImGuiInputTextFlags.ImGuiInputTextFlagsCallbackResize)
        {
            var value = (CString)GCHandle.FromIntPtr(data.UserData).Target!;
            Debug.Assert(data.Buf == value.Pointer);
            value.Resize(data.BufTextLen);
            data.Buf = value.Pointer;
        }
        return 0;
    }

    private sealed class BlenderState : IDisposable
    {
        public bool FirstScan = true;
        public bool Found;
        public readonly CString Path = new();

        public readonly Timer ScanTimer = new();
        public bool ScanPending;

        public void Dispose()
        {
            Path.Dispose();
        }
    }

    private sealed class FBX2glTFState : IDisposable
    {
        public bool FirstScan = true;
        public bool Found;
        public readonly CString Path = new();

        public readonly Timer ScanTimer = new();
        public bool ScanPending;

        public void Dispose()
        {
            Path.Dispose();
        }
    }

    private readonly BlenderState _blender = new();
    private readonly FBX2glTFState _fbx2glTF = new();

    private readonly CString _notFoundPrompt = new();
    private GCHandle _blenderPathHandle;
    private GCHandle _fbx2glTFPathHandle;

    private static ReadOnlySpan<byte> BlenderDownloadUrl => "https://www.blender.org/download/"u8;
    private static ReadOnlySpan<byte> FBX2glTFDownloadUrl => "https://github.com/godotengine/FBX2glTF/releases"u8;
    private static readonly string[] BlenderArguments = ["-b", "-noaudio", "--python-expr", "import bpy; bpy.ops.wm.quit_blender()"];
    private static readonly string[] FBX2glTFArguments = ["-h"];
}
