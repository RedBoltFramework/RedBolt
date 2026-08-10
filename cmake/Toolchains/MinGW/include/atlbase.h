#pragma once

#include <unknwn.h>
#include <memory>
#include <utility>

template <class T>
class CComPtr
{
public:
    T* p = nullptr;

    CComPtr() = default;
    CComPtr(std::nullptr_t) {}

    CComPtr(T* value) : p{value}
    {
        InternalAddRef();
    }

    CComPtr(const CComPtr& value) : p{value.p}
    {
        InternalAddRef();
    }

    template <class U>
    CComPtr(const CComPtr<U>& value) : p{value.p}
    {
        InternalAddRef();
    }

    CComPtr(CComPtr&& value) noexcept : p{value.Detach()} {}

    template <class U>
    CComPtr(CComPtr<U>&& value) noexcept : p{value.Detach()} {}

    ~CComPtr()
    {
        Release();
    }

    CComPtr& operator=(std::nullptr_t)
    {
        Release();
        return *this;
    }

    CComPtr& operator=(T* value)
    {
        if (p != value)
        {
            if (value != nullptr)
                value->AddRef();
            Release();
            p = value;
        }
        return *this;
    }

    CComPtr& operator=(const CComPtr& value)
    {
        return *this = value.p;
    }

    template <class U>
    CComPtr& operator=(const CComPtr<U>& value)
    {
        return *this = value.p;
    }

    CComPtr& operator=(CComPtr&& value) noexcept
    {
        if (this != std::addressof(value))
        {
            Release();
            p = value.Detach();
        }
        return *this;
    }

    template <class U>
    CComPtr& operator=(CComPtr<U>&& value) noexcept
    {
        Release();
        p = value.Detach();
        return *this;
    }

    operator T*() const { return p; }
    T* operator->() const { return p; }
    T** operator&()
    {
        Release();
        return &p;
    }

    bool operator!() const { return p == nullptr; }

    void Attach(T* value)
    {
        Release();
        p = value;
    }

    T* Detach()
    {
        T* value = p;
        p = nullptr;
        return value;
    }

    void Release()
    {
        T* value = p;
        p = nullptr;
        if (value != nullptr)
            value->Release();
    }

    template <class U>
    HRESULT QueryInterface(U** value) const
    {
        return p != nullptr
            ? p->QueryInterface(__uuidof(U), reinterpret_cast<void**>(value))
            : E_POINTER;
    }

private:
    void InternalAddRef()
    {
        if (p != nullptr)
            p->AddRef();
    }
};

template <class T>
class CComQIPtr : public CComPtr<T>
{
public:
    CComQIPtr() = default;

    template <class U>
    CComQIPtr(U* value)
    {
        if (value != nullptr)
            value->QueryInterface(__uuidof(T), reinterpret_cast<void**>(&this->p));
    }

    template <class U>
    CComQIPtr(const CComPtr<U>& value) : CComQIPtr{value.p} {}
};

template <class T>
struct __mingw_uuidof_s<CComPtr<T>> : __mingw_uuidof_s<T>
{};

template <class T>
constexpr const GUID& __mingw_uuidof()
{
    return __mingw_uuidof_s<T>::__uuid_inst;
}
