namespace RedBolt;

/// <summary>Static model component with fixed position in relation to the camera.</summary>
public class Skybox : StaticModel
{
    /// <summary>Construct.</summary>
    public Skybox()
    {
        SetReflectionMode(ReflectionMode.Zone);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _customWorldTransforms.Clear();
        base.Dispose();
    }

    /// <summary>Register object factory. StaticModel must be registered first.</summary>
    public new static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<Skybox>(ObjectCategory.Category_Geometry);
    }

    /// <summary>Process octree raycast. May be called from a worker thread.</summary>
    public override void ProcessRayQuery(RayOctreeQuery query, List<RayQueryResult> results)
    {
        // Do not record a raycast result for a skybox, as it would block all other results
    }

    /// <summary>Calculate distance and prepare batches for rendering. May be called from worker thread(s), possibly re-entrantly.</summary>
    public override void UpdateBatches(FrameInfo frame)
    {
        SetDistance(0.0f);
        var camera = frame.Camera!;

        if (frame.FrameNumber != _lastFrame)
        {
            _customWorldTransforms.Clear();
            _lastFrame = frame.FrameNumber;
        }

        var customWorldTransform = Node.GetWorldTransform();
        customWorldTransform.SetTranslation(Node.GetWorldPosition() + camera.GetEffectiveWorldTransform().Translation);
        if (!_customWorldTransforms.TryGetValue(camera, out var worldTransform))
        {
            worldTransform = [customWorldTransform];
            _customWorldTransforms.Add(camera, worldTransform);
        }

        for (var i = 0; i < _batches.Count; ++i)
        {
            var batch = _batches[i];
            batch.WorldTransform = worldTransform;
            batch.Distance = 0.0f;
            _batches[i] = batch;
        }
    }

    /// <summary>Return skybox image.</summary>
    public ImageCube? GetImage()
    {
        if (_batches.Count == 0 || _batches[0].Material is not { } material)
            return null;

        if (material.GetTexture(ShaderResources.Albedo.GetHash()) is not TextureCube texture)
            return null;

        return GetSubsystem<ResourceCache>()!.GetResourceOrNull<ImageCube>(texture.Name);
    }

    /// <summary>Recalculate the world-space bounding box.</summary>
    protected override void OnWorldBoundingBoxUpdate()
    {
        SetWorldBoundingBox(new BoundingBox(-MathDefs.LargeValue, MathDefs.LargeValue));
    }

    /// <summary>Custom world transform per camera.</summary>
    protected readonly Dictionary<Camera, Matrix3x4[]> _customWorldTransforms = [];
    /// <summary>Last frame counter for knowing when to erase the custom world transforms of previous frame.</summary>
    protected uint _lastFrame;
}
