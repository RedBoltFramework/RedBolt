#pragma once

#include <SharpCpp/Marshaller.hpp>
#include <RmlUi/Core/Types.h>

namespace SharpCpp
{

template <class T>
struct [[sharpcpp::managed_type(
    public_type = "{0}",
    projection = true,
    return_ownership = "owning"
)]] Marshaller<std::unique_ptr<T, Rml::Releaser<T>>>
{
    using cpp_type = std::unique_ptr<T, Rml::Releaser<T>>;
    using abi_type = T*;

    abi_type ConvertToAbi(cpp_type& value) noexcept { return value.release(); }
    cpp_type ConvertToCpp(abi_type value) noexcept { return cpp_type(value); }
};

template <class T>
struct ReturnMarshaller<std::unique_ptr<T, Rml::Releaser<T>>>
    : Marshaller<std::unique_ptr<T, Rml::Releaser<T>>>
{
};

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
