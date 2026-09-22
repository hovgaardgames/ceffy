#include <windows.h>
#include "include/cef_app.h"
#include "helper_app.h"

int APIENTRY wWinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance,
                      LPTSTR lpCmdLine, int nCmdShow) {
    CefMainArgs main_args(hInstance);
    CefRefPtr<CeffyRendererApp> app = new CeffyRendererApp();
    return CefExecuteProcess(main_args, app, nullptr);
}
