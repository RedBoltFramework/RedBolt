#include "ShaderCompilerSupport.hpp"

#include <cassert>
#include <cstring>
#include <stdexcept>
#include <string>
#include <vector>

#if D3D11_SUPPORTED || D3D12_SUPPORTED
    #include <d3dcompiler.h>
#endif

namespace RedBoltNative
{

namespace
{

const char* GetShaderProfile(std::uint32_t type)
{
    switch (type)
    {
    case 0: return "vs_5_0";
    case 1: return "ps_5_0";
    case 2: return "gs_5_0";
    case 3: return "hs_5_0";
    case 4: return "ds_5_0";
    case 5: return "cs_5_0";
    default: assert(false); return "";
    }
}

} // namespace

class ShaderCompilerSupport::Impl
{
public:
    std::vector<std::uint8_t> bytecode_;
    std::string output_;
};

ShaderCompilerSupport::ShaderCompilerSupport()
    : impl_(std::make_unique<Impl>())
{
}

ShaderCompilerSupport::~ShaderCompilerSupport() = default;

bool ShaderCompilerSupport::Compile(
    const void* sourceCode, std::size_t sourceCodeSize, std::uint32_t shaderType, bool debug)
{
    impl_->bytecode_.clear();
    impl_->output_.clear();

#if D3D11_SUPPORTED || D3D12_SUPPORTED
    const char* profile = GetShaderProfile(shaderType);

    unsigned flags = D3DCOMPILE_OPTIMIZATION_LEVEL3;
    if (shaderType == 1)
        flags |= D3DCOMPILE_PREFER_FLOW_CONTROL;
    if (debug)
        flags |= D3DCOMPILE_DEBUG;

    ID3DBlob* bytecodeBlob = nullptr;
    ID3DBlob* errorMessagesBlob = nullptr;

    const HRESULT result = D3DCompile(sourceCode, sourceCodeSize, nullptr, nullptr, nullptr, "main",
        profile, flags, 0, &bytecodeBlob, &errorMessagesBlob);

    if (errorMessagesBlob)
    {
        const char* errorMessageBegin = reinterpret_cast<const char*>(errorMessagesBlob->GetBufferPointer());
        impl_->output_.assign(errorMessageBegin, errorMessageBegin + errorMessagesBlob->GetBufferSize());
        errorMessagesBlob->Release();
    }

    if (bytecodeBlob)
    {
        const auto bytecodeSize = static_cast<unsigned>(bytecodeBlob->GetBufferSize());
        impl_->bytecode_.resize(bytecodeSize);
        std::memcpy(impl_->bytecode_.data(), bytecodeBlob->GetBufferPointer(), bytecodeSize);
        bytecodeBlob->Release();
    }

    return SUCCEEDED(result);
#else
    return false;
#endif
}

std::size_t ShaderCompilerSupport::GetBytecodeSize() const
{
    return impl_->bytecode_.size();
}

std::uint8_t ShaderCompilerSupport::GetBytecodeByte(std::size_t index) const
{
    if (index >= impl_->bytecode_.size())
        throw std::out_of_range("shader compiler bytecode index is out of range");
    return impl_->bytecode_[index];
}

const char* ShaderCompilerSupport::GetOutput() const
{
    return impl_->output_.c_str();
}

} // namespace RedBoltNative
