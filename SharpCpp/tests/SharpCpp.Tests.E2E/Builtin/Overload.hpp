#pragma once

inline int overload_value(int value)
{
    return value;
}

inline int overload_value(int left, int right)
{
    return left + right;
}
