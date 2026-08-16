// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.SystemUI;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Scene;

public struct AttributeHookContext
{
    public IReadOnlyList<WeakReference<Serializable>?>? Objects;
    public AttributeInfo? Info;
    public bool IsUndefined;
    public bool IsDefaultValue;
}

public enum ObjectHookType
{
    /// <summary>Rendered before default attributes rendering.</summary>
    Prepend,
    /// <summary>Rendered after default attributes rendering.</summary>
    Append,
    /// <summary>Rendered instead of default attributes rendering.</summary>
    Replace,
}

public delegate bool AttributeHookFunction(in AttributeHookContext context, ref Variant boxedValue);
public delegate void ObjectHookFunction(IReadOnlyList<WeakReference<Serializable>?> objects);

file static class SerializableInspectorHooks
{
    public static readonly Dictionary<(string ClassName, string AttributeName), AttributeHookFunction>
        AttributeHooks = [];
    public static readonly Dictionary<(string ClassName, ObjectHookType Type), ObjectHookFunction>
        ObjectHooks = [];
}

/// <summary>SystemUI widget used to edit materials.</summary>
public class SerializableInspectorWidget : RedObject
{
    /// <summary>Hooks used to customize attribute rendering.</summary>
    public static void RegisterAttributeHook(
        (string ClassName, string AttributeName) key, AttributeHookFunction function)
    {
        SerializableInspectorHooks.AttributeHooks[key] = function;
    }

    public static void UnregisterAttributeHook((string ClassName, string AttributeName) key)
    {
        SerializableInspectorHooks.AttributeHooks.Remove(key);
    }

    public static AttributeHookFunction? GetAttributeHook((string ClassName, string AttributeName) key)
    {
        return SerializableInspectorHooks.AttributeHooks.GetValueOrDefault(key);
    }

    public static void CopyAttributeHook(
        (string ClassName, string AttributeName) from, (string ClassName, string AttributeName) to)
    {
        var hook = GetAttributeHook(from);
        if (hook is not null)
            RegisterAttributeHook(to, hook);
    }

    /// <summary>Hooks used to customize object rendering.</summary>
    /// <remarks>TODO: Object hooks cannot change object's attributes (yet)</remarks>
    public static void RegisterObjectHook(
        (string ClassName, ObjectHookType Type) key, ObjectHookFunction function)
    {
        SerializableInspectorHooks.ObjectHooks[key] = function;
    }

    public static void UnregisterObjectHook((string ClassName, ObjectHookType Type) key)
    {
        SerializableInspectorHooks.ObjectHooks.Remove(key);
    }

    public static ObjectHookFunction? GetObjectHook((string ClassName, ObjectHookType Type) key)
    {
        return SerializableInspectorHooks.ObjectHooks.GetValueOrDefault(key);
    }

    public static void CopyObjectHook(
        (string ClassName, ObjectHookType Type) from, (string ClassName, ObjectHookType Type) to)
    {
        var hook = GetObjectHook(from);
        if (hook is not null)
            RegisterObjectHook(to, hook);
    }

    /// <summary>Template syntax sugar for hooks.</summary>
    public static void RegisterAttributeHook<T>(string name, AttributeHookFunction function)
        where T : Serializable
    {
        RegisterAttributeHook((typeof(T).GetClassName(), name), function);
    }

    public static void UnregisterAttributeHook<T>(string name)
        where T : Serializable
    {
        UnregisterAttributeHook((typeof(T).GetClassName(), name));
    }

    public static void CopyAttributeHook<T, U>(string name)
        where T : Serializable
        where U : Serializable
    {
        CopyAttributeHook((typeof(T).GetClassName(), name), (typeof(U).GetClassName(), name));
    }

    public static void RegisterObjectHook<T>(ObjectHookType type, ObjectHookFunction function)
        where T : Serializable
    {
        RegisterObjectHook((typeof(T).GetClassName(), type), function);
    }

    public static void UnregisterObjectHook<T>()
        where T : Serializable
    {
        foreach (var type in new[] { ObjectHookType.Prepend, ObjectHookType.Append, ObjectHookType.Replace })
            UnregisterObjectHook((typeof(T).GetClassName(), type));
    }

    public static void CopyObjectHook<T, U>()
        where T : Serializable
        where U : Serializable
    {
        foreach (var type in new[] { ObjectHookType.Prepend, ObjectHookType.Append, ObjectHookType.Replace })
            CopyObjectHook((typeof(T).GetClassName(), type), (typeof(U).GetClassName(), type));
    }

    public event Action<IReadOnlyList<WeakReference<Serializable>?>, AttributeInfo?>? OnEditAttributeBegin;
    public event Action<IReadOnlyList<WeakReference<Serializable>?>, AttributeInfo?>? OnEditAttributeEnd;
    public event Action<IReadOnlyList<WeakReference<Serializable>?>>? OnActionBegin;
    public event Action<IReadOnlyList<WeakReference<Serializable>?>>? OnActionEnd;

    public SerializableInspectorWidget(IReadOnlyList<WeakReference<Serializable>?> objects)
    {
        ArgumentNullException.ThrowIfNull(objects);
        foreach (var reference in objects)
        {
            _objects.Add(reference is not null && reference.TryGetTarget(out var target)
                ? new WeakReference<Serializable>(target)
                : null);
        }
        Debug.Assert(_objects.Count != 0);
    }

    public override void Dispose()
    {
        _textBuffer.Dispose();
        _pendingActions.Clear();
        foreach (var pendingAttribute in _pendingSetAttributes)
        {
            var value = pendingAttribute.Value;
            value.Dispose();
        }
        _pendingSetAttributes.Clear();
        _objects.Clear();
        OnActionEnd = null;
        OnActionBegin = null;
        OnEditAttributeEnd = null;
        OnEditAttributeBegin = null;
        base.Dispose();
    }

    public void RenderTitle()
    {
        _textBuffer.Set(GetTitle());
        ImGui.TextUnformatted(_textBuffer.ContentBytes);
    }

    public void RenderContent()
    {
        PruneObjects();
        if (_objects.Count == 0)
            return;

        _objects[0]!.TryGetTarget(out var firstObject);
        var objectType = firstObject!.GetTypeName();
        var prependHook = GetObjectHook((objectType, ObjectHookType.Prepend));
        var appendHook = GetObjectHook((objectType, ObjectHookType.Append));
        var replaceHook = GetObjectHook((objectType, ObjectHookType.Replace));

        prependHook?.Invoke(_objects);

        if (replaceHook is not null)
            replaceHook(_objects);
        else
            RenderObjects();

        appendHook?.Invoke(_objects);
    }

    public string GetTitle()
    {
        PruneObjects();
        if (_objects.Count == 0)
            return "Nothing selected";

        var extras = new StringBuilder();
        foreach (var reference in _objects)
        {
            reference!.TryGetTarget(out var target);
            uint? id = null;

            // TODO(editor): This is a hack to get IDs
            if (target is Node node)
                id = node.GetID();
            else if (target is Component component)
                id = component.Id;

            if (id.HasValue)
            {
                if (extras.Length != 0)
                    extras.Append(", ");
                extras.Append(id.Value);
            }
        }

        _objects[0]!.TryGetTarget(out var firstObject);
        if (_objects.Count == 1)
        {
            if (extras.Length == 0)
                return $"{firstObject!.GetTypeName()}";
            else
                return $"{firstObject!.GetTypeName()} ({extras})";
        }
        else
        {
            if (extras.Length == 0)
                return $"{_objects.Count}x {firstObject!.GetTypeName()}";
            else
                return $"{_objects.Count}x {firstObject!.GetTypeName()} ({extras})";
        }
    }

    public IReadOnlyList<WeakReference<Serializable>?> GetObjects() => _objects;

    private void PruneObjects()
    {
        for (var index = _objects.Count - 1; index >= 0; --index)
        {
            if (_objects[index] is null || !_objects[index]!.TryGetTarget(out _))
                _objects.RemoveAt(index);
        }
    }

    private void RenderAttribute(AttributeInfo info)
    {
        _objects[0]!.TryGetTarget(out var firstObject);
        var hook = GetAttributeHook((firstObject!.GetTypeName(), info.Name));

        if (hook is null && info.GetMetadata(AttributeMetadata.IsAction).GetBool())
        {
            RenderAction(info);
            return;
        }

        var value = default(Variant);
        var tempValue = default(Variant);
        try
        {
            info.Accessor!.Get(firstObject, ref value);

            var isUndefined = false;
            for (var index = 1; index < _objects.Count; ++index)
            {
                tempValue.Dispose();
                _objects[index]!.TryGetTarget(out var target);
                info.Accessor.Get(target!, ref tempValue);
                if (tempValue == value)
                    continue;
                isUndefined = true;
                break;
            }
            var isDefaultValue = value == info.DefaultValue;

            if (hook is not null)
            {
                var context = new AttributeHookContext
                {
                    Objects = _objects,
                    Info = info,
                    IsUndefined = isUndefined,
                    IsDefaultValue = isDefaultValue,
                };
                if (hook(in context, ref value))
                    _pendingSetAttributes.Add((info, value.Clone()));
                return;
            }

            _textBuffer.Set(info.Name);
            Widgets.ItemLabel(
                _textBuffer.ContentBytes,
                Widgets.GetItemLabelColor(isUndefined, isDefaultValue));

            using var guardBackgroundColor = new ColorScopeGuard(
                ImGuiCol.ImGuiColFrameBg,
                Widgets.GetItemBackgroundColor(isUndefined),
                isUndefined);

            var options = new Widgets.EditVariantOptions();

            if (value.Type == VariantType.VariantVector)
            {
                var structElements = info.GetMetadata(AttributeMetadata.VectorStructElements).GetStringVector();
                if (structElements.Count != 0)
                    options = options.SizedStructVector(structElements);
                else
                    options = options.AllowResize().AllowTypeChange();
            }
            else if (value.Type == VariantType.StringVector)
                options = options.AllowResize();
            else if (value.Type == VariantType.StringVariantMap)
            {
                options = options.AllowResize().AllowTypeChange();
                if (info.GetMetadata(AttributeMetadata.DynamicMetadata).GetBool())
                    options = options.DynamicMetadata();
            }
            else if (info.EnumNames.Count != 0)
                options = options.Enum(info.EnumNames);
            else if (value.Type == VariantType.ResourceRefList)
            {
                var structElements = info.GetMetadata(AttributeMetadata.VectorStructElements).GetStringVector();
                if (structElements.Count != 0)
                    options = options.SizedStructVector(structElements);
                else if (info.GetMetadata(AttributeMetadata.AllowResize).GetBool())
                    options = options.AllowResize();
            }
            else if (value.Type == VariantType.Int
                && (info.Name.EndsWith(" Mask", StringComparison.Ordinal)
                    || info.Name.EndsWith("Collision Layer", StringComparison.Ordinal)))
            {
                options = options.AsBitmask();
            }

            if (Widgets.EditVariant(ref value, in options))
                _pendingSetAttributes.Add((info, value.Clone()));
        }
        finally
        {
            tempValue.Dispose();
            value.Dispose();
        }
    }

    private void RenderAction(AttributeInfo info)
    {
        _objects[0]!.TryGetTarget(out var firstObject);
        _textBuffer.Set(info.Name);
        if (ImGui.Button(_textBuffer.ContentBytes))
            _pendingActions.Add(info);

        ImGui.SameLine();

        var label = default(Variant);
        try
        {
            firstObject!.OnGetAttribute(info, ref label);
            _textBuffer.Set(label.GetString());
            ImGui.TextUnformatted(_textBuffer.ContentBytes);
        }
        finally
        {
            label.Dispose();
        }
    }

    private void RenderObjects()
    {
        _objects[0]!.TryGetTarget(out var firstObject);
        var attributes = firstObject!.GetAttributes();

        foreach (var pendingAttribute in _pendingSetAttributes)
        {
            var value = pendingAttribute.Value;
            value.Dispose();
        }
        _pendingSetAttributes.Clear();
        _pendingActions.Clear();
        foreach (var info in attributes)
        {
            if ((info.Mode & AttributeMode.NoEdit) != 0)
                continue;

            _textBuffer.Set(info.Name);
            using var guard = new IdScopeGuard(_textBuffer.ContentBytes);
            RenderAttribute(info);
        }

        if (_pendingSetAttributes.Count != 0)
        {
            foreach (var pendingAttribute in _pendingSetAttributes)
            {
                OnEditAttributeBegin?.Invoke(_objects, pendingAttribute.Info);
                foreach (var reference in _objects)
                {
                    if (reference is not null && reference.TryGetTarget(out var target))
                    {
                        target.SetAttribute(pendingAttribute.Info.Name, pendingAttribute.Value);
                        target.ApplyAttributes();
                    }
                }
                OnEditAttributeEnd?.Invoke(_objects, pendingAttribute.Info);
            }
        }

        if (_pendingActions.Count != 0)
        {
            OnActionBegin?.Invoke(_objects);
            foreach (var info in _pendingActions)
            {
                foreach (var reference in _objects)
                {
                    if (reference is not null && reference.TryGetTarget(out var target))
                    {
                        using var action = new Variant(true);
                        target.SetAttribute(info.Name, action);
                    }
                }
            }
            OnActionEnd?.Invoke(_objects);
        }
    }

    private readonly List<WeakReference<Serializable>?> _objects = [];
    private readonly List<(AttributeInfo Info, Variant Value)> _pendingSetAttributes = [];
    private readonly List<AttributeInfo> _pendingActions = [];
    private readonly CString _textBuffer = new();
}
