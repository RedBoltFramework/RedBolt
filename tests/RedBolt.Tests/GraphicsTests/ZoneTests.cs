using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ZoneTests
{
    private readonly HeadlessEngineFixture _fixture;

    public ZoneTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    private Renderer Renderer => _fixture.Renderer;

    [Fact]
    public void Default_render_state_works()
    {
        var zone = new Scene().CreateChild("Zone").CreateComponent<Zone>();
        Assert.Equal(DrawableFlags.Zone, zone.GetDrawableFlags());
        Assert.Equal(new Color(0.1f, 0.1f, 0.1f), zone.GetAmbientColor());
        Assert.Equal(Color.Black, zone.GetFogColor());
        Assert.True(MathDefs.ApproximatelyEquivalent(zone.GetFogStart(), 250.0f));
    }

    [Fact]
    public void Class_kind_and_pointer_ownership_match_cpp()
    {
        Assert.False(typeof(Zone).IsSealed);
        Assert.True(typeof(Drawable).IsAssignableFrom(typeof(Zone)));
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(Zone)));
        Assert.True(typeof(ReflectionProbeData).IsValueType);
        Assert.Equal(typeof(WeakReference<Zone>), GetPrivateField("_lastAmbientStartZone").FieldType);
        Assert.Equal(typeof(WeakReference<Zone>), GetPrivateField("_lastAmbientEndZone").FieldType);
        Assert.Equal(typeof(Texture), GetPrivateField("_zoneTexture").FieldType);
    }

    [Fact]
    public void Ambient_color_does_not_rewrite_valid_gradient_cache()
    {
        var zone = new Zone();
        var firstColor = new Color(0.2f, 0.3f, 0.4f);
        zone.SetAmbientColor(firstColor);
        zone.UpdateAmbientGradient();
        zone.SetAmbientGradient(true);

        zone.SetAmbientColor(new Color(0.7f, 0.8f, 0.9f));

        Assert.Equal(firstColor, zone.GetAmbientStartColor());
        Assert.Equal(firstColor, zone.GetAmbientEndColor());
    }

    [Fact]
    public void Zone_texture_attribute_is_derived_from_loaded_resource()
    {
        var zone = new Zone();

        zone.SetZoneTextureAttr(new ResourceRef(ObjectReflection<TextureCube>.TypeId, "Missing.xml"));

        Assert.Null(zone.GetZoneTexture());
        Assert.Equal(string.Empty, zone.GetZoneTextureAttr().Name);
    }

    [Fact]
    public void BoundingBox_and_ambient_roundtrip()
    {
        var zone = new Scene().CreateChild("Zone").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(new Vector3f(-1.0f, -2.0f, -3.0f), new Vector3f(1.0f, 2.0f, 3.0f)));
        zone.SetAmbientColor(new Color(0.2f, 0.3f, 0.4f));

        Assert.True(zone.GetBoundingBox().Equals(
            new BoundingBox(new Vector3f(-1.0f, -2.0f, -3.0f), new Vector3f(1.0f, 2.0f, 3.0f))));
        Assert.Equal(new Color(0.2f, 0.3f, 0.4f), zone.GetAmbientColor());
    }

    [Fact]
    public void Fog_helpers_clamp_and_roundtrip()
    {
        var zone = new Scene().CreateChild("Zone").CreateComponent<Zone>();
        zone.SetFogColor(new Color(0.5f, 0.6f, 0.7f));
        zone.SetFogStart(-1.0f);
        zone.SetFogEnd(-1.0f);
        zone.SetFogHeight(4.0f);
        zone.SetFogHeightScale(5.0f);
        zone.SetPriority(7);

        Assert.Equal(new Color(0.5f, 0.6f, 0.7f), zone.GetFogColor());
        Assert.True(MathDefs.ApproximatelyEquivalent(zone.GetFogStart(), 0.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(zone.GetFogEnd(), 0.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(zone.GetFogHeight(), 4.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(zone.GetFogHeightScale(), 5.0f));
        Assert.Equal(7, zone.GetPriority());
    }

    [Fact]
    public void Mode_flags_work()
    {
        var zone = new Scene().CreateChild("Zone").CreateComponent<Zone>();
        zone.SetHeightFog(true);
        zone.SetOverride(true);
        zone.SetAmbientGradient(true);
        Assert.True(zone.GetHeightFog());
        Assert.True(zone.GetOverride());
        Assert.True(zone.GetAmbientGradient());
    }

    [Fact]
    public void Ambient_gradient_fallback_works()
    {
        var zone = new Scene().CreateChild("Zone").CreateComponent<Zone>();
        zone.SetAmbientColor(new Color(0.2f, 0.3f, 0.4f));
        zone.SetAmbientBrightness(2.0f);
        zone.UpdateCachedData();
        Assert.True(zone.GetAmbientStartColor().Equals(zone.GetAmbientColor(), MathDefs.Epsilon));
        Assert.True(zone.GetAmbientEndColor().Equals(zone.GetAmbientColor(), MathDefs.Epsilon));
        Assert.True(zone.GetAmbientLighting().ApproximatelyEquivalent(
            (zone.GetAmbientColor() * zone.GetAmbientBrightness()).GammaToLinear().ToVector3()));
    }

    [Fact]
    public void Oriented_containment_works()
    {
        var zone = new Scene().CreateChild("Zone").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(new Vector3f(-1.0f, -2.0f, -3.0f), new Vector3f(1.0f, 2.0f, 3.0f)));
        zone.Node.SetTransform(new Vector3f(5.0f, 0.0f, 0.0f), Quaternion.Identity, new Vector3f(2.0f, 1.0f, 1.0f));
        // World box: (3,-2,-3) to (7,2,3). Point (6,0,0) is inside.
        Assert.True(zone.IsInside(new Vector3f(6.0f, 0.0f, 0.0f)));
    }

    [Fact]
    public void World_bounding_box_updates_from_node_transform()
    {
        var zone = new Scene().CreateChild("Zone").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(new Vector3f(-1.0f, -2.0f, -3.0f), new Vector3f(1.0f, 2.0f, 3.0f)));
        zone.Node.SetPosition(new Vector3f(5.0f, 0.0f, 0.0f));

        Assert.Equal(new BoundingBox(new Vector3f(4.0f, -2.0f, -3.0f), new Vector3f(6.0f, 2.0f, 3.0f)), zone.GetWorldBoundingBox());
    }

    [Fact]
    public void Inverse_world_transform_works()
    {
        var zone = new Scene().CreateChild("Zone").CreateComponent<Zone>();
        zone.Node.SetTransform(new Vector3f(5.0f, 0.0f, 0.0f), Quaternion.Identity, new Vector3f(2.0f, 1.0f, 1.0f));
        var result = zone.GetInverseWorldTransform() * new Vector3f(6.0f, 0.0f, 0.0f);
        Assert.True(result.ApproximatelyEquivalent(new Vector3f(0.5f, 0.0f, 0.0f)));
    }

    [Fact]
    public void Drawable_zone_cache_and_masks_work()
    {
        var drawable = new Zone();
        var zone = new Zone();
        drawable.SetLightMask(0x0f);
        drawable.SetShadowMask(0x07);
        zone.SetLightMask(0x03);
        zone.SetShadowMask(0x05);

        drawable.SetZone(zone);
        Assert.Same(zone, drawable.GetZone());
        Assert.False(drawable.IsZoneDirty());
        Assert.Equal(0x03u, drawable.GetLightMaskInZone());
        Assert.Equal(0x05u, drawable.GetShadowMaskInZone());

        drawable.SetZone(zone, temporary: true);
        Assert.True(drawable.IsZoneDirty());
        drawable.SetZone(null);
        Assert.Null(drawable.GetZone());
        Assert.False(drawable.IsZoneDirty());
        Assert.Equal(0x0fu, drawable.GetLightMaskInZone());
        Assert.Equal(0x07u, drawable.GetShadowMaskInZone());
    }

    [Fact]
    public void Ambient_gradient_uses_highest_priority_neighbor_at_each_end()
    {
        var scene = new Scene();
        scene.CreateComponent<Octree>();
        var zone = scene.CreateChild("Zone").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(new Vector3f(-1.0f, -1.0f, -10.0f), new Vector3f(1.0f, 1.0f, 10.0f)));
        var zoneColor = new Color(0.0f, 0.0f, 1.0f);
        var neighborColor = new Color(1.0f, 0.0f, 0.0f);
        zone.SetAmbientColor(zoneColor);
        zone.SetAmbientGradient(true);

        var neighbor = scene.CreateChild("Neighbor").CreateComponent<Zone>();
        neighbor.SetBoundingBox(new BoundingBox(new Vector3f(-10.0f, -10.0f, -20.0f), new Vector3f(10.0f, 10.0f, -1.0f)));
        neighbor.SetAmbientColor(neighborColor);
        neighbor.SetPriority(1);

        Assert.Equal(neighborColor, zone.GetAmbientStartColor());
        Assert.Equal(zoneColor, zone.GetAmbientEndColor());
    }

    [Fact]
    public void Zone_lookup_selects_priority_and_background_zones()
    {
        var index = new ZoneLookupIndex();
        var scene = new Scene();
        var lowPriorityZone = scene.CreateChild("LowPriorityZone").CreateComponent<Zone>();
        lowPriorityZone.SetBoundingBox(new BoundingBox(new Vector3f(-10.0f, -10.0f, -10.0f), new Vector3f(10.0f, 10.0f, 10.0f)));
        lowPriorityZone.SetPriority(0);
        var highPriorityZone = scene.CreateChild("HighPriorityZone").CreateComponent<Zone>();
        highPriorityZone.SetBoundingBox(new BoundingBox(new Vector3f(-1.0f, -1.0f, -1.0f), new Vector3f(1.0f, 1.0f, 1.0f)));
        highPriorityZone.SetPriority(5);

        index.AddZone(lowPriorityZone);
        index.AddZone(highPriorityZone);
        index.Commit();

        var cachedZone = index.QueryZone(Vector3f.Zero, Drawable.DefaultZoneMask);

        Assert.Same(highPriorityZone, cachedZone.Zone);
        Assert.Same(lowPriorityZone, index.GetBackgroundZone());
    }

    [Fact]
    public void Zone_lookup_returns_default_zone_outside_matching_zones()
    {
        var index = new ZoneLookupIndex();
        var scene = new Scene();
        var zone = scene.CreateChild("Zone").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(new Vector3f(-1.0f, -1.0f, -1.0f), new Vector3f(1.0f, 1.0f, 1.0f)));
        index.AddZone(zone);
        index.Commit();

        var cachedZone = index.QueryZone(new Vector3f(4.0f, 0.0f, 0.0f), Drawable.DefaultZoneMask);

        Assert.Same(Renderer.GetDefaultZone(), cachedZone.Zone);
        Assert.True(cachedZone.CacheInvalidationDistanceSquared > 0.0f);
    }

    [Fact]
    public void Draw_debug_geometry_uses_zone_bounds()
    {
        var scene = new Scene();
        scene.CreateComponent<Octree>();
        var zone = scene.CreateChild("Zone").CreateComponent<Zone>();
        var debugRenderer = scene.CreateComponent<DebugRenderer>();
        debugRenderer.SetView(new Camera());

        zone.DrawDebugGeometry(debugRenderer, depthTest: false);

        Assert.True(debugRenderer.HasContent());
    }

    private static System.Reflection.FieldInfo GetPrivateField(string fieldName)
    {
        var field = typeof(Zone).GetField(fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field;
    }
}
