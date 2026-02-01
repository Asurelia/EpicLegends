using UnityEngine;

/// <summary>
/// Adaptateur pour convertir ItemData en EquipmentInstance.
/// Permet de faire le pont entre le systeme d'inventaire (ItemData)
/// et le systeme d'equipement avance (EquipmentInstance).
/// </summary>
public static class ItemEquipmentAdapter
{
    /// <summary>
    /// Convertit un ItemData en EquipmentData runtime.
    /// Cree un EquipmentData temporaire base sur les stats de l'ItemData.
    /// </summary>
    /// <param name="itemData">Donnees de l'item a convertir.</param>
    /// <returns>EquipmentData correspondant ou null si non convertible.</returns>
    public static EquipmentData ConvertToEquipmentData(ItemData itemData)
    {
        if (itemData == null) return null;
        if (!itemData.IsEquippable) return null;

        // Creer un EquipmentData runtime
        var equipData = ScriptableObject.CreateInstance<EquipmentData>();

        // Identification
        equipData.equipmentName = itemData.displayName;
        equipData.description = itemData.description;
        equipData.icon = itemData.icon;
        equipData.modelPrefab = itemData.worldPrefab;

        // Classification
        equipData.slot = itemData.equipSlot;
        equipData.rarity = ConvertRarity(itemData.rarity);

        // Requirements
        equipData.requiredLevel = itemData.requiredLevel;

        // Stats de base - convertir les stats de l'ItemData
        var stats = new System.Collections.Generic.List<EquipmentStat>();

        if (itemData.attackPower > 0)
        {
            stats.Add(new EquipmentStat { statType = StatType.Attack, value = itemData.attackPower });
        }
        if (itemData.defensePower > 0)
        {
            stats.Add(new EquipmentStat { statType = StatType.Defense, value = itemData.defensePower });
        }
        if (itemData.healthBonus > 0)
        {
            stats.Add(new EquipmentStat { statType = StatType.MaxHealth, value = itemData.healthBonus });
        }
        if (itemData.manaBonus > 0)
        {
            stats.Add(new EquipmentStat { statType = StatType.MaxMana, value = itemData.manaBonus });
        }
        if (itemData.critChanceBonus > 0)
        {
            stats.Add(new EquipmentStat { statType = StatType.CritRate, value = itemData.critChanceBonus });
        }
        if (itemData.critDamageBonus > 0)
        {
            stats.Add(new EquipmentStat { statType = StatType.CritDamage, value = itemData.critDamageBonus });
        }

        // Ajouter les stat bonuses personnalises
        if (itemData.statBonuses != null)
        {
            foreach (var bonus in itemData.statBonuses)
            {
                stats.Add(new EquipmentStat
                {
                    statType = bonus.statType,
                    value = bonus.value
                });
            }
        }

        equipData.baseStats = stats.ToArray();

        // Valeur
        equipData.sellPrice = itemData.sellPrice;
        equipData.isSellable = true;
        equipData.isDestroyable = true;

        return equipData;
    }

    /// <summary>
    /// Cree une EquipmentInstance a partir d'un ItemData.
    /// </summary>
    /// <param name="itemData">Donnees de l'item.</param>
    /// <returns>Instance d'equipement ou null si non convertible.</returns>
    public static EquipmentInstance CreateEquipmentInstance(ItemData itemData)
    {
        var equipData = ConvertToEquipmentData(itemData);
        if (equipData == null) return null;

        return new EquipmentInstance(equipData);
    }

    /// <summary>
    /// Convertit ItemRarity en EquipmentRarity.
    /// </summary>
    private static EquipmentRarity ConvertRarity(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common => EquipmentRarity.Common,
            ItemRarity.Uncommon => EquipmentRarity.Uncommon,
            ItemRarity.Rare => EquipmentRarity.Rare,
            ItemRarity.Epic => EquipmentRarity.Epic,
            ItemRarity.Legendary => EquipmentRarity.Legendary,
            ItemRarity.Mythic => EquipmentRarity.Legendary, // Mythic devient Legendary
            _ => EquipmentRarity.Common
        };
    }
}
