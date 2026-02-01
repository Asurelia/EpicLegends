using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire du système New Game Plus.
/// Gère les cycles NG+, les bonus cumulatifs et les déblocages.
/// </summary>
public class NGPlusManager : MonoBehaviour
{
    #region Singleton

    private static NGPlusManager _instance;
    public static NGPlusManager Instance
    {
        get => _instance;
        private set => _instance = value;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    #endregion

    #region Events

    /// <summary>Déclenché quand un nouveau cycle NG+ commence.</summary>
    public event Action<int> OnNGPlusCycleStarted;

    /// <summary>Déclenché quand un bonus NG+ est appliqué.</summary>
    public event Action<NGPlusBonus> OnBonusApplied;

    #endregion

    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private int _maxCycle = 7;
    [SerializeField] private float _enemyHealthScalingPerCycle = 0.25f;
    [SerializeField] private float _enemyDamageScalingPerCycle = 0.15f;
    [SerializeField] private float _xpBonusPerCycle = 0.1f;
    [SerializeField] private float _dropRateBonusPerCycle = 0.05f;

    [Header("Retention Settings")]
    [SerializeField] private bool _retainLevel = true;
    [SerializeField] private bool _retainEquipment = true;
    [SerializeField] private bool _retainSkills = true;
    [SerializeField] private bool _retainAchievements = true;
    [SerializeField] private float _retainedGoldPercent = 0.5f;

    [Header("Unlocks")]
    [SerializeField] private NGPlusUnlock[] _cycleUnlocks;

    #endregion

    #region Private Fields

    private int _currentCycle = 0;
    private HashSet<string> _unlockedFeatures = new HashSet<string>();
    private List<NGPlusBonus> _activeBonus = new List<NGPlusBonus>();

    #endregion

    #region Properties

    /// <summary>Cycle NG+ actuel (0 = première partie).</summary>
    public int CurrentCycle => _currentCycle;

    /// <summary>Est en mode NG+?</summary>
    public bool IsNGPlus => _currentCycle > 0;

    /// <summary>Peut commencer un nouveau cycle?</summary>
    public bool CanStartNewCycle => _currentCycle < _maxCycle;

    /// <summary>Cycle maximum atteint?</summary>
    public bool IsMaxCycle => _currentCycle >= _maxCycle;

    /// <summary>Multiplicateur de santé des ennemis.</summary>
    public float EnemyHealthMultiplier => 1f + (_currentCycle * _enemyHealthScalingPerCycle);

    /// <summary>Multiplicateur de dégâts des ennemis.</summary>
    public float EnemyDamageMultiplier => 1f + (_currentCycle * _enemyDamageScalingPerCycle);

    /// <summary>Multiplicateur d'XP.</summary>
    public float XPMultiplier => 1f + (_currentCycle * _xpBonusPerCycle);

    /// <summary>Multiplicateur de drop rate.</summary>
    public float DropRateMultiplier => 1f + (_currentCycle * _dropRateBonusPerCycle);

    #endregion

    #region Public Methods - NG+ Cycle

    /// <summary>
    /// Démarre un nouveau cycle NG+.
    /// </summary>
    public bool StartNewCycle()
    {
        if (!CanStartNewCycle) return false;

        // Sauvegarder les données à retenir
        var retainedData = CollectRetainedData();

        _currentCycle++;

        // Appliquer les données retenues
        ApplyRetainedData(retainedData);

        // Débloquer les fonctionnalités du nouveau cycle
        UnlockCycleFeatures(_currentCycle);

        // Réinitialiser la progression du monde
        ResetWorldProgression();

        OnNGPlusCycleStarted?.Invoke(_currentCycle);

        Debug.Log($"[NGPlusManager] Started NG+{_currentCycle}");
        return true;
    }

    /// <summary>
    /// Vérifie si une fonctionnalité est débloquée.
    /// </summary>
    public bool IsFeatureUnlocked(string featureId)
    {
        return _unlockedFeatures.Contains(featureId);
    }

    /// <summary>
    /// Obtient le modificateur de difficulté global.
    /// </summary>
    public float GetDifficultyMultiplier()
    {
        return 1f + (_currentCycle * 0.2f);
    }

    /// <summary>
    /// Applique les modificateurs NG+ aux stats d'un ennemi.
    /// </summary>
    public void ApplyToEnemy(EnemyAI enemy)
    {
        if (enemy == null || _currentCycle == 0) return;

        enemy.ApplyWaveModifiers(
            EnemyHealthMultiplier,
            EnemyDamageMultiplier,
            1f // Speed unchanged
        );
    }

    /// <summary>
    /// Calcule l'XP avec bonus NG+.
    /// </summary>
    public int CalculateXP(int baseXP)
    {
        return Mathf.RoundToInt(baseXP * XPMultiplier);
    }

    /// <summary>
    /// Calcule le drop rate avec bonus NG+.
    /// </summary>
    public float CalculateDropRate(float baseRate)
    {
        return baseRate * DropRateMultiplier;
    }

    #endregion

    #region Public Methods - Bonuses

    /// <summary>
    /// Ajoute un bonus actif.
    /// </summary>
    public void AddBonus(NGPlusBonus bonus)
    {
        if (bonus == null) return;
        _activeBonus.Add(bonus);
        OnBonusApplied?.Invoke(bonus);
    }

    /// <summary>
    /// Obtient tous les bonus actifs.
    /// </summary>
    public List<NGPlusBonus> GetActiveBonus()
    {
        return new List<NGPlusBonus>(_activeBonus);
    }

    /// <summary>
    /// Calcule le bonus total d'un type.
    /// </summary>
    public float GetTotalBonusForStat(StatType statType)
    {
        float total = 0f;
        foreach (var bonus in _activeBonus)
        {
            if (bonus.affectedStat == statType)
            {
                total += bonus.value;
            }
        }
        return total;
    }

    #endregion

    #region Public Methods - Save/Load

    /// <summary>
    /// Obtient les données de sauvegarde.
    /// </summary>
    public NGPlusSaveData GetSaveData()
    {
        return new NGPlusSaveData
        {
            currentCycle = _currentCycle,
            unlockedFeatures = new List<string>(_unlockedFeatures),
            activeBonuses = new List<NGPlusBonus>(_activeBonus)
        };
    }

    /// <summary>
    /// Charge les données de sauvegarde.
    /// </summary>
    public void LoadSaveData(NGPlusSaveData data)
    {
        if (data == null) return;

        _currentCycle = data.currentCycle;

        _unlockedFeatures.Clear();
        if (data.unlockedFeatures != null)
        {
            foreach (var feature in data.unlockedFeatures)
            {
                _unlockedFeatures.Add(feature);
            }
        }

        _activeBonus.Clear();
        if (data.activeBonuses != null)
        {
            _activeBonus.AddRange(data.activeBonuses);
        }
    }

    #endregion

    #region Private Methods

    private NGPlusRetainedData CollectRetainedData()
    {
        var data = new NGPlusRetainedData();
        var player = GameManager.Instance?.Player;

        if (player == null) return data;

        // Niveau
        if (_retainLevel)
        {
            var stats = player.GetComponent<PlayerStats>();
            if (stats != null)
            {
                data.playerLevel = stats.Level;
                data.experience = stats.Experience;
            }
        }

        // Or
        var inventory = player.GetComponent<Inventory>();
        if (inventory != null)
        {
            data.gold = Mathf.RoundToInt(inventory.Gold * _retainedGoldPercent);

            // Équipement
            if (_retainEquipment)
            {
                data.inventoryItems = new List<ItemInstance>(inventory.GetAllItems());
            }
        }

        // Skills
        if (_retainSkills && SkillTreeManager.Instance != null)
        {
            data.skillTreeData = SkillTreeManager.Instance.GetSaveData();
        }

        // Achievements
        if (_retainAchievements && AchievementManager.Instance != null)
        {
            data.achievementData = AchievementManager.Instance.GetSaveData();
        }

        return data;
    }

    private void ApplyRetainedData(NGPlusRetainedData data)
    {
        if (data == null) return;

        var player = GameManager.Instance?.Player;
        if (player == null) return;

        // Restaurer le niveau (simplifié - en pratique, on utiliserait LoadSaveData)
        Debug.Log($"[NGPlusManager] Retained: Level {data.playerLevel}, {data.gold} gold");

        // Restaurer l'or
        var inventory = player.GetComponent<Inventory>();
        if (inventory != null)
        {
            inventory.AddGold(data.gold);
        }

        // Skills
        if (_retainSkills && data.skillTreeData != null && SkillTreeManager.Instance != null)
        {
            SkillTreeManager.Instance.LoadSaveData(data.skillTreeData);
        }

        // Achievements
        if (_retainAchievements && data.achievementData != null && AchievementManager.Instance != null)
        {
            AchievementManager.Instance.LoadSaveData(data.achievementData);
        }
    }

    private void UnlockCycleFeatures(int cycle)
    {
        if (_cycleUnlocks == null) return;

        foreach (var unlock in _cycleUnlocks)
        {
            if (unlock != null && unlock.requiredCycle <= cycle)
            {
                if (!string.IsNullOrEmpty(unlock.featureId))
                {
                    _unlockedFeatures.Add(unlock.featureId);
                    Debug.Log($"[NGPlusManager] Unlocked: {unlock.featureName}");
                }

                // Appliquer le bonus
                if (unlock.bonus != null)
                {
                    AddBonus(unlock.bonus);
                }
            }
        }
    }

    private void ResetWorldProgression()
    {
        // Réinitialiser les quêtes
        if (QuestManager.Instance != null)
        {
            // Note: Implémenter QuestManager.ResetAllQuests() si nécessaire
            Debug.Log("[NGPlusManager] Quests would be reset here");
        }

        // Réinitialiser les zones débloquées
        // Note: Implémenter le système de zones si nécessaire

        // Réinitialiser les ennemis
        // Note: Les ennemis sont re-spawnés au chargement de scène
    }

    #endregion
}

/// <summary>
/// Données retenues entre les cycles NG+.
/// </summary>
public class NGPlusRetainedData
{
    public int playerLevel;
    public int experience;
    public int gold;
    public List<ItemInstance> inventoryItems;
    public SkillTreeSaveData skillTreeData;
    public AchievementSaveData achievementData;
}

/// <summary>
/// Bonus NG+.
/// </summary>
[Serializable]
public class NGPlusBonus
{
    public string bonusName;
    public StatType affectedStat;
    public float value;
    public bool isPercentage;
}

/// <summary>
/// Déblocage NG+.
/// </summary>
[Serializable]
public class NGPlusUnlock
{
    public int requiredCycle;
    public string featureId;
    public string featureName;
    public string description;
    public NGPlusBonus bonus;
}

/// <summary>
/// Données de sauvegarde NG+.
/// </summary>
[Serializable]
public class NGPlusSaveData
{
    public int currentCycle;
    public List<string> unlockedFeatures;
    public List<NGPlusBonus> activeBonuses;
}
