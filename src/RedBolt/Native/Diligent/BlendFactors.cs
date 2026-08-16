// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Bindings.Diligent;

using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public readonly struct BlendFactors(float red, float green, float blue, float alpha)
{
    public static BlendFactors One => new(1.0f, 1.0f, 1.0f, 1.0f);

    public readonly float Red = red;
    public readonly float Green = green;
    public readonly float Blue = blue;
    public readonly float Alpha = alpha;
}
