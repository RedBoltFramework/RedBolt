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

namespace RedBolt.SystemUI;

using System;
using System.Collections.Generic;
using System.Diagnostics;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;

/// <summary>Base class for drag&amp;drop payload.</summary>
public class DragDropPayload : IDisposable
{
    public delegate DragDropPayload CreateCallback();

    public static void Set(DragDropPayload payload)
    {
        Context.Instance.SetGlobalVar(DragDropPayloadVariable, Variant.MakeCustomValue(payload));
    }

    public static DragDropPayload? Get()
    {
        using var payload = ImGui.GetDragDropPayload();
        if (!payload.IsNull && payload.IsDataType(DragDropPayloadType))
        {
            using var variant = Context.Instance.GetGlobalVar(DragDropPayloadVariable).Clone();
            return variant.GetCustom<DragDropPayload>();
        }
        return null;
    }

    /// <summary>Call this function on every frame from drag source.</summary>
    public static void UpdateSource(CreateCallback createPayload)
    {
        ImGui.SetDragDropPayload(DragDropPayloadType, IntPtr.Zero, 0, ImGuiCond.ImGuiCondOnce);

        using (var nativePayload = ImGui.GetDragDropPayload())
        {
            if (nativePayload.Data == IntPtr.Zero)
            {
                var createdPayload = createPayload();
                Set(createdPayload);
                nativePayload.Data = new IntPtr(1);
            }
        }

        var payload = Get();
        Debug.Assert(payload is not null);
        ImGui.TextUnformatted(payload!.GetDisplayBytes());
    }

    /// <summary>Format string to display while dragging.</summary>
    public virtual string GetDisplayString() => "Drop me";

    public virtual void Dispose()
    {
        _displayString.Dispose();
        GC.SuppressFinalize(this);
    }

    private ReadOnlySpan<byte> GetDisplayBytes()
    {
        _displayString.Set(GetDisplayString());
        return _displayString.ContentBytes;
    }

    private static ReadOnlySpan<byte> DragDropPayloadType => "DragDropPayload"u8;
    private static readonly StringHash DragDropPayloadVariable = new("SystemUI_DragDropPayload");

    private readonly CString _displayString = new();
}

/// <summary>Resource file descriptor.</summary>
public class ResourceFileDescriptor : ICloneable
{
    /// <summary>Name without path.</summary>
    public string LocalName = string.Empty;
    /// <summary>File name relative to resource root.</summary>
    public string ResourceName = string.Empty;
    /// <summary>Absolute file name.</summary>
    public string FileName = string.Empty;

    /// <summary>Whether the file is a directory.</summary>
    public bool IsDirectory;
    /// <summary>Whether the file or folder is automatically managed, e.g. file is stored in the generated cache.</summary>
    public bool IsAutomatic;

    /// <summary>File type tags.</summary>
    public HashSet<string> TypeNames = [];
    public HashSet<StringHash> Types = [];

    public string MostDerivedType = string.Empty;

    public void AddObjectType(string typeName)
    {
        Types.Add(new StringHash(typeName));
        TypeNames.Add(typeName);
        MostDerivedType = typeName;
    }

    public bool HasObjectType(string typeName)
    {
        return TypeNames.Contains(typeName);
    }

    public bool HasObjectType(StringHash type)
    {
        return Types.Contains(type);
    }

    public void AddObjectType<T>()
    {
        AddObjectType(typeof(T).GetClassName());
    }

    public bool HasObjectType<T>()
    {
        return HasObjectType(typeof(T).GetClassName());
    }

    public bool HasExtension(string extension)
    {
        return LocalName.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
    }

    public bool HasExtension(ReadOnlySpan<string> extensions)
    {
        foreach (var extension in extensions)
        {
            if (HasExtension(extension))
                return true;
        }
        return false;
    }

    public ResourceFileDescriptor Clone()
    {
        return new ResourceFileDescriptor
        {
            LocalName = LocalName,
            ResourceName = ResourceName,
            FileName = FileName,
            IsDirectory = IsDirectory,
            IsAutomatic = IsAutomatic,
            TypeNames = [.. TypeNames],
            Types = [.. Types],
            MostDerivedType = MostDerivedType,
        };
    }

    object ICloneable.Clone() => Clone();
}

/// <summary>Drag&amp;drop payload containing reference to a resource or directory.</summary>
public class ResourceDragDropPayload : DragDropPayload
{
    public override string GetDisplayString()
    {
        return Resources.Count == 1
            ? Resources[0].LocalName
            : $"{Resources.Count} items";
    }

    public List<ResourceFileDescriptor> Resources = [];

    public override void Dispose()
    {
        Resources.Clear();
        base.Dispose();
    }
}
