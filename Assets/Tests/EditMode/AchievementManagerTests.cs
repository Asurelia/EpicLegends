using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Tests pour le AchievementManager.
/// </summary>
[TestFixture]
public class AchievementManagerTests
{
    private GameObject _managerObj;
    private AchievementManager _manager;

    [SetUp]
    public void SetUp()
    {
        // Reset singleton
        var instanceProp = typeof(AchievementManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        instanceProp?.SetValue(null, null);

        _managerObj = new GameObject("AchievementManager");
        _manager = _managerObj.AddComponent<AchievementManager>();

        // In EditMode, Awake is not automatically called, so we initialize manually
        instanceProp?.SetValue(null, _manager);

        // Initialize _stats dictionary
        var statsField = typeof(AchievementManager).GetField("_stats", BindingFlags.NonPublic | BindingFlags.Instance);
        var stats = new System.Collections.Generic.Dictionary<AchievementTrigger, int>();
        foreach (AchievementTrigger trigger in System.Enum.GetValues(typeof(AchievementTrigger)))
        {
            stats[trigger] = 0;
        }
        statsField?.SetValue(_manager, stats);

        // Initialize _progress dictionary
        var progressField = typeof(AchievementManager).GetField("_progress", BindingFlags.NonPublic | BindingFlags.Instance);
        progressField?.SetValue(_manager, new System.Collections.Generic.Dictionary<string, AchievementProgress>());

        // Initialize _unlockedAchievements set
        var unlockedField = typeof(AchievementManager).GetField("_unlockedAchievements", BindingFlags.NonPublic | BindingFlags.Instance);
        unlockedField?.SetValue(_manager, new System.Collections.Generic.HashSet<string>());
    }

    [TearDown]
    public void TearDown()
    {
        // Reset singleton
        var instanceProp = typeof(AchievementManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        instanceProp?.SetValue(null, null);

        if (_managerObj != null)
        {
            Object.DestroyImmediate(_managerObj);
        }
    }

    [Test]
    public void AchievementManager_Singleton_IsSet()
    {
        Assert.IsNotNull(AchievementManager.Instance);
        Assert.AreEqual(_manager, AchievementManager.Instance);
    }

    [Test]
    public void AchievementManager_InitialState_ZeroCounts()
    {
        Assert.AreEqual(0, _manager.UnlockedCount);
    }

    [Test]
    public void AchievementManager_InitialState_ZeroCompletion()
    {
        Assert.AreEqual(0f, _manager.CompletionPercentage);
    }

    [Test]
    public void AchievementManager_IsUnlocked_ReturnsFalse_ForUnknownId()
    {
        bool unlocked = _manager.IsUnlocked("unknown_achievement");
        Assert.IsFalse(unlocked);
    }

    [Test]
    public void AchievementManager_GetProgress_ReturnsNull_ForUnknownId()
    {
        var progress = _manager.GetProgress("unknown_achievement");
        Assert.IsNull(progress);
    }

    [Test]
    public void AchievementManager_GetAchievementById_ReturnsNull_WithNullArray()
    {
        var achievement = _manager.GetAchievementById("test");
        Assert.IsNull(achievement);
    }

    [Test]
    public void AchievementManager_GetStat_ReturnsZero_ForNewStat()
    {
        int value = _manager.GetStat(AchievementTrigger.EnemiesKilled);
        Assert.AreEqual(0, value);
    }

    [Test]
    public void AchievementManager_IncrementStat_IncreasesByOne()
    {
        _manager.IncrementStat(AchievementTrigger.EnemiesKilled);
        int value = _manager.GetStat(AchievementTrigger.EnemiesKilled);
        Assert.AreEqual(1, value);
    }

    [Test]
    public void AchievementManager_IncrementStat_IncreasesByAmount()
    {
        _manager.IncrementStat(AchievementTrigger.DamageDealt, 100);
        int value = _manager.GetStat(AchievementTrigger.DamageDealt);
        Assert.AreEqual(100, value);
    }

    [Test]
    public void AchievementManager_SetStat_SetsValue()
    {
        _manager.SetStat(AchievementTrigger.LevelReached, 10);
        int value = _manager.GetStat(AchievementTrigger.LevelReached);
        Assert.AreEqual(10, value);
    }

    [Test]
    public void AchievementManager_GetSaveData_ReturnsValidData()
    {
        var saveData = _manager.GetSaveData();

        Assert.IsNotNull(saveData);
        Assert.IsNotNull(saveData.unlockedIds);
        Assert.IsNotNull(saveData.progressList);
        Assert.IsNotNull(saveData.stats);
    }

    [Test]
    public void AchievementManager_LoadSaveData_HandlesNull()
    {
        // Should not throw
        _manager.LoadSaveData(null);
    }

    [Test]
    public void AchievementManager_LoadSaveData_RestoresStats()
    {
        var saveData = new AchievementSaveData
        {
            unlockedIds = new System.Collections.Generic.List<string>(),
            progressList = new System.Collections.Generic.List<AchievementProgress>(),
            stats = new System.Collections.Generic.Dictionary<AchievementTrigger, int>
            {
                { AchievementTrigger.EnemiesKilled, 50 },
                { AchievementTrigger.QuestsCompleted, 10 }
            }
        };

        _manager.LoadSaveData(saveData);

        Assert.AreEqual(50, _manager.GetStat(AchievementTrigger.EnemiesKilled));
        Assert.AreEqual(10, _manager.GetStat(AchievementTrigger.QuestsCompleted));
    }

    [Test]
    public void AchievementManager_OnEnemyKilled_IncrementsStat()
    {
        _manager.OnEnemyKilled();
        Assert.AreEqual(1, _manager.GetStat(AchievementTrigger.EnemiesKilled));
    }

    [Test]
    public void AchievementManager_OnBossDefeated_IncrementsStat()
    {
        _manager.OnBossDefeated();
        Assert.AreEqual(1, _manager.GetStat(AchievementTrigger.BossesDefeated));
    }

    [Test]
    public void AchievementManager_OnDamageDealt_IncrementsByAmount()
    {
        _manager.OnDamageDealt(250);
        Assert.AreEqual(250, _manager.GetStat(AchievementTrigger.DamageDealt));
    }

    [Test]
    public void AchievementManager_OnCriticalHit_IncrementsStat()
    {
        _manager.OnCriticalHit();
        Assert.AreEqual(1, _manager.GetStat(AchievementTrigger.CriticalHits));
    }

    [Test]
    public void AchievementManager_OnAreaDiscovered_IncrementsStat()
    {
        _manager.OnAreaDiscovered();
        Assert.AreEqual(1, _manager.GetStat(AchievementTrigger.AreasDiscovered));
    }

    [Test]
    public void AchievementManager_OnSecretFound_IncrementsStat()
    {
        _manager.OnSecretFound();
        Assert.AreEqual(1, _manager.GetStat(AchievementTrigger.SecretsFound));
    }

    [Test]
    public void AchievementManager_OnChestOpened_IncrementsStat()
    {
        _manager.OnChestOpened();
        Assert.AreEqual(1, _manager.GetStat(AchievementTrigger.ChestsOpened));
    }

    [Test]
    public void AchievementManager_OnItemCollected_IncrementsStat()
    {
        _manager.OnItemCollected();
        Assert.AreEqual(1, _manager.GetStat(AchievementTrigger.ItemsCollected));
    }

    [Test]
    public void AchievementManager_OnGoldEarned_IncrementsByAmount()
    {
        _manager.OnGoldEarned(1000);
        Assert.AreEqual(1000, _manager.GetStat(AchievementTrigger.GoldEarned));
    }

    [Test]
    public void AchievementManager_OnItemCrafted_IncrementsStat()
    {
        _manager.OnItemCrafted();
        Assert.AreEqual(1, _manager.GetStat(AchievementTrigger.ItemsCrafted));
    }

    [Test]
    public void AchievementManager_OnRecipeLearned_IncrementsStat()
    {
        _manager.OnRecipeLearned();
        Assert.AreEqual(1, _manager.GetStat(AchievementTrigger.RecipesLearned));
    }

    [Test]
    public void AchievementManager_OnNPCInteracted_IncrementsStat()
    {
        _manager.OnNPCInteracted();
        Assert.AreEqual(1, _manager.GetStat(AchievementTrigger.NPCsInteracted));
    }

    [Test]
    public void AchievementManager_GetUnlockedAchievements_ReturnsEmptyList_WhenNoAchievements()
    {
        var unlocked = _manager.GetUnlockedAchievements();
        Assert.IsNotNull(unlocked);
        Assert.AreEqual(0, unlocked.Count);
    }

    [Test]
    public void AchievementManager_GetLockedAchievements_ReturnsEmptyList_WhenNoAchievements()
    {
        var locked = _manager.GetLockedAchievements();
        Assert.IsNotNull(locked);
        Assert.AreEqual(0, locked.Count);
    }
}
