// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Project;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Utility;

/// <summary>Interface of file and folder factory.</summary>
public abstract class ResourceFactory : RedObject
{
    public delegate (bool IsValid, string ExtraLine) FileNameChecker(string filePath, string fileName);

    public ResourceFactory(int group, string title)
    {
        _group = group;
        _title = title;
    }

    public virtual bool IsEnabled(FileSystemEntry parentEntry) => true;

    public abstract void Open(string baseFilePath, string baseResourcePath);

    public abstract void Render(FileNameChecker checker, ref bool canCommit, ref bool shouldCommit);

    public abstract void CommitAndClose();

    public virtual void DiscardAndClose()
    {
    }

    public int Group => _group;

    public string Title => _title;

    /// <summary>Order factories by group and name, usually for menu rendering.</summary>
    public static bool Compare(ResourceFactory lhs, ResourceFactory rhs)
    {
        return lhs._group < rhs._group
            || (lhs._group == rhs._group && string.CompareOrdinal(lhs._title, rhs._title) < 0);
    }

    private readonly int _group;
    private readonly string _title;
}

/// <summary>Base implementation of ResourceFactory.</summary>
public abstract class BaseResourceFactory : ResourceFactory
{
    public BaseResourceFactory(int group, string title)
        : base(group, title)
    {
        _localFileNameInputHandle = GCHandle.Alloc(_localFileNameInput);
        _newResourcePathInputHandle = GCHandle.Alloc(_newResourcePathInput);
    }

    public abstract string DefaultFileName { get; }

    public virtual bool IsFileNameEditable => true;

    public virtual void RenderAuxilary()
    {
    }

    /// <summary>Implement ResourceFactory.</summary>
    public override void Open(string baseFilePath, string baseResourcePath)
    {
        _baseFilePath = AddTrailingSlash(baseFilePath);
        _baseResourcePath = AddTrailingSlash(baseResourcePath);
        _localFileName = DefaultFileName;
        _localFileNameInput.Set(_localFileName);

        _selectFileNameInput = IsFileNameEditable;
    }

    public override unsafe void Render(FileNameChecker checker, ref bool canCommit, ref bool shouldCommit)
    {
        var (isFileNameValid, extraLine) = checker(_baseFilePath, _localFileName);
        var fileName = _baseFilePath + _localFileName;
        var resourceName = _baseResourcePath + _localFileName;
        _prompt.FormatInPlace($"Would you like to create '{resourceName}'?\n{extraLine}");
        ImGui.Text(_prompt.ContentBytes);

        if (_selectFileNameInput)
            ImGui.SetKeyboardFocusHere();
        ImGui.BeginDisabled(!IsFileNameEditable);
        var callback = (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, int>)&InputTextCallback;
        var isEnterPressed = ImGui.InputText(
            "##FileName"u8,
            _localFileNameInput.Pointer,
            (nuint)_localFileNameInput.Capacity,
            ImGuiInputTextFlags.ImGuiInputTextFlagsAutoSelectAll
                | ImGuiInputTextFlags.ImGuiInputTextFlagsEnterReturnsTrue
                | ImGuiInputTextFlags.ImGuiInputTextFlagsCallbackResize,
            callback,
            GCHandle.ToIntPtr(_localFileNameInputHandle));
        if (!_localFileNameInput.Equals(_localFileName))
            _localFileName = _localFileNameInput.ConvertToString();
        ImGui.EndDisabled();

        _newResourcePath = RemoveTrailingSlash(_baseResourcePath);
        _newResourcePathInput.Set(_newResourcePath);
        ImGui.Text("\uf07b in folder:"u8);
        if (ImGui.InputText(
                "##ResourcePath"u8,
                _newResourcePathInput.Pointer,
                (nuint)_newResourcePathInput.Capacity,
                ImGuiInputTextFlags.ImGuiInputTextFlagsCallbackResize,
                callback,
                GCHandle.ToIntPtr(_newResourcePathInputHandle)))
        {
            _newResourcePath = _newResourcePathInput.ConvertToString();
            Debug.Assert(_baseFilePath.EndsWith(_baseResourcePath, StringComparison.Ordinal));
            _baseFilePath = _baseFilePath[..^_baseResourcePath.Length];
            _baseResourcePath = AddTrailingSlash(_newResourcePath);
            _baseFilePath += _baseResourcePath;
        }

        RenderAuxilary();

        _selectFileNameInput = false;

        canCommit = isFileNameValid;
        shouldCommit = isEnterPressed;
    }

    public override void CommitAndClose()
    {
    }

    protected string FinalFilePath => _baseFilePath;

    protected string FinalResourcePath => _baseResourcePath;

    protected string FinalFileName => _baseFilePath + _localFileName;

    protected string FinalResourceName => _baseResourcePath + _localFileName;

    public override void Dispose()
    {
        _newResourcePathInputHandle.Free();
        _localFileNameInputHandle.Free();
        _prompt.Dispose();
        _newResourcePathInput.Dispose();
        _localFileNameInput.Dispose();
        base.Dispose();
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

    private static string AddTrailingSlash(string pathName)
    {
        var result = pathName.Trim().Replace('\\', '/');
        if (result.Length != 0 && result[^1] != '/')
            result += '/';
        return result;
    }

    private static string RemoveTrailingSlash(string pathName)
    {
        var result = pathName.Trim().Replace('\\', '/');
        if (result.Length != 0 && result[^1] == '/')
            result = result[..^1];
        return result;
    }

    private string _baseFilePath = string.Empty;
    private string _baseResourcePath = string.Empty;

    private string _localFileName = string.Empty;
    private string _newResourcePath = string.Empty;

    private bool _selectFileNameInput;

    private readonly CString _localFileNameInput = new();
    private readonly CString _newResourcePathInput = new();
    private readonly CString _prompt = new();
    private GCHandle _localFileNameInputHandle;
    private GCHandle _newResourcePathInputHandle;
}

/// <summary>Simple implementation of ResourceFactory.</summary>
public class SimpleResourceFactory : BaseResourceFactory
{
    public delegate void Callback(string fileName, string resourceName);

    public SimpleResourceFactory(int group, string title, string fileName, Callback callback)
        : base(group, title)
    {
        _fileName = fileName;
        _callback = callback;

        Debug.Assert(_callback is not null);
    }

    /// <summary>Overridable interface.</summary>
    public override string DefaultFileName => _fileName;

    public override void CommitAndClose()
    {
        _callback!(FinalFileName, FinalResourceName);
    }

    public override void Dispose()
    {
        _callback = null;
        base.Dispose();
    }

    private readonly string _fileName;
    private Callback? _callback;
}
