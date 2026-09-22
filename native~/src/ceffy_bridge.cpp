#include "ceffy_bridge.h"
#include "browser_manager.h"
#include "cef_handlers.h"

#include <cstdio>
#include <cstdint>
#include <cstring>
#include <string>

#include <windows.h>

#include "include/cef_browser.h"

static std::string JsonEscape(const char* str)
{
    std::string out;
    out.reserve(std::strlen(str) + 16);
    out.push_back('"');
    for (const char* p = str; *p; ++p)
    {
        switch (*p)
        {
        case '\\': out += "\\\\"; break;
        case '"':  out += "\\\""; break;
        case '\n': out += "\\n";  break;
        case '\r': out += "\\r";  break;
        case '\t': out += "\\t";  break;
        default:   out.push_back(*p); break;
        }
    }
    out.push_back('"');
    return out;
}

static int UnityKeyCodeToWindowsVK(int unityKeyCode)
{
    switch (unityKeyCode)
    {
    case 8:   return VK_BACK;
    case 9:   return VK_TAB;
    case 13:  return VK_RETURN;
    case 27:  return VK_ESCAPE;
    case 32:  return VK_SPACE;
    case 44:  return VK_OEM_COMMA;
    case 45:  return VK_OEM_MINUS;
    case 46:  return VK_OEM_PERIOD;
    case 47:  return VK_OEM_2;
    case 59:  return VK_OEM_1;
    case 61:  return VK_OEM_PLUS;
    case 91:  return VK_OEM_4;
    case 92:  return VK_OEM_5;
    case 93:  return VK_OEM_6;
    case 96:  return VK_OEM_3;
    case 127: return VK_DELETE;
    case 271: return VK_RETURN;
    case 273: return VK_UP;
    case 274: return VK_DOWN;
    case 275: return VK_RIGHT;
    case 276: return VK_LEFT;
    case 277: return VK_INSERT;
    case 278: return VK_HOME;
    case 279: return VK_END;
    case 280: return VK_PRIOR;
    case 281: return VK_NEXT;
    case 39:  return VK_OEM_7;
    }

    if (unityKeyCode >= 48 && unityKeyCode <= 57)
        return '0' + (unityKeyCode - 48);
    if (unityKeyCode >= 97 && unityKeyCode <= 122)
        return 'A' + (unityKeyCode - 97);
    if (unityKeyCode >= 282 && unityKeyCode <= 293)
        return VK_F1 + (unityKeyCode - 282);

    return 0;
}

static int MakeNativeKeyCode(int windowsKeyCode, cef_key_event_type_t eventType)
{
    int nativeKeyCode = (windowsKeyCode << 16) | 1;
    if (eventType == KEYEVENT_KEYUP)
        nativeKeyCode |= (1 << 30) | (1 << 31);
    return nativeKeyCode;
}

int Ceffy_Initialize(const char* cachePath, int remoteDebuggingPort, long long adapterLuid, unsigned int graphicsVendorId, unsigned int graphicsDeviceId)
{
    printf(">> Ceffy_Initialize: cache=%s port=%d luid=0x%llX vendor=0x%X device=0x%X\n",
           cachePath ? cachePath : "(null)", remoteDebuggingPort, (unsigned long long)adapterLuid, graphicsVendorId, graphicsDeviceId);
    return BrowserManager::Instance().Initialize(cachePath, remoteDebuggingPort, (int64_t)adapterLuid, graphicsVendorId, graphicsDeviceId) ? 1 : 0;
}

void Ceffy_Shutdown()
{
    printf(">> Ceffy_Shutdown\n");
    BrowserManager::Instance().Shutdown();
}

void Ceffy_CloseAllBrowsers()
{
    printf(">> Ceffy_CloseAllBrowsers\n");
    BrowserManager::Instance().CloseAllBrowsers();
}

void Ceffy_SetSubProcessPath(const char* path)
{
    if (!path) return;
    printf(">> Ceffy_SetSubProcessPath: %s\n", path);
    BrowserManager::Instance().SetSubProcessPath(path);
}

int Ceffy_CreateBrowser(const char* url, int width, int height)
{
    if (!url) return -1;
    printf(">> Ceffy_CreateBrowser: %s %dx%d\n", url, width, height);
    return BrowserManager::Instance().CreateBrowser(url, width, height);
}

void Ceffy_CloseBrowser(int browserId)
{
    printf(">> Ceffy_CloseBrowser: %d\n", browserId);
    BrowserManager::Instance().CloseBrowser(browserId);
}

void Ceffy_Navigate(int browserId, const char* url)
{
    if (!url) return;
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry || !entry->browser) return;
    CefRefPtr<CefFrame> frame = entry->browser->GetMainFrame();
    if (frame)
        frame->LoadURL(url);
}

void Ceffy_ExecuteJS(int browserId, const char* code)
{
    if (!code) return;
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry || !entry->browser) return;
    CefRefPtr<CefFrame> frame = entry->browser->GetMainFrame();
    if (frame)
        frame->ExecuteJavaScript(code, "", 0);
}

void Ceffy_SendMessage(int browserId, const char* message)
{
    if (!message) return;
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry || !entry->browser) return;
    CefRefPtr<CefFrame> frame = entry->browser->GetMainFrame();
    if (!frame) return;

    std::string escaped = JsonEscape(message);
    std::string js =
        "(function(){try{if(window.ceffy&&typeof window.ceffy.onMessageFromUnity==='function')"
        "{window.ceffy.onMessageFromUnity(" + escaped + ");}}catch(e)"
        "{console.error('Ceffy dispatch failed',e);}})();";

    frame->ExecuteJavaScript(js, "", 0);
}

void* Ceffy_GetSharedHandle(int browserId)
{
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry || !entry->textureManager) return nullptr;
    return entry->textureManager->GetSharedHandle();
}

void* Ceffy_Resize(int browserId, int width, int height)
{
    if (width <= 0 || height <= 0) return nullptr;
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry) return nullptr;
    if (entry->width == width && entry->height == height)
        return entry->textureManager ? entry->textureManager->GetSharedHandle() : nullptr;

    entry->width = width;
    entry->height = height;

    if (entry->client && entry->client->GetCeffyRenderHandler())
        entry->client->GetCeffyRenderHandler()->SetSize(width, height);

    if (entry->textureManager)
        entry->textureManager->Resize(width, height);
    if (entry->browser && entry->browser->GetHost())
    {
        entry->browser->GetHost()->WasResized();
        entry->browser->GetHost()->Invalidate(PET_VIEW);
    }
    return entry->textureManager ? entry->textureManager->GetSharedHandle() : nullptr;
}

void Ceffy_RequestFrame(int browserId)
{
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry || !entry->browser) return;
    CefRefPtr<CefBrowserHost> host = entry->browser->GetHost();
    if (host)
        host->SendExternalBeginFrame();
}

int Ceffy_EnsureInitialized(int browserId)
{
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry) return 0;

    // CefBrowserHost::CreateBrowser is async -- the browser pointer is set
    // when OnAfterCreated fires on CEF's UI thread.  Wait for it so callers
    // don't silently skip Navigate/RequestFrame when the browser doesn't
    // exist yet (race between scene-reload speed and CEF close+create).
    for (int i = 0; i < 500 && !entry->browser; ++i)
        Sleep(10);

    if (!entry->browser) return 0;

    if (entry->client && entry->client->GetCeffyRenderHandler() &&
        entry->client->GetCeffyRenderHandler()->HasReceivedPaint()) return 1;

    CefRefPtr<CefBrowserHost> host = entry->browser->GetHost();
    if (!host) return 0;

    for (int i = 0; i < 100; ++i)
    {
        host->Invalidate(PET_VIEW);
        host->SendExternalBeginFrame();
        Sleep(10);
        if (entry->client && entry->client->GetCeffyRenderHandler() &&
            entry->client->GetCeffyRenderHandler()->HasReceivedPaint()) return 1;
    }
    return 0;
}

void Ceffy_SetFramerate(int browserId, int fps)
{
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry || !entry->browser) return;
    CefRefPtr<CefBrowserHost> host = entry->browser->GetHost();
    if (host)
        host->SetWindowlessFrameRate(fps);
}

void Ceffy_SendMouseMove(int browserId, int x, int y, int modifiers)
{
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry || !entry->browser) return;
    CefMouseEvent ev;
    ev.x = x;
    ev.y = y;
    ev.modifiers = static_cast<uint32_t>(modifiers);
    entry->browser->GetHost()->SendMouseMoveEvent(ev, false);
}

void Ceffy_SendMouseLeave(int browserId)
{
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry || !entry->browser) return;
    CefMouseEvent ev;
    ev.x = 0;
    ev.y = 0;
    ev.modifiers = 0;
    entry->browser->GetHost()->SendMouseMoveEvent(ev, true);
}

void Ceffy_SendMouseClick(int browserId, int x, int y, int button, int isUp, int clickCount, int modifiers)
{
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry || !entry->browser) return;
    CefMouseEvent ev;
    ev.x = x;
    ev.y = y;
    ev.modifiers = static_cast<uint32_t>(modifiers);
    CefRefPtr<CefBrowserHost> host = entry->browser->GetHost();
    if (isUp == 0)
        host->SetFocus(true);

    host->SendMouseClickEvent(
        ev, static_cast<CefBrowserHost::MouseButtonType>(button), isUp != 0, clickCount);
}

void Ceffy_SendMouseWheel(int browserId, int x, int y, int deltaX, int deltaY, int modifiers)
{
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry || !entry->browser) return;
    CefMouseEvent ev;
    ev.x = x;
    ev.y = y;
    ev.modifiers = static_cast<uint32_t>(modifiers);
    entry->browser->GetHost()->SendMouseWheelEvent(ev, deltaX, deltaY);
}

void Ceffy_SendKeyEvent(int browserId, int eventType, int windowsKeyCode,
                       int nativeKeyCode, int modifiers, int isSystemKey)
{
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry || !entry->browser) return;

    CefKeyEvent keyEvent;
    keyEvent.type = static_cast<cef_key_event_type_t>(eventType);
    keyEvent.windows_key_code = windowsKeyCode;
    keyEvent.native_key_code = nativeKeyCode;
    keyEvent.modifiers = static_cast<uint32_t>(modifiers);
    keyEvent.is_system_key = (isSystemKey != 0);
    keyEvent.focus_on_editable_field = true;

    entry->browser->GetHost()->SendKeyEvent(keyEvent);
}

void Ceffy_SendUnityKeyEvent(int browserId, int eventType, int unityKeyCode, int modifiers)
{
    int windowsKeyCode = UnityKeyCodeToWindowsVK(unityKeyCode);
    if (windowsKeyCode == 0) return;

    auto cefEventType = static_cast<cef_key_event_type_t>(eventType);
    int nativeKeyCode = MakeNativeKeyCode(windowsKeyCode, cefEventType);
    int isSystemKey = ((modifiers & EVENTFLAG_ALT_DOWN) != 0 && (modifiers & EVENTFLAG_ALTGR_DOWN) == 0) ? 1 : 0;
    Ceffy_SendKeyEvent(browserId, eventType, windowsKeyCode, nativeKeyCode, modifiers, isSystemKey);
}

void Ceffy_SetZoomLevel(int browserId, double zoomLevel)
{
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry || !entry->browser) return;
    CefRefPtr<CefBrowserHost> host = entry->browser->GetHost();
    if (host)
        host->SetZoomLevel(zoomLevel);
}

double Ceffy_GetZoomLevel(int browserId)
{
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry || !entry->browser) return 0.0;
    CefRefPtr<CefBrowserHost> host = entry->browser->GetHost();
    return host ? host->GetZoomLevel() : 0.0;
}

// ---------------------------------------------------------------------------
// HTML5 drag & drop
// ---------------------------------------------------------------------------

static CeffyRenderHandler* GetRenderHandler(BrowserEntry* entry)
{
    if (!entry || !entry->client) return nullptr;
    return entry->client->GetCeffyRenderHandler();
}

void Ceffy_DragTargetDragEnter(int browserId, int x, int y, int modifiers, int allowedOps)
{
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry || !entry->browser) return;
    CeffyRenderHandler* rh = GetRenderHandler(entry);
    if (!rh) return;

    uint32_t storedOps = 0;
    CefRefPtr<CefDragData> dragData = rh->GetDragData(storedOps);
    if (!dragData) return;

    uint32_t ops = (allowedOps != 0) ? static_cast<uint32_t>(allowedOps) : storedOps;
    CefMouseEvent ev;
    ev.x = x; ev.y = y; ev.modifiers = static_cast<uint32_t>(modifiers);
    entry->browser->GetHost()->DragTargetDragEnter(
        dragData, ev, static_cast<CefBrowserHost::DragOperationsMask>(ops));
}

void Ceffy_DragTargetDragOver(int browserId, int x, int y, int modifiers, int allowedOps)
{
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry || !entry->browser) return;

    uint32_t ops = (allowedOps != 0) ? static_cast<uint32_t>(allowedOps) : DRAG_OPERATION_EVERY;
    CefMouseEvent ev;
    ev.x = x; ev.y = y; ev.modifiers = static_cast<uint32_t>(modifiers);
    entry->browser->GetHost()->DragTargetDragOver(
        ev, static_cast<CefBrowserHost::DragOperationsMask>(ops));
}

void Ceffy_DragTargetDragLeave(int browserId)
{
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry || !entry->browser) return;
    entry->browser->GetHost()->DragTargetDragLeave();
}

void Ceffy_DragTargetDrop(int browserId, int x, int y, int modifiers)
{
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry || !entry->browser) return;
    CefMouseEvent ev;
    ev.x = x; ev.y = y; ev.modifiers = static_cast<uint32_t>(modifiers);
    entry->browser->GetHost()->DragTargetDrop(ev);
}

void Ceffy_DragSourceEndedAt(int browserId, int x, int y)
{
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry || !entry->browser) return;
    CeffyRenderHandler* rh = GetRenderHandler(entry);
    uint32_t op = rh ? rh->GetCurrentDragOp() : static_cast<uint32_t>(DRAG_OPERATION_NONE);
    if (rh) rh->ClearDragState();
    entry->browser->GetHost()->DragSourceEndedAt(x, y, static_cast<CefBrowserHost::DragOperationsMask>(op));
}

void Ceffy_DragSourceSystemDragEnded(int browserId)
{
    BrowserEntry* entry = BrowserManager::Instance().GetBrowser(browserId);
    if (!entry || !entry->browser) return;
    entry->browser->GetHost()->DragSourceSystemDragEnded();
}

int Ceffy_PollCallback(int* type, int* browserId, const char** data)
{
    if (!type || !browserId || !data) return 0;

    CeffyCallbackData cb;
    if (!BrowserManager::Instance().GetCallbackQueue().TryPop(cb))
        return 0;

    *type = static_cast<int>(cb.type);
    *browserId = cb.browserId;

    size_t len = cb.data.size();
    char* copy = new (std::nothrow) char[len + 1];
    if (copy)
    {
        std::memcpy(copy, cb.data.c_str(), len + 1);
        *data = copy;
    }
    else
    {
        *data = nullptr;
    }
    return 1;
}

void Ceffy_FreeString(const char* str)
{
    delete[] str;
}
