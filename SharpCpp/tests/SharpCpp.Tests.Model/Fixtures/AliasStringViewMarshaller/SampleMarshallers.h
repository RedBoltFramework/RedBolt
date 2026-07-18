#pragma once

#include "Sample.hpp"
#include <SharpCpp/Marshaller.hpp>

namespace SharpCpp
{
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
