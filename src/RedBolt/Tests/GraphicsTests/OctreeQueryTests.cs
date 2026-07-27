using RedBolt;

using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Scene;
namespace RedBolt.Tests;
using Scene = global::RedBolt.Scene.Scene;


[Collection(nameof(HeadlessEngineCollection))]
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
        octree.Update(new FrameInfo());

        var drawableResults = new List<Drawable> { drawable };
        octree.GetDrawables(new BoxOctreeQuery(drawableResults, new BoundingBox(-2.0f, 2.0f)));
        Assert.Same(drawable, Assert.Single(drawableResults));

        var rayResults = new List<RayQueryResult> { new() };
        var query = new RayOctreeQuery(rayResults, new Ray(new Vector3f(0.0f, 0.0f, -10.0f), Vector3f.Forward),
            RayQueryLevel.RayAabb, maxDistance: 100.0f);
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

    [Fact]
    public void Ray_query_result_copies_values_and_preserves_borrowed_pointers()
    {
        var drawable = new StaticModel();
        var node = new Scene().CreateChild("Node");
        var original = new RayQueryResult
        {
            Position = Vector3f.One,
            Distance = 1.0f,
            Drawable = drawable,
            Node = node
        };

        var copy = original;
        copy.Position = Vector3f.Zero;
        copy.Distance = 2.0f;

        Assert.Equal(Vector3f.One, original.Position);
        Assert.Equal(1.0f, original.Distance);
        Assert.Same(drawable, copy.Drawable);
        Assert.Same(node, copy.Node);
    }

    [Fact]
    public void Frustum_query_copies_constructor_value()
    {
        var frustum = new Frustum();
        frustum.Define(new BoundingBox(-2.0f, 2.0f));

        var query = new FrustumOctreeQuery([], frustum);

        Assert.NotSame(frustum, query.Frustum);
        var copiedVertex = query.Frustum.Vertices[0];
        frustum.Define(new BoundingBox(10.0f, 20.0f));
        Assert.Equal(copiedVertex, query.Frustum.Vertices[0]);
    }

    [Fact]
    public void Query_disposal_destroys_only_builtin_storage()
    {
        var drawable = new StaticModel();
        var drawableResults = new List<Drawable> { drawable };
        var drawableQuery = new PointOctreeQuery(drawableResults, Vector3f.Zero);
        drawableQuery.Dispose();
        Assert.Same(drawable, Assert.Single(drawableResults));

        var externalResults = new List<RayQueryResult> { new() { Drawable = drawable } };
        var externalQuery = new RayOctreeQuery(externalResults, new Ray());
        externalQuery.Dispose();
        Assert.Same(drawable, Assert.Single(externalResults).Drawable);

        var builtinQuery = new RayOctreeQuery(new Ray());
        builtinQuery.Result.Add(new RayQueryResult { Drawable = drawable });
        builtinQuery.Dispose();
        Assert.Empty(builtinQuery.Result);
    }
}
