using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EpicLegends.Editor.Tools
{
    /// <summary>
    /// Convertisseur de matériaux vers un style anime/cel-shading
    /// Génère des matériaux toon-shaded à partir de matériaux standards
    /// </summary>
    public class AnimeMaterialConverter : EditorWindow
    {
        [MenuItem("EpicLegends/Tools/Anime Material Converter")]
        public static void ShowWindow()
        {
            var window = GetWindow<AnimeMaterialConverter>("Anime Material");
            window.minSize = new Vector2(600, 700);
        }

        // Enums
        private enum ShadingStyle
        {
            GenshinImpact,
            CelShading2Tone,
            CelShading3Tone,
            Outline,
            Watercolor,
            Custom
        }

        private enum OutlineMode { None, ScreenSpace, ObjectSpace, PostProcess }

        // State
        private Vector2 _scrollPos;
        private Material _sourceMaterial;
        private List<Material> _batchMaterials = new List<Material>();
        private bool _isBatchMode;

        // Conversion settings
        private ShadingStyle _style = ShadingStyle.GenshinImpact;
        private OutlineMode _outlineMode = OutlineMode.ObjectSpace;

        // Color settings
        private Color _shadowColor = new Color(0.6f, 0.5f, 0.6f, 1f);
        private float _shadowThreshold = 0.5f;
        private float _shadowSmoothness = 0.02f;
        private Color _highlightColor = Color.white;
        private float _highlightThreshold = 0.9f;
        private float _highlightSmoothness = 0.05f;

        // Outline settings
        private Color _outlineColor = new Color(0.2f, 0.15f, 0.1f, 1f);
        private float _outlineWidth = 0.003f;
        private bool _outlineScaleWithDistance = true;

        // Rim light
        private bool _enableRimLight = true;
        private Color _rimColor = new Color(1f, 0.9f, 0.8f, 1f);
        private float _rimPower = 3f;
        private float _rimIntensity = 0.5f;

        // Specular
        private bool _enableAnimeSpecular = true;
        private float _specularSize = 0.1f;
        private float _specularSmoothness = 0.05f;

        // Face shading (Genshin style)
        private bool _enableFaceShading;
        private Texture2D _faceShadowMap;
        private float _faceOffset = 0f;

        // Emission
        private bool _preserveEmission = true;
        private float _emissionBoost = 1f;

        // Preview
        private PreviewRenderUtility _preview;
        private Mesh _previewMesh;
        private Material _previewMaterial;
        private Vector2 _previewRotation = new Vector2(25f, -45f);
        private float _previewZoom = 2f;

        // Output
        private string _outputFolder = "Assets/Materials/Anime";
        private string _outputSuffix = "_Toon";

        private void OnEnable()
        {
            InitializePreview();
        }

        private void OnDisable()
        {
            CleanupPreview();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎨 Anime Material Converter", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Convert standard materials to anime/cel-shaded style", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.BeginHorizontal();

            // Left panel - Settings
            EditorGUILayout.BeginVertical(GUILayout.Width(350));
            DrawSettings();
            EditorGUILayout.EndVertical();

            // Right panel - Preview
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawPreview();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Actions
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSettings()
        {
            // Source material
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _isBatchMode = EditorGUILayout.Toggle("Batch Mode", _isBatchMode);

            if (_isBatchMode)
            {
                EditorGUILayout.LabelField($"{_batchMaterials.Count} materials selected");

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add Selected"))
                {
                    foreach (var obj in Selection.objects)
                    {
                        if (obj is Material mat && !_batchMaterials.Contains(mat))
                            _batchMaterials.Add(mat);
                    }
                }
                if (GUILayout.Button("Clear"))
                    _batchMaterials.Clear();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                var newSource = (Material)EditorGUILayout.ObjectField("Material", _sourceMaterial, typeof(Material), false);
                if (newSource != _sourceMaterial)
                {
                    _sourceMaterial = newSource;
                    UpdatePreviewMaterial();
                }
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Style presets
            EditorGUILayout.LabelField("Shading Style", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var newStyle = (ShadingStyle)EditorGUILayout.EnumPopup("Style", _style);
            if (newStyle != _style)
            {
                _style = newStyle;
                ApplyStylePreset();
                UpdatePreviewMaterial();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Shadow settings
            EditorGUILayout.LabelField("Shadow", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _shadowColor = EditorGUILayout.ColorField("Shadow Color", _shadowColor);
            _shadowThreshold = EditorGUILayout.Slider("Threshold", _shadowThreshold, 0f, 1f);
            _shadowSmoothness = EditorGUILayout.Slider("Smoothness", _shadowSmoothness, 0f, 0.2f);

            EditorGUILayout.Space(5);

            _highlightColor = EditorGUILayout.ColorField("Highlight Color", _highlightColor);
            _highlightThreshold = EditorGUILayout.Slider("Highlight Threshold", _highlightThreshold, 0.5f, 1f);
            _highlightSmoothness = EditorGUILayout.Slider("Highlight Smoothness", _highlightSmoothness, 0f, 0.2f);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Outline settings
            EditorGUILayout.LabelField("Outline", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _outlineMode = (OutlineMode)EditorGUILayout.EnumPopup("Mode", _outlineMode);

            if (_outlineMode != OutlineMode.None)
            {
                _outlineColor = EditorGUILayout.ColorField("Color", _outlineColor);
                _outlineWidth = EditorGUILayout.Slider("Width", _outlineWidth, 0.0001f, 0.01f);
                _outlineScaleWithDistance = EditorGUILayout.Toggle("Scale with Distance", _outlineScaleWithDistance);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Rim light
            EditorGUILayout.LabelField("Rim Light", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _enableRimLight = EditorGUILayout.Toggle("Enable", _enableRimLight);

            if (_enableRimLight)
            {
                _rimColor = EditorGUILayout.ColorField("Color", _rimColor);
                _rimPower = EditorGUILayout.Slider("Power", _rimPower, 1f, 10f);
                _rimIntensity = EditorGUILayout.Slider("Intensity", _rimIntensity, 0f, 2f);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Anime specular
            EditorGUILayout.LabelField("Anime Specular", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _enableAnimeSpecular = EditorGUILayout.Toggle("Enable", _enableAnimeSpecular);

            if (_enableAnimeSpecular)
            {
                _specularSize = EditorGUILayout.Slider("Size", _specularSize, 0.01f, 0.5f);
                _specularSmoothness = EditorGUILayout.Slider("Smoothness", _specularSmoothness, 0f, 0.2f);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Face shading (advanced)
            EditorGUILayout.LabelField("Face Shading (Genshin Style)", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _enableFaceShading = EditorGUILayout.Toggle("Enable", _enableFaceShading);

            if (_enableFaceShading)
            {
                _faceShadowMap = (Texture2D)EditorGUILayout.ObjectField("Shadow Map", _faceShadowMap, typeof(Texture2D), false);
                _faceOffset = EditorGUILayout.Slider("Face Offset", _faceOffset, -1f, 1f);

                EditorGUILayout.HelpBox("Face shadow maps control shadow placement based on face direction", MessageType.Info);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Emission
            EditorGUILayout.LabelField("Emission", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _preserveEmission = EditorGUILayout.Toggle("Preserve Emission", _preserveEmission);
            _emissionBoost = EditorGUILayout.Slider("Emission Boost", _emissionBoost, 0f, 3f);

            EditorGUILayout.EndVertical();
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            // Preview controls
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Sphere")) SetPreviewMesh(PrimitiveType.Sphere);
            if (GUILayout.Button("Cube")) SetPreviewMesh(PrimitiveType.Cube);
            if (GUILayout.Button("Cylinder")) SetPreviewMesh(PrimitiveType.Cylinder);
            if (GUILayout.Button("Custom")) LoadCustomPreviewMesh();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Preview render
            Rect previewRect = GUILayoutUtility.GetRect(300, 300, GUILayout.ExpandWidth(true));

            if (_preview != null && _previewMaterial != null)
            {
                // Handle input
                Event e = Event.current;
                if (previewRect.Contains(e.mousePosition))
                {
                    if (e.type == EventType.MouseDrag)
                    {
                        _previewRotation.x += e.delta.y * 0.5f;
                        _previewRotation.y += e.delta.x * 0.5f;
                        e.Use();
                        Repaint();
                    }
                    else if (e.type == EventType.ScrollWheel)
                    {
                        _previewZoom += e.delta.y * 0.1f;
                        _previewZoom = Mathf.Clamp(_previewZoom, 1f, 5f);
                        e.Use();
                        Repaint();
                    }
                }

                // Render preview
                _preview.BeginPreview(previewRect, GUIStyle.none);

                _preview.camera.transform.position = new Vector3(0, 0, -_previewZoom);
                _preview.camera.transform.LookAt(Vector3.zero);

                _preview.lights[0].transform.rotation = Quaternion.Euler(50, 30, 0);

                if (_previewMesh != null)
                {
                    var rotation = Quaternion.Euler(_previewRotation.x, _previewRotation.y, 0);
                    _preview.DrawMesh(_previewMesh, Vector3.zero, rotation, _previewMaterial, 0);
                }

                _preview.camera.Render();
                var texture = _preview.EndPreview();
                GUI.DrawTexture(previewRect, texture);
            }
            else
            {
                EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f));
                GUI.Label(previewRect, "Drag material here or select one above", EditorStyles.centeredGreyMiniLabel);
            }

            // Drag & drop
            if (previewRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is Material mat)
                        {
                            _sourceMaterial = mat;
                            UpdatePreviewMaterial();
                            break;
                        }
                    }
                    Event.current.Use();
                }
            }

            EditorGUILayout.Space(10);

            // Real-time update button
            if (GUILayout.Button("🔄 Update Preview"))
            {
                UpdatePreviewMaterial();
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                        _outputFolder = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }
            EditorGUILayout.EndHorizontal();

            _outputSuffix = EditorGUILayout.TextField("Suffix", _outputSuffix);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Convert button
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = _isBatchMode ? _batchMaterials.Count > 0 : _sourceMaterial != null;

            if (GUILayout.Button("🎨 Convert Material(s)", GUILayout.Height(35)))
            {
                if (_isBatchMode)
                    ConvertBatch();
                else
                    ConvertSingle(_sourceMaterial);
            }

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Generate shader button
            if (GUILayout.Button("📝 Generate Custom Toon Shader", GUILayout.Height(30)))
            {
                GenerateCustomShader();
            }
        }

        private void InitializePreview()
        {
            _preview = new PreviewRenderUtility();
            _preview.camera.farClipPlane = 100f;
            _preview.camera.nearClipPlane = 0.1f;
            _preview.camera.fieldOfView = 30f;
            _preview.camera.transform.position = new Vector3(0, 0, -_previewZoom);
            _preview.camera.backgroundColor = new Color(0.2f, 0.2f, 0.25f);

            SetPreviewMesh(PrimitiveType.Sphere);
        }

        private void CleanupPreview()
        {
            _preview?.Cleanup();
            _preview = null;
        }

        private void SetPreviewMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            _previewMesh = go.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(go);
        }

        private void LoadCustomPreviewMesh()
        {
            string path = EditorUtility.OpenFilePanel("Select Mesh", "Assets", "fbx,obj");
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath))
                {
                    path = "Assets" + path.Substring(Application.dataPath.Length);
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (mesh != null)
                        _previewMesh = mesh;
                }
            }
        }

        private void UpdatePreviewMaterial()
        {
            if (_sourceMaterial == null)
            {
                _previewMaterial = null;
                return;
            }

            // Create a preview material with toon settings
            // For now, we'll modify a copy of the source
            if (_previewMaterial == null)
                _previewMaterial = new Material(Shader.Find("Standard"));

            // Copy base properties
            if (_sourceMaterial.HasProperty("_Color"))
                _previewMaterial.color = _sourceMaterial.color;

            if (_sourceMaterial.HasProperty("_MainTex"))
                _previewMaterial.mainTexture = _sourceMaterial.mainTexture;

            // Apply toon-like modifications (simplified for preview)
            // In real usage, this would use a proper toon shader

            Repaint();
        }

        private void ApplyStylePreset()
        {
            switch (_style)
            {
                case ShadingStyle.GenshinImpact:
                    _shadowColor = new Color(0.65f, 0.55f, 0.65f, 1f);
                    _shadowThreshold = 0.45f;
                    _shadowSmoothness = 0.02f;
                    _outlineMode = OutlineMode.ObjectSpace;
                    _outlineColor = new Color(0.15f, 0.1f, 0.1f, 1f);
                    _outlineWidth = 0.002f;
                    _enableRimLight = true;
                    _rimColor = new Color(1f, 0.95f, 0.9f, 1f);
                    _rimPower = 4f;
                    _rimIntensity = 0.4f;
                    _enableAnimeSpecular = true;
                    _specularSize = 0.08f;
                    break;

                case ShadingStyle.CelShading2Tone:
                    _shadowColor = new Color(0.5f, 0.5f, 0.5f, 1f);
                    _shadowThreshold = 0.5f;
                    _shadowSmoothness = 0.01f;
                    _outlineMode = OutlineMode.ObjectSpace;
                    _outlineColor = Color.black;
                    _outlineWidth = 0.003f;
                    _enableRimLight = false;
                    _enableAnimeSpecular = false;
                    break;

                case ShadingStyle.CelShading3Tone:
                    _shadowColor = new Color(0.6f, 0.6f, 0.6f, 1f);
                    _shadowThreshold = 0.5f;
                    _shadowSmoothness = 0.02f;
                    _highlightThreshold = 0.85f;
                    _outlineMode = OutlineMode.ObjectSpace;
                    _outlineColor = new Color(0.2f, 0.2f, 0.2f, 1f);
                    _outlineWidth = 0.002f;
                    _enableRimLight = true;
                    _rimIntensity = 0.3f;
                    _enableAnimeSpecular = true;
                    break;

                case ShadingStyle.Outline:
                    _shadowThreshold = 0.3f;
                    _shadowSmoothness = 0.1f;
                    _outlineMode = OutlineMode.ObjectSpace;
                    _outlineColor = Color.black;
                    _outlineWidth = 0.005f;
                    _enableRimLight = false;
                    _enableAnimeSpecular = false;
                    break;

                case ShadingStyle.Watercolor:
                    _shadowColor = new Color(0.7f, 0.6f, 0.7f, 1f);
                    _shadowThreshold = 0.4f;
                    _shadowSmoothness = 0.15f;
                    _outlineMode = OutlineMode.None;
                    _enableRimLight = true;
                    _rimColor = Color.white;
                    _rimPower = 2f;
                    _rimIntensity = 0.2f;
                    break;
            }
        }

        private void ConvertSingle(Material source)
        {
            if (source == null) return;

            // Create output folder if needed
            if (!AssetDatabase.IsValidFolder(_outputFolder))
            {
                string[] parts = _outputFolder.Split('/');
                string currentPath = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string folderPath = currentPath + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(folderPath))
                        AssetDatabase.CreateFolder(currentPath, parts[i]);
                    currentPath = folderPath;
                }
            }

            // Create new material
            string newName = source.name + _outputSuffix;
            string newPath = $"{_outputFolder}/{newName}.mat";

            // Try to find or create toon shader
            Shader toonShader = FindOrCreateToonShader();

            Material newMat = new Material(toonShader);
            newMat.name = newName;

            // Copy base properties
            CopyMaterialProperties(source, newMat);

            // Apply toon settings
            ApplyToonSettings(newMat);

            // Save
            AssetDatabase.CreateAsset(newMat, newPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Created anime material: {newPath}");
            Selection.activeObject = newMat;
            EditorGUIUtility.PingObject(newMat);
        }

        private void ConvertBatch()
        {
            foreach (var mat in _batchMaterials)
            {
                ConvertSingle(mat);
            }

            EditorUtility.DisplayDialog("Complete", $"Converted {_batchMaterials.Count} materials", "OK");
        }

        private Shader FindOrCreateToonShader()
        {
            // Try to find existing toon shader
            Shader shader = Shader.Find("EpicLegends/Toon");
            if (shader != null) return shader;

            shader = Shader.Find("Toon/Lit");
            if (shader != null) return shader;

            shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null) return shader;

            // Fallback to standard
            return Shader.Find("Standard");
        }

        private void CopyMaterialProperties(Material source, Material dest)
        {
            // Copy common properties
            if (source.HasProperty("_Color") && dest.HasProperty("_Color"))
                dest.SetColor("_Color", source.GetColor("_Color"));

            if (source.HasProperty("_MainTex") && dest.HasProperty("_MainTex"))
                dest.SetTexture("_MainTex", source.GetTexture("_MainTex"));

            if (source.HasProperty("_BumpMap") && dest.HasProperty("_BumpMap"))
                dest.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));

            if (_preserveEmission)
            {
                if (source.HasProperty("_EmissionColor") && dest.HasProperty("_EmissionColor"))
                {
                    Color emission = source.GetColor("_EmissionColor") * _emissionBoost;
                    dest.SetColor("_EmissionColor", emission);
                    dest.EnableKeyword("_EMISSION");
                }

                if (source.HasProperty("_EmissionMap") && dest.HasProperty("_EmissionMap"))
                    dest.SetTexture("_EmissionMap", source.GetTexture("_EmissionMap"));
            }
        }

        private void ApplyToonSettings(Material mat)
        {
            // Apply toon-specific properties based on shader support
            // These property names depend on the actual toon shader being used

            if (mat.HasProperty("_ShadowColor"))
                mat.SetColor("_ShadowColor", _shadowColor);

            if (mat.HasProperty("_ShadowThreshold"))
                mat.SetFloat("_ShadowThreshold", _shadowThreshold);

            if (mat.HasProperty("_ShadowSmoothness"))
                mat.SetFloat("_ShadowSmoothness", _shadowSmoothness);

            if (mat.HasProperty("_OutlineColor"))
                mat.SetColor("_OutlineColor", _outlineColor);

            if (mat.HasProperty("_OutlineWidth"))
                mat.SetFloat("_OutlineWidth", _outlineWidth);

            if (mat.HasProperty("_RimColor"))
                mat.SetColor("_RimColor", _rimColor);

            if (mat.HasProperty("_RimPower"))
                mat.SetFloat("_RimPower", _rimPower);

            if (mat.HasProperty("_RimIntensity"))
                mat.SetFloat("_RimIntensity", _rimIntensity);

            // Store custom data in material keywords or extra properties
            mat.SetFloat("_ToonStyle", (float)_style);
            mat.SetFloat("_OutlineMode", (float)_outlineMode);
        }

        private void GenerateCustomShader()
        {
            string path = EditorUtility.SaveFilePanel("Save Toon Shader", "Assets/Shaders", "ToonShader", "shader");
            if (string.IsNullOrEmpty(path)) return;

            string shaderCode = GenerateToonShaderCode();
            System.IO.File.WriteAllText(path, shaderCode);

            if (path.StartsWith(Application.dataPath))
            {
                path = "Assets" + path.Substring(Application.dataPath.Length);
                AssetDatabase.ImportAsset(path);
            }

            Debug.Log($"Generated toon shader: {path}");
        }

        private string GenerateToonShaderCode()
        {
            return @"Shader ""EpicLegends/Toon""
{
    Properties
    {
        _MainTex (""Main Texture"", 2D) = ""white"" {}
        _Color (""Color"", Color) = (1,1,1,1)

        [Header(Shadow)]
        _ShadowColor (""Shadow Color"", Color) = (0.6, 0.5, 0.6, 1)
        _ShadowThreshold (""Shadow Threshold"", Range(0, 1)) = 0.5
        _ShadowSmoothness (""Shadow Smoothness"", Range(0, 0.2)) = 0.02

        [Header(Highlight)]
        _HighlightColor (""Highlight Color"", Color) = (1,1,1,1)
        _HighlightThreshold (""Highlight Threshold"", Range(0.5, 1)) = 0.9
        _HighlightSmoothness (""Highlight Smoothness"", Range(0, 0.2)) = 0.05

        [Header(Outline)]
        _OutlineColor (""Outline Color"", Color) = (0.2, 0.15, 0.1, 1)
        _OutlineWidth (""Outline Width"", Range(0, 0.01)) = 0.002

        [Header(Rim Light)]
        _RimColor (""Rim Color"", Color) = (1, 0.9, 0.8, 1)
        _RimPower (""Rim Power"", Range(1, 10)) = 3
        _RimIntensity (""Rim Intensity"", Range(0, 2)) = 0.5

        [Header(Specular)]
        _SpecularSize (""Specular Size"", Range(0.01, 0.5)) = 0.1
        _SpecularSmoothness (""Specular Smoothness"", Range(0, 0.2)) = 0.05

        [Header(Emission)]
        _EmissionColor (""Emission Color"", Color) = (0,0,0,1)
        _EmissionMap (""Emission Map"", 2D) = ""white"" {}
    }

    SubShader
    {
        Tags { ""RenderType""=""Opaque"" ""Queue""=""Geometry"" }

        // Main pass
        Pass
        {
            Name ""FORWARD""
            Tags { ""LightMode""=""ForwardBase"" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog

            #include ""UnityCG.cginc""
            #include ""Lighting.cginc""
            #include ""AutoLight.cginc""

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
                SHADOW_COORDS(4)
                UNITY_FOG_COORDS(5)
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _ShadowColor;
            float _ShadowThreshold;
            float _ShadowSmoothness;
            float4 _HighlightColor;
            float _HighlightThreshold;
            float _HighlightSmoothness;
            float4 _RimColor;
            float _RimPower;
            float _RimIntensity;
            float _SpecularSize;
            float _SpecularSmoothness;
            float4 _EmissionColor;
            sampler2D _EmissionMap;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Base color
                float4 texColor = tex2D(_MainTex, i.uv) * _Color;

                // Lighting
                float3 normal = normalize(i.worldNormal);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float NdotL = dot(normal, lightDir);

                // Shadow attenuation
                float shadow = SHADOW_ATTENUATION(i);

                // Toon shading
                float shadowStep = smoothstep(_ShadowThreshold - _ShadowSmoothness,
                                              _ShadowThreshold + _ShadowSmoothness,
                                              NdotL * shadow);

                float3 shadowColor = lerp(_ShadowColor.rgb, float3(1,1,1), shadowStep);

                // Highlight
                float highlightStep = smoothstep(_HighlightThreshold - _HighlightSmoothness,
                                                 _HighlightThreshold + _HighlightSmoothness,
                                                 NdotL);
                float3 highlight = _HighlightColor.rgb * highlightStep;

                // Rim light
                float rim = 1.0 - saturate(dot(i.viewDir, normal));
                rim = pow(rim, _RimPower) * _RimIntensity;
                float3 rimLight = _RimColor.rgb * rim * shadowStep;

                // Specular
                float3 halfDir = normalize(lightDir + i.viewDir);
                float NdotH = dot(normal, halfDir);
                float specular = smoothstep(1 - _SpecularSize - _SpecularSmoothness,
                                            1 - _SpecularSize + _SpecularSmoothness,
                                            NdotH);

                // Combine
                float3 finalColor = texColor.rgb * shadowColor * _LightColor0.rgb;
                finalColor += highlight + rimLight + specular * _LightColor0.rgb;

                // Emission
                float3 emission = tex2D(_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
                finalColor += emission;

                // Fog
                UNITY_APPLY_FOG(i.fogCoord, finalColor);

                return float4(finalColor, texColor.a);
            }
            ENDCG
        }

        // Outline pass
        Pass
        {
            Name ""OUTLINE""
            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include ""UnityCG.cginc""

            float4 _OutlineColor;
            float _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 normal = normalize(v.normal);
                float3 outlineOffset = normal * _OutlineWidth;
                float4 pos = v.vertex + float4(outlineOffset, 0);
                o.pos = UnityObjectToClipPos(pos);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }

        // Shadow caster
        UsePass ""Legacy Shaders/VertexLit/SHADOWCASTER""
    }

    FallBack ""Diffuse""
}";
        }
    }
}
