//
// Copyright (c) 2022-2022 the rbfx project.
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

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Resource;

/// <summary>SystemUI widget used to edit resources.</summary>
public class ResourceInspectorWidget : BaseWidget
{
    public delegate Variant GetterCallback(Resource resource);

    public delegate void SetterCallback(Resource resource, in Variant value);

    public struct PropertyDesc
    {
        public string Name = string.Empty;
        public Variant DefaultValue;
        public GetterCallback Getter = null!;
        public SetterCallback Setter = null!;
        public string Hint = string.Empty;
        public Widgets.EditVariantOptions Options = new();

        public PropertyDesc()
        {
        }
    }

    public event Action? OnEditBegin;

    public event Action? OnEditEnd;

    public ResourceInspectorWidget(IReadOnlyList<Resource> resources, IReadOnlyList<PropertyDesc> properties)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(properties);
        if (resources.Count == 0)
            throw new ArgumentException("At least one resource is required.", nameof(resources));

        _resources = new List<Resource>(resources);
        _properties = properties;
    }

    public override void Dispose()
    {
        ClearPendingProperties();
        _resources.Clear();
        _textBuffer.Dispose();
        OnEditBegin = null;
        OnEditEnd = null;
        base.Dispose();
    }

    public void RenderTitle()
    {
        if (_resources.Count == 1)
            _textBuffer.Set(_resources[0].GetName());
        else
            _textBuffer.FormatInPlace($"{_resources.Count} {_resources[0].GetTypeName()}");
        ImGui.TextUnformatted(_textBuffer.ContentBytes);
    }

    public override void RenderContent()
    {
        ClearPendingProperties();

        using var idGuard = new IdScopeGuard("RenderProperties"u8);
        if (!ImGui.CollapsingHeader(
                "Properties"u8,
                ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsDefaultOpen))
        {
            return;
        }

        for (var index = 0; index < _properties.Count; ++index)
        {
            var property = _properties[index];
            RenderProperty(in property);
        }

        ImGui.Separator();

        if (_pendingSetProperties.Count == 0)
            return;

        OnEditBegin?.Invoke();
        foreach (var resource in _resources)
        {
            foreach (var pendingProperty in _pendingSetProperties)
            {
                var pendingValue = pendingProperty.Value;
                pendingProperty.Description.Setter(resource, in pendingValue);
            }
        }
        OnEditEnd?.Invoke();
    }

    public virtual bool CanSave() => true;

    public IReadOnlyList<Resource> GetResources() => _resources;

    private void RenderProperty(in PropertyDesc description)
    {
        _textBuffer.Set(description.Name);
        using var idGuard = new IdScopeGuard(_textBuffer.ContentBytes);

        var value = description.Getter(_resources[0]);
        try
        {
            var isUndefined = false;
            for (var index = 1; index < _resources.Count; ++index)
            {
                using var otherValue = description.Getter(_resources[index]);
                if (value == otherValue)
                    continue;
                isUndefined = true;
                break;
            }

            Widgets.ItemLabel(
                _textBuffer.ContentBytes,
                Widgets.GetItemLabelColor(isUndefined, value == description.DefaultValue));
            if (description.Hint.Length != 0 && ImGui.IsItemHovered())
            {
                _textBuffer.Set(description.Hint);
                ImGui.SetTooltip(_textBuffer.ContentBytes);
            }

            using var colorGuard = new ColorScopeGuard(
                ImGuiCol.ImGuiColFrameBg,
                Widgets.GetItemBackgroundColor(isUndefined),
                isUndefined);

            ImGui.BeginDisabled(!CanSave());
            try
            {
                if (Widgets.EditVariant(ref value, in description.Options))
                    _pendingSetProperties.Add(new PendingSetProperty(description, value.Clone()));
            }
            finally
            {
                ImGui.EndDisabled();
            }
        }
        finally
        {
            value.Dispose();
        }
    }

    private void ClearPendingProperties()
    {
        foreach (var property in _pendingSetProperties)
        {
            var value = property.Value;
            value.Dispose();
        }
        _pendingSetProperties.Clear();
    }

    private readonly struct PendingSetProperty(PropertyDesc description, Variant value)
    {
        public PropertyDesc Description { get; } = description;
        public Variant Value { get; } = value;
    }

    private readonly IReadOnlyList<PropertyDesc> _properties;
    private readonly List<PendingSetProperty> _pendingSetProperties = [];
    private readonly List<Resource> _resources;
    private readonly CString _textBuffer = new();
}
