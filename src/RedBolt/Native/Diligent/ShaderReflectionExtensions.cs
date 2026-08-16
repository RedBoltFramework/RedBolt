// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Bindings.Diligent;

public partial class ShaderCodeBufferDesc
{
    public ShaderCodeVariableDesc GetVariableAt(Uint32 index)
    {
        return GetPVariablesAt(index);
    }
}
