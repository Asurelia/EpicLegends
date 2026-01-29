/// <summary>
/// Types de creatures.
/// </summary>
public enum CreatureType
{
    /// <summary>Bete - animaux, loups, ours</summary>
    Beast = 0,

    /// <summary>Dragon - dragons, wyvernes</summary>
    Dragon = 1,

    /// <summary>Esprit - fantomes, sprites</summary>
    Spirit = 2,

    /// <summary>Elementaire - feu, eau, terre, air</summary>
    Elemental = 3,

    /// <summary>Feerique - fees, lutins</summary>
    Fae = 4,

    /// <summary>Demon - demons, diables</summary>
    Demon = 5,

    /// <summary>Angelique - anges, celestins</summary>
    Celestial = 6,

    /// <summary>Mecanique - golems, automates</summary>
    Construct = 7,

    /// <summary>Mort-vivant - squelettes, vampires</summary>
    Undead = 8,

    /// <summary>Plante - treants, fleurs</summary>
    Plant = 9
}

/// <summary>
/// Rarete des creatures.
/// </summary>
public enum CreatureRarity
{
    /// <summary>Commun - facile a trouver</summary>
    Common = 0,

    /// <summary>Peu commun - assez rare</summary>
    Uncommon = 1,

    /// <summary>Rare - difficile a trouver</summary>
    Rare = 2,

    /// <summary>Epique - tres rare</summary>
    Epic = 3,

    /// <summary>Legendaire - extremement rare</summary>
    Legendary = 4,

    /// <summary>Mythique - unique au monde</summary>
    Mythic = 5
}

/// <summary>
/// Role de la creature en combat.
/// </summary>
public enum CreatureRole
{
    /// <summary>Attaquant - degats eleves</summary>
    Attacker = 0,

    /// <summary>Defenseur - tank, haute defense</summary>
    Defender = 1,

    /// <summary>Support - soins, buffs</summary>
    Support = 2,

    /// <summary>Polyvalent - equilibre</summary>
    Balanced = 3
}

/// <summary>
/// Taille de la creature (affecte la montabilite).
/// </summary>
public enum CreatureSize
{
    /// <summary>Petite - ne peut pas etre montee</summary>
    Small = 0,

    /// <summary>Moyenne - peut etre montee</summary>
    Medium = 1,

    /// <summary>Grande - montable, plus rapide</summary>
    Large = 2,

    /// <summary>Enorme - montable, peut voler</summary>
    Huge = 3
}
