namespace RedBolt.PhysicsJolt;

using RedBolt.Math;

public static class PhysicsUtils
{
    public static bool HasWorldScaleChanged(Vector3f oldWorldScale, Vector3f newWorldScale)
    {
        var delta = newWorldScale - oldWorldScale;
        var dot = delta.DotProduct(delta);
        return dot > 0.01f;
    }
}
