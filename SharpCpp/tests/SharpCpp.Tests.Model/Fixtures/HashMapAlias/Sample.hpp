#pragma once

namespace eastl
{
template <typename T>
class basic_string {};

using string = basic_string<char>;
}

namespace eastl
{
template <typename TKey, typename TValue, typename THash = int, typename TPredicate = int, typename TAllocator = int, bool CacheHashCode = true>
class hash_map {};

template <typename TKey, typename TValue, typename THash = int, typename TPredicate = int, typename TAllocator = int, bool CacheHashCode = true>
using unordered_map = hash_map<TKey, TValue, THash, TPredicate, TAllocator, CacheHashCode>;
}

namespace ea = eastl;

namespace Urho3D
{
struct StringHash {};
class Variant
{
public:
    Variant();
};

using VariantMap = ea::unordered_map<StringHash, Variant>;
using StringVariantMap = ea::unordered_map<ea::string, Variant>;

class EventHub
{
public:
    const VariantMap& GetEventDataMap() const;
    const StringVariantMap& GetStringEventDataMap() const;
};
}
