/// <summary>
/// Types de ressources disponibles dans le jeu.
/// </summary>
public enum ResourceType
{
    // Ressources brutes
    Wood = 0,
    Stone = 1,
    IronOre = 2,
    CopperOre = 3,
    GoldOre = 4,
    Coal = 5,
    Crystal = 6,
    Fiber = 7,
    Hide = 8,
    Bone = 9,

    // Ressources transformees - Bois
    Plank = 100,
    Beam = 101,
    Charcoal = 102,

    // Ressources transformees - Pierre
    StoneBrick = 110,
    CutStone = 111,
    Cement = 112,

    // Ressources transformees - Metaux
    IronIngot = 120,
    SteelIngot = 121,
    CopperIngot = 122,
    GoldIngot = 123,
    Alloy = 124,

    // Ressources avancees
    Glass = 200,
    Cloth = 201,
    Leather = 202,
    Rope = 203,
    Nail = 204,
    Gear = 205,
    Circuit = 206,
    TechComponent = 207,
    EnergyCell = 208,

    // Ressources speciales
    ElementalEssence = 300,
    CreatureCore = 301,
    MagicDust = 302,
    AncientFragment = 303
}

/// <summary>
/// Categories de ressources pour le filtrage.
/// </summary>
public enum ResourceCategory
{
    Raw,
    Processed,
    Advanced,
    Special
}
