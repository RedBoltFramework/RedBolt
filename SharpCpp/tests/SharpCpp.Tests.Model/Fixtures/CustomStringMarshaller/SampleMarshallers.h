#pragma once

#include "Sample.hpp"

namespace SharpCpp
{
template <class T>
struct Marshaller;

template <class TString>
struct OwnedStringMarshallerBase
{
    using cpp_type = TString;
    using abi_type = TString*;

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static unsigned long long GetCountCore(const cpp_type& self);

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static const char* GetDataCore(const cpp_type& self);

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static void AddCore(cpp_type& self, unsigned char value);

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static void ClearCore(cpp_type& self);
};

template <class TString>
struct OwnedStringConstReferenceMarshallerBase
{
    using cpp_type = const TString&;
    using abi_type = TString*;
};

template <class TString>
struct OwnedStringConstPointerMarshallerBase
{
    using cpp_type = const TString*;
    using abi_type = TString*;
};

template <class TString>
struct OwnedStringPointerMarshallerBase
{
    using cpp_type = TString*;
    using abi_type = TString*;
};

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
}
