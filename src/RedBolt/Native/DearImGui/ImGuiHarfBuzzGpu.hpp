// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

#pragma once

struct ImFontAtlas;
struct ImFontLoader;

namespace RedBoltNative
{
class HarfBuzzGpuAtlas;

const ImFontLoader* GetImGuiHarfBuzzGpuFontLoader();
HarfBuzzGpuAtlas* GetImGuiHarfBuzzGpuAtlas(ImFontAtlas* atlas);
}
