# Managed plugin modules

A managed plugin assembly declares its application entry point with `PluginModuleAttribute`. The desktop plugin
loader discovers this metadata without loading candidate assemblies.

## Reflected types

Loading a plugin automatically registers generated RedBolt reflection from the plugin assembly and its actual
managed dependency closure. Every type marked with `ReflectedAttribute` in those assemblies becomes available to
scene loading, object factories, inspectors, and asset pipelines. Plugins do not call
`Context.RegisterAssemblyScoped` for their dependencies.

The registration lifetime belongs to the loaded plugin generation. Dependencies are registered before the plugin
is created and are released in reverse order when that generation is unloaded.

Plugin-owned files are resolved through `Plugin.GetPluginPath`. The loader initializes
`Plugin.PluginDirectory` from the discovered entry assembly path, including when the assembly itself is
loaded from a stream. Plugin code should not derive resource directories from `Assembly.Location`.

## Editor extensions

Editor companions derive from `EditorPluginApplication`. A static class marked with
`EditorExtensionAttribute` contributes one scoped extension:

```csharp
[EditorExtension("Example.DefaultTransformer")]
internal static class DefaultTransformerExtension
{
    public static IDisposable Apply(Project project) =>
        project.RegisterDefaultAssetTransformer<ExampleTransformer>();
}
```

The class must be non-generic and static. It must declare exactly one non-generic static method named `Apply` that
accepts one `RedObject`-derived editor target and returns `IDisposable`. The target parameter determines which
existing and future editor objects receive the extension.

The editor initializes the plugin before attaching attributed extensions. On unload it disposes the
extensions in reverse order before releasing plugin-owned resources and subsystems.

Imperative `EditorPluginManager` registration remains available for integrations selected dynamically at runtime.
