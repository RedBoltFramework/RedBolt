#pragma once

#include <cstdint>
#include <SharpCpp/Marshaller.hpp>

#if defined(REDBOLT_JOLT_BINDING_PARSE)
#define _M_ARM 1
#endif

#include <Jolt/Jolt.h>
#include <Jolt/Math/Quat.h>
#include <Jolt/Physics/Body/BodyID.h>
#include <Jolt/Physics/Body/MassProperties.h>
#include <Jolt/Physics/Collision/BroadPhase/BroadPhaseLayer.h>
#include <Jolt/Physics/Collision/CastResult.h>
#include <Jolt/Physics/Collision/RayCast.h>
#include <Jolt/Physics/Collision/Shape/SubShapeID.h>
#include <Jolt/Physics/Collision/ContactListener.h>
namespace SharpCpp
{

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

} // namespace SharpCpp
