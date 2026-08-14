#pragma once

#include <memory>

#include <RmlUi/Core/FontEngineInterface.h>

namespace RedBoltNative
{

class RmlHarfBuzzFontEngine final : public Rml::FontEngineInterface
{
public:
    RmlHarfBuzzFontEngine();
    ~RmlHarfBuzzFontEngine() override;

    void Initialize() override;
    void Shutdown() override;

    bool LoadFontFace(const Rml::String& fileName, int faceIndex, bool fallbackFace, Rml::Style::FontWeight weight) override;
    bool LoadFontFace(Rml::Span<const Rml::byte> data, int faceIndex, const Rml::String& family, Rml::Style::FontStyle style,
        Rml::Style::FontWeight weight, bool fallbackFace) override;

    Rml::FontFaceHandle GetFontFaceHandle(
        const Rml::String& family, Rml::Style::FontStyle style, Rml::Style::FontWeight weight, int size) override;
    Rml::FontEffectsHandle PrepareFontEffects(Rml::FontFaceHandle handle, const Rml::FontEffectList& fontEffects) override;
    const Rml::FontMetrics& GetFontMetrics(Rml::FontFaceHandle handle) override;
    int GetStringWidth(Rml::FontFaceHandle handle, Rml::StringView string, const Rml::TextShapingContext& textShapingContext,
        Rml::Character priorCharacter) override;
    int GenerateString(Rml::RenderManager& renderManager, Rml::FontFaceHandle faceHandle, Rml::FontEffectsHandle fontEffectsHandle,
        Rml::StringView string, Rml::Vector2f position, Rml::ColourbPremultiplied colour, float opacity,
        const Rml::TextShapingContext& textShapingContext, Rml::TexturedMeshList& meshList) override;
    int GetVersion(Rml::FontFaceHandle handle) override;
    void ReleaseFontResources() override;

private:
    class Impl;
    std::unique_ptr<Impl> impl_;
};

} // namespace RedBoltNative
