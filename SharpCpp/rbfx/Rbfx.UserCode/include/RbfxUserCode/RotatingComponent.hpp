#pragma once

#include <Urho3D/Container/Ptr.h>
#include <Urho3D/Container/RefCounted.h>
#include <Urho3D/Core/Context.h>
#include <Urho3D/Scene/LogicComponent.h>

namespace RbfxUserCode
{

class RotatingComponent : public Urho3D::LogicComponent
{
public:
    void SetRotationSpeed(float degreesPerSecond);
    float GetRotationSpeed() const;

protected:
    explicit RotatingComponent(Urho3D::Context* context);

private:
    float rotationSpeed_{45.0f};
};

class SharedPtrPayload : public Urho3D::RefCounted
{
public:
    explicit SharedPtrPayload(int value = 42);

    int GetValue() const;
    void SetValue(int value);

private:
    int value_{};
};

class SharedPtrOwner : public Urho3D::RefCounted
{
public:
    SharedPtrOwner();

    SharedPtrPayload* GetPayloadRaw() const;
    Urho3D::SharedPtr<SharedPtrPayload> GetPayloadShared() const;
    Urho3D::WeakPtr<SharedPtrPayload> GetPayloadWeak() const;

private:
    Urho3D::SharedPtr<SharedPtrPayload> payload_;
};

} // namespace RbfxUserCode
