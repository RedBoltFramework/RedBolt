#pragma once

enum class status_code : int
{
    ready = 1,
    busy = 2,
    error = -1
};

enum class [[sharpcpp::flags]] permissions : unsigned int
{
    none = 0u,
    read = 1u << 0,
    write = 1u << 1,
    execute = 1u << 2,
    read_write = 3u
};

inline status_code roundtrip_status_code(status_code value)
{
    return value;
}

inline status_code echo_in_status_code(const status_code& value)
{
    return value;
}

inline void cycle_ref_status_code(status_code& value)
{
    value = value == status_code::ready
        ? status_code::busy
        : value == status_code::busy
            ? status_code::error
            : status_code::ready;
}

inline void write_out_status_code(status_code* value)
{
    *value = status_code::error;
}

inline permissions roundtrip_permissions(permissions value)
{
    return value;
}

inline permissions echo_in_permissions(const permissions& value)
{
    return value;
}

inline void upgrade_ref_permissions(permissions& value)
{
    value = permissions::read_write;
}

inline void write_out_permissions(permissions* value)
{
    *value = permissions::execute;
}

inline permissions combine_permissions(permissions left, permissions right)
{
    return static_cast<permissions>(
        static_cast<unsigned int>(left) |
        static_cast<unsigned int>(right));
}
