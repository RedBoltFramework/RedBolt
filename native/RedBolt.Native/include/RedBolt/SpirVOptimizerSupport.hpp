#pragma once

#include <cstddef>
#include <cstdint>
#include <memory>

#include <spirv-tools/libspirv.h>

namespace RedBoltNative
{

class SpirVOptimizerSupport
{
public:
    SpirVOptimizerSupport();
    ~SpirVOptimizerSupport();

    bool Optimize(const std::uint32_t* bytecode, std::size_t size, std::uint32_t target, bool isVulkan);
    std::size_t GetBytecodeSize() const;
    std::uint32_t GetBytecodeWord(std::size_t index) const;
    const char* GetOutput() const;

private:
    class Impl;
    std::unique_ptr<Impl> impl_;
};

} // namespace RedBoltNative
