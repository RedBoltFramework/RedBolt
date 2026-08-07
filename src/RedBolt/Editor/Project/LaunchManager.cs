//
// Copyright (c) 2017-2020 the rbfx project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.Editor.Project;

using System;

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
