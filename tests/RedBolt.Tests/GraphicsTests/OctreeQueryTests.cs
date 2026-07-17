using RedBolt;

namespace RedBolt.Tests;

public sealed class OctreeQueryTests
{
    [Fact]
    public void Point_query_filters_drawables_by_point_flags_and_view_mask()
    {
        var drawable = new Scene().CreateChild("Drawable").CreateComponent<StaticModel>();
        drawable.SetBoundingBox(new BoundingBox(-1.0f, 1.0f));
        var result = new List<Drawable>();
        var query = new PointOctreeQuery(result, Vector3f.Zero);

        query.TestDrawables([drawable], inside: false);

        Assert.Same(drawable, Assert.Single(result));
    }

    [Fact]
    public void Sphere_box_and_frustum_queries_match_octant_intersections()
    {
        var box = new BoundingBox(-1.0f, 1.0f);
        var sphere = new Sphere(Vector3f.Zero, 2.0f);
        var frustum = new Frustum();
        frustum.Define(new BoundingBox(-2.0f, 2.0f));

        Assert.NotEqual(Intersection.Outside, new SphereOctreeQuery([], sphere).TestOctant(box, false));
        Assert.NotEqual(Intersection.Outside, new BoxOctreeQuery([], box).TestOctant(new BoundingBox(0.5f, 2.0f), false));
        Assert.NotEqual(Intersection.Outside, new FrustumOctreeQuery([], frustum).TestOctant(box, false));
    }

    [Fact]
    public void All_content_query_ignores_bounds_but_filters_flags()
    {
        var geometry = new StaticModel();
        var result = new List<Drawable>();
        var query = new AllContentOctreeQuery(result, DrawableFlags.Geometry, Drawable.DefaultViewMask);

        query.TestDrawables([geometry], inside: false);

        Assert.Same(geometry, Assert.Single(result));
        Assert.Equal(Intersection.Inside, query.TestOctant(new BoundingBox(), inside: false));
    }

    [Fact]
    public void Octree_query_overloads_clear_and_fill_query_storage()
    {
        var scene = new Scene();
        var octree = scene.CreateComponent<Octree>();
        var drawable = scene.CreateChild("Drawable").CreateComponent<StaticModel>();
        drawable.SetBoundingBox(new BoundingBox(-1.0f, 1.0f));

        var drawableResults = new List<Drawable> { drawable };
        octree.GetDrawables(new BoxOctreeQuery(drawableResults, new BoundingBox(-2.0f, 2.0f)));
        Assert.Same(drawable, Assert.Single(drawableResults));

        var rayResults = new List<RayQueryResult> { new() };
        var query = new RayOctreeQuery(rayResults, new Ray(new Vector3f(0.0f, 0.0f, -10.0f), Vector3f.Forward), maxDistance: 100.0f);
        octree.RaycastSingle(query);
        Assert.Single(rayResults);
        Assert.Same(drawable, rayResults[0].Drawable);
    }

    [Fact]
    public void Query_results_compare_like_cpp_structs()
    {
        var drawable = new StaticModel();
        var node = new Scene().CreateChild("Node");
        var first = new OctreeQueryResult { Drawable = drawable, Node = node };
        var second = new OctreeQueryResult { Drawable = drawable, Node = node };

        Assert.Equal(first, second);
        Assert.False(first != second);

        var rayFirst = new RayQueryResult { Drawable = drawable, Node = node, Distance = 1.0f };
        var raySecond = new RayQueryResult { Drawable = drawable, Node = node, Distance = 1.0f };
        Assert.Equal(rayFirst, raySecond);
        raySecond.Distance = 2.0f;
        Assert.NotEqual(rayFirst, raySecond);
    }
}
