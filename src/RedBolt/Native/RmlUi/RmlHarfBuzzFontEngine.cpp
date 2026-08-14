#include "RmlHarfBuzzFontEngine.hpp"

#include <algorithm>
#include <array>
#include <cctype>
#include <cmath>
#include <cstdint>
#include <limits>
#include <memory>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <utility>
#include <vector>

#include <hb-ot.h>
#include <hb-gpu.h>

#include <HarfBuzzGpuAtlas.hpp>

#include <RmlUi/Core/CallbackTexture.h>
#include <RmlUi/Core/Core.h>
#include <RmlUi/Core/FileInterface.h>
#include <RmlUi/Core/Log.h>
#include <RmlUi/Core/MeshUtilities.h>
#include <RmlUi/Core/RenderManager.h>
#include <RmlUi/Core/StringUtilities.h>

namespace RedBoltNative
{

namespace
{

constexpr float HarfBuzzScale = 64.0f;
constexpr int AtlasTransportWidth = 2048;
constexpr std::array<std::uint8_t, 12> AtlasTransportSignature{
    'R', 'B', 'H', 0xff, 'B', 'G', 'P', 0xff, 'U', '1', 0, 0xff};

std::string ToLower(std::string value)
{
    std::transform(value.begin(), value.end(), value.begin(), [](unsigned char character) {
        return static_cast<char>(std::tolower(character));
    });
    return value;
}

std::string GetFaceName(hb_face_t* face, hb_ot_name_id_t nameId)
{
    const unsigned int fullLength = hb_ot_name_get_utf8(face, nameId, HB_LANGUAGE_INVALID, nullptr, nullptr);
    if (fullLength == 0)
        return {};

    std::string result(fullLength + 1, '\0');
    unsigned int outputLength = static_cast<unsigned int>(result.size());
    hb_ot_name_get_utf8(face, nameId, HB_LANGUAGE_INVALID, &outputLength, result.data());
    result.resize(outputLength);
    return result;
}

Rml::Style::FontWeight GetFaceWeight(hb_face_t* face)
{
    hb_font_t* font = hb_font_create(face);
    hb_ot_font_set_funcs(font);
    const float value = hb_style_get_value(font, HB_STYLE_TAG_WEIGHT);
    hb_font_destroy(font);
    const int weight = std::clamp(static_cast<int>(std::lround(value)), 1, 1000);
    return static_cast<Rml::Style::FontWeight>(weight);
}

Rml::Style::FontStyle GetFaceStyle(hb_face_t* face)
{
    hb_font_t* font = hb_font_create(face);
    hb_ot_font_set_funcs(font);
    const float italic = hb_style_get_value(font, HB_STYLE_TAG_ITALIC);
    const float slant = hb_style_get_value(font, HB_STYLE_TAG_SLANT_ANGLE);
    hb_font_destroy(font);
    return italic != 0.0f || slant != 0.0f ? Rml::Style::FontStyle::Italic : Rml::Style::FontStyle::Normal;
}

struct Glyph
{
    hb_codepoint_t codepoint{};
    int bearingX{};
    int bearingY{};
    int width{};
    int height{};
    int atlasOffset{};
};

struct Face
{
    ~Face()
    {
        if (gpuDraw)
            hb_gpu_draw_destroy(gpuDraw);
        if (gpuFont)
            hb_font_destroy(gpuFont);
        if (face)
            hb_face_destroy(face);
        if (blob)
            hb_blob_destroy(blob);
    }

    hb_blob_t* blob{};
    hb_face_t* face{};
    hb_font_t* gpuFont{};
    hb_gpu_draw_t* gpuDraw{};
    int unitsPerEm{};
    HarfBuzzGpuAtlas atlas;
    std::unordered_map<hb_codepoint_t, Glyph> glyphs;
    std::unordered_set<hb_codepoint_t> failedGlyphs;
    std::shared_ptr<std::vector<std::uint8_t>> atlasTransport;
    std::size_t atlasTransportEncodedBytes{};
    int atlasTransportHeight{};
    std::unique_ptr<Rml::CallbackTextureSource> atlasSource;
    bool atlasDirty{};
    int version{};
    std::string family;
    Rml::Style::FontStyle style{Rml::Style::FontStyle::Normal};
    Rml::Style::FontWeight weight{Rml::Style::FontWeight::Normal};
    bool fallback{};
};

struct ShapedGlyph
{
    hb_codepoint_t codepoint{};
    hb_position_t xAdvance{};
    hb_position_t yAdvance{};
    hb_position_t xOffset{};
    hb_position_t yOffset{};
};

struct FontHandle
{
    ~FontHandle()
    {
        if (font)
            hb_font_destroy(font);
    }

    Face* face{};
    hb_font_t* font{};
    int size{};
    Rml::FontMetrics metrics{};
};

std::vector<ShapedGlyph> Shape(FontHandle& handle, Rml::StringView string, const Rml::TextShapingContext& context)
{
    hb_buffer_t* buffer = hb_buffer_create();
    hb_buffer_add_utf8(buffer, string.begin(), static_cast<int>(string.size()), 0, static_cast<int>(string.size()));

    if (!context.language.empty())
        hb_buffer_set_language(buffer, hb_language_from_string(context.language.c_str(), static_cast<int>(context.language.size())));

    if (context.text_direction == Rml::Style::Direction::Ltr)
        hb_buffer_set_direction(buffer, HB_DIRECTION_LTR);
    else if (context.text_direction == Rml::Style::Direction::Rtl)
        hb_buffer_set_direction(buffer, HB_DIRECTION_RTL);

    hb_buffer_guess_segment_properties(buffer);

    hb_feature_t feature{};
    const hb_feature_t* features = nullptr;
    unsigned int featureCount = 0;
    if (context.font_kerning == Rml::Style::FontKerning::None)
    {
        feature.tag = HB_TAG('k', 'e', 'r', 'n');
        feature.value = 0;
        feature.start = 0;
        feature.end = std::numeric_limits<unsigned int>::max();
        features = &feature;
        featureCount = 1;
    }

    hb_shape(handle.font, buffer, features, featureCount);

    unsigned int glyphCount = 0;
    const hb_glyph_info_t* infos = hb_buffer_get_glyph_infos(buffer, &glyphCount);
    const hb_glyph_position_t* positions = hb_buffer_get_glyph_positions(buffer, &glyphCount);
    std::vector<ShapedGlyph> result;
    result.reserve(glyphCount);
    for (unsigned int index = 0; index < glyphCount; ++index)
    {
        result.push_back({
            infos[index].codepoint,
            positions[index].x_advance,
            positions[index].y_advance,
            positions[index].x_offset,
            positions[index].y_offset,
        });
    }

    hb_buffer_destroy(buffer);
    return result;
}

void RefreshAtlasSource(Face& face)
{
    const auto atlasData = face.atlas.GetData();
    const int atlasHeight = face.atlas.GetHeight();
    if (!face.atlasTransport || face.atlasTransportHeight != atlasHeight)
    {
        face.atlasTransport = std::make_shared<std::vector<std::uint8_t>>(
            static_cast<std::size_t>(AtlasTransportWidth) * atlasHeight * 4);
        for (std::size_t alpha = 3; alpha < face.atlasTransport->size(); alpha += 4)
            (*face.atlasTransport)[alpha] = 0xff;
        std::copy(AtlasTransportSignature.begin(), AtlasTransportSignature.end(), face.atlasTransport->begin());
        face.atlasTransportEncodedBytes = 0;
        face.atlasTransportHeight = atlasHeight;
    }

    const std::size_t usedBytes = static_cast<std::size_t>(face.atlas.GetUsedTexels()) * HarfBuzzGpuAtlas::TexelSize;
    for (std::size_t index = face.atlasTransportEncodedBytes; index < usedBytes; ++index)
        (*face.atlasTransport)[AtlasTransportSignature.size() + (index / 3) * 4 + index % 3] = (*atlasData)[index];
    face.atlasTransportEncodedBytes = usedBytes;
    const auto pixels = face.atlasTransport;
    const Rml::Vector2i callbackDimensions(AtlasTransportWidth, atlasHeight);
    face.atlasSource = std::make_unique<Rml::CallbackTextureSource>(
        [pixels, callbackDimensions](const Rml::CallbackTextureInterface& textureInterface) {
            return textureInterface.GenerateTexture(
                Rml::Span<const Rml::byte>(reinterpret_cast<const Rml::byte*>(pixels->data()), pixels->size()),
                callbackDimensions);
        });
    face.atlasDirty = false;
}

const Glyph* EncodeGlyph(Face& face, hb_codepoint_t codepoint)
{
    if (const auto found = face.glyphs.find(codepoint); found != face.glyphs.end())
        return &found->second;
    if (face.failedGlyphs.contains(codepoint))
        return nullptr;

    if (!hb_gpu_draw_glyph_or_fail(face.gpuDraw, face.gpuFont, codepoint))
    {
        Rml::Log::Message(Rml::Log::LT_ERROR,
            "HarfBuzz GPU glyph failure for RmlUI font %s, glyph %u: outline encoding failed",
            face.family.c_str(), codepoint);
        face.failedGlyphs.insert(codepoint);
        return nullptr;
    }

    hb_glyph_extents_t extents{};
    hb_blob_t* blob = hb_gpu_draw_encode(face.gpuDraw, &extents);
    if (!blob)
    {
        Rml::Log::Message(Rml::Log::LT_ERROR,
            "HarfBuzz GPU glyph failure for RmlUI font %s, glyph %u: blob encoding failed",
            face.family.c_str(), codepoint);
        face.failedGlyphs.insert(codepoint);
        return nullptr;
    }

    Glyph glyph{codepoint, extents.x_bearing, extents.y_bearing, extents.width, -extents.height, 0};
    const unsigned int length = hb_blob_get_length(blob);
    bool success = true;
    if (length != 0)
    {
        unsigned int dataLength = 0;
        const char* data = hb_blob_get_data(blob, &dataLength);
        success = data && dataLength == length && face.atlas.Append(data, length, glyph.atlasOffset);
    }
    hb_gpu_draw_recycle_blob(face.gpuDraw, blob);

    if (!success)
    {
        Rml::Log::Message(Rml::Log::LT_ERROR,
            "HarfBuzz GPU glyph failure for RmlUI font %s, glyph %u: atlas overflow or malformed blob",
            face.family.c_str(), codepoint);
        face.failedGlyphs.insert(codepoint);
        return nullptr;
    }

    const auto [inserted, unused] = face.glyphs.emplace(codepoint, glyph);
    if (length != 0)
    {
        face.atlasDirty = true;
        ++face.version;
    }
    return &inserted->second;
}

void PopulateMetrics(FontHandle& handle)
{
    handle.metrics.size = handle.size;

    hb_font_extents_t extents{};
    if (hb_font_get_h_extents(handle.font, &extents))
    {
        handle.metrics.ascent = static_cast<float>(extents.ascender) / HarfBuzzScale;
        handle.metrics.descent = static_cast<float>(-extents.descender) / HarfBuzzScale;
        handle.metrics.line_spacing =
            static_cast<float>(extents.ascender - extents.descender + extents.line_gap) / HarfBuzzScale;
    }

    hb_position_t position = 0;
    hb_ot_metrics_get_position_with_fallback(handle.font, HB_OT_METRICS_TAG_X_HEIGHT, &position);
    handle.metrics.x_height = static_cast<float>(position) / HarfBuzzScale;
    hb_ot_metrics_get_position_with_fallback(handle.font, HB_OT_METRICS_TAG_UNDERLINE_OFFSET, &position);
    handle.metrics.underline_position = static_cast<float>(-position) / HarfBuzzScale;
    hb_ot_metrics_get_position_with_fallback(handle.font, HB_OT_METRICS_TAG_UNDERLINE_SIZE, &position);
    handle.metrics.underline_thickness = static_cast<float>(position) / HarfBuzzScale;

    hb_codepoint_t ellipsis = 0;
    handle.metrics.has_ellipsis = hb_font_get_nominal_glyph(handle.font, 0x2026, &ellipsis);
}

} // namespace

class RmlHarfBuzzFontEngine::Impl
{
public:
    bool AddFace(Rml::Span<const Rml::byte> data, int faceIndex, std::string family, Rml::Style::FontStyle style,
        Rml::Style::FontWeight weight, bool fallback)
    {
        hb_blob_t* blob = hb_blob_create(reinterpret_cast<const char*>(data.data()), static_cast<unsigned int>(data.size()),
            HB_MEMORY_MODE_DUPLICATE, nullptr, nullptr);
        if (!blob)
            return false;

        hb_face_t* harfBuzzFace = hb_face_create(blob, static_cast<unsigned int>(faceIndex));
        if (!harfBuzzFace || hb_face_get_upem(harfBuzzFace) == 0)
        {
            if (harfBuzzFace)
                hb_face_destroy(harfBuzzFace);
            hb_blob_destroy(blob);
            return false;
        }

        if (family.empty())
        {
            family = GetFaceName(harfBuzzFace, HB_OT_NAME_ID_TYPOGRAPHIC_FAMILY);
            if (family.empty())
                family = GetFaceName(harfBuzzFace, HB_OT_NAME_ID_FONT_FAMILY);
            style = GetFaceStyle(harfBuzzFace);
        }
        if (weight == Rml::Style::FontWeight::Auto)
            weight = GetFaceWeight(harfBuzzFace);
        if (family.empty())
            family = "default";

        auto face = std::make_unique<Face>();
        face->blob = blob;
        face->face = harfBuzzFace;
        face->unitsPerEm = static_cast<int>(hb_face_get_upem(harfBuzzFace));
        face->gpuFont = hb_font_create(harfBuzzFace);
        if (face->gpuFont)
        {
            hb_ot_font_set_funcs(face->gpuFont);
            hb_font_set_scale(face->gpuFont, face->unitsPerEm, face->unitsPerEm);
            face->gpuDraw = hb_gpu_draw_create_or_fail();
        }
        if (!face->gpuFont || !face->gpuDraw)
        {
            Rml::Log::Message(Rml::Log::LT_ERROR,
                "HarfBuzz GPU encoder initialization failed for RmlUI font %s", family.c_str());
            return false;
        }
        face->family = ToLower(std::move(family));
        face->style = style;
        face->weight = weight;
        face->fallback = fallback;
        faces.push_back(std::move(face));
        return true;
    }

    FontHandle* FindHandle(const std::string& family, Rml::Style::FontStyle style, Rml::Style::FontWeight weight, int size)
    {
        Face* selectedFace = nullptr;
        int selectedDistance = std::numeric_limits<int>::max();
        const int desiredWeight =
            weight == Rml::Style::FontWeight::Auto ? static_cast<int>(Rml::Style::FontWeight::Normal) : static_cast<int>(weight);
        for (const auto& face : faces)
        {
            if (face->family != family)
                continue;
            const int stylePenalty = face->style == style ? 0 : 10000;
            const int distance = stylePenalty + std::abs(static_cast<int>(face->weight) - desiredWeight);
            if (distance < selectedDistance)
            {
                selectedFace = face.get();
                selectedDistance = distance;
            }
        }
        if (!selectedFace)
            return nullptr;

        for (const auto& handle : handles)
        {
            if (handle->face == selectedFace && handle->size == size)
                return handle.get();
        }

        auto handle = std::make_unique<FontHandle>();
        handle->face = selectedFace;
        handle->font = hb_font_create(selectedFace->face);
        handle->size = size;
        hb_ot_font_set_funcs(handle->font);
        hb_font_set_scale(handle->font, size * static_cast<int>(HarfBuzzScale), size * static_cast<int>(HarfBuzzScale));
        hb_font_set_ppem(handle->font, static_cast<unsigned int>(size), static_cast<unsigned int>(size));
        PopulateMetrics(*handle);

        FontHandle* result = handle.get();
        handles.push_back(std::move(handle));
        return result;
    }

    std::vector<std::unique_ptr<Face>> faces;
    std::vector<std::unique_ptr<FontHandle>> handles;
};

RmlHarfBuzzFontEngine::RmlHarfBuzzFontEngine() : impl_(std::make_unique<Impl>()) {}

RmlHarfBuzzFontEngine::~RmlHarfBuzzFontEngine() = default;

void RmlHarfBuzzFontEngine::Initialize() {}

void RmlHarfBuzzFontEngine::Shutdown()
{
    ReleaseFontResources();
    impl_->faces.clear();
}

bool RmlHarfBuzzFontEngine::LoadFontFace(
    const Rml::String& fileName, int faceIndex, bool fallbackFace, Rml::Style::FontWeight weight)
{
    Rml::FileInterface* fileInterface = Rml::GetFileInterface();
    const Rml::FileHandle file = fileInterface->Open(fileName);
    if (!file)
        return false;

    const size_t length = fileInterface->Length(file);
    std::vector<Rml::byte> data(length);
    const size_t bytesRead = fileInterface->Read(data.data(), data.size(), file);
    fileInterface->Close(file);
    if (bytesRead != length)
        return false;

    return impl_->AddFace(Rml::Span<const Rml::byte>(data.data(), data.size()), faceIndex, {}, Rml::Style::FontStyle::Normal,
        weight, fallbackFace);
}

bool RmlHarfBuzzFontEngine::LoadFontFace(Rml::Span<const Rml::byte> data, int faceIndex, const Rml::String& family,
    Rml::Style::FontStyle style, Rml::Style::FontWeight weight, bool fallbackFace)
{
    return impl_->AddFace(data, faceIndex, family, style, weight, fallbackFace);
}

Rml::FontFaceHandle RmlHarfBuzzFontEngine::GetFontFaceHandle(
    const Rml::String& family, Rml::Style::FontStyle style, Rml::Style::FontWeight weight, int size)
{
    FontHandle* handle = impl_->FindHandle(ToLower(family), style, weight, size);
    return reinterpret_cast<Rml::FontFaceHandle>(handle);
}

Rml::FontEffectsHandle RmlHarfBuzzFontEngine::PrepareFontEffects(
    Rml::FontFaceHandle, const Rml::FontEffectList&)
{
    return {};
}

const Rml::FontMetrics& RmlHarfBuzzFontEngine::GetFontMetrics(Rml::FontFaceHandle handle)
{
    return reinterpret_cast<FontHandle*>(handle)->metrics;
}

int RmlHarfBuzzFontEngine::GetStringWidth(Rml::FontFaceHandle handle, Rml::StringView string,
    const Rml::TextShapingContext& textShapingContext, Rml::Character priorCharacter)
{
    FontHandle& fontHandle = *reinterpret_cast<FontHandle*>(handle);
    const std::vector<ShapedGlyph> glyphs = Shape(fontHandle, string, textShapingContext);
    for (const ShapedGlyph& glyph : glyphs)
    {
        if (!EncodeGlyph(*fontHandle.face, glyph.codepoint))
            return 0;
    }
    float width = 0.0f;
    for (const ShapedGlyph& glyph : glyphs)
        width += static_cast<float>(glyph.xAdvance) / HarfBuzzScale + textShapingContext.letter_spacing;

    if (priorCharacter != Rml::Character::Null && !string.empty() &&
        textShapingContext.font_kerning != Rml::Style::FontKerning::None)
    {
        hb_codepoint_t priorGlyph = 0;
        hb_codepoint_t currentGlyph = 0;
        const Rml::Character currentCharacter = *Rml::StringIteratorU8(string);
        if (hb_font_get_nominal_glyph(fontHandle.font, static_cast<hb_codepoint_t>(priorCharacter), &priorGlyph) &&
            hb_font_get_nominal_glyph(fontHandle.font, static_cast<hb_codepoint_t>(currentCharacter), &currentGlyph))
        {
            width += static_cast<float>(hb_font_get_glyph_h_kerning(fontHandle.font, priorGlyph, currentGlyph)) / HarfBuzzScale;
        }
    }

    return std::max(static_cast<int>(std::lround(width)), 0);
}

int RmlHarfBuzzFontEngine::GenerateString(Rml::RenderManager& renderManager, Rml::FontFaceHandle faceHandle,
    Rml::FontEffectsHandle, Rml::StringView string, Rml::Vector2f position, Rml::ColourbPremultiplied colour, float,
    const Rml::TextShapingContext& textShapingContext, Rml::TexturedMeshList& meshList)
{
    FontHandle& handle = *reinterpret_cast<FontHandle*>(faceHandle);
    const std::vector<ShapedGlyph> shapedGlyphs = Shape(handle, string, textShapingContext);
    for (const ShapedGlyph& shapedGlyph : shapedGlyphs)
    {
        if (!EncodeGlyph(*handle.face, shapedGlyph.codepoint))
            return 0;
    }
    if (handle.face->atlasDirty)
        RefreshAtlasSource(*handle.face);

    Rml::TexturedMesh* texturedMesh = nullptr;
    if (handle.face->atlasSource)
    {
        meshList.emplace_back();
        texturedMesh = &meshList.back();
        texturedMesh->texture = handle.face->atlasSource->GetTexture(renderManager);
        texturedMesh->mesh.vertices.reserve(shapedGlyphs.size() * 4);
        texturedMesh->mesh.indices.reserve(shapedGlyphs.size() * 6);
    }

    Rml::Vector2f cursor = position;
    for (const ShapedGlyph& shapedGlyph : shapedGlyphs)
    {
        const Glyph& glyph = handle.face->glyphs.at(shapedGlyph.codepoint);
        if (texturedMesh && glyph.atlasOffset != 0 && glyph.width > 0 && glyph.height > 0)
        {
            const float scale = static_cast<float>(handle.size) / handle.face->unitsPerEm;
            const float width = glyph.width * scale;
            const float height = glyph.height * scale;
            const float paddingX = std::min(1.0f, width * 0.25f);
            const float paddingY = std::min(1.0f, height * 0.25f);
            const Rml::Vector2f unpaddedOrigin(
                cursor.x + static_cast<float>(shapedGlyph.xOffset) / HarfBuzzScale + glyph.bearingX * scale,
                cursor.y - static_cast<float>(shapedGlyph.yOffset) / HarfBuzzScale - glyph.bearingY * scale);
            const Rml::Vector2f origin = unpaddedOrigin.Round() - Rml::Vector2f(paddingX, paddingY);
            const Rml::Vector2f dimensions(width + 2.0f * paddingX, height + 2.0f * paddingY);
            const float localLeft = -paddingX / width;
            const float localTop = -paddingY / height;
            const float localRight = 1.0f + paddingX / width;
            const float localBottom = 1.0f + paddingY / height;
            const int lowOffset = glyph.atlasOffset & 0x1ff;
            const int highOffset = glyph.atlasOffset >> 9;
            const Rml::Vector2f topLeft(
                PackHarfBuzzGpuCoordinate(lowOffset, localLeft),
                PackHarfBuzzGpuCoordinate(highOffset, localTop));
            const Rml::Vector2f bottomRight(
                PackHarfBuzzGpuCoordinate(lowOffset, localRight),
                PackHarfBuzzGpuCoordinate(highOffset, localBottom));
            Rml::MeshUtilities::GenerateQuad(texturedMesh->mesh, origin, dimensions, colour, topLeft, bottomRight);
        }
        cursor.x += static_cast<float>(shapedGlyph.xAdvance) / HarfBuzzScale + textShapingContext.letter_spacing;
        cursor.y -= static_cast<float>(shapedGlyph.yAdvance) / HarfBuzzScale;
    }

    return std::max(static_cast<int>(std::lround(cursor.x - position.x)), 0);
}

int RmlHarfBuzzFontEngine::GetVersion(Rml::FontFaceHandle handle)
{
    return reinterpret_cast<FontHandle*>(handle)->face->version;
}

void RmlHarfBuzzFontEngine::ReleaseFontResources()
{
    impl_->handles.clear();
}

} // namespace RedBoltNative
