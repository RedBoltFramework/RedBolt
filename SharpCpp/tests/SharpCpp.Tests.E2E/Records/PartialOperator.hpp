#pragma once

struct partial_operator_box
{
    explicit partial_operator_box(int value)
        : value_(value)
    {
    }

    bool operator<(const partial_operator_box& rhs) const
    {
        return value_ < rhs.value_;
    }

    bool operator==(const partial_operator_box& rhs) const
    {
        return value_ == rhs.value_;
    }

private:
    int value_{};
};
