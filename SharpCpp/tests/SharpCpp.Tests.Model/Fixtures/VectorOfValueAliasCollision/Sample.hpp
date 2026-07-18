#pragma once

namespace eastl
{
template <class T, class TAllocator = void>
class vector
{
};
}

namespace Rml
{
class Vector3
{
};
}

namespace Urho3D
{
template <class T>
class BaseVector3
{
public:
    T x_;
    T y_;
    T z_;
};

using Vector3 = BaseVector3<float>;

class HullBuilder
{
public:
    const eastl::vector<Vector3>& GetVertices() const;
    void SetVertices(const eastl::vector<Vector3>& vertices);

    eastl::vector<Vector3> stored_;
};
}
