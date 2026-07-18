#pragma once

namespace SharpCpp::detail
{
template <class T>
struct OptionalValue
{
    bool HasValue {};
    T Value {};
};
}
