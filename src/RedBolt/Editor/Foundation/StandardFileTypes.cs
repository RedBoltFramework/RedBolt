// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using RedBolt.Audio;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.SystemUI;
using RedBolt.Utility;

public static class StandardFileTypes
{
    public static void Register(Project project)
    {
        project.AddAnalyzeFileCallback(static (description, context) =>
        {
            description.AddObjectType<BinaryFile>();
            if (context.XmlFile is not null)
                description.AddObjectType<XMLFile>();
            if (context.JsonFile is not null)
                description.AddObjectType<JSONFile>();
        });

        project.AddAnalyzeFileCallback(static (description, context) =>
        {
            if (description.HasExtension([".wav", ".ogg"]))
                description.AddObjectType<Sound>();
        });

        project.AddAnalyzeFileCallback(static (description, context) =>
        {
            var isNewSceneWithLegacyExtension = false;
            if (description.HasExtension(".xml")
                && context.HasXMLRoot(SceneResource.GetXmlRootName()))
            {
                using var root = context.XmlFile!.GetRoot();
                isNewSceneWithLegacyExtension = root.HasAttribute("_id");
            }

            if (description.HasExtension(".scene") || context.HasXMLRoot("scene")
                // Support new scene format with legacy extension too
                || isNewSceneWithLegacyExtension)
            {
                description.AddObjectType<Scene>();
                description.AddObjectType<SceneResource>();
            }
        });

        project.AddAnalyzeFileCallback(static (description, context) =>
        {
            if (description.HasExtension(".material") || context.HasXMLRoot("material"))
                description.AddObjectType<Material>();
        });

        project.AddAnalyzeFileCallback(static (description, context) =>
        {
            if (description.HasExtension(".serializable"))
                description.AddObjectType<SerializableResource>();
        });

        project.AddAnalyzeFileCallback(static (description, context) =>
        {
            if (description.HasExtension(".renderpath"))
                description.AddObjectType<RenderPath>();
        });

        project.AddAnalyzeFileCallback(static (description, context) =>
        {
            if (description.HasExtension([".dds", ".bmp", ".jpg", ".jpeg", ".tga", ".png"]))
            {
                description.AddObjectType<Texture>();
                description.AddObjectType<Texture2D>();
            }
            else if (context.HasXMLRoot("cubemap"))
            {
                description.AddObjectType<Texture>();
                description.AddObjectType<TextureCube>();
            }
            else if (context.HasXMLRoot("texture3d"))
            {
                description.AddObjectType<Texture>();
                description.AddObjectType<Texture3D>();
            }
            else if (context.HasXMLRoot("texturearray"))
            {
                description.AddObjectType<Texture>();
                description.AddObjectType<Texture2DArray>();
            }
        });

        project.AddAnalyzeFileCallback(static (description, context) =>
        {
            if (description.HasExtension(".mdl"))
            {
                description.AddObjectType<Model>();
            }
        });

        project.AddAnalyzeFileCallback(static (description, context) =>
        {
            if (description.HasExtension(".ani") || context.HasXMLRoot("animation"))
            {
                description.AddObjectType<Animation>();
            }
        });

        project.AddAnalyzeFileCallback(static (description, context) =>
        {
            if (description.HasExtension([".assetpipeline", ".AssetPipeline.json"]))
            {
                description.AddObjectType<AssetPipeline>();
            }
        });

        project.AddAnalyzeFileCallback(static (description, context) =>
        {
            if (description.HasExtension([".sdf", ".ttf"]))
            {
                description.AddObjectType<Font>();
            }
        });

        project.AddAnalyzeFileCallback(static (description, context) =>
        {
            if (description.HasExtension(".prefab") || context.HasXMLRoot("scene"))
            {
                description.AddObjectType<PrefabResource>();
            }
        });
    }
}
