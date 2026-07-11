namespace RedBolt;

public abstract class ResourceRouter : Object
{
    public abstract void Route(ref FileIdentifier name);
}
