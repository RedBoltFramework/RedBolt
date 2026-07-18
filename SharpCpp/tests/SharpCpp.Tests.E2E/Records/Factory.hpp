#pragma once

struct factory_box
{
    explicit factory_box(int value)
        : value_(value)
    {
    }

    int get_value() const
    {
        return value_;
    }

    static factory_box* create(int value)
    {
        static factory_box box(value);
        box.value_ = value;
        return &box;
    }

    static int add(int lhs, int rhs)
    {
        return lhs + rhs;
    }

    static factory_box* identity(factory_box* value)
    {
        return value;
    }

private:
    int value_{};
};

inline int read_factory_box(const factory_box* value)
{
    return value->get_value();
}
