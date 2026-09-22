#include "texture_manager.h"

#include <d3d11_1.h>
#include <dxgi1_2.h>
#include <cstdio>
#include <algorithm>
#include <vector>

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")

TextureManager::TextureManager(int width, int height, int64_t adapterLuid, unsigned int graphicsVendorId, unsigned int graphicsDeviceId)
    : width_(width), height_(height), adapterLuid_(adapterLuid), graphicsVendorId_(graphicsVendorId), graphicsDeviceId_(graphicsDeviceId)
{
    CreateDevice(adapterLuid, graphicsVendorId, graphicsDeviceId);
    CreateSharedTexture();
    printf(">> TextureManager: Created device and shared texture %dx%d, handle: 0x%llX\n",
           width_, height_, (unsigned long long)(uintptr_t)sharedHandle_);
}

TextureManager::~TextureManager()
{
    printf(">> TextureManager: Disposing\n");
    if (pendingOldTexture_) { pendingOldTexture_->Release(); pendingOldTexture_ = nullptr; }
    if (sharedTexture_)     { sharedTexture_->Release();     sharedTexture_ = nullptr;     }
    if (context_)           { context_->Release();           context_ = nullptr;           }
    if (device_)            { device_->Release();            device_ = nullptr;            }
}

void TextureManager::FindAdapter(int64_t adapterLuid, unsigned int graphicsVendorId, unsigned int graphicsDeviceId, IDXGIAdapter1** outAdapter)
{
    *outAdapter = nullptr;

    IDXGIFactory1* factory = nullptr;
    HRESULT hr = CreateDXGIFactory1(__uuidof(IDXGIFactory1), (void**)&factory);
    if (FAILED(hr) || !factory)
    {
        printf("!! TextureManager: CreateDXGIFactory1 failed 0x%08X\n", (unsigned)hr);
        return;
    }

    IDXGIAdapter1* adapter = nullptr;
    for (UINT i = 0; factory->EnumAdapters1(i, &adapter) != DXGI_ERROR_NOT_FOUND; ++i)
    {
        DXGI_ADAPTER_DESC1 desc{};
        adapter->GetDesc1(&desc);

        int64_t luid = (int64_t)((uint64_t)(uint32_t)desc.AdapterLuid.LowPart |
                                 ((uint64_t)(uint32_t)desc.AdapterLuid.HighPart << 32));

        printf(">>   Enumerating adapter %u: VendorId=0x%04X, DeviceId=0x%04X, DedicatedMem=%llu MB, LUID=0x%llX\n",
               i, desc.VendorId, desc.DeviceId,
               (unsigned long long)(desc.DedicatedVideoMemory / (1024 * 1024)),
               (unsigned long long)luid);

        bool match = false;
        if (adapterLuid != 0 && luid == adapterLuid)
            match = true;
        else if (adapterLuid == 0 && graphicsVendorId != 0 && graphicsDeviceId != 0 &&
                 desc.VendorId == graphicsVendorId && desc.DeviceId == graphicsDeviceId)
            match = true;

        if (match)
        {
            printf(">>   -> Matched Unity GPU adapter at index %u\n", i);
            *outAdapter = adapter;
            factory->Release();
            return;
        }
        adapter->Release();
    }

    printf("!! TextureManager: No adapter matched (luid=0x%llX vendor=0x%X device=0x%X). Falling back to default.\n",
           (unsigned long long)adapterLuid, graphicsVendorId, graphicsDeviceId);
    factory->Release();
}

void TextureManager::CreateDevice(int64_t adapterLuid, unsigned int graphicsVendorId, unsigned int graphicsDeviceId)
{
    IDXGIAdapter1* adapter = nullptr;
    FindAdapter(adapterLuid, graphicsVendorId, graphicsDeviceId, &adapter);

    D3D_DRIVER_TYPE driverType = adapter ? D3D_DRIVER_TYPE_UNKNOWN : D3D_DRIVER_TYPE_HARDWARE;

    D3D_FEATURE_LEVEL featureLevels[] = {
        D3D_FEATURE_LEVEL_11_1,
        D3D_FEATURE_LEVEL_11_0,
        D3D_FEATURE_LEVEL_10_1
    };
    D3D_FEATURE_LEVEL achievedLevel{};

    HRESULT hr = D3D11CreateDevice(
        adapter,
        driverType,
        nullptr,
        D3D11_CREATE_DEVICE_BGRA_SUPPORT,
        featureLevels, _countof(featureLevels),
        D3D11_SDK_VERSION,
        &device_,
        &achievedLevel,
        &context_);

    if (adapter) adapter->Release();

    if (FAILED(hr))
    {
        printf("!! TextureManager: D3D11CreateDevice failed HRESULT 0x%08X\n", (unsigned)hr);
        return;
    }

    printf(">> TextureManager: Created D3D11 device, feature level 0x%X\n", (unsigned)achievedLevel);

    IDXGIDevice* dxgiDevice = nullptr;
    if (SUCCEEDED(device_->QueryInterface(__uuidof(IDXGIDevice), (void**)&dxgiDevice)))
    {
        IDXGIAdapter* actualAdapter = nullptr;
        if (SUCCEEDED(dxgiDevice->GetAdapter(&actualAdapter)))
        {
            DXGI_ADAPTER_DESC desc{};
            actualAdapter->GetDesc(&desc);

            // Convert the wide-char GPU name to UTF-8 for the callback queue.
            char nameBuf[256]{};
            WideCharToMultiByte(CP_UTF8, 0, desc.Description, -1, nameBuf, sizeof(nameBuf) - 1, nullptr, nullptr);

            char fullDesc[320]{};
            snprintf(fullDesc, sizeof(fullDesc),
                     "%s (VendorId=0x%04X, DeviceId=0x%04X, DedicatedMem=%llu MB)",
                     nameBuf, desc.VendorId, desc.DeviceId,
                     (unsigned long long)(desc.DedicatedVideoMemory / (1024 * 1024)));
            adapterDescription_ = fullDesc;

            printf(">>   Adapter: %s\n", fullDesc);
            actualAdapter->Release();
        }
        dxgiDevice->Release();
    }
}

void TextureManager::CreateSharedTexture()
{
    if (!device_) return;

    if (pendingOldTexture_)
    {
        pendingOldTexture_->Release();
        pendingOldTexture_ = nullptr;
    }
    pendingOldHandle_ = nullptr;

    ID3D11Texture2D* oldTexture = sharedTexture_;
    int oldWidth = 0, oldHeight = 0;
    if (oldTexture)
    {
        D3D11_TEXTURE2D_DESC desc{};
        oldTexture->GetDesc(&desc);
        oldWidth = (int)desc.Width;
        oldHeight = (int)desc.Height;
    }

    pendingOldTexture_ = sharedTexture_;
    pendingOldHandle_ = sharedHandle_;
    sharedTexture_ = nullptr;
    sharedHandle_ = nullptr;

    D3D11_TEXTURE2D_DESC td{};
    td.Width = (UINT)width_;
    td.Height = (UINT)height_;
    td.MipLevels = 1;
    td.ArraySize = 1;
    td.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    td.SampleDesc.Count = 1;
    td.SampleDesc.Quality = 0;
    td.Usage = D3D11_USAGE_DEFAULT;
    td.BindFlags = D3D11_BIND_SHADER_RESOURCE | D3D11_BIND_RENDER_TARGET;
    td.CPUAccessFlags = 0;
    td.MiscFlags = D3D11_RESOURCE_MISC_SHARED;

    HRESULT hr = device_->CreateTexture2D(&td, nullptr, &sharedTexture_);
    if (FAILED(hr))
    {
        printf("!! TextureManager: CreateTexture2D failed 0x%08X\n", (unsigned)hr);
        return;
    }

    if (oldTexture && oldWidth > 0 && oldHeight > 0)
    {
        int copyW = (std::min)(oldWidth, width_);
        int copyH = (std::min)(oldHeight, height_);
        D3D11_BOX srcBox{};
        srcBox.left = 0;
        srcBox.top = 0;
        srcBox.front = 0;
        srcBox.right = (UINT)copyW;
        srcBox.bottom = (UINT)copyH;
        srcBox.back = 1;
        context_->CopySubresourceRegion(sharedTexture_, 0, 0, 0, 0, oldTexture, 0, &srcBox);
    }

    IDXGIResource* dxgiResource = nullptr;
    hr = sharedTexture_->QueryInterface(__uuidof(IDXGIResource), (void**)&dxgiResource);
    if (SUCCEEDED(hr) && dxgiResource)
    {
        HANDLE handle = nullptr;
        hr = dxgiResource->GetSharedHandle(&handle);
        if (SUCCEEDED(hr))
            sharedHandle_ = handle;
        else
            printf("!! TextureManager: GetSharedHandle failed 0x%08X\n", (unsigned)hr);
        dxgiResource->Release();
    }

    printf(">> TextureManager: Created shared texture %dx%d, handle: 0x%llX\n",
           width_, height_, (unsigned long long)(uintptr_t)sharedHandle_);
}

void TextureManager::CopyFromSharedTexture(const void* sourceHandle, int x, int y, int w, int h)
{
    CopyFromSharedTextureWithOffset(sourceHandle, x, y, w, h, x, y);
}

static ID3D11Texture2D* OpenSharedTexture(ID3D11Device* device, const void* sourceHandle)
{
    if (!sourceHandle || !device) return nullptr;
    ID3D11Texture2D* sourceTexture = nullptr;
    ID3D11Device1* device1 = nullptr;
    if (SUCCEEDED(device->QueryInterface(__uuidof(ID3D11Device1), (void**)&device1)))
    {
        if (SUCCEEDED(device1->OpenSharedResource1((HANDLE)sourceHandle, __uuidof(ID3D11Texture2D), (void**)&sourceTexture)))
        {
            device1->Release();
            return sourceTexture;
        }
        device1->Release();
    }
    if (SUCCEEDED(device->OpenSharedResource((HANDLE)sourceHandle, __uuidof(ID3D11Texture2D), (void**)&sourceTexture)))
        return sourceTexture;
    return nullptr;
}

void TextureManager::CopyRectsFromSharedTexture(const void* sourceHandle, const CeffyRect* rects, size_t numRects)
{
    CopyRectsFromSharedTextureWithOffset(sourceHandle, rects, numRects, 0, 0);
}

void TextureManager::CopyRectsFromSharedTextureWithOffset(const void* sourceHandle, const CeffyRect* rects, size_t numRects, int offsetX, int offsetY)
{
    if (!sourceHandle || !device_ || !context_ || !sharedTexture_ || !rects || numRects == 0)
        return;

    ID3D11Texture2D* sourceTexture = OpenSharedTexture(device_, sourceHandle);
    if (!sourceTexture)
    {
        printf("!! TextureManager: Failed to open shared texture 0x%llX for batch copy\n", (unsigned long long)(uintptr_t)sourceHandle);
        return;
    }

    D3D11_TEXTURE2D_DESC srcDesc{};
    sourceTexture->GetDesc(&srcDesc);
    const int srcTexW = (int)srcDesc.Width;
    const int srcTexH = (int)srcDesc.Height;

    std::lock_guard<std::mutex> lock(contextLock_);

    for (size_t i = 0; i < numRects; ++i)
    {
        int x = rects[i].x, y = rects[i].y, w = rects[i].w, h = rects[i].h;
        int destX = x + offsetX, destY = y + offsetY;
        if (w <= 0 || h <= 0) continue;
        if (destX < 0) { x -= destX; w += destX; destX = 0; }
        if (destY < 0) { y -= destY; h += destY; destY = 0; }
        if (destX >= width_ || destY >= height_) continue;
        if (destX + w > width_)  w = width_  - destX;
        if (destY + h > height_) h = height_ - destY;
        if (x < 0) { destX -= x; w += x; x = 0; }
        if (y < 0) { destY -= y; h += y; y = 0; }
        if (x >= srcTexW || y >= srcTexH) continue;
        if (x + w > srcTexW) w = srcTexW - x;
        if (y + h > srcTexH) h = srcTexH - y;
        if (w <= 0 || h <= 0) continue;

        D3D11_BOX srcBox{};
        srcBox.left   = (UINT)x;
        srcBox.top    = (UINT)y;
        srcBox.front  = 0;
        srcBox.right  = (UINT)(x + w);
        srcBox.bottom = (UINT)(y + h);
        srcBox.back   = 1;
        context_->CopySubresourceRegion(sharedTexture_, 0, (UINT)destX, (UINT)destY, 0, sourceTexture, 0, &srcBox);
    }
    context_->Flush();
    sourceTexture->Release();
}

void TextureManager::CopyFromSharedTextureWithOffset(const void* sourceHandle,
                                                     int srcX, int srcY, int srcW, int srcH,
                                                     int destX, int destY)
{
    if (!sourceHandle || !device_ || !context_ || !sharedTexture_)
        return;

    int x = srcX, y = srcY, w = srcW, h = srcH;
    if (w <= 0 || h <= 0) return;

    if (destX < 0) { x -= destX; w += destX; destX = 0; }
    if (destY < 0) { y -= destY; h += destY; destY = 0; }
    if (destX >= width_ || destY >= height_) return;
    if (destX + w > width_)  w = width_  - destX;
    if (destY + h > height_) h = height_ - destY;
    if (w <= 0 || h <= 0) return;

    std::lock_guard<std::mutex> lock(contextLock_);

    ID3D11Texture2D* sourceTexture = nullptr;

    ID3D11Device1* device1 = nullptr;
    if (SUCCEEDED(device_->QueryInterface(__uuidof(ID3D11Device1), (void**)&device1)))
    {
        HRESULT hr = device1->OpenSharedResource1((HANDLE)sourceHandle,
                                                  __uuidof(ID3D11Texture2D),
                                                  (void**)&sourceTexture);
        device1->Release();
        if (FAILED(hr))
        {
            sourceTexture = nullptr;
        }
    }

    if (!sourceTexture)
    {
        HRESULT hr = device_->OpenSharedResource((HANDLE)sourceHandle,
                                                 __uuidof(ID3D11Texture2D),
                                                 (void**)&sourceTexture);
        if (FAILED(hr) || !sourceTexture)
        {
            printf("!! TextureManager: Failed to open shared texture handle 0x%llX\n",
                   (unsigned long long)(uintptr_t)sourceHandle);
            return;
        }
    }

    D3D11_TEXTURE2D_DESC srcDesc{};
    sourceTexture->GetDesc(&srcDesc);
    int srcTexW = (int)srcDesc.Width;
    int srcTexH = (int)srcDesc.Height;

    if (x < 0) { destX -= x; w += x; x = 0; }
    if (y < 0) { destY -= y; h += y; y = 0; }
    if (x >= srcTexW || y >= srcTexH)  { sourceTexture->Release(); return; }
    if (x + w > srcTexW) w = srcTexW - x;
    if (y + h > srcTexH) h = srcTexH - y;
    if (w <= 0 || h <= 0) { sourceTexture->Release(); return; }

    D3D11_BOX srcBox{};
    srcBox.left   = (UINT)x;
    srcBox.top    = (UINT)y;
    srcBox.front  = 0;
    srcBox.right  = (UINT)(x + w);
    srcBox.bottom = (UINT)(y + h);
    srcBox.back   = 1;

    context_->CopySubresourceRegion(sharedTexture_, 0, (UINT)destX, (UINT)destY, 0,
                                    sourceTexture, 0, &srcBox);
    context_->Flush();
    sourceTexture->Release();
}

void TextureManager::Resize(int newWidth, int newHeight)
{
    std::lock_guard<std::mutex> lock(contextLock_);
    if (newWidth == width_ && newHeight == height_) return;
    width_ = newWidth;
    height_ = newHeight;
    CreateSharedTexture();
}
