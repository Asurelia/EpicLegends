using System;
using UnityEngine;

/// <summary>
/// Types d'effets de statut.
/// </summary>
public enum StatusEffectType
{
    // Buffs
    AttackUp,
    DefenseUp,
    SpeedUp,
    CritRateUp,
    CritDamageUp,
    Regeneration,
    Shield,
    Invincibility,

    // Debuffs
    AttackDown,
    DefenseDown,
    SpeedDown,
    Poison,
    Burn,
    Freeze,
    Stun,
    Slow,
    Blind,
    Silence,
    Bleed,
    Curse
}

/// <summary>
/// Categorie d'effet (buff ou debuff).
/// </summary>
public enum StatusEffectCategory
{
    Buff,
    Debuff
}

/// <summary>
/// Donnees d'un effet de statut.
/// </summary>
[CreateAssetMenu(fileName = "NewStatusEffect", menuName = "EpicLegends/Status Effect")]
public class StatusEffectData : ScriptableObject
{
    [Header("Identification")]
    public string effectId;
    public string effectName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;

    [Header("Type")]
    public StatusEffectType effectType;
    public StatusEffectCategory category;

    [Header("Duration")]
    public float baseDuration = 5f;
    public bool isPermanent = false;
    public bool canRefresh = true;
    public bool canStack = false;
    public int maxStacks = 1;

    [Header("Effect Values")]
    [Tooltip("Valeur principale de l'effet (% ou flat selon le type)")]
    public float value = 0.2f;
    [Tooltip("Valeur par tick pour DOT/HOT")]
    public float tickValue = 5f;
    [Tooltip("Intervalle entre les ticks")]
    public float tickInterval = 1f;

    [Header("Visuals")]
    public GameObject vfxPrefab;
    public Color effectColor = Color.white;

    [Header("Audio")]
    public AudioClip applySound;
    public AudioClip tickSound;
    public AudioClip removeSound;
}

/// <summary>
/// Instance active d'un effet de statut sur une entite.
/// </summary>
[Serializable]
public class StatusEffectInstance
{
    public StatusEffectData Data { get; private set; }
    public float RemainingDuration { get; private set; }
    public int CurrentStacks { get; private set; }
    public GameObject Source { get; private set; }

    private float _tickTimer;
    private GameObject _vfxInstance;

    public bool IsExpired => !Data.isPermanent && RemainingDuration <= 0;

    // Valeur courante du bouclier (pour StatusEffectType.Shield)
    private float _currentShieldValue;
    public float CurrentShieldValue => _currentShieldValue;

    public event Action<StatusEffectInstance> OnTick;
    public event Action<StatusEffectInstance> OnExpired;
    public event Action<StatusEffectInstance> OnStackChanged;

    public StatusEffectInstance(StatusEffectData data, GameObject source)
    {
        Data = data;
        Source = source;
        RemainingDuration = data.baseDuration;
        CurrentStacks = 1;
        _tickTimer = 0f;

        // Initialiser la valeur du bouclier si applicable
        if (data.effectType == StatusEffectType.Shield)
        {
            _currentShieldValue = data.value;
        }
    }

    /// <summary>
    /// Met a jour l'effet chaque frame.
    /// </summary>
    public void Update(float deltaTime)
    {
        if (Data.isPermanent) return;

        RemainingDuration -= deltaTime;

        // Gerer les ticks (DOT/HOT)
        if (Data.tickInterval > 0)
        {
            _tickTimer += deltaTime;
            if (_tickTimer >= Data.tickInterval)
            {
                _tickTimer -= Data.tickInterval;
                OnTick?.Invoke(this);
            }
        }

        if (RemainingDuration <= 0)
        {
            OnExpired?.Invoke(this);
        }
    }

    /// <summary>
    /// Rafraichit la duree de l'effet.
    /// </summary>
    public void Refresh()
    {
        if (Data.canRefresh)
        {
            RemainingDuration = Data.baseDuration;
        }
    }

    /// <summary>
    /// Ajoute un stack.
    /// </summary>
    public bool AddStack()
    {
        if (!Data.canStack || CurrentStacks >= Data.maxStacks)
            return false;

        CurrentStacks++;
        OnStackChanged?.Invoke(this);
        return true;
    }

    /// <summary>
    /// Retire un stack.
    /// </summary>
    public bool RemoveStack()
    {
        if (CurrentStacks <= 1) return false;

        CurrentStacks--;
        OnStackChanged?.Invoke(this);
        return true;
    }

    /// <summary>
    /// Calcule la valeur totale de l'effet avec les stacks.
    /// </summary>
    public float GetTotalValue()
    {
        return Data.value * CurrentStacks;
    }

    /// <summary>
    /// Calcule la valeur de tick totale avec les stacks.
    /// </summary>
    public float GetTotalTickValue()
    {
        return Data.tickValue * CurrentStacks;
    }

    /// <summary>
    /// Reduit la valeur du bouclier et retourne les degats restants.
    /// </summary>
    /// <param name="damage">Degats a absorber</param>
    /// <returns>Degats non absorbes</returns>
    public float AbsorbDamage(float damage)
    {
        if (Data.effectType != StatusEffectType.Shield) return damage;

        if (damage <= _currentShieldValue)
        {
            _currentShieldValue -= damage;
            return 0f;
        }
        else
        {
            float remaining = damage - _currentShieldValue;
            _currentShieldValue = 0f;
            return remaining;
        }
    }

    /// <summary>
    /// Verifie si le bouclier est epuise.
    /// </summary>
    public bool IsShieldDepleted => Data.effectType == StatusEffectType.Shield && _currentShieldValue <= 0f;

    /// <summary>
    /// Attache le VFX a une cible.
    /// </summary>
    public void AttachVFX(Transform target)
    {
        if (Data.vfxPrefab != null && _vfxInstance == null)
        {
            _vfxInstance = UnityEngine.Object.Instantiate(Data.vfxPrefab, target);
            _vfxInstance.transform.localPosition = Vector3.zero;
        }
    }

    /// <summary>
    /// Detruit le VFX.
    /// </summary>
    public void DestroyVFX()
    {
        if (_vfxInstance != null)
        {
            UnityEngine.Object.Destroy(_vfxInstance);
            _vfxInstance = null;
        }
    }
}
