// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.SystemUI;

/// <summary>Tab that renders Scene and enables Scene manipulation.</summary>
public class ModelViewTab : CustomSceneViewTab
{
    public ModelViewTab()
        : base("Model", "1c4962de-c75c-41fa-bf3f-5bb3f2ba7d53",
            EditorTabFlag.NoContentPadding | EditorTabFlag.OpenByDefault, EditorTabPlacement.DockCenter)
    {
        _modelNode = GetScene()!.CreateChild("Model");
        _cameraOperator = GetCamera()!.Node.CreateComponent<CameraOperator>();
        _cameraOperator.SetBoundingBoxTrackingEnabled(true);
        _cameraOperator.SetEnabled(false);
        _staticModel = _modelNode.CreateComponent<StaticModel>();
        _staticModel.SetCastShadows(true);
    }

    public override void Dispose()
    {
        _cameraOperator = null;
        _staticModel = null;
        _modelNode = null;
        _model = null;
        base.Dispose();
    }

    /// <summary>ResourceEditorTab implementation.</summary>
    public override string GetResourceTitle() => "Model";

    public override bool SupportMultipleResources() => false;

    public override bool CanOpenResource(ResourceFileDescriptor descriptor)
    {
        return descriptor.HasObjectType<Model>();
    }

    public override void ResetCamera()
    {
        if (_model is not null)
        {
            _cameraOperator!.SetBoundingBox(_model.GetBoundingBox());
            _cameraOperator.MoveCamera();
            _state.LastCameraPosition = GetCamera()!.Node.GetPosition();
        }
    }

    /// <summary>ResourceEditorTab implementation.</summary>
    protected override void OnResourceLoaded(string resourceName)
    {
        var cache = GetSubsystem<ResourceCache>();
        _model = cache.GetResource<Model>(resourceName);
        _staticModel!.SetModel(_model);
        ResetCamera();
    }

    protected override void OnResourceUnloaded(string resourceName)
    {
        _model = null;
        _staticModel!.SetModel(_model);
    }

    protected override void OnActiveResourceChanged(string oldResourceName, string newResourceName)
    {
    }

    protected override void OnResourceSaved(string resourceName)
    {
    }

    protected override void OnResourceShallowSaved(string resourceName)
    {
    }

    private Model? _model;
    private Node? _modelNode;
    private StaticModel? _staticModel;
    private CameraOperator? _cameraOperator;
}
