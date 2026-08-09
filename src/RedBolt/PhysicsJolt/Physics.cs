namespace RedBolt.PhysicsJolt;

using RedBolt.Core;

public static class Physics
{
    /// <summary>Register physics library objects.</summary>
    public static void RegisterPhysicsLibrary(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.RegisterAssembly(typeof(Physics).Assembly);
    }
}
