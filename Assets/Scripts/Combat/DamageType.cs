/// <summary>
/// Types de degats dans le jeu.
/// Inclut les degats physiques et les 8 elements.
/// </summary>
public enum DamageType
{
    /// <summary>Degats physiques standards</summary>
    Physical = 0,

    /// <summary>Degats de feu - brule, DoT</summary>
    Fire = 1,

    /// <summary>Degats d'eau - reactions avec feu/glace/electrique</summary>
    Water = 2,

    /// <summary>Degats de glace - ralentit, gele</summary>
    Ice = 3,

    /// <summary>Degats electriques - paralyse, chain</summary>
    Electric = 4,

    /// <summary>Degats de vent - repousse, propage elements</summary>
    Wind = 5,

    /// <summary>Degats de terre - bouclier, cristallise</summary>
    Earth = 6,

    /// <summary>Degats de lumiere - bonus contre Dark</summary>
    Light = 7,

    /// <summary>Degats d'ombre - bonus contre Light</summary>
    Dark = 8,

    /// <summary>Degats purs - ignore les resistances</summary>
    True = 99
}

/// <summary>
/// Etats possibles du combat.
/// </summary>
public enum CombatState
{
    /// <summary>En attente, peut effectuer toute action</summary>
    Idle,

    /// <summary>En train d'attaquer</summary>
    Attacking,

    /// <summary>En train de bloquer</summary>
    Blocking,

    /// <summary>Fenetre de parade active</summary>
    Parrying,

    /// <summary>En esquive avec i-frames</summary>
    Dodging,

    /// <summary>Etourdi apres perte de poise</summary>
    Staggered,

    /// <summary>En train de charger une attaque</summary>
    Charging,

    /// <summary>En recuperation apres une action</summary>
    Recovering,

    /// <summary>Incapable d'agir (knockdown, etc.)</summary>
    Disabled
}

/// <summary>
/// Etats de la hurtbox (zone de reception de degats).
/// </summary>
public enum HurtboxState
{
    /// <summary>Peut recevoir des degats normalement</summary>
    Vulnerable,

    /// <summary>Invincible, ignore tous les degats</summary>
    Invincible,

    /// <summary>En parade, peut contrer l'attaque</summary>
    Parrying,

    /// <summary>En blocage, reduit les degats</summary>
    Blocking,

    /// <summary>Super armure, ne stagger pas mais prend des degats</summary>
    SuperArmor
}
