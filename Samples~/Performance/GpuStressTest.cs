using UnityEngine;
using UnityEngine.SceneManagement;
namespace Ceffy.Demos.Performance 
{
    public class GpuStressTest : MonoBehaviour
    {
        [Header("Frame Rate")]
        [Tooltip("Target frame rate. Set to -1 for unlimited.")]
        public int targetFrameRate = -1;
        
        [Header("Stress Test Settings")]
        [Range(0, 100)]
        public int stressLevel = 5;
        
        [Header("Fill Rate Stress (Overdraw)")]
        public bool enableOverdrawStress = true;
        [Range(1, 200)]
        public int overdrawLayers = 100;
        [Range(1, 64)]
        public int computeIterations = 32;
        
        [Header("Dynamic Objects Stress")]
        public bool enableObjectStress = true;
        [Range(10, 5000)]
        public int stressObjectCount = 500;
        
        [Header("Shader Reference")]
        [Tooltip("Assign GpuStressShader here to ensure it's included in builds")]
        public Shader stressShader;
        
        private Material _stressMaterial;
        private GameObject[] _stressObjects;
        private Mesh _quadMesh;
        
        private void Start()
        {
            ApplyFrameRate();
            CreateStressMaterial();
            CreateQuadMesh();
            SpawnStressObjects();
        }
        
        private void OnValidate()
        {
            ApplyFrameRate();
        }
        
        private void ApplyFrameRate()
        {
            Application.targetFrameRate = targetFrameRate;
        }
        
        private void CreateStressMaterial()
        {
            // Use assigned shader first, then fallback to Shader.Find
            Shader shader = stressShader;
            
            if (shader == null)
            {
                shader = Shader.Find("Hidden/GpuStress");
            }
            
            if (shader != null)
            {
                _stressMaterial = new Material(shader);
            }
            else
            {
                // Fallback to Standard shader (less GPU stress but still works)
                Debug.LogWarning("GpuStressTest: Could not find stress shader. Assign it in the Inspector for builds.");
                _stressMaterial = new Material(Shader.Find("Standard"));
                _stressMaterial.color = new Color(1, 0, 0, 0.1f);
                _stressMaterial.SetFloat("_Mode", 3); // Transparent
                _stressMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _stressMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _stressMaterial.EnableKeyword("_ALPHABLEND_ON");
                _stressMaterial.renderQueue = 3000;
            }
        }
        
        private void CreateQuadMesh()
        {
            _quadMesh = new Mesh();
            _quadMesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3(0.5f, -0.5f, 0),
                new Vector3(0.5f, 0.5f, 0),
                new Vector3(-0.5f, 0.5f, 0)
            };
            _quadMesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };
            _quadMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            _quadMesh.RecalculateNormals();
        }
        
        private void SpawnStressObjects()
        {
            if (!enableObjectStress) return;
            
            _stressObjects = new GameObject[stressObjectCount];
            
            for (int i = 0; i < stressObjectCount; i++)
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                obj.name = $"StressObject_{i}";
                obj.transform.parent = transform;
                obj.transform.position = Random.insideUnitSphere * 10f;
                obj.transform.localScale = Vector3.one * Random.Range(0.2f, 1f);
                
                // Add complex material
                var renderer = obj.GetComponent<Renderer>();
                var mat = new Material(Shader.Find("Standard"));
                mat.color = Random.ColorHSV();
                mat.SetFloat("_Metallic", Random.value);
                mat.SetFloat("_Glossiness", Random.value);
                renderer.material = mat;
                
                // Remove collider for performance (we just want rendering stress)
                Destroy(obj.GetComponent<Collider>());
                
                _stressObjects[i] = obj;
            }
        }
        
        private void Update()
        {
            float intensity = stressLevel / 100f;
            
            // Animate stress objects
            if (enableObjectStress && _stressObjects != null)
            {
                for (int i = 0; i < _stressObjects.Length; i++)
                {
                    if (_stressObjects[i] != null)
                    {
                        _stressObjects[i].transform.Rotate(Vector3.one * Time.deltaTime * 100f * intensity);
                        _stressObjects[i].transform.position += new Vector3(
                            Mathf.Sin(Time.time + i) * 0.01f,
                            Mathf.Cos(Time.time + i) * 0.01f,
                            Mathf.Sin(Time.time * 0.5f + i) * 0.01f
                        ) * intensity;
                    }
                }
            }
        }
        
        private void OnRenderObject()
        {
            if (!enableOverdrawStress || _stressMaterial == null || _quadMesh == null) return;
            
            float intensity = stressLevel / 100f;
            int layers = Mathf.RoundToInt(overdrawLayers * intensity);
            int iterations = Mathf.RoundToInt(computeIterations * intensity * 10);
            
            if (_stressMaterial.HasProperty("_Iterations"))
                _stressMaterial.SetFloat("_Iterations", iterations);
            
            _stressMaterial.SetPass(0);
            
            // Draw many overlapping full-screen quads (massive overdraw)
            for (int i = 0; i < layers; i++)
            {
                float z = i * 0.001f;
                float scale = 20f;
                
                Matrix4x4 matrix = Matrix4x4.TRS(
                    Camera.main.transform.position + Camera.main.transform.forward * (1f + z),
                    Camera.main.transform.rotation,
                    Vector3.one * scale
                );
                
                Graphics.DrawMeshNow(_quadMesh, matrix);
            }
        }
        
        private void OnDestroy()
        {
            if (_stressObjects != null)
            {
                foreach (var obj in _stressObjects)
                {
                    if (obj != null) Destroy(obj);
                }
            }
        }
        
        #region Runtime GUI
        
        [Header("Runtime GUI")]
        public bool showGui = true;
        public KeyCode toggleGuiKey = KeyCode.F1;
        
        private bool _guiExpanded = true;
        private Rect _windowRect = new Rect(10, 10, 320, 400);
        private GUIStyle _windowStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _toggleStyle;
        private GUIStyle _sliderStyle;
        private GUIStyle _buttonStyle;
        private bool _stylesInitialized;
        
        private void InitStyles()
        {
            if (_stylesInitialized) return;
            
            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.fontSize = 14;
            
            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 13;
            _labelStyle.fontStyle = FontStyle.Bold;
            
            _toggleStyle = new GUIStyle(GUI.skin.toggle);
            _toggleStyle.fontSize = 13;
            
            _sliderStyle = new GUIStyle(GUI.skin.horizontalSlider);
            
            _buttonStyle = new GUIStyle(GUI.skin.button);
            _buttonStyle.fontSize = 13;
            
            _stylesInitialized = true;
        }
        
        private void OnGUI()
        {
            if (GUI.Button(new Rect(Screen.width - 210, Screen.height - 40, 200, 30), "Switch to MessagingDemo"))
            {
                SceneManager.LoadScene("MessagingDemo");
            }

#if ENABLE_INPUT_SYSTEM
            var currentEvent = Event.current;
            if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == toggleGuiKey)
#else
            if (Input.GetKeyDown(toggleGuiKey))
#endif
            {
                showGui = !showGui;
            }

            if (!showGui) return;
            
            InitStyles();
            
            _windowRect = GUILayout.Window(
                GetEntityId().GetHashCode(), 
                _windowRect,
                DrawGuiWindow, 
                "GPU Stress Test Controls", 
                _windowStyle,
                GUILayout.Width(320)
            );
        }
        
        private void DrawGuiWindow(int windowId)
        {
            GUILayout.Space(5);
            
            // FPS display
            float fps = 1f / Time.deltaTime;
            float smoothFps = 1f / Time.smoothDeltaTime;
            GUILayout.Label($"FPS: {fps:F1} (Smooth: {smoothFps:F1})", _labelStyle);
            GUILayout.Label($"Frame Time: {Time.deltaTime * 1000f:F2} ms", _labelStyle);
            
            GUILayout.Space(10);
            
            // Target Frame Rate
            GUILayout.Label("Target Frame Rate: " + (targetFrameRate == -1 ? "Unlimited" : targetFrameRate.ToString()), _labelStyle);
            float newFrameRate = GUILayout.HorizontalSlider(targetFrameRate, -1, 120);
            int newFrameRateInt = Mathf.RoundToInt(newFrameRate);
            if (newFrameRateInt != targetFrameRate)
            {
                targetFrameRate = newFrameRateInt;
                ApplyFrameRate();
            }
            
            GUILayout.Space(10);
            
            // Stress Level - main control
            GUILayout.Label($"Stress Level: {stressLevel}", _labelStyle);
            stressLevel = Mathf.RoundToInt(GUILayout.HorizontalSlider(stressLevel, 0, 100));
            
            GUILayout.Space(15);
            
            // Expandable detailed settings
            _guiExpanded = GUILayout.Toggle(_guiExpanded, "Show Detailed Settings", _toggleStyle);
            
            if (_guiExpanded)
            {
                GUILayout.Space(10);
                
                // Overdraw Stress
                GUILayout.BeginVertical(GUI.skin.box);
                enableOverdrawStress = GUILayout.Toggle(enableOverdrawStress, "Enable Overdraw Stress", _toggleStyle);
                if (enableOverdrawStress)
                {
                    GUILayout.Label($"Overdraw Layers: {overdrawLayers}", _labelStyle);
                    overdrawLayers = Mathf.RoundToInt(GUILayout.HorizontalSlider(overdrawLayers, 1, 200));
                    
                    GUILayout.Label($"Compute Iterations: {computeIterations}", _labelStyle);
                    computeIterations = Mathf.RoundToInt(GUILayout.HorizontalSlider(computeIterations, 1, 64));
                }
                GUILayout.EndVertical();
                
                GUILayout.Space(5);
                
                // Object Stress
                GUILayout.BeginVertical(GUI.skin.box);
                bool prevObjectStress = enableObjectStress;
                enableObjectStress = GUILayout.Toggle(enableObjectStress, "Enable Object Stress", _toggleStyle);
                
                if (enableObjectStress)
                {
                    GUILayout.Label($"Object Count: {stressObjectCount}", _labelStyle);
                    int newCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(stressObjectCount, 10, 5000));
                    
                    if (newCount != stressObjectCount)
                    {
                        stressObjectCount = newCount;
                        // Show rebuild button when count changes
                    }
                    
                    if (GUILayout.Button("Rebuild Objects", _buttonStyle))
                    {
                        RebuildStressObjects();
                    }
                }
                else if (prevObjectStress && !enableObjectStress)
                {
                    // Toggled off - destroy objects
                    DestroyStressObjects();
                }
                GUILayout.EndVertical();
            }
            
            GUILayout.Space(10);
            
            // Presets
            GUILayout.Label("Quick Presets:", _labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Off", _buttonStyle)) ApplyPreset(0);
            if (GUILayout.Button("Low", _buttonStyle)) ApplyPreset(1);
            if (GUILayout.Button("Med", _buttonStyle)) ApplyPreset(2);
            if (GUILayout.Button("High", _buttonStyle)) ApplyPreset(3);
            if (GUILayout.Button("Max", _buttonStyle)) ApplyPreset(4);
            GUILayout.EndHorizontal();
            
            GUILayout.Space(5);
            GUILayout.Label($"Press {toggleGuiKey} to toggle GUI", _labelStyle);
            
            GUI.DragWindow();
        }
        
        private void ApplyPreset(int preset)
        {
            switch (preset)
            {
                case 0: // Off
                    stressLevel = 0;
                    enableOverdrawStress = false;
                    enableObjectStress = false;
                    DestroyStressObjects();
                    break;
                case 1: // Low
                    stressLevel = 3;
                    enableOverdrawStress = true;
                    overdrawLayers = 20;
                    computeIterations = 8;
                    break;
                case 2: // Medium
                    stressLevel = 8;
                    enableOverdrawStress = true;
                    overdrawLayers = 50;
                    computeIterations = 24;
                    break;
                case 3: // High
                    stressLevel = 14;
                    enableOverdrawStress = true;
                    overdrawLayers = 100;
                    computeIterations = 48;
                    break;
                case 4: // Max
                    stressLevel = 20;
                    enableOverdrawStress = true;
                    overdrawLayers = 200;
                    computeIterations = 64;
                    break;
            }
        }
        
        private void RebuildStressObjects()
        {
            DestroyStressObjects();
            SpawnStressObjects();
        }
        
        private void DestroyStressObjects()
        {
            if (_stressObjects != null)
            {
                foreach (var obj in _stressObjects)
                {
                    if (obj != null) Destroy(obj);
                }
                _stressObjects = null;
            }
        }
        
        #endregion
    }
}
