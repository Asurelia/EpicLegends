using UnityEngine;

/// <summary>
/// Donnees de configuration d'une recette de fabrication.
/// </summary>
[CreateAssetMenu(fileName = "NewRecipe", menuName = "EpicLegends/Building/Crafting Recipe")]
public class CraftingRecipeData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("Nom de la recette")]
    public string recipeName;

    [Tooltip("Description")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Icone de la recette")]
    public Sprite icon;

    [Tooltip("Categorie de recette")]
    public RecipeCategory category = RecipeCategory.Basic;

    #endregion

    #region Ingredients

    [Header("Ingredients")]
    [Tooltip("Ressources requises")]
    public ResourceCost[] ingredients;

    [Tooltip("Quantite produite")]
    public int outputAmount = 1;

    [Tooltip("Type de ressource produite (si ressource)")]
    public ResourceType outputResourceType;

    [Tooltip("Item produit (si item)")]
    public ItemData outputItem;

    #endregion

    #region Production

    [Header("Production")]
    [Tooltip("Temps de fabrication (secondes)")]
    public float craftTime = 5f;

    [Tooltip("Station de craft requise")]
    public BuildingSubCategory requiredStation = BuildingSubCategory.Workbench;

    [Tooltip("Niveau de station requis")]
    public int requiredStationLevel = 1;

    [Tooltip("Necessite de l'energie?")]
    public bool requiresEnergy = false;

    [Tooltip("Consommation d'energie par craft")]
    public float energyPerCraft = 0f;

    #endregion

    #region Deblocage

    [Header("Deblocage")]
    [Tooltip("Recette debloquee par defaut?")]
    public bool unlockedByDefault = true;

    [Tooltip("Niveau de joueur requis")]
    public int requiredPlayerLevel = 1;

    [Tooltip("Recettes prerequises")]
    public CraftingRecipeData[] prerequisites;

    [Tooltip("Recherche requise")]
    public string requiredResearch;

    #endregion

    #region Public Methods

    /// <summary>
    /// Verifie si les ingredients sont disponibles.
    /// </summary>
    public bool HasIngredients(IResourceContainer container)
    {
        if (container == null) return false;

        foreach (var ingredient in ingredients)
        {
            if (!container.HasResource(ingredient.resourceType, ingredient.amount))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Consomme les ingredients.
    /// </summary>
    public bool ConsumeIngredients(IResourceContainer container)
    {
        if (!HasIngredients(container)) return false;

        foreach (var ingredient in ingredients)
        {
            container.RemoveResource(ingredient.resourceType, ingredient.amount);
        }
        return true;
    }

    #endregion
}

/// <summary>
/// Categories de recettes.
/// </summary>
public enum RecipeCategory
{
    Basic,
    Construction,
    Tools,
    Weapons,
    Armor,
    Consumables,
    Decoration,
    Advanced
}

/// <summary>
/// Interface pour les conteneurs de ressources.
/// </summary>
public interface IResourceContainer
{
    bool HasResource(ResourceType type, int amount);
    bool AddResource(ResourceType type, int amount);
    bool RemoveResource(ResourceType type, int amount);
    int GetResourceCount(ResourceType type);
}
