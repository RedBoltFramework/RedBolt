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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Resource;

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
    public readonly Span<T> MakeSpan<T>(Span<byte> buffer)
    {
        if (Size == 0)
            return [];

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            throw new InvalidOperationException();

        var slice = buffer.Slice(Offset, Size);
        return MemoryMarshal.CreateSpan(
            ref Unsafe.As<byte, T>(ref MemoryMarshal.GetReference(slice)),
            Size / Unsafe.SizeOf<T>());
    }

    /// <summary>
    /// Make a span of type T from memory buffer.
    /// </summary>
    public readonly Span<T> MakeSpan<T>(ParticleGraphMemoryBuffer buffer)
    {
        return buffer.MakeSpan<T>(this);
    }
}

public sealed class ParticleGraphMemoryBuffer
{
    public ParticleGraphMemoryBuffer(int size)
    {
        _buffer = new byte[size];
    }

    public int Size => _buffer.Length;

    public Span<byte> Bytes => _buffer;

    public void Register<T>(ParticleGraphSpan span, int elementCount)
    {
        if (span.Size == 0)
            return;

        _managedSpans.TryAdd(new ManagedSpanKey(span.Offset, typeof(T).TypeHandle), new T[elementCount]);
    }

    public Span<T> MakeSpan<T>(ParticleGraphSpan span)
    {
        if (span.Size == 0)
            return [];

        if (_managedSpans.TryGetValue(new ManagedSpanKey(span.Offset, typeof(T).TypeHandle), out var managedSpan))
            return ((T[])managedSpan).AsSpan();

        return span.MakeSpan<T>(_buffer);
    }

    public Memory<T> MakeMemory<T>(ParticleGraphSpan span)
    {
        if (span.Size == 0)
            return Memory<T>.Empty;

        return ((T[])_managedSpans[new ManagedSpanKey(span.Offset, typeof(T).TypeHandle)]).AsMemory();
    }

    internal void Register(ParticleGraphSpan span, VariantType type, int elementCount)
    {
        switch (type)
        {
            case VariantType.String:
                Register<string>(span, elementCount);
                MakeSpan<string>(span).Fill(string.Empty);
                break;

            case VariantType.Buffer:
                Register<byte[]>(span, elementCount);
                MakeSpan<byte[]>(span).Fill(Array.Empty<byte>());
                break;

            case VariantType.ResourceRef:
                Register<ResourceRef>(span, elementCount);
                var resourceRefs = MakeSpan<ResourceRef>(span);
                for (var i = 0; i < resourceRefs.Length; ++i)
                    resourceRefs[i] = new ResourceRef();
                break;

            case VariantType.ResourceRefList:
                Register<ResourceRefList>(span, elementCount);
                var resourceRefLists = MakeSpan<ResourceRefList>(span);
                for (var i = 0; i < resourceRefLists.Length; ++i)
                    resourceRefLists[i] = new ResourceRefList();
                break;

            case VariantType.VariantVector:
                Register<VariantVector>(span, elementCount);
                var variantVectors = MakeSpan<VariantVector>(span);
                for (var i = 0; i < variantVectors.Length; ++i)
                    variantVectors[i] = new VariantVector();
                break;

            case VariantType.StringVector:
                Register<StringVector>(span, elementCount);
                var stringVectors = MakeSpan<StringVector>(span);
                for (var i = 0; i < stringVectors.Length; ++i)
                    stringVectors[i] = new StringVector();
                break;

            case VariantType.VariantMap:
                Register<VariantMap>(span, elementCount);
                var variantMaps = MakeSpan<VariantMap>(span);
                for (var i = 0; i < variantMaps.Length; ++i)
                    variantMaps[i] = new VariantMap();
                break;

            case VariantType.StringVariantMap:
                Register<StringVariantMap>(span, elementCount);
                var stringVariantMaps = MakeSpan<StringVariantMap>(span);
                for (var i = 0; i < stringVariantMaps.Length; ++i)
                    stringVariantMaps[i] = new StringVariantMap();
                break;

            case VariantType.VariantCurve:
                Register<VariantCurve>(span, elementCount);
                break;
        }
    }

    private readonly struct ManagedSpanKey : IEquatable<ManagedSpanKey>
    {
        public ManagedSpanKey(int offset, RuntimeTypeHandle type)
        {
            _offset = offset;
            _type = type;
        }

        public bool Equals(ManagedSpanKey other)
        {
            return _offset == other._offset && _type.Equals(other._type);
        }

        public override bool Equals(object? obj)
        {
            return obj is ManagedSpanKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_offset, _type);
        }

        private readonly int _offset;
        private readonly RuntimeTypeHandle _type;
    }

    private readonly byte[] _buffer;
    private readonly Dictionary<ManagedSpanKey, Array> _managedSpans = [];
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
        var size = GetParticleGraphValueTypeSize(type) * _capacity;
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

    internal static int GetParticleGraphValueTypeSize(VariantType type)
    {
        return type == VariantType.VariantCurve ? IntPtr.Size : VariantTypeUtility.GetVariantTypeSize(type);
    }
}

/// <summary>
/// Memory layout for intermediate values.
/// </summary>
public class ParticleGraphBufferLayout
{
    /// <summary>
    /// Reset layout.
    /// </summary>
    public void Reset(int capacity)
    {
        _position = 0;
        _capacity = capacity;
    }

    /// <summary>
    /// Allocate span. Returns allocated span index.
    /// </summary>
    public int Allocate(ParticleGraphContainerType container, VariantType type)
    {
        Debug.Assert(container != ParticleGraphContainerType.Auto);
        var index = _spans.Count;
        var size = (container == ParticleGraphContainerType.Scalar ? 1 : _capacity)
            * ParticleGraphAttributeLayout.GetParticleGraphValueTypeSize(type);
        _spans.Add(new PinSpan
        {
            Container = container,
            Type = type,
            Span = new ParticleGraphSpan(_position, size)
        });
        _position += size;
        return index;
    }

    /// <summary>
    /// Get amount of required memory to host intermediate values.
    /// </summary>
    public int RequiredMemory => _position;

    /// <summary>
    /// Get span by index.
    /// </summary>
    public ParticleGraphSpan this[int index] => _spans[index].Span;

    /// <summary>
    /// Attribute layout.
    /// </summary>
    private struct PinSpan
    {
        /// <summary>
        /// Container type of attribute.
        /// </summary>
        public ParticleGraphContainerType Container;

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
    /// Allocated spans.
    /// </summary>
    private readonly List<PinSpan> _spans = [];

    /// <summary>
    /// Size of required attribute buffer.
    /// </summary>
    private int _position;

    /// <summary>
    /// Maximal number of particles.
    /// </summary>
    private int _capacity;
}
