using UnityEngine;

namespace Ceffy.Demos.Worldspace
{
    /// <summary>
    /// Simple fly camera: WASD to move, Q/E for up/down, hold right mouse to look.
    /// Right-mouse look is used so left-click still reaches the world-space Ceffy page.
    /// </summary>
    public sealed class WorldspaceCameraController : MonoBehaviour
    {
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
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxisRaw("Mouse X") * LookSensitivity;
                pitch -= Input.GetAxisRaw("Mouse Y") * LookSensitivity;
                pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            Vector3 local = new Vector3(
                KeyAxis(KeyCode.D, KeyCode.A),
                KeyAxis(KeyCode.E, KeyCode.Q),
                KeyAxis(KeyCode.W, KeyCode.S));

            if (local.sqrMagnitude > 1f)
                local.Normalize();

            float speed = Input.GetKey(KeyCode.LeftShift) ? MoveSpeed * SprintMultiplier : MoveSpeed;
            transform.position += transform.TransformDirection(local) * (speed * Time.deltaTime);
        }

        private static float KeyAxis(KeyCode positive, KeyCode negative)
        {
            float value = 0f;
            if (Input.GetKey(positive)) value += 1f;
            if (Input.GetKey(negative)) value -= 1f;
            return value;
        }
    }
}
