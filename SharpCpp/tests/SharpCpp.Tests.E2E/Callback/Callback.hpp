#pragma once

#include <functional>
#include <string>

struct callback_box
{
    int value_{};

    int get_value()
    {
        return value_;
    }

    void set_value(int value)
    {
        value_ = value;
    }
};

inline void invoke_void_callback(std::function<void(int)> callback, int value)
{
    callback(value);
}

inline int invoke_int_callback(std::function<int(int, int)> callback, int lhs, int rhs)
{
    return callback(lhs, rhs);
}

inline int invoke_copied_callback(std::function<int(int)> callback, int value)
{
    auto copied = callback;
    return copied(value);
}

inline int invoke_object_pointer_callback(std::function<void(callback_box*)> callback, int value)
{
    static callback_box box{};
    box.set_value(value);
    callback(&box);
    return box.get_value();
}

inline int invoke_object_reference_callback(std::function<void(callback_box&)> callback, int value)
{
    static callback_box box{};
    box.set_value(value);
    callback(box);
    return box.get_value();
}

inline int invoke_primitive_pointer_callback(std::function<void(int*)> callback, int value)
{
    int local = value;
    callback(&local);
    return local;
}

inline int invoke_primitive_reference_callback(std::function<void(int&)> callback, int value)
{
    int local = value;
    callback(local);
    return local;
}

inline int invoke_string_reference_callback(std::function<int(const std::string&)> callback)
{
    static const std::string value = "SharpCpp";
    return callback(value);
}

inline void invoke_named_delegate_callback([[sharpcpp::delegate("ValueCallback")]] std::function<void(int)> callback, int value)
{
    callback(value);
}
