/// <summary>
/// Types de statistiques de base du personnage.
/// </summary>
public enum StatType
{
    // Stats de base
    Strength,      // Force - Augmente les dégâts physiques
    Dexterity,     // Dextérité - Augmente la précision et l'esquive
    Intelligence,  // Intelligence - Augmente les dégâts magiques
    Vitality,      // Vitalité - Augmente les points de vie
    Wisdom,        // Sagesse - Augmente les points de mana et la régénération
    Luck,          // Chance - Augmente les chances de critique et le loot

    // Stats dérivées (calculées à partir des stats de base)
    MaxHealth,     // Points de vie maximum
    MaxMana,       // Points de mana maximum
    MaxStamina,    // Points d'endurance maximum
    Attack,        // Puissance d'attaque physique
    Defense,       // Défense physique
    MagicAttack,   // Puissance d'attaque magique
    MagicDefense,  // Défense magique
    CritRate,      // Taux de critique (%)
    CritDamage,    // Multiplicateur de dégâts critiques
    Speed,         // Vitesse de déplacement
    AttackSpeed,   // Vitesse d'attaque
    Accuracy,      // Précision
    Evasion,       // Esquive
    HealthRegen,   // Régénération de vie par seconde
    ManaRegen,     // Régénération de mana par seconde
    StaminaRegen   // Régénération d'endurance par seconde
}
