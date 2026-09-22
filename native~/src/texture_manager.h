#pragma once

#include <cstddef>
#include <cstdint>
#include <mutex>
#include <string>

struct ID3D11Device;
struct ID3D11DeviceContext;
struct ID3D11Texture2D;
struct IDXGIAdapter1;

/** Simple rect for batch copy; matches CefRect layout (x, y, width, height). */
struct CeffyRect {
    int x, y, w, h;
};

class TextureManager {
public:
    TextureManager(int width, int height, int64_t adapterLuid, unsigned int graphicsVendorId = 0, unsigned int graphicsDeviceId = 0);
    ~TextureManager();

    TextureManager(const TextureManager&) = delete;
    TextureManager& operator=(const TextureManager&) = delete;

    /** Copy a single rect from CEF's shared texture (legacy / one-off use). */
    void CopyFromSharedTexture(const void* sourceHandle, int x, int y, int w, int h);
    void CopyFromSharedTextureWithOffset(const void* sourceHandle,
                                         int srcX, int srcY, int srcW, int srcH,
                                         int destX, int destY);

    /** Open CEF's shared texture once and copy all dirty rects. Much cheaper when multiple rects per frame. */
    void CopyRectsFromSharedTexture(const void* sourceHandle, const CeffyRect* rects, size_t numRects);
    void CopyRectsFromSharedTextureWithOffset(const void* sourceHandle, const CeffyRect* rects, size_t numRects, int offsetX, int offsetY);
    void Resize(int newWidth, int newHeight);
    void* GetSharedHandle() const { return sharedHandle_; }
    int Width() const { return width_; }
    int Height() const { return height_; }
    const std::string& GetAdapterDescription() const { return adapterDescription_; }

private:
    void CreateDevice(int64_t adapterLuid, unsigned int graphicsVendorId, unsigned int graphicsDeviceId);
    void CreateSharedTexture();
    void FindAdapter(int64_t adapterLuid, unsigned int graphicsVendorId, unsigned int graphicsDeviceId, IDXGIAdapter1** outAdapter);

    ID3D11Device*        device_  = nullptr;
    ID3D11DeviceContext* context_ = nullptr;
    ID3D11Texture2D*     sharedTexture_    = nullptr;
    void*                sharedHandle_     = nullptr;
    ID3D11Texture2D*     pendingOldTexture_ = nullptr;
    void*                pendingOldHandle_  = nullptr;

    int width_;
    int height_;
    int64_t adapterLuid_;
    unsigned int graphicsVendorId_;
    unsigned int graphicsDeviceId_;
    std::string adapterDescription_;
    std::mutex contextLock_;
};
