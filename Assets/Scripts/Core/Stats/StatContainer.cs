using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Conteneur de statistiques pour un personnage ou entité.
/// Gère les valeurs de base, les modificateurs et calcule les valeurs finales.
/// </summary>
public class StatContainer : MonoBehaviour
{
    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private int _level = 1;

    [Header("Stats de Base (modifiables par niveau)")]
    [SerializeField] private float _baseStrength = 10f;
    [SerializeField] private float _baseDexterity = 10f;
    [SerializeField] private float _baseIntelligence = 10f;
    [SerializeField] private float _baseVitality = 10f;
    [SerializeField] private float _baseWisdom = 10f;
    [SerializeField] private float _baseLuck = 10f;

    [Header("Formules de Stats Dérivées")]
    [SerializeField] private float _healthPerVitality = 10f;
    [SerializeField] private float _manaPerWisdom = 5f;
    [SerializeField] private float _attackPerStrength = 2f;
    [SerializeField] private float _defensePerVitality = 1f;
    [SerializeField] private float _magicAttackPerIntelligence = 2f;
    [SerializeField] private float _magicDefensePerWisdom = 1f;
    [SerializeField] private float _critRatePerLuck = 0.5f;
    [SerializeField] private float _critRatePerDexterity = 0.25f;

    #endregion

    #region Private Fields

    private Dictionary<StatType, float> _baseValues = new Dictionary<StatType, float>();
    private Dictionary<StatType, List<StatModifier>> _modifiers = new Dictionary<StatType, List<StatModifier>>();
    private Dictionary<StatType, float> _cachedFinalValues = new Dictionary<StatType, float>();
    private Dictionary<StatType, bool> _isDirty = new Dictionary<StatType, bool>();

    #endregion

    #region Events

    /// <summary>
    /// Déclenché quand une stat change.
    /// </summary>
    public event Action<StatType, float, float> OnStatChanged; // type, oldValue, newValue

    /// <summary>
    /// Déclenché quand le niveau change.
    /// </summary>
    public event Action<int, int> OnLevelChanged; // oldLevel, newLevel

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        InitializeStats();
    }

    #endregion

    #region Initialization

    private void InitializeStats()
    {
        // Initialiser toutes les stats
        foreach (StatType statType in Enum.GetValues(typeof(StatType)))
        {
            _baseValues[statType] = 0f;
            _modifiers[statType] = new List<StatModifier>();
            _cachedFinalValues[statType] = 0f;
            _isDirty[statType] = true;
        }

        // Définir les valeurs de base
        SetBaseValue(StatType.Strength, _baseStrength);
        SetBaseValue(StatType.Dexterity, _baseDexterity);
        SetBaseValue(StatType.Intelligence, _baseIntelligence);
        SetBaseValue(StatType.Vitality, _baseVitality);
        SetBaseValue(StatType.Wisdom, _baseWisdom);
        SetBaseValue(StatType.Luck, _baseLuck);

        // Recalculer toutes les stats dérivées
        RecalculateAllDerivedStats();
    }

    #endregion

    #region Public Methods - Base Values

    /// <summary>
    /// Obtient la valeur de base d'une stat (sans modificateurs).
    /// </summary>
    public float GetBaseValue(StatType statType)
    {
        return _baseValues.TryGetValue(statType, out float value) ? value : 0f;
    }

    /// <summary>
    /// Définit la valeur de base d'une stat.
    /// </summary>
    public void SetBaseValue(StatType statType, float value)
    {
        if (!_baseValues.ContainsKey(statType))
        {
            _baseValues[statType] = 0f;
        }

        float oldValue = GetFinalValue(statType);
        _baseValues[statType] = value;
        MarkDirty(statType);

        float newValue = GetFinalValue(statType);
        if (!Mathf.Approximately(oldValue, newValue))
        {
            OnStatChanged?.Invoke(statType, oldValue, newValue);
        }

        // Recalculer les stats dérivées si c'est une stat de base
        if (IsBaseStat(statType))
        {
            RecalculateAllDerivedStats();
        }
    }

    /// <summary>
    /// Modifie la valeur de base d'une stat.
    /// </summary>
    public void ModifyBaseValue(StatType statType, float delta)
    {
        SetBaseValue(statType, GetBaseValue(statType) + delta);
    }

    #endregion

    #region Public Methods - Modifiers

    /// <summary>
    /// Ajoute un modificateur à une stat.
    /// </summary>
    public void AddModifier(StatType statType, StatModifier modifier)
    {
        if (!_modifiers.ContainsKey(statType))
        {
            _modifiers[statType] = new List<StatModifier>();
        }

        float oldValue = GetFinalValue(statType);
        _modifiers[statType].Add(modifier);
        _modifiers[statType].Sort((a, b) =>
        {
            int typeCompare = a.Type.CompareTo(b.Type);
            return typeCompare != 0 ? typeCompare : a.Order.CompareTo(b.Order);
        });

        MarkDirty(statType);

        float newValue = GetFinalValue(statType);
        if (!Mathf.Approximately(oldValue, newValue))
        {
            OnStatChanged?.Invoke(statType, oldValue, newValue);
        }
    }

    /// <summary>
    /// Retire un modificateur spécifique.
    /// </summary>
    public bool RemoveModifier(StatType statType, StatModifier modifier)
    {
        if (!_modifiers.TryGetValue(statType, out var list))
        {
            return false;
        }

        float oldValue = GetFinalValue(statType);
        bool removed = list.Remove(modifier);

        if (removed)
        {
            MarkDirty(statType);
            float newValue = GetFinalValue(statType);
            if (!Mathf.Approximately(oldValue, newValue))
            {
                OnStatChanged?.Invoke(statType, oldValue, newValue);
            }
        }

        return removed;
    }

    /// <summary>
    /// Retire tous les modificateurs d'une source donnée.
    /// </summary>
    public void RemoveAllModifiersFromSource(object source)
    {
        foreach (var statType in _modifiers.Keys.ToList())
        {
            float oldValue = GetFinalValue(statType);
            int removed = _modifiers[statType].RemoveAll(m => m.Source == source);

            if (removed > 0)
            {
                MarkDirty(statType);
                float newValue = GetFinalValue(statType);
                if (!Mathf.Approximately(oldValue, newValue))
                {
                    OnStatChanged?.Invoke(statType, oldValue, newValue);
                }
            }
        }
    }

    /// <summary>
    /// Retire tous les modificateurs d'une stat.
    /// </summary>
    public void ClearModifiers(StatType statType)
    {
        if (!_modifiers.TryGetValue(statType, out var list) || list.Count == 0)
        {
            return;
        }

        float oldValue = GetFinalValue(statType);
        list.Clear();
        MarkDirty(statType);

        float newValue = GetFinalValue(statType);
        if (!Mathf.Approximately(oldValue, newValue))
        {
            OnStatChanged?.Invoke(statType, oldValue, newValue);
        }
    }

    /// <summary>
    /// Obtient tous les modificateurs d'une stat.
    /// </summary>
    public IReadOnlyList<StatModifier> GetModifiers(StatType statType)
    {
        return _modifiers.TryGetValue(statType, out var list)
            ? list.AsReadOnly()
            : Array.Empty<StatModifier>();
    }

    #endregion

    #region Public Methods - Final Values

    /// <summary>
    /// Obtient la valeur finale d'une stat (base + modificateurs).
    /// </summary>
    public float GetFinalValue(StatType statType)
    {
        if (_isDirty.TryGetValue(statType, out bool dirty) && !dirty)
        {
            return _cachedFinalValues[statType];
        }

        float finalValue = CalculateFinalValue(statType);
        _cachedFinalValues[statType] = finalValue;
        _isDirty[statType] = false;

        return finalValue;
    }

    /// <summary>
    /// Alias pour GetFinalValue avec indexer.
    /// </summary>
    public float this[StatType statType] => GetFinalValue(statType);

    #endregion

    #region Public Methods - Level

    /// <summary>
    /// Niveau actuel du personnage.
    /// </summary>
    public int Level
    {
        get => _level;
        set
        {
            if (value == _level) return;
            int oldLevel = _level;
            _level = Mathf.Max(1, value);
            OnLevelChanged?.Invoke(oldLevel, _level);
            RecalculateAllDerivedStats();
        }
    }

    #endregion

    #region Private Methods

    private float CalculateFinalValue(StatType statType)
    {
        float baseValue = _baseValues.TryGetValue(statType, out float bv) ? bv : 0f;
        if (!_modifiers.TryGetValue(statType, out var modifiers) || modifiers.Count == 0)
        {
            return baseValue;
        }

        float finalValue = baseValue;
        float sumPercentAdd = 0f;

        foreach (var mod in modifiers)
        {
            switch (mod.Type)
            {
                case ModifierType.Flat:
                    finalValue += mod.Value;
                    break;

                case ModifierType.PercentAdd:
                    sumPercentAdd += mod.Value;
                    break;

                case ModifierType.PercentMult:
                    // Appliquer les PercentAdd accumulés avant le premier PercentMult
                    if (sumPercentAdd != 0f)
                    {
                        finalValue *= 1f + sumPercentAdd;
                        sumPercentAdd = 0f;
                    }
                    finalValue *= 1f + mod.Value;
                    break;
            }
        }

        // Appliquer les PercentAdd restants
        if (sumPercentAdd != 0f)
        {
            finalValue *= 1f + sumPercentAdd;
        }

        return finalValue;
    }

    private void RecalculateAllDerivedStats()
    {
        // Calculer les stats dérivées basées sur les stats de base
        float strength = GetFinalValue(StatType.Strength);
        float dexterity = GetFinalValue(StatType.Dexterity);
        float intelligence = GetFinalValue(StatType.Intelligence);
        float vitality = GetFinalValue(StatType.Vitality);
        float wisdom = GetFinalValue(StatType.Wisdom);
        float luck = GetFinalValue(StatType.Luck);

        // Points de vie
        SetBaseValue(StatType.MaxHealth, 100f + vitality * _healthPerVitality);

        // Mana
        SetBaseValue(StatType.MaxMana, 50f + wisdom * _manaPerWisdom);

        // Stamina (basée sur la vitalité et la dextérité)
        SetBaseValue(StatType.MaxStamina, 100f + (vitality + dexterity) * 2f);

        // Attaque physique
        SetBaseValue(StatType.Attack, strength * _attackPerStrength);

        // Défense physique
        SetBaseValue(StatType.Defense, vitality * _defensePerVitality);

        // Attaque magique
        SetBaseValue(StatType.MagicAttack, intelligence * _magicAttackPerIntelligence);

        // Défense magique
        SetBaseValue(StatType.MagicDefense, wisdom * _magicDefensePerWisdom);

        // Taux de critique (capped à 100%)
        float critRate = luck * _critRatePerLuck + dexterity * _critRatePerDexterity;
        SetBaseValue(StatType.CritRate, Mathf.Min(critRate / 100f, 1f));

        // Dégâts critiques (150% de base + bonus de luck)
        SetBaseValue(StatType.CritDamage, 1.5f + luck * 0.01f);

        // Vitesse de déplacement
        SetBaseValue(StatType.Speed, 1f + dexterity * 0.005f);

        // Vitesse d'attaque
        SetBaseValue(StatType.AttackSpeed, 1f + dexterity * 0.01f);

        // Précision et esquive
        SetBaseValue(StatType.Accuracy, 80f + dexterity * 0.5f + luck * 0.25f);
        SetBaseValue(StatType.Evasion, dexterity * 0.3f + luck * 0.2f);

        // Régénérations
        SetBaseValue(StatType.HealthRegen, vitality * 0.1f);
        SetBaseValue(StatType.ManaRegen, wisdom * 0.2f);
        SetBaseValue(StatType.StaminaRegen, 15f + dexterity * 0.5f);
    }

    private bool IsBaseStat(StatType statType)
    {
        return statType switch
        {
            StatType.Strength => true,
            StatType.Dexterity => true,
            StatType.Intelligence => true,
            StatType.Vitality => true,
            StatType.Wisdom => true,
            StatType.Luck => true,
            _ => false
        };
    }

    private void MarkDirty(StatType statType)
    {
        _isDirty[statType] = true;
    }

    #endregion

    #region Debug

    /// <summary>
    /// Affiche toutes les stats dans la console (debug).
    /// </summary>
    [ContextMenu("Log All Stats")]
    public void LogAllStats()
    {
        Debug.Log($"=== Stats for {gameObject.name} (Level {_level}) ===");
        foreach (StatType statType in Enum.GetValues(typeof(StatType)))
        {
            float baseValue = GetBaseValue(statType);
            float finalValue = GetFinalValue(statType);
            if (finalValue != 0 || baseValue != 0)
            {
                string modInfo = Mathf.Approximately(baseValue, finalValue)
                    ? ""
                    : $" (base: {baseValue:F1})";
                Debug.Log($"{statType}: {finalValue:F1}{modInfo}");
            }
        }
    }

    #endregion
}
