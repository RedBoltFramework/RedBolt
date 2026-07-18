#pragma once

struct [[sharpcpp::director]] callback_counter
{
    explicit callback_counter(int seed)
        : seed_(seed)
    {
    }

    virtual int get_value() const
    {
        return seed_;
    }

    int call_virtual_twice() const
    {
        return get_value() + get_value();
    }

private:
    int seed_{};
};
