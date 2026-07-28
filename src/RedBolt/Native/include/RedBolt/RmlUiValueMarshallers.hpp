#pragma once

#include <SharpCpp/Marshaller.hpp>
#include <RmlUi/Core/Types.h>

namespace SharpCpp
{

template <>
struct Marshaller<Rml::Vector2<int>>
{
    using cpp_type = Rml::Vector2<int>;
    using abi_type = Rml::Vector2<int>;
    abi_type ConvertToAbi(const cpp_type& value) noexcept { return value; }
    cpp_type ConvertToCpp(abi_type value) noexcept { return value; }
};

template <>
struct ReturnMarshaller<Rml::Vector2<int>>
{
    using cpp_type = Rml::Vector2<int>;
    using abi_type = Rml::Vector2<int>;
    abi_type ConvertToAbi(const cpp_type& value) noexcept { return value; }
    cpp_type ConvertToCpp(const abi_type& value) noexcept { return value; }
};

} // namespace SharpCpp
