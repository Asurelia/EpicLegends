using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Gestionnaire de presets d'eclairage avec preview.
/// Menu: EpicLegends > Tools > Lighting Preset Manager
/// </summary>
public class LightingPresetManager : EditorWindow
{
    #region State

    private List<LightingPreset> _presets = new List<LightingPreset>();
    private int _selectedPresetIndex = -1;
    private Vector2 _scrollPosition;
    private float _previewLerp = 0f;
    private LightingPreset _previewFromPreset;
    private bool _isLivePreview = false;

    #endregion

    [MenuItem("EpicLegends/Tools/Lighting Preset Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<LightingPresetManager>("Lighting Presets");
        window.minSize = new Vector2(450, 500);
    }

    private void OnEnable()
    {
        if (_presets.Count == 0)
        {
            CreateDefaultPresets();
        }
    }

    private void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.BeginHorizontal();

        // Left: Preset list
        EditorGUILayout.BeginVertical(GUILayout.Width(200));
        DrawPresetList();
        EditorGUILayout.EndVertical();

        // Right: Properties
        EditorGUILayout.BeginVertical();
        DrawPropertiesPanel();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    #region Drawing

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Save All", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            SavePresets();
        }
        if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            LoadPresets();
        }

        GUILayout.FlexibleSpace();

        _isLivePreview = GUILayout.Toggle(_isLivePreview, "Live Preview", EditorStyles.toolbarButton, GUILayout.Width(90));

        if (GUILayout.Button("Capture Current", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            CaptureCurrentLighting();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawPresetList()
    {
        EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        for (int i = 0; i < _presets.Count; i++)
        {
            var preset = _presets[i];
            bool isSelected = i == _selectedPresetIndex;

            EditorGUILayout.BeginHorizontal();

            // Color preview
            GUI.backgroundColor = preset.ambientColor;
            GUILayout.Box("", GUILayout.Width(20), GUILayout.Height(20));
            GUI.backgroundColor = Color.white;

            // Selection button
            GUIStyle style = isSelected ? EditorStyles.boldLabel : EditorStyles.label;
            if (GUILayout.Button(preset.name, style))
            {
                _selectedPresetIndex = i;
                if (_isLivePreview)
                {
                    ApplyPreset(preset);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Add"))
        {
            AddPreset();
        }
        if (_selectedPresetIndex >= 0 && GUILayout.Button("- Remove"))
        {
            RemovePreset(_selectedPresetIndex);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawPropertiesPanel()
    {
        if (_selectedPresetIndex < 0 || _selectedPresetIndex >= _presets.Count)
        {
            EditorGUILayout.HelpBox("Select a preset to edit", MessageType.Info);
            return;
        }

        var preset = _presets[_selectedPresetIndex];

        EditorGUILayout.LabelField("Preset Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        preset.name = EditorGUILayout.TextField("Name", preset.name);

        EditorGUILayout.Space(10);

        // Ambient
        EditorGUILayout.LabelField("Ambient Lighting", EditorStyles.miniBoldLabel);
        preset.ambientMode = (UnityEngine.Rendering.AmbientMode)EditorGUILayout.EnumPopup("Mode", preset.ambientMode);

        if (preset.ambientMode == UnityEngine.Rendering.AmbientMode.Flat)
        {
            preset.ambientColor = EditorGUILayout.ColorField("Color", preset.ambientColor);
        }
        else if (preset.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight)
        {
            preset.ambientSkyColor = EditorGUILayout.ColorField("Sky Color", preset.ambientSkyColor);
            preset.ambientEquatorColor = EditorGUILayout.ColorField("Equator Color", preset.ambientEquatorColor);
            preset.ambientGroundColor = EditorGUILayout.ColorField("Ground Color", preset.ambientGroundColor);
        }

        preset.ambientIntensity = EditorGUILayout.Slider("Intensity", preset.ambientIntensity, 0f, 2f);

        EditorGUILayout.Space(10);

        // Fog
        EditorGUILayout.LabelField("Fog", EditorStyles.miniBoldLabel);
        preset.fogEnabled = EditorGUILayout.Toggle("Enabled", preset.fogEnabled);

        if (preset.fogEnabled)
        {
            preset.fogColor = EditorGUILayout.ColorField("Color", preset.fogColor);
            preset.fogMode = (FogMode)EditorGUILayout.EnumPopup("Mode", preset.fogMode);

            if (preset.fogMode == FogMode.Linear)
            {
                preset.fogStartDistance = EditorGUILayout.FloatField("Start", preset.fogStartDistance);
                preset.fogEndDistance = EditorGUILayout.FloatField("End", preset.fogEndDistance);
            }
            else
            {
                preset.fogDensity = EditorGUILayout.Slider("Density", preset.fogDensity, 0f, 0.1f);
            }
        }

        EditorGUILayout.Space(10);

        // Directional Light
        EditorGUILayout.LabelField("Sun/Directional Light", EditorStyles.miniBoldLabel);
        preset.sunColor = EditorGUILayout.ColorField("Color", preset.sunColor);
        preset.sunIntensity = EditorGUILayout.Slider("Intensity", preset.sunIntensity, 0f, 5f);
        preset.sunRotation = EditorGUILayout.Vector3Field("Rotation", preset.sunRotation);

        EditorGUILayout.Space(10);

        // Skybox
        EditorGUILayout.LabelField("Skybox", EditorStyles.miniBoldLabel);
        preset.skyboxMaterial = (Material)EditorGUILayout.ObjectField("Material", preset.skyboxMaterial, typeof(Material), false);
        preset.skyboxExposure = EditorGUILayout.Slider("Exposure", preset.skyboxExposure, 0f, 8f);

        if (EditorGUI.EndChangeCheck() && _isLivePreview)
        {
            ApplyPreset(preset);
        }

        EditorGUILayout.Space(20);

        // Apply button
        GUI.backgroundColor = new Color(0.5f, 0.8f, 0.5f);
        if (GUILayout.Button("Apply Preset", GUILayout.Height(30)))
        {
            ApplyPreset(preset);
        }
        GUI.backgroundColor = Color.white;

        // Export as ScriptableObject
        if (GUILayout.Button("Export as ScriptableObject"))
        {
            ExportPreset(preset);
        }
    }

    #endregion

    #region Logic

    private void CreateDefaultPresets()
    {
        // Day - Sunny
        _presets.Add(new LightingPreset
        {
            name = "Day - Sunny",
            ambientMode = UnityEngine.Rendering.AmbientMode.Trilight,
            ambientSkyColor = new Color(0.5f, 0.7f, 1f),
            ambientEquatorColor = new Color(0.6f, 0.6f, 0.5f),
            ambientGroundColor = new Color(0.3f, 0.3f, 0.2f),
            ambientIntensity = 1f,
            fogEnabled = true,
            fogColor = new Color(0.7f, 0.8f, 0.9f),
            fogMode = FogMode.Linear,
            fogStartDistance = 50f,
            fogEndDistance = 300f,
            sunColor = new Color(1f, 0.95f, 0.8f),
            sunIntensity = 1.5f,
            sunRotation = new Vector3(50f, -30f, 0f),
            skyboxExposure = 1.3f
        });

        // Day - Cloudy
        _presets.Add(new LightingPreset
        {
            name = "Day - Cloudy",
            ambientMode = UnityEngine.Rendering.AmbientMode.Trilight,
            ambientSkyColor = new Color(0.5f, 0.5f, 0.55f),
            ambientEquatorColor = new Color(0.5f, 0.5f, 0.5f),
            ambientGroundColor = new Color(0.3f, 0.3f, 0.3f),
            ambientIntensity = 0.8f,
            fogEnabled = true,
            fogColor = new Color(0.6f, 0.6f, 0.65f),
            fogMode = FogMode.Linear,
            fogStartDistance = 30f,
            fogEndDistance = 200f,
            sunColor = new Color(0.8f, 0.8f, 0.8f),
            sunIntensity = 0.8f,
            sunRotation = new Vector3(45f, -30f, 0f),
            skyboxExposure = 0.8f
        });

        // Sunset
        _presets.Add(new LightingPreset
        {
            name = "Sunset",
            ambientMode = UnityEngine.Rendering.AmbientMode.Trilight,
            ambientSkyColor = new Color(0.9f, 0.5f, 0.3f),
            ambientEquatorColor = new Color(0.7f, 0.4f, 0.3f),
            ambientGroundColor = new Color(0.2f, 0.15f, 0.1f),
            ambientIntensity = 0.9f,
            fogEnabled = true,
            fogColor = new Color(0.9f, 0.6f, 0.4f),
            fogMode = FogMode.Linear,
            fogStartDistance = 20f,
            fogEndDistance = 150f,
            sunColor = new Color(1f, 0.5f, 0.2f),
            sunIntensity = 1.2f,
            sunRotation = new Vector3(10f, -60f, 0f),
            skyboxExposure = 1.5f
        });

        // Night
        _presets.Add(new LightingPreset
        {
            name = "Night",
            ambientMode = UnityEngine.Rendering.AmbientMode.Trilight,
            ambientSkyColor = new Color(0.05f, 0.05f, 0.15f),
            ambientEquatorColor = new Color(0.03f, 0.03f, 0.1f),
            ambientGroundColor = new Color(0.02f, 0.02f, 0.05f),
            ambientIntensity = 0.3f,
            fogEnabled = true,
            fogColor = new Color(0.05f, 0.05f, 0.1f),
            fogMode = FogMode.Linear,
            fogStartDistance = 10f,
            fogEndDistance = 80f,
            sunColor = new Color(0.3f, 0.4f, 0.6f),
            sunIntensity = 0.2f,
            sunRotation = new Vector3(-30f, 45f, 0f),
            skyboxExposure = 0.3f
        });

        // Dungeon
        _presets.Add(new LightingPreset
        {
            name = "Dungeon",
            ambientMode = UnityEngine.Rendering.AmbientMode.Flat,
            ambientColor = new Color(0.1f, 0.08f, 0.05f),
            ambientIntensity = 0.5f,
            fogEnabled = true,
            fogColor = new Color(0.05f, 0.03f, 0.02f),
            fogMode = FogMode.Exponential,
            fogDensity = 0.03f,
            sunColor = Color.black,
            sunIntensity = 0f,
            sunRotation = Vector3.zero,
            skyboxExposure = 0f
        });

        // Boss Arena
        _presets.Add(new LightingPreset
        {
            name = "Boss Arena",
            ambientMode = UnityEngine.Rendering.AmbientMode.Flat,
            ambientColor = new Color(0.15f, 0.05f, 0.05f),
            ambientIntensity = 0.6f,
            fogEnabled = true,
            fogColor = new Color(0.2f, 0.05f, 0.05f),
            fogMode = FogMode.Exponential,
            fogDensity = 0.02f,
            sunColor = new Color(1f, 0.3f, 0.2f),
            sunIntensity = 0.5f,
            sunRotation = new Vector3(60f, 0f, 0f),
            skyboxExposure = 0.2f
        });
    }

    private void AddPreset()
    {
        _presets.Add(new LightingPreset
        {
            name = $"Preset {_presets.Count + 1}",
            ambientMode = UnityEngine.Rendering.AmbientMode.Flat,
            ambientColor = Color.gray,
            ambientIntensity = 1f
        });
        _selectedPresetIndex = _presets.Count - 1;
    }

    private void RemovePreset(int index)
    {
        if (index >= 0 && index < _presets.Count)
        {
            _presets.RemoveAt(index);
            _selectedPresetIndex = Mathf.Clamp(_selectedPresetIndex, -1, _presets.Count - 1);
        }
    }

    private void ApplyPreset(LightingPreset preset)
    {
        // Note: RenderSettings changes are applied directly (no Undo support for RenderSettings)

        // Ambient
        RenderSettings.ambientMode = preset.ambientMode;

        if (preset.ambientMode == UnityEngine.Rendering.AmbientMode.Flat)
        {
            RenderSettings.ambientLight = preset.ambientColor;
        }
        else if (preset.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight)
        {
            RenderSettings.ambientSkyColor = preset.ambientSkyColor;
            RenderSettings.ambientEquatorColor = preset.ambientEquatorColor;
            RenderSettings.ambientGroundColor = preset.ambientGroundColor;
        }

        RenderSettings.ambientIntensity = preset.ambientIntensity;

        // Fog
        RenderSettings.fog = preset.fogEnabled;
        if (preset.fogEnabled)
        {
            RenderSettings.fogColor = preset.fogColor;
            RenderSettings.fogMode = preset.fogMode;

            if (preset.fogMode == FogMode.Linear)
            {
                RenderSettings.fogStartDistance = preset.fogStartDistance;
                RenderSettings.fogEndDistance = preset.fogEndDistance;
            }
            else
            {
                RenderSettings.fogDensity = preset.fogDensity;
            }
        }

        // Skybox
        if (preset.skyboxMaterial != null)
        {
            RenderSettings.skybox = preset.skyboxMaterial;
        }

        // Find and update directional light
        var sun = FindDirectionalLight();
        if (sun != null)
        {
            Undo.RecordObject(sun, "Update Sun Light");
            sun.color = preset.sunColor;
            sun.intensity = preset.sunIntensity;
            sun.transform.rotation = Quaternion.Euler(preset.sunRotation);
        }

        SceneView.RepaintAll();
        Debug.Log($"[LightingPresets] Applied preset: {preset.name}");
    }

    private void CaptureCurrentLighting()
    {
        var preset = new LightingPreset
        {
            name = "Captured Settings",
            ambientMode = RenderSettings.ambientMode,
            ambientColor = RenderSettings.ambientLight,
            ambientSkyColor = RenderSettings.ambientSkyColor,
            ambientEquatorColor = RenderSettings.ambientEquatorColor,
            ambientGroundColor = RenderSettings.ambientGroundColor,
            ambientIntensity = RenderSettings.ambientIntensity,
            fogEnabled = RenderSettings.fog,
            fogColor = RenderSettings.fogColor,
            fogMode = RenderSettings.fogMode,
            fogDensity = RenderSettings.fogDensity,
            fogStartDistance = RenderSettings.fogStartDistance,
            fogEndDistance = RenderSettings.fogEndDistance,
            skyboxMaterial = RenderSettings.skybox
        };

        var sun = FindDirectionalLight();
        if (sun != null)
        {
            preset.sunColor = sun.color;
            preset.sunIntensity = sun.intensity;
            preset.sunRotation = sun.transform.rotation.eulerAngles;
        }

        _presets.Add(preset);
        _selectedPresetIndex = _presets.Count - 1;

        Debug.Log("[LightingPresets] Captured current lighting settings");
    }

    private Light FindDirectionalLight()
    {
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var light in lights)
        {
            if (light.type == LightType.Directional)
                return light;
        }
        return null;
    }

    private void SavePresets()
    {
        string path = EditorUtility.SaveFilePanelInProject("Save Presets", "LightingPresets", "asset", "Save lighting presets");

        if (!string.IsNullOrEmpty(path))
        {
            var data = ScriptableObject.CreateInstance<LightingPresetsData>();
            data.presets = _presets.ToArray();

            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[LightingPresets] Saved to {path}");
        }
    }

    private void LoadPresets()
    {
        string path = EditorUtility.OpenFilePanel("Load Presets", "Assets", "asset");

        if (!string.IsNullOrEmpty(path))
        {
            path = "Assets" + path.Substring(Application.dataPath.Length);
            var data = AssetDatabase.LoadAssetAtPath<LightingPresetsData>(path);

            if (data != null)
            {
                _presets = new List<LightingPreset>(data.presets);
                _selectedPresetIndex = -1;
                Debug.Log($"[LightingPresets] Loaded from {path}");
            }
        }
    }

    private void ExportPreset(LightingPreset preset)
    {
        string path = EditorUtility.SaveFilePanelInProject("Export Preset", preset.name, "asset", "Export preset as ScriptableObject");

        if (!string.IsNullOrEmpty(path))
        {
            var data = ScriptableObject.CreateInstance<LightingPresetAsset>();
            data.preset = preset;

            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[LightingPresets] Exported {preset.name} to {path}");
        }
    }

    #endregion

    #region Data Classes

    [System.Serializable]
    public class LightingPreset
    {
        public string name = "New Preset";

        // Ambient
        public UnityEngine.Rendering.AmbientMode ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        public Color ambientColor = Color.gray;
        public Color ambientSkyColor = Color.blue;
        public Color ambientEquatorColor = Color.gray;
        public Color ambientGroundColor = Color.gray;
        public float ambientIntensity = 1f;

        // Fog
        public bool fogEnabled = false;
        public Color fogColor = Color.gray;
        public FogMode fogMode = FogMode.Linear;
        public float fogDensity = 0.01f;
        public float fogStartDistance = 0f;
        public float fogEndDistance = 300f;

        // Sun
        public Color sunColor = Color.white;
        public float sunIntensity = 1f;
        public Vector3 sunRotation = new Vector3(50, -30, 0);

        // Skybox
        public Material skyboxMaterial;
        public float skyboxExposure = 1f;
    }

    #endregion
}

public class LightingPresetsData : ScriptableObject
{
    public LightingPresetManager.LightingPreset[] presets;
}

public class LightingPresetAsset : ScriptableObject
{
    public LightingPresetManager.LightingPreset preset;
}
