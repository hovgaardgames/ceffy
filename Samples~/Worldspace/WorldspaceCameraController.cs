using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace Ceffy.Demos.Worldspace
{
    /// <summary>
    /// Simple fly camera: WASD to move, Q/E for up/down, hold right mouse to look.
    /// Right-mouse look is used so left-click still reaches the world-space Ceffy page.
    /// </summary>
    public sealed class WorldspaceCameraController : MonoBehaviour
    {
#if ENABLE_INPUT_SYSTEM
        private const float InputSystemLookScale = 0.1f;
#endif

        [Tooltip("Meters per second for WASD / QE movement.")]
        public float MoveSpeed = 4f;

        [Tooltip("Multiplier applied while Left Shift is held.")]
        public float SprintMultiplier = 2.5f;

        [Tooltip("Mouse look sensitivity while the right mouse button is held.")]
        public float LookSensitivity = 2f;

        public float MinPitch = -80f;
        public float MaxPitch = 80f;

        private float yaw;
        private float pitch;

        private void Start()
        {
            var euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        }

        private void Update()
        {
            if (IsLooking())
            {
                var look = GetLookDelta();
                yaw += look.x * LookSensitivity;
                pitch -= look.y * LookSensitivity;
                pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            Vector3 local = GetMovement();

            if (local.sqrMagnitude > 1f)
                local.Normalize();

            float speed = IsSprinting() ? MoveSpeed * SprintMultiplier : MoveSpeed;
            transform.position += transform.TransformDirection(local) * (speed * Time.deltaTime);
        }

#if ENABLE_INPUT_SYSTEM
        private static bool IsLooking()
        {
            var mouse = Mouse.current;
            return mouse != null && mouse.rightButton.isPressed;
        }

        private static Vector2 GetLookDelta()
        {
            return Mouse.current.delta.ReadValue() * InputSystemLookScale;
        }

        private static Vector3 GetMovement()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return Vector3.zero;

            return new Vector3(
                KeyAxis(keyboard.dKey, keyboard.aKey),
                KeyAxis(keyboard.eKey, keyboard.qKey),
                KeyAxis(keyboard.wKey, keyboard.sKey));
        }

        private static bool IsSprinting()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.leftShiftKey.isPressed;
        }

        private static float KeyAxis(KeyControl positive, KeyControl negative)
        {
            var value = 0.0f;
            if (positive.isPressed)
                value += 1.0f;
            if (negative.isPressed)
                value -= 1.0f;
            return value;
        }
#else
        private static bool IsLooking()
        {
            return Input.GetMouseButton(1);
        }

        private static Vector2 GetLookDelta()
        {
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        }

        private static Vector3 GetMovement()
        {
            return new Vector3(
                KeyAxis(KeyCode.D, KeyCode.A),
                KeyAxis(KeyCode.E, KeyCode.Q),
                KeyAxis(KeyCode.W, KeyCode.S));
        }

        private static bool IsSprinting()
        {
            return Input.GetKey(KeyCode.LeftShift);
        }

        private static float KeyAxis(KeyCode positive, KeyCode negative)
        {
            float value = 0.0f;
            if (Input.GetKey(positive))
                value += 1.0f;
            if (Input.GetKey(negative))
                value -= 1.0f;
            return value;
        }
#endif
    }
}
