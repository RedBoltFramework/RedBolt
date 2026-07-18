#pragma once

#include <SharpCpp/Marshaller.hpp>
#include <atomic>
#include <exception>
#include <functional>
#include <memory>
#include <cstddef>
#include <tuple>
#include <type_traits>
#include <utility>

namespace SharpCpp
{
namespace detail
{
template <typename T, typename = void>
struct has_clear : std::false_type {};

template <typename T>
struct has_clear<T, std::void_t<decltype(std::declval<T&>().clear())>> : std::true_type {};

template <typename T>
inline constexpr bool has_clear_v = has_clear<T>::value;

template <typename T>
using remove_cvref_t = std::remove_cv_t<std::remove_reference_t<T>>;

template <typename TTuple>
struct MarshallerTupleForTuple;

template <typename... TCppArgs>
struct MarshallerTupleForTuple<std::tuple<TCppArgs...>>
{
    using type = std::tuple<ParameterMarshaller<TCppArgs>...>;
};

template <typename TTuple>
using marshaller_tuple_t = typename MarshallerTupleForTuple<TTuple>::type;

template <typename TCpp>
using callback_parameter_abi_t = typename ParameterMarshaller<TCpp>::abi_type;

template <typename TCpp>
using callback_return_abi_t = typename ReturnMarshaller<TCpp>::abi_type;

using managed_callback_release_pointer_t = void (*)(void*);

inline std::atomic<managed_callback_release_pointer_t> g_managed_callback_release{};

inline void SetManagedCallbackRelease(managed_callback_release_pointer_t release) noexcept
{
    g_managed_callback_release.store(release, std::memory_order_release);
}

inline void ReleaseManagedCallbackState(void* state)
{
    if (state == nullptr)
        return;

    const auto release = g_managed_callback_release.load(std::memory_order_acquire);
    if (release == nullptr)
    {
        std::terminate();
    }

    release(state);
}

template <typename TSignature>
struct ManagedCallbackInvokePointer;

template <typename TResult, typename... TArgs>
struct ManagedCallbackInvokePointer<TResult(TArgs...)>
{
    using type = callback_return_abi_t<TResult> (*)(void*, callback_parameter_abi_t<TArgs>...);
};

template <typename TResult, typename... TArgs>
struct ManagedCallbackInvokePointer<TResult(TArgs...) noexcept>
{
    using type = callback_return_abi_t<TResult> (*)(void*, callback_parameter_abi_t<TArgs>...);
};

template <typename TSignature>
using managed_callback_invoke_pointer_t = typename ManagedCallbackInvokePointer<TSignature>::type;

template <typename TCppArgumentTuple, typename TCallable, typename... TCppArgs, std::size_t... TIndices>
decltype(auto) InvokeCallbackWithExplicitSignatureImpl(
    TCallable&& callable,
    std::index_sequence<TIndices...>,
    TCppArgs&&... cppArguments)
{
    marshaller_tuple_t<TCppArgumentTuple> marshallers;
    auto invoke = [&]() -> decltype(auto)
    {
        return std::invoke(
            std::forward<TCallable>(callable),
            std::get<TIndices>(marshallers).ConvertToAbi(cppArguments)...);
    };

    return invoke();
}

template <typename TCppArgumentTuple, typename TCallable, typename... TCppArgs>
decltype(auto) InvokeCallbackWithExplicitSignature(TCallable&& callable, TCppArgs&&... cppArguments)
{
    return InvokeCallbackWithExplicitSignatureImpl<TCppArgumentTuple>(
        std::forward<TCallable>(callable),
        std::make_index_sequence<std::tuple_size_v<TCppArgumentTuple>>{},
        std::forward<TCppArgs>(cppArguments)...);
}

template <typename TFunction, typename TSignature>
struct ManagedCallbackWrapperFactory;

template <typename TFunction, typename TResult, typename... TArgs>
struct ManagedCallbackWrapperFactory<TFunction, TResult(TArgs...)>
{
    using invoke_pointer = managed_callback_invoke_pointer_t<TResult(TArgs...)>;
    using argument_tuple = std::tuple<TArgs...>;

    static TFunction Create(SharpCpp::ManagedCallbackHandleAbi handle)
    {
        if (handle.callback == nullptr)
        {
            ReleaseManagedCallbackState(handle.state);
            return TFunction{};
        }

        auto lifetime = std::shared_ptr<void>(handle.state, [](void* state)
        {
            ReleaseManagedCallbackState(state);
        });
        auto callback = reinterpret_cast<invoke_pointer>(handle.callback);

        return TFunction([callback, lifetime = std::move(lifetime)](TArgs... cppArguments) -> TResult
        {
            auto invoke = [callback, state = lifetime.get()](callback_parameter_abi_t<TArgs>... abiArguments) -> callback_return_abi_t<TResult>
            {
                return callback(state, abiArguments...);
            };

            if constexpr (std::is_void_v<TResult>)
            {
                InvokeCallbackWithExplicitSignature<argument_tuple>(
                    invoke,
                    std::forward<TArgs>(cppArguments)...);
                return;
            }
            else
            {
                auto result = InvokeCallbackWithExplicitSignature<argument_tuple>(
                    invoke,
                    std::forward<TArgs>(cppArguments)...);
                return ReturnMarshaller<TResult>{}.ConvertToCpp(result);
            }
        });
    }
};

template <typename TFunction, typename TResult, typename... TArgs>
struct ManagedCallbackWrapperFactory<TFunction, TResult(TArgs...) noexcept>
    : ManagedCallbackWrapperFactory<TFunction, TResult(TArgs...)>
{
};

template <typename TFunction, typename TSignature>
TFunction CreateManagedCallbackWrapper(SharpCpp::ManagedCallbackHandleAbi handle)
{
    return ManagedCallbackWrapperFactory<TFunction, remove_cvref_t<TSignature>>::Create(handle);
}

template <typename TResult, typename TCppArgumentTuple, typename TCallable, typename TMarshallerTuple, typename... TAbiArgs, std::size_t... TIndices>
constexpr decltype(auto) InvokeWithMarshallersImpl(
    TCallable&& callable,
    TMarshallerTuple& marshallers,
    std::index_sequence<TIndices...>,
    TAbiArgs&&... abiArguments)
{
    auto invoke_fn = [&]() -> TResult
    {
        return std::invoke(
            std::forward<TCallable>(callable),
            std::get<TIndices>(marshallers).ConvertToCpp(std::forward<TAbiArgs>(abiArguments))...);
    };

    if constexpr (std::is_void_v<TResult>)
    {
        invoke_fn();
        return;
    }
    else
    {
        ReturnMarshaller<TResult> ret_marshaller;
        decltype(auto) result = invoke_fn();
        return ret_marshaller.ConvertToAbi(result);
    }
}

template <typename TResult, typename TCppArgumentTuple, typename TCallable, typename TMarshallerTuple, typename... TAbiArgs, std::size_t... TIndices>
auto InvokeWithMarshallersOwnedImpl(
    TCallable&& callable,
    TMarshallerTuple& marshallers,
    std::index_sequence<TIndices...>,
    TAbiArgs&&... abiArguments) -> std::remove_cv_t<std::remove_reference_t<TResult>>*
{
    using owned_type = std::remove_cv_t<std::remove_reference_t<TResult>>;
    auto invoke_fn = [&]() -> decltype(auto)
    {
        return std::invoke(
            std::forward<TCallable>(callable),
            std::get<TIndices>(marshallers).ConvertToCpp(std::forward<TAbiArgs>(abiArguments))...);
    };

    if constexpr (std::is_lvalue_reference_v<TResult>)
    {
        auto* result = std::addressof(invoke_fn());
        return new owned_type(*result);
    }
    else
    {
        auto result = invoke_fn();
        return new owned_type(std::move(result));
    }
}

template <typename TResult, typename TCppArgumentTuple, typename TMethod, typename TObject, typename TMarshallerTuple, typename... TAbiArgs, std::size_t... TIndices>
constexpr decltype(auto) InvokeMemberWithMarshallersImpl(
    TMethod method,
    TObject&& instance,
    TMarshallerTuple& marshallers,
    std::index_sequence<TIndices...>,
    TAbiArgs&&... abiArguments)
{
    // Decay the method to a value type — the INVOKE specification
    // requires a pointer-to-member value, not a reference-to-member.
    // Also use direct member-access syntax (obj.*m)(args) instead of
    // std::invoke to avoid std::invoke_result deduction issues with
    // reference-returning member functions in template contexts.
    std::decay_t<TMethod> m = method;

    // Inlined invoke+sync — avoids lambda std::invoke_result issues.
    if constexpr (std::is_void_v<TResult>)
    {
        (std::forward<TObject>(instance).*m)(
            std::get<TIndices>(marshallers).ConvertToCpp(std::forward<TAbiArgs>(abiArguments))...);
        return;
    }
    else
    {
        ReturnMarshaller<TResult> ret_marshaller;
        if constexpr (std::is_lvalue_reference_v<TResult>)
        {
            auto* result = std::addressof(
                (std::forward<TObject>(instance).*m)(
                    std::get<TIndices>(marshallers).ConvertToCpp(std::forward<TAbiArgs>(abiArguments))...));
            return ret_marshaller.ConvertToAbi(static_cast<TResult>(*result));
        }
        else
        {
            auto result =
                (std::forward<TObject>(instance).*m)(
                    std::get<TIndices>(marshallers).ConvertToCpp(std::forward<TAbiArgs>(abiArguments))...);
            return ret_marshaller.ConvertToAbi(result);
        }
    }
}

template <typename TResult, typename TCppArgumentTuple, typename TMethod, typename TObject, typename TMarshallerTuple, typename... TAbiArgs, std::size_t... TIndices>
auto InvokeMemberWithMarshallersOwnedImpl(
    TMethod method,
    TObject&& instance,
    TMarshallerTuple& marshallers,
    std::index_sequence<TIndices...>,
    TAbiArgs&&... abiArguments) -> std::remove_cv_t<std::remove_reference_t<TResult>>*
{
    using owned_type = std::remove_cv_t<std::remove_reference_t<TResult>>;
    std::decay_t<TMethod> m = method;

    if constexpr (std::is_lvalue_reference_v<TResult>)
    {
        auto* result = std::addressof(
            (std::forward<TObject>(instance).*m)(
                std::get<TIndices>(marshallers).ConvertToCpp(std::forward<TAbiArgs>(abiArguments))...));
        return new owned_type(*result);
    }
    else
    {
        auto result =
            (std::forward<TObject>(instance).*m)(
                std::get<TIndices>(marshallers).ConvertToCpp(std::forward<TAbiArgs>(abiArguments))...);
        return new owned_type(std::move(result));
    }
}

template <typename FT>
struct FunctionTraits;

template <typename TResult, typename... TArgs>
struct FunctionTraits<TResult(TArgs...)>
{
    using return_type = TResult;
    using argument_tuple = std::tuple<TArgs...>;
};

template <typename TResult, typename... TArgs>
struct FunctionTraits<TResult(TArgs...) noexcept>
{
    using return_type = TResult;
    using argument_tuple = std::tuple<TArgs...>;
};

template <typename FT>
struct MemberFunctionTraits;

template <typename TResult, typename TSelf, typename... TArgs>
struct MemberFunctionTraits<TResult (TSelf::*)(TArgs...)>
{
    using return_type = TResult;
    using class_type = TSelf;
    using argument_tuple = std::tuple<TArgs...>;
};

template <typename TResult, typename TSelf, typename... TArgs>
struct MemberFunctionTraits<TResult (TSelf::*)(TArgs...) const>
{
    using return_type = TResult;
    using class_type = TSelf;
    using argument_tuple = std::tuple<TArgs...>;
};

template <typename TResult, typename TSelf, typename... TArgs>
struct MemberFunctionTraits<TResult (TSelf::*)(TArgs...) noexcept>
{
    using return_type = TResult;
    using class_type = TSelf;
    using argument_tuple = std::tuple<TArgs...>;
};

template <typename TResult, typename TSelf, typename... TArgs>
struct MemberFunctionTraits<TResult (TSelf::*)(TArgs...) const noexcept>
{
    using return_type = TResult;
    using class_type = TSelf;
    using argument_tuple = std::tuple<TArgs...>;
};

template <typename T, typename... TArgs, std::size_t... TIndices, typename... TAbiArgs>
auto ConstructImpl(std::index_sequence<TIndices...>, TAbiArgs&&... abiArguments) -> ret_t<T*>
{
    marshaller_tuple_t<std::tuple<TArgs...>> marshallers;
    ReturnMarshaller<T*> ret_marshaller;
    return ret_marshaller.ConvertToAbi(
        new T(std::get<TIndices>(marshallers).ConvertToCpp(std::forward<TAbiArgs>(abiArguments))...));
}

struct [[maybe_unused]] MarshallerInvokerImpl;
}

struct MarshallerInvoker
{
    template <typename FT, typename TCppArgumentTuple = typename detail::FunctionTraits<FT>::argument_tuple, typename... TAbiArgs>
    static decltype(auto) invoke_as(typename std::add_pointer_t<FT> callable, TAbiArgs&&... abiArguments)
    {
        using traits = detail::FunctionTraits<FT>;
        using result_type = typename traits::return_type;
        using signature_arg_tuple = typename traits::argument_tuple;
        using arg_tuple = TCppArgumentTuple;

        static_assert(
            std::tuple_size_v<arg_tuple> == std::tuple_size_v<signature_arg_tuple>,
            "SharpCpp marshaller argument tuple arity mismatch.");

        detail::marshaller_tuple_t<arg_tuple> marshallers;
        return detail::InvokeWithMarshallersImpl<result_type, arg_tuple>(
            callable,
            marshallers,
            std::make_index_sequence<std::tuple_size_v<arg_tuple>>{},
            std::forward<TAbiArgs>(abiArguments)...);
    }

    template <typename FT, typename TCppArgumentTuple = typename detail::FunctionTraits<FT>::argument_tuple, typename... TAbiArgs>
    static auto invoke_owned_as(typename std::add_pointer_t<FT> callable, TAbiArgs&&... abiArguments)
    {
        using traits = detail::FunctionTraits<FT>;
        using result_type = typename traits::return_type;
        using signature_arg_tuple = typename traits::argument_tuple;
        using arg_tuple = TCppArgumentTuple;

        static_assert(
            std::tuple_size_v<arg_tuple> == std::tuple_size_v<signature_arg_tuple>,
            "SharpCpp marshaller argument tuple arity mismatch.");

        detail::marshaller_tuple_t<arg_tuple> marshallers;
        return detail::InvokeWithMarshallersOwnedImpl<result_type, arg_tuple>(
            callable,
            marshallers,
            std::make_index_sequence<std::tuple_size_v<arg_tuple>>{},
            std::forward<TAbiArgs>(abiArguments)...);
    }

    template <typename FT, typename TCppArgumentTuple = typename detail::FunctionTraits<FT>::argument_tuple, typename TCallable, typename... TAbiArgs>
        requires(!std::is_same_v<std::remove_cvref_t<TCallable>, std::add_pointer_t<FT>>)
    static auto invoke_owned_as(TCallable&& callable, TAbiArgs&&... abiArguments)
    {
        if constexpr (std::is_member_function_pointer_v<std::remove_reference_t<TCallable>>)
        {
            using traits = detail::MemberFunctionTraits<std::remove_reference_t<TCallable>>;
            using result_type = typename traits::return_type;
            using class_type = typename traits::class_type;
            using signature_arg_tuple = typename traits::argument_tuple;
            using arg_tuple = TCppArgumentTuple;

            static_assert(
                std::tuple_size_v<arg_tuple> == std::tuple_size_v<signature_arg_tuple>,
                "SharpCpp marshaller argument tuple arity mismatch.");

            detail::marshaller_tuple_t<arg_tuple> marshallers;
            return detail::InvokeMemberWithMarshallersOwnedImpl<result_type, arg_tuple>(
                std::forward<TCallable>(callable),
                *static_cast<class_type*>(nullptr),
                marshallers,
                std::make_index_sequence<std::tuple_size_v<arg_tuple>>{},
                std::forward<TAbiArgs>(abiArguments)...);
        }
        else
        {
            using traits = detail::FunctionTraits<FT>;
            using result_type = typename traits::return_type;
            using signature_arg_tuple = typename traits::argument_tuple;
            using arg_tuple = TCppArgumentTuple;

            static_assert(
                std::tuple_size_v<arg_tuple> == std::tuple_size_v<signature_arg_tuple>,
                "SharpCpp marshaller argument tuple arity mismatch.");

            detail::marshaller_tuple_t<arg_tuple> marshallers;
            return detail::InvokeWithMarshallersOwnedImpl<result_type, arg_tuple>(
                std::forward<TCallable>(callable),
                marshallers,
                std::make_index_sequence<std::tuple_size_v<arg_tuple>>{},
                std::forward<TAbiArgs>(abiArguments)...);
        }
    }

    template <typename FT, typename TCppArgumentTuple = typename detail::FunctionTraits<FT>::argument_tuple, typename TCallable, typename... TAbiArgs>
        requires(!std::is_same_v<std::remove_cvref_t<TCallable>, std::add_pointer_t<FT>>)
    static decltype(auto) invoke_as(TCallable&& callable, TAbiArgs&&... abiArguments)
    {
        // Detect member function pointers and route to the member-path.
        // When libclang misreports a method as static, the generated code
        // passes a member function pointer to invoke_as. Without this branch,
        // InvokeWithMarshallersImpl would call std::invoke(member_ptr) with
        // no instance, producing a compile error.
        if constexpr (std::is_member_function_pointer_v<std::remove_reference_t<TCallable>>)
        {
            using traits = detail::MemberFunctionTraits<std::remove_reference_t<TCallable>>;
            using result_type = typename traits::return_type;
            using class_type = typename traits::class_type;
            using signature_arg_tuple = typename traits::argument_tuple;
            using arg_tuple = TCppArgumentTuple;

            static_assert(
                std::tuple_size_v<arg_tuple> == std::tuple_size_v<signature_arg_tuple>,
                "SharpCpp marshaller argument tuple arity mismatch.");

            detail::marshaller_tuple_t<arg_tuple> marshallers;
            // Use a nullptr instance — the member function doesn't access
            // this (it's actually a true static in spirit or has no data
            // dependencies). If the method is protected, the compiler will
            // reject this at the call site below anyway.
            return detail::InvokeMemberWithMarshallersImpl<result_type, arg_tuple>(
                std::forward<TCallable>(callable),
                *static_cast<class_type*>(nullptr),
                marshallers,
                std::make_index_sequence<std::tuple_size_v<arg_tuple>>{},
                std::forward<TAbiArgs>(abiArguments)...);
        }
        else
        {
            using traits = detail::FunctionTraits<FT>;
            using result_type = typename traits::return_type;
            using signature_arg_tuple = typename traits::argument_tuple;
            using arg_tuple = TCppArgumentTuple;

            static_assert(
                std::tuple_size_v<arg_tuple> == std::tuple_size_v<signature_arg_tuple>,
                "SharpCpp marshaller argument tuple arity mismatch.");

            detail::marshaller_tuple_t<arg_tuple> marshallers;
            return detail::InvokeWithMarshallersImpl<result_type, arg_tuple>(
                std::forward<TCallable>(callable),
                marshallers,
                std::make_index_sequence<std::tuple_size_v<arg_tuple>>{},
                std::forward<TAbiArgs>(abiArguments)...);
        }
    }

    template <typename FT, typename TCppArgumentTuple = typename detail::MemberFunctionTraits<FT>::argument_tuple, typename TSelfAbi, typename... TAbiArgs>
    static decltype(auto) invoke(FT method, TSelfAbi self, TAbiArgs&&... abiArguments)
    {
        using traits = detail::MemberFunctionTraits<FT>;
        using result_type = typename traits::return_type;
        using class_type = typename traits::class_type;
        using signature_arg_tuple = typename traits::argument_tuple;
        using arg_tuple = TCppArgumentTuple;

        static_assert(
            std::tuple_size_v<arg_tuple> == std::tuple_size_v<signature_arg_tuple>,
            "SharpCpp marshaller argument tuple arity mismatch.");

        Marshaller<class_type*> self_marshaller;
        auto* instance = static_cast<class_type*>(self_marshaller.ConvertToCpp(self));
        detail::marshaller_tuple_t<arg_tuple> marshallers;

        return detail::InvokeMemberWithMarshallersImpl<result_type, arg_tuple>(
            method,
            *instance,
            marshallers,
            std::make_index_sequence<std::tuple_size_v<arg_tuple>>{},
            std::forward<TAbiArgs>(abiArguments)...);
    }

    template <typename FT, typename TCppArgumentTuple = typename detail::MemberFunctionTraits<FT>::argument_tuple, typename TSelfAbi, typename... TAbiArgs>
    static auto invoke_owned(FT method, TSelfAbi self, TAbiArgs&&... abiArguments)
    {
        using traits = detail::MemberFunctionTraits<FT>;
        using result_type = typename traits::return_type;
        using class_type = typename traits::class_type;
        using signature_arg_tuple = typename traits::argument_tuple;
        using arg_tuple = TCppArgumentTuple;

        static_assert(
            std::tuple_size_v<arg_tuple> == std::tuple_size_v<signature_arg_tuple>,
            "SharpCpp marshaller argument tuple arity mismatch.");

        Marshaller<class_type*> self_marshaller;
        auto* instance = static_cast<class_type*>(self_marshaller.ConvertToCpp(self));
        detail::marshaller_tuple_t<arg_tuple> marshallers;

        return detail::InvokeMemberWithMarshallersOwnedImpl<result_type, arg_tuple>(
            method,
            *instance,
            marshallers,
            std::make_index_sequence<std::tuple_size_v<arg_tuple>>{},
            std::forward<TAbiArgs>(abiArguments)...);
    }

    template <typename FT, typename TCppArgumentTuple = typename detail::MemberFunctionTraits<FT>::argument_tuple, typename TSelf, typename... TAbiArgs>
    static decltype(auto) invoke_instance(FT method, TSelf* instance, TAbiArgs&&... abiArguments)
    {
        using traits = detail::MemberFunctionTraits<FT>;
        using result_type = typename traits::return_type;
        using signature_arg_tuple = typename traits::argument_tuple;
        using arg_tuple = TCppArgumentTuple;

        static_assert(
            std::tuple_size_v<arg_tuple> == std::tuple_size_v<signature_arg_tuple>,
            "SharpCpp marshaller argument tuple arity mismatch.");

        detail::marshaller_tuple_t<arg_tuple> marshallers;

        return detail::InvokeMemberWithMarshallersImpl<result_type, arg_tuple>(
            method,
            *instance,
            marshallers,
            std::make_index_sequence<std::tuple_size_v<arg_tuple>>{},
            std::forward<TAbiArgs>(abiArguments)...);
    }

    template <typename FT, typename TCppArgumentTuple = typename detail::MemberFunctionTraits<FT>::argument_tuple, typename TSelf, typename... TAbiArgs>
    static auto invoke_owned_instance(FT method, TSelf* instance, TAbiArgs&&... abiArguments)
    {
        using traits = detail::MemberFunctionTraits<FT>;
        using result_type = typename traits::return_type;
        using signature_arg_tuple = typename traits::argument_tuple;
        using arg_tuple = TCppArgumentTuple;

        static_assert(
            std::tuple_size_v<arg_tuple> == std::tuple_size_v<signature_arg_tuple>,
            "SharpCpp marshaller argument tuple arity mismatch.");

        detail::marshaller_tuple_t<arg_tuple> marshallers;

        return detail::InvokeMemberWithMarshallersOwnedImpl<result_type, arg_tuple>(
            method,
            *instance,
            marshallers,
            std::make_index_sequence<std::tuple_size_v<arg_tuple>>{},
            std::forward<TAbiArgs>(abiArguments)...);
    }

    template <typename FT, typename TMethod, typename TSelfAbi, typename... TAbiArgs,
        std::enable_if_t<
            std::is_member_function_pointer_v<TMethod> &&
            !std::is_same_v<TMethod, FT>,
            int> = 0>
    static decltype(auto) invoke(TMethod method, TSelfAbi self, TAbiArgs&&... abiArguments)
    {
        using traits = detail::MemberFunctionTraits<TMethod>;
        using result_type = typename traits::return_type;
        using class_type = typename traits::class_type;
        using arg_tuple = typename traits::argument_tuple;

        Marshaller<class_type*> self_marshaller;
        auto* instance = static_cast<class_type*>(self_marshaller.ConvertToCpp(self));
        detail::marshaller_tuple_t<arg_tuple> marshallers;

        return detail::InvokeMemberWithMarshallersImpl<result_type, arg_tuple>(
            method,
            *instance,
            marshallers,
            std::make_index_sequence<std::tuple_size_v<arg_tuple>>{},
            std::forward<TAbiArgs>(abiArguments)...);
    }

    template <typename T, typename... TArgs, typename... TAbiArgs>
    static auto construct(TAbiArgs&&... abiArguments) -> ret_t<T*>
    {
        return detail::ConstructImpl<T, TArgs...>(
            std::make_index_sequence<sizeof...(TArgs)>{},
            std::forward<TAbiArgs>(abiArguments)...);
    }

    template <typename T>
    static void destroy(arg_t<T*> self)
    {
        Marshaller<T*> self_marshaller;
        delete self_marshaller.ConvertToCpp(self);
    }

    template <typename TResult, typename TClass>
    static auto get_field(TResult TClass::*field, arg_t<TClass*> self) -> ret_t<TResult>
    {
        Marshaller<TClass*> self_marshaller;
        ReturnMarshaller<TResult> marshaller;
        return marshaller.ConvertToAbi(std::invoke(field, self_marshaller.ConvertToCpp(self)));
    }

    template <typename TResult, typename TClass>
    static auto get_field(TResult TClass::*field, arg_t<TClass*> self, std::true_type) -> field_t<TResult>
    {
        Marshaller<TClass*> self_marshaller;
        return std::addressof(std::invoke(field, self_marshaller.ConvertToCpp(self)));
    }

    template <typename TValue, typename TClass>
    static void set_field(TValue TClass::*field, arg_t<TClass*> self, arg_t<TValue> value)
    {
        Marshaller<TClass*> self_marshaller;
        ParameterMarshaller<TValue> marshaller;
        if constexpr (std::is_array_v<TValue>)
        {
            auto& target = std::invoke(field, self_marshaller.ConvertToCpp(self));
            auto& source = marshaller.ConvertToCpp(value);
            for (std::size_t index = 0; index < std::extent_v<TValue>; ++index)
            {
                target[index] = source[index];
            }
        }
        else
        {
            std::invoke(field, self_marshaller.ConvertToCpp(self)) = marshaller.ConvertToCpp(value);
        }
    }

    template <typename TValue, typename TClass>
    static void set_field(TValue TClass::*field, arg_t<TClass*> self, SharpCpp::field_t<TValue> value)
    {
        Marshaller<TClass*> self_marshaller;
        std::invoke(field, self_marshaller.ConvertToCpp(self)) = *value;
    }

    template <typename TValue, typename TClass>
    static void set_field(TValue TClass::*field, arg_t<TClass*> self, SharpCpp::field_t<TValue> value, std::true_type)
    {
        Marshaller<TClass*> self_marshaller;
        auto& target = std::invoke(field, self_marshaller.ConvertToCpp(self));
        if (value == nullptr)
        {
            if constexpr (detail::has_clear_v<TValue>)
            {
                target.clear();
                return;
            }
            else
            {
                throw std::runtime_error("SharpCpp borrowed field setter cannot clear this field type.");
            }
        }

        target = std::move(*value);
    }
};
}
