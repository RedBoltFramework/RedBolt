// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

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
