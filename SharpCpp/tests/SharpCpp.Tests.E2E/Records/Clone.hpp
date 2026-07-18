#pragma once

struct clone_counter
{
    explicit clone_counter(int value)
        : value_(value)
    {
    }

    clone_counter(const clone_counter&) = default;

    int get_value() const
    {
        return value_;
    }

    void set_value(int value)
    {
        value_ = value;
    }

private:
    int value_{};
};
