using UnityEngine;

/// <summary>
/// Utilitaire statique pour calculer les degats finaux.
/// Applique les formules de reduction, critiques et modificateurs.
/// </summary>
public static class DamageCalculator
{
    #region Constants

    /// <summary>
    /// Degats minimum apres reduction.
    /// </summary>
    public const float MIN_DAMAGE = 1f;

    /// <summary>
    /// Chance critique de base.
    /// </summary>
    public const float BASE_CRIT_CHANCE = 0.05f;

    /// <summary>
    /// Multiplicateur critique de base.
    /// </summary>
    public const float BASE_CRIT_MULTIPLIER = 1.5f;

    /// <summary>
    /// Facteur de scaling de la defense.
    /// </summary>
    public const float DEFENSE_SCALING = 100f;

    #endregion

    #region Public Methods

    /// <summary>
    /// Calcule les degats finaux apres toutes les modifications.
    /// </summary>
    /// <param name="damageInfo">Informations de degats</param>
    /// <param name="targetDefense">Defense de la cible</param>
    /// <param name="elementalModifier">Modificateur elementaire (1 = neutre)</param>
    /// <returns>Degats finaux</returns>
    public static float CalculateFinalDamage(DamageInfo damageInfo, float targetDefense, float elementalModifier = 1f)
    {
        // Degats de base avec critique
        float damage = damageInfo.GetEffectiveDamage();

        // Appliquer le modificateur elementaire
        damage *= elementalModifier;

        // Appliquer la reduction de defense
        // Formule: damage * (100 / (100 + defense))
        float defenseReduction = DEFENSE_SCALING / (DEFENSE_SCALING + targetDefense);
        damage *= defenseReduction;

        // Appliquer le minimum
        return Mathf.Max(MIN_DAMAGE, damage);
    }

    /// <summary>
    /// Calcule les degats avec stats d'attaquant et de cible.
    /// </summary>
    public static float CalculateDamageWithStats(
        DamageInfo damageInfo,
        float attackerAttack,
        float targetDefense,
        float elementalModifier = 1f)
    {
        // Modifier les degats de base par l'attaque
        float modifiedDamage = damageInfo.baseDamage * (1f + attackerAttack / 100f);
        var modifiedInfo = damageInfo;
        modifiedInfo.baseDamage = modifiedDamage;

        return CalculateFinalDamage(modifiedInfo, targetDefense, elementalModifier);
    }

    /// <summary>
    /// Retourne la chance critique de base.
    /// </summary>
    public static float GetBaseCriticalChance()
    {
        return BASE_CRIT_CHANCE;
    }

    /// <summary>
    /// Calcule la chance critique avec bonus.
    /// </summary>
    public static float CalculateCriticalChance(float bonusCritRate)
    {
        return Mathf.Clamp01(BASE_CRIT_CHANCE + bonusCritRate);
    }

    /// <summary>
    /// Determine si un coup est critique.
    /// </summary>
    public static bool RollCritical(float critChance)
    {
        return Random.value < critChance;
    }

    /// <summary>
    /// Calcule le multiplicateur elementaire entre deux elements.
    /// </summary>
    public static float GetElementalMultiplier(DamageType attackElement, DamageType defenseElement)
    {
        // Degats vrais ignorent les resistances
        if (attackElement == DamageType.True)
            return 1f;

        // Meme element = reduit
        if (attackElement == defenseElement && attackElement != DamageType.Physical)
            return 0.5f;

        // Elements opposes = bonus
        if (AreOpposedElements(attackElement, defenseElement))
            return 1.5f;

        return 1f;
    }

    /// <summary>
    /// Verifie si deux elements sont opposes.
    /// </summary>
    public static bool AreOpposedElements(DamageType a, DamageType b)
    {
        return (a == DamageType.Fire && b == DamageType.Water) ||
               (a == DamageType.Water && b == DamageType.Fire) ||
               (a == DamageType.Fire && b == DamageType.Ice) ||
               (a == DamageType.Ice && b == DamageType.Fire) ||
               (a == DamageType.Light && b == DamageType.Dark) ||
               (a == DamageType.Dark && b == DamageType.Light);
    }

    /// <summary>
    /// Calcule les degats de DoT (Damage over Time).
    /// </summary>
    public static float CalculateDoTDamage(float baseDamage, float duration, float tickRate)
    {
        int ticks = Mathf.CeilToInt(duration / tickRate);
        return baseDamage / ticks;
    }

    /// <summary>
    /// Calcule les degats d'une attaque chargee.
    /// </summary>
    public static float CalculateChargedDamage(float baseDamage, float chargeTime, float minCharge, float maxCharge, float maxMultiplier)
    {
        float chargePercent = Mathf.Clamp01((chargeTime - minCharge) / (maxCharge - minCharge));
        float multiplier = Mathf.Lerp(1f, maxMultiplier, chargePercent);
        return baseDamage * multiplier;
    }

    #endregion
}
