#pragma once

template <class T>
struct Marshaller;

struct widget {};

template <class T>
struct helper_base
{
    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static unsigned long long GetCountCore(const T& self);

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static void AddCore(T& self, unsigned char value);
};

template <>
struct [[sharpcpp::managed_type(public_type = "Ea.Widget", managed_base_type = "global::SharpCpp.Support.StringBase")]]
Marshaller<widget> : helper_base<widget>
{
    [[sharpcpp::expose_member(shape = "method", exposed = false)]]
    static void AddCore(widget& self, unsigned char value);
};
