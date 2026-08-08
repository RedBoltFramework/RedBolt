#include "ImGuiHarfBuzzGpu.hpp"

#include <RedBolt/HarfBuzzGpuAtlas.hpp>

#include <hb-gpu.h>
#include <hb-ot.h>
#include <imgui.h>
#include <imgui_internal.h>

#include <cmath>
#include <cstdio>
#include <new>
#include <unordered_map>
#include <unordered_set>

namespace RedBoltNative
{
namespace
{

struct EncodedGlyph
{
    int atlasOffset{};
    hb_glyph_extents_t extents{};
};

struct AtlasData
{
    HarfBuzzGpuAtlas atlas;
};

struct SourceData
{
    ~SourceData()
    {
        if (gpuDraw)
            hb_gpu_draw_destroy(gpuDraw);
        if (font)
            hb_font_destroy(font);
        if (face)
            hb_face_destroy(face);
        if (blob)
            hb_blob_destroy(blob);
    }

    hb_blob_t* blob{};
    hb_face_t* face{};
    hb_font_t* font{};
    hb_gpu_draw_t* gpuDraw{};
    int unitsPerEm{};
    float scaleFactor{};
    std::unordered_map<hb_codepoint_t, EncodedGlyph> glyphs;
    std::unordered_set<hb_codepoint_t> failedGlyphs;
};

struct BakedData
{
    hb_font_t* font{};
    float layoutScale{};
};

void LogGlyphFailure(ImFontConfig* src, ImWchar codepoint, const char* reason)
{
    std::fprintf(stderr, "HarfBuzz GPU glyph failure for font %s, glyph %u, renderer ImGui: %s\n",
        src->Name, static_cast<unsigned>(codepoint), reason);
}

bool LoaderInit(ImFontAtlas* atlas)
{
    atlas->FontLoaderData = IM_NEW(AtlasData)();
    return atlas->FontLoaderData != nullptr;
}

void LoaderShutdown(ImFontAtlas* atlas)
{
    IM_DELETE(static_cast<AtlasData*>(atlas->FontLoaderData));
    atlas->FontLoaderData = nullptr;
}

bool FontSrcInit(ImFontAtlas*, ImFontConfig* src)
{
    auto* data = IM_NEW(SourceData)();
    data->blob = hb_blob_create(static_cast<const char*>(src->FontData), static_cast<unsigned>(src->FontDataSize),
        HB_MEMORY_MODE_DUPLICATE, nullptr, nullptr);
    data->face = data->blob ? hb_face_create(data->blob, src->FontNo) : nullptr;
    data->unitsPerEm = data->face ? static_cast<int>(hb_face_get_upem(data->face)) : 0;
    data->font = data->unitsPerEm ? hb_font_create(data->face) : nullptr;
    if (data->font)
    {
        hb_ot_font_set_funcs(data->font);
        hb_font_set_scale(data->font, data->unitsPerEm, data->unitsPerEm);
    }
    data->gpuDraw = data->font ? hb_gpu_draw_create_or_fail() : nullptr;

    hb_font_extents_t metrics{};
    if (!data->font || !data->gpuDraw || !hb_font_get_h_extents(data->font, &metrics)
        || metrics.ascender == metrics.descender)
    {
        std::fprintf(stderr, "HarfBuzz GPU encoder initialization failed for font %s, renderer ImGui\n", src->Name);
        IM_DELETE(data);
        return false;
    }

    const float refSize = src->DstFont->Sources[0]->SizePixels;
    if (src->MergeMode && src->SizePixels == 0.0f)
        src->SizePixels = refSize;
    data->scaleFactor = 1.0f / static_cast<float>(metrics.ascender - metrics.descender);
    if (src->MergeMode && src->SizePixels != 0.0f && refSize != 0.0f)
        data->scaleFactor *= src->SizePixels / refSize;
    data->scaleFactor *= src->ExtraSizeScale;
    src->FontLoaderData = data;
    return true;
}

void FontSrcDestroy(ImFontAtlas*, ImFontConfig* src)
{
    IM_DELETE(static_cast<SourceData*>(src->FontLoaderData));
    src->FontLoaderData = nullptr;
}

bool FontSrcContainsGlyph(ImFontAtlas*, ImFontConfig* src, ImWchar codepoint)
{
    auto* data = static_cast<SourceData*>(src->FontLoaderData);
    hb_codepoint_t glyph = 0;
    return data && hb_font_get_nominal_glyph(data->font, codepoint, &glyph);
}

bool FontBakedInit(ImFontAtlas*, ImFontConfig* src, ImFontBaked* baked, void* storage)
{
    auto* source = static_cast<SourceData*>(src->FontLoaderData);
    auto* data = new (storage) BakedData();
    data->layoutScale = source->scaleFactor * baked->Size;
    data->font = hb_font_create(source->face);
    if (!data->font)
        return false;
    hb_ot_font_set_funcs(data->font);
    const int metricScale = std::max(1, static_cast<int>(std::lround(source->unitsPerEm * data->layoutScale * 64.0f)));
    hb_font_set_scale(data->font, metricScale, metricScale);

    if (!src->MergeMode)
    {
        hb_font_extents_t extents{};
        if (hb_font_get_h_extents(data->font, &extents))
        {
            baked->Ascent = ImCeil(extents.ascender / 64.0f);
            baked->Descent = ImFloor(extents.descender / 64.0f);
        }
    }
    return true;
}

void FontBakedDestroy(ImFontAtlas*, ImFontConfig*, ImFontBaked*, void* storage)
{
    auto* data = static_cast<BakedData*>(storage);
    if (data->font)
        hb_font_destroy(data->font);
    data->~BakedData();
}

const EncodedGlyph* EncodeGlyph(ImFontAtlas* atlas, ImFontConfig* src, hb_codepoint_t glyph)
{
    auto* source = static_cast<SourceData*>(src->FontLoaderData);
    if (const auto found = source->glyphs.find(glyph); found != source->glyphs.end())
        return &found->second;
    if (source->failedGlyphs.contains(glyph))
        return nullptr;

    if (!hb_gpu_draw_glyph_or_fail(source->gpuDraw, source->font, glyph))
    {
        LogGlyphFailure(src, glyph, "outline encoding failed");
        source->failedGlyphs.insert(glyph);
        return nullptr;
    }

    EncodedGlyph encoded;
    hb_blob_t* blob = hb_gpu_draw_encode(source->gpuDraw, &encoded.extents);
    if (!blob)
    {
        LogGlyphFailure(src, glyph, "blob encoding failed");
        source->failedGlyphs.insert(glyph);
        return nullptr;
    }

    const unsigned length = hb_blob_get_length(blob);
    bool success = true;
    if (length != 0)
    {
        unsigned dataLength = 0;
        const char* blobData = hb_blob_get_data(blob, &dataLength);
        auto* atlasData = static_cast<AtlasData*>(atlas->FontLoaderData);
        success = atlasData && blobData && dataLength == length
            && atlasData->atlas.Append(blobData, length, encoded.atlasOffset);
    }
    hb_gpu_draw_recycle_blob(source->gpuDraw, blob);

    if (!success)
    {
        LogGlyphFailure(src, glyph, "atlas overflow or malformed blob");
        source->failedGlyphs.insert(glyph);
        return nullptr;
    }
    return &source->glyphs.emplace(glyph, encoded).first->second;
}

bool FontBakedLoadGlyph(ImFontAtlas* atlas, ImFontConfig* src, ImFontBaked* baked, void* storage,
    ImWchar codepoint, ImFontGlyph* outGlyph, float* outAdvanceX)
{
    auto* source = static_cast<SourceData*>(src->FontLoaderData);
    auto* bakedData = static_cast<BakedData*>(storage);
    hb_codepoint_t glyph = 0;
    if (!hb_font_get_nominal_glyph(source->font, codepoint, &glyph))
        return false;

    const float advance = hb_font_get_glyph_h_advance(bakedData->font, glyph) / 64.0f;
    if (outAdvanceX)
    {
        *outAdvanceX = advance;
        return true;
    }

    const EncodedGlyph* encoded = EncodeGlyph(atlas, src, glyph);
    if (!encoded)
        return false;

    *outGlyph = ImFontGlyph();
    outGlyph->Codepoint = codepoint;
    outGlyph->AdvanceX = advance;
    outGlyph->PackId = ImFontAtlasRectId_Invalid;
    const float width = encoded->extents.width * bakedData->layoutScale;
    const float height = -encoded->extents.height * bakedData->layoutScale;
    if (encoded->atlasOffset == 0 || width <= 0.0f || height <= 0.0f)
        return true;

    const float refSize = baked->OwnerFont->Sources[0]->SizePixels;
    const float offsetScale = refSize != 0.0f ? baked->Size / refSize : 1.0f;
    const float fontOffsetX = ImFloor(src->GlyphOffset.x * offsetScale + 0.5f);
    const float fontOffsetY = ImFloor(src->GlyphOffset.y * offsetScale + 0.5f);
    const float paddingX = ImMin(1.0f, width * 0.25f);
    const float paddingY = ImMin(1.0f, height * 0.25f);
    const float localLeft = -paddingX / width;
    const float localTop = -paddingY / height;
    const float localRight = 1.0f + paddingX / width;
    const float localBottom = 1.0f + paddingY / height;

    outGlyph->X0 = encoded->extents.x_bearing * bakedData->layoutScale + fontOffsetX - paddingX;
    outGlyph->Y0 = baked->Ascent - encoded->extents.y_bearing * bakedData->layoutScale + fontOffsetY - paddingY;
    outGlyph->X1 = outGlyph->X0 + width + 2.0f * paddingX;
    outGlyph->Y1 = outGlyph->Y0 + height + 2.0f * paddingY;
    const int lowOffset = encoded->atlasOffset & 0x1ff;
    const int highOffset = encoded->atlasOffset >> 9;
    outGlyph->U0 = PackHarfBuzzGpuCoordinate(lowOffset, localLeft);
    outGlyph->V0 = PackHarfBuzzGpuCoordinate(highOffset, localTop);
    outGlyph->U1 = PackHarfBuzzGpuCoordinate(lowOffset, localRight);
    outGlyph->V1 = PackHarfBuzzGpuCoordinate(highOffset, localBottom);
    outGlyph->Visible = true;
    outGlyph->Colored = false;
    return true;
}

} // namespace

const ImFontLoader* GetImGuiHarfBuzzGpuFontLoader()
{
    static ImFontLoader loader;
    loader.Name = "HarfBuzz GPU";
    loader.LoaderInit = LoaderInit;
    loader.LoaderShutdown = LoaderShutdown;
    loader.FontSrcInit = FontSrcInit;
    loader.FontSrcDestroy = FontSrcDestroy;
    loader.FontSrcContainsGlyph = FontSrcContainsGlyph;
    loader.FontBakedInit = FontBakedInit;
    loader.FontBakedDestroy = FontBakedDestroy;
    loader.FontBakedLoadGlyph = FontBakedLoadGlyph;
    loader.FontBakedSrcLoaderDataSize = sizeof(BakedData);
    return &loader;
}

HarfBuzzGpuAtlas* GetImGuiHarfBuzzGpuAtlas(ImFontAtlas* atlas)
{
    if (!atlas || atlas->FontLoader != GetImGuiHarfBuzzGpuFontLoader() || !atlas->FontLoaderData)
        return nullptr;
    return &static_cast<AtlasData*>(atlas->FontLoaderData)->atlas;
}

} // namespace RedBoltNative
