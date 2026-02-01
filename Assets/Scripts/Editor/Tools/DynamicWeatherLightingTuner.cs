using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering;

/// <summary>
/// Dynamic Weather and Lighting Tuner for creating atmospheric effects.
/// Provides real-time preview and preset management for weather and time-of-day systems.
/// </summary>
public class DynamicWeatherLightingTuner : EditorWindow
{
    [MenuItem("EpicLegends/Tools/Weather & Lighting Tuner")]
    public static void ShowWindow()
    {
        var window = GetWindow<DynamicWeatherLightingTuner>("Weather & Lighting");
        window.minSize = new Vector2(450, 700);
    }

    // Data structures
    [System.Serializable]
    public class TimeOfDaySettings
    {
        public string name = "Preset";
        public float timeOfDay = 12f; // 0-24
        public Color sunColor = Color.white;
        public float sunIntensity = 1f;
        public float sunAngle = 45f;
        public Color ambientColor = new Color(0.3f, 0.3f, 0.4f);
        public float ambientIntensity = 1f;
        public Color fogColor = new Color(0.5f, 0.6f, 0.7f);
        public float fogDensity = 0.01f;
        public float fogStart = 10f;
        public float fogEnd = 300f;
        public Color skyTint = Color.white;
        public float exposure = 1f;
        public float shadowStrength = 1f;
        public Color shadowColor = new Color(0.2f, 0.2f, 0.3f);
    }

    [System.Serializable]
    public class WeatherSettings
    {
        public string name = "Clear";
        public WeatherType type = WeatherType.Clear;
        public float intensity = 0f;
        public float windStrength = 0f;
        public Vector3 windDirection = Vector3.right;
        public float cloudCoverage = 0f;
        public float cloudSpeed = 1f;
        public float rainIntensity = 0f;
        public float snowIntensity = 0f;
        public float thunderProbability = 0f;
        public float visibilityMultiplier = 1f;
        public float wetness = 0f;
        public AudioClip ambientSound;
        public float ambientVolume = 1f;
    }

    [System.Serializable]
    public class LightingPreset
    {
        public string name = "New Preset";
        public TimeOfDaySettings timeSettings = new TimeOfDaySettings();
        public WeatherSettings weatherSettings = new WeatherSettings();
    }

    public enum WeatherType
    {
        Clear, Cloudy, Overcast, LightRain, HeavyRain, Thunderstorm,
        LightSnow, HeavySnow, Blizzard, Fog, Sandstorm, Haze
    }

    // State
    private List<LightingPreset> _presets = new List<LightingPreset>();
    private int _currentPresetIndex;
    private TimeOfDaySettings _liveSettings = new TimeOfDaySettings();
    private WeatherSettings _liveWeather = new WeatherSettings();

    private bool _autoApply = true;
    private bool _animateTime;
    private float _timeSpeed = 1f;
    private double _lastUpdateTime;

    private Light _sunLight;
    private Material _skyboxMaterial;

    // UI
    private Vector2 _scrollPos;
    private int _currentTab;
    private readonly string[] _tabNames = { "Time of Day", "Weather", "Presets", "Cycle", "Export" };

    private bool _showAdvancedLighting;
    private bool _showAdvancedWeather;

    // Gradient for time visualization
    private Gradient _dayGradient;

    private void OnEnable()
    {
        InitializeDayGradient();
        FindSceneReferences();

        if (_presets.Count == 0)
        {
            CreateDefaultPresets();
        }

        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void InitializeDayGradient()
    {
        _dayGradient = new Gradient();

        GradientColorKey[] colorKeys = new GradientColorKey[5];
        colorKeys[0] = new GradientColorKey(new Color(0.1f, 0.1f, 0.2f), 0f);    // Midnight
        colorKeys[1] = new GradientColorKey(new Color(0.8f, 0.4f, 0.3f), 0.25f); // Sunrise
        colorKeys[2] = new GradientColorKey(new Color(1f, 0.95f, 0.85f), 0.5f);  // Noon
        colorKeys[3] = new GradientColorKey(new Color(0.9f, 0.5f, 0.3f), 0.75f); // Sunset
        colorKeys[4] = new GradientColorKey(new Color(0.1f, 0.1f, 0.2f), 1f);    // Midnight

        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
        alphaKeys[0] = new GradientAlphaKey(1f, 0f);
        alphaKeys[1] = new GradientAlphaKey(1f, 1f);

        _dayGradient.SetKeys(colorKeys, alphaKeys);
    }

    private void FindSceneReferences()
    {
        // Find directional light (sun)
        Light[] lights = FindObjectsOfType<Light>();
        foreach (var light in lights)
        {
            if (light.type == LightType.Directional)
            {
                _sunLight = light;
                break;
            }
        }

        // Get skybox material
        if (RenderSettings.skybox != null)
        {
            _skyboxMaterial = RenderSettings.skybox;
        }
    }

    private void CreateDefaultPresets()
    {
        // Dawn
        _presets.Add(new LightingPreset
        {
            name = "Dawn",
            timeSettings = new TimeOfDaySettings
            {
                name = "Dawn",
                timeOfDay = 6f,
                sunColor = new Color(1f, 0.7f, 0.5f),
                sunIntensity = 0.5f,
                sunAngle = 10f,
                ambientColor = new Color(0.4f, 0.3f, 0.3f),
                fogColor = new Color(0.6f, 0.5f, 0.4f),
                fogDensity = 0.02f,
                exposure = 0.8f
            },
            weatherSettings = new WeatherSettings { name = "Clear", type = WeatherType.Clear }
        });

        // Morning
        _presets.Add(new LightingPreset
        {
            name = "Morning",
            timeSettings = new TimeOfDaySettings
            {
                name = "Morning",
                timeOfDay = 9f,
                sunColor = new Color(1f, 0.95f, 0.85f),
                sunIntensity = 0.8f,
                sunAngle = 35f,
                ambientColor = new Color(0.4f, 0.4f, 0.5f),
                fogColor = new Color(0.7f, 0.75f, 0.8f),
                fogDensity = 0.005f,
                exposure = 1f
            },
            weatherSettings = new WeatherSettings { name = "Clear", type = WeatherType.Clear }
        });

        // Noon
        _presets.Add(new LightingPreset
        {
            name = "Noon",
            timeSettings = new TimeOfDaySettings
            {
                name = "Noon",
                timeOfDay = 12f,
                sunColor = Color.white,
                sunIntensity = 1.2f,
                sunAngle = 80f,
                ambientColor = new Color(0.5f, 0.5f, 0.6f),
                fogColor = new Color(0.7f, 0.8f, 0.9f),
                fogDensity = 0.002f,
                exposure = 1.2f
            },
            weatherSettings = new WeatherSettings { name = "Clear", type = WeatherType.Clear }
        });

        // Sunset
        _presets.Add(new LightingPreset
        {
            name = "Sunset",
            timeSettings = new TimeOfDaySettings
            {
                name = "Sunset",
                timeOfDay = 18f,
                sunColor = new Color(1f, 0.5f, 0.2f),
                sunIntensity = 0.6f,
                sunAngle = 10f,
                ambientColor = new Color(0.5f, 0.3f, 0.3f),
                fogColor = new Color(0.7f, 0.4f, 0.3f),
                fogDensity = 0.015f,
                exposure = 0.9f
            },
            weatherSettings = new WeatherSettings { name = "Clear", type = WeatherType.Clear }
        });

        // Night
        _presets.Add(new LightingPreset
        {
            name = "Night",
            timeSettings = new TimeOfDaySettings
            {
                name = "Night",
                timeOfDay = 0f,
                sunColor = new Color(0.3f, 0.3f, 0.5f),
                sunIntensity = 0.1f,
                sunAngle = -30f,
                ambientColor = new Color(0.1f, 0.1f, 0.2f),
                fogColor = new Color(0.1f, 0.1f, 0.15f),
                fogDensity = 0.03f,
                exposure = 0.4f
            },
            weatherSettings = new WeatherSettings { name = "Clear", type = WeatherType.Clear }
        });

        // Rainy
        _presets.Add(new LightingPreset
        {
            name = "Rainy Day",
            timeSettings = new TimeOfDaySettings
            {
                name = "Rainy",
                timeOfDay = 14f,
                sunColor = new Color(0.6f, 0.6f, 0.7f),
                sunIntensity = 0.4f,
                sunAngle = 50f,
                ambientColor = new Color(0.3f, 0.35f, 0.4f),
                fogColor = new Color(0.5f, 0.55f, 0.6f),
                fogDensity = 0.02f,
                exposure = 0.7f
            },
            weatherSettings = new WeatherSettings
            {
                name = "Rain",
                type = WeatherType.HeavyRain,
                intensity = 0.8f,
                rainIntensity = 0.8f,
                cloudCoverage = 1f,
                windStrength = 0.3f,
                visibilityMultiplier = 0.7f,
                wetness = 0.9f
            }
        });

        // Foggy
        _presets.Add(new LightingPreset
        {
            name = "Foggy Morning",
            timeSettings = new TimeOfDaySettings
            {
                name = "Foggy",
                timeOfDay = 7f,
                sunColor = new Color(0.9f, 0.85f, 0.8f),
                sunIntensity = 0.3f,
                sunAngle = 20f,
                ambientColor = new Color(0.5f, 0.5f, 0.55f),
                fogColor = new Color(0.7f, 0.75f, 0.8f),
                fogDensity = 0.08f,
                fogStart = 5f,
                fogEnd = 100f,
                exposure = 0.6f
            },
            weatherSettings = new WeatherSettings
            {
                name = "Fog",
                type = WeatherType.Fog,
                intensity = 0.9f,
                cloudCoverage = 0.6f,
                visibilityMultiplier = 0.3f
            }
        });
    }

    private void OnEditorUpdate()
    {
        if (_animateTime)
        {
            double currentTime = EditorApplication.timeSinceStartup;
            float delta = (float)(currentTime - _lastUpdateTime);
            _lastUpdateTime = currentTime;

            _liveSettings.timeOfDay += delta * _timeSpeed * 0.1f;
            if (_liveSettings.timeOfDay >= 24f)
                _liveSettings.timeOfDay -= 24f;

            if (_autoApply)
                ApplySettings();

            Repaint();
        }
    }

    private void OnGUI()
    {
        DrawToolbar();

        _currentTab = GUILayout.Toolbar(_currentTab, _tabNames, GUILayout.Height(30));

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        switch (_currentTab)
        {
            case 0: DrawTimeOfDayTab(); break;
            case 1: DrawWeatherTab(); break;
            case 2: DrawPresetsTab(); break;
            case 3: DrawCycleTab(); break;
            case 4: DrawExportTab(); break;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        _autoApply = GUILayout.Toggle(_autoApply, "Auto Apply", EditorStyles.toolbarButton, GUILayout.Width(80));

        if (GUILayout.Button("Apply Now", EditorStyles.toolbarButton, GUILayout.Width(70)))
            ApplySettings();

        if (GUILayout.Button("Reset", EditorStyles.toolbarButton, GUILayout.Width(50)))
            ResetToDefault();

        GUILayout.FlexibleSpace();

        if (_sunLight == null)
        {
            GUI.color = Color.yellow;
            if (GUILayout.Button("Find Sun", EditorStyles.toolbarButton, GUILayout.Width(70)))
                FindSceneReferences();
            GUI.color = Color.white;
        }
        else
        {
            EditorGUILayout.LabelField($"Sun: {_sunLight.name}", GUILayout.Width(120));
        }

        EditorGUILayout.EndHorizontal();
    }

    #region Time of Day Tab

    private void DrawTimeOfDayTab()
    {
        EditorGUILayout.LabelField("Time of Day", EditorStyles.boldLabel);

        // Time slider with gradient background
        DrawTimeSlider();

        EditorGUILayout.Space(10);

        // Quick time buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Dawn\n6:00", GUILayout.Height(40))) SetTime(6f);
        if (GUILayout.Button("Morning\n9:00", GUILayout.Height(40))) SetTime(9f);
        if (GUILayout.Button("Noon\n12:00", GUILayout.Height(40))) SetTime(12f);
        if (GUILayout.Button("Afternoon\n15:00", GUILayout.Height(40))) SetTime(15f);
        if (GUILayout.Button("Sunset\n18:00", GUILayout.Height(40))) SetTime(18f);
        if (GUILayout.Button("Night\n22:00", GUILayout.Height(40))) SetTime(22f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(20);

        // Sun settings
        EditorGUILayout.LabelField("Sun", EditorStyles.boldLabel);
        _liveSettings.sunColor = EditorGUILayout.ColorField("Color", _liveSettings.sunColor);
        _liveSettings.sunIntensity = EditorGUILayout.Slider("Intensity", _liveSettings.sunIntensity, 0f, 3f);
        _liveSettings.sunAngle = EditorGUILayout.Slider("Angle", _liveSettings.sunAngle, -90f, 90f);

        EditorGUILayout.Space(10);

        // Ambient settings
        EditorGUILayout.LabelField("Ambient", EditorStyles.boldLabel);
        _liveSettings.ambientColor = EditorGUILayout.ColorField("Color", _liveSettings.ambientColor);
        _liveSettings.ambientIntensity = EditorGUILayout.Slider("Intensity", _liveSettings.ambientIntensity, 0f, 2f);

        EditorGUILayout.Space(10);

        // Fog settings
        EditorGUILayout.LabelField("Fog", EditorStyles.boldLabel);
        _liveSettings.fogColor = EditorGUILayout.ColorField("Color", _liveSettings.fogColor);
        _liveSettings.fogDensity = EditorGUILayout.Slider("Density", _liveSettings.fogDensity, 0f, 0.1f);

        _showAdvancedLighting = EditorGUILayout.Foldout(_showAdvancedLighting, "Advanced");
        if (_showAdvancedLighting)
        {
            EditorGUI.indentLevel++;
            _liveSettings.fogStart = EditorGUILayout.FloatField("Fog Start", _liveSettings.fogStart);
            _liveSettings.fogEnd = EditorGUILayout.FloatField("Fog End", _liveSettings.fogEnd);
            _liveSettings.exposure = EditorGUILayout.Slider("Exposure", _liveSettings.exposure, 0.1f, 3f);
            _liveSettings.shadowStrength = EditorGUILayout.Slider("Shadow Strength", _liveSettings.shadowStrength, 0f, 1f);
            _liveSettings.shadowColor = EditorGUILayout.ColorField("Shadow Color", _liveSettings.shadowColor);
            _liveSettings.skyTint = EditorGUILayout.ColorField("Sky Tint", _liveSettings.skyTint);
            EditorGUI.indentLevel--;
        }

        if (_autoApply && GUI.changed)
            ApplySettings();
    }

    private void DrawTimeSlider()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Time display
        int hours = Mathf.FloorToInt(_liveSettings.timeOfDay);
        int minutes = Mathf.FloorToInt((_liveSettings.timeOfDay - hours) * 60f);
        EditorGUILayout.LabelField($"Time: {hours:D2}:{minutes:D2}", EditorStyles.boldLabel);

        // Gradient background
        Rect sliderRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(30));

        // Draw gradient
        for (int i = 0; i < (int)sliderRect.width; i++)
        {
            float t = i / sliderRect.width;
            Color c = _dayGradient.Evaluate(t);
            EditorGUI.DrawRect(new Rect(sliderRect.x + i, sliderRect.y, 1, sliderRect.height), c);
        }

        // Draw time marker
        float markerX = sliderRect.x + (_liveSettings.timeOfDay / 24f) * sliderRect.width;
        EditorGUI.DrawRect(new Rect(markerX - 1, sliderRect.y, 3, sliderRect.height), Color.white);

        // Slider
        _liveSettings.timeOfDay = GUI.HorizontalSlider(
            new Rect(sliderRect.x, sliderRect.y + sliderRect.height + 5, sliderRect.width, 20),
            _liveSettings.timeOfDay, 0f, 24f);

        EditorGUILayout.Space(25);
        EditorGUILayout.EndVertical();
    }

    private void SetTime(float time)
    {
        _liveSettings.timeOfDay = time;

        // Auto-adjust sun angle based on time
        if (time >= 6f && time <= 18f)
        {
            float t = (time - 6f) / 12f;
            _liveSettings.sunAngle = Mathf.Sin(t * Mathf.PI) * 80f;
        }
        else
        {
            _liveSettings.sunAngle = -30f;
        }

        // Auto-adjust colors based on time
        _liveSettings.sunColor = _dayGradient.Evaluate(time / 24f);

        if (_autoApply)
            ApplySettings();
    }

    #endregion

    #region Weather Tab

    private void DrawWeatherTab()
    {
        EditorGUILayout.LabelField("Weather", EditorStyles.boldLabel);

        // Weather type selector with icons
        EditorGUILayout.BeginHorizontal();
        DrawWeatherButton("Clear", WeatherType.Clear, new Color(0.5f, 0.8f, 1f));
        DrawWeatherButton("Cloudy", WeatherType.Cloudy, new Color(0.7f, 0.7f, 0.7f));
        DrawWeatherButton("Rain", WeatherType.LightRain, new Color(0.4f, 0.5f, 0.7f));
        DrawWeatherButton("Storm", WeatherType.Thunderstorm, new Color(0.3f, 0.3f, 0.5f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        DrawWeatherButton("Snow", WeatherType.LightSnow, Color.white);
        DrawWeatherButton("Blizzard", WeatherType.Blizzard, new Color(0.9f, 0.95f, 1f));
        DrawWeatherButton("Fog", WeatherType.Fog, new Color(0.6f, 0.65f, 0.7f));
        DrawWeatherButton("Sandstorm", WeatherType.Sandstorm, new Color(0.8f, 0.7f, 0.5f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(20);

        // Weather intensity
        EditorGUILayout.LabelField($"Current: {_liveWeather.type}", EditorStyles.boldLabel);
        _liveWeather.intensity = EditorGUILayout.Slider("Intensity", _liveWeather.intensity, 0f, 1f);

        EditorGUILayout.Space(10);

        // Precipitation
        if (_liveWeather.type == WeatherType.LightRain || _liveWeather.type == WeatherType.HeavyRain ||
            _liveWeather.type == WeatherType.Thunderstorm)
        {
            EditorGUILayout.LabelField("Rain", EditorStyles.boldLabel);
            _liveWeather.rainIntensity = EditorGUILayout.Slider("Rain Amount", _liveWeather.rainIntensity, 0f, 1f);
            _liveWeather.wetness = EditorGUILayout.Slider("Surface Wetness", _liveWeather.wetness, 0f, 1f);

            if (_liveWeather.type == WeatherType.Thunderstorm)
            {
                _liveWeather.thunderProbability = EditorGUILayout.Slider("Thunder Frequency", _liveWeather.thunderProbability, 0f, 1f);
            }
        }

        if (_liveWeather.type == WeatherType.LightSnow || _liveWeather.type == WeatherType.HeavySnow ||
            _liveWeather.type == WeatherType.Blizzard)
        {
            EditorGUILayout.LabelField("Snow", EditorStyles.boldLabel);
            _liveWeather.snowIntensity = EditorGUILayout.Slider("Snow Amount", _liveWeather.snowIntensity, 0f, 1f);
        }

        EditorGUILayout.Space(10);

        // Wind
        EditorGUILayout.LabelField("Wind", EditorStyles.boldLabel);
        _liveWeather.windStrength = EditorGUILayout.Slider("Strength", _liveWeather.windStrength, 0f, 1f);
        _liveWeather.windDirection = EditorGUILayout.Vector3Field("Direction", _liveWeather.windDirection);

        EditorGUILayout.Space(10);

        // Clouds
        EditorGUILayout.LabelField("Clouds", EditorStyles.boldLabel);
        _liveWeather.cloudCoverage = EditorGUILayout.Slider("Coverage", _liveWeather.cloudCoverage, 0f, 1f);
        _liveWeather.cloudSpeed = EditorGUILayout.Slider("Speed", _liveWeather.cloudSpeed, 0f, 5f);

        _showAdvancedWeather = EditorGUILayout.Foldout(_showAdvancedWeather, "Advanced");
        if (_showAdvancedWeather)
        {
            EditorGUI.indentLevel++;
            _liveWeather.visibilityMultiplier = EditorGUILayout.Slider("Visibility", _liveWeather.visibilityMultiplier, 0f, 1f);
            _liveWeather.ambientVolume = EditorGUILayout.Slider("Ambient Volume", _liveWeather.ambientVolume, 0f, 1f);
            EditorGUI.indentLevel--;
        }

        if (_autoApply && GUI.changed)
            ApplyWeather();
    }

    private void DrawWeatherButton(string label, WeatherType type, Color color)
    {
        bool isSelected = _liveWeather.type == type;
        GUI.color = isSelected ? Color.white : color;

        if (GUILayout.Button(label, GUILayout.Height(35)))
        {
            SetWeatherType(type);
        }

        GUI.color = Color.white;
    }

    private void SetWeatherType(WeatherType type)
    {
        _liveWeather.type = type;

        // Apply presets based on type
        switch (type)
        {
            case WeatherType.Clear:
                _liveWeather.cloudCoverage = 0.1f;
                _liveWeather.rainIntensity = 0f;
                _liveWeather.snowIntensity = 0f;
                _liveWeather.windStrength = 0.1f;
                _liveWeather.visibilityMultiplier = 1f;
                break;

            case WeatherType.Cloudy:
                _liveWeather.cloudCoverage = 0.6f;
                _liveWeather.rainIntensity = 0f;
                _liveWeather.visibilityMultiplier = 0.9f;
                break;

            case WeatherType.LightRain:
                _liveWeather.cloudCoverage = 0.8f;
                _liveWeather.rainIntensity = 0.3f;
                _liveWeather.wetness = 0.5f;
                _liveWeather.visibilityMultiplier = 0.8f;
                break;

            case WeatherType.HeavyRain:
                _liveWeather.cloudCoverage = 1f;
                _liveWeather.rainIntensity = 0.8f;
                _liveWeather.wetness = 1f;
                _liveWeather.windStrength = 0.4f;
                _liveWeather.visibilityMultiplier = 0.6f;
                break;

            case WeatherType.Thunderstorm:
                _liveWeather.cloudCoverage = 1f;
                _liveWeather.rainIntensity = 1f;
                _liveWeather.wetness = 1f;
                _liveWeather.windStrength = 0.7f;
                _liveWeather.thunderProbability = 0.3f;
                _liveWeather.visibilityMultiplier = 0.4f;
                break;

            case WeatherType.LightSnow:
                _liveWeather.cloudCoverage = 0.7f;
                _liveWeather.snowIntensity = 0.3f;
                _liveWeather.rainIntensity = 0f;
                _liveWeather.visibilityMultiplier = 0.85f;
                break;

            case WeatherType.HeavySnow:
                _liveWeather.cloudCoverage = 0.9f;
                _liveWeather.snowIntensity = 0.8f;
                _liveWeather.windStrength = 0.3f;
                _liveWeather.visibilityMultiplier = 0.6f;
                break;

            case WeatherType.Blizzard:
                _liveWeather.cloudCoverage = 1f;
                _liveWeather.snowIntensity = 1f;
                _liveWeather.windStrength = 0.9f;
                _liveWeather.visibilityMultiplier = 0.2f;
                break;

            case WeatherType.Fog:
                _liveWeather.cloudCoverage = 0.5f;
                _liveWeather.visibilityMultiplier = 0.2f;
                break;

            case WeatherType.Sandstorm:
                _liveWeather.windStrength = 0.8f;
                _liveWeather.visibilityMultiplier = 0.3f;
                break;
        }

        _liveWeather.intensity = 0.5f;

        if (_autoApply)
            ApplyWeather();
    }

    #endregion

    #region Presets Tab

    private void DrawPresetsTab()
    {
        EditorGUILayout.LabelField("Lighting Presets", EditorStyles.boldLabel);

        for (int i = 0; i < _presets.Count; i++)
        {
            DrawPresetItem(i);
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("+ Save Current as New Preset", GUILayout.Height(30)))
        {
            _presets.Add(new LightingPreset
            {
                name = $"Preset {_presets.Count + 1}",
                timeSettings = JsonUtility.FromJson<TimeOfDaySettings>(JsonUtility.ToJson(_liveSettings)),
                weatherSettings = JsonUtility.FromJson<WeatherSettings>(JsonUtility.ToJson(_liveWeather))
            });
        }

        EditorGUILayout.Space(10);

        // Default presets
        EditorGUILayout.LabelField("Reset to Defaults", EditorStyles.boldLabel);
        if (GUILayout.Button("Restore Default Presets"))
        {
            if (EditorUtility.DisplayDialog("Restore Defaults", "This will replace all presets with defaults. Continue?", "Yes", "No"))
            {
                _presets.Clear();
                CreateDefaultPresets();
            }
        }
    }

    private void DrawPresetItem(int index)
    {
        var preset = _presets[index];
        bool isSelected = index == _currentPresetIndex;

        EditorGUILayout.BeginHorizontal(isSelected ? "SelectionRect" : EditorStyles.helpBox);

        // Preview color
        Color previewColor = preset.timeSettings.sunColor;
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(20, 20), previewColor);

        preset.name = EditorGUILayout.TextField(preset.name);

        EditorGUILayout.LabelField($"{preset.timeSettings.timeOfDay:F1}h", GUILayout.Width(40));
        EditorGUILayout.LabelField(preset.weatherSettings.type.ToString(), GUILayout.Width(80));

        if (GUILayout.Button("Apply", GUILayout.Width(50)))
        {
            ApplyPreset(index);
        }

        if (GUILayout.Button("Update", GUILayout.Width(55)))
        {
            preset.timeSettings = JsonUtility.FromJson<TimeOfDaySettings>(JsonUtility.ToJson(_liveSettings));
            preset.weatherSettings = JsonUtility.FromJson<WeatherSettings>(JsonUtility.ToJson(_liveWeather));
        }

        GUI.color = Color.red;
        if (GUILayout.Button("X", GUILayout.Width(20)))
        {
            _presets.RemoveAt(index);
        }
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();
    }

    private void ApplyPreset(int index)
    {
        _currentPresetIndex = index;
        var preset = _presets[index];

        _liveSettings = JsonUtility.FromJson<TimeOfDaySettings>(JsonUtility.ToJson(preset.timeSettings));
        _liveWeather = JsonUtility.FromJson<WeatherSettings>(JsonUtility.ToJson(preset.weatherSettings));

        ApplySettings();
        ApplyWeather();
    }

    #endregion

    #region Cycle Tab

    private void DrawCycleTab()
    {
        EditorGUILayout.LabelField("Day/Night Cycle", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUI.color = _animateTime ? Color.green : Color.white;
        if (GUILayout.Button(_animateTime ? "Stop Animation" : "Start Animation", GUILayout.Height(35)))
        {
            _animateTime = !_animateTime;
            _lastUpdateTime = EditorApplication.timeSinceStartup;
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        _timeSpeed = EditorGUILayout.Slider("Time Speed", _timeSpeed, 0.1f, 10f);

        EditorGUILayout.Space(10);

        // Time preview
        DrawTimeSlider();

        EditorGUILayout.Space(20);

        // Cycle settings
        EditorGUILayout.LabelField("Cycle Configuration", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Configure how lighting interpolates between presets during the day/night cycle.\n\n" +
            "In a real implementation, you would:\n" +
            "1. Assign presets to specific times\n" +
            "2. Interpolate between presets based on current time\n" +
            "3. Trigger weather transitions",
            MessageType.Info);

        EditorGUILayout.Space(10);

        // Preset timeline
        EditorGUILayout.LabelField("Preset Timeline", EditorStyles.boldLabel);

        Rect timelineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(60));
        EditorGUI.DrawRect(timelineRect, new Color(0.2f, 0.2f, 0.2f));

        // Draw hour markers
        for (int h = 0; h <= 24; h += 6)
        {
            float x = timelineRect.x + (h / 24f) * timelineRect.width;
            EditorGUI.DrawRect(new Rect(x, timelineRect.y, 1, timelineRect.height), Color.gray);
            GUI.Label(new Rect(x - 10, timelineRect.y + timelineRect.height - 15, 30, 15),
                     $"{h}h", EditorStyles.miniLabel);
        }

        // Draw preset markers
        foreach (var preset in _presets)
        {
            float x = timelineRect.x + (preset.timeSettings.timeOfDay / 24f) * timelineRect.width;
            EditorGUI.DrawRect(new Rect(x - 5, timelineRect.y + 10, 10, 30), preset.timeSettings.sunColor);
            GUI.Label(new Rect(x - 20, timelineRect.y + 45, 50, 15),
                     preset.name, new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });
        }

        // Draw current time
        float currentX = timelineRect.x + (_liveSettings.timeOfDay / 24f) * timelineRect.width;
        EditorGUI.DrawRect(new Rect(currentX - 1, timelineRect.y, 3, timelineRect.height), Color.white);
    }

    #endregion

    #region Export Tab

    private void DrawExportTab()
    {
        EditorGUILayout.LabelField("Export Settings", EditorStyles.boldLabel);

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Export Presets to JSON", GUILayout.Height(30)))
        {
            ExportToJson();
        }

        if (GUILayout.Button("Import Presets from JSON", GUILayout.Height(30)))
        {
            ImportFromJson();
        }

        EditorGUILayout.Space(20);

        EditorGUILayout.LabelField("Generate Scripts", EditorStyles.boldLabel);

        if (GUILayout.Button("Generate TimeOfDay ScriptableObject", GUILayout.Height(30)))
        {
            GenerateScriptableObject();
        }

        if (GUILayout.Button("Generate Weather Controller Script", GUILayout.Height(30)))
        {
            GenerateWeatherController();
        }

        EditorGUILayout.Space(20);

        EditorGUILayout.LabelField("Current Settings Summary", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Time: {_liveSettings.timeOfDay:F1}h");
        EditorGUILayout.LabelField($"Sun Intensity: {_liveSettings.sunIntensity:F2}");
        EditorGUILayout.LabelField($"Weather: {_liveWeather.type} ({_liveWeather.intensity:P0})");
        EditorGUILayout.LabelField($"Cloud Coverage: {_liveWeather.cloudCoverage:P0}");
        EditorGUILayout.LabelField($"Visibility: {_liveWeather.visibilityMultiplier:P0}");
        EditorGUILayout.EndVertical();
    }

    private void ExportToJson()
    {
        string path = EditorUtility.SaveFilePanel("Export Presets", "", "LightingPresets", "json");
        if (string.IsNullOrEmpty(path)) return;

        var data = new LightingPresetsData { presets = _presets };
        string json = JsonUtility.ToJson(data, true);
        System.IO.File.WriteAllText(path, json);
        Debug.Log($"Exported to: {path}");
    }

    private void ImportFromJson()
    {
        string path = EditorUtility.OpenFilePanel("Import Presets", "", "json");
        if (string.IsNullOrEmpty(path)) return;

        string json = System.IO.File.ReadAllText(path);
        var data = JsonUtility.FromJson<LightingPresetsData>(json);
        _presets = data.presets;
        Debug.Log($"Imported {_presets.Count} presets");
    }

    private void GenerateScriptableObject()
    {
        Debug.Log("Generate ScriptableObject - implement based on your project structure");
    }

    private void GenerateWeatherController()
    {
        string script = @"using UnityEngine;

public class WeatherController : MonoBehaviour
{
    [Header(""References"")]
    public Light sunLight;

    [Header(""Time"")]
    [Range(0f, 24f)]
    public float timeOfDay = 12f;
    public float timeSpeed = 1f;
    public bool animateTime = false;

    [Header(""Weather"")]
    public float weatherIntensity = 0f;
    public float cloudCoverage = 0f;
    public float windStrength = 0f;

    private void Update()
    {
        if (animateTime)
        {
            timeOfDay += Time.deltaTime * timeSpeed * 0.01f;
            if (timeOfDay >= 24f) timeOfDay -= 24f;
        }

        UpdateLighting();
    }

    private void UpdateLighting()
    {
        if (sunLight == null) return;

        // Calculate sun angle based on time
        float sunAngle = (timeOfDay - 6f) / 12f * 180f - 90f;
        sunLight.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);

        // Adjust intensity based on time
        float t = Mathf.Sin(timeOfDay / 24f * Mathf.PI);
        sunLight.intensity = Mathf.Lerp(0.1f, 1.2f, t);
    }
}
";

        string path = EditorUtility.SaveFilePanel("Save Weather Controller", "Assets/Scripts", "WeatherController", "cs");
        if (!string.IsNullOrEmpty(path))
        {
            System.IO.File.WriteAllText(path, script);
            AssetDatabase.Refresh();
            Debug.Log($"Weather controller saved to: {path}");
        }
    }

    [System.Serializable]
    private class LightingPresetsData
    {
        public List<LightingPreset> presets;
    }

    #endregion

    #region Apply Settings

    private void ApplySettings()
    {
        if (_sunLight != null)
        {
            _sunLight.color = _liveSettings.sunColor;
            _sunLight.intensity = _liveSettings.sunIntensity;
            _sunLight.shadowStrength = _liveSettings.shadowStrength;

            // Rotate sun based on time
            float sunRotation = (_liveSettings.timeOfDay - 6f) / 12f * 180f;
            _sunLight.transform.rotation = Quaternion.Euler(_liveSettings.sunAngle, 170f, 0f);
        }

        // Ambient
        RenderSettings.ambientLight = _liveSettings.ambientColor;
        RenderSettings.ambientIntensity = _liveSettings.ambientIntensity;

        // Fog
        RenderSettings.fogColor = _liveSettings.fogColor;
        RenderSettings.fogDensity = _liveSettings.fogDensity;
        RenderSettings.fogStartDistance = _liveSettings.fogStart;
        RenderSettings.fogEndDistance = _liveSettings.fogEnd;

        // Skybox
        if (_skyboxMaterial != null)
        {
            if (_skyboxMaterial.HasProperty("_Tint"))
                _skyboxMaterial.SetColor("_Tint", _liveSettings.skyTint);
            if (_skyboxMaterial.HasProperty("_Exposure"))
                _skyboxMaterial.SetFloat("_Exposure", _liveSettings.exposure);
        }

        SceneView.RepaintAll();
    }

    private void ApplyWeather()
    {
        // Adjust fog based on visibility
        float baseFogDensity = _liveSettings.fogDensity;
        RenderSettings.fogDensity = baseFogDensity / _liveWeather.visibilityMultiplier;

        // Adjust ambient based on cloud coverage
        Color adjustedAmbient = Color.Lerp(_liveSettings.ambientColor,
                                           _liveSettings.ambientColor * 0.6f,
                                           _liveWeather.cloudCoverage);
        RenderSettings.ambientLight = adjustedAmbient;

        // Adjust sun intensity based on cloud coverage
        if (_sunLight != null)
        {
            float adjustedIntensity = _liveSettings.sunIntensity * (1f - _liveWeather.cloudCoverage * 0.5f);
            _sunLight.intensity = adjustedIntensity;
        }

        SceneView.RepaintAll();
    }

    private void ResetToDefault()
    {
        _liveSettings = new TimeOfDaySettings();
        _liveWeather = new WeatherSettings();
        ApplySettings();
    }

    #endregion
}
