#pragma once

#include <SharpCpp/Marshaller.hpp>

#include <Urho3D/Container/Ptr.h>
#include <Urho3D/Core/Object.h>
#include <Urho3D/Container/RefCounted.h>

namespace SharpCpp::rbfx::detail
{
template <class TCpp, class TRefCounted>
inline TRefCounted* ResolveRefCountedPointer(TCpp* value) noexcept
{
    if (value == nullptr)
    {
        return nullptr;
    }

    if constexpr (std::is_convertible_v<TCpp*, TRefCounted*>)
    {
        return static_cast<TRefCounted*>(value);
    }

    return dynamic_cast<TRefCounted*>(value);
}

template <class TCpp, class TRefCounted>
inline Urho3D::SharedPtr<TCpp, TRefCounted> MakeSharedPtrFromAbi(TCpp* value) noexcept
{
    return value == nullptr
        ? Urho3D::SharedPtr<TCpp, TRefCounted>{}
        : Urho3D::SharedPtr<TCpp, TRefCounted>(value, ResolveRefCountedPointer<TCpp, TRefCounted>(value));
}

template <class TCpp, class TRefCounted>
inline Urho3D::WeakPtr<TCpp, TRefCounted> MakeWeakPtrFromAbi(TCpp* value) noexcept
{
    return value == nullptr
        ? Urho3D::WeakPtr<TCpp, TRefCounted>{}
        : Urho3D::WeakPtr<TCpp, TRefCounted>(value, ResolveRefCountedPointer<TCpp, TRefCounted>(value));
}
}

namespace SharpCpp
{

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

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <>
struct [[sharpcpp::managed_type(
    managed_static_interfaces = "global::SharpCpp.Rbfx.Bindings.Interop.IStaticTypeInfo<{managed_type}>",
    managed_static_interfaces_abstract = true
)]] Marshaller<Urho3D::Object*>
{
    using cpp_type = Urho3D::Object*;
    using abi_type = Urho3D::Object*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
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

    abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value.Detach();
    }

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return rbfx::detail::MakeSharedPtrFromAbi<TCpp, TRefCounted>(value);
    }
};

template <class TCpp, class TRefCounted>
struct ReturnMarshaller<Urho3D::SharedPtr<TCpp, TRefCounted>>
    : Marshaller<Urho3D::SharedPtr<TCpp, TRefCounted>>
{
};

static_assert(std::is_same_v<ret_t<Urho3D::SharedPtr<Urho3D::RefCounted>>, Urho3D::RefCounted*>);

template <class TCpp, class TRefCounted>
struct Marshaller<const Urho3D::SharedPtr<TCpp, TRefCounted>&>
{
    using cpp_type = const Urho3D::SharedPtr<TCpp, TRefCounted>&;
    using abi_type = TCpp*;

    abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value.Get();
    }

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        storage_ = rbfx::detail::MakeSharedPtrFromAbi<TCpp, TRefCounted>(value);
        return storage_;
    }

private:
    mutable Urho3D::SharedPtr<TCpp, TRefCounted> storage_{};
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

    abi_type ConvertToAbi(const cpp_type& value) noexcept
    {
        return value.Lock().Detach();
    }

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return rbfx::detail::MakeWeakPtrFromAbi<TCpp, TRefCounted>(value);
    }
};

template <class TCpp, class TRefCounted>
struct ReturnMarshaller<Urho3D::WeakPtr<TCpp, TRefCounted>>
    : Marshaller<Urho3D::WeakPtr<TCpp, TRefCounted>>
{
};

static_assert(std::is_same_v<ret_t<Urho3D::WeakPtr<Urho3D::RefCounted>>, Urho3D::RefCounted*>);

template <class TCpp, class TRefCounted>
struct Marshaller<const Urho3D::WeakPtr<TCpp, TRefCounted>&>
{
    using cpp_type = const Urho3D::WeakPtr<TCpp, TRefCounted>&;
    using abi_type = TCpp*;

    abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value.Get();
    }

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        storage_ = rbfx::detail::MakeWeakPtrFromAbi<TCpp, TRefCounted>(value);
        return storage_;
    }

private:
    mutable Urho3D::WeakPtr<TCpp, TRefCounted> storage_{};
};

}
