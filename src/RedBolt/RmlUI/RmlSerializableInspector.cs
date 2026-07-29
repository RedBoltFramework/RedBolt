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

namespace RedBolt.RmlUI;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using RedBolt.Bindings.RmlUi;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Scene;
using CoreVariant = RedBolt.Core.Variant;
using CoreVariantType = RedBolt.Core.VariantType;
using EngineLog = RedBolt.Logging.Log;
using RmlVariant = RedBolt.Bindings.RmlUi.Variant;

/// <summary>Controls type of atttribute widget. Use plain enum for interfacing with RmlUI.</summary>
internal enum AttributeType
{
    Undefined,
    Bool = 1,
    Number = 2,
    Enum = 3
}

internal sealed class RmlSerializableAttribute
{
    public string GetEnumStringValue()
    {
        if (!TryGetSerializable(out var serializable))
            return string.Empty;

        using var value = serializable.GetAttribute(Index);
        var index = value.GetInt();
        return index >= 0 && index < EnumNames.Count ? EnumNames[index] : string.Empty;
    }

    public void SetEnumStringValue(string value)
    {
        if (!TryGetSerializable(out var serializable))
            return;

        for (var index = 0; index < EnumNames.Count; ++index)
        {
            if (EnumNames[index] == value)
            {
                using var attributeValue = new CoreVariant(index);
                serializable.SetAttribute(Index, attributeValue);
                return;
            }
        }
    }

    public CoreVariant GetValue()
    {
        if (!TryGetSerializable(out var serializable))
            return default;

        switch (Type)
        {
            case AttributeType.Bool:
                using (var value = serializable.GetAttribute(Index))
                    return new CoreVariant(value.GetBool());

            case AttributeType.Enum:
                return new CoreVariant(GetEnumStringValue());

            case AttributeType.Number:
                using (var value = serializable.GetAttribute(Index))
                {
                    return InternalType switch
                    {
                        CoreVariantType.Int => new CoreVariant(value.GetInt()),
                        CoreVariantType.Int64 => new CoreVariant(value.GetInt64()),
                        CoreVariantType.Float => new CoreVariant(value.GetFloat()),
                        CoreVariantType.Double => new CoreVariant(value.GetDouble()),
                        _ => default
                    };
                }

            default:
                return default;
        }
    }

    public void SetValue(in CoreVariant variant)
    {
        if (!TryGetSerializable(out var serializable))
            return;

        switch (Type)
        {
            case AttributeType.Bool:
                using (var value = new CoreVariant(variant.GetBool()))
                    serializable.SetAttribute(Index, value);
                break;

            case AttributeType.Enum:
                SetEnumStringValue(variant.GetString());
                break;

            case AttributeType.Number:
                switch (InternalType)
                {
                    case CoreVariantType.Int:
                        using (var value = new CoreVariant(variant.GetInt()))
                            serializable.SetAttribute(Index, value);
                        break;

                    case CoreVariantType.Int64:
                        using (var value = new CoreVariant(variant.GetInt64()))
                            serializable.SetAttribute(Index, value);
                        break;

                    case CoreVariantType.Float:
                        using (var value = new CoreVariant(variant.GetFloat()))
                            serializable.SetAttribute(Index, value);
                        break;

                    case CoreVariantType.Double:
                        using (var value = new CoreVariant(variant.GetDouble()))
                            serializable.SetAttribute(Index, value);
                        break;
                }
                break;
        }
    }

    private bool TryGetSerializable(out Serializable serializable)
    {
        if (Serializable is not null && Serializable.TryGetTarget(out var target))
        {
            serializable = target;
            return true;
        }

        serializable = null!;
        return false;
    }

    public WeakReference<Serializable>? Serializable;
    public int Index;
    public CoreVariantType InternalType;
    public string Name = string.Empty;
    public string EnumSelector = string.Empty;
    public IReadOnlyList<string> EnumNames = [];
    public AttributeType Type;
}

/// <summary>UI widget to inspect contents of Serializable.</summary>
[Reflected(ObjectCategory.Category_RmlUI)]
public partial class RmlSerializableInspector : RmlUIComponent
{
    private enum AttributeMember
    {
        Name,
        Type,
        EnumSelector,
        Value
    }

    private sealed class AttributeBinding(List<RmlSerializableAttribute> attributes, int index)
    {
        public RmlSerializableAttribute Attribute => attributes[index];
    }

    private sealed class AttributeMemberBinding(AttributeBinding attribute, AttributeMember member)
    {
        public AttributeBinding Attribute { get; } = attribute;
        public AttributeMember Member { get; } = member;
    }

    private sealed class AttributeSizeBinding(List<RmlSerializableAttribute> attributes)
    {
        public int Size => attributes.Count;
    }

    private sealed class AttributeStorage : IDisposable
    {
        public nint Add(object value)
        {
            var handle = GCHandle.Alloc(value);
            _handles.Add(handle);
            return GCHandle.ToIntPtr(handle);
        }

        public nint GetOrAdd(nint parent, string name, int index, Func<object> factory)
        {
            var key = (parent, name, index);
            if (_children.TryGetValue(key, out var result))
                return result;

            result = Add(factory());
            _children.Add(key, result);
            return result;
        }

        public T Get<T>(nint ptr)
            where T : class
        {
            return (T)GCHandle.FromIntPtr(ptr).Target!;
        }

        public void Dispose()
        {
            for (var i = _handles.Count - 1; i >= 0; --i)
                _handles[i].Free();
            _handles.Clear();
            _children.Clear();
        }

        private readonly List<GCHandle> _handles = [];
        private readonly Dictionary<(nint Parent, string Name, int Index), nint> _children = [];
    }

    private sealed class AttributeMemberDefinition(AttributeStorage storage) : VariableDefinition(DataVariableType.Scalar)
    {
        public override bool Get(nint ptr, RmlVariant variant)
        {
            var target = GCHandle.FromIntPtr(ptr).Target;
            if (target is AttributeSizeBinding sizeBinding)
            {
                using var size = new CoreVariant(sizeBinding.Size);
                return RmlUI.ToRmlUi(size, variant);
            }

            var binding = (AttributeMemberBinding)target!;
            var attribute = binding.Attribute.Attribute;
            using var value = binding.Member switch
            {
                AttributeMember.Name => new CoreVariant(attribute.Name),
                AttributeMember.Type => new CoreVariant((int)attribute.Type),
                AttributeMember.EnumSelector => new CoreVariant(attribute.EnumSelector),
                AttributeMember.Value => attribute.GetValue(),
                _ => default
            };
            return RmlUI.ToRmlUi(value, variant);
        }

        public override bool Set(nint ptr, RmlVariant variant)
        {
            if (!RmlUI.FromRmlUi(variant, out var value))
                return false;

            using (value)
            {
                var binding = storage.Get<AttributeMemberBinding>(ptr);
                var attribute = binding.Attribute.Attribute;
                switch (binding.Member)
                {
                    case AttributeMember.Name:
                        attribute.Name = value.GetString();
                        break;
                    case AttributeMember.Type:
                        attribute.Type = (AttributeType)value.GetInt();
                        break;
                    case AttributeMember.EnumSelector:
                        attribute.EnumSelector = value.GetString();
                        break;
                    case AttributeMember.Value:
                        attribute.SetValue(value);
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }
    }

    private sealed class AttributeDefinition(
        AttributeStorage storage, AttributeMemberDefinition memberDefinition)
        : VariableDefinition(DataVariableType.Struct)
    {
        public override DataVariable Child(nint ptr, DataAddressEntry address)
        {
            using var nativeName = address.Name;
            var name = nativeName.ToString();
            var member = name switch
            {
                "name" => AttributeMember.Name,
                "type" => AttributeMember.Type,
                "enum_selector" => AttributeMember.EnumSelector,
                "value" => AttributeMember.Value,
                _ => (AttributeMember?)null
            };
            if (member is null)
                return new DataVariable();

            var attribute = storage.Get<AttributeBinding>(ptr);
            var child = storage.GetOrAdd(ptr, name, -1,
                () => new AttributeMemberBinding(attribute, member.Value));
            return new DataVariable(memberDefinition, child);
        }
    }

    private sealed class AttributeVectorDefinition(
        AttributeStorage storage, AttributeDefinition attributeDefinition,
        AttributeMemberDefinition memberDefinition)
        : VariableDefinition(DataVariableType.Array)
    {
        public override int Size(nint ptr) => storage.Get<List<RmlSerializableAttribute>>(ptr).Count;

        public override DataVariable Child(nint ptr, DataAddressEntry address)
        {
            var attributes = storage.Get<List<RmlSerializableAttribute>>(ptr);
            var index = address.Index;
            if (index >= 0 && index < attributes.Count)
            {
                var child = storage.GetOrAdd(ptr, string.Empty, index,
                    () => new AttributeBinding(attributes, index));
                return new DataVariable(attributeDefinition, child);
            }

            using var nativeName = address.Name;
            if (nativeName.ToString() == "size")
            {
                var child = storage.GetOrAdd(ptr, "size", -1,
                    () => new AttributeSizeBinding(attributes));
                return new DataVariable(memberDefinition, child);
            }

            return new DataVariable();
        }
    }

    private sealed class AttributeDefinitions : IDisposable
    {
        public AttributeDefinitions()
        {
            _memberDefinition = new AttributeMemberDefinition(_storage);
            _attributeDefinition = new AttributeDefinition(_storage, _memberDefinition);
            _vectorDefinition = new AttributeVectorDefinition(_storage, _attributeDefinition, _memberDefinition);
        }

        public DataVariable Create(List<RmlSerializableAttribute> attributes) =>
            new(_vectorDefinition, _storage.Add(attributes));

        public void Dispose()
        {
            _vectorDefinition.Dispose();
            _attributeDefinition.Dispose();
            _memberDefinition.Dispose();
            _storage.Dispose();
        }

        private readonly AttributeStorage _storage = new();
        private readonly AttributeMemberDefinition _memberDefinition;
        private readonly AttributeDefinition _attributeDefinition;
        private readonly AttributeVectorDefinition _vectorDefinition;
    }

    public RmlSerializableInspector()
    {
        SetResource("UI/SerializableInspector.rml");
    }

    public override void Dispose()
    {
        base.Dispose();
        _attributeDefinitions.Dispose();
    }

    /// <summary>Connect widget to Serializable.</summary>
    public void Connect(Serializable? serializable)
    {
        if (serializable is null)
        {
            EngineLog.GetLogger().Error("Cannot connect RmlSerializableInspector to object before initialization");
            return;
        }

        _serializable = new WeakReference<Serializable>(serializable);
        _type = serializable.GetTypeName();
        _attributes.Clear();

        var attributes = serializable.GetAttributes();
        var index = 0;
        foreach (var attributeInfo in attributes)
        {
            var attribute = new RmlSerializableAttribute
            {
                Index = index,
                InternalType = attributeInfo.DefaultValue.Type,
                Serializable = _serializable,
                Name = attributeInfo.Name
            };

            if (attribute.InternalType == CoreVariantType.Bool)
                attribute.Type = AttributeType.Bool;
            else if (attributeInfo.EnumNames.Count != 0)
            {
                attribute.Type = AttributeType.Enum;
                attribute.EnumNames = attributeInfo.EnumNames;

                attribute.EnumSelector = "<select data-value='attribute.value' style='width: 90%'>";
                foreach (var option in attribute.EnumNames)
                    attribute.EnumSelector += $"<option value='{option}'>{option}</option>";
                attribute.EnumSelector += "</select>";
            }
            else if (attribute.InternalType == CoreVariantType.Int || attribute.InternalType == CoreVariantType.Int64
                || attribute.InternalType == CoreVariantType.Float || attribute.InternalType == CoreVariantType.Double)
            {
                attribute.Type = AttributeType.Number;
            }

            if (attribute.Type != AttributeType.Undefined)
                _attributes.Add(attribute);

            ++index;
        }

        DirtyVariable("attributes");
        DirtyVariable("type");
    }

    /// <summary>RmlUIComponent implementation.</summary>
    public override void Update(float timeStep)
    {
        base.Update(timeStep);

        if (_serializable is null || !_serializable.TryGetTarget(out _))
        {
            Remove();
            return;
        }

        DirtyVariable("attributes");
    }

    protected override void OnDataModelInitialized()
    {
        var constructor = GetDataModelConstructor()!;
        using var attributesName = new Std.String();
        attributesName.Assign("attributes");
        using var attributes = _attributeDefinitions.Create(_attributes);
        constructor.BindCustomDataVariable(attributesName, attributes);

        BindDataModelProperty("type",
            (ref CoreVariant value) => value = new CoreVariant(_type),
            (in CoreVariant value) => _type = value.GetString());

        SubscribeToEvent<RmlCustomEvent>(GetUI(), (ref RmlCustomEvent args) =>
        {
            if (args.EventType == new StringHash("RmlSerializableInspector_CloseWindow"))
                Remove();
        });
    }

    private WeakReference<Serializable>? _serializable;
    private string _type = string.Empty;
    private readonly List<RmlSerializableAttribute> _attributes = [];
    private readonly AttributeDefinitions _attributeDefinitions = new();
}
