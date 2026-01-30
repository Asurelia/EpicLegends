using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire d'accessibilite.
/// Gere modes daltoniens, sous-titres, scaling UI et options de difficulte.
/// </summary>
public class AccessibilityManager : MonoBehaviour
{
    #region Singleton

    private static AccessibilityManager _instance;
    public static AccessibilityManager Instance
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

        _settings = new AccessibilitySettings();
        _subtitleSettings = new SubtitleSettings();
        _uiScaleSettings = new UIScaleSettings();
        _difficultySettings = new DifficultySettings();
        _inputBindings = new List<InputBindingData>();

        LoadAllSettings();
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

    [Header("Colorblind")]
    [SerializeField] private Material _colorblindShaderMaterial;
    [SerializeField] private ColorblindPreset[] _colorblindPresets;

    [Header("Subtitle")]
    [SerializeField] private GameObject _subtitlePanelPrefab;
    [SerializeField] private float _subtitleDisplayDuration = 5f;

    [Header("UI Scale")]
    [SerializeField] private float _minUIScale = 0.75f;
    [SerializeField] private float _maxUIScale = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;

    // Settings
    private AccessibilitySettings _settings;
    private SubtitleSettings _subtitleSettings;
    private UIScaleSettings _uiScaleSettings;
    private DifficultySettings _difficultySettings;
    private List<InputBindingData> _inputBindings;

    // Sous-titres actifs
    private Queue<SubtitleEntry> _subtitleQueue;
    private SubtitleEntry _currentSubtitle;

    #endregion

    #region Events

    /// <summary>Declenche lors du changement de mode daltonien.</summary>
    public event Action<ColorblindMode> OnColorblindModeChanged;

    /// <summary>Declenche lors du changement d'echelle UI.</summary>
    public event Action<float> OnUIScaleChanged;

    /// <summary>Declenche lors du changement de difficulte.</summary>
    public event Action<DifficultyLevel> OnDifficultyChanged;

    /// <summary>Declenche lors de l'affichage d'un sous-titre.</summary>
    public event Action<SubtitleEntry> OnSubtitleDisplayed;

    /// <summary>Declenche lors du changement de binding.</summary>
    public event Action<string, string> OnInputBindingChanged;

    #endregion

    #region Properties

    /// <summary>Mode daltonien actuel.</summary>
    public ColorblindMode CurrentColorblindMode => _settings.colorblindMode;

    /// <summary>Sous-titres actives?</summary>
    public bool SubtitlesEnabled => _subtitleSettings.enabled;

    /// <summary>Niveau de difficulte actuel.</summary>
    public DifficultyLevel CurrentDifficulty => _difficultySettings.difficulty;

    /// <summary>Settings d'accessibilite.</summary>
    public AccessibilitySettings Settings => _settings;

    /// <summary>Settings de sous-titres.</summary>
    public SubtitleSettings SubtitleSettings => _subtitleSettings;

    /// <summary>Settings d'echelle UI.</summary>
    public UIScaleSettings UIScaleSettings => _uiScaleSettings;

    /// <summary>Settings de difficulte.</summary>
    public DifficultySettings DifficultySettings => _difficultySettings;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        UpdateSubtitles();
    }

    #endregion

    #region Public Methods - Colorblind

    /// <summary>
    /// Definit le mode daltonien.
    /// </summary>
    /// <param name="mode">Mode.</param>
    public void SetColorblindMode(ColorblindMode mode)
    {
        _settings.colorblindMode = mode;
        ApplyColorblindMode(mode);
        SaveSettings();

        OnColorblindModeChanged?.Invoke(mode);
        Log($"Colorblind mode: {mode}");
    }

    /// <summary>
    /// Obtient les couleurs ajustees pour le mode daltonien actuel.
    /// </summary>
    /// <param name="originalColor">Couleur originale.</param>
    /// <returns>Couleur ajustee.</returns>
    public Color GetColorblindAdjustedColor(Color originalColor)
    {
        if (_settings.colorblindMode == ColorblindMode.None)
        {
            return originalColor;
        }

        // Appliquer la matrice de transformation daltonienne
        var matrix = GetColorblindMatrix(_settings.colorblindMode);
        return ApplyColorMatrix(originalColor, matrix);
    }

    /// <summary>
    /// Active/desactive le mode haut contraste.
    /// </summary>
    /// <param name="enabled">Active?</param>
    public void SetHighContrast(bool enabled)
    {
        _settings.highContrast = enabled;
        // TODO: Appliquer via shader ou UI
        SaveSettings();
    }

    #endregion

    #region Public Methods - Subtitles

    /// <summary>
    /// Active/desactive les sous-titres.
    /// </summary>
    /// <param name="enabled">Active?</param>
    public void SetSubtitlesEnabled(bool enabled)
    {
        _subtitleSettings.enabled = enabled;
        SaveSubtitleSettings();
        Log($"Subtitles: {enabled}");
    }

    /// <summary>
    /// Definit la taille des sous-titres.
    /// </summary>
    /// <param name="size">Taille.</param>
    public void SetSubtitleSize(SubtitleSize size)
    {
        _subtitleSettings.fontSize = size;
        SaveSubtitleSettings();
    }

    /// <summary>
    /// Configure les sous-titres.
    /// </summary>
    /// <param name="settings">Settings.</param>
    public void ConfigureSubtitles(SubtitleSettings settings)
    {
        _subtitleSettings = settings;
        SaveSubtitleSettings();
    }

    /// <summary>
    /// Affiche un sous-titre.
    /// </summary>
    /// <param name="text">Texte.</param>
    /// <param name="speaker">Locuteur.</param>
    /// <param name="duration">Duree (0 = defaut).</param>
    public void ShowSubtitle(string text, string speaker = null, float duration = 0f)
    {
        if (!_subtitleSettings.enabled) return;

        var entry = new SubtitleEntry
        {
            text = text,
            speaker = speaker,
            duration = duration > 0 ? duration : _subtitleDisplayDuration,
            timestamp = Time.time
        };

        if (_subtitleQueue == null) _subtitleQueue = new Queue<SubtitleEntry>();
        _subtitleQueue.Enqueue(entry);

        if (_currentSubtitle == null)
        {
            DisplayNextSubtitle();
        }
    }

    /// <summary>
    /// Efface tous les sous-titres.
    /// </summary>
    public void ClearSubtitles()
    {
        _subtitleQueue?.Clear();
        _currentSubtitle = null;
    }

    #endregion

    #region Public Methods - UI Scale

    /// <summary>
    /// Definit l'echelle du texte.
    /// </summary>
    /// <param name="scale">Niveau de scale.</param>
    public void SetTextScale(TextScaleLevel scale)
    {
        _uiScaleSettings.textScale = scale;
        ApplyTextScale(GetTextScaleMultiplier(scale));
        SaveUISettings();
    }

    /// <summary>
    /// Definit l'echelle UI globale.
    /// </summary>
    /// <param name="scale">Echelle (0.75-1.5).</param>
    public void SetUIScale(float scale)
    {
        scale = Mathf.Clamp(scale, _minUIScale, _maxUIScale);
        _uiScaleSettings.uiScale = scale;
        ApplyUIScale(scale);
        SaveUISettings();

        OnUIScaleChanged?.Invoke(scale);
        Log($"UI scale: {scale:P0}");
    }

    /// <summary>
    /// Obtient le multiplicateur de taille de texte.
    /// </summary>
    /// <param name="scale">Niveau.</param>
    /// <returns>Multiplicateur.</returns>
    public float GetTextScaleMultiplier(TextScaleLevel scale)
    {
        return scale switch
        {
            TextScaleLevel.Small => 0.85f,
            TextScaleLevel.Normal => 1f,
            TextScaleLevel.Large => 1.25f,
            TextScaleLevel.ExtraLarge => 1.5f,
            _ => 1f
        };
    }

    #endregion

    #region Public Methods - Input Remapping

    /// <summary>
    /// Obtient le binding d'une action.
    /// </summary>
    /// <param name="actionName">Nom de l'action.</param>
    /// <returns>Binding ou null.</returns>
    public InputBindingData GetBinding(string actionName)
    {
        if (_inputBindings == null) return null;

        foreach (var binding in _inputBindings)
        {
            if (binding.actionName == actionName) return binding;
        }

        return null;
    }

    /// <summary>
    /// Definit le binding clavier d'une action.
    /// </summary>
    /// <param name="actionName">Action.</param>
    /// <param name="key">Touche.</param>
    public void SetKeyboardBinding(string actionName, string key)
    {
        var binding = GetOrCreateBinding(actionName);
        binding.keyboardKey = key;
        SaveInputBindings();

        OnInputBindingChanged?.Invoke(actionName, key);
        Log($"Keyboard binding: {actionName} = {key}");
    }

    /// <summary>
    /// Definit le binding manette d'une action.
    /// </summary>
    /// <param name="actionName">Action.</param>
    /// <param name="button">Bouton.</param>
    public void SetControllerBinding(string actionName, string button)
    {
        var binding = GetOrCreateBinding(actionName);
        binding.controllerButton = button;
        SaveInputBindings();

        OnInputBindingChanged?.Invoke(actionName, button);
        Log($"Controller binding: {actionName} = {button}");
    }

    /// <summary>
    /// Reset tous les bindings aux defauts.
    /// </summary>
    public void ResetBindingsToDefault()
    {
        _inputBindings = GetDefaultBindings();
        SaveInputBindings();
        Log("Bindings reset to defaults");
    }

    /// <summary>
    /// Obtient tous les bindings.
    /// </summary>
    /// <returns>Liste des bindings.</returns>
    public List<InputBindingData> GetAllBindings()
    {
        return new List<InputBindingData>(_inputBindings ?? new List<InputBindingData>());
    }

    #endregion

    #region Public Methods - Difficulty

    /// <summary>
    /// Definit le niveau de difficulte.
    /// </summary>
    /// <param name="level">Niveau.</param>
    public void SetDifficulty(DifficultyLevel level)
    {
        _difficultySettings.difficulty = level;
        ApplyDifficultyPreset(level);
        SaveDifficultySettings();

        OnDifficultyChanged?.Invoke(level);
        Log($"Difficulty: {level}");
    }

    /// <summary>
    /// Configure la difficulte personnalisee.
    /// </summary>
    /// <param name="settings">Settings.</param>
    public void SetCustomDifficulty(DifficultySettings settings)
    {
        _difficultySettings = settings;
        _difficultySettings.difficulty = DifficultyLevel.Custom;
        SaveDifficultySettings();

        OnDifficultyChanged?.Invoke(DifficultyLevel.Custom);
    }

    /// <summary>
    /// Active/desactive l'assistance de visee.
    /// </summary>
    /// <param name="enabled">Active?</param>
    /// <param name="strength">Force (0-1).</param>
    public void SetAutoAim(bool enabled, float strength = 0.5f)
    {
        _difficultySettings.autoAimEnabled = enabled;
        _difficultySettings.autoAimStrength = Mathf.Clamp01(strength);
        SaveDifficultySettings();
        Log($"Auto-aim: {enabled} (strength: {strength:P0})");
    }

    /// <summary>
    /// Obtient le multiplicateur de degats joueur.
    /// </summary>
    /// <returns>Multiplicateur.</returns>
    public float GetPlayerDamageMultiplier()
    {
        return _difficultySettings.playerDamageMultiplier;
    }

    /// <summary>
    /// Obtient le multiplicateur de degats ennemi.
    /// </summary>
    /// <returns>Multiplicateur.</returns>
    public float GetEnemyDamageMultiplier()
    {
        return _difficultySettings.enemyDamageMultiplier;
    }

    /// <summary>
    /// Obtient le multiplicateur d'XP.
    /// </summary>
    /// <returns>Multiplicateur.</returns>
    public float GetXPMultiplier()
    {
        return _difficultySettings.xpMultiplier;
    }

    #endregion

    #region Public Methods - Screen Reader

    /// <summary>
    /// Active/desactive le support lecteur d'ecran.
    /// </summary>
    /// <param name="enabled">Active?</param>
    public void SetScreenReaderSupport(bool enabled)
    {
        _settings.screenReaderEnabled = enabled;
        SaveSettings();
        Log($"Screen reader support: {enabled}");
    }

    /// <summary>
    /// Annonce un texte au lecteur d'ecran.
    /// </summary>
    /// <param name="text">Texte a annoncer.</param>
    /// <param name="priority">Priorite haute?</param>
    public void AnnounceToScreenReader(string text, bool priority = false)
    {
        if (!_settings.screenReaderEnabled) return;

        // NOTE: En production, utiliser l'API native du systeme
        // Windows: UIAutomation, macOS: VoiceOver
        Log($"Screen reader: {text}");
    }

    #endregion

    #region Private Methods - Colorblind

    private void ApplyColorblindMode(ColorblindMode mode)
    {
        if (_colorblindShaderMaterial == null) return;

        bool enabled = mode != ColorblindMode.None;
        // TODO: Activer/configurer le shader post-process

        if (enabled)
        {
            var matrix = GetColorblindMatrix(mode);
            // Passer la matrice au shader
        }
    }

    private Matrix4x4 GetColorblindMatrix(ColorblindMode mode)
    {
        // Matrices de simulation daltonienne (Brettel 1997)
        return mode switch
        {
            ColorblindMode.Protanopia => new Matrix4x4(
                new Vector4(0.567f, 0.433f, 0f, 0f),
                new Vector4(0.558f, 0.442f, 0f, 0f),
                new Vector4(0f, 0.242f, 0.758f, 0f),
                new Vector4(0f, 0f, 0f, 1f)),

            ColorblindMode.Deuteranopia => new Matrix4x4(
                new Vector4(0.625f, 0.375f, 0f, 0f),
                new Vector4(0.7f, 0.3f, 0f, 0f),
                new Vector4(0f, 0.3f, 0.7f, 0f),
                new Vector4(0f, 0f, 0f, 1f)),

            ColorblindMode.Tritanopia => new Matrix4x4(
                new Vector4(0.95f, 0.05f, 0f, 0f),
                new Vector4(0f, 0.433f, 0.567f, 0f),
                new Vector4(0f, 0.475f, 0.525f, 0f),
                new Vector4(0f, 0f, 0f, 1f)),

            ColorblindMode.Achromatopsia => new Matrix4x4(
                new Vector4(0.299f, 0.587f, 0.114f, 0f),
                new Vector4(0.299f, 0.587f, 0.114f, 0f),
                new Vector4(0.299f, 0.587f, 0.114f, 0f),
                new Vector4(0f, 0f, 0f, 1f)),

            _ => Matrix4x4.identity
        };
    }

    private Color ApplyColorMatrix(Color color, Matrix4x4 matrix)
    {
        Vector4 v = new Vector4(color.r, color.g, color.b, color.a);
        Vector4 result = matrix * v;
        return new Color(result.x, result.y, result.z, result.w);
    }

    #endregion

    #region Private Methods - Subtitles

    private void UpdateSubtitles()
    {
        if (_currentSubtitle == null) return;

        if (Time.time - _currentSubtitle.timestamp > _currentSubtitle.duration)
        {
            _currentSubtitle = null;
            DisplayNextSubtitle();
        }
    }

    private void DisplayNextSubtitle()
    {
        if (_subtitleQueue == null || _subtitleQueue.Count == 0)
        {
            _currentSubtitle = null;
            return;
        }

        _currentSubtitle = _subtitleQueue.Dequeue();
        _currentSubtitle.timestamp = Time.time;

        OnSubtitleDisplayed?.Invoke(_currentSubtitle);
    }

    #endregion

    #region Private Methods - UI Scale

    private void ApplyTextScale(float multiplier)
    {
        // TODO: Notifier tous les composants texte
        // Utiliser un event ou un ScriptableObject partage
    }

    private void ApplyUIScale(float scale)
    {
        // TODO: Ajuster le CanvasScaler de tous les Canvas
    }

    #endregion

    #region Private Methods - Difficulty

    private void ApplyDifficultyPreset(DifficultyLevel level)
    {
        switch (level)
        {
            case DifficultyLevel.Story:
                _difficultySettings.playerDamageMultiplier = 2f;
                _difficultySettings.enemyDamageMultiplier = 0.5f;
                _difficultySettings.xpMultiplier = 1.5f;
                _difficultySettings.lootMultiplier = 1.25f;
                _difficultySettings.autoAimEnabled = true;
                _difficultySettings.autoAimStrength = 0.8f;
                break;

            case DifficultyLevel.Easy:
                _difficultySettings.playerDamageMultiplier = 1.5f;
                _difficultySettings.enemyDamageMultiplier = 0.75f;
                _difficultySettings.xpMultiplier = 1.25f;
                _difficultySettings.lootMultiplier = 1.1f;
                _difficultySettings.autoAimEnabled = true;
                _difficultySettings.autoAimStrength = 0.5f;
                break;

            case DifficultyLevel.Normal:
                _difficultySettings.playerDamageMultiplier = 1f;
                _difficultySettings.enemyDamageMultiplier = 1f;
                _difficultySettings.xpMultiplier = 1f;
                _difficultySettings.lootMultiplier = 1f;
                _difficultySettings.autoAimEnabled = false;
                _difficultySettings.autoAimStrength = 0f;
                break;

            case DifficultyLevel.Hard:
                _difficultySettings.playerDamageMultiplier = 0.8f;
                _difficultySettings.enemyDamageMultiplier = 1.25f;
                _difficultySettings.xpMultiplier = 1.1f;
                _difficultySettings.lootMultiplier = 1.15f;
                _difficultySettings.autoAimEnabled = false;
                _difficultySettings.autoAimStrength = 0f;
                break;

            case DifficultyLevel.Nightmare:
                _difficultySettings.playerDamageMultiplier = 0.6f;
                _difficultySettings.enemyDamageMultiplier = 1.5f;
                _difficultySettings.xpMultiplier = 1.25f;
                _difficultySettings.lootMultiplier = 1.3f;
                _difficultySettings.autoAimEnabled = false;
                _difficultySettings.autoAimStrength = 0f;
                break;
        }
    }

    #endregion

    #region Private Methods - Input

    private InputBindingData GetOrCreateBinding(string actionName)
    {
        if (_inputBindings == null) _inputBindings = new List<InputBindingData>();

        foreach (var binding in _inputBindings)
        {
            if (binding.actionName == actionName) return binding;
        }

        var newBinding = new InputBindingData { actionName = actionName };
        _inputBindings.Add(newBinding);
        return newBinding;
    }

    private List<InputBindingData> GetDefaultBindings()
    {
        return new List<InputBindingData>
        {
            new InputBindingData { actionName = "Attack", keyboardKey = "Mouse0", controllerButton = "ButtonWest" },
            new InputBindingData { actionName = "Skill", keyboardKey = "E", controllerButton = "ButtonNorth" },
            new InputBindingData { actionName = "Dodge", keyboardKey = "Space", controllerButton = "ButtonEast" },
            new InputBindingData { actionName = "Jump", keyboardKey = "Space", controllerButton = "ButtonSouth" },
            new InputBindingData { actionName = "Interact", keyboardKey = "F", controllerButton = "ButtonNorth" },
            new InputBindingData { actionName = "Inventory", keyboardKey = "I", controllerButton = "Start" },
            new InputBindingData { actionName = "Map", keyboardKey = "M", controllerButton = "Select" },
            new InputBindingData { actionName = "Sprint", keyboardKey = "LeftShift", controllerButton = "LeftStickButton" }
        };
    }

    #endregion

    #region Private Methods - Persistence

    private void LoadAllSettings()
    {
        // Accessibilite generale
        _settings.colorblindMode = (ColorblindMode)PlayerPrefs.GetInt("Access_ColorblindMode", 0);
        _settings.highContrast = PlayerPrefs.GetInt("Access_HighContrast", 0) == 1;
        _settings.screenReaderEnabled = PlayerPrefs.GetInt("Access_ScreenReader", 0) == 1;

        // Sous-titres
        _subtitleSettings.enabled = PlayerPrefs.GetInt("Subtitle_Enabled", 1) == 1;
        _subtitleSettings.fontSize = (SubtitleSize)PlayerPrefs.GetInt("Subtitle_Size", 1);
        _subtitleSettings.showBackground = PlayerPrefs.GetInt("Subtitle_Background", 1) == 1;
        _subtitleSettings.backgroundOpacity = PlayerPrefs.GetFloat("Subtitle_BgOpacity", 0.7f);
        _subtitleSettings.showSpeakerName = PlayerPrefs.GetInt("Subtitle_Speaker", 1) == 1;

        // UI Scale
        _uiScaleSettings.textScale = (TextScaleLevel)PlayerPrefs.GetInt("UI_TextScale", 1);
        _uiScaleSettings.uiScale = PlayerPrefs.GetFloat("UI_Scale", 1f);

        // Difficulte
        _difficultySettings.difficulty = (DifficultyLevel)PlayerPrefs.GetInt("Diff_Level", 2);
        ApplyDifficultyPreset(_difficultySettings.difficulty);

        // Bindings
        _inputBindings = GetDefaultBindings(); // TODO: Charger depuis JSON

        // Appliquer
        ApplyColorblindMode(_settings.colorblindMode);
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetInt("Access_ColorblindMode", (int)_settings.colorblindMode);
        PlayerPrefs.SetInt("Access_HighContrast", _settings.highContrast ? 1 : 0);
        PlayerPrefs.SetInt("Access_ScreenReader", _settings.screenReaderEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void SaveSubtitleSettings()
    {
        PlayerPrefs.SetInt("Subtitle_Enabled", _subtitleSettings.enabled ? 1 : 0);
        PlayerPrefs.SetInt("Subtitle_Size", (int)_subtitleSettings.fontSize);
        PlayerPrefs.SetInt("Subtitle_Background", _subtitleSettings.showBackground ? 1 : 0);
        PlayerPrefs.SetFloat("Subtitle_BgOpacity", _subtitleSettings.backgroundOpacity);
        PlayerPrefs.SetInt("Subtitle_Speaker", _subtitleSettings.showSpeakerName ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void SaveUISettings()
    {
        PlayerPrefs.SetInt("UI_TextScale", (int)_uiScaleSettings.textScale);
        PlayerPrefs.SetFloat("UI_Scale", _uiScaleSettings.uiScale);
        PlayerPrefs.Save();
    }

    private void SaveDifficultySettings()
    {
        PlayerPrefs.SetInt("Diff_Level", (int)_difficultySettings.difficulty);
        PlayerPrefs.Save();
    }

    private void SaveInputBindings()
    {
        // TODO: Sauvegarder en JSON
    }

    private void Log(string message)
    {
        if (_debugMode)
        {
            Debug.Log($"[Accessibility] {message}");
        }
    }

    #endregion
}

/// <summary>
/// Modes daltoniens.
/// </summary>
public enum ColorblindMode
{
    /// <summary>Pas de filtre.</summary>
    None,

    /// <summary>Protanopie (rouge faible).</summary>
    Protanopia,

    /// <summary>Deuteranopie (vert faible).</summary>
    Deuteranopia,

    /// <summary>Tritanopie (bleu faible).</summary>
    Tritanopia,

    /// <summary>Achromatopsie (noir et blanc).</summary>
    Achromatopsia
}

/// <summary>
/// Preset de filtres daltoniens.
/// </summary>
[System.Serializable]
public class ColorblindPreset
{
    public ColorblindMode mode;
    public string displayName;
    public Texture2D lutTexture;
}

/// <summary>
/// Settings d'accessibilite generaux.
/// </summary>
[System.Serializable]
public class AccessibilitySettings
{
    public ColorblindMode colorblindMode = ColorblindMode.None;
    public bool highContrast = false;
    public bool reducedMotion = false;
    public bool screenReaderEnabled = false;
    public bool largePointer = false;
}

/// <summary>
/// Tailles de sous-titres.
/// </summary>
public enum SubtitleSize
{
    Small,
    Normal,
    Large,
    ExtraLarge
}

/// <summary>
/// Settings de sous-titres.
/// </summary>
[System.Serializable]
public class SubtitleSettings
{
    public bool enabled = true;
    public SubtitleSize fontSize = SubtitleSize.Normal;
    public bool showBackground = true;
    [Range(0f, 1f)] public float backgroundOpacity = 0.7f;
    public bool showSpeakerName = true;
    public bool speakerNameColor = true;
    public bool soundDescriptions = false;
}

/// <summary>
/// Entree de sous-titre.
/// </summary>
[System.Serializable]
public class SubtitleEntry
{
    public string text;
    public string speaker;
    public float duration;
    public float timestamp;
    public Color speakerColor;
}

/// <summary>
/// Niveaux de scale texte.
/// </summary>
public enum TextScaleLevel
{
    Small,
    Normal,
    Large,
    ExtraLarge
}

/// <summary>
/// Settings d'echelle UI.
/// </summary>
[System.Serializable]
public class UIScaleSettings
{
    public TextScaleLevel textScale = TextScaleLevel.Normal;
    [Range(0.75f, 1.5f)] public float uiScale = 1f;
    public bool compactMode = false;
}

/// <summary>
/// Binding d'input.
/// </summary>
[System.Serializable]
public class InputBindingData
{
    public string actionName;
    public string keyboardKey;
    public string controllerButton;
    public bool canRebind = true;
}

/// <summary>
/// Niveaux de difficulte.
/// </summary>
public enum DifficultyLevel
{
    /// <summary>Mode histoire (tres facile).</summary>
    Story,

    /// <summary>Facile.</summary>
    Easy,

    /// <summary>Normal.</summary>
    Normal,

    /// <summary>Difficile.</summary>
    Hard,

    /// <summary>Cauchemar.</summary>
    Nightmare,

    /// <summary>Personnalise.</summary>
    Custom
}

/// <summary>
/// Settings de difficulte.
/// </summary>
[System.Serializable]
public class DifficultySettings
{
    public DifficultyLevel difficulty = DifficultyLevel.Normal;

    [Header("Multiplicateurs de degats")]
    [Range(0.5f, 3f)] public float playerDamageMultiplier = 1f;
    [Range(0.5f, 2f)] public float enemyDamageMultiplier = 1f;

    [Header("Multiplicateurs de recompenses")]
    [Range(0.5f, 2f)] public float xpMultiplier = 1f;
    [Range(0.5f, 2f)] public float lootMultiplier = 1f;

    [Header("Assistance")]
    public bool autoAimEnabled = false;
    [Range(0f, 1f)] public float autoAimStrength = 0f;
    public bool invincibilityFrames = true;
    public float iFrameDuration = 0.5f;
}
