using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Ceffy
{
    
    /// <summary>
    /// Singleton manager for the Ceffy native CEF runtime.
    /// - Starts lazily (first use).
    /// - Stays alive across scene loads.
    /// - Avoids native shutdown between Editor play sessions because CEF cannot be safely reinitialized in-process.
    /// </summary>
    public sealed class WebBrowserRuntime : MonoBehaviour
    {
        private const string CeffyHelper = "CeffyHelper.ceffy";
        private static readonly object instanceLock = new();
        private static WebBrowserRuntime instance;

        /// <summary>
        /// When true, Ceffy logs detailed lifecycle and diagnostics messages.
        /// Errors and warnings are always logged regardless of this setting.
        /// Set via the VerboseLogging toggle on any WebBrowser component.
        /// </summary>
        public static bool VerboseLogging { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (instance)
            {
                try
                {
    #if UNITY_EDITOR
                    // In the Editor, avoid native CEF shutdown during play-mode/domain reload cycles.
                    // Re-initializing libcef in the same editor process is unstable and can crash.
                    if (instance.gameObject)
                        Destroy(instance.gameObject);
    #else
                    instance.ForceShutdownAndCleanup();
    #endif
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Ceffy] ResetStatics cleanup error: {ex.Message}");
                }
            }
            instance = null;
            VerboseLogging = false;
        }

        /// <summary>
        /// Gets the shared runtime instance, creating it on first access.
        /// </summary>
        public static WebBrowserRuntime Instance
        {
            get
            {
                lock (instanceLock)
                {
                    if (!instance)
                    {
                        var go = new GameObject("Ceffy WebBrowserRuntime");
                        DontDestroyOnLoad(go);
                        instance = go.AddComponent<WebBrowserRuntime>();
                    }
                    return instance;
                }
            }
        }

        private bool started;
        private bool ready;
        private int remoteDebuggingPortRequested;

        public bool IsReady => ready;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

    #if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    #endif

            Application.quitting -= Shutdown;
            Application.quitting += Shutdown;
        }

        private void OnDestroy()
        {
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    #endif

            Application.quitting -= Shutdown;
        }

    #if UNITY_EDITOR
        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                lock (instanceLock)
                {
                    // Keep native runtime alive between play sessions to avoid libcef
                    // shutdown/reinitialize crashes inside the Unity editor process.
                    instance = null;
                }
            }
        }
    #endif

        /// <summary>
        /// Starts the runtime if it isn't already started.
        /// The first caller's options win; later callers with different options are ignored.
        /// </summary>
        public void EnsureStarted(int remoteDebuggingPort)
        {
            lock (instanceLock)
            {
                if (started)
                {
                    if (remoteDebuggingPort > 0 && remoteDebuggingPortRequested == 0)
                        Debug.LogWarning($"Ceffy runtime already started without remote debugging. Ignoring later request for port {remoteDebuggingPort}.");
                    return;
                }

                remoteDebuggingPortRequested = remoteDebuggingPort;
                started = StartRuntime();
            }
        }

        /// <summary>
        /// Creates a native browser and returns its identifier, or -1 if the runtime is not ready.
        /// </summary>
        public int CreateBrowser(int width, int height, string url)
        {
            EnsureStarted(0);
            if (!ready) return -1;
            return NativeBridge.Ceffy_CreateBrowser(url ?? "", width, height);
        }

        private static string GetHelperPath()
        {
    #if UNITY_EDITOR
            var pkgInfo = UnityEditor.PackageManager.PackageInfo
                .FindForAssembly(typeof(NativeBridge).Assembly);
            var dir = pkgInfo?.resolvedPath ?? Path.GetFullPath("./");
            return Path.Combine(dir, "NativeRuntime", "win-x64", CeffyHelper);
    #else
            var assembly = Assembly.GetExecutingAssembly();
            var managedDir = Path.GetDirectoryName(assembly.Location);
            var dataDir = Path.GetDirectoryName(managedDir);
            var dir = Path.GetDirectoryName(dataDir);
            return Path.GetFullPath(Path.Combine(dir, "Ceffy.Runtime", "win-x64", CeffyHelper));
    #endif
        }

        private static string GetRuntimeDirectory()
        {
            return Path.GetDirectoryName(GetHelperPath());
        }

    #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);
    #endif

        private bool PrepareNativeBridge()
        {
            var runtimeDir = GetRuntimeDirectory();
            var bridgePath = Path.Combine(runtimeDir, "ceffy_native.dll");

            if (!Directory.Exists(runtimeDir))
            {
                Debug.LogError($"[Ceffy] Runtime directory not found: {runtimeDir}");
                return false;
            }
            if (!File.Exists(bridgePath))
            {
                Debug.LogError($"[Ceffy] Native bridge not found: {bridgePath}");
                return false;
            }

    #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            SetDllDirectory(runtimeDir);
            var handle = LoadLibrary(bridgePath);
            if (handle == IntPtr.Zero)
            {
                var err = Marshal.GetLastWin32Error();
                Debug.LogError($"[Ceffy] Failed to load native bridge '{bridgePath}' (Win32={err}).");
                return false;
            }
    #endif

            if (VerboseLogging)
                Debug.Log($"[Ceffy] Native bridge loaded from: {bridgePath}");
            return true;
        }

        private bool StartRuntime()
        {
            if (!PrepareNativeBridge())
                return false;

            var helperPath = GetHelperPath();
            if (VerboseLogging)
                Debug.Log($"[Ceffy] Helper path: {helperPath}");

            NativeBridge.Ceffy_SetSubProcessPath(helperPath);

            var cachePath = GetInstanceCachePath();
            if (!Directory.Exists(cachePath))
                Directory.CreateDirectory(cachePath);

            if (VerboseLogging)
                Debug.Log($"[Ceffy] Cache path: {cachePath}");

            // Use a different remote debugging port in standalone so Editor and build can run at once.
            int port = GetRemoteDebuggingPortForThisProcess();
            // Pass Unity's GPU so CEF creates its D3D11 device on the same adapter (same WDDM priority, no cross-adapter copy).
            uint vendorId = (uint)SystemInfo.graphicsDeviceVendorID;
            uint deviceId = (uint)SystemInfo.graphicsDeviceID;
            int result = NativeBridge.Ceffy_Initialize(cachePath, port, 0L, vendorId, deviceId);
            if (result != 0)
            {
                ready = true;
                if (VerboseLogging)
                    Debug.Log("[Ceffy] Native CEF initialized successfully.");
                return true;
            }
            Debug.LogError("[Ceffy] Failed to initialize native CEF.");
            return false;
        }

        /// <summary>
        /// Cache path unique per runtime context so Editor and standalone (and multiple builds)
        /// never share the same CEF profile. CEF locks the profile directory; sharing it causes
        /// the second process to fail or crash.
        /// </summary>
        private static string GetInstanceCachePath()
        {
            var basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Ceffy", "BrowserCache");

    #if UNITY_EDITOR
            var projectPath = Path.GetFullPath("./");
            var projectName = SanitizePathSegment(new DirectoryInfo(projectPath).Name);
            return Path.Combine(basePath, "Editor", projectName);
    #else
            var company = SanitizePathSegment(Application.companyName);
            var product = SanitizePathSegment(Application.productName);
            return Path.Combine(basePath, "Player", company, product);
    #endif
        }

        private static string SanitizePathSegment(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "unknown";
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (Array.IndexOf(invalid, c) >= 0 || c == ' ' || c == '.')
                    sb.Append('_');
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// In standalone, use a different port than the default 9222 so Editor (9222) and build
        /// can both run with remote debugging, or at least so the second process does not fail to init.
        /// </summary>
        private int GetRemoteDebuggingPortForThisProcess()
        {
            int requested = remoteDebuggingPortRequested;
    #if !UNITY_EDITOR
            if (requested == 9222)
                requested = 9223;
    #endif
            return requested;
        }

        /// <summary>
        /// Shuts down the managed runtime and, outside the Editor, the native CEF runtime.
        /// </summary>
        public void Shutdown()
        {
            lock (instanceLock)
            {
                if (!started)
                    return;

                ShutdownInternal();
            }
        }

        private void ForceShutdownAndCleanup()
        {
            lock (instanceLock)
            {
                ShutdownInternal();
            }

            if (gameObject)
            {
                Destroy(gameObject);
            }
        }

        private void ShutdownInternal()
        {
            if (ready)
            {
                if (VerboseLogging)
                    Debug.Log("[Ceffy] Shutting down native CEF...");
                try
                {
    #if UNITY_EDITOR
                    // Critical: avoid CefShutdown in Unity Editor play-mode cycles.
                    // CEF re-init in the same editor process is unstable and can crash.
                    // But still close browser instances so helper subprocesses can exit.
                    NativeBridge.Ceffy_CloseAllBrowsers();
                    if (VerboseLogging)
                        Debug.Log("[Ceffy] Editor mode: closed browsers, skipping native Ceffy_Shutdown to avoid CEF re-init crash.");
    #else
                    NativeBridge.Ceffy_Shutdown();
    #endif
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Ceffy] Shutdown error: {ex.Message}");
                }
                ready = false;
            }

            started = false;
            remoteDebuggingPortRequested = 0;
        }
    }
}
