#pragma once

struct [[sharpcpp::director]] protected_counter
{
protected:
    explicit protected_counter(int seed)
        : seed_(seed)
    {
    }

    int increment(int amount)
    {
        seed_ += amount;
        return seed_;
    }

    virtual int scale(int value)
    {
        return (seed_ * factor_) + value;
    }

    int factor_{2};

public:
    int get_seed() const
    {
        return seed_;
    }

    int call_scale(int value)
    {
        return scale(value);
    }

private:
    int seed_{};
};

struct plain_protected_counter
{
protected:
    int hidden_value() const
    {
        return 7;
    }

    int factor_{3};

public:
    plain_protected_counter() = default;

    int visible_value() const
    {
        return hidden_value() * factor_;
    }
};
