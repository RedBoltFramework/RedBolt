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
using System.Runtime.InteropServices;

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
