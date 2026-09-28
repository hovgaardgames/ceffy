using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Ceffy
{
    /// <summary>
    /// Low-level bindings to the Ceffy native plugin. Most consumers should use <see cref="CeffyInstance"/> instead.
    /// </summary>
    public static class NativeBridge
    {
        private const string DLL = "ceffy_native";

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Ceffy_Initialize(string cachePath, int remoteDebuggingPort, long adapterLuid, uint graphicsVendorId, uint graphicsDeviceId);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Ceffy_Shutdown();

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Ceffy_CloseAllBrowsers();

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void Ceffy_SetSubProcessPath(string path);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int Ceffy_CreateBrowser(string url, int width, int height);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Ceffy_CloseBrowser(int browserId);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void Ceffy_Navigate(int browserId, string url);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void Ceffy_ExecuteJS(int browserId, string code);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void Ceffy_SendMessage(int browserId, string message);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr Ceffy_GetSharedHandle(int browserId);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr Ceffy_Resize(int browserId, int width, int height);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Ceffy_RequestFrame(int browserId);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Ceffy_EnsureInitialized(int browserId);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Ceffy_SetFramerate(int browserId, int fps);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Ceffy_SendMouseMove(int browserId, int x, int y, int modifiers);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Ceffy_SendMouseLeave(int browserId);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Ceffy_SendMouseClick(int browserId, int x, int y, int button, int isUp, int clickCount, int modifiers);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Ceffy_SendMouseWheel(int browserId, int x, int y, int deltaX, int deltaY, int modifiers);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Ceffy_SendKeyEvent(int browserId, int eventType, int windowsKeyCode, int nativeKeyCode, int modifiers, int isSystemKey);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Ceffy_SendUnityKeyEvent(int browserId, int eventType, int unityKeyCode, int modifiers);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Ceffy_SetZoomLevel(int browserId, double zoomLevel);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern double Ceffy_GetZoomLevel(int browserId);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Ceffy_DragTargetDragEnter(int browserId, int x, int y, int modifiers, int allowedOps);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Ceffy_DragTargetDragOver(int browserId, int x, int y, int modifiers, int allowedOps);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Ceffy_DragTargetDragLeave(int browserId);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Ceffy_DragTargetDrop(int browserId, int x, int y, int modifiers);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Ceffy_DragSourceEndedAt(int browserId, int x, int y);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Ceffy_DragSourceSystemDragEnded(int browserId);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Ceffy_PollCallback(out int type, out int browserId, out IntPtr data);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Ceffy_FreeString(IntPtr str);

        /// <summary>
        /// Poll one callback from the native queue, marshal it to managed strings,
        /// and free the native memory. Returns true if a callback was available.
        /// </summary>
        public static bool PollCallback(out int type, out int browserId, out string data)
        {
            IntPtr dataPtr;
            int result = Ceffy_PollCallback(out type, out browserId, out dataPtr);
            if (result != 0 && dataPtr != IntPtr.Zero)
            {
                data = Marshal.PtrToStringAnsi(dataPtr);
                Ceffy_FreeString(dataPtr);
                return true;
            }
            type = 0;
            browserId = 0;
            data = null;
            return false;
        }
        
        /// <summary>
        /// Translate a Unity KeyCode and send a key event to CEF.
        /// No-op if the KeyCode has no known mapping.
        /// </summary>
        public static void SendKeyDown(int browserId, KeyCode key, EventFlags modifiers)
            => SendKey(browserId, KeyEventType.RawKeyDown, key, modifiers);
        
        /// <inheritdoc cref="SendKeyDown"/>
        public static void SendKeyUp(int browserId, KeyCode key, EventFlags modifiers)
            => SendKey(browserId, KeyEventType.KeyUp, key, modifiers);

        private static void SendKey(int browserId, KeyEventType type, KeyCode key, EventFlags modifiers)
        {
            Ceffy_SendUnityKeyEvent(browserId, (int)type, (int)key, (int)modifiers);
        }
    }
}
