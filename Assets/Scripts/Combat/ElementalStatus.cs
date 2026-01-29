using System;
using UnityEngine;

/// <summary>
/// Composant qui gere le statut elementaire d'une entite.
/// Suit l'element applique et sa jauge (gauge).
/// </summary>
public class ElementalStatus : MonoBehaviour
{
    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private float _maxGauge = 1f;
    [SerializeField] private float _gaugeDecayRate = 0.1f;

    #endregion

    #region Private Fields

    private ElementType _currentElement;
    private float _currentGauge;
    private bool _hasElement;

    #endregion

    #region Events

    /// <summary>
    /// Declenche quand un element est applique.
    /// </summary>
    public event Action<ElementType, float> OnElementApplied;

    /// <summary>
    /// Declenche quand l'element est consomme (par reaction).
    /// </summary>
    public event Action<ElementType> OnElementConsumed;

    /// <summary>
    /// Declenche quand l'element expire naturellement.
    /// </summary>
    public event Action OnElementExpired;

    #endregion

    #region Properties

    /// <summary>
    /// Element actuel.
    /// </summary>
    public ElementType CurrentElement => _currentElement;

    /// <summary>
    /// Jauge elementaire actuelle.
    /// </summary>
    public float CurrentGauge => _currentGauge;

    /// <summary>
    /// Jauge maximale.
    /// </summary>
    public float MaxGauge => _maxGauge;

    /// <summary>
    /// Ratio de jauge (0-1).
    /// </summary>
    public float GaugeRatio => _maxGauge > 0 ? _currentGauge / _maxGauge : 0f;

    /// <summary>
    /// A-t-on un element applique?
    /// </summary>
    public bool HasElement => _hasElement && _currentGauge > 0f;

    #endregion

    #region Unity Callbacks

    private void Update()
    {
        if (_hasElement && _gaugeDecayRate > 0f)
        {
            UpdateGaugeDecay();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Applique un element avec une certaine quantite de jauge.
    /// </summary>
    /// <param name="element">Element a appliquer</param>
    /// <param name="gaugeAmount">Quantite de jauge (0-1)</param>
    public void ApplyElement(ElementType element, float gaugeAmount)
    {
        if (gaugeAmount <= 0f) return;

        if (_hasElement && _currentElement == element)
        {
            // Meme element - ajouter a la jauge
            _currentGauge = Mathf.Min(_maxGauge, _currentGauge + gaugeAmount);
        }
        else
        {
            // Nouvel element
            _currentElement = element;
            _currentGauge = Mathf.Min(_maxGauge, gaugeAmount);
            _hasElement = true;
        }

        OnElementApplied?.Invoke(element, _currentGauge);
    }

    /// <summary>
    /// Consomme la jauge elementaire (lors d'une reaction).
    /// </summary>
    /// <param name="amount">Quantite a consommer</param>
    /// <returns>True si l'element est entierement consomme</returns>
    public bool ConsumeGauge(float amount)
    {
        if (!_hasElement) return false;

        ElementType consumed = _currentElement;
        _currentGauge -= amount;

        if (_currentGauge <= 0f)
        {
            _currentGauge = 0f;
            _hasElement = false;
            OnElementConsumed?.Invoke(consumed);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Efface l'element actuel.
    /// </summary>
    public void ClearElement()
    {
        if (!_hasElement) return;

        ElementType consumed = _currentElement;
        _currentGauge = 0f;
        _hasElement = false;
        OnElementConsumed?.Invoke(consumed);
    }

    /// <summary>
    /// Force un element specifique.
    /// </summary>
    public void SetElement(ElementType element, float gauge)
    {
        _currentElement = element;
        _currentGauge = Mathf.Clamp(gauge, 0f, _maxGauge);
        _hasElement = _currentGauge > 0f;

        if (_hasElement)
        {
            OnElementApplied?.Invoke(element, _currentGauge);
        }
    }

    #endregion

    #region Private Methods

    private void UpdateGaugeDecay()
    {
        _currentGauge -= _gaugeDecayRate * Time.deltaTime;

        if (_currentGauge <= 0f)
        {
            _currentGauge = 0f;
            _hasElement = false;
            OnElementExpired?.Invoke();
        }
    }

    #endregion
}
