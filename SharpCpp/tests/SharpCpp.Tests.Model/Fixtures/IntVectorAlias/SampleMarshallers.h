#pragma once
#include "Sample.hpp"

namespace SharpCpp
{
template <class T>
struct Marshaller;

template <typename T>
struct [[sharpcpp::managed_type(
    public_type = "Test.Vector<{0}>",
    managed_base_type = "global::SharpCpp.Support.VectorBase<{0}>",
    managed_namespace = "Test"
)]] Marshaller<my::vector<T>> {};

template <typename T>
struct Marshaller<const my::vector<T>&> {};
}
