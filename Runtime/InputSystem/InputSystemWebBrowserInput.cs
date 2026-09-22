#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Ceffy.InputSystem
{
    internal sealed class InputSystemWebBrowserInput : IWebBrowserInput
    {
        private const float ScrollDeltaScale = 120.0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            WebBrowserInput.Register(new InputSystemWebBrowserInput());
        }

        public Vector2 GetMousePosition()
        {
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        }

        public bool GetMouseButton(int button)
        {
            var control = GetMouseButtonControl(button);
            return control != null && control.isPressed;
        }

        public bool GetMouseButtonDown(int button)
        {
            var control = GetMouseButtonControl(button);
            return control != null && control.wasPressedThisFrame;
        }

        public bool GetMouseButtonUp(int button)
        {
            var control = GetMouseButtonControl(button);
            return control != null && control.wasReleasedThisFrame;
        }

        public Vector2 GetMouseScrollDelta()
        {
            return Mouse.current != null ? Mouse.current.scroll.ReadValue() / ScrollDeltaScale : Vector2.zero;
        }

        public bool GetShift()
        {
            return Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
        }

        public bool GetControl()
        {
            return Keyboard.current != null && Keyboard.current.ctrlKey.isPressed;
        }

        public bool GetAlt()
        {
            return Keyboard.current != null && Keyboard.current.altKey.isPressed;
        }

        public bool GetRightAlt()
        {
            return Keyboard.current != null && Keyboard.current.rightAltKey.isPressed;
        }

        private static ButtonControl GetMouseButtonControl(int button)
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return null;

            return button switch
            {
                0 => mouse.leftButton,
                1 => mouse.rightButton,
                2 => mouse.middleButton,
                _ => null
            };
        }
    }
}
#endif
