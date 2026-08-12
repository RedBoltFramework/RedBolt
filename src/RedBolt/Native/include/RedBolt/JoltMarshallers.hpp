#pragma once

#include <cstdint>
#include <SharpCpp/Marshaller.hpp>

#if defined(REDBOLT_JOLT_BINDING_PARSE)
#define _M_ARM 1
#endif

#include <Jolt/Jolt.h>
#include <Jolt/Math/Quat.h>
#include <Jolt/Physics/Body/BodyID.h>
#include <Jolt/Physics/Body/BodyLock.h>
#include <Jolt/Physics/Body/MassProperties.h>
#include <Jolt/Physics/Character/Character.h>
#include <Jolt/Physics/Collision/BroadPhase/BroadPhaseLayer.h>
#include <Jolt/Physics/Collision/CastResult.h>
#include <Jolt/Physics/Collision/RayCast.h>
#include <Jolt/Physics/Collision/Shape/SubShapeID.h>
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
        wheel->mWheelForward = normalizedAxle.Cross(wheel->mWheelUp).NormalizedOr(settings_.mForward);
        wheel->mSuspensionMinLength = std::max(0.0f, suspensionRestLength - maxSuspensionTravel);
        wheel->mSuspensionMaxLength = std::max(wheel->mSuspensionMinLength,
            suspensionRestLength + maxSuspensionTravel);
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
        wheel->mMaxBrakeTorque = std::max(0.0f, maxBrakeTorque);
        wheel->mMaxHandBrakeTorque = wheel->mMaxBrakeTorque;

        // Jolt has no per-wheel suspension force cap. Keep the argument in this narrow adapter so the
        // public rbfx API remains complete and a future native equivalent can be applied without ABI churn.
        (void)maxSuspensionForce;

        const float frictionScale = std::max(0.0f, frictionSlip / 10.5f);
        wheel->mLongitudinalFriction.Clear();
        wheel->mLongitudinalFriction.AddPoint(0.0f, 0.0f);
        wheel->mLongitudinalFriction.AddPoint(0.06f, 1.2f * frictionScale);
        wheel->mLongitudinalFriction.AddPoint(0.2f, frictionScale);
        wheel->mLateralFriction.Clear();
        wheel->mLateralFriction.AddPoint(0.0f, 0.0f);
        wheel->mLateralFriction.AddPoint(3.0f, 1.2f * frictionScale);
        wheel->mLateralFriction.AddPoint(20.0f, frictionScale);

        settings_.mWheels.push_back(wheel);
        engineFactors_.push_back(std::max(0.0f, std::abs(engineFactor)));
    }

    bool Initialize(bool enabled)
    {
        if (vehicle_ != nullptr)
            return true;

        ConfigureDifferentials();
        {
            BodyLockWrite lock(physicsSystem_->GetBodyLockInterface(), bodyId_);
            if (!lock.Succeeded())
                return false;
            vehicle_ = new VehicleConstraint(lock.GetBody(), settings_);
        }

        vehicle_->SetVehicleCollisionTester(new VehicleCollisionTesterRay(objectLayer_));
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
            controller->GetEngine().mMaxTorque = std::max(0.0f, engineTorque);
            controller->SetDriverInput(forward, right, brake, 0.0f);
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
        const RMat44 transform = vehicle_->GetWheelWorldTransform(index, Vec3::sAxisX(), Vec3::sAxisY());
        return Vec3(transform.GetTranslation());
    }

    Quat GetWheelRotation(uint32_t index) const
    {
        if (!HasWheel(index))
            return Quat::sIdentity();
        return vehicle_->GetWheelWorldTransform(index, Vec3::sAxisX(), Vec3::sAxisY()).GetQuaternion();
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
    void ConfigureDifferentials()
    {
        controllerSettings_->mDifferentials.clear();
        float totalFactor = 0.0f;
        for (const float factor : engineFactors_)
            totalFactor += factor;
        if (totalFactor <= 0.0f)
            return;

        for (uint32_t i = 0; i < engineFactors_.size(); ++i)
        {
            if (engineFactors_[i] <= 0.0f)
                continue;
            VehicleDifferentialSettings differential;
            differential.mLeftWheel = static_cast<int>(i);
            differential.mRightWheel = -1;
            differential.mEngineTorqueRatio = engineFactors_[i] / totalFactor;
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

    PhysicsSystem* physicsSystem_{};
    BodyID bodyId_{};
    ObjectLayer objectLayer_{};
    VehicleConstraintSettings settings_{};
    Ref<WheeledVehicleControllerSettings> controllerSettings_{};
    std::vector<float> engineFactors_{};
    Ref<VehicleConstraint> vehicle_{};
    bool addedConstraint_{};
    bool addedStepListener_{};
};

} // namespace JPH
