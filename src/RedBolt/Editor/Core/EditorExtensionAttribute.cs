// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using RedBolt.Core;
using RedBolt.Plugins;

/// <summary>Marks a static type whose conventional Apply method contributes a scoped Editor extension.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EditorExtensionAttribute : Attribute
{
    public EditorExtensionAttribute()
    {
    }

    public EditorExtensionAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>Stable extension name. The attributed type's full name is used when omitted.</summary>
    public string? Name { get; }
}

/// <summary>Plugin application whose attributed Editor extensions are attached automatically.</summary>
public abstract class EditorPluginApplication : PluginApplication
{
    protected sealed override void Load()
    {
        LoadEditorPlugin();
        try
        {
            _extensionRegistrations = EditorExtensionRegistration.Register(
                GetType().Assembly, GetSubsystem<EditorPluginManager>());
        }
        catch
        {
            UnloadEditorPlugin();
            throw;
        }
    }

    protected sealed override void Unload()
    {
        if (_extensionRegistrations is { } registrations)
        {
            for (var index = registrations.Count - 1; index >= 0; --index)
                registrations[index].Dispose();
            _extensionRegistrations = null;
        }
        UnloadEditorPlugin();
    }

    /// <summary>Initialize resources and subsystems before attributed extensions are applied.</summary>
    protected virtual void LoadEditorPlugin()
    {
    }

    /// <summary>Release resources and subsystems after attributed extensions are removed.</summary>
    protected virtual void UnloadEditorPlugin()
    {
    }

    private List<IDisposable>? _extensionRegistrations;
}

internal static class EditorExtensionRegistration
{
    private static readonly MethodInfo RegisterTypedMethod = typeof(EditorExtensionRegistration)
        .GetMethod(nameof(RegisterTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static List<IDisposable> Register(Assembly assembly, EditorPluginManager manager)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(manager);

        var registrations = new List<IDisposable>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (var type in assembly.GetTypes().OrderBy(static type => type.FullName, StringComparer.Ordinal))
            {
                var attribute = type.GetCustomAttribute<EditorExtensionAttribute>(false);
                if (attribute is null)
                    continue;
                if (!type.IsAbstract || !type.IsSealed || type.ContainsGenericParameters)
                    throw Invalid(type, "the attributed type must be a non-generic static class");

                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                        | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(static method => method.Name == "Apply")
                    .ToArray();
                if (methods.Length != 1)
                    throw Invalid(type, "the attributed type must declare exactly one static Apply method");

                var method = methods[0];
                var parameters = method.GetParameters();
                if (method.IsGenericMethodDefinition || parameters.Length != 1
                    || !typeof(RedObject).IsAssignableFrom(parameters[0].ParameterType)
                    || method.ReturnType != typeof(IDisposable))
                {
                    throw Invalid(type,
                        "Apply must be non-generic, accept one RedObject-derived target, and return IDisposable");
                }

                var name = attribute.Name ?? type.FullName
                    ?? throw Invalid(type, "the attributed type has no stable name");
                if (!names.Add(name))
                    throw Invalid(type, $"extension name '{name}' is duplicated in the plugin assembly");

                registrations.Add((IDisposable)RegisterTypedMethod
                    .MakeGenericMethod(parameters[0].ParameterType)
                    .Invoke(null, [manager, name, method])!);
            }
            return registrations;
        }
        catch
        {
            for (var index = registrations.Count - 1; index >= 0; --index)
                registrations[index].Dispose();
            throw;
        }
    }

    private static IDisposable RegisterTyped<T>(EditorPluginManager manager, string name, MethodInfo method)
        where T : RedObject
    {
        var callback = method.CreateDelegate<Func<T, IDisposable?>>();
        return manager.AddScopedPlugin<T>(name, callback);
    }

    private static InvalidOperationException Invalid(Type type, string message) => new(
        $"Invalid Editor extension '{type.FullName}': {message}.");
}
