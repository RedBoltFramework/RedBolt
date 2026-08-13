//
// Copyright (c) 2021 the rbfx project.
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

namespace RedBolt.Particles;

using System.Collections.Generic;
using System.IO;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Resource;

/// <summary>
/// Particle graph effect definition.
/// </summary>
[Reflected]
public partial class ParticleGraphEffect : Resource
{
    /// <summary>
    /// Construct.
    /// </summary>
    public ParticleGraphEffect()
    {
    }

    /// <summary>
    /// Destruct.
    /// </summary>
    public override void Dispose()
    {
        for (var index = _layers.Count - 1; index >= 0; --index)
            _layers[index].Dispose();
        _layers.Clear();
        base.Dispose();
    }

    /// <summary>
    /// Set number of layers.
    /// </summary>
    public void SetNumLayers(int numLayers)
    {
        while (numLayers < _layers.Count)
        {
            _layers[^1].Dispose();
            _layers.RemoveAt(_layers.Count - 1);
        }
        _layers.Capacity = global::System.Math.Max(_layers.Capacity, numLayers);
        while (numLayers > _layers.Count)
            _layers.Add(new ParticleGraphLayer());
    }

    /// <summary>
    /// Get number of layers.
    /// </summary>
    public int NumLayers => _layers.Count;

    /// <summary>
    /// Get layer by index.
    /// </summary>
    public ParticleGraphLayer GetLayer(int layerIndex) => _layers[layerIndex];

    /// <summary>
    /// Load resource from stream. May be called from a worker thread. Return true if successful.
    /// </summary>
    public override bool BeginLoad(Deserializer source)
    {
        var extension = Path.GetExtension(source.Name);

        ResetToDefaults();

        using var xmlFile = new XMLFile();
        if (!xmlFile.Load(source))
            return false;

        return xmlFile.LoadObject("particleGraphEffect", this);
    }

    /// <summary>
    /// Save resource. Return true if successful.
    /// </summary>
    public override bool Save(Serializer destination)
    {
        using var xmlFile = new XMLFile();
        xmlFile.SaveObject("particleGraphEffect", this);
        xmlFile.Save(destination);
        return true;
    }

    /// <summary>
    /// Serialize from/to archive. Return true if successful.
    /// </summary>
    public override void SerializeInBlock(Archive archive)
    {
        var loading = archive.IsInput;
        var size = loading ? 0 : _layers.Count;
        using var block = archive.OpenArrayBlock("layers", size);
        if (loading)
        {
            for (var index = _layers.Count - 1; index >= 0; --index)
                _layers[index].Dispose();
            _layers.Clear();
            size = block.SizeHint;
            _layers.Capacity = global::System.Math.Max(_layers.Capacity, size);
            for (var index = 0; index < size; ++index)
            {
                using var itemBlock = archive.OpenUnorderedBlock("layer");
                var value = new ParticleGraphLayer();
                _layers.Add(value);
                value.SerializeInBlock(archive);
            }
        }
        else
        {
            foreach (var value in _layers)
            {
                using var itemBlock = archive.OpenUnorderedBlock("layer");
                value.SerializeInBlock(archive);
            }
        }
    }

    /// <summary>
    /// Reset to defaults.
    /// </summary>
    private void ResetToDefaults()
    {
        // Needs to be a no-op when async loading, as this does a GetResource() which is not allowed from worker threads
        if (!WorkQueue.IsMainThread())
            return;

        for (var index = _layers.Count - 1; index >= 0; --index)
            _layers[index].Dispose();
        _layers.Clear();
    }

    /// <summary>
    /// Effect layers.
    /// </summary>
    private readonly List<ParticleGraphLayer> _layers = [];
}
