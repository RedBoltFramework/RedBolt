#include <SharpCpp/Export.hpp>
#include <SharpCpp/detail/RbfxRefCountedAccess.hpp>

#include <Urho3D/Container/RefCounted.h>

extern "C"
{
SHARPCPP_EXPORT void* sharpcpp_rbfx_refcounted_get_script_object(Urho3D::RefCounted* self)
{
    return sharpcpp::interop::rbfx::GetScriptObject(self);
}

SHARPCPP_EXPORT void sharpcpp_rbfx_refcounted_set_script_object(Urho3D::RefCounted* self, void* handle, bool isStrong)
{
    sharpcpp::interop::rbfx::AssignScriptObject(self, handle, isStrong);
}

SHARPCPP_EXPORT void sharpcpp_rbfx_refcounted_reset_script_object(Urho3D::RefCounted* self)
{
    sharpcpp::interop::rbfx::ResetScriptObject(self);
}

SHARPCPP_EXPORT void sharpcpp_rbfx_refcounted_add_ref(Urho3D::RefCounted* self)
{
    if (self != nullptr)
    {
        self->AddRef();
    }
}

SHARPCPP_EXPORT void sharpcpp_rbfx_refcounted_release_ref(Urho3D::RefCounted* self)
{
    if (self != nullptr)
    {
        self->ReleaseRef();
    }
}
}
