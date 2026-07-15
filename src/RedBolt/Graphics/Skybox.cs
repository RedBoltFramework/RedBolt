namespace RedBolt;

public sealed class Skybox : StaticModel
{
    // TODO: port - C++ code:
    // void ProcessRayQuery(const RayOctreeQuery& query, ea::vector<RayQueryResult>& results) override;
    // TODO: port - C++ code:
    // void UpdateBatches(const FrameInfo& frame) override;
    // TODO: port - C++ fields:
    // ea::unordered_map<Camera*, Matrix3x4> customWorldTransforms_;
    // unsigned lastFrame_;

    public Skybox()
    {
        SetReflectionMode(ReflectionMode.Zone);
    }

    // C++: ImageCube* GetImage() const;
    public object? GetImage() => null;

    protected override void OnWorldBoundingBoxUpdate()
    {
        SetWorldBoundingBox(new BoundingBox(-MathDefs.LargeValue, MathDefs.LargeValue));
    }
}
