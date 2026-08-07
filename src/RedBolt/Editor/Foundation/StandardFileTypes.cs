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
