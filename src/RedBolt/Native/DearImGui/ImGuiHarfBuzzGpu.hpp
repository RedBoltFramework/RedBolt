#pragma once

struct ImFontAtlas;
struct ImFontLoader;

namespace RedBoltNative
{
class HarfBuzzGpuAtlas;

const ImFontLoader* GetImGuiHarfBuzzGpuFontLoader();
HarfBuzzGpuAtlas* GetImGuiHarfBuzzGpuAtlas(ImFontAtlas* atlas);
}
