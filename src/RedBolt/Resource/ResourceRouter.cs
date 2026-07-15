namespace RedBolt;

// This is a C#-specific abstraction.
public abstract class ResourceRouter : Object
{
    public abstract void Route(ref FileIdentifier name);
}
