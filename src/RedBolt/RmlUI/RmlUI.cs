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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using RedBolt.Bindings.RmlUi;
using RedBolt.Container;
using RedBolt.Core;
using CoreContext = RedBolt.Core.Context;
using CoreVariant = RedBolt.Core.Variant;
using CoreVariantType = RedBolt.Core.VariantType;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.IO;
using EngineLog = RedBolt.Logging.Log;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Resource;
using RmlVariant = RedBolt.Bindings.RmlUi.Variant;
using RmlVariantType = RedBolt.Bindings.RmlUi.VariantType;

/// <summary>Previous and current canvas sizes.</summary>
public struct RmlCanvasResizedArgs
{
    /// <summary>Previous size of canvas.</summary>
    public Vector2i OldSize;
    /// <summary>Current size of canvas.</summary>
    public Vector2i NewSize;
}

/// <summary>Unloaded and replacement documents.</summary>
public struct RmlDocumentReloadedArgs
{
    /// <summary>Pointer to a document that was unloaded.</summary>
    public ElementDocument UnloadedDocument;
    /// <summary>Pointer to a document that was loaded in place of unloaded one.</summary>
    public ElementDocument LoadedDocument;
}

public delegate void RmlMouseMoveHandler(ref Vector2i position);

/// <summary>UI subsystem. Manages the graphical user interface.</summary>
[Reflected]
public partial class RmlUI : RedObject
{
    private sealed class RmlEventListenerInstancer : EventListenerInstancer
    {
        /// <summary>Create an instance of inline event listener, if applicable.</summary>
        public override EventListener InstanceEventListener(Std.String value, Element element)
        {
            var listener = PipeEventListener.CreateInstancer(value.ToString(), element);
            if (listener is not null)
                EventListeners[listener.Handle] = listener;
            return listener!;
        }
    }

    private sealed class RmlContextInstancer : ContextInstancer
    {
        /// <summary>Create instance of RmlContext.</summary>
        public override Bindings.RmlUi.Context InstanceContext(
            Std.String name, nint renderManager, nint textInputHandler)
        {
            return new RmlContext(name, renderManager, textInputHandler);
        }

        /// <summary>Free instance of RmlContext.</summary>
        public override void ReleaseContext(Bindings.RmlUi.Context context)
        {
            context.Dispose();
        }

        /// <summary>RmlContextInstancer is static, nothing to release.</summary>
        protected override void Release()
        {
        }
    }

    private sealed class RmlPlugin : Plugin
    {
        /// <summary>Subscribe to events.</summary>
        public override int GetEventClasses() => 1 << 1;

        /// <summary>Handle documents being closed from within RmlUi middleware.</summary>
        public override void OnDocumentUnload(ElementDocument document)
        {
            var rmlContext = (RmlContext)document.GetContext();
            rmlContext.GetOwnerSubsystem()?.OnDocumentUnload(document);
        }
    }

    private interface IVariantBinding
    {
        bool OwnsResult { get; }
        CoreVariant Get();
        bool Set(in CoreVariant value);
    }

    private sealed class VariantBinding(StrongBox<CoreVariant> value) : IVariantBinding
    {
        public bool OwnsResult => false;
        public CoreVariant Get() => value.Value;
        public bool Set(in CoreVariant newValue)
        {
            var copy = newValue.Clone();
            value.Value.Dispose();
            value.Value = copy;
            return true;
        }
    }

    private sealed class VariantVectorItemBinding(VariantVector value, int index) : IVariantBinding
    {
        public bool OwnsResult => false;
        public CoreVariant Get() => value[index];
        public bool Set(in CoreVariant newValue)
        {
            value[index] = newValue;
            return true;
        }
    }

    private sealed class VariantMapItemBinding(VariantMap value, StringHash name) : IVariantBinding
    {
        public bool OwnsResult => false;
        public CoreVariant Get() => value[name];
        public bool Set(in CoreVariant newValue)
        {
            value[name] = newValue;
            return true;
        }
    }

    private sealed class NestedVariantVectorItemBinding(IVariantBinding parent, int index) : IVariantBinding
    {
        public bool OwnsResult => false;
        public CoreVariant Get() => parent.Get().GetVariantVector()[index];
        public bool Set(in CoreVariant value)
        {
            parent.Get().GetVariantVector()[index] = value;
            return true;
        }
    }

    private sealed class NestedVariantMapItemBinding(IVariantBinding parent, StringHash name) : IVariantBinding
    {
        public bool OwnsResult => false;
        public CoreVariant Get() => parent.Get().GetVariantMap()[name];
        public bool Set(in CoreVariant value)
        {
            parent.Get().GetVariantMap()[name] = value;
            return true;
        }
    }

    private sealed class ResourceRefListItemBinding(IVariantBinding parent, int index) : IVariantBinding
    {
        public bool OwnsResult => true;
        public CoreVariant Get() => new(parent.Get().GetResourceRefList().Names[index]);

        public bool Set(in CoreVariant value)
        {
            var resourceRefList = parent.Get().GetResourceRefList();
            resourceRefList.Names[index] = value.GetString();
            using var replacement = new CoreVariant(resourceRefList);
            return parent.Set(replacement);
        }
    }

    private sealed class StringVectorItemBinding(IVariantBinding parent, int index) : IVariantBinding
    {
        public bool OwnsResult => true;
        public CoreVariant Get() => new(parent.Get().GetStringVector()[index]);
        public bool Set(in CoreVariant value)
        {
            parent.Get().GetStringVector()[index] = value.GetString();
            return true;
        }
    }

    private sealed class DirectStringVectorItemBinding(StringVector value, int index) : IVariantBinding
    {
        public bool OwnsResult => true;
        public CoreVariant Get() => new(value[index]);
        public bool Set(in CoreVariant newValue)
        {
            value[index] = newValue.GetString();
            return true;
        }
    }

    private sealed class IntegerBinding(Func<int> getter) : IVariantBinding
    {
        public bool OwnsResult => true;
        public CoreVariant Get() => new(getter());
        public bool Set(in CoreVariant value) => false;
    }

    private readonly record struct DataVariableBindingKey(nint Parent, string Name, int Index);

    private sealed class DataVariableStorage : IDisposable
    {
        public nint Add(object value)
        {
            var handle = GCHandle.Alloc(value);
            _handles.Add(handle);
            return GCHandle.ToIntPtr(handle);
        }

        public nint GetOrAdd(nint parent, string name, int index, Func<object> factory)
        {
            var key = new DataVariableBindingKey(parent, name, index);
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
        private readonly Dictionary<DataVariableBindingKey, nint> _children = [];
    }

    private sealed class VariantMapDefinition : VariableDefinition
    {
        public VariantMapDefinition(DataTypeRegister typeRegister, DataVariableStorage storage,
            VariantDefinition variantDefinition)
            : base(DataVariableType.Struct)
        {
            _register = typeRegister;
            _storage = storage;
            _variantDefinition = variantDefinition;
        }

        public override DataVariable Child(nint ptr, DataAddressEntry address)
        {
            using var nativeName = address.Name;
            var name = nativeName.ToString();
            if (name.Length == 0)
            {
                EngineLog.GetLogger().Warning("Expected a struct member name but none given.");
                return new DataVariable();
            }

            var value = _storage.Get<VariantMap>(ptr);
            var key = new StringHash(name);
            if (!value.ContainsKey(key))
            {
                EngineLog.GetLogger().Warning("Member {} not found in data struct.", name);
                return new DataVariable();
            }

            var child = _storage.GetOrAdd(ptr, name, -1, () => new VariantMapItemBinding(value, key));
            return new DataVariable(_variantDefinition, child);
        }

        private readonly DataTypeRegister _register;
        private readonly DataVariableStorage _storage;
        private readonly VariantDefinition _variantDefinition;
    }

    private sealed class VariantDefinition : VariableDefinition
    {
        public VariantDefinition(DataTypeRegister typeRegister, DataVariableStorage storage)
            : base(DataVariableType.Scalar)
        {
            _register = typeRegister;
            _storage = storage;
        }

        public override bool Get(nint ptr, RmlVariant variant)
        {
            var binding = _storage.Get<IVariantBinding>(ptr);
            var value = binding.Get();
            try
            {
                return ToRmlUi(value, variant);
            }
            finally
            {
                if (binding.OwnsResult)
                    value.Dispose();
            }
        }

        public override bool Set(nint ptr, RmlVariant variant)
        {
            if (!FromRmlUi(variant, out var value))
                return false;

            try
            {
                return _storage.Get<IVariantBinding>(ptr).Set(value);
            }
            finally
            {
                value.Dispose();
            }
        }

        public override int Size(nint ptr)
        {
            var binding = _storage.Get<IVariantBinding>(ptr);
            var value = binding.Get();
            try
            {
                return value.Type switch
                {
                    CoreVariantType.ResourceRefList => value.GetResourceRefList().Names.Count,
                    CoreVariantType.StringVector => value.GetStringVector().Count,
                    CoreVariantType.VariantVector => value.GetVariantVector().Count,
                    _ => 0
                };
            }
            finally
            {
                if (binding.OwnsResult)
                    value.Dispose();
            }
        }

        public override DataVariable Child(nint ptr, DataAddressEntry address)
        {
            var binding = _storage.Get<IVariantBinding>(ptr);
            var value = binding.Get();
            try
            {
                using var nativeName = address.Name;
                var name = nativeName.ToString();
                if (name.Length == 0)
                {
                    var index = address.Index;
                    switch (value.Type)
                    {
                        case CoreVariantType.VariantVector:
                        {
                            var vector = value.GetVariantVector();
                            if (!ValidateIndex(index, vector.Count))
                                return new DataVariable();
                            var child = _storage.GetOrAdd(ptr, string.Empty, index,
                                () => new NestedVariantVectorItemBinding(binding, index));
                            return new DataVariable(this, child);
                        }
                        case CoreVariantType.ResourceRefList:
                        {
                            var vector = value.GetResourceRefList().Names;
                            if (!ValidateIndex(index, vector.Count))
                                return new DataVariable();
                            var child = _storage.GetOrAdd(ptr, string.Empty, index,
                                () => new ResourceRefListItemBinding(binding, index));
                            return new DataVariable(this, child);
                        }
                        case CoreVariantType.StringVector:
                        {
                            var vector = value.GetStringVector();
                            if (!ValidateIndex(index, vector.Count))
                                return new DataVariable();
                            var child = _storage.GetOrAdd(ptr, string.Empty, index,
                                () => new StringVectorItemBinding(binding, index));
                            return new DataVariable(this, child);
                        }
                        default:
                            EngineLog.GetLogger().Warning(
                                "Index operation is not valid on type {}.", value.GetTypeName());
                            return new DataVariable();
                    }
                }

                switch (value.Type)
                {
                    case CoreVariantType.VariantMap:
                    {
                        var valuePtr = value.GetVariantMap();
                        var key = new StringHash(name);
                        if (!valuePtr.ContainsKey(key))
                        {
                            EngineLog.GetLogger().Warning("Member {} not found in VariantMap.", name);
                            return new DataVariable();
                        }

                        var child = _storage.GetOrAdd(ptr, name, -1,
                            () => new NestedVariantMapItemBinding(binding, key));
                        return new DataVariable(this, child);
                    }
                    default:
                        EngineLog.GetLogger().Warning(
                            "Get property {} operation is not valid on type {}.", name, value.GetTypeName());
                        return new DataVariable();
                }
            }
            finally
            {
                if (binding.OwnsResult)
                    value.Dispose();
            }
        }

        private static bool ValidateIndex(int index, int size)
        {
            if (index < 0 || index >= size)
            {
                EngineLog.GetLogger().Warning("Data array index {} out of bounds.", index);
                return false;
            }
            return true;
        }

        private readonly DataTypeRegister _register;
        private readonly DataVariableStorage _storage;
    }

    private sealed class VariantVectorDefinition : VariableDefinition
    {
        public VariantVectorDefinition(DataVariableStorage storage, VariantDefinition variantDefinition)
            : base(DataVariableType.Array)
        {
            _storage = storage;
            _variantDefinition = variantDefinition;
        }

        public override int Size(nint ptr) => _storage.Get<VariantVector>(ptr).Count;

        public override DataVariable Child(nint ptr, DataAddressEntry address)
        {
            var value = _storage.Get<VariantVector>(ptr);
            var index = address.Index;
            if (index < 0 || index >= value.Count)
            {
                using var nativeName = address.Name;
                var name = nativeName.ToString();
                if (name == "size")
                {
                    var size = _storage.GetOrAdd(ptr, name, index, () => new IntegerBinding(() => value.Count));
                    return new DataVariable(_variantDefinition, size);
                }

                EngineLog.GetLogger().Warning("Data array index out of bounds.");
                return new DataVariable();
            }

            var child = _storage.GetOrAdd(ptr, string.Empty, index,
                () => new VariantVectorItemBinding(value, index));
            return new DataVariable(_variantDefinition, child);
        }

        private readonly DataVariableStorage _storage;
        private readonly VariantDefinition _variantDefinition;
    }

    private sealed class StringVectorDefinition : VariableDefinition
    {
        public StringVectorDefinition(DataVariableStorage storage, VariantDefinition variantDefinition)
            : base(DataVariableType.Array)
        {
            _storage = storage;
            _variantDefinition = variantDefinition;
        }

        public override int Size(nint ptr) => _storage.Get<StringVector>(ptr).Count;

        public override DataVariable Child(nint ptr, DataAddressEntry address)
        {
            var value = _storage.Get<StringVector>(ptr);
            var index = address.Index;
            if (index < 0 || index >= value.Count)
            {
                using var nativeName = address.Name;
                var name = nativeName.ToString();
                if (name == "size")
                {
                    var size = _storage.GetOrAdd(ptr, name, index, () => new IntegerBinding(() => value.Count));
                    return new DataVariable(_variantDefinition, size);
                }

                EngineLog.GetLogger().Warning("Data array index out of bounds.");
                return new DataVariable();
            }

            var child = _storage.GetOrAdd(ptr, string.Empty, index,
                () => new DirectStringVectorItemBinding(value, index));
            return new DataVariable(_variantDefinition, child);
        }

        private readonly DataVariableStorage _storage;
        private readonly VariantDefinition _variantDefinition;
    }

    internal sealed class VariantDefinitions : IDisposable
    {
        public VariantDefinitions(DataTypeRegister typeRegister)
        {
            _variantDefinition = new VariantDefinition(typeRegister, _storage);
            _variantVectorDefinition = new VariantVectorDefinition(_storage, _variantDefinition);
            _stringVectorDefinition = new StringVectorDefinition(_storage, _variantDefinition);
            _variantMapDefinition = new VariantMapDefinition(typeRegister, _storage, _variantDefinition);
        }

        public DataVariable Create(VariantVector value) =>
            new(_variantVectorDefinition, _storage.Add(value));

        public DataVariable Create(VariantMap value) =>
            new(_variantMapDefinition, _storage.Add(value));

        public DataVariable Create(StringVector value) =>
            new(_stringVectorDefinition, _storage.Add(value));

        public DataVariable Create(StrongBox<CoreVariant> value) =>
            new(_variantDefinition, _storage.Add(new VariantBinding(value)));

        public void Dispose()
        {
            _variantMapDefinition.Dispose();
            _stringVectorDefinition.Dispose();
            _variantVectorDefinition.Dispose();
            _variantDefinition.Dispose();
            _storage.Dispose();
        }

        private readonly DataVariableStorage _storage = new();
        private readonly VariantDefinition _variantDefinition;
        private readonly VariantVectorDefinition _variantVectorDefinition;
        private readonly StringVectorDefinition _stringVectorDefinition;
        private readonly VariantMapDefinition _variantMapDefinition;
    }

    /// <summary>For internal use only! Helper class used to associate instance of RmlUI with Rml::Context.</summary>
    internal sealed class RmlContext : Bindings.RmlUi.Context
    {
        /// <summary>Construct.</summary>
        public RmlContext(Std.String name, nint renderManager, nint textInputHandler)
            : base(name, renderManager, textInputHandler)
        {
        }

        /// <summary>Set owner subsystem pointer.</summary>
        public void SetOwnerSubsystem(RmlUI? ui) => _ownerSubsystem = ui;

        /// <summary>Get owner subsystem pointer.</summary>
        public RmlUI? GetOwnerSubsystem() => _ownerSubsystem;

        private RmlUI? _ownerSubsystem;
    }

    private static readonly RmlEventListenerInstancer EventListenerInstancerInstance = new();
    private static readonly RmlContextInstancer ContextInstancerInstance = new();
    private static readonly RmlPlugin PluginInstance = new();
    private static readonly Dictionary<nint, EventListener> EventListeners = [];
    private static readonly Std.String MousePassthroughAttribute = new("mouse-passthrough");
    internal static readonly Std.String ComponentPtrAttribute = new("__RmlUIComponentPtr__");

    /// <summary>Map engine keys to RmlUi keys. Note that top bit is cleared from key constants when they are used as array index.</summary>
    private static readonly Dictionary<Key, Rml.Input.KeyIdentifier> KeyMap = new()
    {
        [Key.Space] = Rml.Input.KeyIdentifier.KiSpace,
        [Key.Num0] = Rml.Input.KeyIdentifier.Ki0,
        [Key.Num1] = Rml.Input.KeyIdentifier.Ki1,
        [Key.Num2] = Rml.Input.KeyIdentifier.Ki2,
        [Key.Num3] = Rml.Input.KeyIdentifier.Ki3,
        [Key.Num4] = Rml.Input.KeyIdentifier.Ki4,
        [Key.Num5] = Rml.Input.KeyIdentifier.Ki5,
        [Key.Num6] = Rml.Input.KeyIdentifier.Ki6,
        [Key.Num7] = Rml.Input.KeyIdentifier.Ki7,
        [Key.Num8] = Rml.Input.KeyIdentifier.Ki8,
        [Key.Num9] = Rml.Input.KeyIdentifier.Ki9,
        [Key.A] = Rml.Input.KeyIdentifier.KiA,
        [Key.B] = Rml.Input.KeyIdentifier.KiB,
        [Key.C] = Rml.Input.KeyIdentifier.KiC,
        [Key.D] = Rml.Input.KeyIdentifier.KiD,
        [Key.E] = Rml.Input.KeyIdentifier.KiE,
        [Key.F] = Rml.Input.KeyIdentifier.KiF,
        [Key.G] = Rml.Input.KeyIdentifier.KiG,
        [Key.H] = Rml.Input.KeyIdentifier.KiH,
        [Key.I] = Rml.Input.KeyIdentifier.KiI,
        [Key.J] = Rml.Input.KeyIdentifier.KiJ,
        [Key.K] = Rml.Input.KeyIdentifier.KiK,
        [Key.L] = Rml.Input.KeyIdentifier.KiL,
        [Key.M] = Rml.Input.KeyIdentifier.KiM,
        [Key.N] = Rml.Input.KeyIdentifier.KiN,
        [Key.O] = Rml.Input.KeyIdentifier.KiO,
        [Key.P] = Rml.Input.KeyIdentifier.KiP,
        [Key.Q] = Rml.Input.KeyIdentifier.KiQ,
        [Key.R] = Rml.Input.KeyIdentifier.KiR,
        [Key.S] = Rml.Input.KeyIdentifier.KiS,
        [Key.T] = Rml.Input.KeyIdentifier.KiT,
        [Key.U] = Rml.Input.KeyIdentifier.KiU,
        [Key.V] = Rml.Input.KeyIdentifier.KiV,
        [Key.W] = Rml.Input.KeyIdentifier.KiW,
        [Key.X] = Rml.Input.KeyIdentifier.KiX,
        [Key.Y] = Rml.Input.KeyIdentifier.KiY,
        [Key.Z] = Rml.Input.KeyIdentifier.KiZ,
        [Key.Semicolon] = Rml.Input.KeyIdentifier.KiOem1,
        [Key.Equals] = Rml.Input.KeyIdentifier.KiOemPlus,
        [Key.Comma] = Rml.Input.KeyIdentifier.KiOemComma,
        [Key.Minus] = Rml.Input.KeyIdentifier.KiOemMinus,
        [Key.Period] = Rml.Input.KeyIdentifier.KiOemPeriod,
        [Key.Slash] = Rml.Input.KeyIdentifier.KiOem2,
        [Key.LeftBracket] = Rml.Input.KeyIdentifier.KiOem4,
        [Key.Backslash] = Rml.Input.KeyIdentifier.KiOem5,
        [Key.RightBracket] = Rml.Input.KeyIdentifier.KiOem6,
        [Key.Kp0] = Rml.Input.KeyIdentifier.KiNumpad0,
        [Key.Kp1] = Rml.Input.KeyIdentifier.KiNumpad1,
        [Key.Kp2] = Rml.Input.KeyIdentifier.KiNumpad2,
        [Key.Kp3] = Rml.Input.KeyIdentifier.KiNumpad3,
        [Key.Kp4] = Rml.Input.KeyIdentifier.KiNumpad4,
        [Key.Kp5] = Rml.Input.KeyIdentifier.KiNumpad5,
        [Key.Kp6] = Rml.Input.KeyIdentifier.KiNumpad6,
        [Key.Kp7] = Rml.Input.KeyIdentifier.KiNumpad7,
        [Key.Kp8] = Rml.Input.KeyIdentifier.KiNumpad8,
        [Key.Kp9] = Rml.Input.KeyIdentifier.KiNumpad9,
        [Key.KpEnter] = Rml.Input.KeyIdentifier.KiNumpadenter,
        [Key.KpMultiply] = Rml.Input.KeyIdentifier.KiMultiply,
        [Key.KpPlus] = Rml.Input.KeyIdentifier.KiAdd,
        [Key.KpSpace] = Rml.Input.KeyIdentifier.KiSeparator,
        [Key.KpMinus] = Rml.Input.KeyIdentifier.KiSubtract,
        [Key.KpDecimal] = Rml.Input.KeyIdentifier.KiDecimal,
        [Key.KpDivide] = Rml.Input.KeyIdentifier.KiDivide,
        [Key.Backspace] = Rml.Input.KeyIdentifier.KiBack,
        [Key.Tab] = Rml.Input.KeyIdentifier.KiTab,
        [Key.Clear] = Rml.Input.KeyIdentifier.KiClear,
        [Key.Return] = Rml.Input.KeyIdentifier.KiReturn,
        [Key.Pause] = Rml.Input.KeyIdentifier.KiPause,
        [Key.CapsLock] = Rml.Input.KeyIdentifier.KiCapital,
        [Key.Escape] = Rml.Input.KeyIdentifier.KiEscape,
        [Key.PageUp] = Rml.Input.KeyIdentifier.KiPrior,
        [Key.PageDown] = Rml.Input.KeyIdentifier.KiNext,
        [Key.End] = Rml.Input.KeyIdentifier.KiEnd,
        [Key.Home] = Rml.Input.KeyIdentifier.KiHome,
        [Key.Left] = Rml.Input.KeyIdentifier.KiLeft,
        [Key.Up] = Rml.Input.KeyIdentifier.KiUp,
        [Key.Right] = Rml.Input.KeyIdentifier.KiRight,
        [Key.Down] = Rml.Input.KeyIdentifier.KiDown,
        [Key.Select] = Rml.Input.KeyIdentifier.KiSelect,
        [Key.PrintScreen] = Rml.Input.KeyIdentifier.KiSnapshot,
        [Key.Insert] = Rml.Input.KeyIdentifier.KiInsert,
        [Key.Delete] = Rml.Input.KeyIdentifier.KiDelete,
        [Key.Help] = Rml.Input.KeyIdentifier.KiHelp,
        [Key.LeftGui] = Rml.Input.KeyIdentifier.KiLwin,
        [Key.RightGui] = Rml.Input.KeyIdentifier.KiRwin,
        [Key.Application] = Rml.Input.KeyIdentifier.KiApps,
        [Key.Power] = Rml.Input.KeyIdentifier.KiPower,
        [Key.Sleep] = Rml.Input.KeyIdentifier.KiSleep,
        [Key.F1] = Rml.Input.KeyIdentifier.KiF1,
        [Key.F2] = Rml.Input.KeyIdentifier.KiF2,
        [Key.F3] = Rml.Input.KeyIdentifier.KiF3,
        [Key.F4] = Rml.Input.KeyIdentifier.KiF4,
        [Key.F5] = Rml.Input.KeyIdentifier.KiF5,
        [Key.F6] = Rml.Input.KeyIdentifier.KiF6,
        [Key.F7] = Rml.Input.KeyIdentifier.KiF7,
        [Key.F8] = Rml.Input.KeyIdentifier.KiF8,
        [Key.F9] = Rml.Input.KeyIdentifier.KiF9,
        [Key.F10] = Rml.Input.KeyIdentifier.KiF10,
        [Key.F11] = Rml.Input.KeyIdentifier.KiF11,
        [Key.F12] = Rml.Input.KeyIdentifier.KiF12,
        [Key.F13] = Rml.Input.KeyIdentifier.KiF13,
        [Key.F14] = Rml.Input.KeyIdentifier.KiF14,
        [Key.F15] = Rml.Input.KeyIdentifier.KiF15,
        [Key.F16] = Rml.Input.KeyIdentifier.KiF16,
        [Key.F17] = Rml.Input.KeyIdentifier.KiF17,
        [Key.F18] = Rml.Input.KeyIdentifier.KiF18,
        [Key.F19] = Rml.Input.KeyIdentifier.KiF19,
        [Key.F20] = Rml.Input.KeyIdentifier.KiF20,
        [Key.F21] = Rml.Input.KeyIdentifier.KiF21,
        [Key.F22] = Rml.Input.KeyIdentifier.KiF22,
        [Key.F23] = Rml.Input.KeyIdentifier.KiF23,
        [Key.F24] = Rml.Input.KeyIdentifier.KiF24,
        [Key.NumLockClear] = Rml.Input.KeyIdentifier.KiNumlock,
        [Key.ScrollLock] = Rml.Input.KeyIdentifier.KiScroll,
        [Key.LeftShift] = Rml.Input.KeyIdentifier.KiLshift,
        [Key.RightShift] = Rml.Input.KeyIdentifier.KiRshift,
        [Key.LeftCtrl] = Rml.Input.KeyIdentifier.KiLcontrol,
        [Key.RightCtrl] = Rml.Input.KeyIdentifier.KiRcontrol,
        [Key.LeftAlt] = Rml.Input.KeyIdentifier.KiLmenu,
        [Key.RightAlt] = Rml.Input.KeyIdentifier.KiRmenu,
        [Key.Mute] = Rml.Input.KeyIdentifier.KiVolumeMute,
        [Key.VolumeDown] = Rml.Input.KeyIdentifier.KiVolumeDown,
        [Key.VolumeUp] = Rml.Input.KeyIdentifier.KiVolumeUp
    };

    /// <summary>Number of instances of RmlUI. Used to initialize and release RmlUi library.</summary>
    private static int _rmlInstanceCounter;
    private static RmlRenderer? _renderer;
    private static RmlSystem? _system;
    private static RmlFile? _file;
    private static RedBoltNative.RmlHarfBuzzFontEngine? _fontEngine;

    /// <summary>UI context name.</summary>
    private readonly string _name;
    /// <summary>RmlUi context.</summary>
    private RmlContext? _rmlContext;
    /// <summary>Surface where UI will be rendered into.</summary>
    private WeakReference<RenderSurface>? _renderSurface;
    /// <summary>Color used to clear render surface if not rendering into backbuffer.</summary>
    private Color _clearColor = Color.TransparentBlack;
    /// <summary>Flag indicating RmlUi debugger is already initialized.</summary>
    private bool _debuggerInitialized;
    /// <summary>Whether current subsystem is rendering or not.</summary>
    private bool _isRendering = true;
    /// <summary>Other instances of RmlUI.</summary>
    private readonly List<WeakReference<RmlUI>> _siblingSubsystems = [];
    private readonly RenderTargetView[] _renderTargets = new RenderTargetView[1];
    private readonly Std.String _textInput = new();
    private bool _disposed;

    /// <summary>Construct.</summary>
    public RmlUI(string name = "master")
    {
        _name = name;

        // Initializing first instance of RmlUI, initialize backend library as well.
        if (Interlocked.Increment(ref _rmlInstanceCounter) == 1)
        {
            _renderer = new RmlRenderer();
            _system = new RmlSystem();
            _file = new RmlFile();
            _fontEngine = new RedBoltNative.RmlHarfBuzzFontEngine();
            RmlUi.SetRenderInterface(_renderer.GetAdaptedInterface());
            RmlUi.SetSystemInterface(_system);
            RmlUi.SetFileInterface(_file);
            RmlUi.SetFontEngineInterface(_fontEngine);
            _ = RmlUi.Initialise();
            Factory.RegisterEventListenerInstancer(EventListenerInstancerInstance);
            Factory.RegisterContextInstancer(ContextInstancerInstance);
            RmlUi.RegisterPlugin(PluginInstance);
        }

        using var nativeName = new Std.String();
        nativeName.Assign(name);
        _rmlContext = (RmlContext)RmlUi.CreateContext(
            nativeName, GetDesiredCanvasSize())!;
        _rmlContext.SetOwnerSubsystem(this);

        var ui = CoreContext.Instance.GetSubsystem<RmlUI>();
        if (ui is not null && !ReferenceEquals(ui, this))
            ui._siblingSubsystems.Add(new WeakReference<RmlUI>(this));

        var input = GetSubsystem<Input>();
        SubscribeToEvent<InputEvents.MouseButtonDownEvent>(input, HandleMouseButtonDown);
        SubscribeToEvent<InputEvents.MouseButtonUpEvent>(input, HandleMouseButtonUp);
        SubscribeToEvent<InputEvents.MouseMoveEvent>(input, HandleMouseMove);
        SubscribeToEvent<InputEvents.MouseWheelEvent>(input, HandleMouseWheel);
        SubscribeToEvent<InputEvents.TouchBeginEvent>(input, HandleTouchBegin);
        SubscribeToEvent<InputEvents.TouchEndEvent>(input, HandleTouchEnd);
        SubscribeToEvent<InputEvents.TouchMoveEvent>(input, HandleTouchMove);
        SubscribeToEvent<InputEvents.KeyDownEvent>(input, HandleKeyDown);
        SubscribeToEvent<InputEvents.KeyUpEvent>(input, HandleKeyUp);
        SubscribeToEvent<InputEvents.TextInputEvent>(HandleTextInput);
        SubscribeToEvent<InputEvents.DropFileEvent>(HandleDropFile);

        SubscribeToEvent<GraphicsEvents.ScreenModeEvent>(HandleScreenMode);
        SubscribeToEvent<CoreEvents.PostUpdateEvent>(HandlePostUpdate);
        SubscribeToEvent<GraphicsEvents.EndAllViewsRenderEvent>(HandleEndAllViewsRender);

        SubscribeToEvent<ResourceEvents.FileChanged>(HandleResourceReloaded);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        var ui = CoreContext.Instance.GetSubsystem<RmlUI>();
        if (ui is not null && !ReferenceEquals(ui, this))
        {
            for (var i = ui._siblingSubsystems.Count - 1; i >= 0; --i)
            {
                if (!ui._siblingSubsystems[i].TryGetTarget(out var sibling) || ReferenceEquals(sibling, this))
                    ui._siblingSubsystems.RemoveAt(i);
            }
        }

        if (_rmlContext is not null)
        {
            using var nativeName = new Std.String();
            nativeName.Assign(_name);
            if (!RmlUi.RemoveContext(nativeName))
                EngineLog.GetLogger().Error("Removal of RmlUI context {} failed.", _name);
        }
        _rmlContext = null;

        if (Interlocked.Decrement(ref _rmlInstanceCounter) == 0)
        {
            // Freeing last instance of RmlUI, deinitialize backend library.
            Factory.RegisterEventListenerInstancer(null);
            var renderer = _renderer;
            var system = _system;
            var file = _file;
            var fontEngine = _fontEngine;
            RmlUi.ReleaseTextures();
            RmlUi.Shutdown();
            renderer?.Dispose();
            system?.Dispose();
            file?.Dispose();
            fontEngine?.Dispose();
            _renderer = null;
            _system = null;
            _file = null;
            _fontEngine = null;
        }

        _textInput.Dispose();
        base.Dispose();
    }

    /// <summary>Load a specified rml document. When resource reloader is active, returned pointer will be invalidated when associated resource change triggers reloading a document.</summary>
    /// <param name="path">Resource path to the .rml file.</param>
    /// <param name="placeholderContext">Optional context pointer for placeholder substitution (e.g., RmlUIComponent instance).</param>
    public ElementDocument? LoadDocument(string path, nint placeholderContext = default)
    {
        // Load .rml file contents and update placeholders.
        var cache = GetSubsystem<ResourceCache>();
        using var file = cache.GetFile(path);
        if (file is null)
            return null;

        var fileContents = file.ReadString();
        if (placeholderContext != 0)
            fileContents = InsertVariablePlaceholders(fileContents, placeholderContext);

        // Mark resource as loaded in RmlFile interface to ensure auto-reload works.
        _file!.AddResourceLoaded(path);

        // Load document from memory.
        using var nativeContents = new Std.String();
        using var nativePath = new Std.String();
        nativeContents.Assign(fileContents);
        nativePath.Assign(path);
        return _rmlContext!.LoadDocumentFromMemory(nativeContents, nativePath);
    }

    /// <summary>Show or hide RmlUi debugger.</summary>
    public void SetDebuggerVisible(bool visible)
    {
        if (!_debuggerInitialized)
        {
            RmlUi.Initialise(_rmlContext!);
            _debuggerInitialized = true;
        }
        RmlUi.SetVisible(visible);
    }

    /// <summary>Return whether RmlUi debugger is visible.</summary>
    public bool IsDebuggerVisible() => RmlUi.IsVisible();

    /// <summary>Load a font resource for RmlUi to use.</summary>
    public bool LoadFont(string resourceName, bool fallback = false)
    {
        using var nativeName = new Std.String();
        nativeName.Assign(resourceName);
        return RmlUi.LoadFontFace(nativeName, fallback);
    }

    /// <summary>Reload fonts from resource cache.</summary>
    public void ReloadFonts()
    {
        var cache = GetSubsystem<ResourceCache>();

        List<string> fonts = [];
        foreach (var pattern in new[] { "*.ttf", "*.otf" })
        {
            cache.Scan(fonts, "Fonts/", pattern,
                ScanFlags.ScanFiles | ScanFlags.ScanRecursive | ScanFlags.ScanAppend);
        }

        foreach (var font in fonts)
            LoadFont($"Fonts/{font}");
    }

    /// <summary>Returns RmlUi context object.</summary>
    public Bindings.RmlUi.Context GetRmlContext() => _rmlContext!;

    /// <summary>Set UI scale aka dp to px ratio. 1.0 is default (pixel perfect).</summary>
    public void SetScale(float scale) => _rmlContext!.SetDensityIndependentPixelRatio(scale);

    /// <summary>Return UI scale.</summary>
    public float GetScale() => _rmlContext!.GetDensityIndependentPixelRatio();

    /// <summary>Set render target where this instance will render into.</summary>
    public void SetRenderTarget(RenderSurface? target, Color? clearColor = null)
    {
        _renderSurface = target is null ? null : new WeakReference<RenderSurface>(target);
        _clearColor = clearColor ?? Color.TransparentBlack;
        var args = new RmlCanvasResizedArgs
        {
            OldSize = _rmlContext!.GetDimensions(),
            NewSize = GetDesiredCanvasSize()
        };
        _rmlContext.SetDimensions(args.NewSize);
        OnCanvasResizedEvent?.Invoke(args);
    }

    /// <summary>Set render target where this instance will render into.</summary>
    public void SetRenderTarget(Texture2D? target, Color? clearColor = null)
    {
        SetRenderTarget(target?.GetRenderSurface(), clearColor);
    }

    /// <summary>Set render target where this instance will render into.</summary>
    public void SetRenderTarget(Color? clearColor = null)
    {
        SetRenderTarget((RenderSurface?)null, clearColor);
    }

    /// <summary>Enable or disable this UI subsystem. When disabled, no inputs will be processed and nothing will be rendered.</summary>
    public void SetRendering(bool enable) => _isRendering = enable;

    /// <summary>Return true if this subsystem is rendering. When disabled, it is still possible to manually render by calling Render() method.</summary>
    public bool IsRendering() => _isRendering;

    /// <summary>Return true if input is captured by UI.</summary>
    public bool IsInputCaptured()
    {
        if (IsInputCapturedInternal())
            return true;

        for (var i = _siblingSubsystems.Count - 1; i >= 0; --i)
        {
            if (!_siblingSubsystems[i].TryGetTarget(out var other))
            {
                _siblingSubsystems.RemoveAt(i);
                continue;
            }
            if (other.IsInputCapturedInternal())
                return true;
        }
        return false;
    }

    /// <summary>Returns true if any window of this UI context is hovered by mouse.</summary>
    public bool IsHovered()
    {
        var hover = _rmlContext!.GetHoverElement();
        if (hover is null)
            return false;
        if (hover == _rmlContext.GetRootElement())
            return false;

        var defaultValue = false;
        if (hover.GetAttribute(MousePassthroughAttribute, in defaultValue))
            return false;
        return true;
    }

    /// <summary>Update the UI logic. Called by HandlePostUpdate().</summary>
    public void Update(float timeStep)
    {
        _ = timeStep;
        _rmlContext?.Update();
        OnUpdated?.Invoke();
    }

    /// <summary>Render UI.</summary>
    public void Render()
    {
        var renderDevice = CoreContext.Instance.GetSubsystem<RenderDevice>();
        if (renderDevice is null)
            return;

        var renderContext = renderDevice.GetRenderContext();
        var renderSurface = GetRenderSurface();
        if (renderSurface is null)
        {
            renderContext.SetSwapChainRenderTargets();
        }
        else
        {
            _renderTargets[0] = renderSurface.GetView();
            renderContext.SetRenderTargets(null, _renderTargets);
            if (_clearColor.A > 0)
                renderContext.ClearRenderTarget(0, _clearColor);
        }
        renderContext.SetFullViewport();

        if (_renderer is not null)
        {
            _renderer.BeginRendering();
            _rmlContext!.Render();
            _renderer.EndRendering();
        }
    }

    /// <summary>Unload passed document and load its rml again, return newly loaded document. This operation preserves document position and size.</summary>
    public ElementDocument? ReloadDocument(ElementDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        System.Diagnostics.Debug.Assert(document.GetContext() == _rmlContext);

        // Keep some properties of the old document
        var oldPosition = document.GetAbsoluteOffset(BoxArea.Border);
        var oldModal = document.IsModal() ? ModalFlag.Modal : ModalFlag.None;
        var oldVisible = document.IsVisible();

        var oldFocusedElement = _rmlContext!.GetFocusElement();
        var focus = oldFocusedElement is not null && oldFocusedElement.GetOwnerDocument() == document
            ? FocusFlag.Document
            : FocusFlag.Auto;

        var oldLeftProperty = document.GetProperty(PropertyId.Left);
        var oldTopProperty = document.GetProperty(PropertyId.Top);
        var oldWidthProperty = document.GetProperty(PropertyId.Width);
        var oldHeightProperty = document.GetProperty(PropertyId.Height);

        // Retrieve placeholder context from original document
        var defaultPlaceholder = nint.Zero;
        var placeholderContext = document.GetAttribute(ComponentPtrAttribute, in defaultPlaceholder);

        // Try to reload document
        using var sourceUrl = document.GetSourceURL();
        var newDocument = LoadDocument(sourceUrl.ToString(), placeholderContext);
        if (newDocument is null)
            return null;

        // Setup persistent properties
        if (oldLeftProperty is not null)
            newDocument.SetProperty(PropertyId.Left, oldLeftProperty);
        if (oldTopProperty is not null)
            newDocument.SetProperty(PropertyId.Top, oldTopProperty);
        if (oldWidthProperty is not null)
            newDocument.SetProperty(PropertyId.Width, oldWidthProperty);
        if (oldHeightProperty is not null)
            newDocument.SetProperty(PropertyId.Height, oldHeightProperty);
        newDocument.UpdateDocument();

        if (oldVisible)
            newDocument.Show(oldModal, focus);

        var args = new RmlDocumentReloadedArgs
        {
            UnloadedDocument = document,
            LoadedDocument = newDocument
        };
        OnDocumentReloaded?.Invoke(args);

        document.Close();
        _ = oldPosition;
        return newDocument;
    }

    /// <summary>Emitted when mouse input is detected.</summary>
    public event RmlMouseMoveHandler? OnMouseMoveEvent;
    /// <summary>Emitted when a window document owned by this subsystem is closed.</summary>
    public event Action<ElementDocument>? OnDocumentClosedEvent;
    /// <summary>Emitted when underlying UI canvas is resized.</summary>
    public event Action<RmlCanvasResizedArgs>? OnCanvasResizedEvent;
    /// <summary>Emitted when automatic resource reloading triggers reload of a document.</summary>
    public event Action<RmlDocumentReloadedArgs>? OnDocumentReloaded;
    /// <summary>Emitted when documents are updated.</summary>
    public event Action? OnUpdated;

    /// <summary>Register UI library objects.</summary>
    public static void RegisterRmlUILibrary(CoreContext context)
    {
        context.RegisterAssembly(typeof(RmlUI).Assembly);
    }

    internal static void ReleaseEventListener(EventListener listener)
    {
        EventListeners.Remove(listener.Handle);
    }

    /// <summary>Try to convert variant from RmlUI to RedBolt.</summary>
    public static bool FromRmlUi(RmlVariant source, out CoreVariant destination)
    {
        switch (source.GetVariantType())
        {
            case RmlVariantType.None:
                destination = default;
                return true;
            case RmlVariantType.Bool:
                destination = new CoreVariant(source.Get<bool>());
                return true;
            case RmlVariantType.Int:
                destination = new CoreVariant(source.Get<int>());
                return true;
            case RmlVariantType.Int64:
                destination = new CoreVariant(source.Get<long>());
                return true;
            case RmlVariantType.Float:
                destination = new CoreVariant(source.Get<float>());
                return true;
            case RmlVariantType.Double:
                destination = new CoreVariant(source.Get<double>());
                return true;
            case RmlVariantType.String:
            {
                using var value = source.Get<Std.String>();
                destination = new CoreVariant(value.ToString());
                return true;
            }
            case RmlVariantType.Voidptr:
                destination = new CoreVariant(source.Get<nint>());
                return true;
            case RmlVariantType.Vector2:
                destination = new CoreVariant(source.Get<Vector2f>());
                return true;
            case RmlVariantType.Vector3:
                destination = new CoreVariant(source.Get<Vector3f>());
                return true;
            case RmlVariantType.Vector4:
                destination = new CoreVariant(source.Get<Vector4f>());
                return true;
            case RmlVariantType.Colourf:
                destination = new CoreVariant(source.Get<Color>());
                return true;
            case RmlVariantType.Colourb:
            {
                var value = source.Get<RmlUiLegacyColourb>();
                destination = new CoreVariant(new Color(value.Red, value.Green, value.Blue, value.Alpha) * (1.0f / 255.0f));
                return true;
            }
            case RmlVariantType.Byte:
                destination = new CoreVariant((uint)source.Get<byte>());
                return true;
            case RmlVariantType.Char:
                destination = new CoreVariant((int)source.Get<sbyte>());
                return true;
            case RmlVariantType.Uint:
                destination = new CoreVariant(source.Get<uint>());
                return true;
            case RmlVariantType.Uint64:
                destination = new CoreVariant(source.Get<ulong>());
                return true;
            case RmlVariantType.Scriptinterface:
            case RmlVariantType.Transformptr:
            case RmlVariantType.Transitionlist:
            case RmlVariantType.Animationlist:
            case RmlVariantType.Decoratorsptr:
            case RmlVariantType.Filtersptr:
            case RmlVariantType.Fonteffectsptr:
            case RmlVariantType.Colorstoplist:
            case RmlVariantType.Boxshadowlist:
                break;
        }

        using var stringValue = source.Get<Std.String>();
        destination = new CoreVariant(stringValue.ToString());
        return true;
    }

    /// <summary>Try to convert variant from RedBolt to RmlUI.</summary>
    public static bool ToRmlUi(in CoreVariant source, RmlVariant destination)
    {
        switch (source.Type)
        {
            case CoreVariantType.None:
                destination.Clear();
                return true;
            case CoreVariantType.Bool:
                destination.Assign(source.GetBool());
                return true;
            case CoreVariantType.Int:
                destination.Assign(source.GetInt());
                return true;
            case CoreVariantType.Int64:
                destination.Assign(source.GetInt64());
                return true;
            case CoreVariantType.Float:
                destination.Assign(source.GetFloat());
                return true;
            case CoreVariantType.Double:
                destination.Assign(source.GetDouble());
                return true;
            case CoreVariantType.String:
            {
                using var assignedString = new Std.String();
                assignedString.Assign(source.GetString());
                destination.Assign(assignedString);
                return true;
            }
            case CoreVariantType.VoidPtr:
                destination.Assign(source.GetVoidPtr());
                return true;
            case CoreVariantType.Vector2:
                destination.Assign(source.GetVector2());
                return true;
            case CoreVariantType.Vector3:
                destination.Assign(source.GetVector3());
                return true;
            case CoreVariantType.Vector4:
                destination.Assign(source.GetVector4());
                return true;
            case CoreVariantType.Color:
                destination.Assign(source.GetColor());
                return true;
            case CoreVariantType.Quaternion:
            case CoreVariantType.Buffer:
            case CoreVariantType.ResourceRef:
            case CoreVariantType.ResourceRefList:
            case CoreVariantType.VariantVector:
            case CoreVariantType.VariantMap:
            case CoreVariantType.IntRect:
            case CoreVariantType.Vector2i:
            case CoreVariantType.Ptr:
            case CoreVariantType.Matrix3:
            case CoreVariantType.Matrix3x4:
            case CoreVariantType.Matrix4:
            case CoreVariantType.StringVector:
            case CoreVariantType.Rect:
            case CoreVariantType.Vector3i:
            case CoreVariantType.Custom:
            case CoreVariantType.VariantCurve:
            case CoreVariantType.StringVariantMap:
            case CoreVariantType.DoubleVector2:
            case CoreVariantType.DoubleVector3:
            case CoreVariantType.CString:
                break;
        }

        var stringValue = source.ToString();
        if (stringValue.Length == 0)
        {
            EngineLog.GetLogger().Error("This variant type conversion is not supported: {}", source.Type);
            return false;
        }
        using var nativeString = new Std.String();
        nativeString.Assign(stringValue);
        destination.Assign(nativeString);
        return true;
    }

    internal static VariantDefinitions RegisterVariantDefinition(DataTypeRegister typeRegister)
    {
        return new VariantDefinitions(typeRegister);
    }

    internal static string InsertVariablePlaceholders(string content, nint dataModelId)
    {
        // Replace special placeholders in .rml file contents and model names to allow deriving unique model names
        return content.Replace("{{__data_model_id}}", dataModelId.ToString(), StringComparison.Ordinal);
    }

    private static MouseButton MakeTouchIDMask(int id)
    {
        return (MouseButton)(1u << id);
    }

    private static int MouseButtonUrho3DToRml(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => 0,
            MouseButton.Middle => 2,
            MouseButton.Right => 1,
            MouseButton.X1 => 3,
            MouseButton.X2 => 4,
            _ => -1
        };
    }

    private static int ModifiersUrho3DToRml(Qualifier modifier)
    {
        var rmlModifiers = 0;
        if ((modifier & Qualifier.Alt) != 0)
            rmlModifiers |= (int)Rml.Input.KeyModifier.KmAlt;
        if ((modifier & Qualifier.Ctrl) != 0)
            rmlModifiers |= (int)Rml.Input.KeyModifier.KmCtrl;
        if ((modifier & Qualifier.Shift) != 0)
            rmlModifiers |= (int)Rml.Input.KeyModifier.KmShift;
        return rmlModifiers;
    }

    /// <summary>Returns a size that this UI screen will cover.</summary>
    private Vector2i GetDesiredCanvasSize()
    {
        var renderSurface = GetRenderSurface();
        if (renderSurface is not null)
            return new Vector2i(renderSurface.GetWidth(), renderSurface.GetHeight());

        var graphics = CoreContext.Instance.GetSubsystem<Graphics>();
        if (graphics is not null)
            return new Vector2i(graphics.Width, graphics.Height);

        // Irrelevant. Canvas will soon be resized once actual screen mode comes in.
        return new Vector2i(512, 512);
    }

    /// <summary>Return true if input is captured by this instance of RmlUI.</summary>
    private bool IsInputCapturedInternal()
    {
        var element = _rmlContext!.GetFocusElement();
        if (element is null)
            return false;
        using var tag = element.GetTagName();
        var tagName = tag.ToString();
        return tagName is "input" or "textarea" or "select";
    }

    /// <summary>Signal that document belonging to this subsystem was closed.</summary>
    private void OnDocumentUnload(ElementDocument document)
    {
        OnDocumentClosedEvent?.Invoke(document);
    }

    private RenderSurface? GetRenderSurface()
    {
        return _renderSurface is not null && _renderSurface.TryGetTarget(out var renderSurface)
            ? renderSurface
            : null;
    }

    /// <summary>Handle screen mode event.</summary>
    private void HandleScreenMode(ref GraphicsEvents.ScreenModeEvent eventData)
    {
        var args = new RmlCanvasResizedArgs
        {
            OldSize = _rmlContext!.GetDimensions(),
            NewSize = GetDesiredCanvasSize()
        };
        _rmlContext.SetDimensions(args.NewSize);
        OnCanvasResizedEvent?.Invoke(args);
    }

    /// <summary>Handle mouse button down event.</summary>
    private void HandleMouseButtonDown(ref InputEvents.MouseButtonDownEvent eventData)
    {
        var button = MouseButtonUrho3DToRml(eventData.Button);
        var modifiers = ModifiersUrho3DToRml(eventData.Qualifiers);

        // Manage focus between multiple RmlUI contexts. If no element or root element is hovered - unfocus focused input element.
        if (!IsHovered() && IsInputCapturedInternal())
        {
            var isTextInputActive = _system!.TextInputActivatedThisFrame();
            _rmlContext!.GetFocusElement()!.Blur();
            if (isTextInputActive)
                GetSubsystem<Input>().SetScreenKeyboardVisible(true);
            // Do not process click as it is clearly not meant for this context.
            return;
        }
        _rmlContext!.ProcessMouseButtonDown(button, modifiers);
    }

    /// <summary>Handle mouse button up event.</summary>
    private void HandleMouseButtonUp(ref InputEvents.MouseButtonUpEvent eventData)
    {
        var button = MouseButtonUrho3DToRml(eventData.Button);
        var modifiers = ModifiersUrho3DToRml(eventData.Qualifiers);
        _rmlContext!.ProcessMouseButtonUp(button, modifiers);
    }

    /// <summary>Handle mouse move event.</summary>
    private void HandleMouseMove(ref InputEvents.MouseMoveEvent eventData)
    {
        var modifiers = ModifiersUrho3DToRml(eventData.Qualifiers);
        var pos = new Vector2i(eventData.X, eventData.Y);
        OnMouseMoveEvent?.Invoke(ref pos);
        if (pos.X >= 0 && pos.Y >= 0)
            _rmlContext!.ProcessMouseMove(pos.X, pos.Y, modifiers);
    }

    /// <summary>Handle mouse wheel event.</summary>
    private void HandleMouseWheel(ref InputEvents.MouseWheelEvent eventData)
    {
        var input = GetSubsystem<Input>();
        if (input.IsMouseGrabbed())
            return;
        var modifiers = ModifiersUrho3DToRml(eventData.Qualifiers);
        _rmlContext!.ProcessMouseWheel(-eventData.Wheel, modifiers);
    }

    /// <summary>Handle touch begin event.</summary>
    private void HandleTouchBegin(ref InputEvents.TouchBeginEvent eventData)
    {
        var input = GetSubsystem<Input>();
        if (input.IsMouseGrabbed())
            return;
        var touchId = MakeTouchIDMask(eventData.TouchID);
        var modifiers = ModifiersUrho3DToRml(input.GetQualifiers());
        var button = MouseButtonUrho3DToRml(touchId);
        var pos = new Vector2i(eventData.X, eventData.Y);
        OnMouseMoveEvent?.Invoke(ref pos);
        if (pos.X >= 0 && pos.Y >= 0)
            _rmlContext!.ProcessMouseMove(pos.X, pos.Y, modifiers);
        _rmlContext!.ProcessMouseButtonDown(button, modifiers);
    }

    /// <summary>Handle touch end event.</summary>
    private void HandleTouchEnd(ref InputEvents.TouchEndEvent eventData)
    {
        var input = GetSubsystem<Input>();
        var touchId = MakeTouchIDMask(eventData.TouchID);
        var modifiers = ModifiersUrho3DToRml(input.GetQualifiers());
        var button = MouseButtonUrho3DToRml(touchId);
        _rmlContext!.ProcessMouseMove(eventData.X, eventData.Y, modifiers);
        _rmlContext.ProcessMouseButtonUp(button, modifiers);
    }

    /// <summary>Handle touch move event.</summary>
    private void HandleTouchMove(ref InputEvents.TouchMoveEvent eventData)
    {
        var input = GetSubsystem<Input>();
        // const MouseButton touchId = MakeTouchIDMask(eventData[P_TOUCHID].GetInt());
        var modifiers = ModifiersUrho3DToRml(input.GetQualifiers());
        var pos = new Vector2i(eventData.X, eventData.Y);
        OnMouseMoveEvent?.Invoke(ref pos);
        if (pos.X >= 0 && pos.Y >= 0)
            _rmlContext!.ProcessMouseMove(pos.X, pos.Y, modifiers);
    }

    /// <summary>Handle press event.</summary>
    private void HandleKeyDown(ref InputEvents.KeyDownEvent eventData)
    {
        if (!KeyMap.TryGetValue(eventData.Key, out var key))
            return;
        var modifiers = ModifiersUrho3DToRml(eventData.Qualifiers);
        _rmlContext!.ProcessKeyDown(key, modifiers);
        if (key is Rml.Input.KeyIdentifier.KiReturn or Rml.Input.KeyIdentifier.KiNumpadenter)
            _rmlContext.ProcessTextInput((sbyte)'\n');
    }

    /// <summary>Handle release event.</summary>
    private void HandleKeyUp(ref InputEvents.KeyUpEvent eventData)
    {
        if (!KeyMap.TryGetValue(eventData.Key, out var key))
            return;
        var modifiers = ModifiersUrho3DToRml(eventData.Qualifiers);
        _rmlContext!.ProcessKeyUp(key, modifiers);
    }

    /// <summary>Handle text input event.</summary>
    private void HandleTextInput(ref InputEvents.TextInputEvent eventData)
    {
        _textInput.Assign(eventData.Text);
        _rmlContext!.ProcessTextInput(_textInput);
    }

    /// <summary>Handle logic post-update event.</summary>
    private void HandlePostUpdate(ref CoreEvents.PostUpdateEvent eventData)
    {
        Update(eventData.TimeStep);
    }

    /// <summary>Handle a file being drag-dropped into the application window.</summary>
    private void HandleDropFile(ref InputEvents.DropFileEvent eventData)
    {
        var input = GetSubsystem<Input>();

        // Sending the UI variant of the event only makes sense if the OS cursor is visible (not locked to window center)
        if (!input.IsMouseVisible())
            return;

        if (_rmlContext!.GetHoverElement() is { } element)
        {
            using var args = new Std.UnorderedMap<Std.String, RmlVariant>();
            using var pathName = new Std.String();
            using var pathValue = new Std.String();
            using var pathVariant = new RmlVariant();
            using var eventType = new Std.String();
            pathName.Assign("path");
            pathValue.Assign(eventData.FileName);
            pathVariant.Assign(pathValue);
            args.Add(pathName, pathVariant);
            eventType.Assign("dropfile");
            element.DispatchEvent(eventType, args);
        }
    }

    /// <summary>Handle rendering to a texture.</summary>
    private void HandleEndAllViewsRender(ref GraphicsEvents.EndAllViewsRenderEvent eventData)
    {
        if (_isRendering)
            Render();
    }

    /// <summary>Handle resource reloading.</summary>
    private void HandleResourceReloaded(ref ResourceEvents.FileChanged eventData)
    {
        var resourceName = eventData.ResourceName;
        if (_file!.IsResourceLoaded(resourceName))
        {
            _file.ClearLoadedResources();

            RmlUi.ReleaseTextures();
            Factory.ClearStyleSheetCache();
            Factory.ClearTemplateCache();

            List<ElementDocument> unloadingDocuments = [];
            for (var i = 0; i < _rmlContext!.GetNumDocuments(); ++i)
            {
                var document = _rmlContext.GetDocument(i);
                if (document is null)
                    continue;
                using var sourceUrl = document.GetSourceURL();
                if (sourceUrl.ToString().Length != 0)
                    unloadingDocuments.Add(document);
            }

            foreach (var document in unloadingDocuments)
                ReloadDocument(document);
        }
    }
}
