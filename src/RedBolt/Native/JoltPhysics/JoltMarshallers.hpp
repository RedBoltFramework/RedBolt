#pragma once

#include <cstdint>
#include <SharpCpp/Marshaller.hpp>

#if defined(REDBOLT_JOLT_BINDING_PARSE)
#define _M_ARM 1
#endif

#include <Jolt/Jolt.h>
#include <Jolt/Core/Array.h>
#include <Jolt/Core/Mutex.h>
#include <Jolt/Core/Reference.h>
#include <Jolt/Core/StaticArray.h>
#include <Jolt/Math/Quat.h>
#include <Jolt/Physics/Body/BodyID.h>
#include <Jolt/Physics/Collision/BroadPhase/BroadPhaseLayer.h>
#include <Jolt/Physics/Collision/CastResult.h>
#include <Jolt/Physics/Collision/RayCast.h>
#include <Jolt/Physics/Collision/Shape/SubShapeID.h>
#include <Jolt/Physics/Vehicle/VehicleConstraint.h>
namespace SharpCpp
{

template <class T, class TAllocator>
struct [[sharpcpp::managed_type(
    public_type = "JPH.Array<{0}>",
    managed_base_type = "global::SharpCpp.Support.VectorBase<{0}>",
    managed_namespace = "JPH"
)]] Marshaller<JPH::Array<T, TAllocator>>
    : SequenceCollectionMarshallerBase<JPH::Array<T, TAllocator>, T> {};

template <class T, class TAllocator>
struct Marshaller<JPH::Array<T, TAllocator>&>
    : OwnedHandleReferenceMarshallerBase<JPH::Array<T, TAllocator>> {};

template <class T, class TAllocator>
struct Marshaller<const JPH::Array<T, TAllocator>&>
    : OwnedHandleConstReferenceMarshallerBase<JPH::Array<T, TAllocator>> {};

template <class T, class TAllocator>
struct Marshaller<JPH::Array<T, TAllocator>*>
    : OwnedHandlePointerMarshallerBase<JPH::Array<T, TAllocator>> {};

template <class T, class TAllocator>
struct Marshaller<const JPH::Array<T, TAllocator>*>
    : OwnedHandleConstPointerMarshallerBase<JPH::Array<T, TAllocator>> {};

template <class T, class TAllocator>
struct [[sharpcpp::managed_type(return_ownership = "owning")]]
ReturnMarshaller<JPH::Array<T, TAllocator>>
    : OwnedHandleReturnMarshallerBase<JPH::Array<T, TAllocator>> {};

template <class T, JPH::uint N>
struct [[sharpcpp::managed_type(
    public_type = "JPH.StaticArray<{0}>",
    managed_base_type = "global::SharpCpp.Support.VectorBase<{0}>",
    managed_namespace = "JPH"
)]] Marshaller<JPH::StaticArray<T, N>>
    : IndexedSequenceCollectionMarshallerBase<JPH::StaticArray<T, N>, T> {};

template <class T, JPH::uint N>
struct Marshaller<JPH::StaticArray<T, N>&>
    : OwnedHandleReferenceMarshallerBase<JPH::StaticArray<T, N>> {};

template <class T, JPH::uint N>
struct Marshaller<const JPH::StaticArray<T, N>&>
    : OwnedHandleConstReferenceMarshallerBase<JPH::StaticArray<T, N>> {};

template <class T, JPH::uint N>
struct Marshaller<JPH::StaticArray<T, N>*>
    : OwnedHandlePointerMarshallerBase<JPH::StaticArray<T, N>> {};

template <class T, JPH::uint N>
struct Marshaller<const JPH::StaticArray<T, N>*>
    : OwnedHandleConstPointerMarshallerBase<JPH::StaticArray<T, N>> {};

template <class T, JPH::uint N>
struct [[sharpcpp::managed_type(return_ownership = "owning")]]
ReturnMarshaller<JPH::StaticArray<T, N>>
    : OwnedHandleReturnMarshallerBase<JPH::StaticArray<T, N>> {};

struct JoltVector3Abi
{
    float x;
    float y;
    float z;
};

struct JoltQuaternionAbi
{
    float w;
    float x;
    float y;
    float z;
};

struct JoltRayAbi
{
    JoltVector3Abi origin;
    JoltVector3Abi direction;
};

struct JoltRayCastResultAbi
{
    uint32_t bodyId;
    float fraction;
    uint32_t subShapeId;
};

inline JoltVector3Abi ToAbi(JPH::Vec3Arg value) noexcept
{
    return {value.GetX(), value.GetY(), value.GetZ()};
}

inline JPH::Vec3 ToCpp(const JoltVector3Abi& value) noexcept
{
    return {value.x, value.y, value.z};
}

inline JoltQuaternionAbi ToAbi(JPH::QuatArg value) noexcept
{
    return {value.GetW(), value.GetX(), value.GetY(), value.GetZ()};
}

inline JPH::Quat ToCpp(const JoltQuaternionAbi& value) noexcept
{
    return {value.x, value.y, value.z, value.w};
}

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::RedBolt.Math.BaseRealVector3<float>",
    managed_marshaller = "global::RedBolt.Math.Vector3fMarshaller",
    suppress_emission = true
)]] Marshaller<JPH::Vec3>
{
    using cpp_type = JPH::Vec3;
    using abi_type = JoltVector3Abi;

    abi_type ConvertToAbi(cpp_type value) noexcept { return ToAbi(value); }
    cpp_type ConvertToCpp(const abi_type& value) noexcept { return ToCpp(value); }
};

template <>
struct Marshaller<const JPH::Vec3&> : ProjectedValueConstReferenceMarshallerBase<JPH::Vec3> {};

template <>
struct Marshaller<JPH::Vec3&> : ProjectedValueReferenceMarshallerBase<JPH::Vec3> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::RedBolt.Math.Quaternion",
    suppress_emission = true
)]] Marshaller<JPH::Quat>
{
    using cpp_type = JPH::Quat;
    using abi_type = JoltQuaternionAbi;

    abi_type ConvertToAbi(cpp_type value) noexcept { return ToAbi(value); }
    cpp_type ConvertToCpp(const abi_type& value) noexcept { return ToCpp(value); }
};

template <>
struct Marshaller<const JPH::Quat&> : ProjectedValueConstReferenceMarshallerBase<JPH::Quat> {};

template <>
struct Marshaller<JPH::Quat&> : ProjectedValueReferenceMarshallerBase<JPH::Quat> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "uint",
    suppress_emission = true
)]] Marshaller<JPH::BodyID>
{
    using cpp_type = JPH::BodyID;
    using abi_type = uint32_t;

    abi_type ConvertToAbi(cpp_type value) noexcept { return value.GetIndexAndSequenceNumber(); }
    cpp_type ConvertToCpp(abi_type value) noexcept { return JPH::BodyID(value); }
};

template <>
struct Marshaller<const JPH::BodyID&> : ProjectedValueConstReferenceMarshallerBase<JPH::BodyID> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "byte",
    suppress_emission = true
)]] Marshaller<JPH::BroadPhaseLayer>
{
    using cpp_type = JPH::BroadPhaseLayer;
    using abi_type = uint8_t;

    abi_type ConvertToAbi(cpp_type value) noexcept { return value.GetValue(); }
    cpp_type ConvertToCpp(abi_type value) noexcept { return JPH::BroadPhaseLayer(value); }
};

template <>
struct [[sharpcpp::managed_type(
    public_type = "uint",
    suppress_emission = true
)]] Marshaller<JPH::SubShapeID>
{
    using cpp_type = JPH::SubShapeID;
    using abi_type = uint32_t;

    abi_type ConvertToAbi(cpp_type value) noexcept { return value.GetValue(); }
    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        JPH::SubShapeID result;
        result.SetValue(value);
        return result;
    }
};

template <>
struct Marshaller<const JPH::SubShapeID&> : ProjectedValueConstReferenceMarshallerBase<JPH::SubShapeID> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::RedBolt.Math.Ray",
    suppress_emission = true
)]] Marshaller<JPH::RRayCast>
{
    using cpp_type = JPH::RRayCast;
    using abi_type = JoltRayAbi;

    abi_type ConvertToAbi(const cpp_type& value) noexcept
    {
        return {ToAbi(value.mOrigin), ToAbi(value.mDirection)};
    }
    cpp_type ConvertToCpp(const abi_type& value) noexcept
    {
        return {ToCpp(value.origin), ToCpp(value.direction)};
    }
};

template <>
struct Marshaller<const JPH::RRayCast&> : ProjectedValueConstReferenceMarshallerBase<JPH::RRayCast> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::RedBolt.Bindings.Jolt.JoltRayCastResult",
    suppress_emission = true
)]] Marshaller<JPH::RayCastResult>
{
    using cpp_type = JPH::RayCastResult;
    using abi_type = JoltRayCastResultAbi;

    abi_type ConvertToAbi(const cpp_type& value) noexcept
    {
        return {value.mBodyID.GetIndexAndSequenceNumber(), value.mFraction, value.mSubShapeID2.GetValue()};
    }
    cpp_type ConvertToCpp(const abi_type& value) noexcept
    {
        JPH::RayCastResult result;
        result.mBodyID = JPH::BodyID(value.bodyId);
        result.mFraction = value.fraction;
        result.mSubShapeID2.SetValue(value.subShapeId);
        return result;
    }
};

template <>
struct Marshaller<JPH::RayCastResult&> : ProjectedValueReferenceMarshallerBase<JPH::RayCastResult> {};

template <class T>
struct [[sharpcpp::managed_type(
    public_type = "{0}",
    projection = true
)]] Marshaller<JPH::Ref<T>>
{
    using cpp_type = JPH::Ref<T>;
    using abi_type = T*;

    abi_type ConvertToAbi(const cpp_type& value) noexcept { return value.GetPtr(); }
    cpp_type ConvertToCpp(abi_type value) noexcept { return cpp_type(value); }
};

template <class T>
struct Marshaller<const JPH::Ref<T>&>
{
    using cpp_type = const JPH::Ref<T>&;
    using abi_type = T*;

    abi_type ConvertToAbi(cpp_type value) noexcept { return value.GetPtr(); }
    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        value_ = value;
        return value_;
    }

private:
    JPH::Ref<T> value_{};
};

template <class T>
struct [[sharpcpp::managed_type(return_ownership = "owning")]]
ReturnMarshaller<JPH::Ref<T>> : Marshaller<JPH::Ref<T>>
{
    using typename Marshaller<JPH::Ref<T>>::abi_type;
    using typename Marshaller<JPH::Ref<T>>::cpp_type;

    abi_type ConvertToAbi(const cpp_type& value) noexcept
    {
        auto* result = value.GetPtr();
        if (result != nullptr)
            result->AddRef();
        return result;
    }
};

template <class T>
struct [[sharpcpp::managed_type(return_ownership = "owning")]]
ReturnMarshaller<const JPH::Ref<T>&> : Marshaller<const JPH::Ref<T>&>
{
    using typename Marshaller<const JPH::Ref<T>&>::abi_type;
    using typename Marshaller<const JPH::Ref<T>&>::cpp_type;

    abi_type ConvertToAbi(cpp_type value) noexcept
    {
        auto* result = value.GetPtr();
        if (result != nullptr)
            result->AddRef();
        return result;
    }
};

template <class T>
struct [[sharpcpp::managed_type(
    public_type = "{0}",
    projection = true
)]] Marshaller<JPH::RefConst<T>>
{
    using cpp_type = JPH::RefConst<T>;
    using abi_type = const T*;

    abi_type ConvertToAbi(const cpp_type& value) noexcept { return value.GetPtr(); }
    cpp_type ConvertToCpp(abi_type value) noexcept { return cpp_type(value); }
};

template <class T>
struct Marshaller<const JPH::RefConst<T>&>
{
    using cpp_type = const JPH::RefConst<T>&;
    using abi_type = const T*;

    abi_type ConvertToAbi(cpp_type value) noexcept { return value.GetPtr(); }
    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        value_ = value;
        return value_;
    }

private:
    JPH::RefConst<T> value_{};
};

template <class T>
struct [[sharpcpp::managed_type(return_ownership = "owning")]]
ReturnMarshaller<JPH::RefConst<T>> : Marshaller<JPH::RefConst<T>>
{
    using typename Marshaller<JPH::RefConst<T>>::abi_type;
    using typename Marshaller<JPH::RefConst<T>>::cpp_type;

    abi_type ConvertToAbi(const cpp_type& value) noexcept
    {
        const auto* result = value.GetPtr();
        if (result != nullptr)
            result->AddRef();
        return result;
    }
};

template <class T>
struct [[sharpcpp::managed_type(return_ownership = "owning")]]
ReturnMarshaller<const JPH::RefConst<T>&> : Marshaller<const JPH::RefConst<T>&>
{
    using typename Marshaller<const JPH::RefConst<T>&>::abi_type;
    using typename Marshaller<const JPH::RefConst<T>&>::cpp_type;

    abi_type ConvertToAbi(cpp_type value) noexcept
    {
        const auto* result = value.GetPtr();
        if (result != nullptr)
            result->AddRef();
        return result;
    }
};

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::System.IntPtr",
    suppress_emission = true
)]] Marshaller<std::shared_mutex*>
{
    using cpp_type = std::shared_mutex*;
    using abi_type = void*;

    abi_type ConvertToAbi(cpp_type value) noexcept { return value; }
    cpp_type ConvertToCpp(abi_type value) noexcept { return static_cast<cpp_type>(value); }
};

template <>
struct Marshaller<JPH::VehicleConstraint> : detail::DefaultMarshallerBase<JPH::VehicleConstraint>
{
    using cpp_type = JPH::VehicleConstraint;
    using abi_type = JPH::VehicleConstraint*;

    // PhysicsStepListener is a secondary base, so its address may differ from
    // the VehicleConstraint address and must be adjusted before crossing the ABI.
    [[sharpcpp::expose_member]]
    static void* GetPhysicsStepListenerHandle(cpp_type& self) noexcept
    {
        return static_cast<JPH::PhysicsStepListener*>(&self);
    }
};

} // namespace SharpCpp
