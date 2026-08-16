// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.IO;

using Microsoft.Extensions.Logging;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Logging;

public static partial class ArchiveSerialization
{
    /// <summary>Serialize shared pointer to Object.</summary>
    public static void SerializeSharedPtr<T>(
        Archive archive, string name, ref T? value, bool singleBlock = false, bool ignoreUnknown = false)
        where T : RedObject
    {
        var loading = archive.IsInput;
        using var block = ignoreUnknown
            ? archive.OpenSafeUnorderedBlock(name)
            : archive.OpenUnorderedBlock(name);

        var type = StringHash.Empty;
        var typeName = string.Empty;
        if (!loading && value is not null)
        {
            type = value.GetTypeNameHash();
            typeName = value.GetTypeName();
        }

        SerializeStringHash(archive, singleBlock ? "_Class" : "type", ref type, typeName);

        if (loading)
        {
            // Serialize null object
            if (type == StringHash.Empty)
            {
                value = null;
                return;
            }

            // Create instance
            value = archive.Context?.CreateObject(type) as T;
            if (value is null)
            {
                if (ignoreUnknown)
                {
                    Log.GetLogger().LogWarning("Unknown object type is ignored on serialization");
                    return;
                }
                else
                {
                    throw new ArchiveException(
                        $"Failed to create object '{archive.CurrentBlockPath}/{name}' of type {type.ToDebugString()}");
                }
            }
        }

        if (value is not null)
        {
            if (singleBlock)
                value.SerializeInBlock(archive);
            else
            {
                using var valueBlock = archive.OpenUnorderedBlock("value");
                value.SerializeInBlock(archive);
            }
        }
    }

    /// <summary>Serialize shared pointer to Object.</summary>
    public static void SerializeValue<T>(Archive archive, string name, ref T? value)
        where T : RedObject
    {
        SerializeSharedPtr(archive, name, ref value);
    }
}
