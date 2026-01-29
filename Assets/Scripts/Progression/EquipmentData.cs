using UnityEngine;

/// <summary>
/// Donnees d'un equipement.
/// Definit les stats, slots, rarete et bonus d'un equipement.
/// </summary>
[CreateAssetMenu(fileName = "NewEquipment", menuName = "EpicLegends/Progression/Equipment Data")]
public class EquipmentData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("Nom de l'equipement")]
    public string equipmentName;

    [Tooltip("Description")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Icone")]
    public Sprite icon;

    [Tooltip("Modele 3D")]
    public GameObject modelPrefab;

    #endregion

    #region Classification

    [Header("Classification")]
    [Tooltip("Slot d'equipement")]
    public EquipmentSlot slot;

    [Tooltip("Rarete")]
    public EquipmentRarity rarity = EquipmentRarity.Common;

    [Tooltip("Type d'armure (si applicable)")]
    public ArmorType armorType;

    [Tooltip("Type d'arme (si applicable)")]
    public WeaponType weaponType;

    #endregion

    #region Requirements

    [Header("Pre-requis")]
    [Tooltip("Niveau requis")]
    public int requiredLevel = 1;

    [Tooltip("Classes autorisees (vide = toutes)")]
    public ClassType[] allowedClasses;

    [Tooltip("Stats requises")]
    public StatRequirement[] statRequirements;

    #endregion

    #region Base Stats

    [Header("Stats de base")]
    [Tooltip("Stats fixes")]
    public EquipmentStat[] baseStats;

    [Tooltip("Valeur d'armure (armures)")]
    public int armorValue;

    [Tooltip("Degats de base (armes)")]
    public int baseDamage;

    [Tooltip("Vitesse d'attaque (armes)")]
    public float attackSpeed = 1f;

    #endregion

    #region Random Stats

    [Header("Stats aleatoires")]
    [Tooltip("Nombre de slots de stats aleatoires")]
    [Range(0, 5)]
    public int randomStatSlots = 0;

    [Tooltip("Definitions des stats aleatoires possibles")]
    public RandomStatDefinition[] possibleRandomStats;

    #endregion

    #region Sockets

    [Header("Sockets")]
    [Tooltip("Nombre de sockets")]
    [Range(0, 5)]
    public int socketCount = 0;

    #endregion

    #region Set Bonus

    [Header("Bonus d'ensemble")]
    [Tooltip("Ensemble d'equipement")]
    public EquipmentSetData equipmentSet;

    #endregion

    #region Enchantment

    [Header("Enchantement")]
    [Tooltip("Niveau d'enchantement maximum")]
    public int maxEnchantLevel = 10;

    [Tooltip("Bonus par niveau d'enchantement")]
    public float enchantBonusPerLevel = 0.05f;

    #endregion

    #region Value

    [Header("Valeur")]
    [Tooltip("Prix de vente de base")]
    public int sellPrice;

    [Tooltip("Peut etre vendu?")]
    public bool isSellable = true;

    [Tooltip("Peut etre detruit?")]
    public bool isDestroyable = true;

    #endregion

    #region Public Methods

    /// <summary>
    /// Calcule la valeur totale de puissance de l'equipement.
    /// </summary>
    /// <returns>Score de puissance.</returns>
    public int GetPowerScore()
    {
        int score = 0;

        // Ajouter les stats de base
        if (baseStats != null)
        {
            foreach (var stat in baseStats)
            {
                score += Mathf.RoundToInt(stat.value * GetStatWeight(stat.statType));
            }
        }

        // Bonus de rarete
        score = Mathf.RoundToInt(score * GetRarityMultiplier());

        // Armure/Degats
        score += armorValue;
        score += baseDamage;

        return score;
    }

    /// <summary>
    /// Obtient le multiplicateur de rarete.
    /// </summary>
    /// <returns>Multiplicateur.</returns>
    public float GetRarityMultiplier()
    {
        switch (rarity)
        {
            case EquipmentRarity.Common: return 1f;
            case EquipmentRarity.Uncommon: return 1.2f;
            case EquipmentRarity.Rare: return 1.5f;
            case EquipmentRarity.Epic: return 2f;
            case EquipmentRarity.Legendary: return 3f;
            default: return 1f;
        }
    }

    /// <summary>
    /// Obtient la couleur de la rarete.
    /// </summary>
    /// <returns>Couleur.</returns>
    public Color GetRarityColor()
    {
        switch (rarity)
        {
            case EquipmentRarity.Common: return Color.white;
            case EquipmentRarity.Uncommon: return Color.green;
            case EquipmentRarity.Rare: return Color.blue;
            case EquipmentRarity.Epic: return new Color(0.6f, 0f, 0.8f); // Violet
            case EquipmentRarity.Legendary: return new Color(1f, 0.5f, 0f); // Orange
            default: return Color.white;
        }
    }

    private float GetStatWeight(StatType stat)
    {
        // Poids pour le calcul de puissance
        switch (stat)
        {
            case StatType.Strength:
            case StatType.Intelligence:
                return 2f;
            case StatType.Vitality:
                return 1.5f;
            case StatType.Dexterity:
            case StatType.Wisdom:
                return 1.2f;
            case StatType.Luck:
                return 1f;
            default:
                return 1f;
        }
    }

    #endregion
}

// Note: EquipmentSlot est deja defini dans Items/ItemCategory.cs

/// <summary>
/// Raretes d'equipement.
/// </summary>
public enum EquipmentRarity
{
    /// <summary>Commun (blanc).</summary>
    Common,

    /// <summary>Peu commun (vert).</summary>
    Uncommon,

    /// <summary>Rare (bleu).</summary>
    Rare,

    /// <summary>Epique (violet).</summary>
    Epic,

    /// <summary>Legendaire (orange).</summary>
    Legendary
}

/// <summary>
/// Stat fixe d'un equipement.
/// </summary>
[System.Serializable]
public struct EquipmentStat
{
    [Tooltip("Type de stat")]
    public StatType statType;

    [Tooltip("Valeur")]
    public float value;
}

/// <summary>
/// Pre-requis de stat pour un equipement.
/// </summary>
[System.Serializable]
public struct StatRequirement
{
    [Tooltip("Type de stat")]
    public StatType statType;

    [Tooltip("Valeur minimum requise")]
    public int minValue;
}

/// <summary>
/// Definition d'une stat aleatoire possible.
/// </summary>
[System.Serializable]
public struct RandomStatDefinition
{
    [Tooltip("Type de stat")]
    public StatType statType;

    [Tooltip("Valeur minimum")]
    public float minValue;

    [Tooltip("Valeur maximum")]
    public float maxValue;

    [Tooltip("Poids (probabilite)")]
    [Range(0f, 1f)]
    public float weight;
}
