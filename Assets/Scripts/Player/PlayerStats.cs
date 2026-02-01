using System;
using UnityEngine;

/// <summary>
/// Manages player stats: Health, Mana, Stamina.
/// Provides events for UI updates and game logic.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _healthRegenRate = 0f;

    [Header("Mana")]
    [SerializeField] private float _maxMana = 50f;
    [SerializeField] private float _manaRegenRate = 2f;

    [Header("Stamina")]
    [SerializeField] private float _maxStamina = 100f;
    [SerializeField] private float _staminaRegenRate = 15f;
    [SerializeField] private float _staminaDrainRate = 20f;

    [Header("Attributes")]
    [SerializeField] private int _strength = 10;
    [SerializeField] private int _agility = 10;
    [SerializeField] private int _intelligence = 10;

    [Header("Combat")]
    [SerializeField] private float _critChance = 0.05f;
    [SerializeField] private float _critDamage = 1.5f;

    [Header("Progression")]
    [SerializeField] private int _level = 1;
    [SerializeField] private int _experience = 0;
    [SerializeField] private int _gold = 0;

    // Current values
    private float _currentHealth;
    private float _currentMana;
    private float _currentStamina;

    // Events for UI and other systems
    public event Action<float, float> OnHealthChanged;  // current, max
    public event Action<float, float> OnManaChanged;    // current, max
    public event Action<float, float> OnStaminaChanged; // current, max
    public event Action OnDeath;

    // Reference to player controller for stamina drain
    private PlayerController _playerController;

    #region Unity Callbacks

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();

        // Initialize to max values
        _currentHealth = _maxHealth;
        _currentMana = _maxMana;
        _currentStamina = _maxStamina;
    }

    private void Start()
    {
        // Notify UI of initial values
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        OnManaChanged?.Invoke(_currentMana, _maxMana);
        OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
    }

    private void Update()
    {
        HandleRegeneration();
        HandleStaminaDrain();
    }

    #endregion

    #region Regeneration

    private void HandleRegeneration()
    {
        // Health regen (if enabled)
        if (_healthRegenRate > 0 && _currentHealth < _maxHealth)
        {
            ModifyHealth(_healthRegenRate * Time.deltaTime);
        }

        // Mana regen
        if (_currentMana < _maxMana)
        {
            ModifyMana(_manaRegenRate * Time.deltaTime);
        }

        // Stamina regen (only when not sprinting)
        bool isSprinting = _playerController != null && _playerController.IsSprinting;
        if (!isSprinting && _currentStamina < _maxStamina)
        {
            ModifyStamina(_staminaRegenRate * Time.deltaTime);
        }
    }

    private void HandleStaminaDrain()
    {
        if (_playerController == null) return;

        if (_playerController.IsSprinting && _currentStamina > 0)
        {
            ModifyStamina(-_staminaDrainRate * Time.deltaTime);
        }
    }

    #endregion

    #region Public Methods - Health

    /// <summary>
    /// Apply damage to the player.
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        ModifyHealth(-Mathf.Abs(amount));

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Heal the player.
    /// </summary>
    public void Heal(float amount)
    {
        if (IsDead) return;

        ModifyHealth(Mathf.Abs(amount));
    }

    private void ModifyHealth(float amount)
    {
        _currentHealth = Mathf.Clamp(_currentHealth + amount, 0, _maxHealth);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    private void Die()
    {
        Debug.Log("Player died!");
        OnDeath?.Invoke();
    }

    #endregion

    #region Public Methods - Mana

    /// <summary>
    /// Try to use mana for an ability.
    /// </summary>
    /// <returns>True if enough mana was available.</returns>
    public bool TryUseMana(float amount)
    {
        if (_currentMana < amount) return false;

        ModifyMana(-amount);
        return true;
    }

    /// <summary>
    /// Restore mana.
    /// </summary>
    public void RestoreMana(float amount)
    {
        ModifyMana(Mathf.Abs(amount));
    }

    private void ModifyMana(float amount)
    {
        _currentMana = Mathf.Clamp(_currentMana + amount, 0, _maxMana);
        OnManaChanged?.Invoke(_currentMana, _maxMana);
    }

    #endregion

    #region Public Methods - Stamina

    /// <summary>
    /// Try to use stamina for an action.
    /// </summary>
    /// <returns>True if enough stamina was available.</returns>
    public bool TryUseStamina(float amount)
    {
        if (_currentStamina < amount) return false;

        ModifyStamina(-amount);
        return true;
    }

    /// <summary>
    /// Check if player has enough stamina.
    /// </summary>
    public bool HasStamina(float amount)
    {
        return _currentStamina >= amount;
    }

    private void ModifyStamina(float amount)
    {
        _currentStamina = Mathf.Clamp(_currentStamina + amount, 0, _maxStamina);
        OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
    }

    #endregion

    #region Public Properties

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;
    public float HealthPercent => _currentHealth / _maxHealth;

    public float CurrentMana => _currentMana;
    public float MaxMana => _maxMana;
    public float ManaPercent => _currentMana / _maxMana;

    public float CurrentStamina => _currentStamina;
    public float MaxStamina => _maxStamina;
    public float StaminaPercent => _currentStamina / _maxStamina;

    public bool IsDead => _currentHealth <= 0;

    // Attributes
    public int Strength => _strength;
    public int Agility => _agility;
    public int Intelligence => _intelligence;

    // Combat
    public float CritChance => _critChance;
    public float CritDamage => _critDamage;

    // Progression
    public int Level => _level;
    public int Experience => _experience;
    public int Gold => _gold;

    #endregion

    #region Public Methods - Progression

    /// <summary>
    /// Ajoute de l'experience et gere le level up.
    /// </summary>
    public void AddExperience(int amount)
    {
        if (amount <= 0) return;

        _experience += amount;

        // Verifier level up
        int xpForNextLevel = GetXPForLevel(_level + 1);
        while (_experience >= xpForNextLevel && _level < 100)
        {
            _experience -= xpForNextLevel;
            LevelUp();
            xpForNextLevel = GetXPForLevel(_level + 1);
        }

        OnExperienceChanged?.Invoke(_experience, xpForNextLevel);
    }

    /// <summary>
    /// Ajoute de l'or.
    /// </summary>
    public void AddGold(int amount)
    {
        _gold += amount;
        OnGoldChanged?.Invoke(_gold);
    }

    /// <summary>
    /// Retire de l'or.
    /// </summary>
    public bool SpendGold(int amount)
    {
        if (_gold < amount) return false;
        _gold -= amount;
        OnGoldChanged?.Invoke(_gold);
        return true;
    }

    private void LevelUp()
    {
        _level++;
        // Augmenter les stats max
        _maxHealth += 10f;
        _maxMana += 5f;
        _maxStamina += 5f;
        // Soigner completement
        _currentHealth = _maxHealth;
        _currentMana = _maxMana;
        _currentStamina = _maxStamina;

        OnLevelUp?.Invoke(_level);
        Debug.Log($"[PlayerStats] Level Up! Now level {_level}");
    }

    private int GetXPForLevel(int level)
    {
        // Formule: XP = 100 * level^2.2
        return Mathf.RoundToInt(100f * Mathf.Pow(level, 2.2f));
    }

    // Events pour progression
    public event Action<int, int> OnExperienceChanged; // current, required
    public event Action<int> OnGoldChanged;
    public event Action<int> OnLevelUp;

    #endregion
}
