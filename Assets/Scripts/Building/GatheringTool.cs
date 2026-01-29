using System;
using UnityEngine;

/// <summary>
/// Outil de collecte de ressources.
/// </summary>
public class GatheringTool : MonoBehaviour
{
    #region Fields

    [Header("Configuration")]
    [SerializeField] private ToolType _toolType = ToolType.Axe;
    [SerializeField] private float _gatherSpeed = 1f;
    [SerializeField] private float _gatherRange = 2f;
    [SerializeField] private int _bonusAmount = 0;
    [SerializeField] private float _bonusChance = 0f;

    [Header("Durabilite")]
    [SerializeField] private bool _hasDurability = true;
    [SerializeField] private int _maxDurability = 100;
    [SerializeField] private int _durabilityPerUse = 1;

    [Header("Animation")]
    [SerializeField] private string _gatherAnimTrigger = "Gather";
    [SerializeField] private float _gatherCooldown = 0.5f;

    // Etat
    private int _currentDurability;
    private float _cooldownTimer = 0f;
    private ResourceSource _targetSource;
    private bool _isGathering = false;
    private float _gatherProgress = 0f;

    #endregion

    #region Events

    public event Action<ResourceType, int> OnResourceGathered;
    public event Action<int, int> OnDurabilityChanged;
    public event Action OnToolBroken;

    #endregion

    #region Properties

    /// <summary>Type d'outil.</summary>
    public ToolType ToolType => _toolType;

    /// <summary>Vitesse de collecte.</summary>
    public float GatherSpeed => _gatherSpeed;

    /// <summary>Portee de collecte.</summary>
    public float GatherRange => _gatherRange;

    /// <summary>Durabilite actuelle.</summary>
    public int CurrentDurability => _currentDurability;

    /// <summary>Durabilite maximum.</summary>
    public int MaxDurability => _maxDurability;

    /// <summary>Pourcentage de durabilite.</summary>
    public float DurabilityPercent => _maxDurability > 0 ? (float)_currentDurability / _maxDurability : 1f;

    /// <summary>Est casse?</summary>
    public bool IsBroken => _hasDurability && _currentDurability <= 0;

    /// <summary>En train de collecter?</summary>
    public bool IsGathering => _isGathering;

    /// <summary>Progression (0-1).</summary>
    public float GatherProgress => _gatherProgress;

    /// <summary>Pret a utiliser?</summary>
    public bool CanUse => !IsBroken && _cooldownTimer <= 0f;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _currentDurability = _maxDurability;
    }

    private void Update()
    {
        // Cooldown
        if (_cooldownTimer > 0)
        {
            _cooldownTimer -= Time.deltaTime;
        }

        // Progression de collecte
        if (_isGathering && _targetSource != null)
        {
            UpdateGathering();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Demarre la collecte sur une source.
    /// </summary>
    public bool StartGathering(ResourceSource source)
    {
        if (!CanUse) return false;
        if (source == null || source.IsDepleted) return false;
        if (source.ResourceData == null) return false;
        if (!source.ResourceData.CanGatherWith(_toolType)) return false;

        // Verifier la distance
        float dist = Vector3.Distance(transform.position, source.transform.position);
        if (dist > _gatherRange) return false;

        _targetSource = source;
        _isGathering = true;
        _gatherProgress = 0f;

        return true;
    }

    /// <summary>
    /// Annule la collecte.
    /// </summary>
    public void CancelGathering()
    {
        _isGathering = false;
        _targetSource = null;
        _gatherProgress = 0f;
    }

    /// <summary>
    /// Collecte en un coup (pour test ou outils speciaux).
    /// </summary>
    public bool InstantGather(ResourceSource source)
    {
        if (!CanUse) return false;
        if (source == null || source.IsDepleted) return false;
        if (source.ResourceData == null) return false;
        if (!source.ResourceData.CanGatherWith(_toolType)) return false;

        if (source.TryGather(_toolType, out var type, out var amount))
        {
            // Bonus
            amount = ApplyBonuses(amount);

            // Durabilite
            UseDurability();

            // Cooldown
            _cooldownTimer = _gatherCooldown;

            OnResourceGathered?.Invoke(type, amount);

            // Ajouter au ResourceManager
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.AddResource(type, amount);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Repare l'outil.
    /// </summary>
    public void Repair(int amount)
    {
        if (!_hasDurability) return;

        int oldDurability = _currentDurability;
        _currentDurability = Mathf.Min(_maxDurability, _currentDurability + amount);

        if (_currentDurability != oldDurability)
        {
            OnDurabilityChanged?.Invoke(_currentDurability, _maxDurability);
        }
    }

    /// <summary>
    /// Repare completement l'outil.
    /// </summary>
    public void FullRepair()
    {
        Repair(_maxDurability);
    }

    #endregion

    #region Private Methods

    private void UpdateGathering()
    {
        if (_targetSource == null || _targetSource.IsDepleted)
        {
            CancelGathering();
            return;
        }

        float gatherTime = _targetSource.ResourceData.gatherTime / _gatherSpeed;
        _gatherProgress += Time.deltaTime / gatherTime;

        if (_gatherProgress >= 1f)
        {
            CompleteGathering();
        }
    }

    private void CompleteGathering()
    {
        if (_targetSource != null && _targetSource.TryGather(_toolType, out var type, out var amount))
        {
            // Bonus
            amount = ApplyBonuses(amount);

            // Durabilite
            UseDurability();

            OnResourceGathered?.Invoke(type, amount);

            // Ajouter au ResourceManager
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.AddResource(type, amount);
            }
        }

        // Continuer a collecter si la source n'est pas epuisee
        if (_targetSource != null && !_targetSource.IsDepleted)
        {
            _gatherProgress = 0f;
        }
        else
        {
            CancelGathering();
        }
    }

    private int ApplyBonuses(int baseAmount)
    {
        int final = baseAmount + _bonusAmount;

        // Chance de bonus
        if (_bonusChance > 0 && UnityEngine.Random.value < _bonusChance)
        {
            final += 1;
        }

        return final;
    }

    private void UseDurability()
    {
        if (!_hasDurability) return;

        int oldDurability = _currentDurability;
        _currentDurability = Mathf.Max(0, _currentDurability - _durabilityPerUse);

        if (_currentDurability != oldDurability)
        {
            OnDurabilityChanged?.Invoke(_currentDurability, _maxDurability);
        }

        if (_currentDurability <= 0)
        {
            OnToolBroken?.Invoke();
        }
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Configure l'outil.
    /// </summary>
    public void Configure(ToolType type, float speed, float range, int durability)
    {
        _toolType = type;
        _gatherSpeed = speed;
        _gatherRange = range;
        _maxDurability = durability;
        _currentDurability = durability;
    }

    #endregion
}
