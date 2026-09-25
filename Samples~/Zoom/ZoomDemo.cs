using UnityEngine;

namespace Ceffy.Demos.Zoom
{
    public sealed class ZoomDemo : MonoBehaviour
    {
        [Tooltip("Optional. If not set, the component will search the scene.")]
        public CeffyInstance ceffyInstance;

        private static readonly double[] ZoomPresets = { 25, 50, 75, 90, 100, 110, 125, 150, 175, 200, 250, 300 };

        private double _currentPercent = 100;
        private bool _nativeAvailable = true;
        private Rect _windowRect = new Rect(10, 10, 280, 300);
        private GUIStyle _windowStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _smallButtonStyle;
        private bool _stylesInitialized;

        private void Awake()
        {
            if (ceffyInstance == null)
            {
                ceffyInstance = GetComponent<CeffyInstance>();
                if (ceffyInstance == null)
                    ceffyInstance = FindAnyObjectByType<CeffyInstance>();
            }
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _windowStyle = new GUIStyle(GUI.skin.window) { fontSize = 14 };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };

            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 13 };

            _smallButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                fixedWidth = 36,
                fixedHeight = 28
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (ceffyInstance == null) return;

            InitStyles();

            _windowRect = GUILayout.Window(
                GetEntityId().GetHashCode(),
                _windowRect,
                DrawWindow,
                "Zoom Controls",
                _windowStyle,
                GUILayout.Width(280)
            );
        }

        private void ApplyZoom(double percent)
        {
            _currentPercent = System.Math.Max(25, System.Math.Min(300, percent));
            if (!_nativeAvailable) return;
            try { ceffyInstance.SetZoomPercent(_currentPercent); }
            catch (System.EntryPointNotFoundException) { _nativeAvailable = false; }
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.Space(5);

            if (!_nativeAvailable)
                GUILayout.Label("Native DLL needs rebuild (Ceffy_SetZoomLevel not found)", _labelStyle);

            GUILayout.Label($"Zoom: {_currentPercent:F0}%", _labelStyle);
            GUILayout.Space(5);

            float sliderVal = GUILayout.HorizontalSlider((float)_currentPercent, 25f, 300f);
            if (Mathf.Abs(sliderVal - (float)_currentPercent) > 0.5f)
                ApplyZoom(sliderVal);

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("\u2013", _smallButtonStyle)) StepZoom(-1);
            if (GUILayout.Button("+", _smallButtonStyle)) StepZoom(1);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset (100%)", _buttonStyle, GUILayout.Height(28)))
                ApplyZoom(100);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("Presets:", _labelStyle);
            const int columns = 4;
            for (int i = 0; i < ZoomPresets.Length; i += columns)
            {
                GUILayout.BeginHorizontal();
                for (int j = 0; j < columns && i + j < ZoomPresets.Length; j++)
                {
                    double preset = ZoomPresets[i + j];
                    if (GUILayout.Button($"{preset:F0}%", _buttonStyle))
                        ApplyZoom(preset);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(5);
            GUI.DragWindow();
        }

        private void StepZoom(int direction)
        {
            if (direction > 0)
            {
                for (int i = 0; i < ZoomPresets.Length; i++)
                    if (ZoomPresets[i] > _currentPercent + 0.5) { ApplyZoom(ZoomPresets[i]); return; }
            }
            else
            {
                for (int i = ZoomPresets.Length - 1; i >= 0; i--)
                    if (ZoomPresets[i] < _currentPercent - 0.5) { ApplyZoom(ZoomPresets[i]); return; }
            }
        }
    }
}
