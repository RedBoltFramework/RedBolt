#pragma once

namespace std
{
template <class TSignature>
class function
{
public:
    function(decltype(nullptr) = nullptr);
};
}

namespace eastl
{
template <class>
class function;

template <class R, class... Args>
class function<R(Args...)>
{
public:
    function(decltype(nullptr) = nullptr);
};

template <class TKey, class TValue, class THash, class TPredicate, class TAllocator>
class hash_map
{
};
}

namespace ea = eastl;

template <class T>
struct Marshaller;

template <class TSignature>
struct [[sharpcpp::managed_type(callback = true, managed_marshaller = "global::Test.CallbackMarshaller")]] Marshaller<std::function<TSignature>> {};

template <class TSignature>
struct [[sharpcpp::managed_type(callback = true, managed_marshaller = "global::Test.CallbackMarshaller")]] Marshaller<eastl::function<TSignature>> {};

template <class TKey, class TValue, class THash, class TPredicate, class TAllocator>
struct [[sharpcpp::managed_type(
    public_type = "global::Test.Map<{0}, {1}>",
    managed_base_type = "global::SharpCpp.Support.UnorderedMapBase<{0}, {1}>",
    managed_namespace = "Test"
)]] Marshaller<eastl::hash_map<TKey, TValue, THash, TPredicate, TAllocator>> {};

namespace Test
{
struct Vector2;
}

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::Test.Vector2",
    marshaller_type = "global::Test.Vector2")]] Marshaller<Test::Vector2>
{
    using cpp_type = Test::Vector2;
    using abi_type = Test::Vector2;
};
