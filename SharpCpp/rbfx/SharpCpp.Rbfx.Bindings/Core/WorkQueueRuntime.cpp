#include <SharpCpp/Export.hpp>

#include <Urho3D/Core/WorkQueue.h>

#include <EASTL/shared_ptr.h>

namespace
{
using TaskCallback = void (*)(void* callbackHandle, unsigned threadIndex, Urho3D::WorkQueue* queue);
using ReleaseCallback = void (*)(void* callbackHandle);

void ReleaseManagedHandle(ReleaseCallback release, void* handle)
{
    if (release != nullptr && handle != nullptr)
    {
        release(handle);
    }
}

Urho3D::TaskFunction WrapManagedTask(TaskCallback callback, ReleaseCallback release, void* callbackHandle)
{
    const ea::shared_ptr<void> callbackLifetime(callbackHandle, [release](void* handle) { ReleaseManagedHandle(release, handle); });

    return [callback, callbackLifetime](unsigned threadIndex, Urho3D::WorkQueue* queue)
    {
        callback(callbackLifetime.get(), threadIndex, queue);
    };
}
}

extern "C"
{
SHARPCPP_EXPORT void SharpCpp_Rbfx_WorkQueue_PostTaskForMainThread(
    Urho3D::WorkQueue* queue,
    TaskCallback callback,
    ReleaseCallback release,
    void* callbackHandle,
    Urho3D::TaskPriority priority)
{
    if (queue != nullptr && callback != nullptr)
    {
        queue->PostTaskForMainThread(WrapManagedTask(callback, release, callbackHandle), priority);
        return;
    }

    ReleaseManagedHandle(release, callbackHandle);
}

SHARPCPP_EXPORT void SharpCpp_Rbfx_WorkQueue_PostDelayedTaskForMainThread(
    Urho3D::WorkQueue* queue,
    TaskCallback callback,
    ReleaseCallback release,
    void* callbackHandle)
{
    if (queue != nullptr && callback != nullptr)
    {
        queue->PostDelayedTaskForMainThread(WrapManagedTask(callback, release, callbackHandle));
        return;
    }

    ReleaseManagedHandle(release, callbackHandle);
}
}
