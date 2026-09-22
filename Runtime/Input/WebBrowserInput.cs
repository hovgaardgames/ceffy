using UnityEngine;

namespace Ceffy
{
    /// <summary>
    /// Provides browser input through the active input provider.
    /// </summary>
    public static class WebBrowserInput
    {
        private static readonly IWebBrowserInput LegacyInput = new LegacyWebBrowserInput();
        private static IWebBrowserInput currentInput = LegacyInput;

        /// <summary>
        /// Registers an input provider, or restores legacy input when <paramref name="input"/> is null.
        /// </summary>
        public static void Register(IWebBrowserInput input)
        {
            currentInput = input ?? LegacyInput;
        }

        public static Vector2 GetMousePosition()
        {
            return currentInput.GetMousePosition();
        }

        public static bool GetMouseButton(int button)
        {
            return currentInput.GetMouseButton(button);
        }

        public static bool GetMouseButtonDown(int button)
        {
            return currentInput.GetMouseButtonDown(button);
        }

        public static bool GetMouseButtonUp(int button)
        {
            return currentInput.GetMouseButtonUp(button);
        }

        public static Vector2 GetMouseScrollDelta()
        {
            return currentInput.GetMouseScrollDelta();
        }

        public static bool GetShift()
        {
            return currentInput.GetShift();
        }

        public static bool GetControl()
        {
            return currentInput.GetControl();
        }

        public static bool GetAlt()
        {
            return currentInput.GetAlt();
        }

        public static bool GetRightAlt()
        {
            return currentInput.GetRightAlt();
        }
    }
}
