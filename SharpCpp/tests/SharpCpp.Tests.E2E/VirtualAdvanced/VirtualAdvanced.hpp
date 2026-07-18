#pragma once

struct [[sharpcpp::director]] adv_virtual_counter
{
    explicit adv_virtual_counter(int seed)
        : seed_(seed)
    {
    }

    virtual ~adv_virtual_counter() = default;

    virtual int get_value() const
    {
        return seed_;
    }

    virtual int add(int amount)
    {
        seed_ += amount;
        return seed_;
    }

    int get_seed() const
    {
        return seed_;
    }

protected:
    int seed_{};
};

struct [[sharpcpp::director]] adv_abstract_accumulator
{
protected:
    explicit adv_abstract_accumulator(int base_value)
        : base_value_(base_value)
    {
    }

public:
    virtual ~adv_abstract_accumulator() = default;

    virtual int compute(int value) const = 0;

    int get_base_value() const
    {
        return base_value_;
    }

protected:
    int base_value_{};
};

struct adv_public_virtual_base
{
    explicit adv_public_virtual_base(int bias)
        : bias_(bias)
    {
    }

    virtual ~adv_public_virtual_base() = default;

    virtual int adjust(int value) const
    {
        return value + bias_;
    }

protected:
    int bias_{};
};

struct [[sharpcpp::director]] adv_protected_override_derived : public adv_public_virtual_base
{
    explicit adv_protected_override_derived(int bias)
        : adv_public_virtual_base(bias)
    {
    }

protected:
    int adjust(int value) const override
    {
        return value + bias_ + 10;
    }
};

struct [[sharpcpp::director]] adv_protected_virtual_base
{
    explicit adv_protected_virtual_base(int bias)
        : bias_(bias)
    {
    }

    virtual ~adv_protected_virtual_base() = default;

    int invoke_adjust(int value) const
    {
        return adjust(value);
    }

protected:
    virtual int adjust(int value) const
    {
        return value + bias_ + 20;
    }

    int bias_{};
};

struct [[sharpcpp::director]] adv_public_override_derived : public adv_protected_virtual_base
{
    explicit adv_public_override_derived(int bias)
        : adv_protected_virtual_base(bias)
    {
    }

public:
    int adjust(int value) const override
    {
        return value + bias_ + 30;
    }
};

struct adv_final_root
{
    explicit adv_final_root(int seed)
        : seed_(seed)
    {
    }

    virtual ~adv_final_root() = default;

    virtual int mode() const
    {
        return seed_ + 1;
    }

protected:
    int seed_{};
};

struct adv_final_mid : public adv_final_root
{
    explicit adv_final_mid(int seed)
        : adv_final_root(seed)
    {
    }

    int mode() const final
    {
        return seed_ + 20;
    }

    virtual int value() const
    {
        return seed_ + 2;
    }
};

struct adv_final_leaf : public adv_final_mid
{
    explicit adv_final_leaf(int seed)
        : adv_final_mid(seed)
    {
    }

    int value() const override
    {
        return seed_ + 30;
    }
};

inline int invoke_adv_virtual_counter_get_value(const adv_virtual_counter& value)
{
    return value.get_value();
}

inline int invoke_adv_virtual_counter_add(adv_virtual_counter& value, int amount)
{
    return value.add(amount);
}

inline int invoke_adv_abstract_accumulator_compute(const adv_abstract_accumulator& value, int amount)
{
    return value.compute(amount);
}

inline int invoke_adv_public_virtual_base_adjust(const adv_public_virtual_base& value, int amount)
{
    return value.adjust(amount);
}

inline int invoke_adv_protected_virtual_base_adjust(const adv_protected_virtual_base& value, int amount)
{
    return value.invoke_adjust(amount);
}

inline int invoke_adv_final_root_mode(const adv_final_root& value)
{
    return value.mode();
}

inline int invoke_adv_final_mid_value(const adv_final_mid& value)
{
    return value.value();
}
