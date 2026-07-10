using RedBolt;

namespace RedBolt.Tests;

public sealed class SplinePathTests
{
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
    public void Control_point_management_and_move_work()
    {
        var scene = new Scene();
        var pathNode = scene.CreateChild("Path");
        var path = pathNode.CreateComponent<SplinePath>();
        path.SetInterpolationMode(InterpolationMode.LinearCurve);
        path.SetSpeed(5.0f);
        var a = scene.CreateChild("A", 10);
        a.SetWorldPosition(new Vector3f(0.0f, 0.0f, 0.0f));
        var b = scene.CreateChild("B", 11);
        b.SetWorldPosition(new Vector3f(10.0f, 0.0f, 0.0f));
        var controlled = scene.CreateChild("Controlled", 12);

        path.AddControlPoint(a);
        path.AddControlPoint(b);
        path.SetControlledNode(controlled);
        path.Reset();
        path.Move(1.0f);

        Assert.Equal((uint)2, path.GetControlPointIdsAttr()[0]);
        Assert.True(path.GetLength() > 0.0f);
        Assert.True(controlled.GetWorldPosition().ApproximatelyEquivalent(new Vector3f(5.0f, 0.0f, 0.0f), 0.05f));

        path.RemoveControlPoint(a);
        Assert.Equal((uint)1, path.GetControlPointIdsAttr()[0]);
        path.ClearControlPoints();
        Assert.Equal((uint)0, path.GetControlPointIdsAttr()[0]);
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
        path.SetControlPointIdsAttr([2u, 30u, 31u]);
        path.SetControlledIdAttr(32u);
        path.ApplyAttributes();
        path.SetInterpolationMode(InterpolationMode.LinearCurve);
        path.SetPosition(0.25f);

        Assert.Same(controlled, path.GetControlledNode());
        Assert.True(path.GetPoint(0.25f).ApproximatelyEquivalent(new Vector3f(5.0f, 0.0f, 0.0f), 0.05f));
    }
}
