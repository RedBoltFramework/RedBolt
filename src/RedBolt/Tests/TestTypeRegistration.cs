using RedBolt.Core;

namespace RedBolt.Tests;

internal static class TestTypeRegistration
{
    public static void RegisterFactory<T>()
        where T : RedObject, new()
    {
        var context = Context.Instance;
        var reflection = context.GetReflection<T>();
        if (reflection is null)
            context.AddFactoryReflection<T>();
        else if (!reflection.HasObjectFactory())
            reflection.SetObjectFactory(static () => new T());
    }
}
