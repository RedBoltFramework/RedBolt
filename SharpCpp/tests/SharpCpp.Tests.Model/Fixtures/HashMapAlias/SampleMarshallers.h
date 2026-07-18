#pragma once

#include "Sample.hpp"

namespace SharpCpp
{
template <class T>
struct Marshaller;

template <typename T>
struct ReturnMarshaller;

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::Urho3D.Math.StringHash",
    marshaller_type = "global::Urho3D.Math.StringHash"
)]] Marshaller<Urho3D::StringHash> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "Ea.String",
    managed_base_type = "global::SharpCpp.Support.StringBase",
    managed_namespace = "Ea"
)]] Marshaller<eastl::basic_string<char>> {};

template <typename TKey, typename TValue, typename THash, typename TPredicate, typename TAllocator, bool CacheHashCode>
struct [[sharpcpp::managed_type(
    public_type = "Ea.UnorderedMap<{0}, {1}>",
    managed_base_type = "global::SharpCpp.Support.UnorderedMapBase<{0}, {1}>",
    managed_base_type_constraints = "where {0} : notnull",
    managed_namespace = "Ea"
)]] Marshaller<eastl::unordered_map<TKey, TValue, THash, TPredicate, TAllocator, CacheHashCode>> {};

template <typename TKey, typename TValue, typename THash, typename TPredicate, typename TAllocator, bool CacheHashCode>
struct Marshaller<const eastl::unordered_map<TKey, TValue, THash, TPredicate, TAllocator, CacheHashCode>&> {};

template <typename TKey, typename TValue, typename THash, typename TPredicate, typename TAllocator, bool CacheHashCode>
struct ReturnMarshaller<eastl::unordered_map<TKey, TValue, THash, TPredicate, TAllocator, CacheHashCode>> {};
}
