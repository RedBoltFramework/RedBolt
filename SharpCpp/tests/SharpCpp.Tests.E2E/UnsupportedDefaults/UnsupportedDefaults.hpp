#pragma once

#define E2E_UNSUPPORTED_DEFAULT_SINGLE 42.0f
#define E2E_UNSUPPORTED_DEFAULT_A 10.0f
#define E2E_UNSUPPORTED_DEFAULT_B 20.0f
#define E2E_UNSUPPORTED_DEFAULT_OFFSET 4
#define E2E_UNSUPPORTED_DEFAULT_DELTA 6
#define E2E_UNSUPPORTED_DEFAULT_RHS 7
#define E2E_UNSUPPORTED_DEFAULT_ALTERNATING_OFFSET 8
#define E2E_UNSUPPORTED_DEFAULT_ALTERNATING_DELTA 6

/// Returns 0 when x is less than the threshold (defaults to 42.0f).
inline int check_threshold_single_default(float x, float threshold = E2E_UNSUPPORTED_DEFAULT_SINGLE)
{
    return x < threshold ? 0 : 1;
}

/// Returns 0 when a < b (defaults a=10.0f, b=20.0f).
inline int check_two_defaults_cascade(float x, float a = E2E_UNSUPPORTED_DEFAULT_A, float b = E2E_UNSUPPORTED_DEFAULT_B)
{
    (void)x;
    return a < b ? 0 : 1;
}

/// Plain function with no unexpressible defaults (regression guard).
inline int check_plain_no_defaults(int value)
{
    return value * 2;
}

inline int check_mixed_defaults(int seed, int factor = 2, int offset = E2E_UNSUPPORTED_DEFAULT_OFFSET, int tail = 5)
{
    return (seed * 1000) + (factor * 100) + (offset * 10) + tail;
}

inline int check_alternating_defaults(
    int seed,
    int factor = 2,
    int offset = E2E_UNSUPPORTED_DEFAULT_ALTERNATING_OFFSET,
    int middle = 7,
    int delta = E2E_UNSUPPORTED_DEFAULT_ALTERNATING_DELTA,
    int tail = 5)
{
    return (seed * 100000) + (factor * 10000) + (offset * 1000) + (middle * 100) + (delta * 10) + tail;
}

struct unsupported_default_counter
{
    explicit unsupported_default_counter(int seed, int factor = 2, int offset = E2E_UNSUPPORTED_DEFAULT_OFFSET, int tail = 5)
        : value_((seed * 1000) + (factor * 100) + (offset * 10) + tail)
    {
    }

    int get_value() const
    {
        return value_;
    }

    int combine(int amount, int bias = 3, int delta = E2E_UNSUPPORTED_DEFAULT_DELTA, int scale = 2) const
    {
        return value_ + (amount * 1000) + (bias * 100) + (delta * 10) + scale;
    }

    static int add(int lhs, int rhs = E2E_UNSUPPORTED_DEFAULT_RHS, int tail = 5)
    {
        return (lhs * 100) + (rhs * 10) + tail;
    }

private:
    int value_{};
};
