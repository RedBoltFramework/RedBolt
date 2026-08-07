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

namespace RedBolt.Editor.Project;

using System;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.Scene;

/// <summary>Scene creation parameters.</summary>
public struct DefaultSceneParameters
{
    public bool HighQuality;
    public bool CreateObjects;
    public bool IsPrefab;
}

/// <summary>Create default scene.</summary>
public static class DefaultSceneFunctions
{
    public static void CreateDefaultScene(string fileName, in DefaultSceneParameters parameters)
    {
        var cache = Context.Instance.GetSubsystem<ResourceCache>()!;

        using var sceneResource = new SceneResource();
        var scene = sceneResource.Scene;
        scene.CreateComponent<Octree>();

        if (parameters.HighQuality)
        {
            var renderPipeline = scene.CreateComponent<RenderPipeline>();
            var settings = renderPipeline.Settings;

            var renderBufferManagerSettings = settings.RenderBufferManager;
            renderBufferManagerSettings.ColorSpace = RenderPipelineColorSpace.LinearHDR;
            settings.RenderBufferManager = renderBufferManagerSettings;
            var sceneProcessorSettings = settings.SceneProcessor;
            sceneProcessorSettings.PcfKernelSize = 5;
            settings.SceneProcessor = sceneProcessorSettings;

            renderPipeline.SetSettings(settings);
            renderPipeline.SetRenderPassEnabled("Postprocess: FXAA v3", true);
        }

        if (parameters.CreateObjects)
        {
            if (parameters.IsPrefab)
            {
                var prefabNode = scene.CreateChild("[Prefab Node]");
                var prefabGeometry = prefabNode.CreateComponent<StaticModel>();
                prefabGeometry.SetModel(cache.Get<Model>("Models/Box.mdl"));
                prefabGeometry.SetMaterial(cache.Get<Material>("Materials/DefaultWhite.xml"));
                prefabGeometry.SetCastShadows(true);
            }

            var skyboxNode = scene.CreateChild("Skybox");
            var skybox = skyboxNode.CreateComponent<Skybox>();
            skybox.SetModel(cache.Get<Model>("Models/Box.mdl"));
            skybox.SetMaterial(cache.Get<Material>("Materials/DefaultSkybox.xml"));

            var zoneNode = scene.CreateChild("Global Zone");
            var zone = zoneNode.CreateComponent<Zone>();
            zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
            zone.SetAmbientColor(Color.Black);
            zone.SetBackgroundBrightness(parameters.IsPrefab ? 1.0f : 0.5f);
            zone.SetZoneTexture(cache.Get<TextureCube>("Textures/DefaultSkybox.xml"));

            if (!parameters.IsPrefab)
            {
                var lightNode = scene.CreateChild("Global Light");
                lightNode.SetDirection(new Vector3f(1.0f, -1.0f, 1.0f));
                var light = lightNode.CreateComponent<Light>();
                light.SetLightType(LightType.Directional);
                light.SetColor(Color.White);
                light.SetBrightness(0.5f);
                light.SetCastShadows(true);

                var cubeNode = scene.CreateChild("Sample Cube");
                cubeNode.SetScale(3.0f);
                var cubeGeometry = cubeNode.CreateComponent<StaticModel>();
                cubeGeometry.SetModel(cache.Get<Model>("Models/TeaPot.mdl"));
                cubeGeometry.SetMaterial(cache.Get<Material>("Materials/DefaultWhite.xml"));
                cubeGeometry.SetCastShadows(true);
            }

            var planeNode = scene.CreateChild("Ground Plane");
            planeNode.SetScale(7.0f);
            var planeGeometry = planeNode.CreateComponent<StaticModel>();
            planeGeometry.SetModel(cache.Get<Model>("Models/Plane.mdl"));
            planeGeometry.SetMaterial(cache.Get<Material>("Materials/DefaultGrey.xml"));
        }

        if (parameters.IsPrefab)
        {
            using var prefabResource = new PrefabResource();
            scene.GeneratePrefab(prefabResource.GetMutableScenePrefab());
            prefabResource.NormalizeIds();
            prefabResource.SaveFile(new FileIdentifier(fileName));
        }
        else if (fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            using var xmlFile = new XMLFile();
            using var xmlRoot = xmlFile.CreateRoot("scene");
            scene.SaveXML(xmlRoot);
            xmlFile.SaveFile(new FileIdentifier(fileName));
        }
        else
        {
            sceneResource.SaveFile(new FileIdentifier(fileName));
        }
    }
}
