/// <summary>
/// Types d'armes disponibles dans le jeu.
/// </summary>
public enum WeaponType
{
    /// <summary>
    /// Epee - equilibree, degats et vitesse moyens.
    /// Combo fluide, bonne polyvalence.
    /// </summary>
    Sword = 0,

    /// <summary>
    /// Grande epee - degats eleves, vitesse lente.
    /// Peut briser les gardes, grande portee.
    /// </summary>
    Greatsword = 1,

    /// <summary>
    /// Doubles lames - degats faibles, vitesse tres elevee.
    /// Combos longs, haute mobilite.
    /// </summary>
    DualBlades = 2,

    /// <summary>
    /// Lance - portee moyenne-longue, degats moyens.
    /// Attaques directionnelles, thrust damage.
    /// </summary>
    Spear = 3,

    /// <summary>
    /// Arc - attaque a distance, peut charger.
    /// Degats selon charge, headshot bonus.
    /// </summary>
    Bow = 4,

    /// <summary>
    /// Staff/Baton - magie, degats elementaires.
    /// Catalyseur pour sorts.
    /// </summary>
    Staff = 5,

    /// <summary>
    /// Faux - degats en arc, drain de vie.
    /// Attaques circulaires.
    /// </summary>
    Scythe = 6
}

/// <summary>
/// Categories de portee pour les armes.
/// </summary>
public enum WeaponRangeCategory
{
    /// <summary>Corps a corps proche</summary>
    Melee = 0,

    /// <summary>Corps a corps etendu (lance, grande epee)</summary>
    MeleeExtended = 1,

    /// <summary>Attaque a distance</summary>
    Ranged = 2
}
