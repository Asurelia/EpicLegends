using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Données de base d'un objet (ScriptableObject).
/// Définit les propriétés immuables d'un type d'objet.
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "EpicLegends/Items/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("ID unique de l'objet")]
    public string itemId;

    [Tooltip("Nom affiché dans le jeu")]
    public string displayName;

    [Tooltip("Description de l'objet")]
    [TextArea(2, 5)]
    public string description;

    [Header("Catégorisation")]
    [Tooltip("Catégorie de l'objet")]
    public ItemCategory category;

    [Tooltip("Rareté de l'objet")]
    public ItemRarity rarity;

    [Header("Visuel")]
    [Tooltip("Icône de l'objet pour l'inventaire")]
    public Sprite icon;

    [Tooltip("Préfab de l'objet dans le monde")]
    public GameObject worldPrefab;

    [Header("Empilement")]
    [Tooltip("L'objet peut-il être empilé?")]
    public bool isStackable = false;

    [Tooltip("Taille maximale d'une pile")]
    [Range(1, 999)]
    public int maxStackSize = 1;

    [Header("Valeur")]
    [Tooltip("Prix de vente chez un marchand")]
    public int sellPrice = 0;

    [Tooltip("Prix d'achat chez un marchand")]
    public int buyPrice = 0;

    [Header("Équipement (si applicable)")]
    [Tooltip("Slot d'équipement (None si non équipable)")]
    public EquipmentSlot equipSlot = EquipmentSlot.None;

    [Tooltip("Niveau requis pour équiper")]
    public int requiredLevel = 1;

    [Header("Statistiques (si équipement)")]
    [Tooltip("Bonus de stats apportés par l'objet")]
    public List<ItemStatBonus> statBonuses = new List<ItemStatBonus>();

    [Header("Combat Stats")]
    public float attackPower = 0f;
    public float defensePower = 0f;
    public float healthBonus = 0f;
    public float manaBonus = 0f;
    public float critChanceBonus = 0f;
    public float critDamageBonus = 0f;

    [Header("Consumable Effects")]
    public float healAmount = 0f;
    public float manaRestoreAmount = 0f;

    [Header("Consommable (si applicable)")]
    [Tooltip("L'objet est-il consommable?")]
    public bool isConsumable = false;

    [Tooltip("Cooldown après utilisation (secondes)")]
    public float useCooldown = 0f;

    [Header("Audio")]
    [Tooltip("Son joué lors du ramassage")]
    public AudioClip pickupSound;

    [Tooltip("Son joué lors de l'utilisation")]
    public AudioClip useSound;

    /// <summary>
    /// Retourne true si l'objet peut être équipé.
    /// </summary>
    public bool IsEquippable => equipSlot != EquipmentSlot.None;

    /// <summary>
    /// Retourne la couleur associée à la rareté.
    /// </summary>
    public Color RarityColor => GetRarityColor(rarity);

    /// <summary>
    /// Obtient la couleur associée à une rareté.
    /// </summary>
    public static Color GetRarityColor(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common => Color.white,
            ItemRarity.Uncommon => new Color(0.2f, 0.8f, 0.2f),    // Vert
            ItemRarity.Rare => new Color(0.3f, 0.5f, 1f),          // Bleu
            ItemRarity.Epic => new Color(0.7f, 0.3f, 0.9f),        // Violet
            ItemRarity.Legendary => new Color(1f, 0.6f, 0f),       // Orange
            ItemRarity.Mythic => new Color(1f, 0.2f, 0.2f),        // Rouge
            _ => Color.white
        };
    }

    private void OnValidate()
    {
        // Auto-générer l'ID si vide
        if (string.IsNullOrEmpty(itemId))
        {
            itemId = name.ToLower().Replace(" ", "_");
        }

        // S'assurer que maxStackSize est au moins 1
        if (maxStackSize < 1) maxStackSize = 1;

        // Non-stackable = max 1
        if (!isStackable) maxStackSize = 1;
    }
}

/// <summary>
/// Bonus de statistique apporté par un objet.
/// </summary>
[System.Serializable]
public struct ItemStatBonus
{
    public StatType statType;
    public ModifierType modifierType;
    public float value;

    public ItemStatBonus(StatType stat, float val, ModifierType type = ModifierType.Flat)
    {
        statType = stat;
        value = val;
        modifierType = type;
    }

    /// <summary>
    /// Convertit en StatModifier.
    /// </summary>
    public StatModifier ToStatModifier(object source)
    {
        return new StatModifier(value, modifierType, 0, source);
    }
}
