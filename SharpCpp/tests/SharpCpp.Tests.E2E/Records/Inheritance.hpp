#pragma once

struct base_counter
{
    explicit base_counter(int value)
        : value_(value)
    {
    }

    virtual int get_kind() const
    {
        return 1;
    }

    int get_value() const
    {
        return value_;
    }

protected:
    int value_{};
};

struct derived_counter : base_counter
{
    derived_counter(int value, int bonus)
        : base_counter(value)
        , bonus_(bonus)
    {
    }

    int get_bonus() const
    {
        return bonus_;
    }

    int get_kind() const override
    {
        return 2;
    }

    int add_all() const
    {
        return value_ + bonus_;
    }

    base_counter* echo_base(base_counter* value)
    {
        return value;
    }

    static base_counter* identity_base(base_counter* value)
    {
        return value;
    }

private:
    int bonus_{};
};

struct inheritance_holder
{
    base_counter* base_view{};
};

inline inheritance_holder* create_inheritance_holder(base_counter* base_view)
{
    static inheritance_holder holder {};
    holder.base_view = base_view;
    return &holder;
}

inline int read_base_counter(base_counter* value)
{
    return value->get_value();
}

inline int read_base_counter_kind(base_counter* value)
{
    return value->get_kind();
}

inline base_counter* get_shared_derived_as_base()
{
    static derived_counter shared(30, 4);
    return &shared;
}
