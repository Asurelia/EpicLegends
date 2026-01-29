/// <summary>
/// Types d'ennemis.
/// </summary>
public enum EnemyType
{
    /// <summary>Ennemi de base, facile a tuer</summary>
    Basic = 0,

    /// <summary>Ennemi ameliore avec plus de vie/degats</summary>
    Elite = 1,

    /// <summary>Boss avec patterns d'attaque complexes</summary>
    Boss = 2,

    /// <summary>Mini-boss entre elite et boss</summary>
    MiniBoss = 3
}

/// <summary>
/// Comportements d'IA disponibles.
/// </summary>
public enum AIBehavior
{
    /// <summary>Attaque de front, rush le joueur</summary>
    Aggressive = 0,

    /// <summary>Attend et contre-attaque</summary>
    Defensive = 1,

    /// <summary>Fuit quand blesse</summary>
    Cowardly = 2,

    /// <summary>Garde ses distances et attaque a distance</summary>
    Ranged = 3,

    /// <summary>Support d'autres ennemis (buff, heal)</summary>
    Support = 4,

    /// <summary>Patrouille et alerte d'autres ennemis</summary>
    Scout = 5
}

/// <summary>
/// Etats de l'IA ennemie.
/// </summary>
public enum EnemyAIState
{
    /// <summary>En attente, ne fait rien</summary>
    Idle = 0,

    /// <summary>Patrouille sur un chemin</summary>
    Patrol = 1,

    /// <summary>Pourchasse la cible</summary>
    Chase = 2,

    /// <summary>En combat, attaque</summary>
    Combat = 3,

    /// <summary>Fuit la cible</summary>
    Flee = 4,

    /// <summary>Retourne a la position d'origine</summary>
    Return = 5,

    /// <summary>En stagger, ne peut pas agir</summary>
    Staggered = 6,

    /// <summary>Mort</summary>
    Dead = 7
}
