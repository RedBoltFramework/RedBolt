#pragma once

#include <SharpCpp/Marshaller.hpp>
#include <imgui.h>

namespace SharpCpp
{

template <>
struct [[sharpcpp::managed_type(
    public_type = "Vector2f",
    managed_marshaller = "global::RedBolt.Math.Vector2fMarshaller",
    suppress_emission = true
)]]
Marshaller<ImVec2>
{
    using cpp_type = ImVec2;
    using abi_type = ImVec2;
    abi_type ConvertToAbi(const cpp_type& v) noexcept { return v; }
    cpp_type ConvertToCpp(abi_type v) noexcept { return v; }
};

template <>
struct Marshaller<const ImVec2&>
{
    using cpp_type = const ImVec2&;
    using abi_type = const ImVec2*;
    abi_type ConvertToAbi(cpp_type v) noexcept { return &v; }
    cpp_type ConvertToCpp(abi_type v) noexcept { return *v; }
};

template <>
struct ReturnMarshaller<ImVec2>
{
    using cpp_type = ImVec2;
    using abi_type = ImVec2;
    abi_type ConvertToAbi(const cpp_type& v) noexcept { return v; }
    cpp_type ConvertToCpp(const abi_type& v) noexcept { return v; }
};

template <>
struct [[sharpcpp::managed_type(
    public_type = "Vector4f",
    managed_marshaller = "global::RedBolt.Math.Vector4fMarshaller",
    suppress_emission = true
)]]
Marshaller<ImVec4>
{
    using cpp_type = ImVec4;
    using abi_type = ImVec4;
    abi_type ConvertToAbi(const cpp_type& v) noexcept { return v; }
    cpp_type ConvertToCpp(abi_type v) noexcept { return v; }
};

template <>
struct Marshaller<const ImVec4&>
{
    using cpp_type = const ImVec4&;
    using abi_type = const ImVec4*;
    abi_type ConvertToAbi(cpp_type v) noexcept { return &v; }
    cpp_type ConvertToCpp(abi_type v) noexcept { return *v; }
};

template <>
struct ReturnMarshaller<ImVec4>
{
    using cpp_type = ImVec4;
    using abi_type = ImVec4;
    abi_type ConvertToAbi(const cpp_type& v) noexcept { return v; }
    cpp_type ConvertToCpp(const abi_type& v) noexcept { return v; }
};

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::RedBolt.Math.Color",
    marshaller_type = "global::RedBolt.Math.Color",
    suppress_emission = true
)]]
Marshaller<ImColor>
{
    using cpp_type = ImColor;
    using abi_type = ImColor;
    abi_type ConvertToAbi(const cpp_type& v) noexcept { return v; }
    cpp_type ConvertToCpp(abi_type v) noexcept { return v; }
};

} // namespace SharpCpp
