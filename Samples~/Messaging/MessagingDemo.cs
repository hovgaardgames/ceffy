using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ceffy.Demos.Messaging
{
    /// <summary>
    /// Minimal demo component showing how to send/receive messages between JavaScript and Unity.
    /// Attach this to any GameObject (optionally alongside a CeffyInstance).
    /// </summary>
    public sealed class MessagingDemo : MonoBehaviour
    {
        [Tooltip("Optional. If not set, the component will search the scene.")]
        public CeffyInstance ceffyInstance;

        [TextArea(3, 12)]
        public string lastMessageFromJs;

        private void Awake()
        {
            if (ceffyInstance == null)
            {
                ceffyInstance = GetComponent<CeffyInstance>();
                if (ceffyInstance == null)
                    ceffyInstance = FindAnyObjectByType<CeffyInstance>();
            }
        }

        private void OnEnable()
        {
            if (ceffyInstance != null)
                ceffyInstance.OnMessageFromCeffy += HandleMessageFromJs;
        }

        private void OnDisable()
        {
            if (ceffyInstance != null)
                ceffyInstance.OnMessageFromCeffy -= HandleMessageFromJs;
        }

        private void Start()
        {
            // Prove Unity -> JS works (page can log it via onMessageFromUnity).
            if (ceffyInstance != null)
                ceffyInstance.SendToCeffy("{\"type\":\"unity-ready\",\"ts\":" + Time.frameCount + "}");
        }

        private void HandleMessageFromJs(string message)
        {
            lastMessageFromJs = message;
            Debug.Log($"[Ceffy MessagingDemo] From JS: {message}");

            // Echo back so the page can see round-trip behavior.
            if (ceffyInstance != null)
                ceffyInstance.SendToCeffy("{\"type\":\"echo\",\"message\":" + JsonEscape(message) + "}");
        }

        public void SendToPage(string message)
        {
            if (ceffyInstance == null)
            {
                Debug.LogWarning("[Ceffy MessagingDemo] No CeffyInstance assigned/found.");
                return;
            }

            ceffyInstance.SendToCeffy(message);
        }

        private static string JsonEscape(string value)
        {
            if (value == null)
                return "null";

            // Minimal JSON string escaping (sufficient for demo usage).
            return "\"" + value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t") + "\"";
        }

        private void OnGUI()
        {
            if (GUI.Button(new Rect(Screen.width - 210, Screen.height - 40, 200, 30), "Switch to PerformanceDemo"))
            {
                SceneManager.LoadScene("PerformanceDemo");
            }
        }
    }
}

