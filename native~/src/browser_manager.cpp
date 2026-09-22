#include "browser_manager.h"
#include "cef_handlers.h"
#include "texture_manager.h"

#include "include/cef_app.h"
#include "include/cef_browser.h"

#include <cstdio>
#include <vector>
#include <Windows.h>

BrowserManager& BrowserManager::Instance() {
    static BrowserManager instance;
    return instance;
}

bool BrowserManager::Initialize(const char* cachePath, int remoteDebuggingPort, int64_t adapterLuid, unsigned int graphicsVendorId, unsigned int graphicsDeviceId) {
    if (initialized_) {
        printf(">> BrowserManager already initialized\n");
        return true;
    }

    adapterLuid_ = adapterLuid;
    graphicsVendorId_ = graphicsVendorId;
    graphicsDeviceId_ = graphicsDeviceId;

    CefMainArgs main_args(GetModuleHandle(nullptr));

    CefSettings settings;
    settings.multi_threaded_message_loop = true;
    settings.windowless_rendering_enabled = true;
    settings.no_sandbox = true;
    settings.log_severity = LOGSEVERITY_WARNING;
    settings.remote_debugging_port = remoteDebuggingPort;

    CefString(&settings.cache_path) = cachePath;
    CefString(&settings.root_cache_path) = cachePath;

    if (!subProcessPath_.empty()) {
        CefString(&settings.browser_subprocess_path) = subProcessPath_;
    }

    CefRefPtr<CeffyBrowserApp> app(new CeffyBrowserApp());

    if (!CefInitialize(main_args, settings, app, nullptr)) {
        printf("!! CefInitialize failed\n");
        return false;
    }

    initialized_ = true;
    printf(">> CEF initialized (cache=%s, debug_port=%d, luid=%lld, vendor=0x%X device=0x%X)\n",
           cachePath, remoteDebuggingPort, static_cast<long long>(adapterLuid), graphicsVendorId, graphicsDeviceId);
    return true;
}

void BrowserManager::Shutdown() {
    if (!initialized_) return;

    CloseAllBrowsers();

    CefShutdown();
    initialized_ = false;
    printf(">> CEF shut down\n");
}

void BrowserManager::CloseAllBrowsers() {
    if (!initialized_) return;

    std::vector<CefRefPtr<CefBrowserHost>> hostsToClose;
    {
        std::lock_guard<std::mutex> lock(browsersMutex_);
        for (auto& kv : browsers_) {
            if (kv.second && kv.second->browser && kv.second->browser->GetHost()) {
                hostsToClose.push_back(kv.second->browser->GetHost());
            }
        }
    }

    for (auto& host : hostsToClose) {
        host->CloseBrowser(true);
    }

    // Wait for OnBeforeClose -> UnregisterBrowser so we only consider browsers
    // closed after CEF has actually destroyed them.
    size_t remaining = 0;
    for (int i = 0; i < 500; ++i) { // ~5s max
        {
            std::lock_guard<std::mutex> lock(browsersMutex_);
            remaining = browsers_.size();
        }
        if (remaining == 0) break;
        Sleep(10);
    }

    if (remaining == 0) {
        printf(">> All browsers closed\n");
    } else {
        printf("!! CloseAllBrowsers timeout: %zu browser(s) still active\n", remaining);
    }
}

void BrowserManager::SetSubProcessPath(const char* path) {
    subProcessPath_ = path ? path : "";
    printf(">> Sub-process path set: %s\n", subProcessPath_.c_str());
}

int BrowserManager::CreateBrowser(const char* url, int width, int height) {
    if (!initialized_) {
        printf("!! CreateBrowser called before Initialize\n");
        return -1;
    }
    if (!url || width <= 0 || height <= 0) {
        printf("!! CreateBrowser invalid args: url=%s size=%dx%d\n", url ? url : "(null)", width, height);
        return -1;
    }

    const int browserId = nextBrowserId_.fetch_add(1);
    auto textureManager = std::make_unique<TextureManager>(width, height, adapterLuid_, graphicsVendorId_, graphicsDeviceId_);

    const std::string& gpuDesc = textureManager->GetAdapterDescription();
    if (!gpuDesc.empty()) {
        CeffyCallbackData logCb;
        logCb.type = CeffyCallbackType::NativeLog;
        logCb.browserId = browserId;
        logCb.data = "[Ceffy] CEF GPU: " + gpuDesc;
        callbackQueue_.Push(std::move(logCb));
    }

    CefRefPtr<CeffyClient> client(
        new CeffyClient(browserId, width, height, textureManager.get(), &callbackQueue_));

    CefWindowInfo windowInfo;
    windowInfo.SetAsWindowless(nullptr);
    windowInfo.shared_texture_enabled = true;
    windowInfo.external_begin_frame_enabled = true;
    windowInfo.bounds.width = width;
    windowInfo.bounds.height = height;

    CefBrowserSettings browserSettings;
    browserSettings.windowless_frame_rate = 90;

    auto entry = std::make_unique<BrowserEntry>();
    entry->client = client;
    entry->textureManager = std::move(textureManager);
    entry->width = width;
    entry->height = height;

    {
        std::lock_guard<std::mutex> lock(browsersMutex_);
        browsers_[browserId] = std::move(entry);
    }

    const bool createAccepted = CefBrowserHost::CreateBrowser(
        windowInfo, client, url, browserSettings, nullptr, nullptr);

    if (!createAccepted) {
        std::lock_guard<std::mutex> lock(browsersMutex_);
        browsers_.erase(browserId);
        printf("!! CreateBrowser failed (async enqueue) for url: %s\n", url);
        return -1;
    }

    printf(">> Browser created id=%d url=%s (%dx%d)\n", browserId, url, width, height);
    return browserId;
}

void BrowserManager::CloseBrowser(int browserId) {
    CefRefPtr<CefBrowserHost> host;
    {
        std::lock_guard<std::mutex> lock(browsersMutex_);
        auto it = browsers_.find(browserId);
        if (it == browsers_.end()) return;
        if (it->second->browser) {
            host = it->second->browser->GetHost();
        } else {
            // Browser never fully created; safe to remove immediately.
            browsers_.erase(it);
        }
    }
    if (host) {
        host->CloseBrowser(true);
        printf(">> Browser close requested id=%d\n", browserId);
    }
}

BrowserEntry* BrowserManager::GetBrowser(int browserId) {
    std::lock_guard<std::mutex> lock(browsersMutex_);
    auto it = browsers_.find(browserId);
    return (it != browsers_.end()) ? it->second.get() : nullptr;
}

void BrowserManager::RegisterBrowser(int browserId, CefRefPtr<CefBrowser> browser,
                                     CefRefPtr<CeffyClient> client) {
    std::lock_guard<std::mutex> lock(browsersMutex_);
    auto it = browsers_.find(browserId);
    if (it != browsers_.end()) {
        it->second->browser = browser;
        if (client)
            it->second->client = client;
    } else {
        auto entry = std::make_unique<BrowserEntry>();
        entry->browser = browser;
        entry->client = client;
        entry->width = 0;
        entry->height = 0;
        browsers_[browserId] = std::move(entry);
    }
}

void BrowserManager::UnregisterBrowser(int browserId) {
    std::lock_guard<std::mutex> lock(browsersMutex_);
    browsers_.erase(browserId);
    printf(">> Browser unregistered id=%d\n", browserId);
}
