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

struct ShapedGlyph
{
    ImFontGlyph glyph;
    unsigned cluster{};
    float xOffset{};
    float yOffset{};
    float yAdvance{};
};

struct AtlasData
{
    ~AtlasData()
    {
        if (buffer)
            hb_buffer_destroy(buffer);
    }

    HarfBuzzGpuAtlas atlas;
    hb_buffer_t* buffer{hb_buffer_create()};
    ImVector<ShapedGlyph> shapedGlyphs;
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
    return atlas->FontLoaderData != nullptr && static_cast<AtlasData*>(atlas->FontLoaderData)->buffer != nullptr;
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

bool BuildGlyph(ImFontAtlas* atlas, ImFontConfig* src, ImFontBaked* baked, BakedData* bakedData,
    hb_codepoint_t glyph, ImWchar codepoint, float advance, bool encode, ImFontGlyph* outGlyph)
{
    *outGlyph = ImFontGlyph();
    outGlyph->Codepoint = codepoint;
    outGlyph->AdvanceX = advance;
    outGlyph->PackId = ImFontAtlasRectId_Invalid;
    if (!encode)
        return true;

    const EncodedGlyph* encoded = EncodeGlyph(atlas, src, glyph);
    if (!encoded)
        return false;

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

    const bool encode = !atlas->Builder || !atlas->Builder->PreloadedAllGlyphsRanges;
    return BuildGlyph(atlas, src, baked, bakedData, glyph, codepoint, advance, encode, outGlyph);
}

bool IsGlyphInSourceRanges(const ImFontConfig* src, ImWchar codepoint)
{
    if (const ImWchar* ranges = src->GlyphRanges)
    {
        bool included = false;
        for (; ranges[0] != 0; ranges += 2)
            if (codepoint >= ranges[0] && codepoint <= ranges[1])
            {
                included = true;
                break;
            }
        if (!included)
            return false;
    }

    if (const ImWchar* ranges = src->GlyphExcludeRanges)
        for (; ranges[0] != 0; ranges += 2)
            if (codepoint >= ranges[0] && codepoint <= ranges[1])
                return false;
    return true;
}

int FindSourceIndex(ImFont* font, ImWchar codepoint)
{
    for (int i = 0; i < font->Sources.Size; ++i)
    {
        auto* src = font->Sources[i];
        auto* data = static_cast<SourceData*>(src->FontLoaderData);
        hb_codepoint_t glyph = 0;
        if (IsGlyphInSourceRanges(src, codepoint) && data
            && hb_font_get_nominal_glyph(data->font, codepoint, &glyph))
            return i;
    }
    return 0;
}

BakedData* GetBakedData(ImFontBaked* baked, int sourceIndex)
{
    char* storage = static_cast<char*>(baked->FontLoaderDatas);
    for (int i = 0; i < sourceIndex; ++i)
    {
        const ImFontConfig* src = baked->OwnerFont->Sources[i];
        const ImFontLoader* loader = src->FontLoader ? src->FontLoader : baked->OwnerFont->OwnerAtlas->FontLoader;
        storage += loader->FontBakedSrcLoaderDataSize;
    }
    return reinterpret_cast<BakedData*>(storage);
}

const char* DecodeCodepoint(const char* text, const char* textEnd, ImWchar& codepoint)
{
    unsigned value = static_cast<unsigned char>(*text);
    int length = value < 0x80 ? 1 : ImTextCharFromUtf8(&value, text, textEnd);
    if (length <= 0)
        length = 1;
    codepoint = static_cast<ImWchar>(value);
    return text + length;
}

void ApplySourceConfig(ImFontConfig* src, ImFontBaked* baked, ImFontGlyph& glyph)
{
    const float refSize = baked->OwnerFont->Sources[0]->SizePixels;
    const float scale = refSize != 0.0f ? baked->Size / refSize : 1.0f;
    const float advance = ImClamp(glyph.AdvanceX, src->GlyphMinAdvanceX * scale, src->GlyphMaxAdvanceX * scale);
    if (advance != glyph.AdvanceX)
    {
        const float offset = src->PixelSnapH
            ? ImTrunc((advance - glyph.AdvanceX) * 0.5f)
            : (advance - glyph.AdvanceX) * 0.5f;
        glyph.X0 += offset;
        glyph.X1 += offset;
    }
    glyph.AdvanceX = (src->PixelSnapH ? IM_ROUND(advance) : advance) + src->GlyphExtraAdvanceX;
}

const ImVector<ShapedGlyph>& ShapeText(ImFont* font, ImFontBaked* baked, const char* text, const char* textEnd)
{
    auto* atlasData = static_cast<AtlasData*>(font->OwnerAtlas->FontLoaderData);
    atlasData->shapedGlyphs.resize(0);
    const char* runBegin = text;
    while (runBegin < textEnd)
    {
        ImWchar firstCodepoint = 0;
        const char* firstEnd = DecodeCodepoint(runBegin, textEnd, firstCodepoint);
        if (firstCodepoint == '\r')
        {
            runBegin = firstEnd;
            continue;
        }

        const int sourceIndex = FindSourceIndex(font, firstCodepoint);
        const char* runEnd = firstEnd;
        while (runEnd < textEnd)
        {
            ImWchar codepoint = 0;
            const char* next = DecodeCodepoint(runEnd, textEnd, codepoint);
            if (codepoint == '\r' || FindSourceIndex(font, codepoint) != sourceIndex)
                break;
            runEnd = next;
        }

        ImFontConfig* src = font->Sources[sourceIndex];
        BakedData* bakedData = GetBakedData(baked, sourceIndex);
        hb_buffer_clear_contents(atlasData->buffer);
        const int runLength = static_cast<int>(runEnd - runBegin);
        hb_buffer_add_utf8(atlasData->buffer, runBegin, runLength, 0, runLength);
        hb_buffer_guess_segment_properties(atlasData->buffer);
        hb_shape(bakedData->font, atlasData->buffer, nullptr, 0);

        unsigned infoCount = 0;
        unsigned positionCount = 0;
        const hb_glyph_info_t* infos = hb_buffer_get_glyph_infos(atlasData->buffer, &infoCount);
        const hb_glyph_position_t* positions = hb_buffer_get_glyph_positions(atlasData->buffer, &positionCount);
        const unsigned count = ImMin(infoCount, positionCount);
        for (unsigned i = 0; i < count; ++i)
        {
            ShapedGlyph shaped;
            const float advance = positions[i].x_advance / 64.0f;
            BuildGlyph(font->OwnerAtlas, src, baked, bakedData, infos[i].codepoint, 0, advance, true, &shaped.glyph);
            ApplySourceConfig(src, baked, shaped.glyph);
            shaped.glyph.SourceIdx = static_cast<unsigned>(sourceIndex);
            shaped.cluster = static_cast<unsigned>(runBegin - text) + infos[i].cluster;
            shaped.xOffset = positions[i].x_offset / 64.0f;
            shaped.yOffset = -positions[i].y_offset / 64.0f;
            shaped.yAdvance = -positions[i].y_advance / 64.0f;
            atlasData->shapedGlyphs.push_back(shaped);
        }
        runBegin = runEnd;
    }
    return atlasData->shapedGlyphs;
}

float GetClusterAdvance(const ImVector<ShapedGlyph>& glyphs, unsigned cluster)
{
    float result = 0.0f;
    for (const ShapedGlyph& glyph : glyphs)
        if (glyph.cluster == cluster)
            result += glyph.glyph.AdvanceX;
    return result;
}

const char* FindLineEnd(const char* text, const char* textEnd)
{
    const void* newline = ImMemchr(text, '\n', static_cast<size_t>(textEnd - text));
    return newline ? static_cast<const char*>(newline) : textEnd;
}

const char* FontCalcWordWrapPosition(ImFont* font, float size, const char* text, const char* textEnd,
    float wrapWidth, ImDrawTextFlags flags)
{
    const char* lineEnd = FindLineEnd(text, textEnd);
    ImFontBaked* baked = font->GetFontBaked(size);
    const ImVector<ShapedGlyph>& glyphs = ShapeText(font, baked, text, lineEnd);
    const float scale = size / baked->Size;
    const float limit = wrapWidth / scale;
    const bool keepBlanks = (flags & ImDrawTextFlags_WrapKeepBlanks) != 0;
    const char* lastWrap = nullptr;
    float width = 0.0f;
    for (const char* cursor = text; cursor < lineEnd;)
    {
        ImWchar codepoint = 0;
        const char* next = DecodeCodepoint(cursor, lineEnd, codepoint);
        const float advance = GetClusterAdvance(glyphs, static_cast<unsigned>(cursor - text));
        if (width + advance > limit)
        {
            if (lastWrap && lastWrap > text)
                return lastWrap;
            return cursor == text ? next : cursor;
        }
        width += advance;

        const bool blank = codepoint == ' ' || codepoint == '\t';
        const bool punctuation = codepoint == '.' || codepoint == ',' || codepoint == ';'
            || codepoint == '!' || codepoint == '?' || codepoint == '"';
        if (blank)
            lastWrap = keepBlanks ? next : cursor;
        else if (punctuation)
            lastWrap = next;
        cursor = next;
    }
    return lineEnd;
}

ImVec2 FontCalcTextSize(ImFont* font, float size, float maxWidth, float wrapWidth,
    const char* textBegin, const char* textEndDisplay, const char* textEnd, const char** outRemaining,
    ImVec2* outOffset, ImDrawTextFlags flags)
{
    if (!textEndDisplay)
        textEndDisplay = textEnd;
    ImFontBaked* baked = font->GetFontBaked(size);
    const float scale = size / baked->Size;
    const float lineHeight = size;
    ImVec2 textSize(0.0f, 0.0f);
    float lineWidth = 0.0f;
    const bool wordWrap = wrapWidth > 0.0f;
    const char* cursor = textBegin;
    while (cursor < textEndDisplay)
    {
        const char* lineEnd = FindLineEnd(cursor, textEndDisplay);
        const char* spanEnd = wordWrap
            ? FontCalcWordWrapPosition(font, size, cursor, lineEnd, wrapWidth - lineWidth, flags)
            : lineEnd;
        const ImVector<ShapedGlyph>& glyphs = ShapeText(font, baked, cursor, spanEnd);
        float spanWidth = 0.0f;
        for (const ShapedGlyph& glyph : glyphs)
            spanWidth += glyph.glyph.AdvanceX * scale;

        if (lineWidth + spanWidth >= maxWidth)
        {
            float acceptedWidth = 0.0f;
            for (const char* character = cursor; character < spanEnd;)
            {
                ImWchar codepoint = 0;
                const char* next = DecodeCodepoint(character, spanEnd, codepoint);
                const float advance = GetClusterAdvance(glyphs, static_cast<unsigned>(character - cursor)) * scale;
                if (lineWidth + acceptedWidth + advance >= maxWidth)
                {
                    lineWidth += acceptedWidth;
                    cursor = character;
                    goto done;
                }
                acceptedWidth += advance;
                character = next;
            }
        }
        lineWidth += spanWidth;
        cursor = spanEnd;

        if (wordWrap && cursor < lineEnd)
        {
            textSize.x = ImMax(textSize.x, lineWidth);
            textSize.y += lineHeight;
            lineWidth = 0.0f;
            cursor = ImTextCalcWordWrapNextLineStart(cursor, lineEnd, flags);
            if (flags & ImDrawTextFlags_StopOnNewLine)
                break;
            continue;
        }
        if (lineEnd < textEndDisplay)
        {
            textSize.x = ImMax(textSize.x, lineWidth);
            textSize.y += lineHeight;
            lineWidth = 0.0f;
            cursor = lineEnd + 1;
            if (flags & ImDrawTextFlags_StopOnNewLine)
                break;
            continue;
        }
        cursor = lineEnd;
    }

done:
    textSize.x = ImMax(textSize.x, lineWidth);
    if (outOffset)
        *outOffset = ImVec2(lineWidth, textSize.y + lineHeight);
    if (lineWidth > 0.0f || textSize.y == 0.0f)
        textSize.y += lineHeight;
    if (outRemaining)
        *outRemaining = cursor;
    return textSize;
}

void RenderShapedSpan(ImFont* font, ImFontBaked* baked, ImDrawList* drawList, float size,
    float& x, float& y, ImU32 color, const ImVec4& clipRect, const char* text, const char* textEnd,
    ImDrawTextFlags flags)
{
    const ImVector<ShapedGlyph>& glyphs = ShapeText(font, baked, text, textEnd);
    const float scale = size / baked->Size;
    const bool fineClip = (flags & ImDrawTextFlags_CpuFineClip) != 0;
    const ImU32 untintedColor = color | ~IM_COL32_A_MASK;
    for (const ShapedGlyph& shaped : glyphs)
    {
        const ImFontGlyph& glyph = shaped.glyph;
        if (glyph.Visible)
        {
            float x1 = x + (glyph.X0 + shaped.xOffset) * scale;
            float x2 = x + (glyph.X1 + shaped.xOffset) * scale;
            float y1 = y + (glyph.Y0 + shaped.yOffset) * scale;
            float y2 = y + (glyph.Y1 + shaped.yOffset) * scale;
            float u1 = glyph.U0;
            float v1 = glyph.V0;
            float u2 = glyph.U1;
            float v2 = glyph.V1;
            if (x1 <= clipRect.z && x2 >= clipRect.x && y1 <= clipRect.w && y2 >= clipRect.y)
            {
                if (fineClip)
                {
                    if (x1 < clipRect.x) { u1 += (1.0f - (x2 - clipRect.x) / (x2 - x1)) * (u2 - u1); x1 = clipRect.x; }
                    if (y1 < clipRect.y) { v1 += (1.0f - (y2 - clipRect.y) / (y2 - y1)) * (v2 - v1); y1 = clipRect.y; }
                    if (x2 > clipRect.z) { u2 = u1 + ((clipRect.z - x1) / (x2 - x1)) * (u2 - u1); x2 = clipRect.z; }
                    if (y2 > clipRect.w) { v2 = v1 + ((clipRect.w - y1) / (y2 - y1)) * (v2 - v1); y2 = clipRect.w; }
                }
                if (x1 < x2 && y1 < y2)
                {
                    drawList->PrimReserve(6, 4);
                    drawList->PrimRectUV(ImVec2(x1, y1), ImVec2(x2, y2), ImVec2(u1, v1), ImVec2(u2, v2),
                        glyph.Colored ? untintedColor : color);
                }
            }
        }
        x += glyph.AdvanceX * scale;
        y += shaped.yAdvance * scale;
    }
}

void FontRenderText(ImFont* font, ImDrawList* drawList, float size, const ImVec2& position, ImU32 color,
    const ImVec4& clipRect, const char* text, const char* textEnd, float wrapWidth, ImDrawTextFlags flags)
{
    float x = position.x;
    float y = position.y;
    if ((drawList->Flags & ImDrawListFlags_TextNoPixelSnap) == 0)
    {
        x = IM_TRUNC(x);
        y = IM_TRUNC(y);
    }
    const float originX = x;
    const float lineHeight = size;
    const bool wordWrap = wrapWidth > 0.0f;
    ImFontBaked* baked = font->GetFontBaked(size);
    const char* cursor = text;
    while (cursor < textEnd && y <= clipRect.w)
    {
        const char* lineEnd = FindLineEnd(cursor, textEnd);
        const char* spanEnd = wordWrap
            ? FontCalcWordWrapPosition(font, size, cursor, lineEnd, wrapWidth - (x - originX), flags)
            : lineEnd;
        RenderShapedSpan(font, baked, drawList, size, x, y, color, clipRect, cursor, spanEnd, flags);
        cursor = spanEnd;
        if (wordWrap && cursor < lineEnd)
        {
            x = originX;
            y += lineHeight;
            cursor = ImTextCalcWordWrapNextLineStart(cursor, lineEnd, flags);
            continue;
        }
        if (lineEnd < textEnd)
        {
            x = originX;
            y += lineHeight;
            cursor = lineEnd + 1;
            continue;
        }
        cursor = lineEnd;
    }
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
    loader.FontCalcWordWrapPosition = FontCalcWordWrapPosition;
    loader.FontCalcTextSize = FontCalcTextSize;
    loader.FontRenderText = FontRenderText;
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
