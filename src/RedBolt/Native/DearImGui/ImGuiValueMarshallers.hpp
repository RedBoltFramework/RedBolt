// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

#pragma once

#include <SharpCpp/Marshaller.hpp>
#include <imgui_internal.h>

namespace SharpCpp
{

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::System.ReadOnlySpan<byte>",
    managed_marshaller = "global::SharpCpp.Support.Utf8SpanAbiMarshaller",
    suppress_emission = true,
    undefaultable = true
)]]
Marshaller<ImStrv>
{
    using cpp_type = ImStrv;
    struct abi_type
    {
        const char* data{};
        std::size_t size{};
    };

    abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return { value.Begin, value.Begin == nullptr ? 0 : static_cast<std::size_t>(value.End - value.Begin) };
    }

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value.data == nullptr ? cpp_type("") : cpp_type(value.data, value.data + value.size);
    }
};

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
    public_type = "global::RedBolt.Math.Rect",
    managed_marshaller = "global::RedBolt.Math.RectMarshaller",
    suppress_emission = true
)]]
Marshaller<ImRect>
{
    using cpp_type = ImRect;
    using abi_type = ImRect;
    abi_type ConvertToAbi(const cpp_type& v) noexcept { return v; }
    cpp_type ConvertToCpp(abi_type v) noexcept { return v; }
};

template <>
struct Marshaller<const ImRect&>
{
    using cpp_type = const ImRect&;
    using abi_type = const ImRect*;
    abi_type ConvertToAbi(cpp_type v) noexcept { return &v; }
    cpp_type ConvertToCpp(abi_type v) noexcept { return *v; }
};

template <>
struct ReturnMarshaller<ImRect>
{
    using cpp_type = ImRect;
    using abi_type = ImRect;
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
