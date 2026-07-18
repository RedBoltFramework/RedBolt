#pragma once

namespace Test
{

class ForcedAbstract
{
public:
    explicit ForcedAbstract(int seed)
        : seed_(seed)
    {
    }

    virtual ~ForcedAbstract() = default;

    virtual int Compute() const = 0;

protected:
    int seed_{};
};

}
