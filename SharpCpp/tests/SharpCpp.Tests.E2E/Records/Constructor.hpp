#pragma once

struct constructor_box
{
    explicit constructor_box(int value)
        : value_(value)
    {
    }

    constructor_box(int left, int right)
        : value_(left + right)
    {
    }

    int get_value()
    {
        return value_;
    }

private:
    int value_{};
};
