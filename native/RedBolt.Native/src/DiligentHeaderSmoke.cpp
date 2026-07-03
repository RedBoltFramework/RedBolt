#include <Constants.h>
#include <GraphicsTypes.h>

int RedBoltDiligentHeaderSmoke()
{
    static_assert(DILIGENT_MAX_BUFFER_SLOTS == Diligent::MAX_BUFFER_SLOTS);
    static_assert(Diligent::VT_FLOAT32 != Diligent::VT_UNDEFINED);
    return static_cast<int>(Diligent::MAX_BUFFER_SLOTS);
}
