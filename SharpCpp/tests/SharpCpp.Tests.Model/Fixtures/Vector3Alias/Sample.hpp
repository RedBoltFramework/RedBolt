#pragma once

namespace Urho3D
{
template <class T>
class BaseVector3
{
public:
    BaseVector3() = default;
    BaseVector3(T x, T y, T z) : x_(x), y_(y), z_(z) {}

    T x_;
    T y_;
    T z_;
};

using Vector3 = BaseVector3<float>;

class ActionBuilder
{
public:
    void JumpBy(const Vector3& offset);
    void MoveBy(const Vector3& offset = Vector3(0, 0, 0));
};
}
