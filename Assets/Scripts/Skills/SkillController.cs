using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controleur de competences pour un personnage.
/// Gere les slots, cooldowns et l'execution des competences.
/// </summary>
public class SkillController : MonoBehaviour
{
    #region Constants

    public const int MAX_SKILL_SLOTS = 8;
    public const int MAX_PASSIVE_SLOTS = 4;

    #endregion

    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private int _activeSkillSlots = 4;
    [SerializeField] private int _passiveSkillSlots = 2;

    [Header("Stats")]
    [SerializeField] private float _attackStat = 0f;
    [SerializeField] private float _healingStat = 0f;

    #endregion

    #region Private Fields

    private SkillData[] _equippedSkills;
    private SkillData[] _equippedPassives;
    private float[] _cooldownTimers;
    private bool[] _isOnCooldown;

    // Bonus passifs cumules
    private float _totalAttackBonus;
    private float _totalDefenseBonus;
    private float _totalSpeedBonus;
    private float _totalCritBonus;

    #endregion

    #region Events

    /// <summary>
    /// Declenche quand une competence est utilisee.
    /// </summary>
    public event Action<SkillData, int> OnSkillUsed;

    /// <summary>
    /// Declenche quand une competence est equipee.
    /// </summary>
    public event Action<SkillData, int> OnSkillEquipped;

    /// <summary>
    /// Declenche quand un cooldown se termine.
    /// </summary>
    public event Action<int> OnCooldownComplete;

    /// <summary>
    /// Declenche quand les bonus passifs changent.
    /// </summary>
    public event Action OnPassiveBonusesChanged;

    #endregion

    #region Properties

    /// <summary>
    /// Nombre de slots actifs.
    /// </summary>
    public int ActiveSkillSlots => _activeSkillSlots;

    /// <summary>
    /// Bonus d'attaque total des passifs.
    /// </summary>
    public float TotalAttackBonus => _totalAttackBonus;

    /// <summary>
    /// Bonus de defense total des passifs.
    /// </summary>
    public float TotalDefenseBonus => _totalDefenseBonus;

    /// <summary>
    /// Bonus de vitesse total des passifs.
    /// </summary>
    public float TotalSpeedBonus => _totalSpeedBonus;

    /// <summary>
    /// Bonus de critique total des passifs.
    /// </summary>
    public float TotalCritBonus => _totalCritBonus;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _equippedSkills = new SkillData[MAX_SKILL_SLOTS];
        _equippedPassives = new SkillData[MAX_PASSIVE_SLOTS];
        _cooldownTimers = new float[MAX_SKILL_SLOTS];
        _isOnCooldown = new bool[MAX_SKILL_SLOTS];
    }

    private void Update()
    {
        UpdateCooldowns();
    }

    #endregion

    #region Public Methods - Equipment

    /// <summary>
    /// Equipe une competence dans un slot.
    /// </summary>
    public bool EquipSkill(SkillData skill, int slot)
    {
        if (skill == null) return false;
        if (slot < 0 || slot >= _activeSkillSlots) return false;

        // Retirer l'ancienne competence
        if (_equippedSkills[slot] != null)
        {
            UnequipSkill(slot);
        }

        _equippedSkills[slot] = skill;
        _cooldownTimers[slot] = 0f;
        _isOnCooldown[slot] = false;

        OnSkillEquipped?.Invoke(skill, slot);
        return true;
    }

    /// <summary>
    /// Retire une competence d'un slot.
    /// </summary>
    public void UnequipSkill(int slot)
    {
        if (slot < 0 || slot >= MAX_SKILL_SLOTS) return;
        _equippedSkills[slot] = null;
        _cooldownTimers[slot] = 0f;
        _isOnCooldown[slot] = false;
    }

    /// <summary>
    /// Equipe une competence passive.
    /// </summary>
    public bool EquipPassive(SkillData skill, int slot)
    {
        if (skill == null || skill.skillType != SkillType.Passive) return false;
        if (slot < 0 || slot >= _passiveSkillSlots) return false;

        // Retirer l'ancien passif
        if (_equippedPassives[slot] != null)
        {
            UnequipPassive(slot);
        }

        _equippedPassives[slot] = skill;
        RecalculatePassiveBonuses();
        return true;
    }

    /// <summary>
    /// Retire une competence passive.
    /// </summary>
    public void UnequipPassive(int slot)
    {
        if (slot < 0 || slot >= MAX_PASSIVE_SLOTS) return;
        _equippedPassives[slot] = null;
        RecalculatePassiveBonuses();
    }

    /// <summary>
    /// Obtient la competence dans un slot.
    /// </summary>
    public SkillData GetSkillInSlot(int slot)
    {
        if (slot < 0 || slot >= MAX_SKILL_SLOTS) return null;
        return _equippedSkills[slot];
    }

    /// <summary>
    /// Obtient le passif dans un slot.
    /// </summary>
    public SkillData GetPassiveInSlot(int slot)
    {
        if (slot < 0 || slot >= MAX_PASSIVE_SLOTS) return null;
        return _equippedPassives[slot];
    }

    #endregion

    #region Public Methods - Usage

    /// <summary>
    /// Utilise une competence.
    /// </summary>
    public bool UseSkill(int slot)
    {
        if (slot < 0 || slot >= _activeSkillSlots) return false;

        var skill = _equippedSkills[slot];
        if (skill == null) return false;
        if (!IsSkillReady(slot)) return false;

        // Verifier les couts (deleguee au PlayerStats/ResourceManager)
        // Pour l'instant, on suppose que les ressources sont disponibles

        // Demarrer le cooldown
        _cooldownTimers[slot] = skill.GetEffectiveCooldown();
        _isOnCooldown[slot] = true;

        // Executer l'effet de la competence
        ExecuteSkill(skill);

        OnSkillUsed?.Invoke(skill, slot);
        return true;
    }

    /// <summary>
    /// Verifie si une competence est prete.
    /// </summary>
    public bool IsSkillReady(int slot)
    {
        if (slot < 0 || slot >= MAX_SKILL_SLOTS) return false;
        if (_equippedSkills[slot] == null) return false;
        return !_isOnCooldown[slot];
    }

    /// <summary>
    /// Obtient le temps de cooldown restant.
    /// </summary>
    public float GetCooldownRemaining(int slot)
    {
        if (slot < 0 || slot >= MAX_SKILL_SLOTS) return 0f;
        return _cooldownTimers[slot];
    }

    /// <summary>
    /// Obtient le pourcentage de cooldown restant.
    /// </summary>
    public float GetCooldownPercent(int slot)
    {
        if (slot < 0 || slot >= MAX_SKILL_SLOTS) return 0f;
        var skill = _equippedSkills[slot];
        if (skill == null) return 0f;

        float maxCooldown = skill.GetEffectiveCooldown();
        if (maxCooldown <= 0f) return 0f;

        return _cooldownTimers[slot] / maxCooldown;
    }

    /// <summary>
    /// Reinitialise tous les cooldowns.
    /// </summary>
    public void ResetAllCooldowns()
    {
        for (int i = 0; i < MAX_SKILL_SLOTS; i++)
        {
            _cooldownTimers[i] = 0f;
            _isOnCooldown[i] = false;
        }
    }

    /// <summary>
    /// Reduit le cooldown d'une competence.
    /// </summary>
    public void ReduceCooldown(int slot, float amount)
    {
        if (slot < 0 || slot >= MAX_SKILL_SLOTS) return;
        _cooldownTimers[slot] = Mathf.Max(0f, _cooldownTimers[slot] - amount);

        if (_cooldownTimers[slot] <= 0f)
        {
            _isOnCooldown[slot] = false;
            OnCooldownComplete?.Invoke(slot);
        }
    }

    #endregion

    #region Public Methods - Stats

    /// <summary>
    /// Definit la stat d'attaque.
    /// </summary>
    public void SetAttackStat(float value)
    {
        _attackStat = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Definit la stat de soin.
    /// </summary>
    public void SetHealingStat(float value)
    {
        _healingStat = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Obtient les degats calcules d'une competence.
    /// </summary>
    public float GetSkillDamage(int slot)
    {
        var skill = GetSkillInSlot(slot);
        if (skill == null) return 0f;
        return skill.CalculateDamage(_attackStat + _totalAttackBonus);
    }

    #endregion

    #region Private Methods

    private void UpdateCooldowns()
    {
        for (int i = 0; i < MAX_SKILL_SLOTS; i++)
        {
            if (!_isOnCooldown[i]) continue;

            _cooldownTimers[i] -= Time.deltaTime;

            if (_cooldownTimers[i] <= 0f)
            {
                _cooldownTimers[i] = 0f;
                _isOnCooldown[i] = false;
                OnCooldownComplete?.Invoke(i);
            }
        }
    }

    private void RecalculatePassiveBonuses()
    {
        _totalAttackBonus = 0f;
        _totalDefenseBonus = 0f;
        _totalSpeedBonus = 0f;
        _totalCritBonus = 0f;

        foreach (var passive in _equippedPassives)
        {
            if (passive == null) continue;

            _totalAttackBonus += passive.passiveAttackBonus;
            _totalDefenseBonus += passive.passiveDefenseBonus;
            _totalSpeedBonus += passive.passiveSpeedBonus;
            _totalCritBonus += passive.passiveCritBonus;
        }

        OnPassiveBonusesChanged?.Invoke();
    }

    private void ExecuteSkill(SkillData skill)
    {
        // Deleguer l'execution au SkillExecutor
        if (SkillExecutor.Instance != null)
        {
            var playerStats = GetComponent<PlayerStats>();
            SkillExecutor.Instance.Execute(skill, transform, playerStats);
        }
        else
        {
            Debug.LogWarning("[SkillController] SkillExecutor.Instance non trouve!");
        }
    }

    #endregion
}
