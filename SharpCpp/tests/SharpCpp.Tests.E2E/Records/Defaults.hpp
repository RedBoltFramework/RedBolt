#pragma once

enum class default_mode : int
{
    idle = 1,
    active = 2
};

struct default_box
{
    explicit default_box(int seed, int factor = 2, default_mode mode = default_mode::active)
        : value_(seed * factor)
        , mode_(mode)
    {
    }

    int get_value() const
    {
        return value_;
    }

    default_mode get_mode() const
    {
        return mode_;
    }

    int combine(int amount, int bias = 3, default_mode mode = default_mode::active) const
    {
        return value_ + amount + (bias * 10) + (mode == default_mode::active ? 100 : 200);
    }

    static default_box* create(int seed, int factor = 2, default_mode mode = default_mode::active)
    {
        return new default_box(seed, factor, mode);
    }

    static int add(int lhs, int rhs = 23)
    {
        return lhs + rhs;
    }

private:
    int value_{};
    default_mode mode_{};
};

inline int default_score(int seed, int factor = 2, bool enabled = true)
{
    return (seed * 100) + (factor * 10) + (enabled ? 1 : 2);
}
