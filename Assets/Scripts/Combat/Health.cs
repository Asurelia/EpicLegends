using System;
using UnityEngine;

/// <summary>
/// Composant de sante generique pour toute entite.
/// Utiliser pour les ennemis, PNJ, objets destructibles.
/// Pour le joueur, utiliser PlayerStats.
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _defense = 0f;
    [SerializeField] private bool _destroyOnDeath = true;
    [SerializeField] private float _destroyDelay = 0f;

    [Header("Resistances elementaires (0 = normal, 1 = immune, -1 = double degats)")]
    [SerializeField] private float _physicalResistance = 0f;
    [SerializeField] private float _fireResistance = 0f;
    [SerializeField] private float _waterResistance = 0f;
    [SerializeField] private float _iceResistance = 0f;
    [SerializeField] private float _electricResistance = 0f;
    [SerializeField] private float _windResistance = 0f;
    [SerializeField] private float _earthResistance = 0f;
    [SerializeField] private float _lightResistance = 0f;
    [SerializeField] private float _darkResistance = 0f;

    private float _currentHealth;

    // Events
    public event Action<float, float> OnHealthChanged;  // current, max
    public event Action<DamageInfo> OnDamaged;
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

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (IsDead) return;

        // Calculer les degats effectifs avec critique
        float baseDamage = damageInfo.GetEffectiveDamage();

        // Appliquer la resistance elementaire
        float resistance = GetResistance(damageInfo.damageType);
        float finalDamage = baseDamage * (1f - Mathf.Clamp(resistance, -1f, 1f));

        // Degats purs ignorent les resistances
        if (damageInfo.damageType == DamageType.True)
        {
            finalDamage = baseDamage;
        }
        else
        {
            // Appliquer la reduction de defense
            finalDamage = DamageCalculator.CalculateFinalDamage(
                new DamageInfo { baseDamage = finalDamage, damageType = damageInfo.damageType },
                _defense
            );
        }

        _currentHealth = Mathf.Max(0, _currentHealth - finalDamage);

        // Notifier les listeners
        OnDamaged?.Invoke(damageInfo);
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
    /// Soigne cette entite.
    /// </summary>
    public void Heal(float amount)
    {
        if (IsDead) return;

        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + Mathf.Abs(amount));
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    /// <summary>
    /// Definit la sante a une valeur specifique.
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
    /// Reinitialise la sante au maximum.
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
            DamageType.Fire => _fireResistance,
            DamageType.Water => _waterResistance,
            DamageType.Ice => _iceResistance,
            DamageType.Electric => _electricResistance,
            DamageType.Wind => _windResistance,
            DamageType.Earth => _earthResistance,
            DamageType.Light => _lightResistance,
            DamageType.Dark => _darkResistance,
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
    public float Defense => _defense;

    #endregion
}
