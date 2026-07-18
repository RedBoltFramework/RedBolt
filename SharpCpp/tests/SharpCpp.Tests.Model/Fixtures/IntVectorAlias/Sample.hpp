#pragma once

namespace my
{
template <class T>
class vector {};
}

using IntVector = my::vector<int>;

class IntVectorConsumer
{
public:
    void Accept(const IntVector& items);
};
