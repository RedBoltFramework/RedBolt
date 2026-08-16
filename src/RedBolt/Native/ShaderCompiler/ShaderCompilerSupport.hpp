// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

#pragma once

#include <cstddef>
#include <cstdint>
#include <memory>

namespace RedBoltNative
{

class ShaderCompilerSupport
{
public:
    ShaderCompilerSupport();
    ~ShaderCompilerSupport();

    bool Compile(const void* sourceCode, std::size_t sourceCodeSize, std::uint32_t shaderType, bool debug);
    std::size_t GetBytecodeSize() const;
    std::uint8_t GetBytecodeByte(std::size_t index) const;
    const char* GetOutput() const;

private:
    class Impl;
    std::unique_ptr<Impl> impl_;
};

} // namespace RedBoltNative
