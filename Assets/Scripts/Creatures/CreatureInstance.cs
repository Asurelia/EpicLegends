using System;
using UnityEngine;

/// <summary>
/// Instance d'une creature capturee.
/// Represente une creature specifique avec son niveau, experience, etc.
/// </summary>
[System.Serializable]
public class CreatureInstance
{
    #region Fields

    [SerializeField] private CreatureData _data;
    [SerializeField] private string _nickname;
    [SerializeField] private int _level = 1;
    [SerializeField] private int _experience;
    [SerializeField] private float _currentHealth;
    [SerializeField] private float _currentMana;
    [SerializeField] private float _happiness = 50f;

    // Stats individuelles (IV - valeurs individuelles)
    [SerializeField] private float _healthIV;
    [SerializeField] private float _attackIV;
    [SerializeField] private float _defenseIV;
    [SerializeField] private float _speedIV;

    #endregion

    #region Events

    public event Action<int> OnLevelUp;
    public event Action<float, float> OnHealthChanged;
    public event Action<float> OnHappinessChanged;

    #endregion

    #region Properties

    public CreatureData Data => _data;
    public string Nickname => string.IsNullOrEmpty(_nickname) ? _data.creatureName : _nickname;
    public int Level => _level;
    public int Experience => _experience;
    public float Happiness => _happiness;

    public float MaxHealth => _data.GetHealthAtLevel(_level) * (1f + _healthIV / 100f);
    public float MaxMana => _data.GetManaAtLevel(_level);
    public float Attack => _data.GetAttackAtLevel(_level) * (1f + _attackIV / 100f);
    public float Defense => _data.GetDefenseAtLevel(_level) * (1f + _defenseIV / 100f);
    public float Speed => _data.GetSpeedAtLevel(_level) * (1f + _speedIV / 100f);

    public float CurrentHealth => _currentHealth;
    public float CurrentMana => _currentMana;
    public float HealthPercent => MaxHealth > 0 ? _currentHealth / MaxHealth : 0f;
    public float ManaPercent => MaxMana > 0 ? _currentMana / MaxMana : 0f;

    public bool IsFainted => _currentHealth <= 0f;
    public bool CanBattle => !IsFainted && _currentHealth > 0f;

    public int ExperienceToNextLevel => GetExperienceForLevel(_level + 1) - GetExperienceForLevel(_level);
    public int ExperienceInCurrentLevel => _experience - GetExperienceForLevel(_level);
    public float LevelProgress => ExperienceToNextLevel > 0 ? (float)ExperienceInCurrentLevel / ExperienceToNextLevel : 0f;

    #endregion

    #region Constructors

    public CreatureInstance(CreatureData data)
    {
        _data = data;
        _level = 1;
        _experience = 0;

        // Generer des IVs aleatoires (0-31)
        _healthIV = UnityEngine.Random.Range(0f, 31f);
        _attackIV = UnityEngine.Random.Range(0f, 31f);
        _defenseIV = UnityEngine.Random.Range(0f, 31f);
        _speedIV = UnityEngine.Random.Range(0f, 31f);

        // Initialiser la vie et le mana
        _currentHealth = MaxHealth;
        _currentMana = MaxMana;
        _happiness = 50f;
    }

    public CreatureInstance(CreatureData data, int level) : this(data)
    {
        _level = Mathf.Clamp(level, 1, data.maxLevel);
        _experience = GetExperienceForLevel(_level);
        _currentHealth = MaxHealth;
        _currentMana = MaxMana;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Ajoute de l'experience et gere le level up.
    /// </summary>
    public void AddExperience(int amount)
    {
        if (_level >= _data.maxLevel) return;

        _experience += amount;

        // Verifier le level up
        while (_level < _data.maxLevel && _experience >= GetExperienceForLevel(_level + 1))
        {
            LevelUp();
        }
    }

    /// <summary>
    /// Monte de niveau.
    /// </summary>
    private void LevelUp()
    {
        float oldMaxHealth = MaxHealth;
        _level++;

        // Augmenter la vie proportionnellement
        float newMaxHealth = MaxHealth;
        _currentHealth = (_currentHealth / oldMaxHealth) * newMaxHealth;

        OnLevelUp?.Invoke(_level);
    }

    /// <summary>
    /// Soigne la creature.
    /// </summary>
    public void Heal(float amount)
    {
        float oldHealth = _currentHealth;
        _currentHealth = Mathf.Min(MaxHealth, _currentHealth + amount);
        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
    }

    /// <summary>
    /// Soigne completement.
    /// </summary>
    public void FullHeal()
    {
        _currentHealth = MaxHealth;
        _currentMana = MaxMana;
        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
    }

    /// <summary>
    /// Applique des degats.
    /// </summary>
    public void TakeDamage(float amount)
    {
        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
    }

    /// <summary>
    /// Utilise du mana.
    /// </summary>
    public bool UseMana(float amount)
    {
        if (_currentMana < amount) return false;
        _currentMana -= amount;
        return true;
    }

    /// <summary>
    /// Restaure du mana.
    /// </summary>
    public void RestoreMana(float amount)
    {
        _currentMana = Mathf.Min(MaxMana, _currentMana + amount);
    }

    /// <summary>
    /// Change le surnom.
    /// </summary>
    public void SetNickname(string nickname)
    {
        _nickname = nickname;
    }

    /// <summary>
    /// Modifie le bonheur.
    /// </summary>
    public void ModifyHappiness(float amount)
    {
        _happiness = Mathf.Clamp(_happiness + amount, 0f, 100f);
        OnHappinessChanged?.Invoke(_happiness);
    }

    /// <summary>
    /// Obtient les abilities disponibles.
    /// </summary>
    public SkillData[] GetAvailableAbilities()
    {
        return _data.GetAbilitiesAtLevel(_level);
    }

    /// <summary>
    /// Peut utiliser l'ultime?
    /// </summary>
    public bool CanUseUltimate()
    {
        return _level >= _data.ultimateUnlockLevel && _data.ultimateAbility != null;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Calcule l'experience requise pour un niveau.
    /// Formule: niveau^3
    /// </summary>
    private int GetExperienceForLevel(int level)
    {
        return level * level * level;
    }

    #endregion
}
