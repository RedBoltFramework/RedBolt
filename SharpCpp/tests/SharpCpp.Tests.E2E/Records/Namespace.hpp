#pragma once

namespace e2e::records
{
struct namespaced_counter
{
    explicit namespaced_counter(int value)
        : value_(value)
    {
    }

    int get_value() const
    {
        return value_;
    }

    static namespaced_counter* identity(namespaced_counter* other)
    {
        return other;
    }

    int value_{};
};
}