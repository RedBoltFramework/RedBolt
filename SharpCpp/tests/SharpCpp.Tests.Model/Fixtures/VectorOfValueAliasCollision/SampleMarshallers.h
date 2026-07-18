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

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::Urho3D.Math.Vector3",
    marshaller_type = "global::Urho3D.Math.Vector3"
)]] Marshaller<Urho3D::Vector3>
{
    using cpp_type = Urho3D::Vector3;
    using abi_type = Urho3D::Vector3;
};

template <>
struct Marshaller<const Urho3D::Vector3&>
{
    using cpp_type = const Urho3D::Vector3&;
    using abi_type = const Urho3D::Vector3*;
};
}
