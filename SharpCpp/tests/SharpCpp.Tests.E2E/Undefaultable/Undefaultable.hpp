#pragma once

/// Free function: undefaultable const char* after expressible int default.
/// Overload A: (int threshold = 5)
/// Overload B: (int threshold, const char* label)
inline int describe_level(int value, int threshold = 5, const char* label = "default")
{
    (void)label;
    return value >= threshold ? 1 : 0;
}

/// Free function: undefaultable const char* with no expressible before-params.
/// Overload A: ()
/// Overload B: (const char* label)
inline int check_undefaultable_only(const char* label = "none")
{
    (void)label;
    return 42;
}

/// Free function: void return with undefaultable param (cascade coverage).
inline void apply_config(const char* config = "default", int factor = 3)
{
    (void)config;
    (void)factor;
}

/// Free function: mixed expressible + unexpressible + undefaultable.
inline int mixed_defaults(int a = 1, const char* b = "b", int c = 3)
{
    return (a * 100) + c;
}

struct undefaultable_handler
{
    explicit undefaultable_handler(int id, const char* name = "unnamed")
        : id_(id)
        , name_(name ? name : "unnamed")
    {
    }

    int get_id() const
    {
        return id_;
    }

    const char* get_name() const
    {
        return name_;
    }

    int process(int amount, const char* tag = "default", int scale = 2) const
    {
        return id_ + amount + scale;
    }

    static int lookup(const char* key = "default", int fallback = -1)
    {
        (void)key;
        return fallback;
    }

    static void clear_cache(const char* reason = "unknown")
    {
        (void)reason;
    }

private:
    int id_{};
    const char* name_{};
};
