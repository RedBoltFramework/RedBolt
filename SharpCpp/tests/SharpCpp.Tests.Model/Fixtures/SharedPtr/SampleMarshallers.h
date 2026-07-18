#pragma once

#include "Sample.hpp"

namespace SharpCpp
{
template <class T>
struct Marshaller;

template <>
struct [[sharpcpp::managed_type(
    public_type = "RefCounted",
    managed_base_type = "global::Urho3D.Interop.RefCountedNativeObject",
    managed_instance_cache_factory = "global::Urho3D.Interop.RefCountedInstanceCache.Shared",
    managed_instance_cache_type = "global::SharpCpp.Support.INativeInstanceCache",
    release_strategy = "base"
)]] Marshaller<Urho3D::RefCounted*>
{
    using cpp_type = Urho3D::RefCounted*;
    using abi_type = Urho3D::RefCounted*;
};

template <class TCpp, class TRefCounted>
struct [[sharpcpp::managed_type(
    public_type = "{0}",
    projection = true,
    return_ownership = "owning"
)]] Marshaller<Urho3D::SharedPtr<TCpp, TRefCounted>>
{
    using cpp_type = Urho3D::SharedPtr<TCpp, TRefCounted>;
    using abi_type = TCpp*;
};

template <class TCpp, class TRefCounted>
struct Marshaller<const Urho3D::SharedPtr<TCpp, TRefCounted>&>
{
    using cpp_type = const Urho3D::SharedPtr<TCpp, TRefCounted>&;
    using abi_type = TCpp*;
};

template <class TCpp, class TRefCounted>
struct [[sharpcpp::managed_type(
    public_type = "{0}",
    projection = true,
    return_ownership = "owning"
)]] Marshaller<Urho3D::WeakPtr<TCpp, TRefCounted>>
{
    using cpp_type = Urho3D::WeakPtr<TCpp, TRefCounted>;
    using abi_type = TCpp*;
};
}
