#include <RbfxUserCode/RotatingComponent.hpp>

namespace RbfxUserCode
{

RotatingComponent::RotatingComponent(Urho3D::Context* context)
    : LogicComponent(context)
{
}

void RotatingComponent::SetRotationSpeed(float degreesPerSecond)
{
    rotationSpeed_ = degreesPerSecond;
}

float RotatingComponent::GetRotationSpeed() const
{
    return rotationSpeed_;
}

SharedPtrPayload::SharedPtrPayload(int value)
    : value_(value)
{
}

int SharedPtrPayload::GetValue() const
{
    return value_;
}

void SharedPtrPayload::SetValue(int value)
{
    value_ = value;
}

SharedPtrOwner::SharedPtrOwner()
    : payload_(new SharedPtrPayload(42))
{
}

SharedPtrPayload* SharedPtrOwner::GetPayloadRaw() const
{
    return payload_.Get();
}

Urho3D::SharedPtr<SharedPtrPayload> SharedPtrOwner::GetPayloadShared() const
{
    return payload_;
}

Urho3D::WeakPtr<SharedPtrPayload> SharedPtrOwner::GetPayloadWeak() const
{
    return payload_;
}

} // namespace RbfxUserCode
