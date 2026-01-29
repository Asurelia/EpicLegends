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

    #endregion
}
