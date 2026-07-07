#include <RedBolt/DiligentSupport.hpp>

#include <stdexcept>

namespace Diligent
{

const ShaderCodeVariableDesc* GetShaderCodeBufferVariableAt(const ShaderCodeBufferDesc& self, Uint32 index)
{
    if (self.pVariables == nullptr || index >= self.NumVariables)
        throw std::out_of_range("Diligent shader reflection variable access out of range.");

    return &self.pVariables[index];
}

} // namespace Diligent
