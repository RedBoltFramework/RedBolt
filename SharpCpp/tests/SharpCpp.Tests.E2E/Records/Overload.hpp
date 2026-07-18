#pragma once

struct overload_box
{
    explicit overload_box(int value)
        : value_(value)
    {
    }

    int mix(int value) const
    {
        return value_ + value;
    }

    int mix(int left, int right) const
    {
        return value_ + left + right;
    }

    static int combine(int value)
    {
        return value;
    }

    static int combine(int left, int right)
    {
        return left + right;
    }

private:
    int value_{};
};
