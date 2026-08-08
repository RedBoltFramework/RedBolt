#pragma once

#include <algorithm>
#include <array>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <memory>
#include <vector>

namespace RedBoltNative
{

class HarfBuzzGpuAtlas
{
public:
    static constexpr int Width = 512;
    static constexpr int InitialHeight = 8;
    static constexpr int MaximumHeight = 512;
    static constexpr int TexelSize = 8;
    static constexpr int MaximumTexels = Width * MaximumHeight;
    static constexpr std::array<std::uint8_t, TexelSize> Signature{'R', 'B', 'H', 'B', 'G', 'P', 'U', '1'};

    HarfBuzzGpuAtlas() : data_(std::make_shared<std::vector<std::uint8_t>>(Width * InitialHeight * TexelSize))
    {
        std::copy(Signature.begin(), Signature.end(), data_->begin());
    }

    bool Append(const void* source, std::size_t numBytes, int& offset)
    {
        offset = 0;
        if (!source || numBytes == 0 || numBytes % TexelSize != 0)
            return false;

        const int numTexels = static_cast<int>(numBytes / TexelSize);
        if (numTexels > MaximumTexels - usedTexels_)
            return false;

        const int requiredTexels = usedTexels_ + numTexels;
        int requiredHeight = height_;
        while (requiredHeight * Width < requiredTexels)
            requiredHeight *= 2;
        if (requiredHeight > MaximumHeight)
            return false;

        if (requiredHeight != height_)
        {
            auto grown = std::make_shared<std::vector<std::uint8_t>>(Width * requiredHeight * TexelSize);
            std::copy(data_->begin(), data_->end(), grown->begin());
            data_ = std::move(grown);
            height_ = requiredHeight;
        }

        offset = usedTexels_;
        std::memcpy(data_->data() + static_cast<std::size_t>(offset) * TexelSize, source, numBytes);
        dirtyTexel_ = std::min(dirtyTexel_, offset);
        usedTexels_ = requiredTexels;
        ++version_;
        return true;
    }

    int GetHeight() const { return height_; }
    int GetUsedTexels() const { return usedTexels_; }
    int GetDirtyTexel() const { return dirtyTexel_; }
    std::uint64_t GetVersion() const { return version_; }
    const std::shared_ptr<std::vector<std::uint8_t>>& GetData() const { return data_; }

private:
    std::shared_ptr<std::vector<std::uint8_t>> data_;
    int height_{InitialHeight};
    int usedTexels_{1};
    int dirtyTexel_{MaximumTexels};
    std::uint64_t version_{};
};

inline float PackHarfBuzzGpuCoordinate(int atlasOffsetPart, float localCoordinate)
{
    return 2.0f + static_cast<float>(atlasOffsetPart) + 0.25f + 0.5f * localCoordinate;
}

} // namespace RedBoltNative
