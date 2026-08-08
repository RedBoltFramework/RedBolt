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

using System.Linq;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.SystemUI;

/// <summary>Tab that renders Scene and enables Scene manipulation.</summary>
public class AnimationViewTab : CustomSceneViewTab
{
    public AnimationViewTab()
        : base("Animation", "a8e49ac3-8edb-493c-ac7e-0d42530c62fb",
            EditorTabFlag.NoContentPadding | EditorTabFlag.OpenByDefault, EditorTabPlacement.DockCenter)
    {
        _modelNode = GetScene()!.CreateChild("Model");
        _animatedModel = _modelNode.CreateComponent<AnimatedModel>();
        _animatedModel.SetCastShadows(true);
        _animationController = _modelNode.CreateComponent<AnimationController>();
    }

    public override void Dispose()
    {
        _animationController = null;
        _animatedModel = null;
        _modelNode = null;
        _animation = null;
        _model = null;
        base.Dispose();
    }

    /// <summary>ResourceEditorTab implementation.</summary>
    public override void RenderContent()
    {
        if (_animation is null)
            return;

        base.RenderContent();
    }

    public override string GetResourceTitle() => "Animation";

    public override bool SupportMultipleResources() => false;

    public override bool CanOpenResource(ResourceFileDescriptor descriptor)
    {
        var name = descriptor.TypeNames.First();
        return descriptor.HasObjectType<Animation>();
    }

    public override void ResetCamera()
    {
        if (_model is not null)
            _state.LookAt(_model.GetBoundingBox());
    }

    /// <summary>ResourceEditorTab implementation.</summary>
    protected override void OnResourceLoaded(string resourceName)
    {
        var cache = GetSubsystem<ResourceCache>();
        _animation = cache.GetResource<Animation>(resourceName, false);

        var parameters = new AnimationParameters(_animation).Layer(0);
        parameters.IsLooped = true;
        _animationController!.StopAll();
        _animationController.PlayNewExclusive(parameters);
    }

    protected override void OnResourceUnloaded(string resourceName)
    {
        _animation = null;
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

    protected override void RenderTitle()
    {
        base.RenderTitle();

        var cache = GetSubsystem<ResourceCache>();

        var textureType = _model is not null
            ? ObjectReflection<Model>.TypeId
            : ObjectReflection<Texture2D>.TypeId;
        var textureName = _model is not null ? _model.GetName() : string.Empty;
        if (Widgets.EditResourceRef(ref textureType, ref textureName, AllowedTextureTypes))
        {
            _model = textureName.Length != 0
                ? cache.GetResource(ObjectReflection<Model>.TypeId, textureName) as Model
                : null;
            _animatedModel!.SetModel(_model);
            if (_model is not null)
                ResetCamera();
        }
    }

    private Model? _model;
    private Animation? _animation;
    private Node? _modelNode;
    private AnimatedModel? _animatedModel;
    private AnimationController? _animationController;

    private static readonly StringVector AllowedTextureTypes = [nameof(Model)];
}
