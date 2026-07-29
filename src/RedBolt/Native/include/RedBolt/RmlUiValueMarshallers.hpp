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

template <class T>
struct RmlValueMarshaller
{
    using cpp_type = T;
    using abi_type = T;
    abi_type ConvertToAbi(const cpp_type& value) noexcept { return value; }
    cpp_type ConvertToCpp(const abi_type& value) noexcept { return value; }
};

template <class T>
struct RmlConstReferenceValueMarshaller
{
    using cpp_type = const T&;
    using abi_type = T;
    abi_type ConvertToAbi(cpp_type value) noexcept { return value; }
    cpp_type ConvertToCpp(const abi_type& value) noexcept
    {
        value_ = value;
        return value_;
    }

private:
    T value_{};
};

template <>
struct [[sharpcpp::managed_type(
    public_type = "Vector2i",
    managed_marshaller = "global::RedBolt.Math.Vector2iMarshaller",
    suppress_emission = true
)]] Marshaller<Rml::Vector2<int>> : RmlValueMarshaller<Rml::Vector2<int>> {};
template <> struct ReturnMarshaller<Rml::Vector2<int>> : RmlValueMarshaller<Rml::Vector2<int>> {};
template <>
struct [[sharpcpp::managed_type(
    public_type = "Vector2f",
    managed_marshaller = "global::RedBolt.Math.Vector2fMarshaller",
    suppress_emission = true
)]] Marshaller<Rml::Vector2f> : RmlValueMarshaller<Rml::Vector2f> {};
template <> struct Marshaller<const Rml::Vector2f&> : RmlConstReferenceValueMarshaller<Rml::Vector2f> {};
template <> struct ReturnMarshaller<Rml::Vector2f> : RmlValueMarshaller<Rml::Vector2f> {};
template <>
struct [[sharpcpp::managed_type(
    public_type = "Vector3f",
    managed_marshaller = "global::RedBolt.Math.Vector3fMarshaller",
    suppress_emission = true
)]] Marshaller<Rml::Vector3f> : RmlValueMarshaller<Rml::Vector3f> {};
template <> struct Marshaller<const Rml::Vector3f&> : RmlConstReferenceValueMarshaller<Rml::Vector3f> {};
template <> struct ReturnMarshaller<Rml::Vector3f> : RmlValueMarshaller<Rml::Vector3f> {};
template <>
struct [[sharpcpp::managed_type(
    public_type = "Vector4f",
    managed_marshaller = "global::RedBolt.Math.Vector4fMarshaller",
    suppress_emission = true
)]] Marshaller<Rml::Vector4f> : RmlValueMarshaller<Rml::Vector4f> {};
template <> struct Marshaller<const Rml::Vector4f&> : RmlConstReferenceValueMarshaller<Rml::Vector4f> {};
template <> struct ReturnMarshaller<Rml::Vector4f> : RmlValueMarshaller<Rml::Vector4f> {};
template <>
struct [[sharpcpp::managed_type(
    public_type = "global::RedBolt.Math.Color",
    managed_marshaller = "global::RedBolt.Math.ColorMarshaller",
    suppress_emission = true
)]] Marshaller<Rml::Colourf> : RmlValueMarshaller<Rml::Colourf> {};
template <> struct Marshaller<const Rml::Colourf&> : RmlConstReferenceValueMarshaller<Rml::Colourf> {};
template <> struct ReturnMarshaller<Rml::Colourf> : RmlValueMarshaller<Rml::Colourf> {};
template <> struct Marshaller<Rml::Colourb> : RmlValueMarshaller<Rml::Colourb> {};
template <> struct Marshaller<const Rml::Colourb&> : RmlConstReferenceValueMarshaller<Rml::Colourb> {};
template <> struct ReturnMarshaller<Rml::Colourb> : RmlValueMarshaller<Rml::Colourb> {};

} // namespace SharpCpp
