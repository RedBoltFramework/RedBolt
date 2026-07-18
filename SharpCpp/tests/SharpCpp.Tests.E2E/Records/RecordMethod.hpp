#pragma once

struct counter_box
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

    int add(int delta)
    {
        value_ += delta;
        return value_;
    }

    bool is_value(int expected)
    {
        return value_ == expected;
    }

    /// Returns a reference to a persistent instance (for testing reference returns).
    static counter_box& get_reference(int value)
    {
        static counter_box box{};
        box.value_ = value;
        return box;
    }
};

inline counter_box* create_counter_box(int value)
{
    static counter_box box {};
    box.set_value(value);
    return &box;
}

/// Returns a reference to a counter_box (free-function form).
inline counter_box& get_counter_box_ref(int value)
{
    static counter_box box{};
    box.value_ = value;
    return box;
}
