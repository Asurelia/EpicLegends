using UnityEngine;

/// <summary>
/// Donnees de configuration d'un type de batiment.
/// </summary>
[CreateAssetMenu(fileName = "NewBuilding", menuName = "EpicLegends/Building/Building Data")]
public class BuildingData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("Nom du batiment")]
    public string buildingName;

    [Tooltip("Description")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Icone du batiment")]
    public Sprite icon;

    [Tooltip("Categorie")]
    public BuildingCategory category = BuildingCategory.Structure;

    [Tooltip("Sous-categorie")]
    public BuildingSubCategory subCategory = BuildingSubCategory.Foundation;

    #endregion

    #region Placement

    [Header("Placement")]
    [Tooltip("Taille sur la grille (X, Z)")]
    public Vector2Int gridSize = Vector2Int.one;

    [Tooltip("Hauteur du batiment")]
    public float height = 3f;

    [Tooltip("Peut etre place sur le sol?")]
    public bool canPlaceOnGround = true;

    [Tooltip("Necessite une fondation?")]
    public bool requiresFoundation = false;

    [Tooltip("Peut etre empile?")]
    public bool canStack = false;

    [Tooltip("Peut etre tourne?")]
    public bool canRotate = true;

    [Tooltip("Angles de rotation disponibles")]
    public int[] rotationAngles = { 0, 90, 180, 270 };

    [Tooltip("Peut etre place dans l'eau?")]
    public bool canPlaceInWater = false;

    [Tooltip("Prefab du batiment")]
    public GameObject prefab;

    [Tooltip("Prefab de preview")]
    public GameObject previewPrefab;

    #endregion

    #region Stats

    [Header("Stats")]
    [Tooltip("Points de vie maximum")]
    public float maxHealth = 100f;

    [Tooltip("Defense (reduction des degats)")]
    public float defense = 0f;

    [Tooltip("Tier de base")]
    public BuildingTier baseTier = BuildingTier.Wood;

    [Tooltip("Peut etre ameliore?")]
    public bool canUpgrade = true;

    [Tooltip("Tier maximum")]
    public BuildingTier maxTier = BuildingTier.Tech;

    #endregion

    #region Cout de Construction

    [Header("Cout de Construction")]
    [Tooltip("Ressources requises")]
    public ResourceCost[] buildCosts;

    [Tooltip("Temps de construction (secondes)")]
    public float buildTime = 5f;

    [Tooltip("Niveau de batiment requis pour debloquer")]
    public int requiredBuildingLevel = 1;

    #endregion

    #region Fonctionnalite

    [Header("Fonctionnalite")]
    [Tooltip("Fournit de l'energie?")]
    public bool providesEnergy = false;

    [Tooltip("Quantite d'energie fournie")]
    public float energyProvided = 0f;

    [Tooltip("Consomme de l'energie?")]
    public bool consumesEnergy = false;

    [Tooltip("Quantite d'energie consommee")]
    public float energyConsumed = 0f;

    [Tooltip("Peut stocker des items?")]
    public bool hasStorage = false;

    [Tooltip("Nombre de slots de stockage")]
    public int storageSlots = 0;

    [Tooltip("Permet le craft?")]
    public bool hasCrafting = false;

    [Tooltip("Recettes disponibles")]
    public CraftingRecipeData[] availableRecipes;

    #endregion

    #region Audio/Visual

    [Header("Audio/Visual")]
    [Tooltip("Son de placement")]
    public AudioClip placeSound;

    [Tooltip("Son de destruction")]
    public AudioClip destroySound;

    [Tooltip("Effet de construction")]
    public GameObject buildVFX;

    [Tooltip("Effet de destruction")]
    public GameObject destroyVFX;

    #endregion

    #region Snap Points

    [Header("Points de Connexion")]
    [Tooltip("Points de snap pour connexion")]
    public SnapPoint[] snapPoints;

    #endregion

    #region Public Methods

    /// <summary>
    /// Calcule le cout d'amelioration vers un tier.
    /// </summary>
    public ResourceCost[] GetUpgradeCost(BuildingTier targetTier)
    {
        if (!canUpgrade || targetTier <= baseTier)
            return new ResourceCost[0];

        // Multiplicateur base sur le tier cible
        float multiplier = targetTier switch
        {
            BuildingTier.Stone => 1.5f,
            BuildingTier.Metal => 2.5f,
            BuildingTier.Tech => 4f,
            _ => 1f
        };

        var upgradeCosts = new ResourceCost[buildCosts.Length];
        for (int i = 0; i < buildCosts.Length; i++)
        {
            upgradeCosts[i] = new ResourceCost
            {
                resourceType = GetUpgradedResourceType(buildCosts[i].resourceType, targetTier),
                amount = Mathf.CeilToInt(buildCosts[i].amount * multiplier)
            };
        }

        return upgradeCosts;
    }

    /// <summary>
    /// Obtient les stats ameliorees pour un tier.
    /// </summary>
    public float GetHealthForTier(BuildingTier tier)
    {
        float multiplier = tier switch
        {
            BuildingTier.Wood => 1f,
            BuildingTier.Stone => 1.5f,
            BuildingTier.Metal => 2.5f,
            BuildingTier.Tech => 4f,
            _ => 1f
        };

        return maxHealth * multiplier;
    }

    /// <summary>
    /// Obtient la defense pour un tier.
    /// </summary>
    public float GetDefenseForTier(BuildingTier tier)
    {
        float bonus = tier switch
        {
            BuildingTier.Wood => 0f,
            BuildingTier.Stone => 10f,
            BuildingTier.Metal => 25f,
            BuildingTier.Tech => 50f,
            _ => 0f
        };

        return defense + bonus;
    }

    private ResourceType GetUpgradedResourceType(ResourceType baseType, BuildingTier tier)
    {
        // Convertir les ressources de base vers les ressources du tier
        if (baseType == ResourceType.Wood && tier >= BuildingTier.Stone)
            return ResourceType.Stone;
        if (baseType == ResourceType.Stone && tier >= BuildingTier.Metal)
            return ResourceType.IronIngot;
        if (baseType == ResourceType.IronIngot && tier >= BuildingTier.Tech)
            return ResourceType.TechComponent;

        return baseType;
    }

    #endregion
}

/// <summary>
/// Categories de batiments.
/// </summary>
public enum BuildingCategory
{
    /// <summary>Elements structurels (murs, sols, toits)</summary>
    Structure,

    /// <summary>Production et crafting</summary>
    Production,

    /// <summary>Stockage</summary>
    Storage,

    /// <summary>Defense (murs, tourelles)</summary>
    Defense,

    /// <summary>Utilitaires (energie, logistique)</summary>
    Utility,

    /// <summary>Agriculture</summary>
    Farming,

    /// <summary>Decoratif</summary>
    Decoration
}

/// <summary>
/// Sous-categories de batiments.
/// </summary>
public enum BuildingSubCategory
{
    // Structure
    Foundation,
    Wall,
    Floor,
    Roof,
    Door,
    Window,
    Stairs,

    // Production
    Workbench,
    Furnace,
    Forge,
    AlchemyTable,

    // Storage
    Chest,
    Silo,
    Warehouse,

    // Defense
    DefenseWall,
    Tower,
    Turret,
    Trap,

    // Utility
    Generator,
    Conduit,
    ConveyorBelt,
    Splitter,

    // Farming
    FarmPlot,
    AnimalPen,
    WaterWell,

    // Decoration
    Light,
    Furniture,
    Trophy
}

/// <summary>
/// Tiers de batiments.
/// </summary>
public enum BuildingTier
{
    Wood = 0,
    Stone = 1,
    Metal = 2,
    Tech = 3
}

/// <summary>
/// Cout en ressources.
/// </summary>
[System.Serializable]
public struct ResourceCost
{
    public ResourceType resourceType;
    public int amount;
}

/// <summary>
/// Point de connexion pour snap.
/// </summary>
[System.Serializable]
public struct SnapPoint
{
    public Vector3 localPosition;
    public SnapPointType type;
    public Vector3 direction;
}

/// <summary>
/// Types de points de snap.
/// </summary>
public enum SnapPointType
{
    WallBottom,
    WallTop,
    WallSide,
    FloorEdge,
    RoofEdge,
    DoorFrame
}
