// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Project;

using System;
using System.Collections.Generic;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;

/// <summary>Launch configuration.</summary>
public class LaunchConfiguration : ICloneable, IDisposable
{
    public static readonly string UnspecifiedName = "(unspecified)";

    public string Name;
    public string MainPlugin;
    public StringVariantMap EngineParameters;

    public LaunchConfiguration()
    {
        Name = string.Empty;
        MainPlugin = string.Empty;
        EngineParameters = new StringVariantMap();
    }

    public LaunchConfiguration(string name, string mainPlugin)
    {
        Name = name;
        MainPlugin = mainPlugin;
        EngineParameters = new StringVariantMap();
    }

    private LaunchConfiguration(LaunchConfiguration rhs)
    {
        Name = rhs.Name;
        MainPlugin = rhs.MainPlugin;
        EngineParameters = new StringVariantMap(rhs.EngineParameters);
    }

    public void SerializeInBlock(Archive archive)
    {
        ArchiveSerialization.SerializeOptionalValue(
            archive, "Name", ref Name, string.Empty, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(
            archive, "MainPlugin", ref MainPlugin, string.Empty, ArchiveSerialization.SerializeValue);

        var serializeEngineParameters = !archive.IsUnorderedAccessSupportedInCurrentBlock
            || (archive.IsInput
                ? archive.HasElementOrBlock("EngineParameters")
                : EngineParameters.Count != 0);
        if (serializeEngineParameters)
            ArchiveSerialization.SerializeValue(archive, "EngineParameters", EngineParameters);
        else if (archive.IsInput)
            EngineParameters.Clear();
    }

    public uint ToHash()
    {
        var hash = 0u;
        CombineHash(ref hash, StringHash.Calculate(Name));
        CombineHash(ref hash, StringHash.Calculate(MainPlugin));

        var engineParametersHash = 0u;
        foreach (var (key, value) in EngineParameters)
        {
            CombineHash(ref engineParametersHash, StringHash.Calculate(key));
            CombineHash(ref engineParametersHash, value.ToHash());
        }
        CombineHash(ref hash, engineParametersHash);
        return hash;
    }

    public LaunchConfiguration Clone() => new(this);

    object ICloneable.Clone() => Clone();

    public void Dispose()
    {
        EngineParameters.Dispose();
    }

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }
}

/// <summary>Manages launch configurations in the project.</summary>
public class LaunchManager : RedObject
{
    public LaunchManager()
    {
    }

    public override void Dispose()
    {
        DisposeConfigurations();
        base.Dispose();
    }

    public override void SerializeInBlock(Archive archive)
    {
        var serializeConfigurations = !archive.IsUnorderedAccessSupportedInCurrentBlock
            || (archive.IsInput
                ? archive.HasElementOrBlock("Configurations")
                : _configurations.Count != 0);
        if (!serializeConfigurations)
        {
            if (archive.IsInput)
                DisposeConfigurations();
            return;
        }

        var size = _configurations.Count;
        using var block = archive.OpenArrayBlock("Configurations", size);
        if (archive.IsInput)
        {
            DisposeConfigurations();
            for (var index = 0; index < block.SizeHint; ++index)
                _configurations.Add(new LaunchConfiguration());
        }

        for (var index = 0; index < (archive.IsInput ? block.SizeHint : size); ++index)
        {
            using var elementBlock = archive.OpenUnorderedBlock("element");
            _configurations[index].SerializeInBlock(archive);
        }
    }

    public void AddConfiguration(LaunchConfiguration configuration)
    {
        _configurations.Add(configuration.Clone());
    }

    public void RemoveConfiguration(int index)
    {
        _configurations[index].Dispose();
        _configurations.RemoveAt(index);
    }

    public LaunchConfiguration? FindConfiguration(string name)
    {
        foreach (var configuration in _configurations)
        {
            if (configuration.Name == name)
                return configuration;
        }
        return null;
    }

    public bool HasConfiguration(string name)
    {
        return FindConfiguration(name) is not null;
    }

    public List<LaunchConfiguration> MutableConfigurations => _configurations;

    public IReadOnlyList<LaunchConfiguration> Configurations => _configurations;

    public StringVector GetSortedConfigurations()
    {
        var result = new StringVector();
        foreach (var configuration in _configurations)
            result.Add(configuration.Name);
        result.Sort(StringComparer.Ordinal);
        for (var index = result.Count - 1; index > 0; --index)
        {
            if (result[index] == result[index - 1])
                result.RemoveAt(index);
        }
        return result;
    }

    private void DisposeConfigurations()
    {
        for (var index = _configurations.Count - 1; index >= 0; --index)
            _configurations[index].Dispose();
        _configurations.Clear();
    }

    private readonly List<LaunchConfiguration> _configurations = [];
}
