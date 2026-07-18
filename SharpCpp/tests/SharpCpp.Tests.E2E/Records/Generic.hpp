#pragma once

template <typename T>
[[sharpcpp::instantiate("int")]]
[[sharpcpp::instantiate("float")]]
[[sharpcpp::instantiate("double")]]
T generic_add(T left, T right)
{
    return left + right;
}

struct generic_payload
{
    explicit generic_payload(int value)
        : value_(value)
    {
    }

    int get_value() const
    {
        return value_;
    }

    void set_value(int value)
    {
        value_ = value;
    }

private:
    int value_{};
};

template <typename T>
struct [[sharpcpp::rename("HandleBoxOf<{0}>")]] handle_box
{
    explicit handle_box(T value)
        : value_(value)
    {
    }

    T get_value() const
    {
        return value_;
    }

    void set_value(T value)
    {
        value_ = value;
    }

    static handle_box<T>* create(T value)
    {
        return new handle_box<T>(value);
    }

private:
    T value_{};
};

inline handle_box<generic_payload*>* create_generic_payload_box(generic_payload* value)
{
    return handle_box<generic_payload*>::create(value);
}

template <typename T>
struct
[[sharpcpp::rename("BoxOf<{0}>")]]
[[sharpcpp::instantiate("double")]]
value_box
{
    explicit value_box(T value)
        : value_(value)
    {
    }

    T get_value() const
    {
        return value_;
    }

    void set_value(T value)
    {
        value_ = value;
    }

    static value_box<T>* create(T value)
    {
        return new value_box<T>(value);
    }

    template <typename U>
    [[sharpcpp::instantiate("int")]]
    U project(U scale) const
    {
        return static_cast<U>(value_) * scale;
    }

private:
    T value_{};
};

inline value_box<int>* create_int_box(int value)
{
    return value_box<int>::create(value);
}

inline value_box<float>* create_float_box(float value)
{
    return value_box<float>::create(value);
}
