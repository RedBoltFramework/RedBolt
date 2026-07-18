#pragma once

struct payload_record
{
    explicit payload_record(int value)
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

    static payload_record* create(int value)
    {
        return new payload_record(value);
    }

private:
    int value_{};
};

struct owner_record
{
    explicit owner_record(payload_record* payload)
        : payload_(payload)
    {
    }

    payload_record* get_payload() const
    {
        return payload_;
    }

    payload_record* replace_payload(payload_record* payload)
    {
        auto previous = payload_;
        payload_ = payload;
        return previous;
    }

    static owner_record* create(payload_record* payload)
    {
        static owner_record owner(payload);
        owner.payload_ = payload;
        return &owner;
    }

    static payload_record* identity(payload_record* payload)
    {
        return payload;
    }

private:
    payload_record* payload_{};
};

inline payload_record* echo_payload(payload_record* payload)
{
    return payload;
}
