using RedBolt;
using System.Runtime.CompilerServices;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class BillboardSetTests
{
    private readonly HeadlessEngineFixture _fixture;

    public BillboardSetTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
        FirstCubeTestData.AddResourceDirs(Resources);
    }

    private ResourceCache Resources => _fixture.Resources;

    [Fact]
    public void Defaults_match_cpp_constructor()
    {
        var set = new Scene().CreateChild("Billboards").CreateComponent<BillboardSet>();

        Assert.Equal(DrawableFlags.Geometry, set.GetDrawableFlags());
        Assert.Equal(0u, set.GetNumBillboards());
        Assert.True(set.IsRelative());
        Assert.True(set.IsScaled());
        Assert.False(set.IsSorted());
        Assert.False(set.IsFixedScreenSize());
        Assert.Equal(FaceCameraMode.RotateXyz, set.GetFaceCameraMode());
        Assert.Equal(0.0f, set.GetMinAngle());
        Assert.Equal(1.0f, set.GetAnimationLodBias());
        Assert.Null(set.GetMaterial());

        var batches = set.GetBatches();
        Assert.Single(batches);
        Assert.Equal(GeometryType.Billboard, batches[0].GeometryType);
        Assert.Equal(1u, batches[0].NumWorldTransforms);
        Assert.NotNull(batches[0].Geometry);
    }

    [Fact]
    public void Num_billboards_defaults_and_commit_grow_shrink()
    {
        var set = new Scene().CreateChild("Billboards").CreateComponent<BillboardSet>();
        set.SetNumBillboards(3);

        Assert.Equal(3u, set.GetNumBillboards());
        for (var i = 0u; i < 3u; ++i)
        {
            ref var billboard = ref set.GetBillboard(i);
            Assert.Equal(Vector3f.Zero, billboard.Position);
            Assert.Equal(Vector2f.One, billboard.Size);
            Assert.Equal(Rect.Positive, billboard.Uv);
            Assert.Equal(new Color(1.0f, 1.0f, 1.0f), billboard.Color);
            Assert.Equal(0.0f, billboard.Rotation);
            Assert.Equal(Vector3f.Up, billboard.Direction);
            Assert.False(billboard.Enabled);
            Assert.Equal(1.0f, billboard.ScreenScaleFactor);
        }

        set.SetNumBillboards(1);
        Assert.Equal(1u, set.GetNumBillboards());
        set.SetNumBillboards(1);
        Assert.Equal(1u, set.GetNumBillboards());
    }

    [Fact]
    public void Material_and_flags_roundtrip()
    {
        var material = Resources.Get<Material>("Materials/FirstCube");
        var set = new Scene().CreateChild("Billboards").CreateComponent<BillboardSet>();

        set.SetMaterial(material);
        set.SetRelative(false);
        set.SetScaled(false);
        set.SetSorted(true);
        set.SetFixedScreenSize(true);
        set.SetMinAngle(15.0f);
        set.SetAnimationLodBias(2.5f);

        Assert.Same(material, set.GetMaterial());
        Assert.False(set.IsRelative());
        Assert.False(set.IsScaled());
        Assert.True(set.IsSorted());
        Assert.True(set.IsFixedScreenSize());
        Assert.Equal(15.0f, set.GetMinAngle());
        Assert.Equal(2.5f, set.GetAnimationLodBias());

        var attr = set.GetMaterialAttr();
        Assert.Equal(material.Name, attr.Name);
        set.SetMaterial(null);
        set.SetMaterialAttr(attr);
        Assert.Same(material, set.GetMaterial());
    }

    [Fact]
    public void Face_camera_mode_updates_geometry_type_for_special_modes()
    {
        var set = new Scene().CreateChild("Billboards").CreateComponent<BillboardSet>();

        set.SetFaceCameraMode(FaceCameraMode.LookAtY);
        Assert.Equal(FaceCameraMode.LookAtY, set.GetFaceCameraMode());
        Assert.Equal(GeometryType.Billboard, set.GetBatches()[0].GeometryType);

        set.SetFaceCameraMode(FaceCameraMode.Direction);
        Assert.Equal(FaceCameraMode.Direction, set.GetFaceCameraMode());
        Assert.Equal(GeometryType.DirectionalBillboard, set.GetBatches()[0].GeometryType);

        set.SetFaceCameraMode(FaceCameraMode.AxisAngle);
        Assert.Equal(FaceCameraMode.AxisAngle, set.GetFaceCameraMode());
        Assert.Equal(GeometryType.StaticNoInstancing, set.GetBatches()[0].GeometryType);

        set.SetFaceCameraMode(FaceCameraMode.RotateXyz);
        Assert.Equal(FaceCameraMode.RotateXyz, set.GetFaceCameraMode());
        Assert.Equal(GeometryType.Billboard, set.GetBatches()[0].GeometryType);
    }

    [Fact]
    public void Billboards_attribute_roundtrip_new_format()
    {
        var set = new Scene().CreateChild("Billboards").CreateComponent<BillboardSet>();
        set.SetNumBillboards(2);

        ref var first = ref set.GetBillboard(0);
        first.Position = new Vector3f(1, 2, 3);
        first.Size = new Vector2f(4, 5);
        first.Uv = new Rect(0.1f, 0.2f, 0.3f, 0.4f);
        first.Color = new Color(0.5f, 0.6f, 0.7f, 0.8f);
        first.Rotation = 12.0f;
        first.Direction = new Vector3f(0, 0, 1);
        first.Enabled = true;
        ref var second = ref set.GetBillboard(1);
        second.Position = new Vector3f(-1, -2, -3);
        second.Enabled = false;

        var attr = set.GetBillboardsAttr();
        Assert.Equal(2 * 7 + 1, attr.Count);
        Assert.Equal(2, attr[0].GetInt());

        var restored = new Scene().CreateChild("Restored").CreateComponent<BillboardSet>();
        restored.SetBillboardsAttr(attr);
        Assert.Equal(2u, restored.GetNumBillboards());

        ref var restoredFirst = ref restored.GetBillboard(0);
        Assert.Equal(new Vector3f(1, 2, 3), restoredFirst.Position);
        Assert.Equal(new Vector2f(4, 5), restoredFirst.Size);
        Assert.True(MathDefs.ApproximatelyEquivalent(restoredFirst.Uv.Min.X, 0.1f));
        Assert.True(MathDefs.ApproximatelyEquivalent(restoredFirst.Uv.Min.Y, 0.2f));
        Assert.True(MathDefs.ApproximatelyEquivalent(restoredFirst.Uv.Max.X, 0.3f));
        Assert.True(MathDefs.ApproximatelyEquivalent(restoredFirst.Uv.Max.Y, 0.4f));
        Assert.True(MathDefs.ApproximatelyEquivalent(restoredFirst.Color.R, 0.5f));
        Assert.Equal(12.0f, restoredFirst.Rotation);
        Assert.Equal(new Vector3f(0, 0, 1), restoredFirst.Direction);
        Assert.True(restoredFirst.Enabled);
        Assert.False(restored.GetBillboard(1).Enabled);
    }

    [Fact]
    public void World_bounding_box_merges_enabled_billboards_and_node_position()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Billboards");
        node.SetWorldPosition(new Vector3f(10, 0, 0));
        var set = node.CreateComponent<BillboardSet>();
        set.SetNumBillboards(2);

        ref var disabled = ref set.GetBillboard(0);
        disabled.Position = new Vector3f(100, 0, 0);
        disabled.Size = new Vector2f(2, 2);
        disabled.Enabled = false;

        ref var enabled = ref set.GetBillboard(1);
        enabled.Position = new Vector3f(0, 5, 0);
        enabled.Size = new Vector2f(2, 2);
        enabled.Enabled = true;
        set.Commit();

        var box = set.GetWorldBoundingBox();
        Assert.True(box.Defined);
        // Node position is always merged.
        Assert.True(box.IsInside(new Vector3f(10, 0, 0)) != Intersection.Outside);
        // Enabled billboard center is near node+relative position when relative is true.
        Assert.True(box.IsInside(new Vector3f(10, 5, 0)) != Intersection.Outside);
        // Disabled far billboard is not required to expand the box.
        Assert.True(box.Max.X < 100.0f);
    }

    [Fact]
    public void ProcessRayQuery_triangle_level_hits_enabled_billboard_sphere()
    {
        var scene = new Scene();
        var node = scene.CreateChild("Billboards");
        node.SetWorldPosition(Vector3f.Zero);
        var set = node.CreateComponent<BillboardSet>();
        set.SetNumBillboards(1);

        ref var billboard = ref set.GetBillboard(0);
        billboard.Position = new Vector3f(0, 0, 5);
        billboard.Size = new Vector2f(2, 2);
        billboard.Enabled = true;
        set.Commit();

        var results = new List<RayQueryResult>();
        var ray = new Ray(Vector3f.Zero, Vector3f.Forward);
        var query = new RayOctreeQuery(results, ray, RayQueryLevel.RayTriangle, 100.0f);
        set.ProcessRayQuery(query, results);

        Assert.NotEmpty(results);
        Assert.Same(set, results[0].Drawable);
        Assert.Equal(0u, results[0].SubObject);
        Assert.True(results[0].Distance > 0.0f);
        Assert.True(results[0].Distance < 100.0f);
    }

    [Fact]
    public void UpdateBatches_and_geometry_build_draw_range_for_enabled_billboards()
    {
        var scene = new Scene();
        var cameraNode = scene.CreateChild("Camera");
        cameraNode.SetWorldPosition(new Vector3f(0, 0, -10));
        var camera = cameraNode.CreateComponent<Camera>();

        var node = scene.CreateChild("Billboards");
        var set = node.CreateComponent<BillboardSet>();
        set.SetNumBillboards(2);

        ref var first = ref set.GetBillboard(0);
        first.Position = Vector3f.Zero;
        first.Size = Vector2f.One;
        first.Enabled = true;

        ref var second = ref set.GetBillboard(1);
        second.Enabled = false;
        set.Commit();

        var frame = new CommonFrameInfo
        {
            FrameNumber = 1,
            TimeStep = 1.0f / 60.0f,
            ViewportSize = new Vector2i(1280, 720),
            Camera = camera,
            Scene = scene
        };
        frame.Cameras[0] = camera;

        set.UpdateBatches(frame);
        set.UpdateBatchesDelayed(frame);
        set.UpdateGeometry(frame);

        var geometry = set.GetBatches()[0].Geometry!;
        Assert.Equal(6u, geometry.GetIndexCount());
        Assert.Equal(PrimitiveType.TriangleList, geometry.GetPrimitiveType());
        Assert.Equal(UpdateGeometryType.MainThread, set.GetUpdateGeometryType());
    }

    [Fact]
    public void Face_camera_mode_names_match_cpp_table()
    {
        var names = BillboardSet.GetFaceCameraModeNames();
        Assert.Equal(
        [
            "None",
            "Rotate XYZ",
            "Rotate Y",
            "LookAt XYZ",
            "LookAt Y",
            "LookAt Mixed",
            "Direction",
            "Axis Angle"
        ], names);
    }

    [Fact]
    public void Invalid_billboard_index_returns_null_reference()
    {
        var set = new BillboardSet();

        Assert.True(Unsafe.IsNullRef(ref set.GetBillboard(0)));
    }

    [Fact]
    public void Dispose_destroys_owned_billboards()
    {
        var set = new BillboardSet();
        set.SetNumBillboards(2);

        set.Dispose();

        Assert.Equal(0u, set.GetNumBillboards());
    }
}
