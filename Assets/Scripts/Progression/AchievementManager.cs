using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire des achievements. Singleton global.
/// Suit la progression et debloque les achievements.
/// </summary>
public class AchievementManager : MonoBehaviour
{
    #region Singleton

    public static AchievementManager Instance { get; private set; }

    #endregion

    #region Events

    public event Action<AchievementData> OnAchievementUnlocked;
    public event Action<AchievementData, int, int> OnProgressUpdated; // achievement, current, required

    #endregion

    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private AchievementData[] _allAchievements;

    [Header("Audio")]
    [SerializeField] private AudioClip _defaultUnlockSound;

    #endregion

    #region Private Fields

    private Dictionary<string, AchievementProgress> _progress = new Dictionary<string, AchievementProgress>();
    private HashSet<string> _unlockedAchievements = new HashSet<string>();
    private AudioSource _audioSource;

    // Compteurs de statistiques
    private Dictionary<AchievementTrigger, int> _stats = new Dictionary<AchievementTrigger, int>();

    #endregion

    #region Properties

    public int TotalAchievements => _allAchievements?.Length ?? 0;
    public int UnlockedCount => _unlockedAchievements.Count;
    public float CompletionPercentage => TotalAchievements > 0 ? (float)UnlockedCount / TotalAchievements * 100f : 0f;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // DontDestroyOnLoad only works in Play mode
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null && Application.isPlaying)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        InitializeStats();
        InitializeProgress();
    }

    private void Start()
    {
        SubscribeToEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    #endregion

    #region Initialization

    private void InitializeStats()
    {
        foreach (AchievementTrigger trigger in Enum.GetValues(typeof(AchievementTrigger)))
        {
            _stats[trigger] = 0;
        }
    }

    private void InitializeProgress()
    {
        if (_allAchievements == null) return;

        foreach (var achievement in _allAchievements)
        {
            if (achievement != null && !string.IsNullOrEmpty(achievement.achievementId))
            {
                _progress[achievement.achievementId] = new AchievementProgress
                {
                    achievementId = achievement.achievementId,
                    currentValue = 0,
                    isUnlocked = false
                };
            }
        }
    }

    private void SubscribeToEvents()
    {
        // Combat events
        // Note: DamageCalculator n'a pas de pattern Singleton
        // Les events de combat sont geres differemment

        // Quest events
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted += OnQuestCompleted;
        }

        // Player events
        var player = GameManager.Instance?.Player;
        if (player != null)
        {
            var stats = player.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.OnLevelUp += OnPlayerLevelUp;
            }
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted -= OnQuestCompleted;
        }
    }

    #endregion

    #region Public Methods - Progress Tracking

    /// <summary>
    /// Incremente un compteur de statistique.
    /// </summary>
    public void IncrementStat(AchievementTrigger trigger, int amount = 1)
    {
        if (!_stats.ContainsKey(trigger)) return;

        _stats[trigger] += amount;
        CheckAchievementsForTrigger(trigger);
    }

    /// <summary>
    /// Definit la valeur d'un compteur.
    /// </summary>
    public void SetStat(AchievementTrigger trigger, int value)
    {
        _stats[trigger] = value;
        CheckAchievementsForTrigger(trigger);
    }

    /// <summary>
    /// Obtient la valeur d'un compteur.
    /// </summary>
    public int GetStat(AchievementTrigger trigger)
    {
        return _stats.TryGetValue(trigger, out int value) ? value : 0;
    }

    /// <summary>
    /// Met a jour un trigger custom.
    /// </summary>
    public void UpdateCustomProgress(string customId, int value)
    {
        foreach (var achievement in _allAchievements)
        {
            if (achievement.trigger == AchievementTrigger.Custom &&
                achievement.customTriggerId == customId)
            {
                UpdateProgress(achievement, value);
            }
        }
    }

    /// <summary>
    /// Debloque manuellement un achievement.
    /// </summary>
    public void UnlockAchievement(string achievementId)
    {
        var achievement = GetAchievementById(achievementId);
        if (achievement != null)
        {
            Unlock(achievement);
        }
    }

    #endregion

    #region Public Methods - Query

    /// <summary>
    /// Verifie si un achievement est debloque.
    /// </summary>
    public bool IsUnlocked(string achievementId)
    {
        return _unlockedAchievements.Contains(achievementId);
    }

    /// <summary>
    /// Obtient la progression d'un achievement.
    /// </summary>
    public AchievementProgress GetProgress(string achievementId)
    {
        return _progress.TryGetValue(achievementId, out var progress) ? progress : null;
    }

    /// <summary>
    /// Obtient un achievement par son ID.
    /// </summary>
    public AchievementData GetAchievementById(string achievementId)
    {
        if (_allAchievements == null) return null;

        foreach (var achievement in _allAchievements)
        {
            if (achievement != null && achievement.achievementId == achievementId)
            {
                return achievement;
            }
        }
        return null;
    }

    /// <summary>
    /// Obtient tous les achievements.
    /// </summary>
    public AchievementData[] GetAllAchievements()
    {
        return _allAchievements;
    }

    /// <summary>
    /// Obtient les achievements debloques.
    /// </summary>
    public List<AchievementData> GetUnlockedAchievements()
    {
        var unlocked = new List<AchievementData>();
        if (_allAchievements == null) return unlocked;

        foreach (var achievement in _allAchievements)
        {
            if (achievement != null && IsUnlocked(achievement.achievementId))
            {
                unlocked.Add(achievement);
            }
        }
        return unlocked;
    }

    /// <summary>
    /// Obtient les achievements verrouilles (visibles).
    /// </summary>
    public List<AchievementData> GetLockedAchievements(bool includeHidden = false)
    {
        var locked = new List<AchievementData>();
        if (_allAchievements == null) return locked;

        foreach (var achievement in _allAchievements)
        {
            if (achievement != null && !IsUnlocked(achievement.achievementId))
            {
                if (!achievement.isHidden || includeHidden)
                {
                    locked.Add(achievement);
                }
            }
        }
        return locked;
    }

    #endregion

    #region Public Methods - Save/Load

    /// <summary>
    /// Obtient les donnees de sauvegarde.
    /// </summary>
    public AchievementSaveData GetSaveData()
    {
        var saveData = new AchievementSaveData
        {
            unlockedIds = new List<string>(_unlockedAchievements),
            progressList = new List<AchievementProgress>(_progress.Values),
            stats = new Dictionary<AchievementTrigger, int>(_stats)
        };
        return saveData;
    }

    /// <summary>
    /// Charge les donnees de sauvegarde.
    /// </summary>
    public void LoadSaveData(AchievementSaveData saveData)
    {
        if (saveData == null) return;

        _unlockedAchievements.Clear();
        if (saveData.unlockedIds != null)
        {
            foreach (var id in saveData.unlockedIds)
            {
                _unlockedAchievements.Add(id);
            }
        }

        _progress.Clear();
        if (saveData.progressList != null)
        {
            foreach (var progress in saveData.progressList)
            {
                _progress[progress.achievementId] = progress;
            }
        }

        if (saveData.stats != null)
        {
            foreach (var kvp in saveData.stats)
            {
                _stats[kvp.Key] = kvp.Value;
            }
        }
    }

    #endregion

    #region Private Methods

    private void CheckAchievementsForTrigger(AchievementTrigger trigger)
    {
        if (_allAchievements == null) return;

        foreach (var achievement in _allAchievements)
        {
            if (achievement == null) continue;
            if (achievement.trigger != trigger) continue;
            if (IsUnlocked(achievement.achievementId)) continue;

            int currentValue = _stats[trigger];
            UpdateProgress(achievement, currentValue);
        }
    }

    private void UpdateProgress(AchievementData achievement, int currentValue)
    {
        if (IsUnlocked(achievement.achievementId)) return;
        if (!achievement.ArePrerequisitesMet(this)) return;

        if (_progress.TryGetValue(achievement.achievementId, out var progress))
        {
            progress.currentValue = currentValue;
            OnProgressUpdated?.Invoke(achievement, currentValue, achievement.requiredValue);

            if (currentValue >= achievement.requiredValue)
            {
                Unlock(achievement);
            }
        }
    }

    private void Unlock(AchievementData achievement)
    {
        if (IsUnlocked(achievement.achievementId)) return;

        _unlockedAchievements.Add(achievement.achievementId);

        if (_progress.TryGetValue(achievement.achievementId, out var progress))
        {
            progress.isUnlocked = true;
            progress.unlockTime = DateTime.Now;
        }

        // Donner les recompenses
        GiveRewards(achievement);

        // Jouer le son
        var sound = achievement.unlockSound != null ? achievement.unlockSound : _defaultUnlockSound;
        if (sound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(sound);
        }

        OnAchievementUnlocked?.Invoke(achievement);

        Debug.Log($"[AchievementManager] Achievement debloque: {achievement.title}");
    }

    private void GiveRewards(AchievementData achievement)
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        var stats = player.GetComponent<PlayerStats>();
        var inventory = player.GetComponent<Inventory>();

        if (stats != null)
        {
            if (achievement.xpReward > 0)
            {
                stats.AddExperience(achievement.xpReward);
            }
            if (achievement.goldReward > 0)
            {
                stats.AddGold(achievement.goldReward);
            }
        }

        if (inventory != null && achievement.itemReward != null)
        {
            inventory.AddItem(achievement.itemReward, achievement.itemRewardQuantity);
        }
    }

    #endregion

    #region Event Handlers

    private void OnQuestCompleted(QuestData quest)
    {
        IncrementStat(AchievementTrigger.QuestsCompleted);
    }

    private void OnPlayerLevelUp(int newLevel)
    {
        SetStat(AchievementTrigger.LevelReached, newLevel);
    }

    #endregion

    #region Convenience Methods for Common Triggers

    public void OnEnemyKilled() => IncrementStat(AchievementTrigger.EnemiesKilled);
    public void OnBossDefeated() => IncrementStat(AchievementTrigger.BossesDefeated);
    public void OnDamageDealt(int damage) => IncrementStat(AchievementTrigger.DamageDealt, damage);
    public void OnCriticalHit() => IncrementStat(AchievementTrigger.CriticalHits);
    public void OnAreaDiscovered() => IncrementStat(AchievementTrigger.AreasDiscovered);
    public void OnSecretFound() => IncrementStat(AchievementTrigger.SecretsFound);
    public void OnChestOpened() => IncrementStat(AchievementTrigger.ChestsOpened);
    public void OnItemCollected() => IncrementStat(AchievementTrigger.ItemsCollected);
    public void OnGoldEarned(int amount) => IncrementStat(AchievementTrigger.GoldEarned, amount);
    public void OnItemCrafted() => IncrementStat(AchievementTrigger.ItemsCrafted);
    public void OnRecipeLearned() => IncrementStat(AchievementTrigger.RecipesLearned);
    public void OnNPCInteracted() => IncrementStat(AchievementTrigger.NPCsInteracted);

    #endregion
}

/// <summary>
/// Progression d'un achievement.
/// </summary>
[Serializable]
public class AchievementProgress
{
    public string achievementId;
    public int currentValue;
    public bool isUnlocked;
    public DateTime unlockTime;
}

/// <summary>
/// Donnees de sauvegarde des achievements.
/// </summary>
[Serializable]
public class AchievementSaveData
{
    public List<string> unlockedIds;
    public List<AchievementProgress> progressList;
    public Dictionary<AchievementTrigger, int> stats;
}
