#include "helper_app.h"
#include "include/cef_browser.h"
#include "include/cef_frame.h"
#include <string>

// ---------------------------------------------------------------------------
// CeffyRendererApp
// ---------------------------------------------------------------------------

void CeffyRendererApp::OnContextCreated(CefRefPtr<CefBrowser> browser,
                                       CefRefPtr<CefFrame> frame,
                                       CefRefPtr<CefV8Context> context) {
    if (!frame->IsMain())
        return;

    CefRefPtr<CefV8Value> global = context->GetGlobal();

    if (global->HasValue("ceffy"))
        return;

    CefRefPtr<CefV8Value> ceffyObj = CefV8Value::CreateObject(nullptr, nullptr);

    CefRefPtr<CefV8Handler> handler = new CeffySendToUnityHandler(browser, frame);
    CefRefPtr<CefV8Value> sendToUnityFunc =
        CefV8Value::CreateFunction("SendToUnity", handler);

    ceffyObj->SetValue("SendToUnity", sendToUnityFunc,
                      V8_PROPERTY_ATTRIBUTE_READONLY);
    ceffyObj->SetValue("onMessageFromUnity", CefV8Value::CreateNull(),
                      V8_PROPERTY_ATTRIBUTE_NONE);

    global->SetValue("ceffy", ceffyObj, V8_PROPERTY_ATTRIBUTE_READONLY);
}

bool CeffyRendererApp::OnProcessMessageReceived(
    CefRefPtr<CefBrowser> browser,
    CefRefPtr<CefFrame> frame,
    CefProcessId source_process,
    CefRefPtr<CefProcessMessage> message) {

    if (message->GetName() == "DispatchToJS") {
        CefString raw = message->GetArgumentList()->GetString(0);
        std::string msg = raw.ToString();

        std::string escaped;
        escaped.reserve(msg.size() + 16);
        for (char c : msg) {
            switch (c) {
                case '\\': escaped += "\\\\"; break;
                case '"':  escaped += "\\\""; break;
                case '\n': escaped += "\\n";  break;
                case '\r': escaped += "\\r";  break;
                case '\t': escaped += "\\t";  break;
                default:   escaped += c;      break;
            }
        }

        std::string js =
            "(function(){try{if(window.ceffy&&typeof window.ceffy.onMessageFromUnity==='function'){"
            "window.ceffy.onMessageFromUnity(\"" + escaped + "\");}}catch(e){}})();";

        frame->ExecuteJavaScript(js, frame->GetURL(), 0);
        return true;
    }

    return false;
}

// ---------------------------------------------------------------------------
// CeffySendToUnityHandler
// ---------------------------------------------------------------------------

CeffySendToUnityHandler::CeffySendToUnityHandler(CefRefPtr<CefBrowser> browser,
                                               CefRefPtr<CefFrame> frame)
    : browser_(browser), frame_(frame) {}

bool CeffySendToUnityHandler::Execute(const CefString& name,
                                     CefRefPtr<CefV8Value> object,
                                     const CefV8ValueList& arguments,
                                     CefRefPtr<CefV8Value>& retval,
                                     CefString& exception) {
    if (arguments.empty()) {
        exception = "SendToUnity requires one argument";
        return true;
    }

    CefString value;
    if (arguments[0]->IsString()) {
        value = arguments[0]->GetStringValue();
    } else {
        exception = "SendToUnity argument must be a string";
        return true;
    }

    CefRefPtr<CefProcessMessage> msg =
        CefProcessMessage::Create("SendToUnity");
    msg->GetArgumentList()->SetString(0, value);
    frame_->SendProcessMessage(PID_BROWSER, msg);

    return true;
}
