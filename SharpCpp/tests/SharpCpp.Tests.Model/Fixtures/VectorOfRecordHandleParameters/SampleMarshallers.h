#pragma once

#include "Sample.hpp"

namespace SharpCpp
{
template <class T>
struct Marshaller;

template <typename T, typename TAllocator>
struct [[sharpcpp::managed_type(
    public_type = "Ea.Vector<{0}>",
    managed_base_type = "global::SharpCpp.Support.VectorBase<{0}>",
    managed_namespace = "Ea"
)]] Marshaller<eastl::vector<T, TAllocator>>
{
    using cpp_type = eastl::vector<T, TAllocator>;
    using abi_type = eastl::vector<T, TAllocator>*;
};
}
