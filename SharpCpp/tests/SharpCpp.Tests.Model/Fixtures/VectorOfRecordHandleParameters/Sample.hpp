#pragma once

namespace eastl
{
template <class T, class TAllocator = void>
class vector
{
};
}

namespace Urho3D
{
class Object
{
};

class EventHub
{
public:
    void UnsubscribeFromAllEventsExcept(const eastl::vector<Urho3D::Object*>& exceptions);
};
}
