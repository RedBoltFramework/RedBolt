#pragma once

struct deleted_box
{
    deleted_box() {}
    deleted_box(int) = delete;

    deleted_box& operator=(const deleted_box&) = delete;
    int deleted_method() = delete;
};
