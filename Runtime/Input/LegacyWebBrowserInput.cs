using UnityEngine;

namespace Ceffy
{
    internal sealed class LegacyWebBrowserInput : IWebBrowserInput
    {
        public Vector2 GetMousePosition()
        {
            return Input.mousePosition;
        }

        public bool GetMouseButton(int button)
        {
            return Input.GetMouseButton(button);
        }

        public bool GetMouseButtonDown(int button)
        {
            return Input.GetMouseButtonDown(button);
        }

        public bool GetMouseButtonUp(int button)
        {
            return Input.GetMouseButtonUp(button);
        }

        public Vector2 GetMouseScrollDelta()
        {
            return Input.mouseScrollDelta;
        }

        public bool GetShift()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        public bool GetControl()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

        public bool GetAlt()
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        public bool GetRightAlt()
        {
            return Input.GetKey(KeyCode.RightAlt);
        }
    }
}
