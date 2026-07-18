#pragma once

#include <array>
#include <cstddef>
#include <exception>
#include <functional>
#include <iterator>
#include <list>
#include <map>
#include <memory>
#include <stdexcept>
#include <string>
#include <string_view>
#include <type_traits>
#include <unordered_map>
#include <utility>
#include <vector>

namespace SharpCpp
{
struct ManagedCallbackHandleAbi
{
    void* state{};
    void* callback{};
};

struct NativeArrayAbi
{
    void* handle{};
    std::size_t length{};
};

template <class T, std::size_t N>
struct NativeArray {};

namespace detail
{
template <class T>
using remove_cvref_t = std::remove_cv_t<std::remove_reference_t<T>>;

template <class T, class = void>
struct HasSync : std::false_type {};

template <class T>
struct HasSync<T, std::void_t<decltype(std::declval<T&>().Sync())>> : std::true_type {};

template <class T>
inline constexpr bool HasSyncValue = HasSync<T>::value;

template <class TMarshaller>
struct SyncOnDestroyMarshaller : TMarshaller
{
    using TMarshaller::TMarshaller;

    ~SyncOnDestroyMarshaller() noexcept
    {
        if constexpr (HasSyncValue<TMarshaller>)
        {
            this->Sync();
        }
    }
};

template <class T>
inline constexpr bool DefaultPassesByValue =
    !std::is_reference_v<T> &&
    (std::is_scalar_v<remove_cvref_t<T>> || std::is_enum_v<remove_cvref_t<T>>);

template <class T>
using DefaultPointerAbi = std::conditional_t<
    std::is_const_v<std::remove_reference_t<T>>,
    std::add_pointer_t<std::add_const_t<remove_cvref_t<T>>>,
    std::add_pointer_t<remove_cvref_t<T>>>;

template <class T>
using DefaultAbi = std::conditional_t<DefaultPassesByValue<T>, T, DefaultPointerAbi<T>>;

template <class T, bool PassesByValue = DefaultPassesByValue<T>>
struct DefaultMarshallerBase;

template <class T>
struct DefaultMarshallerBase<T, true>
{
    using is_default_marshaller = std::true_type;
    using cpp_type = T;
    using abi_type = T;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <class T>
struct DefaultMarshallerBase<T, false>
{
    using is_default_marshaller = std::true_type;
    using cpp_type = T;
    using abi_type = DefaultPointerAbi<T>;
    using value_type = remove_cvref_t<T>;

    constexpr abi_type ConvertToAbi(value_type& value) noexcept
    {
        return std::addressof(value);
    }

    constexpr abi_type ConvertToAbi(const value_type& value) noexcept
    {
        return const_cast<value_type*>(std::addressof(value));
    }

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        if (value == nullptr)
        {
            // SharpCpp complex value ABI cannot be null.
            std::terminate();
        }

        return *value;
    }
};

template <class T, bool PassesByValue = DefaultPassesByValue<T>>
struct DefaultReturnMarshallerBase;

template <class T>
struct DefaultReturnMarshallerBase<T, true> : DefaultMarshallerBase<T, true>
{
};

template <class T>
struct DefaultReturnMarshallerBase<T, false>
{
    using cpp_type = T;
    using value_type = remove_cvref_t<T>;
    using abi_type = DefaultPointerAbi<T>;

    abi_type ConvertToAbi(cpp_type value) noexcept
    {
        if constexpr (std::is_lvalue_reference_v<T>)
        {
            return std::addressof(value);
        }
        else
        {
            return new value_type(std::move(value));
        }
    }

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        if (value == nullptr)
        {
            // SharpCpp complex return ABI cannot be null.
            std::terminate();
        }

        return *value;
    }
};

template <typename TFunction, typename TSignature>
TFunction CreateManagedCallbackWrapper(ManagedCallbackHandleAbi handle);
}

template <class TString>
struct OwnedStringMarshallerBase
{
    using cpp_type = TString;
    using abi_type = TString*;

    constexpr abi_type ConvertToAbi(cpp_type& value) noexcept
    {
        return &value;
    }

    constexpr cpp_type& ConvertToCpp(abi_type value) noexcept
    {
        return *value;
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static std::size_t GetCountCore(const cpp_type& self)
    {
        return self.size();
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static const char* GetDataCore(const cpp_type& self)
    {
        return self.data();
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static void AddCore(cpp_type& self, unsigned char value)
    {
        self.push_back(static_cast<char>(value));
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static void ClearCore(cpp_type& self)
    {
        self.clear();
    }
};

template <class TString>
struct OwnedStringConstReferenceMarshallerBase
{
    using cpp_type = const TString&;
    using abi_type = TString*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return const_cast<TString*>(&value);
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return *value;
    }
};

template <class TString>
struct OwnedStringReferenceMarshallerBase
{
    using cpp_type = TString&;
    using abi_type = TString*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return &value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return *value;
    }
};

template <class TString>
struct OwnedStringConstPointerMarshallerBase
{
    using cpp_type = const TString*;
    using abi_type = TString*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return const_cast<TString*>(value);
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <class TString>
struct OwnedStringPointerMarshallerBase
{
    using cpp_type = TString*;
    using abi_type = TString*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <class TString>
struct OwnedStringReturnMarshallerBase
{
    using cpp_type = TString;
    using abi_type = TString*;

    abi_type ConvertToAbi(const cpp_type& value) noexcept
    {
        return new TString(value);
    }

    abi_type ConvertToAbi(cpp_type&& value) noexcept
    {
        return new TString(std::move(value));
    }

    cpp_type& ConvertToCpp(abi_type value) noexcept
    {
        return *value;
    }
};

template <class TValue>
struct OwnedHandleMarshallerBase
{
    using cpp_type = TValue;
    using abi_type = TValue*;

    template <class TSource>
    abi_type ConvertToAbi(TSource&& value) noexcept
    {
        return new TValue(std::forward<TSource>(value));
    }

    template <class TCpp = cpp_type>
    TCpp ConvertToCpp(abi_type value) noexcept
    {
        if constexpr (std::is_copy_constructible_v<TCpp>)
        {
            return *value;
        }

        return std::move(*value);
    }
};

template <class TValue>
struct OwnedHandleReferenceMarshallerBase
{
    using cpp_type = TValue&;
    using abi_type = TValue*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return &value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return *value;
    }
};

template <class TValue>
struct OwnedHandleConstReferenceMarshallerBase
{
    using cpp_type = const TValue&;
    using abi_type = TValue*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return const_cast<TValue*>(&value);
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return *value;
    }
};

template <class TValue>
struct OwnedHandlePointerMarshallerBase
{
    using cpp_type = TValue*;
    using abi_type = TValue*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <class TValue>
struct OwnedHandleConstPointerMarshallerBase
{
    using cpp_type = const TValue*;
    using abi_type = TValue*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return const_cast<TValue*>(value);
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <class TValue>
struct OwnedHandleReturnMarshallerBase : OwnedHandleMarshallerBase<TValue>
{
};

template <class TFunction, class TSignature>
struct ManagedCallbackMarshallerBase
{
    using cpp_type = TFunction;
    using abi_type = ManagedCallbackHandleAbi;

    template <class T = cpp_type>
    abi_type ConvertToAbi(const T&) noexcept
    {
        static_assert(!std::is_same_v<T, T>,
            "SharpCpp does not support native-to-managed callback function-object returns in this slice.");
        return {};
    }

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return detail::CreateManagedCallbackWrapper<cpp_type, TSignature>(value);
    }
};

template <class TFunction, class TSignature>
struct ManagedCallbackConstReferenceMarshallerBase
{
    using cpp_type = const TFunction&;
    using abi_type = ManagedCallbackHandleAbi;

    template <class T = cpp_type>
    abi_type ConvertToAbi(T) noexcept
    {
        static_assert(!std::is_same_v<T, T>,
            "SharpCpp does not support native-to-managed callback function-object returns in this slice.");
        return {};
    }

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        storage_ = detail::CreateManagedCallbackWrapper<TFunction, TSignature>(value);
        return storage_;
    }

private:
    mutable TFunction storage_{};
};

template <class TStringView>
struct StringViewMarshallerBase
{
    using cpp_type = TStringView;
    struct abi_type
    {
        const char* data{};
        std::size_t size{};
    };

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return { value.data(), value.size() };
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value.data == nullptr ? cpp_type{} : cpp_type(value.data, value.size);
    }
};

template <class TStringView>
struct StringViewConstReferenceMarshallerBase
{
    using cpp_type = const TStringView&;
    using abi_type = typename StringViewMarshallerBase<TStringView>::abi_type;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return { value.data(), value.size() };
    }

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        storage_ = value.data == nullptr ? TStringView{} : TStringView(value.data, value.size);
        return storage_;
    }

private:
    mutable TStringView storage_{};
};

template <class TCollection, class T>
struct SequenceCollectionMarshallerBase : OwnedHandleMarshallerBase<TCollection>
{
    using cpp_type = TCollection;
    using abi_type = typename OwnedHandleMarshallerBase<TCollection>::abi_type;

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false, disambiguate = false)]]
    static unsigned long long GetCountCore(const cpp_type& self)
    {
        return self.size();
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false, disambiguate = false)]]
    static void ClearCore(cpp_type& self)
    {
        self.clear();
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static T GetAtCore(const cpp_type& self, unsigned long long index)
    {
        return self[static_cast<std::size_t>(index)];
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static void SetAtCore(cpp_type& self, unsigned long long index, const T& value)
    {
        self[static_cast<std::size_t>(index)] = value;
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static void AddCore(cpp_type& self, const T& value)
    {
        self.push_back(value);
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static void InsertAtCore(cpp_type& self, unsigned long long index, const T& value)
    {
        self.insert(self.begin() + static_cast<std::size_t>(index), value);
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static void RemoveAtCore(cpp_type& self, unsigned long long index)
    {
        self.erase(self.begin() + static_cast<std::size_t>(index));
    }
};

template <class TList, class T>
struct ListCollectionMarshallerBase : OwnedHandleMarshallerBase<TList>
{
    using cpp_type = TList;
    using abi_type = typename OwnedHandleMarshallerBase<TList>::abi_type;

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false, disambiguate = false)]]
    static unsigned long long GetCountCore(const cpp_type& self)
    {
        return self.size();
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false, disambiguate = false)]]
    static void ClearCore(cpp_type& self)
    {
        self.clear();
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static T GetAtCore(const cpp_type& self, unsigned long long index)
    {
        return *std::next(self.begin(), static_cast<typename cpp_type::difference_type>(index));
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static void SetAtCore(cpp_type& self, unsigned long long index, const T& value)
    {
        *std::next(self.begin(), static_cast<typename cpp_type::difference_type>(index)) = value;
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static void AddCore(cpp_type& self, const T& value)
    {
        self.push_back(value);
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static void InsertAtCore(cpp_type& self, unsigned long long index, const T& value)
    {
        self.insert(std::next(self.begin(), static_cast<typename cpp_type::difference_type>(index)), value);
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static void RemoveAtCore(cpp_type& self, unsigned long long index)
    {
        self.erase(std::next(self.begin(), static_cast<typename cpp_type::difference_type>(index)));
    }
};

template <class TMap, class TKey, class TValue>
struct MapCollectionMarshallerBase : OwnedHandleMarshallerBase<TMap>
{
    using cpp_type = TMap;
    using abi_type = typename OwnedHandleMarshallerBase<TMap>::abi_type;

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false, disambiguate = false)]]
    static unsigned long long GetCountCore(const cpp_type& self)
    {
        return self.size();
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false, disambiguate = false)]]
    static void ClearCore(cpp_type& self)
    {
        self.clear();
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static bool ContainsKeyCore(const cpp_type& self, const TKey& key)
    {
        return self.find(key) != self.end();
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static TValue GetValueCore(const cpp_type& self, const TKey& key)
    {
        return self.find(key)->second;
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static void SetValueCore(cpp_type& self, const TKey& key, const TValue& value)
    {
        self[key] = value;
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static bool AddCore(cpp_type& self, const TKey& key, const TValue& value)
    {
        return self.emplace(key, value).second;
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static bool RemoveCore(cpp_type& self, const TKey& key)
    {
        return self.erase(key) != 0;
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static TKey GetKeyAtCore(const cpp_type& self, unsigned long long index)
    {
        return std::next(self.begin(), static_cast<typename cpp_type::difference_type>(index))->first;
    }

    [[sharpcpp::expose_member(shape = "protected_override", exposed = false)]]
    static TValue GetValueAtCore(const cpp_type& self, unsigned long long index)
    {
        return std::next(self.begin(), static_cast<typename cpp_type::difference_type>(index))->second;
    }
};

template <class TArray, std::size_t N>
struct FixedArrayReferenceMarshallerBase
{
    using cpp_type = TArray&;
    using abi_type = NativeArrayAbi;

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        Validate(value);
        return *static_cast<TArray*>(value.handle);
    }

    abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return { &value, N };
    }

    static void Validate(abi_type value) noexcept
    {
        if (value.handle == nullptr || value.length != N)
        {
            // SharpCpp fixed array length mismatch.
            std::terminate();
        }
    }
};

template <class TArray, std::size_t N>
struct FixedArrayConstReferenceMarshallerBase
{
    using cpp_type = const TArray&;
    using abi_type = NativeArrayAbi;

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        Validate(value);
        return *static_cast<const TArray*>(value.handle);
    }

    abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return { const_cast<TArray*>(&value), N };
    }

    static void Validate(abi_type value) noexcept
    {
        if (value.handle == nullptr || value.length != N)
        {
            // SharpCpp fixed array length mismatch.
            std::terminate();
        }
    }
};

template <class TArray, std::size_t N>
struct FixedArrayValueMarshallerBase
{
    using cpp_type = TArray;
    using abi_type = NativeArrayAbi;

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        FixedArrayConstReferenceMarshallerBase<TArray, N>::Validate(value);
        return *static_cast<const TArray*>(value.handle);
    }

    abi_type ConvertToAbi(cpp_type& value) noexcept
    {
        return { &value, N };
    }

    abi_type ConvertToAbi(const cpp_type& value) noexcept
    {
        return { const_cast<TArray*>(&value), N };
    }
};

template <class TArray, std::size_t N>
struct FixedArrayPointerMarshallerBase
{
    using cpp_type = TArray*;
    using abi_type = NativeArrayAbi;

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        if (value.handle == nullptr)
        {
            return nullptr;
        }

        if (value.length != N)
        {
            // SharpCpp fixed array length mismatch.
            std::terminate();
        }

        return static_cast<TArray*>(value.handle);
    }

    abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return { value, value == nullptr ? 0 : N };
    }
};

template <class TArray, std::size_t N>
struct FixedArrayConstPointerMarshallerBase
{
    using cpp_type = const TArray*;
    using abi_type = NativeArrayAbi;

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        if (value.handle == nullptr)
        {
            return nullptr;
        }

        if (value.length != N)
        {
            // SharpCpp fixed array length mismatch.
            std::terminate();
        }

        return static_cast<const TArray*>(value.handle);
    }

    abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return { const_cast<TArray*>(value), value == nullptr ? 0 : N };
    }
};

template <class TArray, std::size_t N>
struct FixedArrayReturnMarshallerBase
{
    using cpp_type = TArray;
    using abi_type = NativeArrayAbi;

    abi_type ConvertToAbi(const cpp_type& value) noexcept
    {
        return { new TArray(value), N };
    }

    abi_type ConvertToAbi(cpp_type&& value) noexcept
    {
        return { new TArray(std::move(value)), N };
    }

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        FixedArrayConstReferenceMarshallerBase<TArray, N>::Validate(value);
        return *static_cast<const TArray*>(value.handle);
    }
};

template <class TElement, std::size_t N>
struct NativeFixedArrayMarshallerBase
{
    using cpp_type = TElement[N];
    using abi_type = NativeArrayAbi;

    cpp_type& ConvertToCpp(abi_type value) noexcept
    {
        Validate(value);
        return *reinterpret_cast<TElement (*)[N]>(value.handle);
    }

    abi_type ConvertToAbi(cpp_type& value) noexcept
    {
        return { value, N };
    }

    abi_type ConvertToAbi(const cpp_type& value) noexcept
    {
        return { const_cast<TElement*>(value), N };
    }

    static void Validate(abi_type value) noexcept
    {
        if (value.handle == nullptr || value.length != N)
        {
            // SharpCpp fixed array length mismatch.
            std::terminate();
        }
    }
};

template <class T>
struct Marshaller : detail::DefaultMarshallerBase<T> {};

template <class TResult, class... TArgs>
struct Marshaller<TResult(TArgs...)>
{
    using cpp_type = TResult (*)(TArgs...);
    using abi_type = cpp_type;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <class TResult, class... TArgs>
struct Marshaller<TResult (*)(TArgs...)>
{
    using cpp_type = TResult (*)(TArgs...);
    using abi_type = cpp_type;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <>
struct Marshaller<void>
{
    using cpp_type = void;
    using abi_type = void;
};

template <class TSignature>
struct [[sharpcpp::managed_type(callback = true)]] Marshaller<std::function<TSignature>>
    : ManagedCallbackMarshallerBase<std::function<TSignature>, TSignature>
{
};

template <class TSignature>
struct Marshaller<const std::function<TSignature>>
    : ManagedCallbackMarshallerBase<std::function<TSignature>, TSignature>
{
};

template <class TSignature>
struct Marshaller<std::function<TSignature>&&>
    : ManagedCallbackMarshallerBase<std::function<TSignature>, TSignature>
{
};

template <class TSignature>
struct Marshaller<const std::function<TSignature>&>
    : ManagedCallbackConstReferenceMarshallerBase<std::function<TSignature>, TSignature>
{
};

template <>
struct [[sharpcpp::managed_type(public_type = "global::System.IntPtr")]] Marshaller<void*>
{
    using cpp_type = void*;
    using abi_type = void*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <>
struct [[sharpcpp::managed_type(public_type = "global::System.IntPtr")]] Marshaller<const void*>
{
    using cpp_type = const void*;
    using abi_type = const void*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <>
struct [[sharpcpp::managed_type(public_type = "global::System.IntPtr")]] Marshaller<signed char*>
{
    using cpp_type = signed char*;
    using abi_type = signed char*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <>
struct [[sharpcpp::managed_type(public_type = "global::System.IntPtr")]] Marshaller<const signed char*>
{
    using cpp_type = const signed char*;
    using abi_type = const signed char*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <>
struct [[sharpcpp::managed_type(public_type = "global::System.IntPtr")]] Marshaller<volatile signed char*>
{
    using cpp_type = volatile signed char*;
    using abi_type = volatile signed char*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <>
struct [[sharpcpp::managed_type(public_type = "global::System.IntPtr")]] Marshaller<const volatile signed char*>
{
    using cpp_type = const volatile signed char*;
    using abi_type = const volatile signed char*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <>
struct [[sharpcpp::managed_type(public_type = "global::System.IntPtr")]] Marshaller<unsigned char*>
{
    using cpp_type = unsigned char*;
    using abi_type = unsigned char*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <>
struct [[sharpcpp::managed_type(public_type = "global::System.IntPtr")]] Marshaller<const unsigned char*>
{
    using cpp_type = const unsigned char*;
    using abi_type = const unsigned char*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <>
struct [[sharpcpp::managed_type(public_type = "global::System.IntPtr")]] Marshaller<volatile unsigned char*>
{
    using cpp_type = volatile unsigned char*;
    using abi_type = volatile unsigned char*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <>
struct [[sharpcpp::managed_type(public_type = "global::System.IntPtr")]] Marshaller<const volatile unsigned char*>
{
    using cpp_type = const volatile unsigned char*;
    using abi_type = const volatile unsigned char*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <>
struct Marshaller<const signed char*&>
{
    using cpp_type = const signed char*&;
    using abi_type = const signed char**;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        storage_ = value;
        return &storage_;
    }

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        abi_argument_ = value;
        storage_ = *value;
        return storage_;
    }

    void Sync() noexcept
    {
        if (abi_argument_)
            *abi_argument_ = storage_;
    }

private:
    mutable const signed char* storage_{};
    mutable abi_type abi_argument_{};
};

template <>
struct Marshaller<const unsigned char*&>
{
    using cpp_type = const unsigned char*&;
    using abi_type = const unsigned char**;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        storage_ = value;
        return &storage_;
    }

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        abi_argument_ = value;
        storage_ = *value;
        return storage_;
    }

    void Sync() noexcept
    {
        if (abi_argument_)
            *abi_argument_ = storage_;
    }

private:
    mutable const unsigned char* storage_{};
    mutable abi_type abi_argument_{};
};

template <class T>
struct Marshaller<T&>
{
    using cpp_type = T&;
    using value_abi_type = typename Marshaller<T>::abi_type;
    using abi_type = std::conditional_t<
        std::is_pointer_v<T>,
        value_abi_type*,
        std::conditional_t<std::is_pointer_v<value_abi_type>, value_abi_type, value_abi_type*>>;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return reinterpret_cast<abi_type>(std::addressof(value));
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return *reinterpret_cast<T*>(value);
    }
};

template <class T>
struct Marshaller<const T&>
{
    using cpp_type = const T&;
    using value_abi_type = typename Marshaller<T>::abi_type;
    using abi_type = std::conditional_t<
        std::is_pointer_v<T>,
        std::add_pointer_t<std::add_pointer_t<std::add_const_t<std::remove_pointer_t<value_abi_type>>>>,
        std::conditional_t<
            std::is_pointer_v<value_abi_type>,
            std::add_pointer_t<std::add_const_t<std::remove_pointer_t<value_abi_type>>>,
            std::add_pointer_t<std::add_const_t<value_abi_type>>>>;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return reinterpret_cast<abi_type>(const_cast<std::remove_reference_t<cpp_type>*>(std::addressof(value)));
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return *reinterpret_cast<const T*>(value);
    }
};

template <class T>
    requires (!std::is_fundamental_v<T> && !std::is_enum_v<T> && !std::is_pointer_v<T>)
struct Marshaller<const T>
{
    using cpp_type = const T;
    using reference_marshaller_type = Marshaller<const T&>;
    using abi_type = typename reference_marshaller_type::abi_type;

    abi_type ConvertToAbi(const cpp_type& value) noexcept
    {
        return reference_marshaller_type{}.ConvertToAbi(value);
    }

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return reference_marshaller_type{}.ConvertToCpp(value);
    }
};

template <class T>
struct Marshaller<T*>
{
    using cpp_type = T*;
    using value_abi_type = typename Marshaller<T>::abi_type;
    using abi_type = std::conditional_t<
        std::is_pointer_v<T>,
        value_abi_type*,
        std::conditional_t<std::is_pointer_v<value_abi_type>, value_abi_type, value_abi_type*>>;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return reinterpret_cast<abi_type>(value);
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return reinterpret_cast<cpp_type>(value);
    }

};

template <class T>
struct Marshaller<const T*>
{
    using cpp_type = const T*;
    using value_abi_type = typename Marshaller<T>::abi_type;
    using abi_type = std::conditional_t<
        std::is_pointer_v<value_abi_type>,
        std::add_pointer_t<std::add_const_t<std::remove_pointer_t<value_abi_type>>>,
        std::add_pointer_t<std::add_const_t<value_abi_type>>>;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return reinterpret_cast<abi_type>(value);
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return reinterpret_cast<cpp_type>(value);
    }
};

template <class T>
    requires (!std::is_fundamental_v<T> && !std::is_void_v<T>)
struct Marshaller<const T*&>
{
    using cpp_type = const T*&;
    using pointer_marshaller_type = Marshaller<const T*>;
    using abi_pointer_type = typename pointer_marshaller_type::abi_type;
    using abi_type = abi_pointer_type*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        abi_storage_ = pointer_marshaller_type{}.ConvertToAbi(value);
        return &abi_storage_;
    }

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        abi_argument_ = value;
        storage_ = pointer_marshaller_type{}.ConvertToCpp(*value);
        return storage_;
    }

    void Sync() noexcept
    {
        if (abi_argument_)
            *abi_argument_ = pointer_marshaller_type{}.ConvertToAbi(storage_);
    }

private:
    mutable const T* storage_{};
    mutable abi_pointer_type abi_storage_{};
    mutable abi_type abi_argument_{};
};

template <>
struct [[sharpcpp::managed_type(public_type = "global::System.ReadOnlySpan<byte>", undefaultable = true)]] Marshaller<const char*>
{
    using cpp_type = const char*;
    using abi_type = const char*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <>
struct Marshaller<const char*&>
{
    using cpp_type = const char*&;
    using abi_type = const char*;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    cpp_type ConvertToCpp(abi_type value) noexcept
    {
        storage_ = value;
        return storage_;
    }

private:
    mutable const char* storage_{};
};

template <>
struct [[sharpcpp::managed_type(public_type = "string")]] Marshaller<const char**>
{
    using cpp_type = const char**;
    using abi_type = const char**;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <>
struct [[sharpcpp::managed_type(public_type = "string")]] Marshaller<char**>
{
    using cpp_type = char**;
    using abi_type = char**;

    constexpr abi_type ConvertToAbi(cpp_type value) noexcept
    {
        return value;
    }

    constexpr cpp_type ConvertToCpp(abi_type value) noexcept
    {
        return value;
    }
};

template <>
struct [[sharpcpp::managed_type(
    managed_base_type = "global::SharpCpp.Support.StringBase",
    public_type = "Std.String",
    managed_namespace = "Std"
)]]
Marshaller<std::basic_string<char> > : OwnedStringMarshallerBase<std::basic_string<char>> {};

template <>
struct Marshaller<std::string&> : OwnedStringReferenceMarshallerBase<std::string> {};

template <>
struct Marshaller<const std::string&> : OwnedStringConstReferenceMarshallerBase<std::string> {};

template <>
struct Marshaller<const std::string*> : OwnedStringConstPointerMarshallerBase<std::string> {};

template <>
struct Marshaller<std::string*> : OwnedStringPointerMarshallerBase<std::string> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::System.ReadOnlySpan<byte>",
    managed_marshaller = "global::SharpCpp.Support.Utf8SpanAbiMarshaller",
    undefaultable = true
)]]
Marshaller<std::string_view> : StringViewMarshallerBase<std::string_view> {};

template <>
struct Marshaller<const std::string_view&> : StringViewConstReferenceMarshallerBase<std::string_view> {};

template <typename T, typename TAllocator>
struct [[sharpcpp::managed_type(
    public_type = "Std.Vector<{0}>",
    managed_base_type = "global::SharpCpp.Support.VectorBase<{0}>",
    managed_namespace = "Std"
)]]
Marshaller<std::vector<T, TAllocator>> : SequenceCollectionMarshallerBase<std::vector<T, TAllocator>, T> {};

template <typename T, typename TAllocator>
struct Marshaller<std::vector<T, TAllocator>&> : OwnedHandleReferenceMarshallerBase<std::vector<T, TAllocator>> {};

template <typename T, typename TAllocator>
struct Marshaller<const std::vector<T, TAllocator>&> : OwnedHandleConstReferenceMarshallerBase<std::vector<T, TAllocator>> {};

template <typename T, typename TAllocator>
struct Marshaller<std::vector<T, TAllocator>*> : OwnedHandlePointerMarshallerBase<std::vector<T, TAllocator>> {};

template <typename T, typename TAllocator>
struct Marshaller<const std::vector<T, TAllocator>*> : OwnedHandleConstPointerMarshallerBase<std::vector<T, TAllocator>> {};

template <typename T, std::size_t N>
struct [[sharpcpp::managed_type(
    public_type = "Std.Array<{0}>",
    managed_base_type = "global::SharpCpp.Support.ArrayBase<{0}>",
    managed_namespace = "Std",
    fixed_array = true
)]]
Marshaller<std::array<T, N>> : FixedArrayValueMarshallerBase<std::array<T, N>, N> {};

template <typename T, std::size_t N>
struct [[sharpcpp::managed_type(
    public_type = "SharpCpp.NativeArray<{0}>",
    managed_base_type = "global::SharpCpp.Support.ArrayBase<{0}>",
    managed_namespace = "SharpCpp",
    fixed_array = true,
    native_array = true
)]]
Marshaller<NativeArray<T, N>> {};

template <typename T, std::size_t N>
struct Marshaller<std::array<T, N>&> : FixedArrayReferenceMarshallerBase<std::array<T, N>, N> {};

template <typename T, std::size_t N>
struct Marshaller<const std::array<T, N>&> : FixedArrayConstReferenceMarshallerBase<std::array<T, N>, N> {};

template <typename T, std::size_t N>
struct Marshaller<std::array<T, N>*> : FixedArrayPointerMarshallerBase<std::array<T, N>, N> {};

template <typename T, std::size_t N>
struct Marshaller<const std::array<T, N>*> : FixedArrayConstPointerMarshallerBase<std::array<T, N>, N> {};

template <typename T, std::size_t N>
struct Marshaller<T[N]> : NativeFixedArrayMarshallerBase<T, N> {};

template <typename T, typename TAllocator>
struct [[sharpcpp::managed_type(
    public_type = "Std.List<{0}>",
    managed_base_type = "global::SharpCpp.Support.ListBase<{0}>",
    managed_namespace = "Std"
)]]
Marshaller<std::list<T, TAllocator>> : ListCollectionMarshallerBase<std::list<T, TAllocator>, T> {};

template <typename T, typename TAllocator>
struct Marshaller<std::list<T, TAllocator>&> : OwnedHandleReferenceMarshallerBase<std::list<T, TAllocator>> {};

template <typename T, typename TAllocator>
struct Marshaller<const std::list<T, TAllocator>&> : OwnedHandleConstReferenceMarshallerBase<std::list<T, TAllocator>> {};

template <typename T, typename TAllocator>
struct Marshaller<std::list<T, TAllocator>*> : OwnedHandlePointerMarshallerBase<std::list<T, TAllocator>> {};

template <typename T, typename TAllocator>
struct Marshaller<const std::list<T, TAllocator>*> : OwnedHandleConstPointerMarshallerBase<std::list<T, TAllocator>> {};

template <typename TKey, typename TValue, typename TCompare, typename TAllocator>
struct [[sharpcpp::managed_type(
    public_type = "Std.Map<{0}, {1}>",
    managed_base_type = "global::SharpCpp.Support.MapBase<{0}, {1}>",
    managed_base_type_constraints = "where {0} : notnull",
    managed_namespace = "Std"
)]]
Marshaller<std::map<TKey, TValue, TCompare, TAllocator>> : MapCollectionMarshallerBase<std::map<TKey, TValue, TCompare, TAllocator>, TKey, TValue> {};

template <typename TKey, typename TValue, typename TCompare, typename TAllocator>
struct Marshaller<std::map<TKey, TValue, TCompare, TAllocator>&> : OwnedHandleReferenceMarshallerBase<std::map<TKey, TValue, TCompare, TAllocator>> {};

template <typename TKey, typename TValue, typename TCompare, typename TAllocator>
struct Marshaller<const std::map<TKey, TValue, TCompare, TAllocator>&> : OwnedHandleConstReferenceMarshallerBase<std::map<TKey, TValue, TCompare, TAllocator>> {};

template <typename TKey, typename TValue, typename TCompare, typename TAllocator>
struct Marshaller<std::map<TKey, TValue, TCompare, TAllocator>*> : OwnedHandlePointerMarshallerBase<std::map<TKey, TValue, TCompare, TAllocator>> {};

template <typename TKey, typename TValue, typename TCompare, typename TAllocator>
struct Marshaller<const std::map<TKey, TValue, TCompare, TAllocator>*> : OwnedHandleConstPointerMarshallerBase<std::map<TKey, TValue, TCompare, TAllocator>> {};

template <typename TKey, typename TValue, typename THash, typename TKeyEqual, typename TAllocator>
struct [[sharpcpp::managed_type(
    public_type = "Std.UnorderedMap<{0}, {1}>",
    managed_base_type = "global::SharpCpp.Support.UnorderedMapBase<{0}, {1}>",
    managed_base_type_constraints = "where {0} : notnull",
    managed_namespace = "Std"
)]]
Marshaller<std::unordered_map<TKey, TValue, THash, TKeyEqual, TAllocator>> : MapCollectionMarshallerBase<std::unordered_map<TKey, TValue, THash, TKeyEqual, TAllocator>, TKey, TValue> {};

template <typename TKey, typename TValue, typename THash, typename TKeyEqual, typename TAllocator>
struct Marshaller<std::unordered_map<TKey, TValue, THash, TKeyEqual, TAllocator>&> : OwnedHandleReferenceMarshallerBase<std::unordered_map<TKey, TValue, THash, TKeyEqual, TAllocator>> {};

template <typename TKey, typename TValue, typename THash, typename TKeyEqual, typename TAllocator>
struct Marshaller<const std::unordered_map<TKey, TValue, THash, TKeyEqual, TAllocator>&> : OwnedHandleConstReferenceMarshallerBase<std::unordered_map<TKey, TValue, THash, TKeyEqual, TAllocator>> {};

template <typename TKey, typename TValue, typename THash, typename TKeyEqual, typename TAllocator>
struct Marshaller<std::unordered_map<TKey, TValue, THash, TKeyEqual, TAllocator>*> : OwnedHandlePointerMarshallerBase<std::unordered_map<TKey, TValue, THash, TKeyEqual, TAllocator>> {};

template <typename TKey, typename TValue, typename THash, typename TKeyEqual, typename TAllocator>
struct Marshaller<const std::unordered_map<TKey, TValue, THash, TKeyEqual, TAllocator>*> : OwnedHandleConstPointerMarshallerBase<std::unordered_map<TKey, TValue, THash, TKeyEqual, TAllocator>> {};

template <class T>
using arg_t = typename Marshaller<T>::abi_type;

template <class T>
using field_t = T*;

template <class T>
using ParameterMarshaller = detail::SyncOnDestroyMarshaller<Marshaller<T>>;

namespace detail
{
template <class T, class = void>
struct IsDefaultMarshaller : std::false_type {};

template <class T>
struct IsDefaultMarshaller<T, std::void_t<typename Marshaller<T>::is_default_marshaller>> : std::true_type {};

template <class T>
using ConfiguredReturnMarshallerBase = std::conditional_t<
    IsDefaultMarshaller<remove_cvref_t<T>>::value,
    DefaultReturnMarshallerBase<T>,
    std::conditional_t<
        std::is_lvalue_reference_v<T>,
        Marshaller<T>,
        Marshaller<std::remove_cv_t<T>>>>;
}

template <class T>
struct ReturnMarshaller : detail::ConfiguredReturnMarshallerBase<T> {};

template <>
struct ReturnMarshaller<void> : Marshaller<void> {};

template <>
struct ReturnMarshaller<std::basic_string<char> > : OwnedStringReturnMarshallerBase<std::basic_string<char>> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::System.ReadOnlySpan<byte>",
    managed_marshaller = "global::SharpCpp.Support.Utf8SpanAbiReturnMarshaller"
)]] ReturnMarshaller<std::string_view> : Marshaller<std::string_view> {};

template <typename T, typename TAllocator>
struct ReturnMarshaller<std::vector<T, TAllocator>> : OwnedHandleReturnMarshallerBase<std::vector<T, TAllocator>> {};

template <typename T, std::size_t N>
struct ReturnMarshaller<std::array<T, N>> : FixedArrayReturnMarshallerBase<std::array<T, N>, N> {};

template <typename T, std::size_t N>
struct ReturnMarshaller<T[N]> : NativeFixedArrayMarshallerBase<T, N> {};

template <typename T, typename TAllocator>
struct ReturnMarshaller<std::list<T, TAllocator>> : OwnedHandleReturnMarshallerBase<std::list<T, TAllocator>> {};

template <typename TKey, typename TValue, typename TCompare, typename TAllocator>
struct ReturnMarshaller<std::map<TKey, TValue, TCompare, TAllocator>> : OwnedHandleReturnMarshallerBase<std::map<TKey, TValue, TCompare, TAllocator>> {};

template <typename TKey, typename TValue, typename THash, typename TKeyEqual, typename TAllocator>
struct ReturnMarshaller<std::unordered_map<TKey, TValue, THash, TKeyEqual, TAllocator>> : OwnedHandleReturnMarshallerBase<std::unordered_map<TKey, TValue, THash, TKeyEqual, TAllocator>> {};

template <>
struct [[sharpcpp::managed_type(
    public_type = "global::System.ReadOnlySpan<byte>",
    managed_marshaller = "global::SharpCpp.Support.Utf8SpanReturnMarshaller"
)]] ReturnMarshaller<const char*> : Marshaller<const char*> {};

namespace detail
{
struct DefaultMarshallerComplexSmoke
{
    int Value{};
};
}

namespace detail
{
template <typename T>
auto TryClone(T* instance, int) -> decltype(new T(*instance), (T*)nullptr)
{
    return new T(*instance);
}

template <typename T>
T* TryClone(T*, long)
{
    return nullptr;
}
}

template <class T>
using ret_t = typename ReturnMarshaller<T>::abi_type;

static_assert(std::is_same_v<arg_t<int>, int>);
static_assert(std::is_same_v<arg_t<int*>, int*>);
static_assert(std::is_same_v<arg_t<int**>, int**>);
static_assert(std::is_same_v<arg_t<detail::DefaultMarshallerComplexSmoke>, detail::DefaultMarshallerComplexSmoke*>);
static_assert(std::is_same_v<arg_t<detail::DefaultMarshallerComplexSmoke**>, detail::DefaultMarshallerComplexSmoke**>);
static_assert(std::is_same_v<arg_t<const detail::DefaultMarshallerComplexSmoke**>, const detail::DefaultMarshallerComplexSmoke**>);
static_assert(std::is_same_v<arg_t<const detail::DefaultMarshallerComplexSmoke&>, const detail::DefaultMarshallerComplexSmoke*>);
static_assert(std::is_same_v<ret_t<int>, int>);
static_assert(std::is_same_v<ret_t<detail::DefaultMarshallerComplexSmoke>, detail::DefaultMarshallerComplexSmoke*>);
}
