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

using RedBolt.Container;
using RedBolt.Core;

/// <summary>
/// Particle graph effect definition.
/// </summary>
public class ParticleGraphSystem : RedObject
{
    public ParticleGraphSystem()
    {
        RegisterParticleGraphLibrary(Context, this);
    }

    public override void Dispose()
    {
        _factories.Clear();
        base.Dispose();
    }

    public ParticleGraphNode? CreateObject(StringHash type)
    {
        return _factories.TryGetValue(type, out var factory) ? factory() : null;
    }

    public void Register<T>() where T : ParticleGraphNode, new()
    {
        _factories.Add(typeof(T).GetTypeId(), static () => new T());
    }

    /// <summary>
    /// Register Particle Graph library objects.
    /// </summary>
    public static void RegisterParticleGraphLibrary(Context context, ParticleGraphSystem system)
    {
        context.RegisterAssembly(typeof(ParticleGraphEffect).Assembly);
        ParticleGraphNodes.All.RegisterGraphNodes(system);
    }

    private readonly Dictionary<StringHash, Func<ParticleGraphNode>> _factories = [];
}
