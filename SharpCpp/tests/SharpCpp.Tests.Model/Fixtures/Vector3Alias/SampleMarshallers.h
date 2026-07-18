#pragma once

#include "Sample.hpp"

namespace SharpCpp
{
template <class T>
struct Marshaller;

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
