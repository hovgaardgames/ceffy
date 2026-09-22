#include "cef_handlers.h"
#include "browser_manager.h"
#include "texture_manager.h"
#include "callback_queue.h"

#include "include/cef_browser.h"
#include "include/cef_command_line.h"

#include <cstdio>
#include <sstream>
#include <vector>

// ===========================================================================
// CeffyBrowserApp
// ===========================================================================

void CeffyBrowserApp::OnBeforeCommandLineProcessing(
    const CefString& process_type,
    CefRefPtr<CefCommandLine> command_line) {
    if (!command_line)
        return;

    command_line->AppendSwitch("disable-background-timer-throttling");
    command_line->AppendSwitch("disable-renderer-backgrounding");
    command_line->AppendSwitch("disable-backgrounding-occluded-windows");
    command_line->AppendSwitch("force_high_performance_gpu");
    command_line->AppendSwitch("in-process-gpu");

    command_line->AppendSwitch("disable-extensions");
    command_line->AppendSwitch("disable-component-extensions-with-background-pages");
    command_line->AppendSwitch("disable-component-update");
    command_line->AppendSwitch("disable-pdf-extension");
    command_line->AppendSwitch("disable-plugins");
    command_line->AppendSwitch("disable-usb-keyboard-detect");
    command_line->AppendSwitch("disable-speech-api");
    command_line->AppendSwitch("disable-notifications");
    command_line->AppendSwitch("disable-webrtc");

    // Local file access support (StreamingAssets/file:// usage).
    command_line->AppendSwitch("allow-file-access-from-files");
    command_line->AppendSwitch("allow-universal-access-from-files");
    command_line->AppendSwitch("disable-web-security");

    // Allows DevTools frontend origins to attach to the remote debugger.
    command_line->AppendSwitchWithValue("remote-allow-origins", "*");

    command_line->AppendSwitchWithValue(
        "disable-features",
        "CalculateNativeWinOcclusion,WebUSB,WebOTP,WebBluetooth,WebMIDI,"
        "PaymentRequest,WebXR,WebXRDeviceAPI,SpeechRecognition,SpeechSynthesis,"
        "Notifications,BackgroundSync,BackgroundFetch,IdleDetection,Serial,"
        "StorageAccessAPI,DirectSockets");
}

// ===========================================================================
// CeffyRenderHandler
// ===========================================================================

CeffyRenderHandler::CeffyRenderHandler(int browserId, int width, int height,
                                     TextureManager* textureManager,
                                     CallbackQueue* callbackQueue)
    : browserId_(browserId), width_(width), height_(height),
      textureManager_(textureManager), callbackQueue_(callbackQueue) {}

void CeffyRenderHandler::SetSize(int w, int h) {
    width_ = w;
    height_ = h;
}

void CeffyRenderHandler::GetViewRect(CefRefPtr<CefBrowser> browser, CefRect& rect) {
    rect.Set(0, 0, width_, height_);
}

void CeffyRenderHandler::OnPaint(CefRefPtr<CefBrowser> /*browser*/,
                                PaintElementType /*type*/,
                                const RectList& /*dirtyRects*/,
                                const void* /*buffer*/,
                                int /*width*/, int /*height*/) {
    // Software paint path unused -- we rely on OnAcceleratedPaint.
}

void CeffyRenderHandler::OnAcceleratedPaint(CefRefPtr<CefBrowser> /*browser*/,
                                           PaintElementType type,
                                           const RectList& dirtyRects,
                                           const CefAcceleratedPaintInfo& info) {
    if (!textureManager_ || dirtyRects.empty()) {
        if (textureManager_) hasReceivedPaint_.store(true);
        return;
    }

    std::vector<CeffyRect> rects(dirtyRects.size());
    for (size_t i = 0; i < dirtyRects.size(); ++i) {
        rects[i].x = dirtyRects[i].x;
        rects[i].y = dirtyRects[i].y;
        rects[i].w = dirtyRects[i].width;
        rects[i].h = dirtyRects[i].height;
    }
    const void* handle = info.shared_texture_handle;
    if (type == PET_VIEW) {
        textureManager_->CopyRectsFromSharedTexture(handle, rects.data(), rects.size());
    } else if (type == PET_POPUP && popupVisible_) {
        textureManager_->CopyRectsFromSharedTextureWithOffset(
            handle, rects.data(), rects.size(), popupRect_.x, popupRect_.y);
    }

    hasReceivedPaint_.store(true);
}

void CeffyRenderHandler::OnPopupShow(CefRefPtr<CefBrowser> /*browser*/, bool show) {
    popupVisible_ = show;
}

void CeffyRenderHandler::OnPopupSize(CefRefPtr<CefBrowser> /*browser*/, const CefRect& rect) {
    popupRect_ = rect;
}

bool CeffyRenderHandler::StartDragging(CefRefPtr<CefBrowser> /*browser*/,
                                      CefRefPtr<CefDragData> drag_data,
                                      DragOperationsMask allowed_ops,
                                      int x, int y) {
    {
        std::lock_guard<std::mutex> lock(dragMutex_);
        dragData_ = drag_data;
        dragAllowedOps_ = static_cast<uint32_t>(allowed_ops);
        currentDragOp_ = 0;
    }

    if (callbackQueue_) {
        std::ostringstream json;
        json << "{\"x\":" << x << ",\"y\":" << y
             << ",\"ops\":" << static_cast<uint32_t>(allowed_ops) << "}";
        CeffyCallbackData cb;
        cb.type = CeffyCallbackType::DragStart;
        cb.browserId = browserId_;
        cb.data = json.str();
        callbackQueue_->Push(std::move(cb));
    }
    return true; // host takes ownership of the drag
}

void CeffyRenderHandler::UpdateDragCursor(CefRefPtr<CefBrowser> /*browser*/,
                                         DragOperation operation) {
    std::lock_guard<std::mutex> lock(dragMutex_);
    currentDragOp_ = static_cast<uint32_t>(operation);
}

CefRefPtr<CefDragData> CeffyRenderHandler::GetDragData(uint32_t& outAllowedOps) {
    std::lock_guard<std::mutex> lock(dragMutex_);
    outAllowedOps = dragAllowedOps_;
    return dragData_;
}

uint32_t CeffyRenderHandler::GetCurrentDragOp() {
    std::lock_guard<std::mutex> lock(dragMutex_);
    return currentDragOp_;
}

void CeffyRenderHandler::ClearDragState() {
    std::lock_guard<std::mutex> lock(dragMutex_);
    dragData_ = nullptr;
    dragAllowedOps_ = 0;
    currentDragOp_ = 0;
}

// ===========================================================================
// CeffyLifeSpanHandler
// ===========================================================================

CeffyLifeSpanHandler::CeffyLifeSpanHandler(int browserId)
    : browserId_(browserId) {}

void CeffyLifeSpanHandler::OnAfterCreated(CefRefPtr<CefBrowser> browser) {
    printf(">> Browser after-created native_id=%d bridge_id=%d\n", browser->GetIdentifier(), browserId_);
    BrowserManager::Instance().RegisterBrowser(browserId_, browser, nullptr);

    CefRefPtr<CefBrowserHost> host = browser->GetHost();
    if (host) {
        host->Invalidate(PET_VIEW);
        host->SendExternalBeginFrame();
        host->SendExternalBeginFrame();
        host->SendExternalBeginFrame();
    }
}

bool CeffyLifeSpanHandler::DoClose(CefRefPtr<CefBrowser> browser) {
    (void)browser;
    // DoClose is only a close-request stage. Actual destruction happens in
    // OnBeforeClose; unregister there so lifecycle tracking remains accurate.
    return false;
}

void CeffyLifeSpanHandler::OnBeforeClose(CefRefPtr<CefBrowser> /*browser*/) {
    BrowserManager::Instance().UnregisterBrowser(browserId_);
}

bool CeffyLifeSpanHandler::OnBeforePopup(CefRefPtr<CefBrowser> /*browser*/,
                                        CefRefPtr<CefFrame> /*frame*/,
                                        int /*popup_id*/,
                                        const CefString& /*target_url*/,
                                        const CefString& /*target_frame_name*/,
                                        WindowOpenDisposition /*target_disposition*/,
                                        bool /*user_gesture*/,
                                        const CefPopupFeatures& /*popupFeatures*/,
                                        CefWindowInfo& /*windowInfo*/,
                                        CefRefPtr<CefClient>& /*client*/,
                                        CefBrowserSettings& /*settings*/,
                                        CefRefPtr<CefDictionaryValue>& /*extra_info*/,
                                        bool* /*no_javascript_access*/) {
    return true;
}

// ===========================================================================
// CeffyDisplayHandler
// ===========================================================================

static std::string EscapeJson(const std::string& s) {
    std::string out;
    out.reserve(s.size() + 16);
    for (char c : s) {
        switch (c) {
            case '\\': out += "\\\\"; break;
            case '"':  out += "\\\""; break;
            case '\n': out += "\\n";  break;
            case '\r': out += "\\r";  break;
            case '\t': out += "\\t";  break;
            default:   out += c;      break;
        }
    }
    return out;
}

CeffyDisplayHandler::CeffyDisplayHandler(int browserId, CallbackQueue* callbackQueue)
    : browserId_(browserId), callbackQueue_(callbackQueue) {}

bool CeffyDisplayHandler::OnConsoleMessage(CefRefPtr<CefBrowser> /*browser*/,
                                          cef_log_severity_t level,
                                          const CefString& message,
                                          const CefString& source,
                                          int line) {
    int lvl;
    switch (level) {
        case LOGSEVERITY_DEBUG:   lvl = 1; break;
        case LOGSEVERITY_INFO:    lvl = 2; break;
        case LOGSEVERITY_WARNING: lvl = 3; break;
        case LOGSEVERITY_ERROR:   lvl = 4; break;
        default:                  lvl = 0; break;
    }

    std::string msgUtf8 = message.ToString();
    std::string srcUtf8 = source.ToString();

    std::ostringstream json;
    json << "{\"level\":" << lvl
         << ",\"message\":\"" << EscapeJson(msgUtf8) << "\""
         << ",\"source\":\"" << EscapeJson(srcUtf8) << "\""
         << ",\"line\":" << line << "}";

    if (callbackQueue_) {
        CeffyCallbackData cb;
        cb.type = CeffyCallbackType::ConsoleMessage;
        cb.browserId = browserId_;
        cb.data = json.str();
        callbackQueue_->Push(std::move(cb));
    }

    return false;
}

// ===========================================================================
// CeffyClient
// ===========================================================================

CeffyClient::CeffyClient(int browserId, int width, int height,
                       TextureManager* textureManager, CallbackQueue* callbackQueue)
    : browserId_(browserId), callbackQueue_(callbackQueue) {
    renderHandler_   = new CeffyRenderHandler(browserId, width, height, textureManager, callbackQueue);
    lifeSpanHandler_ = new CeffyLifeSpanHandler(browserId);
    displayHandler_  = new CeffyDisplayHandler(browserId, callbackQueue);
}

bool CeffyClient::OnProcessMessageReceived(CefRefPtr<CefBrowser> /*browser*/,
                                          CefRefPtr<CefFrame> /*frame*/,
                                          CefProcessId /*source_process*/,
                                          CefRefPtr<CefProcessMessage> message) {
    if (message->GetName() == "SendToUnity") {
        CefRefPtr<CefListValue> args = message->GetArgumentList();
        if (args && args->GetSize() > 0) {
            std::string payload = args->GetString(0).ToString();
            if (callbackQueue_) {
                CeffyCallbackData cb;
                cb.type = CeffyCallbackType::JsMessage;
                cb.browserId = browserId_;
                cb.data = std::move(payload);
                callbackQueue_->Push(std::move(cb));
            }
        }
        return true;
    }
    return false;
}
