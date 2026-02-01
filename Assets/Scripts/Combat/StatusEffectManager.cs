using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire des effets de statut sur une entite.
/// Attacher a chaque entite pouvant recevoir des buffs/debuffs.
/// </summary>
public class StatusEffectManager : MonoBehaviour
{
    #region Events

    public event Action<StatusEffectInstance> OnEffectApplied;
    public event Action<StatusEffectInstance> OnEffectRemoved;
    public event Action<StatusEffectInstance> OnEffectRefreshed;
    public event Action<StatusEffectInstance> OnEffectStacked;

    #endregion

    #region Private Fields

    private List<StatusEffectInstance> _activeEffects = new List<StatusEffectInstance>();
    private Health _health;
    private PlayerStats _playerStats;
    private AudioSource _audioSource;

    // Modificateurs de stats actuels
    private float _attackModifier = 0f;
    private float _defenseModifier = 0f;
    private float _speedModifier = 0f;
    private float _critRateModifier = 0f;
    private float _critDamageModifier = 0f;

    #endregion

    #region Properties

    public IReadOnlyList<StatusEffectInstance> ActiveEffects => _activeEffects.AsReadOnly();
    public int BuffCount => _activeEffects.FindAll(e => e.Data.category == StatusEffectCategory.Buff).Count;
    public int DebuffCount => _activeEffects.FindAll(e => e.Data.category == StatusEffectCategory.Debuff).Count;

    // Modificateurs accessibles
    public float AttackModifier => _attackModifier;
    public float DefenseModifier => _defenseModifier;
    public float SpeedModifier => _speedModifier;
    public float CritRateModifier => _critRateModifier;
    public float CritDamageModifier => _critDamageModifier;

    public bool IsStunned => HasEffect(StatusEffectType.Stun) || HasEffect(StatusEffectType.Freeze);
    public bool IsSilenced => HasEffect(StatusEffectType.Silence);
    public bool IsInvincible => HasEffect(StatusEffectType.Invincibility);

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _health = GetComponent<Health>();
        _playerStats = GetComponent<PlayerStats>();
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Update()
    {
        UpdateEffects(Time.deltaTime);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Applique un effet de statut.
    /// </summary>
    public StatusEffectInstance ApplyEffect(StatusEffectData effectData, GameObject source = null)
    {
        if (effectData == null) return null;

        // Verifier si immunise (invincibilite contre debuffs)
        if (effectData.category == StatusEffectCategory.Debuff && IsInvincible)
        {
            Debug.Log($"[StatusEffectManager] {gameObject.name} est invincible, debuff ignore");
            return null;
        }

        // Chercher un effet existant du meme type
        var existing = FindEffect(effectData.effectType);

        if (existing != null)
        {
            // Effet deja present
            if (effectData.canStack && existing.CurrentStacks < effectData.maxStacks)
            {
                // Ajouter un stack
                existing.AddStack();
                existing.Refresh();
                RecalculateModifiers();
                OnEffectStacked?.Invoke(existing);
                PlaySound(effectData.applySound);
                return existing;
            }
            else if (effectData.canRefresh)
            {
                // Rafraichir la duree
                existing.Refresh();
                OnEffectRefreshed?.Invoke(existing);
                return existing;
            }
            else
            {
                // Effet non stackable et non rafraichissable
                return null;
            }
        }

        // Creer un nouvel effet
        var newEffect = new StatusEffectInstance(effectData, source);
        newEffect.OnTick += HandleEffectTick;
        newEffect.OnExpired += HandleEffectExpired;
        newEffect.AttachVFX(transform);

        _activeEffects.Add(newEffect);
        RecalculateModifiers();

        PlaySound(effectData.applySound);
        OnEffectApplied?.Invoke(newEffect);

        Debug.Log($"[StatusEffectManager] {effectData.effectName} applique sur {gameObject.name}");

        return newEffect;
    }

    /// <summary>
    /// Retire un effet specifique.
    /// </summary>
    public bool RemoveEffect(StatusEffectType type)
    {
        var effect = FindEffect(type);
        if (effect == null) return false;

        return RemoveEffectInstance(effect);
    }

    /// <summary>
    /// Retire tous les buffs.
    /// </summary>
    public void RemoveAllBuffs()
    {
        var buffs = _activeEffects.FindAll(e => e.Data.category == StatusEffectCategory.Buff);
        foreach (var buff in buffs)
        {
            RemoveEffectInstance(buff);
        }
    }

    /// <summary>
    /// Retire tous les debuffs.
    /// </summary>
    public void RemoveAllDebuffs()
    {
        var debuffs = _activeEffects.FindAll(e => e.Data.category == StatusEffectCategory.Debuff);
        foreach (var debuff in debuffs)
        {
            RemoveEffectInstance(debuff);
        }
    }

    /// <summary>
    /// Retire tous les effets.
    /// </summary>
    public void RemoveAllEffects()
    {
        var allEffects = new List<StatusEffectInstance>(_activeEffects);
        foreach (var effect in allEffects)
        {
            RemoveEffectInstance(effect);
        }
    }

    /// <summary>
    /// Verifie si un type d'effet est actif.
    /// </summary>
    public bool HasEffect(StatusEffectType type)
    {
        return FindEffect(type) != null;
    }

    /// <summary>
    /// Trouve un effet par son type.
    /// </summary>
    public StatusEffectInstance FindEffect(StatusEffectType type)
    {
        return _activeEffects.Find(e => e.Data.effectType == type);
    }

    /// <summary>
    /// Obtient le nombre de stacks d'un effet.
    /// </summary>
    public int GetEffectStacks(StatusEffectType type)
    {
        var effect = FindEffect(type);
        return effect?.CurrentStacks ?? 0;
    }

    /// <summary>
    /// Calcule les degats modifies par les effets.
    /// </summary>
    public float ModifyOutgoingDamage(float baseDamage)
    {
        float modified = baseDamage * (1f + _attackModifier);
        return modified;
    }

    /// <summary>
    /// Calcule les degats recus modifies par les effets.
    /// </summary>
    public float ModifyIncomingDamage(float baseDamage)
    {
        if (IsInvincible) return 0f;

        // Shield absorbe les degats
        var shield = FindEffect(StatusEffectType.Shield);
        if (shield != null)
        {
            // Utiliser la methode AbsorbDamage pour reduire la valeur du bouclier
            baseDamage = shield.AbsorbDamage(baseDamage);

            // Retirer le bouclier si epuise
            if (shield.IsShieldDepleted)
            {
                RemoveEffect(StatusEffectType.Shield);
            }

            // Si tout a ete absorbe, retourner 0
            if (baseDamage <= 0f)
            {
                return 0f;
            }
        }

        float modified = baseDamage * (1f - _defenseModifier);
        return Mathf.Max(0f, modified);
    }

    #endregion

    #region Private Methods

    private void UpdateEffects(float deltaTime)
    {
        // Copier la liste pour eviter les modifications pendant l'iteration
        var effectsCopy = new List<StatusEffectInstance>(_activeEffects);

        foreach (var effect in effectsCopy)
        {
            effect.Update(deltaTime);
        }
    }

    private void HandleEffectTick(StatusEffectInstance effect)
    {
        switch (effect.Data.effectType)
        {
            case StatusEffectType.Regeneration:
                ApplyHealing(effect.GetTotalTickValue());
                break;

            case StatusEffectType.Poison:
            case StatusEffectType.Burn:
            case StatusEffectType.Bleed:
                ApplyDamageOverTime(effect.GetTotalTickValue(), effect.Data.effectType);
                break;
        }

        PlaySound(effect.Data.tickSound);
    }

    private void HandleEffectExpired(StatusEffectInstance effect)
    {
        RemoveEffectInstance(effect);
    }

    private bool RemoveEffectInstance(StatusEffectInstance effect)
    {
        if (!_activeEffects.Contains(effect)) return false;

        effect.OnTick -= HandleEffectTick;
        effect.OnExpired -= HandleEffectExpired;
        effect.DestroyVFX();

        _activeEffects.Remove(effect);
        RecalculateModifiers();

        PlaySound(effect.Data.removeSound);
        OnEffectRemoved?.Invoke(effect);

        Debug.Log($"[StatusEffectManager] {effect.Data.effectName} retire de {gameObject.name}");

        return true;
    }

    private void RecalculateModifiers()
    {
        // Reset tous les modificateurs
        _attackModifier = 0f;
        _defenseModifier = 0f;
        _speedModifier = 0f;
        _critRateModifier = 0f;
        _critDamageModifier = 0f;

        foreach (var effect in _activeEffects)
        {
            float value = effect.GetTotalValue();

            switch (effect.Data.effectType)
            {
                case StatusEffectType.AttackUp:
                    _attackModifier += value;
                    break;
                case StatusEffectType.AttackDown:
                    _attackModifier -= value;
                    break;
                case StatusEffectType.DefenseUp:
                    _defenseModifier += value;
                    break;
                case StatusEffectType.DefenseDown:
                    _defenseModifier -= value;
                    break;
                case StatusEffectType.SpeedUp:
                    _speedModifier += value;
                    break;
                case StatusEffectType.SpeedDown:
                case StatusEffectType.Slow:
                    _speedModifier -= value;
                    break;
                case StatusEffectType.CritRateUp:
                    _critRateModifier += value;
                    break;
                case StatusEffectType.CritDamageUp:
                    _critDamageModifier += value;
                    break;
            }
        }
    }

    private void ApplyHealing(float amount)
    {
        if (_health != null)
        {
            _health.Heal(amount);
        }
        else if (_playerStats != null)
        {
            _playerStats.Heal(amount);
        }
    }

    private void ApplyDamageOverTime(float amount, StatusEffectType sourceType)
    {
        if (_health != null)
        {
            DamageType damageType = sourceType switch
            {
                StatusEffectType.Burn => DamageType.Fire,
                StatusEffectType.Poison => DamageType.Dark, // Poison utilise le type Dark
                _ => DamageType.Physical
            };

            var damageInfo = new DamageInfo(amount, damageType);
            _health.TakeDamage(damageInfo);
        }
        else if (_playerStats != null)
        {
            _playerStats.TakeDamage(amount);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip, 0.5f);
        }
    }

    #endregion
}
