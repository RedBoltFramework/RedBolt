#pragma once

enum class placement_status : int
{
    ready = 1,
    busy = 2,
    error = 3
};

enum class [[sharpcpp::flags]] placement_permissions : unsigned int
{
    none = 0u,
    read = 1u << 0,
    write = 1u << 1,
    execute = 1u << 2,
    read_write = 3u
};

struct enum_box
{
    enum_box(placement_status status, placement_permissions permissions)
        : status_(status)
        , permissions_(permissions)
    {
    }

    placement_status get_status() const
    {
        return status_;
    }

    void set_status(placement_status status)
    {
        status_ = status;
    }

    placement_permissions get_permissions() const
    {
        return permissions_;
    }

    void set_permissions(placement_permissions permissions)
    {
        permissions_ = permissions;
    }

    static enum_box* create(placement_status status, placement_permissions permissions)
    {
        static enum_box value(status, permissions);
        value.status_ = status;
        value.permissions_ = permissions;
        return &value;
    }

    static placement_permissions combine_permissions(placement_permissions left, placement_permissions right)
    {
        return static_cast<placement_permissions>(
            static_cast<unsigned int>(left) |
            static_cast<unsigned int>(right));
    }

private:
    placement_status status_;
    placement_permissions permissions_;
};
