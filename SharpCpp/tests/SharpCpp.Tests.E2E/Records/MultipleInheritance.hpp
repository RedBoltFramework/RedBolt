#pragma once

struct mi_left
{
    virtual int left_value() const
    {
        return 10;
    }
};

struct mi_right
{
    virtual int right_value() const
    {
        return 20;
    }

    int right_only() const
    {
        return right_value() + 1;
    }
};

struct [[sharpcpp::director]] mi_counter : mi_left, mi_right
{
    mi_counter() = default;

    int left_value() const override
    {
        return 100;
    }

    int right_value() const override
    {
        return 200;
    }

    int sum_virtuals() const
    {
        return left_value() + right_value();
    }
};
