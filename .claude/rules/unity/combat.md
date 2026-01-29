---
paths:
  - "Assets/Scripts/Combat/**/*.cs"
  - "Assets/Scripts/**/*Damage*.cs"
  - "Assets/Scripts/**/*Health*.cs"
  - "Assets/Scripts/**/*Attack*.cs"
---

# Combat System Rules

## Damage System Pattern
```csharp
public interface IDamageable
{
    void TakeDamage(float amount, DamageType type);
    bool IsDead { get; }
}

public enum DamageType
{
    Physical,
    Magical,
    Fire,
    Ice,
    Poison
}
```

## Health Component Pattern
```csharp
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private float _maxHealth = 100f;
    private float _currentHealth;

    public event System.Action<float> OnHealthChanged;
    public event System.Action OnDeath;

    public bool IsDead => _currentHealth <= 0;

    private void Awake()
    {
        _currentHealth = _maxHealth;
    }

    public void TakeDamage(float amount, DamageType type)
    {
        if (IsDead) return;

        _currentHealth = Mathf.Max(0, _currentHealth - amount);
        OnHealthChanged?.Invoke(_currentHealth / _maxHealth);

        if (IsDead)
        {
            OnDeath?.Invoke();
        }
    }
}
```

## Attack Pattern
```csharp
public class MeleeAttack : MonoBehaviour
{
    [SerializeField] private float _damage = 10f;
    [SerializeField] private float _range = 2f;
    [SerializeField] private LayerMask _targetLayers;

    public void Attack()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position + transform.forward * _range,
            _range,
            _targetLayers
        );

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<IDamageable>(out var target))
            {
                target.TakeDamage(_damage, DamageType.Physical);
            }
        }
    }
}
```

## Rules
- ALWAYS use interfaces for damage targets (IDamageable)
- ALWAYS check IsDead before applying damage
- ALWAYS use events for health changes (not direct UI updates)
- NEVER hardcode damage values - use ScriptableObjects
- ALWAYS use LayerMask for hit detection
