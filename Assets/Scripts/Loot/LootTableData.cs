using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Table de loot definissant les items pouvant drop et leurs probabilites.
/// </summary>
[CreateAssetMenu(fileName = "NewLootTable", menuName = "EpicLegends/Loot/Loot Table")]
public class LootTableData : ScriptableObject
{
    #region Nested Classes

    [Serializable]
    public class LootEntry
    {
        [Tooltip("Item a dropper")]
        public ItemData item;

        [Tooltip("Chance de drop (0-100)")]
        [Range(0f, 100f)]
        public float dropChance = 10f;

        [Tooltip("Quantite minimum")]
        [Min(1)]
        public int minQuantity = 1;

        [Tooltip("Quantite maximum")]
        [Min(1)]
        public int maxQuantity = 1;

        [Tooltip("Niveau minimum pour dropper")]
        public int minLevel = 1;

        [Tooltip("Niveau maximum pour dropper (0 = pas de limite)")]
        public int maxLevel = 0;
    }

    [Serializable]
    public class GoldDrop
    {
        [Tooltip("Or minimum")]
        public int minGold = 1;

        [Tooltip("Or maximum")]
        public int maxGold = 10;

        [Tooltip("Chance de dropper de l'or (0-100)")]
        [Range(0f, 100f)]
        public float dropChance = 80f;
    }

    #endregion

    #region Serialized Fields

    [Header("Configuration")]
    [Tooltip("Nom de la table")]
    public string tableName;

    [Tooltip("Nombre de rolls (tentatives de drop)")]
    [Min(1)]
    public int rollCount = 1;

    [Tooltip("Garantir au moins un drop?")]
    public bool guaranteeOneDrop = false;

    [Header("Gold")]
    public GoldDrop goldDrop;

    [Header("Loot Entries")]
    public List<LootEntry> entries = new List<LootEntry>();

    [Header("Rarity Modifiers")]
    [Tooltip("Multiplicateur pour items Common")]
    public float commonMultiplier = 1f;

    [Tooltip("Multiplicateur pour items Uncommon")]
    public float uncommonMultiplier = 0.8f;

    [Tooltip("Multiplicateur pour items Rare")]
    public float rareMultiplier = 0.5f;

    [Tooltip("Multiplicateur pour items Epic")]
    public float epicMultiplier = 0.25f;

    [Tooltip("Multiplicateur pour items Legendary")]
    public float legendaryMultiplier = 0.1f;

    [Tooltip("Multiplicateur pour items Mythic")]
    public float mythicMultiplier = 0.02f;

    #endregion

    #region Public Methods

    /// <summary>
    /// Genere le loot basé sur cette table.
    /// </summary>
    /// <param name="playerLevel">Niveau du joueur (pour filtrage)</param>
    /// <param name="luckModifier">Modificateur de chance (1 = normal)</param>
    /// <returns>Liste des items droppes avec leurs quantites</returns>
    public List<LootResult> GenerateLoot(int playerLevel = 1, float luckModifier = 1f)
    {
        List<LootResult> results = new List<LootResult>();

        // Rolls pour les items
        for (int roll = 0; roll < rollCount; roll++)
        {
            foreach (var entry in entries)
            {
                if (!IsEligible(entry, playerLevel)) continue;

                float adjustedChance = GetAdjustedDropChance(entry, luckModifier);

                if (UnityEngine.Random.value * 100f < adjustedChance)
                {
                    int quantity = UnityEngine.Random.Range(entry.minQuantity, entry.maxQuantity + 1);
                    results.Add(new LootResult
                    {
                        item = entry.item,
                        quantity = quantity
                    });
                }
            }
        }

        // Garantir un drop si configure et rien n'a droppe
        if (guaranteeOneDrop && results.Count == 0 && entries.Count > 0)
        {
            var eligibleEntries = entries.FindAll(e => IsEligible(e, playerLevel));
            if (eligibleEntries.Count > 0)
            {
                var randomEntry = eligibleEntries[UnityEngine.Random.Range(0, eligibleEntries.Count)];
                int quantity = UnityEngine.Random.Range(randomEntry.minQuantity, randomEntry.maxQuantity + 1);
                results.Add(new LootResult
                {
                    item = randomEntry.item,
                    quantity = quantity
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Genere une quantite d'or.
    /// </summary>
    public int GenerateGold(float luckModifier = 1f)
    {
        if (goldDrop == null) return 0;
        if (UnityEngine.Random.value * 100f > goldDrop.dropChance * luckModifier) return 0;

        return UnityEngine.Random.Range(goldDrop.minGold, goldDrop.maxGold + 1);
    }

    #endregion

    #region Private Methods

    private bool IsEligible(LootEntry entry, int playerLevel)
    {
        if (entry.item == null) return false;
        if (playerLevel < entry.minLevel) return false;
        if (entry.maxLevel > 0 && playerLevel > entry.maxLevel) return false;
        return true;
    }

    private float GetAdjustedDropChance(LootEntry entry, float luckModifier)
    {
        float baseChance = entry.dropChance;

        // Appliquer le modificateur de rarete
        if (entry.item != null)
        {
            float rarityMod = GetRarityMultiplier(entry.item.rarity);
            baseChance *= rarityMod;
        }

        // Appliquer le modificateur de chance
        return baseChance * luckModifier;
    }

    private float GetRarityMultiplier(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common => commonMultiplier,
            ItemRarity.Uncommon => uncommonMultiplier,
            ItemRarity.Rare => rareMultiplier,
            ItemRarity.Epic => epicMultiplier,
            ItemRarity.Legendary => legendaryMultiplier,
            ItemRarity.Mythic => mythicMultiplier,
            _ => 1f
        };
    }

    #endregion
}

/// <summary>
/// Resultat d'un drop de loot.
/// </summary>
[Serializable]
public class LootResult
{
    public ItemData item;
    public int quantity;
}
