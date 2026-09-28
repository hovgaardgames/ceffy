using System;
using UnityEngine;

namespace Ceffy.Demos.SharedInstance
{
    /// <summary>
    /// Drives the Shared Instance demo scene: spins the orbit root and pushes periodic
    /// content updates into each employee's nameplate page. All objects are authored in
    /// the scene; this component only animates and feeds them.
    /// </summary>
    public sealed class SharedInstanceDemo : MonoBehaviour
    {
        [Serializable]
        public sealed class Employee
        {
            [Tooltip("Name shown on the nameplate page.")]
            public string Name = "Employee";

            [Tooltip("Shared nameplate instance that follows this employee's cube.")]
            public CeffyInstance Nameplate;
        }

        [Header("Orbit")]
        [Tooltip("Spun around world Y so the employee cubes orbit. Optional.")]
        public Transform OrbitRoot;

        [Tooltip("Degrees per second applied to OrbitRoot.")]
        public float OrbitSpeed = 20f;

        [Header("Content updates")]
        [Tooltip("Seconds between nameplate progress updates.")]
        public float ProgressUpdateInterval = 0.1f;

        [Tooltip("Employees authored in the scene, each paired with its nameplate instance.")]
        public Employee[] Employees = Array.Empty<Employee>();

        [Header("World panel")]
        [Tooltip("Interactive panel. Optional; only used to log its messages.")]
        public CeffyInstance Panel;

        private float[] progress;
        private float updateTimer;

        private void Start()
        {
            progress = new float[Employees.Length];

            // Shared instances queue messages until their page has loaded, so this is safe to send now.
            for (int i = 0; i < Employees.Length; i++)
            {
                progress[i] = i / (float)Employees.Length;
                SendNameplate(i);
            }

            if (Panel)
                Panel.OnMessageFromCeffy += msg => Debug.Log($"[SharedInstanceDemo] Panel message: {msg}");
        }

        private void Update()
        {
            // Derive the angle from absolute time rather than accumulating deltaTime, so
            // frame-time jitter cannot make the orbit judder or drift over a long session.
            if (OrbitRoot)
                OrbitRoot.localRotation = Quaternion.Euler(0f, Mathf.Repeat(OrbitSpeed * Time.time, 360f), 0f);

            updateTimer += Time.deltaTime;
            if (updateTimer < ProgressUpdateInterval)
                return;

            updateTimer = 0f;
            for (int i = 0; i < Employees.Length; i++)
            {
                progress[i] = Mathf.Repeat(progress[i] + 0.02f, 1f);
                SendNameplate(i);
            }
        }

        private void SendNameplate(int index)
        {
            var employee = Employees[index];
            if (!employee.Nameplate)
                return;

            int pct = Mathf.RoundToInt(progress[index] * 100f);
            employee.Nameplate.SendToCeffy("{\"name\":\"" + employee.Name + "\",\"progress\":" + pct + "}");
        }
    }
}
