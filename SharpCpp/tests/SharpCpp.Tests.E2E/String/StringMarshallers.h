#pragma once

#include <SharpCpp/Marshaller.hpp>

#include "String.hpp"

namespace SharpCpp
{
template <>
struct [[sharpcpp::managed_type(
    managed_base_type = "global::SharpCpp.Support.StringBase",
    public_type = "Ea.String",
    managed_namespace = "Ea"
)]]
Marshaller<ea_compat_string> : OwnedStringMarshallerBase<ea_compat_string> {};

template <>
struct Marshaller<const ea_compat_string&> : OwnedStringConstReferenceMarshallerBase<ea_compat_string> {};

template <>
struct Marshaller<const ea_compat_string*> : OwnedStringConstPointerMarshallerBase<ea_compat_string> {};

template <>
struct Marshaller<ea_compat_string*> : OwnedStringPointerMarshallerBase<ea_compat_string> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::System.ReadOnlySpan<byte>",
    managed_marshaller = "global::SharpCpp.Support.Utf8SpanAbiMarshaller",
    undefaultable = true
)]]
Marshaller<ea::string_view> : StringViewMarshallerBase<ea::string_view> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::System.ReadOnlySpan<byte>",
    managed_marshaller = "global::SharpCpp.Support.Utf8SpanAbiReturnMarshaller"
)]] ReturnMarshaller<ea::string_view> : Marshaller<ea::string_view> {};
}
