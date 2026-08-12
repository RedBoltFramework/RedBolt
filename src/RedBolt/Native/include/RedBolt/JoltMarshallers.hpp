#pragma once

#include <cstdint>
#include <SharpCpp/Marshaller.hpp>

#if defined(REDBOLT_JOLT_BINDING_PARSE)
#define _M_ARM 1
#endif

#include <Jolt/Jolt.h>
#include <Jolt/Math/Quat.h>
#include <Jolt/Physics/Body/BodyID.h>
#include <Jolt/Physics/Body/BodyFilter.h>
#include <Jolt/Physics/Body/BodyLock.h>
#include <Jolt/Physics/Body/MassProperties.h>
#include <Jolt/Physics/Character/Character.h>
#include <Jolt/Physics/Collision/BroadPhase/BroadPhaseLayer.h>
#include <Jolt/Physics/Collision/CastResult.h>
#include <Jolt/Physics/Collision/CollisionCollectorImpl.h>
#include <Jolt/Physics/Collision/RayCast.h>
#include <Jolt/Physics/Collision/ShapeCast.h>
#include <Jolt/Physics/Collision/Shape/BoxShape.h>
#include <Jolt/Physics/Collision/Shape/ConvexHullShape.h>
#include <Jolt/Physics/Collision/Shape/HeightFieldShape.h>
#include <Jolt/Physics/Collision/Shape/MeshShape.h>
#include <Jolt/Physics/Collision/Shape/RotatedTranslatedShape.h>
#include <Jolt/Physics/Collision/Shape/SphereShape.h>
#include <Jolt/Physics/Collision/Shape/StaticCompoundShape.h>
#include <Jolt/Physics/Collision/Shape/SubShapeID.h>
#include <Jolt/Physics/Collision/Shape/TaperedCylinderShape.h>
#include <Jolt/Physics/Collision/Shape/TriangleShape.h>
#include <Jolt/Physics/Collision/ContactListener.h>
#include <Jolt/Physics/PhysicsSystem.h>
#include <Jolt/Physics/Vehicle/VehicleCollisionTester.h>
#include <Jolt/Physics/Vehicle/VehicleConstraint.h>
#include <Jolt/Physics/Vehicle/WheeledVehicleController.h>

#include <algorithm>
#include <cmath>
#include <vector>
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

namespace JPH
{

inline void SetCharacterSettingsShape(CharacterSettings& settings, const Shape* shape)
{
    settings.mShape = shape;
}

inline void RedBoltResetBodyForces(PhysicsSystem& physicsSystem, uint32_t bodyId)
{
    BodyLockWrite lock(physicsSystem.GetBodyLockInterface(), BodyID(bodyId));
    if (!lock.Succeeded())
        return;
    lock.GetBody().ResetForce();
    lock.GetBody().ResetTorque();
}

class RedBoltHeightFieldShape : public NonCopyable
{
public:
    RedBoltHeightFieldShape(const void* samples, uint32_t sampleCount, Vec3Arg offset, Vec3Arg scale)
    {
        if (samples == nullptr || sampleCount < 2)
            return;

        HeightFieldShapeSettings settings(static_cast<const float*>(samples), offset, scale, sampleCount);
        const auto result = settings.Create();
        if (result.IsValid())
            shape_ = result.Get().GetPtr();
    }

    bool IsValid() const { return shape_ != nullptr; }
    const Shape* GetShape() const { return shape_; }

private:
    RefConst<Shape> shape_{};
};

class RedBoltPlaneShape : public NonCopyable
{
public:
    RedBoltPlaneShape()
        : shape_(new RotatedTranslatedShape(
            Vec3(0.0f, -1000.0f, 0.0f), Quat::sIdentity(), new BoxShape(Vec3::sReplicate(1000.0f))))
    {
    }

    bool IsValid() const { return shape_ != nullptr; }
    const Shape* GetShape() const { return shape_; }

private:
    RefConst<Shape> shape_{};
};

class RedBoltConeShape : public NonCopyable
{
public:
    RedBoltConeShape(float halfHeight, float radius, float convexRadius)
    {
        TaperedCylinderShapeSettings settings(
            std::max(halfHeight, 0.001f), 0.0f, std::max(radius, 0.001f), std::max(convexRadius, 0.0f));
        const auto result = settings.Create();
        if (result.IsValid())
            shape_ = result.Get().GetPtr();
    }

    bool IsValid() const { return shape_ != nullptr; }
    const Shape* GetShape() const { return shape_; }

private:
    RefConst<Shape> shape_{};
};

class RedBoltGeometryShape : public NonCopyable
{
public:
    RedBoltGeometryShape(const void* vertices, uint32_t vertexCount, const void* indices, uint32_t indexCount,
        uint32_t shapeType, float convexRadius)
    {
        if (vertices == nullptr || vertexCount == 0)
            return;

        const auto* sourceVertices = static_cast<const SharpCpp::JoltVector3Abi*>(vertices);
        if (shapeType == 1)
        {
            Array<Vec3> points;
            points.reserve(vertexCount);
            for (uint32_t i = 0; i < vertexCount; ++i)
                points.emplace_back(SharpCpp::ToCpp(sourceVertices[i]));

            ConvexHullShapeSettings settings(points, std::max(convexRadius, 0.0f));
            const auto result = settings.Create();
            if (result.IsValid())
                shape_ = result.Get().GetPtr();
            return;
        }

        if (indices == nullptr || indexCount < 3)
            return;

        const auto* sourceIndices = static_cast<const uint32_t*>(indices);
        if (shapeType == 2)
        {
            StaticCompoundShapeSettings settings;
            for (uint32_t i = 0; i + 2 < indexCount; i += 3)
            {
                const uint32_t i0 = sourceIndices[i];
                const uint32_t i1 = sourceIndices[i + 1];
                const uint32_t i2 = sourceIndices[i + 2];
                if (i0 >= vertexCount || i1 >= vertexCount || i2 >= vertexCount)
                    continue;
                settings.AddShape(Vec3::sZero(), Quat::sIdentity(),
                    new TriangleShape(SharpCpp::ToCpp(sourceVertices[i0]), SharpCpp::ToCpp(sourceVertices[i1]),
                        SharpCpp::ToCpp(sourceVertices[i2]), std::max(convexRadius, 0.0f)));
            }
            if (settings.mSubShapes.empty())
                return;
            const auto result = settings.Create();
            if (result.IsValid())
                shape_ = result.Get().GetPtr();
            return;
        }

        VertexList meshVertices;
        meshVertices.reserve(vertexCount);
        for (uint32_t i = 0; i < vertexCount; ++i)
            meshVertices.emplace_back(sourceVertices[i].x, sourceVertices[i].y, sourceVertices[i].z);

        IndexedTriangleList triangles;
        triangles.reserve(indexCount / 3);
        for (uint32_t i = 0; i + 2 < indexCount; i += 3)
        {
            const uint32_t i0 = sourceIndices[i];
            const uint32_t i1 = sourceIndices[i + 1];
            const uint32_t i2 = sourceIndices[i + 2];
            if (i0 < vertexCount && i1 < vertexCount && i2 < vertexCount)
                triangles.emplace_back(i0, i1, i2, 0);
        }
        if (triangles.empty())
            return;

        MeshShapeSettings settings(std::move(meshVertices), std::move(triangles));
        const auto result = settings.Create();
        if (result.IsValid())
            shape_ = result.Get().GetPtr();
    }

    bool IsValid() const { return shape_ != nullptr; }
    const Shape* GetShape() const { return shape_; }

private:
    RefConst<Shape> shape_{};
};

class RedBoltCompoundShape : public NonCopyable
{
public:
    RedBoltCompoundShape() = default;

    void AddShape(const Shape* shape)
    {
        if (shape != nullptr && shape_ == nullptr)
            settings_.AddShape(Vec3::sZero(), Quat::sIdentity(), shape);
    }

    bool Build()
    {
        if (shape_ != nullptr)
            return true;
        const auto result = settings_.Create();
        if (result.IsValid())
            shape_ = result.Get().GetPtr();
        return shape_ != nullptr;
    }

    const Shape* GetShape() const { return shape_; }

private:
    StaticCompoundShapeSettings settings_{};
    RefConst<Shape> shape_{};
};

class RedBoltPhysicsQueryResults : public NonCopyable
{
public:
    RedBoltPhysicsQueryResults() = default;

    void Raycast(const PhysicsSystem& physicsSystem, const RRayCast& ray,
        const BroadPhaseLayerFilter& broadPhaseLayerFilter, const ObjectLayerFilter& objectLayerFilter)
    {
        hits_.clear();
        RayCastSettings settings;
        AllHitCollisionCollector<CastRayCollector> collector;
        physicsSystem.GetNarrowPhaseQuery().CastRay(
            ray, settings, collector, broadPhaseLayerFilter, objectLayerFilter);
        collector.Sort();
        for (const RayCastResult& hit : collector.mHits)
        {
            BodyLockRead lock(physicsSystem.GetBodyLockInterface(), hit.mBodyID);
            if (!lock.SucceededAndIsInBroadPhase())
                continue;
            const RVec3 position = ray.GetPointOnRay(hit.mFraction);
            hits_.push_back({hit.mBodyID, Vec3(position),
                lock.GetBody().GetWorldSpaceSurfaceNormal(hit.mSubShapeID2, position), hit.mFraction});
        }
    }

    void SphereCast(const PhysicsSystem& physicsSystem, const RRayCast& ray, float radius,
        const BroadPhaseLayerFilter& broadPhaseLayerFilter, const ObjectLayerFilter& objectLayerFilter)
    {
        RefConst<Shape> shape = new SphereShape(std::max(radius, 0.001f));
        CastShape(physicsSystem, shape, Vec3::sOne(), RMat44::sTranslation(ray.mOrigin), ray.mDirection,
            broadPhaseLayerFilter, objectLayerFilter);
    }

    void ConvexCast(const PhysicsSystem& physicsSystem, const Shape* shape, Vec3Arg scale,
        Vec3Arg startPosition, QuatArg startRotation, Vec3Arg endPosition,
        uint32_t ignoreBodyId, const BroadPhaseLayerFilter& broadPhaseLayerFilter,
        const ObjectLayerFilter& objectLayerFilter)
    {
        hits_.clear();
        if (shape == nullptr)
            return;
        CastShape(physicsSystem, shape, scale,
            RMat44::sRotationTranslation(startRotation, RVec3(startPosition)), endPosition - startPosition,
            ignoreBodyId, broadPhaseLayerFilter, objectLayerFilter);
    }

    void QuerySphere(const PhysicsSystem& physicsSystem, Vec3Arg center, float radius,
        const BroadPhaseLayerFilter& broadPhaseLayerFilter, const ObjectLayerFilter& objectLayerFilter)
    {
        RefConst<Shape> shape = new SphereShape(std::max(radius, 0.001f));
        CollideShape(physicsSystem, shape, RMat44::sTranslation(RVec3(center)),
            broadPhaseLayerFilter, objectLayerFilter);
    }

    void QueryBox(const PhysicsSystem& physicsSystem, Vec3Arg minimum, Vec3Arg maximum,
        const BroadPhaseLayerFilter& broadPhaseLayerFilter, const ObjectLayerFilter& objectLayerFilter)
    {
        const Vec3 halfExtent = Vec3::sMax((maximum - minimum) * 0.5f, Vec3::sReplicate(0.001f));
        RefConst<Shape> shape = new BoxShape(halfExtent);
        CollideShape(physicsSystem, shape, RMat44::sTranslation(RVec3((minimum + maximum) * 0.5f)),
            broadPhaseLayerFilter, objectLayerFilter);
    }

    void QueryShape(const PhysicsSystem& physicsSystem, const Shape* shape, Vec3Arg position, QuatArg rotation,
        uint32_t ignoreBodyId, const BroadPhaseLayerFilter& broadPhaseLayerFilter,
        const ObjectLayerFilter& objectLayerFilter)
    {
        bodyIds_.clear();
        if (shape == nullptr)
            return;
        CollideShape(physicsSystem, shape, RMat44::sRotationTranslation(rotation, RVec3(position)),
            ignoreBodyId, broadPhaseLayerFilter, objectLayerFilter);
    }

    uint32_t GetNumHits() const { return static_cast<uint32_t>(hits_.size()); }
    BodyID GetHitBodyId(uint32_t index) const { return index < hits_.size() ? hits_[index].bodyId : BodyID(); }
    Vec3 GetHitPosition(uint32_t index) const { return index < hits_.size() ? hits_[index].position : Vec3::sZero(); }
    Vec3 GetHitNormal(uint32_t index) const { return index < hits_.size() ? hits_[index].normal : Vec3::sZero(); }
    float GetHitFraction(uint32_t index) const { return index < hits_.size() ? hits_[index].fraction : 0.0f; }
    uint32_t GetNumBodies() const { return static_cast<uint32_t>(bodyIds_.size()); }
    BodyID GetBodyId(uint32_t index) const { return index < bodyIds_.size() ? bodyIds_[index] : BodyID(); }

private:
    struct Hit
    {
        BodyID bodyId;
        Vec3 position;
        Vec3 normal;
        float fraction;
    };

    void CollideShape(const PhysicsSystem& physicsSystem, const Shape* shape, RMat44Arg transform,
        const BroadPhaseLayerFilter& broadPhaseLayerFilter, const ObjectLayerFilter& objectLayerFilter)
    {
        CollideShape(physicsSystem, shape, transform, BodyID::cInvalidBodyID,
            broadPhaseLayerFilter, objectLayerFilter);
    }

    void CollideShape(const PhysicsSystem& physicsSystem, const Shape* shape, RMat44Arg transform,
        uint32_t ignoreBodyId, const BroadPhaseLayerFilter& broadPhaseLayerFilter,
        const ObjectLayerFilter& objectLayerFilter)
    {
        bodyIds_.clear();
        CollideShapeSettings settings;
        AllHitCollisionCollector<CollideShapeCollector> collector;
        if (ignoreBodyId == BodyID::cInvalidBodyID)
        {
            physicsSystem.GetNarrowPhaseQuery().CollideShape(shape, Vec3::sOne(), transform, settings,
                RVec3::sZero(), collector, broadPhaseLayerFilter, objectLayerFilter);
        }
        else
        {
            const IgnoreSingleBodyFilter bodyFilter{BodyID(ignoreBodyId)};
            physicsSystem.GetNarrowPhaseQuery().CollideShape(shape, Vec3::sOne(), transform, settings,
                RVec3::sZero(), collector, broadPhaseLayerFilter, objectLayerFilter, bodyFilter);
        }
        for (const CollideShapeResult& hit : collector.mHits)
        {
            if (std::find(bodyIds_.begin(), bodyIds_.end(), hit.mBodyID2) == bodyIds_.end())
                bodyIds_.push_back(hit.mBodyID2);
        }
    }

    void CastShape(const PhysicsSystem& physicsSystem, const Shape* shape, Vec3Arg scale,
        RMat44Arg startTransform, Vec3Arg direction, const BroadPhaseLayerFilter& broadPhaseLayerFilter,
        const ObjectLayerFilter& objectLayerFilter)
    {
        CastShape(physicsSystem, shape, scale, startTransform, direction, BodyID::cInvalidBodyID,
            broadPhaseLayerFilter, objectLayerFilter);
    }

    void CastShape(const PhysicsSystem& physicsSystem, const Shape* shape, Vec3Arg scale,
        RMat44Arg startTransform, Vec3Arg direction, uint32_t ignoreBodyId,
        const BroadPhaseLayerFilter& broadPhaseLayerFilter, const ObjectLayerFilter& objectLayerFilter)
    {
        hits_.clear();
        const RShapeCast shapeCast = RShapeCast::sFromWorldTransform(shape, scale, startTransform, direction);
        ShapeCastSettings settings;
        ClosestHitCollisionCollector<CastShapeCollector> collector;
        if (ignoreBodyId == BodyID::cInvalidBodyID)
        {
            physicsSystem.GetNarrowPhaseQuery().CastShape(
                shapeCast, settings, RVec3::sZero(), collector, broadPhaseLayerFilter, objectLayerFilter);
        }
        else
        {
            const IgnoreSingleBodyFilter bodyFilter{BodyID(ignoreBodyId)};
            physicsSystem.GetNarrowPhaseQuery().CastShape(shapeCast, settings, RVec3::sZero(), collector,
                broadPhaseLayerFilter, objectLayerFilter, bodyFilter);
        }
        if (!collector.HadHit())
            return;
        const ShapeCastResult& hit = collector.mHit;
        hits_.push_back({hit.mBodyID2, Vec3(hit.mContactPointOn2),
            -hit.mPenetrationAxis.NormalizedOr(Vec3::sAxisY()), hit.mFraction});
    }

    Array<Hit> hits_;
    Array<BodyID> bodyIds_;
};

class RedBoltRaycastVehicle : public NonCopyable
{
public:
    RedBoltRaycastVehicle(PhysicsSystem& physicsSystem, const BodyID& bodyId, Vec3Arg up, Vec3Arg forward,
        uint32_t objectLayer, float engineTorque)
        : physicsSystem_(&physicsSystem)
        , bodyId_(bodyId)
        , objectLayer_(static_cast<ObjectLayer>(objectLayer))
    {
        settings_.mUp = up;
        settings_.mForward = forward;
        controllerSettings_ = new WheeledVehicleControllerSettings();
        controllerSettings_->mEngine.mMaxTorque = engineTorque;
        controllerSettings_->mEngine.mNormalizedTorque.Clear();
        controllerSettings_->mEngine.mNormalizedTorque.AddPoint(0.0f, 1.0f);
        controllerSettings_->mEngine.mNormalizedTorque.AddPoint(1.0f, 1.0f);
        controllerSettings_->mTransmission.mGearRatios = {1.0f};
        controllerSettings_->mTransmission.mReverseGearRatios = {-1.0f};
        settings_.mController = controllerSettings_;
    }

    ~RedBoltRaycastVehicle()
    {
        Remove();
    }

    void AddWheel(Vec3Arg position, Vec3Arg direction, Vec3Arg axle, float rollInfluence, float radius,
        float suspensionRestLength, float maxSuspensionTravel, float suspensionStiffness, float dampingCompression,
        float dampingRelaxation, float frictionSlip, float maxSuspensionForce, float maxBrakeTorque,
        float steeringFactor, float engineFactor)
    {
        auto* wheel = new WheelSettingsWV();
        wheel->mPosition = position;
        wheel->mSuspensionDirection = direction.NormalizedOr(Vec3::sAxisY() * -1.0f);
        wheel->mSteeringAxis = -wheel->mSuspensionDirection;
        wheel->mWheelUp = -wheel->mSuspensionDirection;
        const Vec3 normalizedAxle = axle.NormalizedOr(settings_.mForward.Cross(settings_.mUp));
        // Bullet derives the wheel's forward vector as up x axle. Reversing this cross product also reverses
        // propulsion and steering, which is especially visible in the rbfx sample that uses axle = -X.
        wheel->mWheelForward = wheel->mWheelUp.Cross(normalizedAxle).NormalizedOr(settings_.mForward);
        wheel->mSuspensionMinLength = std::max(0.0f, suspensionRestLength - maxSuspensionTravel);
        // Bullet only casts as far as rest length + wheel radius, so positive travel beyond the rest length is
        // unreachable. In Jolt the maximum length is also the spring's natural length. Using rest + travel here
        // made the vehicle ride too high on a very long spring and caused persistent bouncing.
        wheel->mSuspensionMaxLength = std::max(wheel->mSuspensionMinLength, suspensionRestLength);
        wheel->mSuspensionSpring.mMode = ESpringMode::MassNormalizedStiffnessAndDamping;
        wheel->mSuspensionSpring.mStiffness = std::max(0.0f, suspensionStiffness);
        wheel->mSuspensionSpring.mDamping = std::max(0.0f, 0.5f * (dampingCompression + dampingRelaxation));
        wheel->mRadius = std::max(0.001f, radius);
        wheel->mWidth = std::max(0.01f, radius * 0.8f);
        const Vec3 neutralSuspensionPoint = position + wheel->mSuspensionDirection * suspensionRestLength;
        const float upOffset = neutralSuspensionPoint.Dot(settings_.mUp);
        wheel->mSuspensionForcePoint = neutralSuspensionPoint
            + settings_.mUp * upOffset * (std::clamp(rollInfluence, 0.0f, 1.0f) - 1.0f);
        wheel->mEnableSuspensionForcePoint = rollInfluence < 1.0f;
        wheel->mMaxSteerAngle = std::clamp(std::abs(steeringFactor), 0.0f, 0.5f * JPH_PI);
        // Bullet's brake value is an angular impulse cap per simulation step; Jolt expects torque. Preserve the
        // rbfx value at its nominal 60 Hz physics rate instead of making the sample brakes thirty times weaker.
        wheel->mMaxBrakeTorque = std::max(0.0f, maxBrakeTorque * wheel->mRadius * 60.0f);
        wheel->mMaxHandBrakeTorque = wheel->mMaxBrakeTorque;

        // Jolt has no per-wheel suspension force cap. Keep the argument in this narrow adapter so the
        // public rbfx API remains complete and a future native equivalent can be applied without ABI churn.
        (void)maxSuspensionForce;

        // Bullet uses frictionSlip only as a combined tire-impulse cap, and the rbfx sample uses 1000 as an
        // effectively unlimited sentinel. Jolt's values also shape tire response; feeding 1000 through linearly
        // over-constrains the parallel steering wheels and scrubs the vehicle to a stop at full lock.
        const float frictionScale = std::clamp(frictionSlip / 10.5f, 0.0f, 2.0f);
        wheel->mLongitudinalFriction.Clear();
        wheel->mLongitudinalFriction.AddPoint(0.0f, 0.0f);
        wheel->mLongitudinalFriction.AddPoint(0.06f, 1.2f * frictionScale);
        wheel->mLongitudinalFriction.AddPoint(0.2f, frictionScale);
        wheel->mLateralFriction.Clear();
        wheel->mLateralFriction.AddPoint(0.0f, 0.0f);
        wheel->mLateralFriction.AddPoint(3.0f, 1.2f * frictionScale);
        wheel->mLateralFriction.AddPoint(20.0f, frictionScale);

        settings_.mWheels.push_back(wheel);
        engineTorqueFactors_.push_back(std::max(0.0f, std::abs(engineFactor) * wheel->mRadius));
        suspensionStiffness_.push_back(wheel->mSuspensionSpring.mStiffness);
        suspensionDamping_.push_back(wheel->mSuspensionSpring.mDamping);
    }

    bool Initialize(bool enabled)
    {
        if (vehicle_ != nullptr)
            return true;

        ConfigureSuspension();
        ConfigureDifferentials();
        {
            BodyLockWrite lock(physicsSystem_->GetBodyLockInterface(), bodyId_);
            if (!lock.Succeeded())
                return false;
            vehicle_ = new VehicleConstraint(lock.GetBody(), settings_);
        }

        // A narrow sphere cast retains raycast-vehicle behavior while avoiding contact-normal chatter when a
        // wheel crosses adjacent terrain triangles. Jolt's own wheeled vehicle tests use this stable path.
        vehicle_->SetVehicleCollisionTester(new VehicleCollisionTesterCastSphere(objectLayer_, GetCollisionRadius()));
        physicsSystem_->AddConstraint(vehicle_);
        addedConstraint_ = true;
        SetEnabled(enabled);
        return true;
    }

    void Remove()
    {
        if (vehicle_ == nullptr)
            return;
        if (addedStepListener_)
        {
            physicsSystem_->RemoveStepListener(vehicle_);
            addedStepListener_ = false;
        }
        if (addedConstraint_)
        {
            physicsSystem_->RemoveConstraint(vehicle_);
            addedConstraint_ = false;
        }
        vehicle_ = nullptr;
    }

    void SetEnabled(bool enabled)
    {
        if (vehicle_ == nullptr)
            return;
        vehicle_->SetEnabled(enabled);
        if (enabled == addedStepListener_)
            return;
        if (enabled)
        {
            physicsSystem_->AddStepListener(vehicle_);
            addedStepListener_ = true;
        }
        else
        {
            physicsSystem_->RemoveStepListener(vehicle_);
            addedStepListener_ = false;
        }
    }

    void SetDriverInput(float forward, float right, float brake, float engineTorque)
    {
        if (auto* controller = GetController())
        {
            controller->GetEngine().mMaxTorque = std::max(0.0f, engineTorque * engineTorqueScale_);
            // Jolt derives its lateral axis as forward x up, which is -X for RedBolt's +Z-forward convention.
            controller->SetDriverInput(forward, -right, brake, 0.0f);
        }
    }

    uint32_t GetNumWheels() const
    {
        return vehicle_ != nullptr ? static_cast<uint32_t>(settings_.mWheels.size()) : 0;
    }

    Vec3 GetWheelPosition(uint32_t index) const
    {
        if (!HasWheel(index))
            return Vec3::sZero();
        const RMat44 transform = GetWheelWorldTransform(index);
        return Vec3(transform.GetTranslation());
    }

    Quat GetWheelRotation(uint32_t index) const
    {
        if (!HasWheel(index))
            return Quat::sIdentity();
        return GetWheelWorldTransform(index).GetQuaternion();
    }

    Vec3 GetWheelLocalPosition(uint32_t index) const
    {
        if (!HasWheel(index))
            return Vec3::sZero();
        return GetWheelLocalTransform(index).GetTranslation();
    }

    Quat GetWheelLocalRotation(uint32_t index) const
    {
        if (!HasWheel(index))
            return Quat::sIdentity();
        return GetWheelLocalTransform(index).GetQuaternion();
    }

    bool IsWheelInContact(uint32_t index) const
    {
        return HasWheel(index) && vehicle_->GetWheel(index)->HasContact();
    }

    Vec3 GetWheelContactPosition(uint32_t index) const
    {
        if (!IsWheelInContact(index))
            return Vec3::sZero();
        return Vec3(vehicle_->GetWheel(index)->GetContactPosition());
    }

    Vec3 GetWheelContactNormal(uint32_t index) const
    {
        if (!IsWheelInContact(index))
            return Vec3::sZero();
        return vehicle_->GetWheel(index)->GetContactNormal();
    }

    float GetWheelSuspensionLength(uint32_t index) const
    {
        return HasWheel(index) ? vehicle_->GetWheel(index)->GetSuspensionLength() : 0.0f;
    }

    float GetWheelAngularVelocity(uint32_t index) const
    {
        return HasWheel(index) ? vehicle_->GetWheel(index)->GetAngularVelocity() : 0.0f;
    }

    void SetWheelAngularVelocity(uint32_t index, float angularVelocity)
    {
        if (HasWheel(index))
            vehicle_->GetWheel(index)->SetAngularVelocity(angularVelocity);
    }

private:
    void ConfigureSuspension()
    {
        // Bullet multiplies every wheel's normalized spring and damper by the full chassis mass. Jolt multiplies
        // by the effective mass at one wheel, so compensate by the number of load-bearing wheel springs.
        const float wheelCount = static_cast<float>(settings_.mWheels.size());
        for (uint32_t i = 0; i < settings_.mWheels.size(); ++i)
        {
            auto* wheel = static_cast<WheelSettingsWV*>(settings_.mWheels[i].GetPtr());
            wheel->mSuspensionSpring.mStiffness = suspensionStiffness_[i] * wheelCount;
            wheel->mSuspensionSpring.mDamping = suspensionDamping_[i] * wheelCount;
        }
    }

    void ConfigureDifferentials()
    {
        controllerSettings_->mDifferentials.clear();
        float totalFactor = 0.0f;
        for (const float factor : engineTorqueFactors_)
            totalFactor += factor;
        engineTorqueScale_ = totalFactor;
        controllerSettings_->mEngine.mMaxTorque *= engineTorqueScale_;
        if (totalFactor <= 0.0f)
            return;

        for (uint32_t i = 0; i < engineTorqueFactors_.size(); ++i)
        {
            if (engineTorqueFactors_[i] <= 0.0f)
                continue;
            VehicleDifferentialSettings differential;
            differential.mLeftWheel = static_cast<int>(i);
            differential.mRightWheel = -1;
            differential.mDifferentialRatio = 1.0f;
            differential.mEngineTorqueRatio = engineTorqueFactors_[i] / totalFactor;
            controllerSettings_->mDifferentials.push_back(differential);
        }
    }

    WheeledVehicleController* GetController() const
    {
        return vehicle_ != nullptr ? static_cast<WheeledVehicleController*>(vehicle_->GetController()) : nullptr;
    }

    bool HasWheel(uint32_t index) const
    {
        return vehicle_ != nullptr && index < settings_.mWheels.size();
    }

    float GetCollisionRadius() const
    {
        float radius = FLT_MAX;
        for (const auto& wheelBase : settings_.mWheels)
        {
            const auto* wheel = static_cast<const WheelSettingsWV*>(wheelBase.GetPtr());
            radius = std::min(radius, 0.5f * wheel->mWidth);
        }
        return radius != FLT_MAX ? std::max(0.01f, radius) : 0.01f;
    }

    RMat44 GetWheelWorldTransform(uint32_t index) const
    {
        // Bullet exposes -axle as the model-space right axis. Jolt's wheel basis exposes axle, so flip the model
        // axis here to keep rbfx wheel node rotations and spin direction unchanged.
        return vehicle_->GetWheelWorldTransform(index, -Vec3::sAxisX(), Vec3::sAxisY());
    }

    Mat44 GetWheelLocalTransform(uint32_t index) const
    {
        return vehicle_->GetWheelLocalTransform(index, -Vec3::sAxisX(), Vec3::sAxisY());
    }

    PhysicsSystem* physicsSystem_{};
    BodyID bodyId_{};
    ObjectLayer objectLayer_{};
    VehicleConstraintSettings settings_{};
    Ref<WheeledVehicleControllerSettings> controllerSettings_{};
    std::vector<float> engineTorqueFactors_{};
    std::vector<float> suspensionStiffness_{};
    std::vector<float> suspensionDamping_{};
    float engineTorqueScale_{};
    Ref<VehicleConstraint> vehicle_{};
    bool addedConstraint_{};
    bool addedStepListener_{};
};

} // namespace JPH
