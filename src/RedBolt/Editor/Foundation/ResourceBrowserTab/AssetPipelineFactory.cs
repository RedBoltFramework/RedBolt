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

namespace RedBolt.Editor.Foundation;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Assets;
using RedBolt.Editor.Project;
using RedBolt.IO;
using RedBolt.Resource;
using RedBolt.Utility;

/// <summary>Camera controller used by Scene View.</summary>
public class AssetPipelineFactory : BaseResourceFactory
{
    public AssetPipelineFactory()
        : base(0, "Asset Pipeline")
    {
    }

    /// <summary>Implement BaseResourceFactory.</summary>
    public override string DefaultFileName => "Default.assetpipeline";

    public override void RenderAuxilary()
    {
        ImGui.Separator();

        ImGui.Checkbox("Model Importer"u8, ref _modelImporter);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Add default ModelImporter to the pipeline."u8);

        ImGui.Separator();
    }

    public override void CommitAndClose()
    {
        var cache = GetSubsystem<ResourceCache>();

        var pipeline = new AssetPipeline();
        ModelImporter? modelImporter = null;
        try
        {
            if (_modelImporter)
            {
                modelImporter = new ModelImporter();
                pipeline.AddTransformer(modelImporter);
            }

            pipeline.SaveFile(new FileIdentifier(FinalFileName));
        }
        finally
        {
            modelImporter?.Dispose();
            pipeline.Dispose();
        }
    }

    private bool _modelImporter;
}
