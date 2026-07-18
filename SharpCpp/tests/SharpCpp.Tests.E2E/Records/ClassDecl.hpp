#pragma once

class class_decl_box
{
public:
    explicit class_decl_box(int value)
        : value_(value)
    {
    }

    int get_value() const
    {
        return value_;
    }

private:
    int value_{};
};
