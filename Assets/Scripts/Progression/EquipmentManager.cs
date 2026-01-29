using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire d'equipement du personnage.
/// Gere les slots d'equipement, les bonus d'ensemble et les stats.
/// </summary>
public class EquipmentManager : MonoBehaviour
{
    #region Fields

    [Header("Configuration")]
    [SerializeField] private int _maxLoadouts = 3;

    // Equipements actuels par slot
    private Dictionary<EquipmentSlot, EquipmentInstance> _equippedItems;

    // Ensembles actifs
    private Dictionary<EquipmentSetData, int> _activeSets;

    // Loadouts sauvegardes
    private List<EquipmentLoadout> _savedLoadouts;

    #endregion

    #region Events

    /// <summary>Declenche lors d'un equip.</summary>
    public event Action<EquipmentSlot, EquipmentInstance> OnItemEquipped;

    /// <summary>Declenche lors d'un unequip.</summary>
    public event Action<EquipmentSlot, EquipmentInstance> OnItemUnequipped;

    /// <summary>Declenche lors d'un changement de bonus d'ensemble.</summary>
    public event Action<EquipmentSetData, int> OnSetBonusChanged;

    /// <summary>Declenche lors d'un changement de stats.</summary>
    public event Action OnStatsChanged;

    #endregion

    #region Properties

    /// <summary>Nombre d'items equipes.</summary>
    public int EquippedCount => _equippedItems?.Count ?? 0;

    /// <summary>Score de puissance total.</summary>
    public int TotalPowerScore
    {
        get
        {
            if (_equippedItems == null) return 0;

            int total = 0;
            foreach (var item in _equippedItems.Values)
            {
                total += item?.GetPowerScore() ?? 0;
            }
            return total;
        }
    }

    /// <summary>Armure totale.</summary>
    public int TotalArmor
    {
        get
        {
            if (_equippedItems == null) return 0;

            int total = 0;
            foreach (var item in _equippedItems.Values)
            {
                total += item?.GetTotalArmor() ?? 0;
            }
            return total;
        }
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _equippedItems = new Dictionary<EquipmentSlot, EquipmentInstance>();
        _activeSets = new Dictionary<EquipmentSetData, int>();
        _savedLoadouts = new List<EquipmentLoadout>();
    }

    #endregion

    #region Public Methods - Equip/Unequip

    /// <summary>
    /// Equipe un item.
    /// </summary>
    /// <param name="item">Item a equiper.</param>
    /// <returns>True si equipe avec succes.</returns>
    public bool Equip(EquipmentInstance item)
    {
        if (item == null || item.BaseData == null) return false;

        var slot = item.Slot;

        // Retirer l'item actuel si present
        EquipmentInstance previousItem = null;
        if (_equippedItems.TryGetValue(slot, out previousItem))
        {
            Unequip(slot);
        }

        // Equiper le nouvel item
        _equippedItems[slot] = item;

        // Mettre a jour les ensembles
        UpdateSetBonuses();

        OnItemEquipped?.Invoke(slot, item);
        OnStatsChanged?.Invoke();

        return true;
    }

    /// <summary>
    /// Desequipe un item d'un slot.
    /// </summary>
    /// <param name="slot">Slot a vider.</param>
    /// <returns>Item desequipe ou null.</returns>
    public EquipmentInstance Unequip(EquipmentSlot slot)
    {
        if (_equippedItems == null) return null;
        if (!_equippedItems.TryGetValue(slot, out var item)) return null;

        _equippedItems.Remove(slot);

        // Mettre a jour les ensembles
        UpdateSetBonuses();

        OnItemUnequipped?.Invoke(slot, item);
        OnStatsChanged?.Invoke();

        return item;
    }

    /// <summary>
    /// Desequipe tous les items.
    /// </summary>
    /// <returns>Liste des items desequipes.</returns>
    public List<EquipmentInstance> UnequipAll()
    {
        var items = new List<EquipmentInstance>();

        if (_equippedItems == null) return items;

        foreach (var slot in new List<EquipmentSlot>(_equippedItems.Keys))
        {
            var item = Unequip(slot);
            if (item != null)
            {
                items.Add(item);
            }
        }

        return items;
    }

    /// <summary>
    /// Obtient l'item equipe dans un slot.
    /// </summary>
    /// <param name="slot">Slot a verifier.</param>
    /// <returns>Item equipe ou null.</returns>
    public EquipmentInstance GetEquipped(EquipmentSlot slot)
    {
        if (_equippedItems == null) return null;
        return _equippedItems.TryGetValue(slot, out var item) ? item : null;
    }

    /// <summary>
    /// Verifie si un slot est occupe.
    /// </summary>
    /// <param name="slot">Slot a verifier.</param>
    /// <returns>True si occupe.</returns>
    public bool IsSlotOccupied(EquipmentSlot slot)
    {
        return _equippedItems != null && _equippedItems.ContainsKey(slot);
    }

    /// <summary>
    /// Obtient tous les items equipes.
    /// </summary>
    /// <returns>Dictionnaire des items.</returns>
    public Dictionary<EquipmentSlot, EquipmentInstance> GetAllEquipped()
    {
        return _equippedItems != null
            ? new Dictionary<EquipmentSlot, EquipmentInstance>(_equippedItems)
            : new Dictionary<EquipmentSlot, EquipmentInstance>();
    }

    #endregion

    #region Public Methods - Stats

    /// <summary>
    /// Obtient le bonus total d'une stat de tous les equipements.
    /// </summary>
    /// <param name="stat">Type de stat.</param>
    /// <returns>Bonus total.</returns>
    public float GetTotalStatBonus(StatType stat)
    {
        float total = 0f;

        if (_equippedItems != null)
        {
            foreach (var item in _equippedItems.Values)
            {
                total += item?.GetTotalStat(stat) ?? 0f;
            }
        }

        // Ajouter les bonus d'ensemble
        total += GetSetStatBonus(stat);

        return total;
    }

    /// <summary>
    /// Obtient le bonus de stat des ensembles actifs.
    /// </summary>
    /// <param name="stat">Type de stat.</param>
    /// <returns>Bonus des ensembles.</returns>
    public float GetSetStatBonus(StatType stat)
    {
        float total = 0f;

        if (_activeSets == null) return total;

        foreach (var kvp in _activeSets)
        {
            var setData = kvp.Key;
            int pieceCount = kvp.Value;

            var activeBonuses = setData.GetActiveBonuses(pieceCount);
            foreach (var bonus in activeBonuses)
            {
                if (bonus.bonusType == SetBonusType.StatBonus && bonus.affectedStat == stat)
                {
                    total += bonus.bonusValue;
                }
            }
        }

        return total;
    }

    /// <summary>
    /// Obtient toutes les stats de tous les equipements.
    /// </summary>
    /// <returns>Dictionnaire des stats.</returns>
    public Dictionary<StatType, float> GetAllStatBonuses()
    {
        var stats = new Dictionary<StatType, float>();

        foreach (StatType stat in Enum.GetValues(typeof(StatType)))
        {
            float value = GetTotalStatBonus(stat);
            if (value != 0)
            {
                stats[stat] = value;
            }
        }

        return stats;
    }

    #endregion

    #region Public Methods - Sets

    /// <summary>
    /// Obtient les ensembles actifs et leur nombre de pieces.
    /// </summary>
    /// <returns>Dictionnaire des ensembles.</returns>
    public Dictionary<EquipmentSetData, int> GetActiveSets()
    {
        return _activeSets != null
            ? new Dictionary<EquipmentSetData, int>(_activeSets)
            : new Dictionary<EquipmentSetData, int>();
    }

    /// <summary>
    /// Obtient le nombre de pieces d'un ensemble equipees.
    /// </summary>
    /// <param name="setData">Ensemble a verifier.</param>
    /// <returns>Nombre de pieces.</returns>
    public int GetEquippedSetPieceCount(EquipmentSetData setData)
    {
        if (setData == null || _activeSets == null) return 0;
        return _activeSets.TryGetValue(setData, out int count) ? count : 0;
    }

    #endregion

    #region Public Methods - Loadouts

    /// <summary>
    /// Sauvegarde l'equipement actuel dans un loadout.
    /// </summary>
    /// <param name="name">Nom du loadout.</param>
    /// <returns>True si sauvegarde.</returns>
    public bool SaveLoadout(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (_savedLoadouts == null) _savedLoadouts = new List<EquipmentLoadout>();
        if (_savedLoadouts.Count >= _maxLoadouts) return false;

        var loadout = new EquipmentLoadout
        {
            name = name,
            items = new Dictionary<EquipmentSlot, EquipmentInstance>(_equippedItems ?? new Dictionary<EquipmentSlot, EquipmentInstance>())
        };

        // Remplacer si existe
        int existing = _savedLoadouts.FindIndex(l => l.name == name);
        if (existing >= 0)
        {
            _savedLoadouts[existing] = loadout;
        }
        else
        {
            _savedLoadouts.Add(loadout);
        }

        return true;
    }

    /// <summary>
    /// Charge un loadout sauvegarde.
    /// </summary>
    /// <param name="name">Nom du loadout.</param>
    /// <returns>Items desequipes.</returns>
    public List<EquipmentInstance> LoadLoadout(string name)
    {
        var unequipped = UnequipAll();

        if (_savedLoadouts == null) return unequipped;

        var loadout = _savedLoadouts.Find(l => l.name == name);
        if (loadout == null) return unequipped;

        foreach (var kvp in loadout.items)
        {
            Equip(kvp.Value);
        }

        return unequipped;
    }

    /// <summary>
    /// Obtient les noms des loadouts sauvegardes.
    /// </summary>
    /// <returns>Liste des noms.</returns>
    public List<string> GetLoadoutNames()
    {
        var names = new List<string>();

        if (_savedLoadouts != null)
        {
            foreach (var loadout in _savedLoadouts)
            {
                names.Add(loadout.name);
            }
        }

        return names;
    }

    #endregion

    #region Public Methods - Comparison

    /// <summary>
    /// Compare un item avec l'item actuellement equipe dans le meme slot.
    /// </summary>
    /// <param name="newItem">Nouvel item a comparer.</param>
    /// <returns>Dictionnaire des differences de stats.</returns>
    public Dictionary<StatType, float> CompareWithEquipped(EquipmentInstance newItem)
    {
        var diffs = new Dictionary<StatType, float>();

        if (newItem == null) return diffs;

        var currentItem = GetEquipped(newItem.Slot);

        foreach (StatType stat in Enum.GetValues(typeof(StatType)))
        {
            float newValue = newItem.GetTotalStat(stat);
            float currentValue = currentItem?.GetTotalStat(stat) ?? 0f;
            float diff = newValue - currentValue;

            if (diff != 0)
            {
                diffs[stat] = diff;
            }
        }

        return diffs;
    }

    #endregion

    #region Private Methods

    private void UpdateSetBonuses()
    {
        var newSets = new Dictionary<EquipmentSetData, int>();

        if (_equippedItems != null)
        {
            foreach (var item in _equippedItems.Values)
            {
                var setData = item?.BaseData?.equipmentSet;
                if (setData != null)
                {
                    if (!newSets.ContainsKey(setData))
                    {
                        newSets[setData] = 0;
                    }
                    newSets[setData]++;
                }
            }
        }

        // Detecter les changements et notifier
        if (_activeSets != null)
        {
            foreach (var kvp in newSets)
            {
                int oldCount = _activeSets.TryGetValue(kvp.Key, out int c) ? c : 0;
                if (oldCount != kvp.Value)
                {
                    OnSetBonusChanged?.Invoke(kvp.Key, kvp.Value);
                }
            }
        }

        _activeSets = newSets;
    }

    #endregion
}

/// <summary>
/// Loadout d'equipement sauvegarde.
/// </summary>
[System.Serializable]
public class EquipmentLoadout
{
    public string name;
    public Dictionary<EquipmentSlot, EquipmentInstance> items;
}
