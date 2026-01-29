using UnityEngine;

/// <summary>
/// Donnees de configuration d'un type de ressource.
/// </summary>
[CreateAssetMenu(fileName = "NewResource", menuName = "EpicLegends/Building/Resource Data")]
public class ResourceData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("Type de ressource")]
    public ResourceType resourceType;

    [Tooltip("Nom affiche")]
    public string displayName;

    [Tooltip("Description")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Icone")]
    public Sprite icon;

    [Tooltip("Categorie")]
    public ResourceCategory category = ResourceCategory.Raw;

    #endregion

    #region Stockage

    [Header("Stockage")]
    [Tooltip("Taille de stack maximum")]
    public int maxStackSize = 100;

    [Tooltip("Poids par unite")]
    public float weight = 1f;

    [Tooltip("Valeur de base")]
    public int baseValue = 1;

    #endregion

    #region Collecte

    [Header("Collecte")]
    [Tooltip("Peut etre collecte directement?")]
    public bool canBeGathered = true;

    [Tooltip("Outil requis")]
    public ToolType requiredTool = ToolType.None;

    [Tooltip("Temps de collecte (secondes)")]
    public float gatherTime = 1f;

    [Tooltip("Quantite par collecte")]
    public int gatherAmount = 1;

    [Tooltip("Experience donnee par collecte")]
    public int gatherXP = 1;

    #endregion

    #region Sources

    [Header("Sources")]
    [Tooltip("Prefab de la source (arbre, rocher, etc.)")]
    public GameObject sourcePrefab;

    [Tooltip("Biomes ou cette ressource apparait")]
    public BiomeType[] spawnBiomes;

    [Tooltip("Rarete (0-1)")]
    [Range(0f, 1f)]
    public float rarity = 1f;

    #endregion

    #region Audio/Visual

    [Header("Audio/Visual")]
    [Tooltip("Son de collecte")]
    public AudioClip gatherSound;

    [Tooltip("Particules de collecte")]
    public GameObject gatherVFX;

    [Tooltip("Couleur de la ressource")]
    public Color resourceColor = Color.white;

    #endregion

    #region Public Methods

    /// <summary>
    /// Obtient le nom d'affichage.
    /// </summary>
    public string GetDisplayName()
    {
        return string.IsNullOrEmpty(displayName) ? resourceType.ToString() : displayName;
    }

    /// <summary>
    /// Calcule la valeur d'une quantite.
    /// </summary>
    public int GetValue(int amount)
    {
        return baseValue * amount;
    }

    /// <summary>
    /// Verifie si l'outil est adapte.
    /// </summary>
    public bool CanGatherWith(ToolType tool)
    {
        if (requiredTool == ToolType.None) return true;
        return tool == requiredTool || tool == ToolType.Universal;
    }

    #endregion
}

/// <summary>
/// Types d'outils pour la collecte.
/// </summary>
public enum ToolType
{
    None,
    Axe,
    Pickaxe,
    Shovel,
    Sickle,
    FishingRod,
    Universal
}

/// <summary>
/// Types de biomes.
/// </summary>
public enum BiomeType
{
    Forest,
    Plains,
    Mountains,
    Desert,
    Swamp,
    Tundra,
    Beach,
    Ocean,
    Cave,
    Volcanic
}
