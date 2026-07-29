// Copyright (c) 2017-2024 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.RmlUI;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Bindings.RmlUi;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using CoreVariant = RedBolt.Core.Variant;
using EngineLog = RedBolt.Logging.Log;
using RmlVariant = RedBolt.Bindings.RmlUi.Variant;

/// <summary>Adds a single window to game screen.</summary>
[Reflected(ObjectCategory.Category_RmlUI)]
public partial class RmlUIComponent : LogicComponent
{
    public delegate void GetterFunc(ref CoreVariant value);
    public delegate void SetterFunc(in CoreVariant value);
    public delegate void EventFunc(VariantVector arguments);

    /// <summary>Construct.</summary>
    public RmlUIComponent()
    {
        _resource = new ResourceRef(ObjectReflection<BinaryFile>.TypeId);
        _componentHandle = GCHandle.Alloc(this);
        SetUpdateEventMask(UpdateEvent.UseUpdate);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        CloseInternal();
        if (_componentHandle.IsAllocated)
            _componentHandle.Free();
        base.Dispose();
    }

    /// <summary>Implement LogicComponent.</summary>
    public override void Update(float timeStep)
    {
        // There should be only a few of RmlUIComponent enabled at a time, so this is not a performance issue.
        UpdateConnectedCanvas();
    }

    public bool BindDataModelProperty(string name, GetterFunc getter, SetterFunc setter)
    {
        var constructor = ExpectDataModelConstructor();
        if (constructor is null)
        {
            return false;
        }

        using var nativeName = new Std.String();
        nativeName.Assign(name);
        return constructor.BindFunc(nativeName, outputValue =>
        {
            var variant = new CoreVariant();
            try
            {
                getter(ref variant);
                RmlUI.ToRmlUi(variant, outputValue);
            }
            finally
            {
                variant.Dispose();
            }
        }, inputValue =>
        {
            if (RmlUI.FromRmlUi(inputValue, out var variant))
            {
                using (variant)
                    setter(in variant);
            }
        });
    }

    public bool BindDataModelVariant(string name, StrongBox<CoreVariant> value)
    {
        var constructor = ExpectDataModelConstructor();
        if (constructor is null || _variantDefinitions is null)
        {
            return false;
        }

        using var nativeName = new Std.String();
        nativeName.Assign(name);
        using var dataVariable = _variantDefinitions.Create(value);
        return constructor.BindCustomDataVariable(nativeName, dataVariable);
    }

    public bool BindDataModelVariantVector(string name, VariantVector value)
    {
        var constructor = ExpectDataModelConstructor();
        if (constructor is null || _variantDefinitions is null)
        {
            return false;
        }

        using var nativeName = new Std.String();
        nativeName.Assign(name);
        using var dataVariable = _variantDefinitions.Create(value);
        return constructor.BindCustomDataVariable(nativeName, dataVariable);
    }

    public bool BindDataModelVariantMap(string name, VariantMap value)
    {
        var constructor = ExpectDataModelConstructor();
        if (constructor is null || _variantDefinitions is null)
        {
            return false;
        }

        using var nativeName = new Std.String();
        nativeName.Assign(name);
        using var dataVariable = _variantDefinitions.Create(value);
        return constructor.BindCustomDataVariable(nativeName, dataVariable);
    }

    public bool BindDataModelStringVector(string name, StringVector value)
    {
        var constructor = ExpectDataModelConstructor();
        if (constructor is null || _variantDefinitions is null)
        {
            return false;
        }

        using var nativeName = new Std.String();
        nativeName.Assign(name);
        using var dataVariable = _variantDefinitions.Create(value);
        return constructor.BindCustomDataVariable(nativeName, dataVariable);
    }

    public bool BindDataModelEvent(string name, EventFunc eventCallback)
    {
        var constructor = ExpectDataModelConstructor();
        if (constructor is null)
        {
            return false;
        }

        using var nativeName = new Std.String();
        nativeName.Assign(name);
        return constructor.BindEventCallback(nativeName, (model, @event, args) =>
        {
            using var urhoArgs = new VariantVector();
            foreach (var src in args)
            {
                if (RmlUI.FromRmlUi(src, out var argument))
                {
                    urhoArgs.Add(argument);
                    argument.Dispose();
                }
            }
            eventCallback(urhoArgs);
        });
    }

    /// <summary>Mark data model variable dirty. Variable is synchronized with RmlUI on next update.</summary>
    protected bool IsVariableDirty(string variableName)
    {
        if (_dataModel is null)
            return false;
        using var nativeName = new Std.String();
        nativeName.Assign(variableName);
        return _dataModel.IsVariableDirty(nativeName);
    }

    protected void DirtyVariable(string variableName)
    {
        if (_dataModel is null)
            return;
        using var nativeName = new Std.String();
        nativeName.Assign(variableName);
        _dataModel.DirtyVariable(nativeName);
    }

    /// <summary>Mark all data model variables dirty. Variables are synchronized with RmlUI on next update.</summary>
    protected void DirtyAllVariables()
    {
        _dataModel?.DirtyAllVariables();
    }

    protected static Action<DataModelHandle, Event, Std.Vector<RmlVariant>> WrapCallback(Action callback)
    {
        return (model, @event, args) =>
        {
            var enabled = args.Count == 0 || args[0].Get<bool>();
            if (enabled)
                callback();
        };
    }

    protected static Action<DataModelHandle, Event, Std.Vector<RmlVariant>> WrapCallback(
        Action<Std.Vector<RmlVariant>> callback)
    {
        return (model, @event, args) =>
        {
            var enabled = args.Count == 0 || args[0].Get<bool>();
            if (enabled)
                callback(args);
        };
    }

    /// <summary>Returns instance of the component from the document.</summary>
    public static RmlUIComponent? FromDocument(ElementDocument? document)
    {
        if (document is not null)
        {
            var defaultValue = nint.Zero;
            var value = document.GetAttribute(RmlUI.ComponentPtrAttribute, in defaultValue);
            if (value != 0)
                return GCHandle.FromIntPtr(value).Target as RmlUIComponent;
        }
        return null;
    }

    public void SetResource(ResourceRef resourceRef)
    {
        _resource = resourceRef;
        if (_resource.Type == StringHash.Empty)
            _resource.Type = ObjectReflection<BinaryFile>.TypeId;
        UpdateDocumentOpen();
    }

    public void SetResource(string resourceName)
    {
        var resourceRef = new ResourceRef(ObjectReflection<BinaryFile>.TypeId, resourceName);
        SetResource(resourceRef);
    }

    [Serialized("Resource", DefaultSource = nameof(GetResourceDefault), Setter = nameof(SetResource))]
    public ResourceRef GetResource() => _resource;

    public bool GetUseNormalizedCoordinates() => _useNormalized;

    public void SetUseNormalizedCoordinates(bool enable) => _useNormalized = enable;

    [Serialized("Position", DefaultSource = nameof(GetPositionDefault), Setter = nameof(SetPosition))]
    public Vector2f GetPosition()
    {
        if (_document is null)
            return _position;

        var pos = _document.GetAbsoluteOffset(BoxArea.Border);
        if (_useNormalized)
        {
            var canvasSize = _document.GetContext().GetDimensions();
            pos.X = 1.0f / canvasSize.X * pos.X;
            pos.Y = 1.0f / canvasSize.Y * pos.Y;
        }
        return pos;
    }

    public void SetPosition(Vector2f pos)
    {
        if (_document is null)
        {
            _position = pos;
            return;
        }

        if (pos == Vector2f.Zero)
            return;

        if (_useNormalized)
        {
            var canvasSize = _document.GetContext().GetDimensions();
            pos.X = MathDefs.Round(canvasSize.X * pos.X);
            pos.Y = MathDefs.Round(canvasSize.Y * pos.Y);
        }
        using var left = new Property(pos.X, Unit.Px);
        using var top = new Property(pos.Y, Unit.Px);
        _document.SetProperty(PropertyId.Left, left);
        _document.SetProperty(PropertyId.Top, top);
        _document.UpdateDocument();
    }

    [Serialized("Size", DefaultSource = nameof(GetSizeDefault), Setter = nameof(SetSize))]
    public Vector2f GetSize()
    {
        if (_document is null)
            return _size;

        if (_autoSize)
            return Vector2f.Zero;

        var size = _document.GetBox().GetSize(BoxArea.Content);
        if (_useNormalized)
        {
            var canvasSize = _document.GetContext().GetDimensions();
            size.X = 1.0f / canvasSize.X * size.X;
            size.Y = 1.0f / canvasSize.Y * size.Y;
        }
        return size;
    }

    public void SetSize(Vector2f size)
    {
        if (_document is null)
        {
            _size = size;
            return;
        }

        if (size == Vector2f.Zero || _autoSize)
            return;

        if (_useNormalized)
        {
            var canvasSize = _document.GetContext().GetDimensions();
            size.X = MathDefs.Round(canvasSize.X * size.X);
            size.Y = MathDefs.Round(canvasSize.Y * size.Y);
        }
        using var width = new Property(size.X, Unit.Px);
        using var height = new Property(size.Y, Unit.Px);
        _document.SetProperty(PropertyId.Width, width);
        _document.SetProperty(PropertyId.Height, height);
        _document.UpdateDocument();
    }

    public void SetAutoSize(bool enable) => _autoSize = enable;

    public bool GetAutoSize() => _autoSize;

    /// <summary>Return RmlUI subsystem this component renders into.</summary>
    public RmlUI GetUI()
    {
        if (_canvasComponent is not null && _canvasComponent.TryGetTarget(out var canvasComponent))
            return canvasComponent.GetUI();
        return GetSubsystem<RmlUI>()!;
    }

    /// <summary>Return currently open document, may be null.</summary>
    public ElementDocument? GetDocument() => _document;

    /// <summary>Set current document body element font size in pixels, also known as "em" unit.</summary>
    public void SetEmSize(float sizePx)
    {
        if (_document is null)
        {
            return;
        }

        using var fontSize = new Property(sizePx, Unit.Px);
        _document.SetProperty(PropertyId.FontSize, fontSize);
    }

    /// <summary>Return current document body element font size in pixels.</summary>
    public float GetEmSize()
    {
        if (_document is null)
        {
            return 0.0f;
        }

        using var numericValue = _document.GetProperty(PropertyId.FontSize)!.GetNumericValue();
        return _document.ResolveLength(numericValue);
    }

    /// <summary>Return whether the document is modal.</summary>
    public bool IsModal() => _modal;

    /// <summary>Set whether the document is modal.</summary>
    public void SetModal(bool modal)
    {
        if (_modal == modal)
            return;

        _modal = modal;
        if (_document is null)
            return;

        if (_document.IsVisible())
        {
            var oldFocusedElement = _document.GetContext().GetFocusElement();
            var focus = oldFocusedElement is not null && ReferenceEquals(oldFocusedElement.GetOwnerDocument(), _document)
                ? FocusFlag.Document
                : FocusFlag.Auto;

            _document.Hide();
            _document.Show(modal ? ModalFlag.Modal : ModalFlag.None, focus);
        }
    }

    /// <summary>Focus document.</summary>
    public void Focus(bool focusVisible)
    {
        _document?.Focus(focusVisible);
    }

    /// <summary>Called when the data model was created. Bind your data and event callbacks here.</summary>
    protected virtual string GetDataModelName() => "{{__data_model_id}}";

    protected virtual void OnDataModelInitialized()
    {
    }

    protected virtual void OnDocumentPreLoad()
    {
    }

    protected virtual void OnDocumentPostLoad()
    {
    }

    protected virtual void OnDocumentPreUnload()
    {
    }

    protected virtual void OnDocumentPostUnload()
    {
    }

    /// <summary>Called after all pending changes are processed by RmlUI.</summary>
    protected virtual void OnDocumentUpdated()
    {
        UpdatePendingFocus();
    }

    /// <summary>Return data model constructor. Only available in OnDataModelInitialized method.</summary>
    protected DataModelConstructor? GetDataModelConstructor() => _modelConstructor;

    /// <summary>If current focus is invalid, focus on the first valid navigable element.</summary>
    protected bool RestoreFocus()
    {
        if (_document is not null && _document.IsVisible() && !_suppressRestoreFocus &&
            !IsElementNavigable(_document.GetFocusLeafNode()))
        {
            var nextElement = _document.FindNextTabElement(_document, true);
            if (nextElement is not null && nextElement.Focus(true))
            {
                using var options = new ScrollIntoViewOptions(ScrollAlignment.Nearest);
                nextElement.ScrollIntoView(options);
                return true;
            }
        }
        return false;
    }

    /// <summary>Schedule element focus by ID on next update. This is useful when focus-ability is not updated yet.</summary>
    /// <summary>Empty id focuses on any focusable element.</summary>
    protected void ScheduleFocusById(string elementId)
    {
        _pendingFocusId = elementId;
    }

    /// <summary>Implement Component.</summary>
    public override void OnSetEnabled()
    {
        base.OnSetEnabled();
        UpdateDocumentOpen();
    }

    /// <summary>Implement Component.</summary>
    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        if (_node is not null)
            UpdateConnectedCanvas();
        UpdateDocumentOpen();
    }

    /// <summary>Get data model constructor. Logs error if the constructor is not available.</summary>
    private DataModelConstructor? ExpectDataModelConstructor()
    {
        var constructor = GetDataModelConstructor();
        if (constructor is null)
        {
            EngineLog.GetLogger().Error("BindDataModelProperty can only be executed from OnDataModelInitialized");
        }
        return constructor;
    }

    private void UpdatePendingFocus()
    {
        _suppressRestoreFocus = false;

        if (_pendingFocusId is not null && _document is not null)
        {
            if (_pendingFocusId.Length == 0)
            {
                // If failed to restore focus, keep trying
                if (RestoreFocus())
                    _pendingFocusId = null;
            }
            else
            {
                using var elementId = new Std.String();
                elementId.Assign(_pendingFocusId);
                var element = _document.GetElementById(elementId);
                if (element is not null && element.Focus(true))
                {
                    using var options = new ScrollIntoViewOptions(ScrollAlignment.Nearest);
                    element.ScrollIntoView(options);
                    _suppressRestoreFocus = true;
                }
                _pendingFocusId = null;
            }
        }
    }

    /// <summary>Open a window document if it was not already open.</summary>
    private void OpenInternal()
    {
        if (_document is not null)
            return; // Already open.

        if (_resource.Name.Length == 0)
        {
            EngineLog.GetLogger().Error("UI document can be opened after setting resource path.");
            return;
        }

        var ui = GetUI();
        ui.OnDocumentClosedEvent += OnDocumentClosed;
        ui.OnCanvasResizedEvent += OnUICanvasResized;
        ui.OnDocumentReloaded += OnDocumentReloaded;
        ui.OnUpdated += OnDocumentUpdated;

        OnDocumentPreLoad();

        if (_dataModel is null)
        {
            CreateDataModel();
            OnDataModelInitialized();
            _dataModel = _modelConstructor!.GetModelHandle();
            _modelConstructor.Dispose();
            _modelConstructor = null;
        }

        // Load document from resource with placeholder substitution.
        SetDocument(ui.LoadDocument(_resource.Name, GCHandle.ToIntPtr(_componentHandle)));

        if (_document is null)
        {
            EngineLog.GetLogger().Error("Failed to load UI document: {}", _resource.Name);
            return;
        }

        SetPosition(_position);
        SetSize(_size);
        _document.Show(_modal ? ModalFlag.Modal : ModalFlag.None, FocusFlag.None);
        OnDocumentPostLoad();
        RestoreFocus();
    }

    /// <summary>Close a window document if it was open.</summary>
    private void CloseInternal()
    {
        if (_document is null)
            return; // Already closed.

        var ui = ((RmlUI.RmlContext)_document.GetContext()).GetOwnerSubsystem()!;
        ui.OnDocumentClosedEvent -= OnDocumentClosed;
        ui.OnCanvasResizedEvent -= OnUICanvasResized;
        ui.OnDocumentReloaded -= OnDocumentReloaded;
        ui.OnUpdated -= OnDocumentUpdated;

        _position = GetPosition();
        _size = GetSize();

        OnDocumentPreUnload();
        _document.Close();
        SetDocument(null);

        if (_dataModel is not null)
            RemoveDataModel();

        OnDocumentPostUnload();
    }

    /// <summary>Handle subsystem events.</summary>
    private void OnDocumentClosed(ElementDocument document)
    {
        if (ReferenceEquals(_document, document))
        {
            SetDocument(null);
        }
    }

    /// <summary>Handle subsystem events.</summary>
    private void OnUICanvasResized(RmlCanvasResizedArgs args)
    {
        if (!_useNormalized)
            // Element is positioned using absolute pixel values. Nothing to adjust.
            return;

        // When using normalized coordinates, element is positioned relative to canvas size. When canvas is resized old
        // positions are no longer valid. Convert pixel size and position back to normalized coordiantes using old size and
        // reapply them, which will use new canvas size for calculating new pixel position and size.

        var pos = _document!.GetAbsoluteOffset(BoxArea.Border);
        var size = _document.GetBox().GetSize(BoxArea.Content);

        pos.X = 1.0f / args.OldSize.X * pos.X;
        pos.Y = 1.0f / args.OldSize.Y * pos.Y;
        size.X = 1.0f / args.OldSize.X * size.X;
        size.Y = 1.0f / args.OldSize.Y * size.Y;

        SetPosition(pos);
        SetSize(size);
    }

    /// <summary>Handle subsystem events.</summary>
    private void OnDocumentReloaded(RmlDocumentReloadedArgs args)
    {
        if (ReferenceEquals(_document, args.UnloadedDocument))
            SetDocument(args.LoadedDocument);
    }

    /// <summary>Set currently active document and link it to the component.</summary>
    private void SetDocument(ElementDocument? document)
    {
        if (!ReferenceEquals(_document, document))
        {
            if (_document is not null)
                _document.SetAttribute(RmlUI.ComponentPtrAttribute, nint.Zero);

            _document = document;

            if (_document is not null)
                _document.SetAttribute(RmlUI.ComponentPtrAttribute, GCHandle.ToIntPtr(_componentHandle));
        }
    }

    private void DoFocusById(DataModelHandle model, Event @event, Std.Vector<RmlVariant> args)
    {
        if (args.Count != 2)
        {
            EngineLog.GetLogger().Warning("RmlUIComponent::DoFocusById is called with unexpected arguments");
            return;
        }

        var enabled = args.Count > 0 ? args[0].Get<bool>() : true;
        using var id = args[1].Get<Std.String>();
        if (enabled)
            ScheduleFocusById(id.ToString());
    }

    private void CreateDataModel()
    {
        var ui = GetUI();
        var context = ui.GetRmlContext();

        _dataModelName = RmlUI.InsertVariablePlaceholders(
            GetDataModelName(), GCHandle.ToIntPtr(_componentHandle));

        _typeRegister = new DataTypeRegister();
        using var nativeModelName = new Std.String();
        nativeModelName.Assign(_dataModelName);
        _modelConstructor = context.CreateDataModel(nativeModelName, _typeRegister);
        _variantDefinitions = RmlUI.RegisterVariantDefinition(_typeRegister);

        using var focusName = new Std.String();
        focusName.Assign("focus");
        _modelConstructor.BindEventCallback(focusName, DoFocusById);

        using var keyOkName = new Std.String();
        keyOkName.Assign("is_key_ok");
        _modelConstructor.RegisterTransformFunc(keyOkName, IsKeyOk);
        using var keyCancelName = new Std.String();
        keyCancelName.Assign("is_key_cancel");
        _modelConstructor.RegisterTransformFunc(keyCancelName, IsKeyCancel);
        using var keySecondaryName = new Std.String();
        keySecondaryName.Assign("is_key_secondary");
        _modelConstructor.RegisterTransformFunc(keySecondaryName, IsKeySecondary);
        using var keyTertiaryName = new Std.String();
        keyTertiaryName.Assign("is_key_tertiary");
        _modelConstructor.RegisterTransformFunc(keyTertiaryName, IsKeyTertiary);
    }

    private void RemoveDataModel()
    {
        var ui = GetUI();
        var context = ui.GetRmlContext();
        using var nativeModelName = new Std.String();
        nativeModelName.Assign(_dataModelName);
        context.RemoveDataModel(nativeModelName);

        _dataModel!.Dispose();
        _dataModel = null;
        _variantDefinitions!.Dispose();
        _variantDefinitions = null;
        _typeRegister!.Dispose();
        _typeRegister = null;
        _dataModelName = string.Empty;
    }

    private void UpdateDocumentOpen()
    {
        var shouldBeOpen = IsEnabledEffective && _resource.Name.Length != 0;
        var isOpen = _document is not null;

        if (shouldBeOpen && !isOpen)
            OpenInternal();
        else if (!shouldBeOpen && isOpen)
            CloseInternal();
    }

    private void UpdateConnectedCanvas()
    {
        var newCanvasComponent = IsEnabledEffective ? _node!.GetComponent<RmlCanvasComponent>() : null;
        var canvasComponent = GetCanvasComponent();
        if (!ReferenceEquals(canvasComponent, newCanvasComponent))
        {
            var wasOpen = _document is not null;
            CloseInternal();
            _canvasComponent = newCanvasComponent is null ? null : new WeakReference<RmlCanvasComponent>(newCanvasComponent);
            if (wasOpen)
                OpenInternal();
        }
    }

    private static bool IsElementNavigable(Element? element)
    {
        // The element itself should be tab-able
        if (element is null || element.GetComputedValues().TabIndex() == TabIndex.None)
            return false;

        // Entire element hierarchy should be visible and focusable
        while (element is not null)
        {
            if (!element.IsVisible() || element.GetComputedValues().Focus() == RedBolt.Bindings.RmlUi.Focus.None)
                return false;

            element = element.GetParentNode();
        }

        return true;
    }

    private static Rml.Input.KeyIdentifier GetKeyArg(Std.Vector<RmlVariant> arguments)
    {
        var value = arguments.Count >= 1 ? arguments[0].Get<int>() : 0;
        return (Rml.Input.KeyIdentifier)value;
    }

    private static RmlVariant IsKeyOk(Std.Vector<RmlVariant> arguments)
    {
        var key = GetKeyArg(arguments);
        return new RmlVariant(key == Rml.Input.KeyIdentifier.KiReturn);
    }

    private static RmlVariant IsKeyCancel(Std.Vector<RmlVariant> arguments)
    {
        var key = GetKeyArg(arguments);
        return new RmlVariant(key == Rml.Input.KeyIdentifier.KiEscape);
    }

    private static RmlVariant IsKeySecondary(Std.Vector<RmlVariant> arguments)
    {
        var key = GetKeyArg(arguments);
        return new RmlVariant(key == Rml.Input.KeyIdentifier.KiSpace);
    }

    private static RmlVariant IsKeyTertiary(Std.Vector<RmlVariant> arguments)
    {
        var key = GetKeyArg(arguments);
        return new RmlVariant(key == Rml.Input.KeyIdentifier.KiBack);
    }

    private RmlCanvasComponent? GetCanvasComponent()
    {
        return _canvasComponent is not null && _canvasComponent.TryGetTarget(out var canvasComponent)
            ? canvasComponent
            : null;
    }

    private static object? GetResourceDefault() =>
        new ResourceRef(ObjectReflection<BinaryFile>.TypeId);

    private static object? GetPositionDefault() => Vector2f.Zero;

    private static object? GetSizeDefault() => Vector2f.Zero;

    /// <summary>Attributes.</summary>
    private ResourceRef _resource;
    [Serialized("Use Normalized Coordinates")]
    private bool _useNormalized;
    private Vector2f _size;
    private Vector2f _position;
    [Serialized("Auto Size")]
    private bool _autoSize = true;
    private bool _modal;

    /// <summary>Currently open document. Null if document was closed.</summary>
    private ElementDocument? _document;
    /// <summary>Component which holds RmlUI instance containing UI managed by this component. May be null if UI is rendered into default RmlUI subsystem.</summary>
    private WeakReference<RmlCanvasComponent>? _canvasComponent;

    /// <summary>Type registry for the data model.</summary>
    private DataTypeRegister? _typeRegister;
    private RmlUI.VariantDefinitions? _variantDefinitions;
    /// <summary>Data model for the document.</summary>
    private DataModelHandle? _dataModel;
    /// <summary>Name of the data model.</summary>
    private string _dataModelName = string.Empty;

    /// <summary>Data model constructor.</summary>
    private DataModelConstructor? _modelConstructor;

    /// <summary>Element id to be focused on next update.</summary>
    private string? _pendingFocusId;
    /// <summary>Whether to suppress next call to RestoreFocus().</summary>
    private bool _suppressRestoreFocus;
    private GCHandle _componentHandle;
}
