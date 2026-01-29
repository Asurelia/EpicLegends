using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barre de progression UI reutil  isable.
/// </summary>
public class UIProgressBar : MonoBehaviour
{
    #region Serialized Fields

    [SerializeField] private Image _fillImage;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private TextMeshProUGUI _valueText;

    [Header("Settings")]
    [SerializeField] private bool _showText = true;
    [SerializeField] private string _textFormat = "{0:0}/{1:0}";
    [SerializeField] private bool _smoothFill = true;
    [SerializeField] private float _fillSpeed = 5f;

    [Header("Colors")]
    [SerializeField] private Gradient _fillGradient;
    [SerializeField] private bool _useGradient = false;

    #endregion

    #region Private Fields

    private float _currentProgress = 0f;
    private float _targetProgress = 0f;
    private float _currentValue;
    private float _maxValue = 100f;

    #endregion

    #region Events

    /// <summary>
    /// Declenche quand la progression change.
    /// </summary>
    public event Action<float> OnProgressChanged;

    /// <summary>
    /// Declenche quand la barre atteint 0.
    /// </summary>
    public event Action OnEmpty;

    /// <summary>
    /// Declenche quand la barre atteint 100%.
    /// </summary>
    public event Action OnFull;

    #endregion

    #region Properties

    /// <summary>
    /// Progression actuelle (0-1).
    /// </summary>
    public float Progress => _currentProgress;

    /// <summary>
    /// Valeur actuelle.
    /// </summary>
    public float CurrentValue => _currentValue;

    /// <summary>
    /// Valeur maximale.
    /// </summary>
    public float MaxValue => _maxValue;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        // Initialiser le gradient par defaut si non configure
        if (_fillGradient == null || _fillGradient.colorKeys.Length == 0)
        {
            _fillGradient = new Gradient();
            _fillGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.red, 0f),
                    new GradientColorKey(Color.yellow, 0.5f),
                    new GradientColorKey(Color.green, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            );
        }
    }

    private void Update()
    {
        if (_smoothFill && Mathf.Abs(_currentProgress - _targetProgress) > 0.001f)
        {
            _currentProgress = Mathf.Lerp(_currentProgress, _targetProgress, Time.deltaTime * _fillSpeed);
            UpdateVisuals();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Definit la progression directement (0-1).
    /// </summary>
    public void SetProgress(float progress, bool immediate = false)
    {
        float previousProgress = _currentProgress;
        _targetProgress = Mathf.Clamp01(progress);

        if (immediate || !_smoothFill)
        {
            _currentProgress = _targetProgress;
        }

        _currentValue = _targetProgress * _maxValue;
        UpdateVisuals();

        if (Mathf.Abs(previousProgress - _targetProgress) > 0.001f)
        {
            OnProgressChanged?.Invoke(_targetProgress);

            if (_targetProgress <= 0f && previousProgress > 0f)
                OnEmpty?.Invoke();
            else if (_targetProgress >= 1f && previousProgress < 1f)
                OnFull?.Invoke();
        }
    }

    /// <summary>
    /// Definit la valeur avec min/max.
    /// </summary>
    public void SetValue(float current, float max, bool immediate = false)
    {
        _currentValue = current;
        _maxValue = max;

        float progress = max > 0 ? current / max : 0f;
        SetProgress(progress, immediate);
    }

    /// <summary>
    /// Ajoute a la progression.
    /// </summary>
    public void AddProgress(float amount)
    {
        SetProgress(_targetProgress + amount);
    }

    /// <summary>
    /// Ajoute a la valeur.
    /// </summary>
    public void AddValue(float amount)
    {
        SetValue(_currentValue + amount, _maxValue);
    }

    /// <summary>
    /// Remet a zero.
    /// </summary>
    public void Reset()
    {
        SetProgress(0f, true);
    }

    /// <summary>
    /// Remplit completement.
    /// </summary>
    public void Fill()
    {
        SetProgress(1f, true);
    }

    /// <summary>
    /// Change la couleur de remplissage.
    /// </summary>
    public void SetFillColor(Color color)
    {
        if (_fillImage != null)
            _fillImage.color = color;
    }

    /// <summary>
    /// Configure l'affichage du texte.
    /// </summary>
    public void SetTextFormat(string format)
    {
        _textFormat = format;
        UpdateVisuals();
    }

    /// <summary>
    /// Active/desactive l'affichage du texte.
    /// </summary>
    public void ShowText(bool show)
    {
        _showText = show;
        if (_valueText != null)
            _valueText.gameObject.SetActive(show);
    }

    #endregion

    #region Private Methods

    private void UpdateVisuals()
    {
        // Mettre a jour le fill
        if (_fillImage != null)
        {
            _fillImage.fillAmount = _currentProgress;

            if (_useGradient && _fillGradient != null)
            {
                _fillImage.color = _fillGradient.Evaluate(_currentProgress);
            }
        }

        // Mettre a jour le texte
        if (_valueText != null && _showText)
        {
            _valueText.text = string.Format(_textFormat, _currentValue, _maxValue);
        }
    }

    #endregion
}
