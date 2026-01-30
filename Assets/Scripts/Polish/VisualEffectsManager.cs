using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Gestionnaire d'effets visuels.
/// Gere post-processing, camera effects, meteo et cycle jour/nuit.
/// Style: Esthetique Genshin Impact (couleurs vibrantes, transitions douces).
/// </summary>
public class VisualEffectsManager : MonoBehaviour
{
    #region Singleton

    private static VisualEffectsManager _instance;
    public static VisualEffectsManager Instance
    {
        get => _instance;
        private set => _instance = value;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            SafeDestroy(gameObject);
            return;
        }
        Instance = this;

        _currentWeather = new WeatherData { type = WeatherType.Clear };
        _dayNightSettings = new DayNightSettings();
        _postProcessPreset = new PostProcessPreset();
    }

    private void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(obj);
        }
        else
        {
            Destroy(obj);
        }
#else
        Destroy(obj);
#endif
    }

    #endregion

    #region Fields

    [Header("Post Processing")]
    [SerializeField] private PostProcessPreset _defaultPreset;
    [SerializeField] private PostProcessPreset _combatPreset;
    [SerializeField] private PostProcessPreset _cinematicPreset;

    [Header("Camera Effects")]
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private float _defaultFOV = 60f;
    [SerializeField] private AnimationCurve _shakeCurve;

    [Header("Weather")]
    [SerializeField] private ParticleSystem _rainEffect;
    [SerializeField] private ParticleSystem _snowEffect;
    [SerializeField] private ParticleSystem _fogEffect;
    [SerializeField] private float _weatherTransitionTime = 30f;

    [Header("Day/Night")]
    [SerializeField] private Light _sunLight;
    [SerializeField] private Gradient _skyColorGradient;
    [SerializeField] private Gradient _sunColorGradient;
    [SerializeField] private AnimationCurve _sunIntensityCurve;
    [SerializeField] private float _cycleLengthMinutes = 24f;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;

    // Etats actuels
    private PostProcessPreset _postProcessPreset;
    private WeatherData _currentWeather;
    private WeatherData _targetWeather;
    private DayNightSettings _dayNightSettings;

    // Camera shake
    private Vector3 _originalCameraPosition;
    private Coroutine _shakeCoroutine;

    // Hit stop
    private Coroutine _hitStopCoroutine;
    private float _originalTimeScale;

    // Transitions
    private Coroutine _weatherTransitionCoroutine;
    private Coroutine _postProcessTransitionCoroutine;

    #endregion

    #region Events

    /// <summary>Declenche lors du changement de meteo.</summary>
    public event Action<WeatherType> OnWeatherChanged;

    /// <summary>Declenche lors du changement de periode.</summary>
    public event Action<TimeOfDay> OnTimeOfDayChanged;

    /// <summary>Declenche lors d'un screen shake.</summary>
    public event Action<float> OnScreenShake;

    #endregion

    #region Properties

    /// <summary>Meteo actuelle.</summary>
    public WeatherType CurrentWeather => _currentWeather.type;

    /// <summary>Periode du jour actuelle.</summary>
    public TimeOfDay CurrentTimeOfDay => GetTimeOfDay(_dayNightSettings.currentHour);

    /// <summary>Heure actuelle (0-24).</summary>
    public float CurrentHour => _dayNightSettings.currentHour;

    /// <summary>Cycle jour/nuit actif?</summary>
    public bool IsDayNightCycleActive { get; private set; }

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (_mainCamera != null)
        {
            _originalCameraPosition = _mainCamera.transform.localPosition;
        }

        _originalTimeScale = Time.timeScale;
        ApplyPostProcessPreset(_defaultPreset ?? _postProcessPreset);
    }

    private void Update()
    {
        if (IsDayNightCycleActive)
        {
            UpdateDayNightCycle();
        }

        UpdateWeatherEffects();
    }

    #endregion

    #region Public Methods - Post Processing

    /// <summary>
    /// Applique un preset de post-processing.
    /// </summary>
    /// <param name="preset">Preset.</param>
    /// <param name="transitionTime">Temps de transition.</param>
    public void ApplyPostProcessPreset(PostProcessPreset preset, float transitionTime = 0f)
    {
        if (preset == null) return;

        if (transitionTime > 0)
        {
            if (_postProcessTransitionCoroutine != null)
            {
                StopCoroutine(_postProcessTransitionCoroutine);
            }
            _postProcessTransitionCoroutine = StartCoroutine(TransitionPostProcess(preset, transitionTime));
        }
        else
        {
            _postProcessPreset = preset;
            ApplyPostProcessImmediate(preset);
        }

        Log($"Post-process preset applied");
    }

    /// <summary>
    /// Active le mode combat (contraste eleve, saturation).
    /// </summary>
    public void EnterCombatMode()
    {
        ApplyPostProcessPreset(_combatPreset ?? CreateCombatPreset(), 0.5f);
    }

    /// <summary>
    /// Quitte le mode combat.
    /// </summary>
    public void ExitCombatMode()
    {
        ApplyPostProcessPreset(_defaultPreset ?? _postProcessPreset, 1f);
    }

    /// <summary>
    /// Active le mode cinematique.
    /// </summary>
    public void EnterCinematicMode()
    {
        ApplyPostProcessPreset(_cinematicPreset ?? CreateCinematicPreset(), 0.3f);
    }

    #endregion

    #region Public Methods - Camera Effects

    /// <summary>
    /// Declenche un screen shake.
    /// </summary>
    /// <param name="data">Donnees du shake.</param>
    public void TriggerScreenShake(ScreenShakeData data)
    {
        if (data == null || _mainCamera == null) return;

        if (_shakeCoroutine != null)
        {
            StopCoroutine(_shakeCoroutine);
            _mainCamera.transform.localPosition = _originalCameraPosition;
        }

        _shakeCoroutine = StartCoroutine(ScreenShakeCoroutine(data));
        OnScreenShake?.Invoke(data.intensity);
    }

    /// <summary>
    /// Declenche un screen shake simple.
    /// </summary>
    /// <param name="intensity">Intensite (0-1).</param>
    /// <param name="duration">Duree.</param>
    public void TriggerScreenShake(float intensity, float duration)
    {
        TriggerScreenShake(new ScreenShakeData
        {
            intensity = intensity,
            duration = duration,
            frequency = 15f
        });
    }

    /// <summary>
    /// Declenche un hit stop (freeze frame).
    /// </summary>
    /// <param name="data">Donnees du hit stop.</param>
    public void TriggerHitStop(HitStopData data)
    {
        if (data == null) return;

        if (_hitStopCoroutine != null)
        {
            StopCoroutine(_hitStopCoroutine);
            Time.timeScale = _originalTimeScale;
        }

        _hitStopCoroutine = StartCoroutine(HitStopCoroutine(data));
    }

    /// <summary>
    /// Declenche un hit stop simple.
    /// </summary>
    /// <param name="duration">Duree.</param>
    /// <param name="timeScale">Time scale (0.01 = quasi-pause).</param>
    public void TriggerHitStop(float duration, float timeScale = 0.01f)
    {
        TriggerHitStop(new HitStopData
        {
            duration = duration,
            timeScale = timeScale
        });
    }

    /// <summary>
    /// Change le FOV de la camera.
    /// </summary>
    /// <param name="targetFOV">FOV cible.</param>
    /// <param name="duration">Duree de transition.</param>
    public void SetCameraFOV(float targetFOV, float duration = 0.5f)
    {
        if (_mainCamera == null) return;
        StartCoroutine(TransitionFOV(targetFOV, duration));
    }

    /// <summary>
    /// Reset le FOV au defaut.
    /// </summary>
    public void ResetCameraFOV()
    {
        SetCameraFOV(_defaultFOV, 0.3f);
    }

    #endregion

    #region Public Methods - Weather

    /// <summary>
    /// Change la meteo.
    /// </summary>
    /// <param name="type">Type de meteo.</param>
    /// <param name="intensity">Intensite (0-1).</param>
    /// <param name="transitionTime">Temps de transition.</param>
    public void SetWeather(WeatherType type, float intensity = 1f, float transitionTime = -1f)
    {
        if (transitionTime < 0) transitionTime = _weatherTransitionTime;

        _targetWeather = new WeatherData
        {
            type = type,
            intensity = Mathf.Clamp01(intensity),
            transitionDuration = transitionTime
        };

        if (_weatherTransitionCoroutine != null)
        {
            StopCoroutine(_weatherTransitionCoroutine);
        }

        _weatherTransitionCoroutine = StartCoroutine(TransitionWeather(_targetWeather, transitionTime));

        OnWeatherChanged?.Invoke(type);
        Log($"Weather changing to: {type} (intensity: {intensity:P0})");
    }

    /// <summary>
    /// Obtient les donnees meteo actuelles.
    /// </summary>
    /// <returns>Donnees meteo.</returns>
    public WeatherData GetCurrentWeatherData()
    {
        return _currentWeather;
    }

    #endregion

    #region Public Methods - Day/Night Cycle

    /// <summary>
    /// Demarre le cycle jour/nuit.
    /// </summary>
    public void StartDayNightCycle()
    {
        IsDayNightCycleActive = true;
        Log("Day/night cycle started");
    }

    /// <summary>
    /// Arrete le cycle jour/nuit.
    /// </summary>
    public void StopDayNightCycle()
    {
        IsDayNightCycleActive = false;
        Log("Day/night cycle stopped");
    }

    /// <summary>
    /// Definit l'heure du jour.
    /// </summary>
    /// <param name="hour">Heure (0-24).</param>
    /// <param name="instant">Changement instantane?</param>
    public void SetTimeOfDay(float hour, bool instant = false)
    {
        hour = Mathf.Repeat(hour, 24f);

        if (instant)
        {
            _dayNightSettings.currentHour = hour;
            ApplyTimeOfDay(hour);
        }
        else
        {
            StartCoroutine(TransitionTimeOfDay(hour, 5f));
        }

        Log($"Time set to: {hour:F1}h");
    }

    /// <summary>
    /// Definit la duree du cycle.
    /// </summary>
    /// <param name="minutes">Duree en minutes reelles pour 24h en jeu.</param>
    public void SetCycleLength(float minutes)
    {
        _cycleLengthMinutes = Mathf.Max(1f, minutes);
        _dayNightSettings.cycleLengthMinutes = _cycleLengthMinutes;
    }

    /// <summary>
    /// Obtient les settings jour/nuit.
    /// </summary>
    /// <returns>Settings.</returns>
    public DayNightSettings GetDayNightSettings()
    {
        return _dayNightSettings;
    }

    #endregion

    #region Private Methods - Post Processing

    private void ApplyPostProcessImmediate(PostProcessPreset preset)
    {
        // NOTE: En production, utiliser URP Volume ou HDRP
        // Ici on stocke les valeurs pour reference

        _postProcessPreset = preset;

        // TODO: Appliquer via URP Volume Profile
        // Example:
        // if (preset.bloomEnabled) bloom.intensity.value = preset.bloomIntensity;
        // if (preset.colorGradingEnabled) colorGrading.saturation.value = preset.saturation;
    }

    private IEnumerator TransitionPostProcess(PostProcessPreset target, float duration)
    {
        var start = _postProcessPreset ?? new PostProcessPreset();
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            // Interpoler les valeurs
            var current = new PostProcessPreset
            {
                bloomEnabled = target.bloomEnabled,
                bloomIntensity = Mathf.Lerp(start.bloomIntensity, target.bloomIntensity, t),
                bloomThreshold = Mathf.Lerp(start.bloomThreshold, target.bloomThreshold, t),
                colorGradingEnabled = target.colorGradingEnabled,
                saturation = Mathf.Lerp(start.saturation, target.saturation, t),
                contrast = Mathf.Lerp(start.contrast, target.contrast, t),
                vignetteEnabled = target.vignetteEnabled,
                vignetteIntensity = Mathf.Lerp(start.vignetteIntensity, target.vignetteIntensity, t)
            };

            ApplyPostProcessImmediate(current);
            yield return null;
        }

        ApplyPostProcessImmediate(target);
    }

    private PostProcessPreset CreateCombatPreset()
    {
        return new PostProcessPreset
        {
            bloomEnabled = true,
            bloomIntensity = 0.8f,
            bloomThreshold = 0.8f,
            colorGradingEnabled = true,
            saturation = 1.15f,
            contrast = 1.1f,
            vignetteEnabled = true,
            vignetteIntensity = 0.25f
        };
    }

    private PostProcessPreset CreateCinematicPreset()
    {
        return new PostProcessPreset
        {
            bloomEnabled = true,
            bloomIntensity = 0.4f,
            bloomThreshold = 0.9f,
            colorGradingEnabled = true,
            saturation = 0.9f,
            contrast = 1.05f,
            vignetteEnabled = true,
            vignetteIntensity = 0.4f
        };
    }

    #endregion

    #region Private Methods - Camera Effects

    private IEnumerator ScreenShakeCoroutine(ScreenShakeData data)
    {
        float elapsed = 0f;

        while (elapsed < data.duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = elapsed / data.duration;

            // Utiliser la courbe si disponible, sinon decroissance lineaire
            float intensity = data.intensity;
            if (_shakeCurve != null && _shakeCurve.length > 0)
            {
                intensity *= _shakeCurve.Evaluate(progress);
            }
            else
            {
                intensity *= (1f - progress);
            }

            // Calculer l'offset
            float x = (Mathf.PerlinNoise(Time.time * data.frequency, 0f) - 0.5f) * 2f * intensity;
            float y = (Mathf.PerlinNoise(0f, Time.time * data.frequency) - 0.5f) * 2f * intensity;

            _mainCamera.transform.localPosition = _originalCameraPosition + new Vector3(x, y, 0f);

            yield return null;
        }

        _mainCamera.transform.localPosition = _originalCameraPosition;
        _shakeCoroutine = null;
    }

    private IEnumerator HitStopCoroutine(HitStopData data)
    {
        _originalTimeScale = Time.timeScale;
        Time.timeScale = data.timeScale;

        // Attendre en temps reel
        float elapsed = 0f;
        while (elapsed < data.duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Ease out vers la normale
        float easeTime = 0.1f;
        elapsed = 0f;
        while (elapsed < easeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(data.timeScale, _originalTimeScale, elapsed / easeTime);
            yield return null;
        }

        Time.timeScale = _originalTimeScale;
        _hitStopCoroutine = null;
    }

    private IEnumerator TransitionFOV(float targetFOV, float duration)
    {
        float startFOV = _mainCamera.fieldOfView;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _mainCamera.fieldOfView = Mathf.Lerp(startFOV, targetFOV, elapsed / duration);
            yield return null;
        }

        _mainCamera.fieldOfView = targetFOV;
    }

    #endregion

    #region Private Methods - Weather

    private void UpdateWeatherEffects()
    {
        // Mettre a jour les particules selon l'intensite actuelle
        if (_currentWeather == null) return;

        UpdateParticleSystem(_rainEffect, _currentWeather.type == WeatherType.Rain || _currentWeather.type == WeatherType.Storm);
        UpdateParticleSystem(_snowEffect, _currentWeather.type == WeatherType.Snow);
        UpdateParticleSystem(_fogEffect, _currentWeather.type == WeatherType.Fog);
    }

    private void UpdateParticleSystem(ParticleSystem ps, bool shouldPlay)
    {
        if (ps == null) return;

        if (shouldPlay && !ps.isPlaying)
        {
            ps.Play();
        }
        else if (!shouldPlay && ps.isPlaying)
        {
            ps.Stop();
        }

        if (shouldPlay)
        {
            var emission = ps.emission;
            emission.rateOverTimeMultiplier = _currentWeather.intensity;
        }
    }

    private IEnumerator TransitionWeather(WeatherData target, float duration)
    {
        var start = _currentWeather;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            _currentWeather = new WeatherData
            {
                type = t > 0.5f ? target.type : start.type,
                intensity = Mathf.Lerp(start.intensity, target.intensity, t),
                transitionDuration = target.transitionDuration
            };

            yield return null;
        }

        _currentWeather = target;
        _weatherTransitionCoroutine = null;
    }

    #endregion

    #region Private Methods - Day/Night

    private void UpdateDayNightCycle()
    {
        if (_dayNightSettings == null) return;

        // Avancer le temps
        float hoursPerSecond = 24f / (_cycleLengthMinutes * 60f);
        _dayNightSettings.currentHour += hoursPerSecond * Time.deltaTime;
        _dayNightSettings.currentHour = Mathf.Repeat(_dayNightSettings.currentHour, 24f);

        ApplyTimeOfDay(_dayNightSettings.currentHour);

        // Detecter changement de periode
        var newTimeOfDay = GetTimeOfDay(_dayNightSettings.currentHour);
        if (newTimeOfDay != _dayNightSettings.lastTimeOfDay)
        {
            _dayNightSettings.lastTimeOfDay = newTimeOfDay;
            OnTimeOfDayChanged?.Invoke(newTimeOfDay);
        }
    }

    private void ApplyTimeOfDay(float hour)
    {
        if (_sunLight == null) return;

        // Normaliser (0-1) pour les gradients
        float normalizedTime = hour / 24f;

        // Rotation du soleil
        float sunAngle = (hour - 6f) * 15f; // 6h = 0°, 12h = 90°, 18h = 180°
        _sunLight.transform.rotation = Quaternion.Euler(sunAngle, -30f, 0f);

        // Couleur du soleil
        if (_sunColorGradient != null)
        {
            _sunLight.color = _sunColorGradient.Evaluate(normalizedTime);
        }

        // Intensite du soleil
        if (_sunIntensityCurve != null && _sunIntensityCurve.length > 0)
        {
            _sunLight.intensity = _sunIntensityCurve.Evaluate(normalizedTime);
        }
        else
        {
            // Courbe par defaut: max a midi, min a minuit
            float intensity = Mathf.Sin((hour - 6f) * Mathf.PI / 12f);
            _sunLight.intensity = Mathf.Clamp01(intensity) * 1.5f;
        }

        // Couleur du ciel (via RenderSettings)
        if (_skyColorGradient != null)
        {
            RenderSettings.ambientSkyColor = _skyColorGradient.Evaluate(normalizedTime);
        }
    }

    private IEnumerator TransitionTimeOfDay(float targetHour, float duration)
    {
        float startHour = _dayNightSettings.currentHour;
        float elapsed = 0f;

        // Gerer le passage par minuit
        float diff = targetHour - startHour;
        if (Mathf.Abs(diff) > 12f)
        {
            diff = diff > 0 ? diff - 24f : diff + 24f;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            _dayNightSettings.currentHour = Mathf.Repeat(startHour + diff * t, 24f);
            ApplyTimeOfDay(_dayNightSettings.currentHour);
            yield return null;
        }

        _dayNightSettings.currentHour = targetHour;
        ApplyTimeOfDay(targetHour);
    }

    private TimeOfDay GetTimeOfDay(float hour)
    {
        if (hour >= 5f && hour < 8f) return TimeOfDay.Dawn;
        if (hour >= 8f && hour < 17f) return TimeOfDay.Day;
        if (hour >= 17f && hour < 20f) return TimeOfDay.Dusk;
        return TimeOfDay.Night;
    }

    #endregion

    #region Private Methods - Utility

    private void Log(string message)
    {
        if (_debugMode)
        {
            Debug.Log($"[VFX] {message}");
        }
    }

    #endregion
}

/// <summary>
/// Preset de post-processing.
/// Style Genshin: Bloom doux, couleurs vibrantes.
/// </summary>
[System.Serializable]
public class PostProcessPreset
{
    [Header("Bloom")]
    public bool bloomEnabled = true;
    [Range(0f, 2f)] public float bloomIntensity = 0.5f;
    [Range(0f, 1f)] public float bloomThreshold = 0.9f;
    public Color bloomTint = Color.white;

    [Header("Color Grading")]
    public bool colorGradingEnabled = true;
    [Range(0.5f, 1.5f)] public float saturation = 1.1f;
    [Range(0.5f, 1.5f)] public float contrast = 1.05f;
    [Range(-0.5f, 0.5f)] public float temperature = 0f;
    [Range(-0.5f, 0.5f)] public float tint = 0f;

    [Header("Vignette")]
    public bool vignetteEnabled = true;
    [Range(0f, 1f)] public float vignetteIntensity = 0.25f;
    [Range(0f, 1f)] public float vignetteSmoothness = 0.5f;

    [Header("Other")]
    public bool depthOfFieldEnabled = false;
    public float dofFocusDistance = 10f;
    public bool motionBlurEnabled = false;
    [Range(0f, 1f)] public float motionBlurIntensity = 0.3f;
}

/// <summary>
/// Donnees de screen shake.
/// </summary>
[System.Serializable]
public class ScreenShakeData
{
    [Range(0f, 2f)] public float intensity = 0.5f;
    [Range(0f, 2f)] public float duration = 0.3f;
    [Range(1f, 50f)] public float frequency = 15f;
    public bool useTrauma = false;
}

/// <summary>
/// Donnees de hit stop.
/// </summary>
[System.Serializable]
public class HitStopData
{
    [Range(0f, 0.5f)] public float duration = 0.1f;
    [Range(0f, 0.1f)] public float timeScale = 0.01f;
}

/// <summary>
/// Types de meteo.
/// </summary>
public enum WeatherType
{
    Clear,
    Cloudy,
    Rain,
    Storm,
    Snow,
    Fog,
    Sandstorm,
    Blizzard
}

/// <summary>
/// Donnees meteo.
/// </summary>
[System.Serializable]
public class WeatherData
{
    public WeatherType type;
    [Range(0f, 1f)] public float intensity = 1f;
    public float transitionDuration = 30f;
    public float windSpeed = 0f;
    public Vector3 windDirection = Vector3.right;
}

/// <summary>
/// Periodes du jour.
/// </summary>
public enum TimeOfDay
{
    Dawn,   // 5h-8h
    Day,    // 8h-17h
    Dusk,   // 17h-20h
    Night   // 20h-5h
}

/// <summary>
/// Settings du cycle jour/nuit.
/// </summary>
[System.Serializable]
public class DayNightSettings
{
    [Range(0f, 24f)] public float currentHour = 12f;
    public float cycleLengthMinutes = 24f;
    public bool pauseAtNight = false;
    public TimeOfDay lastTimeOfDay = TimeOfDay.Day;
}
