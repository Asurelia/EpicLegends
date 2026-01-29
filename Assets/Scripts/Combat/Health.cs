using System;
using UnityEngine;

/// <summary>
/// Generic health component for any entity.
/// Use this for enemies, NPCs, destructible objects.
/// For players, use PlayerStats instead.
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private bool _destroyOnDeath = true;
    [SerializeField] private float _destroyDelay = 0f;

    [Header("Resistances (0 = normal, 1 = immune, -1 = double damage)")]
    [SerializeField] private float _physicalResistance = 0f;
    [SerializeField] private float _magicalResistance = 0f;
    [SerializeField] private float _fireResistance = 0f;
    [SerializeField] private float _iceResistance = 0f;
    [SerializeField] private float _lightningResistance = 0f;
    [SerializeField] private float _poisonResistance = 0f;

    private float _currentHealth;

    // Events
    public event Action<float, float> OnHealthChanged;  // current, max
    public event Action<float, DamageType> OnDamaged;   // amount, type
    public event Action OnDeath;

    #region Unity Callbacks

    private void Awake()
    {
        _currentHealth = _maxHealth;
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    #endregion

    #region IDamageable Implementation

    public void TakeDamage(float amount, DamageType damageType = DamageType.Physical)
    {
        if (IsDead) return;

        // Calculate final damage after resistance
        float resistance = GetResistance(damageType);
        float finalDamage = amount * (1f - Mathf.Clamp(resistance, -1f, 1f));

        // True damage ignores resistances
        if (damageType == DamageType.True)
        {
            finalDamage = amount;
        }

        _currentHealth = Mathf.Max(0, _currentHealth - finalDamage);

        // Notify listeners
        OnDamaged?.Invoke(finalDamage, damageType);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    public bool IsDead => _currentHealth <= 0;

    #endregion

    #region Public Methods

    /// <summary>
    /// Heal this entity.
    /// </summary>
    public void Heal(float amount)
    {
        if (IsDead) return;

        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + Mathf.Abs(amount));
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    /// <summary>
    /// Set health to a specific value.
    /// </summary>
    public void SetHealth(float value)
    {
        _currentHealth = Mathf.Clamp(value, 0, _maxHealth);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Reset health to max.
    /// </summary>
    public void ResetHealth()
    {
        _currentHealth = _maxHealth;
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    #endregion

    #region Private Methods

    private float GetResistance(DamageType type)
    {
        return type switch
        {
            DamageType.Physical => _physicalResistance,
            DamageType.Magical => _magicalResistance,
            DamageType.Fire => _fireResistance,
            DamageType.Ice => _iceResistance,
            DamageType.Lightning => _lightningResistance,
            DamageType.Poison => _poisonResistance,
            DamageType.True => 0f,
            _ => 0f
        };
    }

    private void Die()
    {
        OnDeath?.Invoke();

        if (_destroyOnDeath)
        {
            if (_destroyDelay > 0)
            {
                Destroy(gameObject, _destroyDelay);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    #endregion

    #region Public Properties

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;
    public float HealthPercent => _currentHealth / _maxHealth;

    #endregion
}
