/// <summary>
/// Les 8 elements du jeu.
/// Chaque element peut reagir avec d'autres pour creer des effets speciaux.
/// </summary>
public enum ElementType
{
    /// <summary>Feu - brule, DoT, reagit avec Water/Ice</summary>
    Fire = 0,

    /// <summary>Eau - mouille, reagit avec Fire/Ice/Electric</summary>
    Water = 1,

    /// <summary>Glace - gele, ralentit, reagit avec Fire/Water/Electric</summary>
    Ice = 2,

    /// <summary>Electrique - paralyse, chaine, reagit avec Water/Ice</summary>
    Electric = 3,

    /// <summary>Vent - repousse, propage les autres elements (Swirl)</summary>
    Wind = 4,

    /// <summary>Terre - cristallise, cree des boucliers</summary>
    Earth = 5,

    /// <summary>Lumiere - bonus contre Dark</summary>
    Light = 6,

    /// <summary>Ombre - bonus contre Light</summary>
    Dark = 7
}

/// <summary>
/// Types de reactions elementaires possibles.
/// </summary>
public enum ElementalReactionType
{
    /// <summary>Pas de reaction</summary>
    None = 0,

    /// <summary>Fire + Water = Vaporize (2x degats)</summary>
    Vaporize = 1,

    /// <summary>Fire + Ice = Melt (2x degats, enleve gel)</summary>
    Melt = 2,

    /// <summary>Fire + Electric = Overload (1.5x degats, explosion AoE)</summary>
    Overload = 3,

    /// <summary>Ice + Electric = Superconduct (1.5x degats, reduit defense)</summary>
    Superconduct = 4,

    /// <summary>Water + Electric = Electro-Charged (DoT electrique)</summary>
    ElectroCharged = 5,

    /// <summary>Water + Ice = Frozen (immobilise)</summary>
    Frozen = 6,

    /// <summary>Wind + autre element = Swirl (propage l'element)</summary>
    Swirl = 7,

    /// <summary>Earth + autre element = Crystallize (cree bouclier)</summary>
    Crystallize = 8,

    /// <summary>Light + Dark = Radiance (explosion lumineuse)</summary>
    Radiance = 9,

    /// <summary>Dark + Light = Eclipse (explosion sombre)</summary>
    Eclipse = 10
}
