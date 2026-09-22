#pragma once

#include "include/cef_app.h"
#include "include/cef_render_process_handler.h"
#include "include/cef_v8.h"

// CefApp for renderer sub-processes: injects window.ceffy and handles
// JS -> browser process messaging via CefProcessMessage.
class CeffyRendererApp : public CefApp,
                        public CefRenderProcessHandler {
public:
    CefRefPtr<CefRenderProcessHandler> GetRenderProcessHandler() override {
        return this;
    }

    void OnContextCreated(CefRefPtr<CefBrowser> browser,
                          CefRefPtr<CefFrame> frame,
                          CefRefPtr<CefV8Context> context) override;

    bool OnProcessMessageReceived(CefRefPtr<CefBrowser> browser,
                                  CefRefPtr<CefFrame> frame,
                                  CefProcessId source_process,
                                  CefRefPtr<CefProcessMessage> message) override;

    IMPLEMENT_REFCOUNTING(CeffyRendererApp);
};

// V8 handler backing window.ceffy.SendToUnity()
class CeffySendToUnityHandler : public CefV8Handler {
public:
    CeffySendToUnityHandler(CefRefPtr<CefBrowser> browser,
                           CefRefPtr<CefFrame> frame);

    bool Execute(const CefString& name,
                 CefRefPtr<CefV8Value> object,
                 const CefV8ValueList& arguments,
                 CefRefPtr<CefV8Value>& retval,
                 CefString& exception) override;

    IMPLEMENT_REFCOUNTING(CeffySendToUnityHandler);

private:
    CefRefPtr<CefBrowser> browser_;
    CefRefPtr<CefFrame> frame_;
};
