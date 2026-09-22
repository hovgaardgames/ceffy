#pragma once

#include <atomic>
#include <mutex>
#include "include/cef_client.h"
#include "include/cef_render_handler.h"
#include "include/cef_life_span_handler.h"
#include "include/cef_display_handler.h"
#include "include/cef_drag_data.h"
#include "include/cef_app.h"

class TextureManager;
class CallbackQueue;

// ---------------------------------------------------------------------------
// CefApp for the browser process (minimal -- no special overrides needed)
// ---------------------------------------------------------------------------
class CeffyBrowserApp : public CefApp {
public:
    void OnBeforeCommandLineProcessing(
        const CefString& process_type,
        CefRefPtr<CefCommandLine> command_line) override;

    IMPLEMENT_REFCOUNTING(CeffyBrowserApp);
};

// ---------------------------------------------------------------------------
// Offscreen render handler -- receives shared-texture paint callbacks
// ---------------------------------------------------------------------------
class CeffyRenderHandler : public CefRenderHandler {
public:
    CeffyRenderHandler(int browserId, int width, int height,
                      TextureManager* textureManager,
                      CallbackQueue* callbackQueue);

    void SetSize(int w, int h);
    bool HasReceivedPaint() const { return hasReceivedPaint_.load(); }

    // Drag support: called by Ceffy_DragTarget* bridge functions
    CefRefPtr<CefDragData> GetDragData(uint32_t& outAllowedOps);
    uint32_t GetCurrentDragOp();
    void ClearDragState();

    // CefRenderHandler
    void GetViewRect(CefRefPtr<CefBrowser> browser, CefRect& rect) override;
    void OnAcceleratedPaint(CefRefPtr<CefBrowser> browser,
                            PaintElementType type,
                            const RectList& dirtyRects,
                            const CefAcceleratedPaintInfo& info) override;
    void OnPaint(CefRefPtr<CefBrowser> browser, PaintElementType type,
                 const RectList& dirtyRects, const void* buffer,
                 int width, int height) override;
    void OnPopupShow(CefRefPtr<CefBrowser> browser, bool show) override;
    void OnPopupSize(CefRefPtr<CefBrowser> browser, const CefRect& rect) override;
    bool StartDragging(CefRefPtr<CefBrowser> browser,
                       CefRefPtr<CefDragData> drag_data,
                       DragOperationsMask allowed_ops,
                       int x, int y) override;
    void UpdateDragCursor(CefRefPtr<CefBrowser> browser,
                          DragOperation operation) override;

    IMPLEMENT_REFCOUNTING(CeffyRenderHandler);

private:
    int browserId_;
    int width_;
    int height_;
    TextureManager* textureManager_;
    CallbackQueue* callbackQueue_;
    std::atomic<bool> hasReceivedPaint_{false};
    CefRect popupRect_;
    bool popupVisible_ = false;

    mutable std::mutex dragMutex_;
    CefRefPtr<CefDragData> dragData_;
    uint32_t dragAllowedOps_ = 0;
    uint32_t currentDragOp_ = 0;
};

// ---------------------------------------------------------------------------
// Lifespan handler -- browser create / close
// ---------------------------------------------------------------------------
class CeffyLifeSpanHandler : public CefLifeSpanHandler {
public:
    explicit CeffyLifeSpanHandler(int browserId);

    void OnAfterCreated(CefRefPtr<CefBrowser> browser) override;
    bool DoClose(CefRefPtr<CefBrowser> browser) override;
    void OnBeforeClose(CefRefPtr<CefBrowser> browser) override;
    bool OnBeforePopup(CefRefPtr<CefBrowser> browser,
                       CefRefPtr<CefFrame> frame,
                       int popup_id,
                       const CefString& target_url,
                       const CefString& target_frame_name,
                       WindowOpenDisposition target_disposition,
                       bool user_gesture,
                       const CefPopupFeatures& popupFeatures,
                       CefWindowInfo& windowInfo,
                       CefRefPtr<CefClient>& client,
                       CefBrowserSettings& settings,
                       CefRefPtr<CefDictionaryValue>& extra_info,
                       bool* no_javascript_access) override;

    IMPLEMENT_REFCOUNTING(CeffyLifeSpanHandler);

private:
    int browserId_;
};

// ---------------------------------------------------------------------------
// Display handler -- forwards console messages to the callback queue
// ---------------------------------------------------------------------------
class CeffyDisplayHandler : public CefDisplayHandler {
public:
    CeffyDisplayHandler(int browserId, CallbackQueue* callbackQueue);

    bool OnConsoleMessage(CefRefPtr<CefBrowser> browser,
                          cef_log_severity_t level,
                          const CefString& message,
                          const CefString& source,
                          int line) override;

    IMPLEMENT_REFCOUNTING(CeffyDisplayHandler);

private:
    int browserId_;
    CallbackQueue* callbackQueue_;
};

// ---------------------------------------------------------------------------
// CefClient -- routes to the handlers above; receives process messages
// ---------------------------------------------------------------------------
class CeffyClient : public CefClient {
public:
    CeffyClient(int browserId, int width, int height,
               TextureManager* textureManager, CallbackQueue* callbackQueue);

    CefRefPtr<CefRenderHandler>   GetRenderHandler()   override { return renderHandler_; }
    CefRefPtr<CefLifeSpanHandler> GetLifeSpanHandler() override { return lifeSpanHandler_; }
    CefRefPtr<CefDisplayHandler>  GetDisplayHandler()  override { return displayHandler_; }

    bool OnProcessMessageReceived(CefRefPtr<CefBrowser> browser,
                                  CefRefPtr<CefFrame> frame,
                                  CefProcessId source_process,
                                  CefRefPtr<CefProcessMessage> message) override;

    CeffyRenderHandler* GetCeffyRenderHandler() { return renderHandler_.get(); }

    IMPLEMENT_REFCOUNTING(CeffyClient);

private:
    CefRefPtr<CeffyRenderHandler>   renderHandler_;
    CefRefPtr<CeffyLifeSpanHandler> lifeSpanHandler_;
    CefRefPtr<CeffyDisplayHandler>  displayHandler_;
    int browserId_;
    CallbackQueue* callbackQueue_;
};
