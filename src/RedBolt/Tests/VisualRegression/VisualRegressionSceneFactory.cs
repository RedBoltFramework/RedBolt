namespace RedBolt.Tests;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

internal static class VisualRegressionSceneFactory
{
    public static Scene CreateScene(
        Color? ambientColor = null,
        float ambientBrightness = 1.0f,
        Color? fogColor = null,
        float fogStart = 1000.0f,
        float fogEnd = 2000.0f)
    {
        var scene = new Scene();
        scene.CreateComponent<Octree>();

        var zone = scene.CreateChild("Zone").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(ambientColor ?? new Color(0.35f, 0.35f, 0.35f));
        zone.SetAmbientBrightness(ambientBrightness);
        zone.SetFogColor(fogColor ?? Color.Black);
        zone.SetFogStart(fogStart);
        zone.SetFogEnd(fogEnd);
        return scene;
    }

    public static StaticModel AddModel(
        Scene scene,
        ResourceCache resources,
        string modelName,
        string materialName,
        Vector3f position,
        float scale = 1.0f,
        Quaternion? rotation = null)
    {
        var node = scene.CreateChild(Path.GetFileNameWithoutExtension(modelName));
        node.SetPosition(position);
        node.SetScale(scale);
        if (rotation.HasValue)
            node.SetRotation(rotation.Value);

        var model = node.CreateComponent<StaticModel>();
        model.SetModel(resources.Get<Model>(modelName));
        model.SetMaterial(resources.Get<Material>(materialName));
        return model;
    }

    public static Camera AddCamera(
        Scene scene,
        Vector3f position,
        Vector3f target,
        float aspectRatio,
        bool orthographic = false,
        float orthoSize = 10.0f,
        float fov = 45.0f)
    {
        var node = scene.CreateChild("Camera");
        node.SetPosition(position);
        node.LookAt(target, Vector3f.Up);

        var camera = node.CreateComponent<Camera>();
        camera.SetAspectRatio(aspectRatio);
        camera.SetOrthographic(orthographic);
        camera.SetOrthoSize(orthoSize);
        camera.SetFov(fov);
        camera.SetNearClip(0.1f);
        camera.SetFarClip(100.0f);
        return camera;
    }

    public static Light AddLight(
        Scene scene,
        LightType type,
        Vector3f position,
        Vector3f target,
        Color color,
        uint lightMask = uint.MaxValue,
        float range = 10.0f,
        float brightness = 1.0f,
        bool castShadows = false)
    {
        var node = scene.CreateChild($"{type}Light");
        node.SetPosition(position);
        node.LookAt(target, Vector3f.Up);

        var light = node.CreateComponent<Light>();
        light.SetLightType(type);
        light.SetColor(color);
        light.SetBrightness(brightness);
        light.SetRange(range);
        light.SetLightMask(lightMask);
        light.SetCastShadows(castShadows);
        return light;
    }
}
