#include <SDL.h>
#include <SDL_version.h>

#include <Constants.h>
#include <GraphicsTypes.h>

#include <cstdio>

int main()
{
    SDL_version version{};
    SDL_GetVersion(&version);

    if (version.major < 2 || Diligent::MAX_BUFFER_SLOTS <= 0)
    {
        std::fprintf(stderr, "Invalid dependency versions: SDL %d.%d.%d, Diligent slots %u\n",
            version.major, version.minor, version.patch, Diligent::MAX_BUFFER_SLOTS);
        return 1;
    }

    if (SDL_Init(0) != 0)
    {
        std::fprintf(stderr, "SDL_Init failed: %s\n", SDL_GetError());
        return 2;
    }

    SDL_Quit();
    std::printf("RedBolt native dependency smoke OK: SDL %d.%d.%d, Diligent max buffer slots %u\n",
        version.major, version.minor, version.patch, Diligent::MAX_BUFFER_SLOTS);
    return 0;
}
