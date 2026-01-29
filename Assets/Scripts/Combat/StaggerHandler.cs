using System;
using UnityEngine;

/// <summary>
/// Gere le systeme de poise et stagger.
/// La poise determine la resistance au stagger.
/// </summary>
public class StaggerHandler : MonoBehaviour
{
    #region Serialized Fields

    [Header("Poise")]
    [SerializeField] private float _maxPoise = 100f;
    [SerializeField] private float _poiseRegenRate = 10f;
    [SerializeField] private float _poiseRegenDelay = 2f;

    [Header("Stagger")]
    [SerializeField] private float _staggerDuration = 1f;
    [SerializeField] private bool _immuneDuringStagger = true;

    #endregion

    #region Private Fields

    private float _currentPoise;
    private bool _isStaggered;
    private float _staggerTimer;
    private float _poiseRegenTimer;

    #endregion

    #region Events

    /// <summary>
    /// Declenche quand la poise est brisee.
    /// </summary>
    public event Action OnStaggered;

    /// <summary>
    /// Declenche quand le stagger se termine.
    /// </summary>
    public event Action OnStaggerRecovered;

    /// <summary>
    /// Declenche quand la poise change.
    /// </summary>
    public event Action<float, float> OnPoiseChanged;

    #endregion

    #region Properties

    /// <summary>
    /// Poise maximale.
    /// </summary>
    public float MaxPoise
    {
        get => _maxPoise;
        set
        {
            _maxPoise = Mathf.Max(1f, value);
            _currentPoise = Mathf.Min(_currentPoise, _maxPoise);
        }
    }

    /// <summary>
    /// Poise actuelle.
    /// </summary>
    public float CurrentPoise => _currentPoise;

    /// <summary>
    /// Taux de regeneration de la poise par seconde.
    /// </summary>
    public float PoiseRegenRate
    {
        get => _poiseRegenRate;
        set => _poiseRegenRate = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Est-ce que l'entite est en stagger?
    /// </summary>
    public bool IsStaggered => _isStaggered;

    /// <summary>
    /// Ratio de poise restante (0-1).
    /// </summary>
    public float PoiseRatio => _maxPoise > 0 ? _currentPoise / _maxPoise : 0f;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        ResetPoise();
    }

    private void Update()
    {
        if (_isStaggered)
        {
            UpdateStagger();
        }
        else
        {
            UpdatePoiseRegen();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Remet la poise au maximum.
    /// </summary>
    public void ResetPoise()
    {
        float oldPoise = _currentPoise;
        _currentPoise = _maxPoise;
        _isStaggered = false;
        _staggerTimer = 0f;
        _poiseRegenTimer = 0f;

        if (oldPoise != _currentPoise)
        {
            OnPoiseChanged?.Invoke(_currentPoise, _maxPoise);
        }
    }

    /// <summary>
    /// Applique des degats de stagger.
    /// </summary>
    public void ApplyStagger(float staggerValue)
    {
        if (_isStaggered && _immuneDuringStagger) return;
        if (staggerValue <= 0) return;

        float oldPoise = _currentPoise;
        _currentPoise = Mathf.Max(0f, _currentPoise - staggerValue);
        _poiseRegenTimer = _poiseRegenDelay;

        OnPoiseChanged?.Invoke(_currentPoise, _maxPoise);

        if (_currentPoise <= 0f && !_isStaggered)
        {
            TriggerStagger();
        }
    }

    /// <summary>
    /// Regenere la poise manuellement.
    /// </summary>
    public void RegeneratePoise(float deltaTime)
    {
        if (_isStaggered) return;

        float oldPoise = _currentPoise;
        _currentPoise = Mathf.Min(_maxPoise, _currentPoise + _poiseRegenRate * deltaTime);

        if (oldPoise != _currentPoise)
        {
            OnPoiseChanged?.Invoke(_currentPoise, _maxPoise);
        }
    }

    /// <summary>
    /// Force un stagger immediatement.
    /// </summary>
    public void ForceStagger()
    {
        _currentPoise = 0f;
        TriggerStagger();
    }

    /// <summary>
    /// Termine le stagger immediatement.
    /// </summary>
    public void EndStagger()
    {
        if (!_isStaggered) return;

        _isStaggered = false;
        _staggerTimer = 0f;
        _currentPoise = _maxPoise * 0.5f; // Recupere 50% de poise
        OnStaggerRecovered?.Invoke();
    }

    #endregion

    #region Private Methods

    private void TriggerStagger()
    {
        _isStaggered = true;
        _staggerTimer = _staggerDuration;
        OnStaggered?.Invoke();
    }

    private void UpdateStagger()
    {
        _staggerTimer -= Time.deltaTime;
        if (_staggerTimer <= 0f)
        {
            EndStagger();
        }
    }

    private void UpdatePoiseRegen()
    {
        if (_currentPoise >= _maxPoise) return;

        _poiseRegenTimer -= Time.deltaTime;
        if (_poiseRegenTimer <= 0f)
        {
            RegeneratePoise(Time.deltaTime);
        }
    }

    #endregion
}
