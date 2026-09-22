#pragma once

#include <map>
#include <memory>
#include <mutex>
#include <string>
#include <atomic>

#include "include/cef_browser.h"
#include "callback_queue.h"
#include "texture_manager.h"

class CeffyClient;

struct BrowserEntry {
    CefRefPtr<CefBrowser> browser;
    CefRefPtr<CeffyClient> client;
    std::unique_ptr<TextureManager> textureManager;
    std::atomic<bool> hasReceivedPaint{false};
    int width;
    int height;
};

class BrowserManager {
public:
    static BrowserManager& Instance();

    bool Initialize(const char* cachePath, int remoteDebuggingPort, int64_t adapterLuid, unsigned int graphicsVendorId, unsigned int graphicsDeviceId);
    void Shutdown();
    void SetSubProcessPath(const char* path);

    int  CreateBrowser(const char* url, int width, int height);
    void CloseBrowser(int browserId);
    void CloseAllBrowsers();

    BrowserEntry* GetBrowser(int browserId);
    void RegisterBrowser(int browserId, CefRefPtr<CefBrowser> browser, CefRefPtr<CeffyClient> client);
    void UnregisterBrowser(int browserId);

    CallbackQueue& GetCallbackQueue() { return callbackQueue_; }
    int64_t GetAdapterLuid() const { return adapterLuid_; }

private:
    BrowserManager() = default;
    ~BrowserManager() = default;
    BrowserManager(const BrowserManager&) = delete;
    BrowserManager& operator=(const BrowserManager&) = delete;

    std::map<int, std::unique_ptr<BrowserEntry>> browsers_;
    std::mutex browsersMutex_;
    CallbackQueue callbackQueue_;
    int64_t adapterLuid_ = 0;
    unsigned int graphicsVendorId_ = 0;
    unsigned int graphicsDeviceId_ = 0;
    std::string subProcessPath_;
    bool initialized_ = false;
    std::atomic<int> nextBrowserId_{1};
};
