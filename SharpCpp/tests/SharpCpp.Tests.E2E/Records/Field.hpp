#pragma once

enum class field_mode : int
{
    idle = 1,
    active = 2,
    suspended = 3
};

struct field_payload
{
    int value{};
};

struct field_box
{
    field_box(int count, bool enabled, field_mode mode, field_payload* payload, int fixed_value)
        : count(count)
        , enabled(enabled)
        , mode(mode)
        , payload(payload)
        , fixed_value(fixed_value)
    {
    }

    int count{};
    bool enabled{};
    field_mode mode{field_mode::idle};
    field_payload* payload{};
    field_payload payload_samples[2]{};
    int samples[3]{};
    const int fixed_value{};
};

inline field_payload* create_field_payload(int value)
{
    return new field_payload { value };
}

inline field_box* create_field_box(int count, bool enabled, field_mode mode, field_payload* payload, int fixed_value)
{
    return new field_box(count, enabled, mode, payload, fixed_value);
}
