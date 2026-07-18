#pragma once

#include <cstdlib>
#include <cstring>
#include <string>
#include <string_view>

namespace eastl
{
template <typename T>
class basic_string_view
{
public:
    using size_type = std::size_t;

    constexpr basic_string_view() = default;

    constexpr basic_string_view(const T* value)
        : value_(value)
        , size_(value != nullptr ? std::char_traits<T>::length(value) : 0)
    {
    }

    constexpr basic_string_view(const T* value, size_type size)
        : value_(value)
        , size_(value != nullptr ? size : 0)
    {
    }

    constexpr const T* data() const noexcept { return value_; }
    constexpr size_type size() const noexcept { return size_; }

private:
    const T* value_{};
    size_type size_{};
};

using string_view = basic_string_view<char>;
}

namespace ea = eastl;

inline int string_length(const char* value)
{
    int count = 0;
    while (*value++)
    {
        ++count;
    }
    return count;
}

inline bool string_equals(const char* lhs, const char* rhs)
{
    while (*lhs && *rhs && *lhs == *rhs)
    {
        ++lhs;
        ++rhs;
    }
    return *lhs == *rhs;
}

inline void get_name([[sharpcpp::out]] const char** value)
{
    const char* source = "SharpCpp";
    auto* buf = (char*)std::malloc(std::strlen(source) + 1);
    std::strcpy(buf, source);
    *value = buf;
}

inline void replace_name([[sharpcpp::ref]] char** value)
{
    const char* source = "replaced";
    auto* buf = (char*)std::malloc(std::strlen(source) + 1);
    std::strcpy(buf, source);
    std::free(*value);
    *value = buf;
}

inline const char* get_message()
{
    return "Hello from SharpCpp";
}

inline int measure_std_string(const std::string& value)
{
    return static_cast<int>(value.size());
}

inline int string_view_length(std::string_view value)
{
    return static_cast<int>(value.size());
}

inline bool string_view_equals(std::string_view lhs, std::string_view rhs)
{
    return lhs == rhs;
}

inline int string_view_embedded_null_length(std::string_view value)
{
    return static_cast<int>(value.size());
}

inline std::string_view get_string_view_message()
{
    return "Hello from string_view";
}

inline std::string_view get_string_view_with_embedded_null()
{
    static constexpr char value[] = { 'a', '\0', 'b' };
    return std::string_view(value, sizeof(value));
}

inline int ea_string_view_length(ea::string_view value)
{
    return static_cast<int>(value.size());
}

inline int ea_string_view_embedded_null_length(ea::string_view value)
{
    return static_cast<int>(value.size());
}

inline ea::string_view get_ea_string_view_message()
{
    return "Hello from ea::string_view";
}

class ea_compat_string
{
public:
    ea_compat_string() = default;

    std::size_t size() const
    {
        return value_.size();
    }

    const char* data() const
    {
        return value_.data();
    }

    void push_back(char value)
    {
        value_.push_back(value);
    }

    void clear()
    {
        value_.clear();
    }

private:
    std::string value_;
};

inline int measure_ea_string(const ea_compat_string& value)
{
    return static_cast<int>(value.size());
}
