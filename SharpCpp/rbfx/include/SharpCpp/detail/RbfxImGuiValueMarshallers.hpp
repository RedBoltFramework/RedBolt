#pragma once

#include <SharpCpp/Marshaller.hpp>
#include "RbfxMathMarshallers.hpp"

#include <ImGui/imgui.h>

namespace SharpCpp
{

inline ImVec2 ToCppImVec2(const Vector2Abi& value) { return {value.X, value.Y}; }
inline Vector2Abi ToAbiImVec2(const ImVec2& value) { return {value.x, value.y}; }
inline ImVec4 ToCppImVec4(const Vector4Abi& value) { return {value.X, value.Y, value.Z, value.W}; }
inline Vector4Abi ToAbiImVec4(const ImVec4& value) { return {value.x, value.y, value.z, value.w}; }

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::Urho3D.Math.Vector2",
    marshaller_type = "global::Urho3D.Math.Vector2",
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
    using abi_type = Vector2Abi;
    abi_type ConvertToAbi(const cpp_type& v) noexcept { return ToAbiImVec2(v); }
    cpp_type ConvertToCpp(const abi_type& v) noexcept { return ToCppImVec2(v); }
};

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::Urho3D.Math.Vector4",
    marshaller_type = "global::Urho3D.Math.Vector4",
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
    using abi_type = Vector4Abi;
    abi_type ConvertToAbi(const cpp_type& v) noexcept { return ToAbiImVec4(v); }
    cpp_type ConvertToCpp(const abi_type& v) noexcept { return ToCppImVec4(v); }
};

} // namespace SharpCpp
