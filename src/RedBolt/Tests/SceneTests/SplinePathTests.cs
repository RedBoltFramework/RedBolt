using RedBolt;

namespace RedBolt.Tests;

public sealed class SplinePathTests
{
    public SplinePathTests()
    {
        TestTypeRegistration.RegisterFactory<SplinePath>();
    }

    [Fact]
    public void Spline_class_kind_and_owned_knot_lifetimes_match_cpp()
    {
        Assert.False(typeof(Spline).IsSealed);
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(Spline)));

        using var text = new CString("owned");
        using var value = new Variant(text);
        using var source = new VariantVector([value]);
        using var spline = new Spline(source);
        using var clone = (Spline)spline.Clone();
        var borrowedKnot = spline.GetKnots()[0];
        using var copiedKnot = spline.GetKnot(0);

        source.Dispose();
        spline.Clear();

        Assert.Equal(string.Empty, borrowedKnot.GetCString().ToString());
        Assert.Equal("owned", copiedKnot.GetCString().ToString());
        using var cloneKnot = clone.GetKnot(0);
        Assert.Equal("owned", cloneKnot.GetCString().ToString());
    }

    [Fact]
    public void Bezier_spline_reduces_all_knot_levels()
    {
        using var spline = new Spline(
            [new Variant(0.0f), new Variant(10.0f), new Variant(20.0f)],
            InterpolationMode.BezierCurve);

        using var point = spline.GetPoint(0.5f);

        Assert.Equal(10.0f, point.GetFloat());
    }

    [Fact]
    public void Linear_spline_interpolation_works()
    {
        var spline = new Spline([new Variant(new Vector3f(0.0f, 0.0f, 0.0f)), new Variant(new Vector3f(10.0f, 0.0f, 0.0f))], InterpolationMode.LinearCurve);
        Assert.True(spline.GetPoint(0.5f).GetVector3().ApproximatelyEquivalent(new Vector3f(5.0f, 0.0f, 0.0f)));
    }

    [Fact]
    public void Catmull_rom_full_interpolation_works()
    {
        var spline = new Spline(
        [
            new Variant(new Vector3f(0.0f, 0.0f, 0.0f)),
            new Variant(new Vector3f(10.0f, 0.0f, 0.0f)),
            new Variant(new Vector3f(20.0f, 0.0f, 0.0f)),
            new Variant(new Vector3f(30.0f, 0.0f, 0.0f))
        ],
        InterpolationMode.CatmullRomFullCurve);

        var point = spline.GetPoint(0.5f).GetVector3();
        Assert.True(point.X > 10.0f && point.X < 20.0f);
    }

    [Fact]
    public void Double_spline_interpolation_works()
    {
        var spline = new Spline([new Variant(2.0), new Variant(6.0)], InterpolationMode.LinearCurve);
        Assert.Equal(4.0, spline.GetPoint(0.5f).GetDouble());
    }

    [Fact]
    public void Knot_vectors_are_copied_on_construction_and_assignment()
    {
        var constructorKnots = new VariantVector { new Variant(1.0f) };
        var spline = new Spline(constructorKnots);

        constructorKnots.Add(new Variant(2.0f));
        Assert.Single(spline.GetKnots());

        var assignedKnots = new VariantVector { new Variant(3.0f) };
        spline.SetKnots(assignedKnots);
        assignedKnots.Clear();

        Assert.Single(spline.GetKnots());
        Assert.Equal(3.0f, spline.GetKnot(0).GetFloat());
    }

    [Fact]
    public void Control_point_management_and_move_work()
    {
        var scene = new Scene();
        var pathNode = scene.CreateChild("Path");
        var path = pathNode.CreateComponent<SplinePath>();
        path.InterpolationMode = InterpolationMode.LinearCurve;
        path.Speed = 5.0f;
        var a = scene.CreateChild("A", 10);
        a.SetWorldPosition(new Vector3f(0.0f, 0.0f, 0.0f));
        var b = scene.CreateChild("B", 11);
        b.SetWorldPosition(new Vector3f(10.0f, 0.0f, 0.0f));
        var controlled = scene.CreateChild("Controlled", 12);

        path.AddControlPoint(a);
        path.AddControlPoint(b);
        path.ControlledNode = controlled;
        path.Reset();
        path.Move(1.0f);

        Assert.Equal(2u, path.ControlPointIdsAttr[0].GetUInt());
        Assert.True(path.Length > 0.0f);
        Assert.True(controlled.GetWorldPosition().ApproximatelyEquivalent(new Vector3f(5.0f, 0.0f, 0.0f), 0.05f));

        path.RemoveControlPoint(a);
        Assert.Equal(1u, path.ControlPointIdsAttr[0].GetUInt());
        path.ClearControlPoints();
        Assert.Equal(0u, path.ControlPointIdsAttr[0].GetUInt());
    }

    [Fact]
    public void Attribute_id_resolution_works()
    {
        var scene = new Scene();
        var pathNode = scene.CreateChild("Path");
        var path = pathNode.CreateComponent<SplinePath>();
        var a = scene.CreateChild("A", 30);
        a.SetWorldPosition(new Vector3f(0.0f, 0.0f, 0.0f));
        var b = scene.CreateChild("B", 31);
        b.SetWorldPosition(new Vector3f(20.0f, 0.0f, 0.0f));
        var controlled = scene.CreateChild("Controlled", 32);
        using var controlPointIds = new VariantVector([new Variant(2u), new Variant(30u), new Variant(31u)]);
        path.ControlPointIdsAttr = controlPointIds;
        path.ControlledIdAttr = 32u;
        path.ApplyAttributes();
        path.InterpolationMode = InterpolationMode.LinearCurve;
        path.SetPosition(0.25f);

        Assert.Same(controlled, path.ControlledNode);
        Assert.True(path.GetPoint(0.25f).ApproximatelyEquivalent(new Vector3f(5.0f, 0.0f, 0.0f), 0.05f));
    }

    [Fact]
    public void Class_shape_listener_updates_and_attribute_value_ownership_match_cpp()
    {
        Assert.False(typeof(SplinePath).IsSealed);

        var scene = new Scene();
        var path = scene.CreateChild("Path").CreateComponent<SplinePath>();
        path.InterpolationMode = InterpolationMode.LinearCurve;
        var a = scene.CreateChild("A", 40);
        var b = scene.CreateChild("B", 41);
        a.SetWorldPosition(Vector3f.Zero);
        b.SetWorldPosition(new Vector3f(10.0f, 0.0f, 0.0f));

        path.AddControlPoint(a);
        path.AddControlPoint(b);
        Assert.Contains(path, a.GetListeners());
        Assert.Contains(path, b.GetListeners());

        a.SetWorldPosition(new Vector3f(2.0f, 0.0f, 0.0f));
        Assert.True(path.GetPoint(0.0f).ApproximatelyEquivalent(new Vector3f(2.0f, 0.0f, 0.0f)));

        using var ids = new VariantVector([new Variant(3u), new Variant(40u)]);
        path.ControlPointIdsAttr = ids;
        ids.Dispose();

        Assert.Equal(4, path.ControlPointIdsAttr.Count);
        Assert.Equal(3u, path.ControlPointIdsAttr[0].GetUInt());
        Assert.Equal(40u, path.ControlPointIdsAttr[1].GetUInt());
        Assert.Equal(0u, path.ControlPointIdsAttr[2].GetUInt());
        Assert.Equal(0u, path.ControlPointIdsAttr[3].GetUInt());

        path.ClearControlPoints();
        Assert.DoesNotContain(path, a.GetListeners());
        Assert.DoesNotContain(path, b.GetListeners());
    }

    [Fact]
    public void Null_controlled_node_and_missing_controlled_id_preserve_existing_target()
    {
        var scene = new Scene();
        var path = scene.CreateChild("Path").CreateComponent<SplinePath>();
        var controlled = scene.CreateChild("Controlled", 50);
        path.ControlledNode = controlled;

        path.ControlledNode = null;
        Assert.Same(controlled, path.ControlledNode);

        path.ControlledIdAttr = 51u;
        path.ApplyAttributes();
        Assert.Same(controlled, path.ControlledNode);
    }
}
