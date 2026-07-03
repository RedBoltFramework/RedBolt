using System.Reflection;

try
{
    var sdlAssembly = Assembly.Load("RedBolt.Bindings.Sdl");
    var diligentAssembly = Assembly.Load("RedBolt.Bindings.Diligent");

    var bindingsType = sdlAssembly.GetType("RedBolt.Bindings.Sdl.SdlNativeBindings")
        ?? throw new InvalidOperationException("Generated SDL binding class was not found.");

    var valueTypeEnum = diligentAssembly.GetTypes()
        .FirstOrDefault(type => type.IsEnum && type.GetField("VtFloat32") is not null)
        ?? throw new InvalidOperationException("Generated Diligent VALUE_TYPE enum was not found.");

    if (Enum.Parse(valueTypeEnum, "VtFloat32") is not object vtFloat32 || Convert.ToInt32(vtFloat32) <= 0)
        throw new InvalidOperationException("Generated Diligent VT_FLOAT32 enum value was invalid.");

    var initResult = Convert.ToInt32(InvokeGenerated(bindingsType, "SdlInit", 0u));
    if (initResult != 0)
        throw new InvalidOperationException($"Generated SDL_Init binding failed with {initResult}.");

    _ = Convert.ToUInt32(InvokeGenerated(bindingsType, "SdlWasInit", 0u));
    InvokeGenerated(bindingsType, "SdlQuit");

    Console.WriteLine("RedBolt generated dependency binding smoke OK");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static object? InvokeGenerated(Type bindingsType, string methodName, params object[] args)
{
    var method = bindingsType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
        ?? throw new MissingMethodException(bindingsType.FullName, methodName);
    return method.Invoke(null, args);
}
