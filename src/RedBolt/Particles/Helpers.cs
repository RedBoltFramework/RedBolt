//
// Copyright (c) 2021-2022 the rbfx project.
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

namespace RedBolt.Particles.ParticleGraphNodes;

using RedBolt.Core;

public delegate void PatternUpdate<T0>(in UpdateContext context, int numParticles, SparseSpan<T0> pin0);
public delegate void PatternUpdate<T0, T1>(in UpdateContext context, int numParticles,
    SparseSpan<T0> pin0, SparseSpan<T1> pin1);
public delegate void PatternUpdate<T0, T1, T2>(in UpdateContext context, int numParticles,
    SparseSpan<T0> pin0, SparseSpan<T1> pin1, SparseSpan<T2> pin2);
public delegate void PatternUpdate<T0, T1, T2, T3>(in UpdateContext context, int numParticles,
    SparseSpan<T0> pin0, SparseSpan<T1> pin1, SparseSpan<T2> pin2, SparseSpan<T3> pin3);
public delegate void PatternUpdate<T0, T1, T2, T3, T4>(in UpdateContext context, int numParticles,
    SparseSpan<T0> pin0, SparseSpan<T1> pin1, SparseSpan<T2> pin2, SparseSpan<T3> pin3, SparseSpan<T4> pin4);
public delegate void PatternUpdate<T0, T1, T2, T3, T4, T5>(in UpdateContext context, int numParticles,
    SparseSpan<T0> pin0, SparseSpan<T1> pin1, SparseSpan<T2> pin2, SparseSpan<T3> pin3, SparseSpan<T4> pin4,
    SparseSpan<T5> pin5);
public delegate void PatternUpdate<T0, T1, T2, T3, T4, T5, T6>(in UpdateContext context, int numParticles,
    SparseSpan<T0> pin0, SparseSpan<T1> pin1, SparseSpan<T2> pin2, SparseSpan<T3> pin3, SparseSpan<T4> pin4,
    SparseSpan<T5> pin5, SparseSpan<T6> pin6);

public static class Helpers
{
    public static NodePattern MakePattern<T0>(PatternUpdate<T0> update, ParticleGraphTypedPin<T0> pin0)
    {
        var pattern = new NodePattern((ref UpdateContext context, System.Span<ParticleGraphPinRef> pinRefs) =>
        {
            var span0 = context.GetSpan<T0>(pinRefs[0]);
            update(in context, context.Indices.Length, span0);
        });
        return pattern.WithPin(new ParticleGraphPin(pin0.Flags, pin0.Name, VariantTypeUtility.GetVariantType<T0>()));
    }

    public static NodePattern MakePattern<T0, T1>(PatternUpdate<T0, T1> update,
        ParticleGraphTypedPin<T0> pin0, ParticleGraphTypedPin<T1> pin1)
    {
        var pattern = new NodePattern((ref UpdateContext context, System.Span<ParticleGraphPinRef> pinRefs) =>
        {
            var span0 = context.GetSpan<T0>(pinRefs[0]);
            var span1 = context.GetSpan<T1>(pinRefs[1]);
            update(in context, context.Indices.Length, span0, span1);
        });
        return pattern
            .WithPin(new ParticleGraphPin(pin0.Flags, pin0.Name, VariantTypeUtility.GetVariantType<T0>()))
            .WithPin(new ParticleGraphPin(pin1.Flags, pin1.Name, VariantTypeUtility.GetVariantType<T1>()));
    }

    public static NodePattern MakePattern<T0, T1, T2>(PatternUpdate<T0, T1, T2> update,
        ParticleGraphTypedPin<T0> pin0, ParticleGraphTypedPin<T1> pin1, ParticleGraphTypedPin<T2> pin2)
    {
        var pattern = new NodePattern((ref UpdateContext context, System.Span<ParticleGraphPinRef> pinRefs) =>
        {
            var span0 = context.GetSpan<T0>(pinRefs[0]);
            var span1 = context.GetSpan<T1>(pinRefs[1]);
            var span2 = context.GetSpan<T2>(pinRefs[2]);
            update(in context, context.Indices.Length, span0, span1, span2);
        });
        return pattern
            .WithPin(new ParticleGraphPin(pin0.Flags, pin0.Name, VariantTypeUtility.GetVariantType<T0>()))
            .WithPin(new ParticleGraphPin(pin1.Flags, pin1.Name, VariantTypeUtility.GetVariantType<T1>()))
            .WithPin(new ParticleGraphPin(pin2.Flags, pin2.Name, VariantTypeUtility.GetVariantType<T2>()));
    }

    public static NodePattern MakePattern<T0, T1, T2, T3>(PatternUpdate<T0, T1, T2, T3> update,
        ParticleGraphTypedPin<T0> pin0, ParticleGraphTypedPin<T1> pin1, ParticleGraphTypedPin<T2> pin2,
        ParticleGraphTypedPin<T3> pin3)
    {
        var pattern = new NodePattern((ref UpdateContext context, System.Span<ParticleGraphPinRef> pinRefs) =>
        {
            var span0 = context.GetSpan<T0>(pinRefs[0]);
            var span1 = context.GetSpan<T1>(pinRefs[1]);
            var span2 = context.GetSpan<T2>(pinRefs[2]);
            var span3 = context.GetSpan<T3>(pinRefs[3]);
            update(in context, context.Indices.Length, span0, span1, span2, span3);
        });
        return pattern
            .WithPin(new ParticleGraphPin(pin0.Flags, pin0.Name, VariantTypeUtility.GetVariantType<T0>()))
            .WithPin(new ParticleGraphPin(pin1.Flags, pin1.Name, VariantTypeUtility.GetVariantType<T1>()))
            .WithPin(new ParticleGraphPin(pin2.Flags, pin2.Name, VariantTypeUtility.GetVariantType<T2>()))
            .WithPin(new ParticleGraphPin(pin3.Flags, pin3.Name, VariantTypeUtility.GetVariantType<T3>()));
    }

    public static NodePattern MakePattern<T0, T1, T2, T3, T4>(PatternUpdate<T0, T1, T2, T3, T4> update,
        ParticleGraphTypedPin<T0> pin0, ParticleGraphTypedPin<T1> pin1, ParticleGraphTypedPin<T2> pin2,
        ParticleGraphTypedPin<T3> pin3, ParticleGraphTypedPin<T4> pin4)
    {
        var pattern = new NodePattern((ref UpdateContext context, System.Span<ParticleGraphPinRef> pinRefs) =>
        {
            var span0 = context.GetSpan<T0>(pinRefs[0]);
            var span1 = context.GetSpan<T1>(pinRefs[1]);
            var span2 = context.GetSpan<T2>(pinRefs[2]);
            var span3 = context.GetSpan<T3>(pinRefs[3]);
            var span4 = context.GetSpan<T4>(pinRefs[4]);
            update(in context, context.Indices.Length, span0, span1, span2, span3, span4);
        });
        return pattern
            .WithPin(new ParticleGraphPin(pin0.Flags, pin0.Name, VariantTypeUtility.GetVariantType<T0>()))
            .WithPin(new ParticleGraphPin(pin1.Flags, pin1.Name, VariantTypeUtility.GetVariantType<T1>()))
            .WithPin(new ParticleGraphPin(pin2.Flags, pin2.Name, VariantTypeUtility.GetVariantType<T2>()))
            .WithPin(new ParticleGraphPin(pin3.Flags, pin3.Name, VariantTypeUtility.GetVariantType<T3>()))
            .WithPin(new ParticleGraphPin(pin4.Flags, pin4.Name, VariantTypeUtility.GetVariantType<T4>()));
    }

    public static NodePattern MakePattern<T0, T1, T2, T3, T4, T5>(
        PatternUpdate<T0, T1, T2, T3, T4, T5> update,
        ParticleGraphTypedPin<T0> pin0, ParticleGraphTypedPin<T1> pin1, ParticleGraphTypedPin<T2> pin2,
        ParticleGraphTypedPin<T3> pin3, ParticleGraphTypedPin<T4> pin4, ParticleGraphTypedPin<T5> pin5)
    {
        var pattern = new NodePattern((ref UpdateContext context, System.Span<ParticleGraphPinRef> pinRefs) =>
        {
            var span0 = context.GetSpan<T0>(pinRefs[0]);
            var span1 = context.GetSpan<T1>(pinRefs[1]);
            var span2 = context.GetSpan<T2>(pinRefs[2]);
            var span3 = context.GetSpan<T3>(pinRefs[3]);
            var span4 = context.GetSpan<T4>(pinRefs[4]);
            var span5 = context.GetSpan<T5>(pinRefs[5]);
            update(in context, context.Indices.Length, span0, span1, span2, span3, span4, span5);
        });
        return pattern
            .WithPin(new ParticleGraphPin(pin0.Flags, pin0.Name, VariantTypeUtility.GetVariantType<T0>()))
            .WithPin(new ParticleGraphPin(pin1.Flags, pin1.Name, VariantTypeUtility.GetVariantType<T1>()))
            .WithPin(new ParticleGraphPin(pin2.Flags, pin2.Name, VariantTypeUtility.GetVariantType<T2>()))
            .WithPin(new ParticleGraphPin(pin3.Flags, pin3.Name, VariantTypeUtility.GetVariantType<T3>()))
            .WithPin(new ParticleGraphPin(pin4.Flags, pin4.Name, VariantTypeUtility.GetVariantType<T4>()))
            .WithPin(new ParticleGraphPin(pin5.Flags, pin5.Name, VariantTypeUtility.GetVariantType<T5>()));
    }

    public static NodePattern MakePattern<T0, T1, T2, T3, T4, T5, T6>(
        PatternUpdate<T0, T1, T2, T3, T4, T5, T6> update,
        ParticleGraphTypedPin<T0> pin0, ParticleGraphTypedPin<T1> pin1, ParticleGraphTypedPin<T2> pin2,
        ParticleGraphTypedPin<T3> pin3, ParticleGraphTypedPin<T4> pin4, ParticleGraphTypedPin<T5> pin5,
        ParticleGraphTypedPin<T6> pin6)
    {
        var pattern = new NodePattern((ref UpdateContext context, System.Span<ParticleGraphPinRef> pinRefs) =>
        {
            var span0 = context.GetSpan<T0>(pinRefs[0]);
            var span1 = context.GetSpan<T1>(pinRefs[1]);
            var span2 = context.GetSpan<T2>(pinRefs[2]);
            var span3 = context.GetSpan<T3>(pinRefs[3]);
            var span4 = context.GetSpan<T4>(pinRefs[4]);
            var span5 = context.GetSpan<T5>(pinRefs[5]);
            var span6 = context.GetSpan<T6>(pinRefs[6]);
            update(in context, context.Indices.Length, span0, span1, span2, span3, span4, span5, span6);
        });
        return pattern
            .WithPin(new ParticleGraphPin(pin0.Flags, pin0.Name, VariantTypeUtility.GetVariantType<T0>()))
            .WithPin(new ParticleGraphPin(pin1.Flags, pin1.Name, VariantTypeUtility.GetVariantType<T1>()))
            .WithPin(new ParticleGraphPin(pin2.Flags, pin2.Name, VariantTypeUtility.GetVariantType<T2>()))
            .WithPin(new ParticleGraphPin(pin3.Flags, pin3.Name, VariantTypeUtility.GetVariantType<T3>()))
            .WithPin(new ParticleGraphPin(pin4.Flags, pin4.Name, VariantTypeUtility.GetVariantType<T4>()))
            .WithPin(new ParticleGraphPin(pin5.Flags, pin5.Name, VariantTypeUtility.GetVariantType<T5>()))
            .WithPin(new ParticleGraphPin(pin6.Flags, pin6.Name, VariantTypeUtility.GetVariantType<T6>()));
    }
}
