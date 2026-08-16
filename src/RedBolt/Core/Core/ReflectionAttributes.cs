// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Core;

using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.CompilerServices;

using RedBolt.Container;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ReflectedAttribute : Attribute
{
    public ReflectedAttribute(string category = "")
    {
        Category = category;
    }

    public string Category { get; }

    public bool Factory { get; set; } = true;

    public bool InheritAttributes { get; set; } = true;

    public Type? InheritAttributesFrom { get; set; }

    public int InheritAttributesOrder { get; set; }
}

[AttributeUsage(
    AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = false)]
public sealed class SerializedAttribute : Attribute
{
    public SerializedAttribute(string name)
    {
        Name = name;
    }

    public SerializedAttribute(string name, Type valueType)
    {
        Name = name;
        ValueType = valueType;
    }

    public string Name { get; }

    public Type? ValueType { get; }

    public string? AccessorFactory { get; set; }

    public Type? AccessorType { get; set; }

    public string? Configure { get; set; }

    public string? DefaultSource { get; set; }

    public string[]? EnumNames { get; set; }

    public string? EnumNamesSource { get; set; }

    public string? Getter { get; set; }

    public AttributeMode Mode { get; set; } = AttributeMode.Default;

    public string? Setter { get; set; }

    public AttributeScopeHint ScopeHint { get; set; } = AttributeScopeHint.Attribute;
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class CallableAttribute : Attribute
{
    public CallableAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public string Label { get; set; } = string.Empty;

    public string? LabelSource { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RemoveSerializedAttribute : Attribute
{
    public RemoveSerializedAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}

public static class GeneratedReflectionRegistration
{
    private sealed class ContextRegistration
    {
        public readonly HashSet<Type> Registered = [];
        public readonly HashSet<Type> Registering = [];
    }

    private static readonly object Sync = new();
    private static readonly Dictionary<Type, Action<Context>> TypeRegistrars = [];
    private static readonly Dictionary<Assembly, List<Type>> AssemblyTypes = [];
    private static readonly ConditionalWeakTable<Context, ContextRegistration> ContextRegistrations = new();
    private static Context? _currentContext;

    public static void Add(Type type, Action<Context> registrar)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(registrar);

        Context? context;
        lock (Sync)
        {
            if (!TypeRegistrars.TryAdd(type, registrar))
                throw new InvalidOperationException($"Generated reflection for {type.FullName} is registered multiple times.");

            if (!AssemblyTypes.TryGetValue(type.Assembly, out var types))
            {
                types = [];
                AssemblyTypes.Add(type.Assembly, types);
            }
            types.Add(type);
            var loadContext = AssemblyLoadContext.GetLoadContext(type.Assembly);
            context = type.Assembly == typeof(Context).Assembly
                || loadContext is not null && loadContext != AssemblyLoadContext.Default
                ? null
                : _currentContext;
        }

        if (context is not null)
            RegisterType(context, type);
    }

    public static void RegisterAssembly(Context context, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(assembly);

        Type[] types;
        lock (Sync)
        {
            if (!AssemblyTypes.TryGetValue(assembly, out var registeredTypes))
                RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);

            if (!AssemblyTypes.TryGetValue(assembly, out registeredTypes))
                throw new InvalidOperationException($"Assembly {assembly.FullName} has no generated RedBolt reflection registrar.");

            types = registeredTypes.ToArray();
        }

        foreach (var type in types.OrderBy(GetInheritanceDepth))
            RegisterType(context, type, true);
    }

    internal static AssemblyRegistration RegisterAssemblyScoped(Context context, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(assembly);

        Type[] types;
        lock (Sync)
        {
            if (!AssemblyTypes.TryGetValue(assembly, out var registeredTypes))
                RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
            if (!AssemblyTypes.TryGetValue(assembly, out registeredTypes))
                return new AssemblyRegistration(context, assembly, []);
            types = registeredTypes.ToArray();
        }

        var ownedTypes = types
            .Where(type => context.GetReflection(new StringHash(type.GetClassName())) is null)
            .ToArray();
        var conflict = types.FirstOrDefault(type =>
        {
            var reflection = context.GetReflection(new StringHash(type.GetClassName()));
            return reflection is not null && reflection.ManagedType != type;
        });
        if (conflict is not null)
            throw new InvalidOperationException($"Reflection type '{conflict.GetClassName()}' is already registered by another assembly.");
        try
        {
            RegisterAssembly(context, assembly);
            return new AssemblyRegistration(context, assembly, ownedTypes);
        }
        catch
        {
            UnregisterAssembly(context, ownedTypes);
            throw;
        }
    }

    internal static void UnregisterAssembly(Context context, IReadOnlyList<Type> types)
    {
        for (var index = types.Count - 1; index >= 0; --index)
        {
            var type = types[index];
            var typeId = new StringHash(type.GetClassName());
            if (context.GetReflection(typeId)?.ManagedType == type)
                context.RemoveReflection(typeId);
        }

        lock (Sync)
        {
            if (!ContextRegistrations.TryGetValue(context, out var registration))
                return;
            foreach (var type in types)
                registration.Registered.Remove(type);
        }
    }

    internal static void ForgetCollectibleAssembly(Assembly assembly)
    {
        if (AssemblyLoadContext.GetLoadContext(assembly)?.IsCollectible != true)
            return;

        lock (Sync)
        {
            if (!AssemblyTypes.Remove(assembly, out var types))
                return;
            foreach (var type in types)
                TypeRegistrars.Remove(type);
        }
    }

    public static void RegisterType(Context context, Type type)
    {
        RegisterType(context, type, false);
    }

    public static ObjectReflection? FindBaseReflection(Context context, Type baseType)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(baseType);

        for (var type = baseType; type is not null && typeof(RedObject).IsAssignableFrom(type); type = type.BaseType)
        {
            var reflection = context.GetReflection(new StringHash(type.GetClassName()));
            if (reflection is not null)
                return reflection;
        }
        return null;
    }

    internal static void RefreshExternalAssemblies(Context context)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (AssemblyLoadContext.GetLoadContext(assembly) == AssemblyLoadContext.Default)
                RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
        }

        Assembly[] assemblies;
        lock (Sync)
        {
            assemblies = AssemblyTypes.Keys
                .Where(static assembly => assembly != typeof(Context).Assembly
                    && AssemblyLoadContext.GetLoadContext(assembly) == AssemblyLoadContext.Default)
                .ToArray();
        }

        foreach (var assembly in assemblies)
            RegisterAssembly(context, assembly);
    }

    private static void RegisterType(Context context, Type type, bool refresh)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(type);

        Action<Context> registrar;
        ContextRegistration registration;
        lock (Sync)
        {
            if (!TypeRegistrars.TryGetValue(type, out registrar!))
                RuntimeHelpers.RunModuleConstructor(type.Module.ModuleHandle);

            if (!TypeRegistrars.TryGetValue(type, out registrar!))
                throw new InvalidOperationException($"Type {type.FullName} has no generated RedBolt reflection registrar.");

            registration = ContextRegistrations.GetOrCreateValue(context);
            if (!refresh && registration.Registered.Contains(type))
                return;
            if (!registration.Registering.Add(type))
                throw new InvalidOperationException($"Generated reflection inheritance cycle contains {type.FullName}.");
        }

        var succeeded = false;
        try
        {
            registrar(context);
            succeeded = true;
        }
        finally
        {
            lock (Sync)
            {
                registration.Registering.Remove(type);
                if (succeeded)
                    registration.Registered.Add(type);
            }
        }
    }

    internal static void SetCurrentContext(Context context)
    {
        Assembly[] assemblies;
        lock (Sync)
        {
            _currentContext = context;
            assemblies = AssemblyTypes.Keys
                .Where(static assembly => AssemblyLoadContext.GetLoadContext(assembly) == AssemblyLoadContext.Default)
                .ToArray();
        }

        foreach (var assembly in assemblies)
            RegisterAssembly(context, assembly);
    }

    private static int GetInheritanceDepth(Type type)
    {
        var depth = 0;
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            ++depth;
        return depth;
    }
}

/// <summary>Scoped ownership of reflection registered from one user assembly.</summary>
public sealed class AssemblyRegistration : IDisposable
{
    private Context? _context;
    private IReadOnlyList<Type>? _types;

    internal AssemblyRegistration(Context context, Assembly assembly, IReadOnlyList<Type> types)
    {
        _context = context;
        Assembly = assembly;
        _types = types;
    }

    public Assembly Assembly { get; }

    public void Dispose()
    {
        if (_context is null || _types is null)
            return;
        GeneratedReflectionRegistration.UnregisterAssembly(_context, _types);
        _context = null;
        _types = null;
        GC.SuppressFinalize(this);
    }
}
