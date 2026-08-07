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

namespace RedBolt.Editor.Foundation;

using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.SystemUI;

// TODO: port Foundation_ModelViewTab when editor plugins are ported.
// void Foundation_ModelViewTab(Context* context, Project* project)
// {
//     project->AddTab(MakeShared<ModelViewTab>(context));
// }

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
