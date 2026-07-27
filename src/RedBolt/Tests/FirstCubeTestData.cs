namespace RedBolt.Tests;

internal static class FirstCubeTestData
{
    public static void AddResourceDirs(ResourceCache resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        RbfxTestData.AddResourceDirs(resources);
        if (resources.GetExistingResource<Material>("Materials/FirstCube") is null)
        {
            var material = resources.GetResource<Material>("Materials/StoneTiled.xml").Clone("Materials/FirstCube");
            foreach (var textureName in material.GetTextures().Values.Select(static texture => texture.Name).ToArray())
                material.SetTexture(textureName, null);
            if (resources.Exists<Technique>("Techniques/NoTextureUnlit.xml"))
                material.SetTechnique(0, resources.GetResource<Technique>("Techniques/NoTextureUnlit.xml"));
            material.SetShaderParameter("MatDiffColor", new Vector4f(1.0f, 0.7f, 0.2f, 1.0f));
            resources.AddManualResource(material);
        }
    }
}
