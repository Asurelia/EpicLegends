/// <summary>
/// Catégorie d'un objet.
/// </summary>
public enum ItemCategory
{
    /// <summary>Armes (épées, arcs, bâtons, etc.)</summary>
    Weapon,

    /// <summary>Armures (casque, plastron, jambières, etc.)</summary>
    Armor,

    /// <summary>Accessoires (anneaux, amulettes, etc.)</summary>
    Accessory,

    /// <summary>Consommables (potions, nourriture, etc.)</summary>
    Consumable,

    /// <summary>Matériaux de craft</summary>
    Material,

    /// <summary>Objets de quête</summary>
    Quest,

    /// <summary>Objets clés (clés, laissez-passer, etc.)</summary>
    KeyItem,

    /// <summary>Créatures capturées</summary>
    Creature
}

/// <summary>
/// Rareté d'un objet.
/// </summary>
public enum ItemRarity
{
    /// <summary>Commun (blanc)</summary>
    Common = 0,

    /// <summary>Peu commun (vert)</summary>
    Uncommon = 1,

    /// <summary>Rare (bleu)</summary>
    Rare = 2,

    /// <summary>Épique (violet)</summary>
    Epic = 3,

    /// <summary>Légendaire (orange)</summary>
    Legendary = 4,

    /// <summary>Mythique (rouge)</summary>
    Mythic = 5
}

/// <summary>
/// Slot d'équipement.
/// </summary>
public enum EquipmentSlot
{
    None,
    MainHand,
    OffHand,
    Head,
    Body,
    Hands,
    Legs,
    Feet,
    Accessory1,
    Accessory2
}
