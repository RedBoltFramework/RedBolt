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

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using RedBolt.Container;
using RedBolt.Core;

/// <summary>
/// Memory layout definition.
/// </summary>
public struct ParticleGraphSpan
{
    /// <summary>
    /// Construct.
    /// </summary>
    public ParticleGraphSpan()
    {
        Offset = 0;
        Size = 0;
    }

    /// <summary>
    /// Construct.
    /// </summary>
    public ParticleGraphSpan(int offset, int sizeInBytes)
    {
        Offset = offset;
        Size = sizeInBytes;
    }

    /// <summary>
    /// Offset in the buffer.
    /// </summary>
    public int Offset;

    /// <summary>
    /// Size in bytes.
    /// </summary>
    public int Size;

    /// <summary>
    /// Make a span of type T from memory buffer.
    /// </summary>
    public readonly Span<T> MakeSpan<T>(Span<byte> buffer) where T : unmanaged
    {
        if (Size == 0)
            return [];
        var slice = buffer.Slice(Offset, Size);
        return MemoryMarshal.Cast<byte, T>(slice);
    }

    // TODO: port managed-reference value support when particle memory buffer ownership is ported.
    // template <typename T> ea::span<T> MakeSpan(ea::span<uint8_t> buffer) const
    // {
    //     if (size_ == 0)
    //         return {};
    //     const auto slice = buffer.subspan(offset_, size_);
    //     return ea::span<T>(reinterpret_cast<T*>(slice.begin()), reinterpret_cast<T*>(slice.end()));
    // }
}

/// <summary>
/// Memory layout for attributes.
/// </summary>
public class ParticleGraphAttributeLayout
{
    /// <summary>
    /// Reset layout.
    /// </summary>
    public void Reset(int offset, int capacity)
    {
        _capacity = capacity;
        _position = offset;
        _attributes.Clear();
    }

    /// <summary>
    /// Get or add attribute.
    /// </summary>
    public int GetOrAddAttribute(string name, VariantType type)
    {
        var nameHash = new StringHash(name);

        //Look for attribute with linear complexity. We going to have low number of attributes so it doesn't worth to build a map.
        for (var i = 0; i < _attributes.Count; ++i)
        {
            if (_attributes[i].NameHash == nameHash && _attributes[i].Type == type)
                return i;
        }

        var index = _attributes.Count;
        var size = VariantTypeUtility.GetVariantTypeSize(type) * _capacity;
        _attributes.Add(new AttrSpan
        {
            Name = name,
            NameHash = nameHash,
            Type = type,
            Span = new ParticleGraphSpan(_position, size)
        });
        _position += size;
        return index;
    }

    /// <summary>
    /// Get name by attribute index.
    /// </summary>
    public string GetName(int attrIndex) => _attributes[attrIndex].Name;

    /// <summary>
    /// Get type by attribute index.
    /// </summary>
    public VariantType GetType(int attrIndex) => _attributes[attrIndex].Type;

    /// <summary>
    /// Get number of attributes.
    /// </summary>
    public int NumAttributes => _attributes.Count;

    /// <summary>
    /// Get amount of required memory to host all attributes.
    /// </summary>
    public int RequiredMemory => _position;

    /// <summary>
    /// Get span by attribute index.
    /// </summary>
    public ParticleGraphSpan GetSpan(int attrIndex) => _attributes[attrIndex].Span;

    /// <summary>
    /// Attribute layout.
    /// </summary>
    private struct AttrSpan
    {
        /// <summary>
        /// Name of attribute.
        /// </summary>
        public string Name;

        /// <summary>
        /// Attribute name hash.
        /// </summary>
        public StringHash NameHash;

        /// <summary>
        /// Type of attribute.
        /// </summary>
        public VariantType Type;

        /// <summary>
        /// Location at emitter attribute buffer.
        /// </summary>
        public ParticleGraphSpan Span;
    }

    /// <summary>
    /// All known attributes.
    /// </summary>
    private readonly List<AttrSpan> _attributes = [];

    /// <summary>
    /// Size of required attribute buffer.
    /// </summary>
    private int _position;

    /// <summary>
    /// Maximal number of particles.
    /// </summary>
    private int _capacity;
}
