#include <SharpCpp/Export.hpp>

#include <Urho3D/Core/TypeInfo.h>

#include <memory>
#include <mutex>
#include <string>
#include <unordered_map>

namespace
{
using ManagedTypeInfoMap = std::unordered_map<std::string, std::unique_ptr<Urho3D::TypeInfo>>;

ManagedTypeInfoMap& GetManagedTypeInfos()
{
    static ManagedTypeInfoMap typeInfos;
    return typeInfos;
}

std::mutex& GetManagedTypeInfoMutex()
{
    static std::mutex mutex;
    return mutex;
}
} // namespace

extern "C"
{
SHARPCPP_EXPORT void* sharpcpp_rbfx_get_or_create_managed_type_info(const char* typeName, const Urho3D::TypeInfo* baseTypeInfo)
{
    if (typeName == nullptr || *typeName == '\0')
    {
        return nullptr;
    }

    auto& mutex = GetManagedTypeInfoMutex();
    std::lock_guard lock{mutex};

    auto& typeInfos = GetManagedTypeInfos();
    auto [iterator, inserted] = typeInfos.try_emplace(typeName, nullptr);
    if (inserted || iterator->second == nullptr)
    {
        iterator->second = std::make_unique<Urho3D::TypeInfo>(iterator->first.c_str(), baseTypeInfo);
    }

    return iterator->second.get();
}
}
