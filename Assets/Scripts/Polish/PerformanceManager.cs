using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire de performance.
/// Gere LOD, qualite, memoire et metriques.
/// </summary>
public class PerformanceManager : MonoBehaviour
{
    #region Singleton

    private static PerformanceManager _instance;
    public static PerformanceManager Instance
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

        _fpsHistory = new Queue<float>();
        _currentMetrics = new PerformanceMetrics();
        _memoryStats = new MemoryStats();

        LoadQualitySettings();
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

    #region Constants

    private const int FPS_HISTORY_SIZE = 60;
    private const float MEMORY_CHECK_INTERVAL = 5f;
    private const float ADAPTIVE_CHECK_INTERVAL = 2f;

    #endregion

    #region Fields

    [Header("Configuration")]
    [SerializeField] private QualityLevel _defaultQuality = QualityLevel.High;
    [SerializeField] private bool _adaptiveQuality = true;
    [SerializeField] private int _targetFPS = 60;

    [Header("LOD Settings")]
    [SerializeField] private LODLevelData[] _lodLevels;
    [SerializeField] private float _lodBias = 1f;

    [Header("Memory")]
    [SerializeField] private int _memoryWarningThresholdMB = 2048;
    [SerializeField] private int _memoryCriticalThresholdMB = 3072;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;
    [SerializeField] private bool _showOverlay = false;

    // Metriques
    private PerformanceMetrics _currentMetrics;
    private MemoryStats _memoryStats;
    private Queue<float> _fpsHistory;

    // Timers
    private float _lastMemoryCheck;
    private float _lastAdaptiveCheck;
    private float _deltaTime;

    // Settings
    private QualitySettingsData _currentSettings;

    #endregion

    #region Events

    /// <summary>Declenche lors d'un changement de qualite.</summary>
    public event Action<QualityLevel> OnQualityChanged;

    /// <summary>Declenche lors d'un avertissement memoire.</summary>
    public event Action<int> OnMemoryWarning;

    /// <summary>Declenche lors d'une baisse de FPS.</summary>
    public event Action<int> OnFPSDrop;

    #endregion

    #region Properties

    /// <summary>Niveau de qualite actuel.</summary>
    public QualityLevel CurrentQuality { get; private set; }

    /// <summary>Metriques actuelles.</summary>
    public PerformanceMetrics CurrentMetrics => _currentMetrics;

    /// <summary>Stats memoire.</summary>
    public MemoryStats MemoryStats => _memoryStats;

    /// <summary>FPS cible.</summary>
    public int TargetFPS => _targetFPS;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        UpdateMetrics();

        if (Time.time - _lastMemoryCheck >= MEMORY_CHECK_INTERVAL)
        {
            UpdateMemoryStats();
            _lastMemoryCheck = Time.time;
        }

        if (_adaptiveQuality && Time.time - _lastAdaptiveCheck >= ADAPTIVE_CHECK_INTERVAL)
        {
            CheckAdaptiveQuality();
            _lastAdaptiveCheck = Time.time;
        }
    }

    private void OnGUI()
    {
        if (_showOverlay && _debugMode)
        {
            DrawDebugOverlay();
        }
    }

    #endregion

    #region Public Methods - Quality

    /// <summary>
    /// Definit le niveau de qualite.
    /// </summary>
    /// <param name="level">Niveau de qualite.</param>
    public void SetQualityLevel(QualityLevel level)
    {
        CurrentQuality = level;
        _currentSettings = GetSettingsForLevel(level);
        ApplyQualitySettings(_currentSettings);

        OnQualityChanged?.Invoke(level);
        Log($"Quality set to: {level}");
    }

    /// <summary>
    /// Obtient les settings pour un niveau.
    /// </summary>
    /// <param name="level">Niveau.</param>
    /// <returns>Settings.</returns>
    public QualitySettingsData GetSettingsForLevel(QualityLevel level)
    {
        var settings = new QualitySettingsData();

        switch (level)
        {
            case QualityLevel.Low:
                settings.shadowQuality = ShadowQualityLevel.Off;
                settings.textureQuality = TextureQualityLevel.Quarter;
                settings.antiAliasing = AntiAliasingLevel.Off;
                settings.drawDistance = 500f;
                settings.lodBias = 0.5f;
                settings.particleQuality = 0.25f;
                settings.postProcessing = false;
                settings.vsync = false;
                break;

            case QualityLevel.Medium:
                settings.shadowQuality = ShadowQualityLevel.Low;
                settings.textureQuality = TextureQualityLevel.Half;
                settings.antiAliasing = AntiAliasingLevel.FXAA;
                settings.drawDistance = 750f;
                settings.lodBias = 0.75f;
                settings.particleQuality = 0.5f;
                settings.postProcessing = true;
                settings.vsync = false;
                break;

            case QualityLevel.High:
                settings.shadowQuality = ShadowQualityLevel.Medium;
                settings.textureQuality = TextureQualityLevel.Full;
                settings.antiAliasing = AntiAliasingLevel.MSAA2x;
                settings.drawDistance = 1000f;
                settings.lodBias = 1f;
                settings.particleQuality = 0.75f;
                settings.postProcessing = true;
                settings.vsync = true;
                break;

            case QualityLevel.Ultra:
                settings.shadowQuality = ShadowQualityLevel.Ultra;
                settings.textureQuality = TextureQualityLevel.Full;
                settings.antiAliasing = AntiAliasingLevel.MSAA4x;
                settings.drawDistance = 1500f;
                settings.lodBias = 1.5f;
                settings.particleQuality = 1f;
                settings.postProcessing = true;
                settings.vsync = true;
                break;

            case QualityLevel.Custom:
                settings = _currentSettings ?? GetSettingsForLevel(QualityLevel.High);
                break;
        }

        return settings;
    }

    /// <summary>
    /// Applique des settings personnalises.
    /// </summary>
    /// <param name="settings">Settings.</param>
    public void ApplyCustomSettings(QualitySettingsData settings)
    {
        CurrentQuality = QualityLevel.Custom;
        _currentSettings = settings;
        ApplyQualitySettings(settings);

        OnQualityChanged?.Invoke(QualityLevel.Custom);
    }

    #endregion

    #region Public Methods - LOD

    /// <summary>
    /// Definit le biais LOD global.
    /// </summary>
    /// <param name="bias">Biais (0.5 = plus agressif, 2 = moins agressif).</param>
    public void SetLODBias(float bias)
    {
        _lodBias = Mathf.Clamp(bias, 0.25f, 2f);
        QualitySettings.lodBias = _lodBias;
        Log($"LOD bias set to: {_lodBias}");
    }

    /// <summary>
    /// Obtient le niveau LOD pour une distance.
    /// </summary>
    /// <param name="distance">Distance a la camera.</param>
    /// <returns>Niveau LOD (0 = plus detaille).</returns>
    public int GetLODLevelForDistance(float distance)
    {
        if (_lodLevels == null || _lodLevels.Length == 0) return 0;

        float adjustedDistance = distance / _lodBias;

        for (int i = 0; i < _lodLevels.Length; i++)
        {
            if (adjustedDistance < _lodLevels[i].distance)
            {
                return i;
            }
        }

        return _lodLevels.Length;
    }

    #endregion

    #region Public Methods - Memory

    /// <summary>
    /// Force un nettoyage memoire.
    /// </summary>
    public void ForceGarbageCollection()
    {
        var before = GC.GetTotalMemory(false);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var after = GC.GetTotalMemory(true);

        Log($"GC: {(before - after) / 1024 / 1024}MB freed");
    }

    /// <summary>
    /// Decharge les assets non utilises.
    /// </summary>
    public void UnloadUnusedAssets()
    {
        Resources.UnloadUnusedAssets();
        Log("Unused assets unloaded");
    }

    #endregion

    #region Public Methods - FPS

    /// <summary>
    /// Definit le FPS cible.
    /// </summary>
    /// <param name="fps">FPS cible (-1 = illimite).</param>
    public void SetTargetFPS(int fps)
    {
        _targetFPS = fps;
        Application.targetFrameRate = fps;
        Log($"Target FPS: {fps}");
    }

    /// <summary>
    /// Active/desactive VSync.
    /// </summary>
    /// <param name="enabled">Active?</param>
    public void SetVSync(bool enabled)
    {
        QualitySettings.vSyncCount = enabled ? 1 : 0;
        Log($"VSync: {enabled}");
    }

    #endregion

    #region Public Methods - Metrics

    /// <summary>
    /// Obtient le FPS moyen.
    /// </summary>
    /// <returns>FPS moyen.</returns>
    public float GetAverageFPS()
    {
        if (_fpsHistory == null || _fpsHistory.Count == 0) return 0;

        float sum = 0;
        foreach (var fps in _fpsHistory)
        {
            sum += fps;
        }
        return sum / _fpsHistory.Count;
    }

    /// <summary>
    /// Active/desactive l'overlay de debug.
    /// </summary>
    /// <param name="show">Afficher?</param>
    public void ShowDebugOverlay(bool show)
    {
        _showOverlay = show;
    }

    #endregion

    #region Private Methods

    private void LoadQualitySettings()
    {
        // Charger depuis PlayerPrefs ou utiliser defaut
        int savedQuality = PlayerPrefs.GetInt("QualityLevel", (int)_defaultQuality);
        SetQualityLevel((QualityLevel)savedQuality);
    }

    private void ApplyQualitySettings(QualitySettingsData settings)
    {
        // Ombres
        switch (settings.shadowQuality)
        {
            case ShadowQualityLevel.Off:
                QualitySettings.shadows = ShadowQuality.Disable;
                break;
            case ShadowQualityLevel.Low:
                QualitySettings.shadows = ShadowQuality.HardOnly;
                QualitySettings.shadowResolution = ShadowResolution.Low;
                break;
            case ShadowQualityLevel.Medium:
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowResolution = ShadowResolution.Medium;
                break;
            case ShadowQualityLevel.High:
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowResolution = ShadowResolution.High;
                break;
            case ShadowQualityLevel.Ultra:
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
                break;
        }

        // Textures
        QualitySettings.globalTextureMipmapLimit = settings.textureQuality switch
        {
            TextureQualityLevel.Quarter => 2,
            TextureQualityLevel.Half => 1,
            TextureQualityLevel.Full => 0,
            _ => 0
        };

        // Anti-aliasing
        QualitySettings.antiAliasing = settings.antiAliasing switch
        {
            AntiAliasingLevel.Off => 0,
            AntiAliasingLevel.FXAA => 0, // Gere par post-process
            AntiAliasingLevel.MSAA2x => 2,
            AntiAliasingLevel.MSAA4x => 4,
            AntiAliasingLevel.MSAA8x => 8,
            _ => 0
        };

        // LOD
        QualitySettings.lodBias = settings.lodBias;

        // VSync
        QualitySettings.vSyncCount = settings.vsync ? 1 : 0;

        // Sauvegarder
        PlayerPrefs.SetInt("QualityLevel", (int)CurrentQuality);
        PlayerPrefs.Save();
    }

    private void UpdateMetrics()
    {
        _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;

        _currentMetrics.currentFPS = Mathf.RoundToInt(1f / _deltaTime);
        _currentMetrics.frameTimeMs = _deltaTime * 1000f;

        // Historique FPS
        if (_fpsHistory == null) _fpsHistory = new Queue<float>();
        _fpsHistory.Enqueue(_currentMetrics.currentFPS);
        while (_fpsHistory.Count > FPS_HISTORY_SIZE)
        {
            _fpsHistory.Dequeue();
        }

        // Calculer moyenne et min
        float sum = 0, min = float.MaxValue;
        foreach (var fps in _fpsHistory)
        {
            sum += fps;
            if (fps < min) min = fps;
        }
        _currentMetrics.averageFPS = Mathf.RoundToInt(sum / _fpsHistory.Count);
        _currentMetrics.minFPS = Mathf.RoundToInt(min);

        // Detecter chute de FPS
        if (_currentMetrics.currentFPS < _targetFPS * 0.7f)
        {
            OnFPSDrop?.Invoke(_currentMetrics.currentFPS);
        }
    }

    private void UpdateMemoryStats()
    {
        _memoryStats.usedMemoryMB = (int)(GC.GetTotalMemory(false) / 1024 / 1024);
        _memoryStats.totalMemoryMB = SystemInfo.systemMemorySize;
        _memoryStats.gcCollections = GC.CollectionCount(0);

        // Avertissements
        if (_memoryStats.usedMemoryMB > _memoryCriticalThresholdMB)
        {
            OnMemoryWarning?.Invoke(_memoryStats.usedMemoryMB);
            LogWarning($"Memory critical: {_memoryStats.usedMemoryMB}MB");
        }
        else if (_memoryStats.usedMemoryMB > _memoryWarningThresholdMB)
        {
            OnMemoryWarning?.Invoke(_memoryStats.usedMemoryMB);
        }
    }

    private void CheckAdaptiveQuality()
    {
        if (!_adaptiveQuality) return;

        float avgFPS = GetAverageFPS();

        // Baisser la qualite si FPS trop bas
        if (avgFPS < _targetFPS * 0.8f && CurrentQuality > QualityLevel.Low)
        {
            SetQualityLevel(CurrentQuality - 1);
            Log($"Adaptive: lowered quality to {CurrentQuality}");
        }
        // Augmenter si FPS eleve et stable
        else if (avgFPS > _targetFPS * 1.1f && CurrentQuality < QualityLevel.Ultra)
        {
            SetQualityLevel(CurrentQuality + 1);
            Log($"Adaptive: raised quality to {CurrentQuality}");
        }
    }

    private void DrawDebugOverlay()
    {
        GUILayout.BeginArea(new Rect(10, 10, 200, 150));
        GUILayout.BeginVertical("box");

        GUILayout.Label($"FPS: {_currentMetrics.currentFPS} (avg: {_currentMetrics.averageFPS})");
        GUILayout.Label($"Frame: {_currentMetrics.frameTimeMs:F1}ms");
        GUILayout.Label($"Memory: {_memoryStats.usedMemoryMB}MB");
        GUILayout.Label($"Quality: {CurrentQuality}");
        GUILayout.Label($"GC: {_memoryStats.gcCollections}");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void Log(string message)
    {
        if (_debugMode)
        {
            Debug.Log($"[Perf] {message}");
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[Perf] {message}");
    }

    #endregion
}

/// <summary>
/// Niveaux de qualite.
/// </summary>
public enum QualityLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Ultra = 3,
    Custom = 4
}

/// <summary>
/// Qualite des ombres.
/// </summary>
public enum ShadowQualityLevel
{
    Off,
    Low,
    Medium,
    High,
    Ultra
}

/// <summary>
/// Qualite des textures.
/// </summary>
public enum TextureQualityLevel
{
    Quarter,
    Half,
    Full
}

/// <summary>
/// Niveau d'anti-aliasing.
/// </summary>
public enum AntiAliasingLevel
{
    Off,
    FXAA,
    MSAA2x,
    MSAA4x,
    MSAA8x
}

/// <summary>
/// Settings de qualite.
/// </summary>
[System.Serializable]
public class QualitySettingsData
{
    public ShadowQualityLevel shadowQuality;
    public TextureQualityLevel textureQuality;
    public AntiAliasingLevel antiAliasing;
    public float drawDistance = 1000f;
    public float lodBias = 1f;
    public float particleQuality = 1f;
    public bool postProcessing = true;
    public bool vsync = true;
}

/// <summary>
/// Donnees de niveau LOD.
/// </summary>
[System.Serializable]
public class LODLevelData
{
    [Tooltip("Distance pour ce niveau LOD")]
    public float distance = 50f;

    [Tooltip("Reduction de qualite (0-1)")]
    [Range(0f, 1f)]
    public float qualityReduction = 0.5f;

    [Tooltip("Mesh a utiliser")]
    public Mesh mesh;
}

/// <summary>
/// Statistiques memoire.
/// </summary>
[System.Serializable]
public class MemoryStats
{
    public int usedMemoryMB;
    public int totalMemoryMB;
    public int gcCollections;

    public float UsagePercent => totalMemoryMB > 0 ? (float)usedMemoryMB / totalMemoryMB : 0f;
}

/// <summary>
/// Metriques de performance.
/// </summary>
[System.Serializable]
public class PerformanceMetrics
{
    public int currentFPS;
    public int averageFPS;
    public int minFPS;
    public float frameTimeMs;
    public float gpuTimeMs;
}

/// <summary>
/// Etats de chargement.
/// </summary>
public enum LoadingState
{
    Idle,
    Loading,
    Streaming,
    Complete,
    Error
}

/// <summary>
/// Progression de chargement.
/// </summary>
[System.Serializable]
public class LoadingProgress
{
    public float progress;
    public string currentTask;
    public LoadingState state;
    public int loadedAssets;
    public int totalAssets;
}
