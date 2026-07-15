namespace RedBolt;

// C++: No direct C++ equivalent. Resource routing in C++ is handled via ResourceCache.
// This is a C#-specific abstraction.
public abstract class ResourceRouter : Object
{
    public abstract void Route(ref FileIdentifier name);
}
