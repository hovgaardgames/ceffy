#pragma once

#ifdef Ceffy_NATIVE_EXPORTS
#define Ceffy_API __declspec(dllexport)
#else
#define Ceffy_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define Ceffy_CALLBACK_CONSOLE_MESSAGE 1
#define Ceffy_CALLBACK_JS_MESSAGE      2
#define Ceffy_CALLBACK_NATIVE_LOG      4

Ceffy_API int  Ceffy_Initialize(const char* cachePath, int remoteDebuggingPort, long long adapterLuid, unsigned int graphicsVendorId, unsigned int graphicsDeviceId);
Ceffy_API void Ceffy_Shutdown();
Ceffy_API void Ceffy_CloseAllBrowsers();
Ceffy_API void Ceffy_SetSubProcessPath(const char* path);

Ceffy_API int  Ceffy_CreateBrowser(const char* url, int width, int height);
Ceffy_API void Ceffy_CloseBrowser(int browserId);

Ceffy_API void  Ceffy_Navigate(int browserId, const char* url);
Ceffy_API void  Ceffy_ExecuteJS(int browserId, const char* code);
Ceffy_API void  Ceffy_SendMessage(int browserId, const char* message);

Ceffy_API void* Ceffy_GetSharedHandle(int browserId);
Ceffy_API void* Ceffy_Resize(int browserId, int width, int height);
Ceffy_API void  Ceffy_RequestFrame(int browserId);
Ceffy_API int   Ceffy_EnsureInitialized(int browserId);
Ceffy_API void  Ceffy_SetFramerate(int browserId, int fps);

Ceffy_API void Ceffy_SendMouseMove(int browserId, int x, int y, int modifiers);
Ceffy_API void Ceffy_SendMouseLeave(int browserId);
Ceffy_API void Ceffy_SendMouseClick(int browserId, int x, int y, int button, int isUp, int clickCount, int modifiers);
Ceffy_API void Ceffy_SendMouseWheel(int browserId, int x, int y, int deltaX, int deltaY, int modifiers);
Ceffy_API void Ceffy_SendKeyEvent(int browserId, int eventType, int windowsKeyCode, int nativeKeyCode, int modifiers, int isSystemKey);
Ceffy_API void Ceffy_SendUnityKeyEvent(int browserId, int eventType, int unityKeyCode, int modifiers);

Ceffy_API void   Ceffy_SetZoomLevel(int browserId, double zoomLevel);
Ceffy_API double Ceffy_GetZoomLevel(int browserId);

// HTML5 drag & drop support.
// Call flow for a drag that originates inside the browser:
//   1. OnStartDragging fires → Ceffy_CALLBACK_DRAG_START queued to Unity
//   2. Unity calls Ceffy_DragTargetDragEnter once at the drag start position
//   3. Unity calls Ceffy_DragTargetDragOver for each subsequent mouse move
//   4. On mouse-up inside: Ceffy_DragTargetDrop → Ceffy_DragSourceEndedAt → Ceffy_DragSourceSystemDragEnded
//   5. On mouse-up outside: Ceffy_DragTargetDragLeave → Ceffy_DragSourceEndedAt → Ceffy_DragSourceSystemDragEnded
Ceffy_API void Ceffy_DragTargetDragEnter(int browserId, int x, int y, int modifiers, int allowedOps);
Ceffy_API void Ceffy_DragTargetDragOver(int browserId, int x, int y, int modifiers, int allowedOps);
Ceffy_API void Ceffy_DragTargetDragLeave(int browserId);
Ceffy_API void Ceffy_DragTargetDrop(int browserId, int x, int y, int modifiers);
Ceffy_API void Ceffy_DragSourceEndedAt(int browserId, int x, int y);
Ceffy_API void Ceffy_DragSourceSystemDragEnded(int browserId);

Ceffy_API int  Ceffy_PollCallback(int* type, int* browserId, const char** data);
Ceffy_API void Ceffy_FreeString(const char* str);

#ifdef __cplusplus
}
#endif
