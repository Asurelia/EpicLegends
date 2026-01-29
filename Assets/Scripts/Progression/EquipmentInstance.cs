using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Instance d'un equipement avec stats aleatoires et modifications.
/// Represente un equipement unique dans l'inventaire du joueur.
/// </summary>
[System.Serializable]
public class EquipmentInstance
{
    #region Fields

    [SerializeField] private EquipmentData _baseData;
    [SerializeField] private string _uniqueId;
    [SerializeField] private int _enchantLevel = 0;
    [SerializeField] private List<RolledStat> _randomStats;
    [SerializeField] private List<GemData> _socketedGems;

    #endregion

    #region Properties

    /// <summary>Donnees de base de l'equipement.</summary>
    public EquipmentData BaseData => _baseData;

    /// <summary>ID unique de cette instance.</summary>
    public string UniqueId => _uniqueId;

    /// <summary>Niveau d'enchantement actuel.</summary>
    public int EnchantLevel => _enchantLevel;

    /// <summary>Stats aleatoires generees.</summary>
    public IReadOnlyList<RolledStat> RandomStats => _randomStats;

    /// <summary>Gemmes sockettees.</summary>
    public IReadOnlyList<GemData> SocketedGems => _socketedGems;

    /// <summary>Slot d'equipement.</summary>
    public EquipmentSlot Slot => _baseData?.slot ?? EquipmentSlot.MainHand;

    /// <summary>Nom affiche.</summary>
    public string DisplayName
    {
        get
        {
            if (_baseData == null) return "Unknown";
            if (_enchantLevel > 0)
            {
                return $"{_baseData.equipmentName} +{_enchantLevel}";
            }
            return _baseData.equipmentName;
        }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Cree une nouvelle instance d'equipement.
    /// </summary>
    /// <param name="data">Donnees de base.</param>
    public EquipmentInstance(EquipmentData data)
    {
        _baseData = data;
        _uniqueId = System.Guid.NewGuid().ToString();
        _randomStats = new List<RolledStat>();
        _socketedGems = new List<GemData>();

        GenerateRandomStats();
    }

    #endregion

    #region Public Methods - Stats

    /// <summary>
    /// Obtient la valeur totale d'une stat (base + random + gems + enchant).
    /// </summary>
    /// <param name="statType">Type de stat.</param>
    /// <returns>Valeur totale.</returns>
    public float GetTotalStat(StatType statType)
    {
        float total = 0f;

        // Stats de base
        if (_baseData?.baseStats != null)
        {
            foreach (var stat in _baseData.baseStats)
            {
                if (stat.statType == statType)
                {
                    total += stat.value;
                }
            }
        }

        // Stats aleatoires
        if (_randomStats != null)
        {
            foreach (var stat in _randomStats)
            {
                if (stat.statType == statType)
                {
                    total += stat.value;
                }
            }
        }

        // Bonus de gemmes
        if (_socketedGems != null)
        {
            foreach (var gem in _socketedGems)
            {
                if (gem != null && gem.bonusStat == statType)
                {
                    total += gem.bonusValue;
                }
            }
        }

        // Bonus d'enchantement
        if (_enchantLevel > 0 && _baseData != null)
        {
            total *= 1f + (_enchantLevel * _baseData.enchantBonusPerLevel);
        }

        return total;
    }

    /// <summary>
    /// Obtient toutes les stats avec leurs valeurs totales.
    /// </summary>
    /// <returns>Dictionnaire des stats.</returns>
    public Dictionary<StatType, float> GetAllStats()
    {
        var stats = new Dictionary<StatType, float>();

        foreach (StatType stat in System.Enum.GetValues(typeof(StatType)))
        {
            float value = GetTotalStat(stat);
            if (value > 0)
            {
                stats[stat] = value;
            }
        }

        return stats;
    }

    /// <summary>
    /// Obtient l'armure totale (avec enchantement).
    /// </summary>
    /// <returns>Valeur d'armure.</returns>
    public int GetTotalArmor()
    {
        if (_baseData == null) return 0;

        float armor = _baseData.armorValue;

        if (_enchantLevel > 0)
        {
            armor *= 1f + (_enchantLevel * _baseData.enchantBonusPerLevel);
        }

        return Mathf.RoundToInt(armor);
    }

    /// <summary>
    /// Obtient les degats totaux (avec enchantement).
    /// </summary>
    /// <returns>Valeur de degats.</returns>
    public int GetTotalDamage()
    {
        if (_baseData == null) return 0;

        float damage = _baseData.baseDamage;

        if (_enchantLevel > 0)
        {
            damage *= 1f + (_enchantLevel * _baseData.enchantBonusPerLevel);
        }

        return Mathf.RoundToInt(damage);
    }

    #endregion

    #region Public Methods - Enchantment

    /// <summary>
    /// Enchante l'equipement d'un niveau.
    /// </summary>
    /// <returns>True si l'enchantement a reussi.</returns>
    public bool Enchant()
    {
        if (_baseData == null) return false;
        if (_enchantLevel >= _baseData.maxEnchantLevel) return false;

        _enchantLevel++;
        return true;
    }

    /// <summary>
    /// Verifie si l'equipement peut etre enchante.
    /// </summary>
    /// <returns>True si enchantable.</returns>
    public bool CanEnchant()
    {
        return _baseData != null && _enchantLevel < _baseData.maxEnchantLevel;
    }

    /// <summary>
    /// Obtient le cout d'enchantement au niveau actuel.
    /// </summary>
    /// <returns>Cout en ressources.</returns>
    public int GetEnchantCost()
    {
        // Formule: 100 * niveau * multiplicateur_rarete
        int baseCost = 100;
        float rarityMult = _baseData?.GetRarityMultiplier() ?? 1f;

        return Mathf.RoundToInt(baseCost * (_enchantLevel + 1) * rarityMult);
    }

    #endregion

    #region Public Methods - Sockets

    /// <summary>
    /// Sockette une gemme.
    /// </summary>
    /// <param name="gem">Gemme a socketter.</param>
    /// <returns>True si reussi.</returns>
    public bool SocketGem(GemData gem)
    {
        if (gem == null) return false;
        if (_baseData == null) return false;
        if (_socketedGems == null) _socketedGems = new List<GemData>();
        if (_socketedGems.Count >= _baseData.socketCount) return false;

        _socketedGems.Add(gem);
        return true;
    }

    /// <summary>
    /// Retire une gemme d'un socket.
    /// </summary>
    /// <param name="index">Index du socket.</param>
    /// <returns>Gemme retiree ou null.</returns>
    public GemData RemoveGem(int index)
    {
        if (_socketedGems == null) return null;
        if (index < 0 || index >= _socketedGems.Count) return null;

        var gem = _socketedGems[index];
        _socketedGems.RemoveAt(index);
        return gem;
    }

    /// <summary>
    /// Obtient le nombre de sockets disponibles.
    /// </summary>
    /// <returns>Sockets libres.</returns>
    public int GetAvailableSocketCount()
    {
        if (_baseData == null) return 0;
        int used = _socketedGems?.Count ?? 0;
        return Mathf.Max(0, _baseData.socketCount - used);
    }

    #endregion

    #region Public Methods - Requirements

    /// <summary>
    /// Verifie si l'equipement peut etre utilise par un personnage.
    /// </summary>
    /// <param name="level">Niveau du personnage.</param>
    /// <param name="classType">Classe du personnage.</param>
    /// <param name="stats">Stats du personnage.</param>
    /// <returns>True si utilisable.</returns>
    public bool MeetsRequirements(int level, ClassType classType, Dictionary<StatType, int> stats)
    {
        if (_baseData == null) return false;

        // Verifier le niveau
        if (level < _baseData.requiredLevel) return false;

        // Verifier la classe
        if (_baseData.allowedClasses != null && _baseData.allowedClasses.Length > 0)
        {
            bool classAllowed = false;
            foreach (var allowed in _baseData.allowedClasses)
            {
                if (allowed == classType)
                {
                    classAllowed = true;
                    break;
                }
            }
            if (!classAllowed) return false;
        }

        // Verifier les stats requises
        if (_baseData.statRequirements != null && stats != null)
        {
            foreach (var req in _baseData.statRequirements)
            {
                if (!stats.TryGetValue(req.statType, out int value) || value < req.minValue)
                {
                    return false;
                }
            }
        }

        return true;
    }

    #endregion

    #region Public Methods - Utility

    /// <summary>
    /// Calcule le score de puissance total.
    /// </summary>
    /// <returns>Score de puissance.</returns>
    public int GetPowerScore()
    {
        int score = _baseData?.GetPowerScore() ?? 0;

        // Ajouter les stats aleatoires
        if (_randomStats != null)
        {
            foreach (var stat in _randomStats)
            {
                score += Mathf.RoundToInt(stat.value);
            }
        }

        // Bonus d'enchantement
        if (_enchantLevel > 0)
        {
            score = Mathf.RoundToInt(score * (1f + _enchantLevel * 0.1f));
        }

        // Bonus de gemmes
        if (_socketedGems != null)
        {
            score += _socketedGems.Count * 10;
        }

        return score;
    }

    /// <summary>
    /// Compare avec un autre equipement.
    /// </summary>
    /// <param name="other">Autre equipement.</param>
    /// <returns>Difference de puissance (positif = meilleur).</returns>
    public int CompareTo(EquipmentInstance other)
    {
        if (other == null) return GetPowerScore();

        return GetPowerScore() - other.GetPowerScore();
    }

    #endregion

    #region Private Methods

    private void GenerateRandomStats()
    {
        if (_baseData == null) return;
        if (_baseData.randomStatSlots <= 0) return;
        if (_baseData.possibleRandomStats == null || _baseData.possibleRandomStats.Length == 0) return;

        _randomStats = new List<RolledStat>();

        // Determiner le nombre de stats a generer
        int statsToGenerate = _baseData.randomStatSlots;

        // Bonus de rarete pour plus de stats
        if (_baseData.rarity >= EquipmentRarity.Epic)
        {
            statsToGenerate = Mathf.Min(statsToGenerate + 1, _baseData.possibleRandomStats.Length);
        }

        // Generer les stats
        var availableStats = new List<RandomStatDefinition>(_baseData.possibleRandomStats);

        for (int i = 0; i < statsToGenerate && availableStats.Count > 0; i++)
        {
            // Selection ponderee
            var selected = SelectWeightedRandom(availableStats);
            if (selected.weight > 0)
            {
                float value = Random.Range(selected.minValue, selected.maxValue);

                // Bonus de rarete
                value *= _baseData.GetRarityMultiplier();

                _randomStats.Add(new RolledStat
                {
                    statType = selected.statType,
                    value = value
                });

                // Retirer pour eviter les doublons
                availableStats.RemoveAll(s => s.statType == selected.statType);
            }
        }
    }

    private RandomStatDefinition SelectWeightedRandom(List<RandomStatDefinition> stats)
    {
        float totalWeight = 0f;
        foreach (var stat in stats)
        {
            totalWeight += stat.weight;
        }

        float random = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var stat in stats)
        {
            cumulative += stat.weight;
            if (random <= cumulative)
            {
                return stat;
            }
        }

        return stats.Count > 0 ? stats[0] : default;
    }

    #endregion
}

/// <summary>
/// Stat aleatoire generee.
/// </summary>
[System.Serializable]
public struct RolledStat
{
    public StatType statType;
    public float value;
}

/// <summary>
/// Donnees d'une gemme sockettable.
/// </summary>
[System.Serializable]
public class GemData
{
    public string gemName;
    public StatType bonusStat;
    public float bonusValue;
    public Sprite icon;
}
