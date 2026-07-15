namespace RedBolt;

public sealed class Skybox : StaticModel
{
    private readonly Dictionary<Camera, Matrix3x4> _customWorldTransforms = [];
    private uint _lastFrame;

    public Skybox()
    {
        SetReflectionMode(ReflectionMode.Zone);
    }

    public override void ProcessRayQuery(RayOctreeQuery query, List<RayQueryResult> results)
    {
    }

    public override void UpdateBatches(CommonFrameInfo frame)
    {
        SetDistance(0.0f);
        var camera = frame.Camera;
        if (camera is null || !IsAttached)
            return;

        if (frame.FrameNumber != _lastFrame)
        {
            _customWorldTransforms.Clear();
            _lastFrame = frame.FrameNumber;
        }

        var customWorldTransform = Node.GetWorldTransform();
        customWorldTransform.SetTranslation(Node.GetWorldPosition() + camera.GetEffectiveWorldTransform().Translation);
        _customWorldTransforms[camera] = customWorldTransform;
        var worldTransform = _customWorldTransforms[camera];
        for (var i = 0; i < _batches.Count; ++i)
        {
            var batch = _batches[i];
            batch.WorldTransform = [worldTransform];
            batch.Distance = 0.0f;
            _batches[i] = batch;
        }
    }

    public ImageCube? GetImage()
    {
        if (_batches.Count == 0 || _batches[0].Material is not { } material)
            return null;

        if (material.GetTexture(new StringHash("Albedo")) is not TextureCube texture)
            return null;

        return GetSubsystem<ResourceCache>()?.GetResourceOrNull<ImageCube>(texture.Name);
    }

    protected override void OnWorldBoundingBoxUpdate()
    {
        SetWorldBoundingBox(new BoundingBox(-MathDefs.LargeValue, MathDefs.LargeValue));
    }
}
