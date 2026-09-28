#include "helper_app.h"
#include "include/cef_browser.h"
#include "include/cef_frame.h"
#include <iomanip>
#include <sstream>
#include <string>

namespace {
constexpr char kSharedSlotPrefix[] = "ceffy-slot-";

std::string EscapeJson(const std::string& value) {
    std::ostringstream escaped;
    for (unsigned char c : value) {
        switch (c) {
            case '\\': escaped << "\\\\"; break;
            case '"':  escaped << "\\\""; break;
            case '\b': escaped << "\\b"; break;
            case '\f': escaped << "\\f"; break;
            case '\n': escaped << "\\n"; break;
            case '\r': escaped << "\\r"; break;
            case '\t': escaped << "\\t"; break;
            default:
                if (c < 0x20) {
                    escaped << "\\u"
                            << std::hex << std::setw(4) << std::setfill('0')
                            << static_cast<int>(c)
                            << std::dec;
                } else {
                    escaped << c;
                }
        }
    }
    return escaped.str();
}
}

// ---------------------------------------------------------------------------
// CeffyRendererApp
// ---------------------------------------------------------------------------

void CeffyRendererApp::OnContextCreated(CefRefPtr<CefBrowser> browser,
                                       CefRefPtr<CefFrame> frame,
                                       CefRefPtr<CefV8Context> context) {
    const std::string frameName = frame->GetName().ToString();
    if (!frame->IsMain() && frameName.rfind(kSharedSlotPrefix, 0) != 0)
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
        std::string escaped = EscapeJson(raw.ToString());

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
    } else if (!frame_->IsMain()) {
        CefRefPtr<CefV8Value> json =
            CefV8Context::GetCurrentContext()->GetGlobal()->GetValue("JSON");
        CefRefPtr<CefV8Value> stringify = json->GetValue("stringify");
        CefV8ValueList stringifyArgs = {arguments[0]};
        CefRefPtr<CefV8Value> result =
            stringify->ExecuteFunction(json, stringifyArgs);
        if (result && result->IsString())
            value = result->GetStringValue();
        else {
            exception = "SendToUnity argument is not serializable";
            return true;
        }
    } else {
        exception = "SendToUnity argument must be a string";
        return true;
    }

    CefRefPtr<CefProcessMessage> msg =
        CefProcessMessage::Create("SendToUnity");
    if (frame_->IsMain()) {
        msg->GetArgumentList()->SetString(0, value);
    } else {
        const std::string frameName = frame_->GetName().ToString();
        const std::string slotId = frameName.substr(sizeof(kSharedSlotPrefix) - 1);
        const std::string payload = value.ToString();
        msg->GetArgumentList()->SetString(
            0,
            "{\"type\":\"msg\",\"id\":\"" + EscapeJson(slotId) +
                "\",\"data\":\"" + EscapeJson(payload) + "\"}");
    }
    frame_->SendProcessMessage(PID_BROWSER, msg);

    return true;
}
