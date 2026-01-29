/// <summary>
/// Interface for any entity that can take damage.
/// Implement this on players, enemies, destructible objects, etc.
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// Apply damage to this entity.
    /// </summary>
    /// <param name="amount">Amount of damage to apply.</param>
    /// <param name="damageType">Type of damage (physical, magical, etc.)</param>
    void TakeDamage(float amount, DamageType damageType = DamageType.Physical);

    /// <summary>
    /// Returns true if the entity is dead.
    /// </summary>
    bool IsDead { get; }
}

/// <summary>
/// Types of damage in the game.
/// Can be used for resistances and immunities.
/// </summary>
public enum DamageType
{
    Physical,
    Magical,
    Fire,
    Ice,
    Lightning,
    Poison,
    True  // Ignores all resistances
}
