using UnityEngine;

/// <summary>
/// Types de declencheurs d'achievements.
/// </summary>
public enum AchievementTrigger
{
    // Combat
    EnemiesKilled,
    BossesDefeated,
    DamageDealt,
    CriticalHits,
    ComboReached,
    DeathsCount,

    // Progression
    LevelReached,
    QuestsCompleted,
    SkillsUnlocked,
    ClassesUnlocked,

    // Exploration
    AreasDiscovered,
    SecretsFound,
    DistanceTraveled,
    ChestsOpened,

    // Collection
    ItemsCollected,
    GoldEarned,
    RareItemsFound,
    SetsCompleted,

    // Crafting
    ItemsCrafted,
    RecipesLearned,
    UpgradesPerformed,

    // Gathering
    ResourcesGathered,

    // Territory
    TerritoriesCaptured,

    // Creatures
    CreaturesCaptured,
    CreaturesTamed,

    // Social
    NPCsInteracted,
    TradesCompleted,

    // Special
    PlayTime,
    Custom
}

/// <summary>
/// Rarete des achievements.
/// </summary>
public enum AchievementRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

/// <summary>
/// Donnees d'un achievement.
/// </summary>
[CreateAssetMenu(fileName = "NewAchievement", menuName = "EpicLegends/Achievement")]
public class AchievementData : ScriptableObject
{
    [Header("Identification")]
    public string achievementId;
    public string title;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;
    public AchievementRarity rarity = AchievementRarity.Common;

    [Header("Unlock Condition")]
    public AchievementTrigger trigger;
    public int requiredValue = 1;
    public string customTriggerId;

    [Header("Progression")]
    public bool isHidden = false;
    public bool hasProgressBar = true;

    [Header("Rewards")]
    public int xpReward = 0;
    public int goldReward = 0;
    public ItemData itemReward;
    public int itemRewardQuantity = 1;

    [Header("Prerequisites")]
    public AchievementData[] prerequisites;

    [Header("Audio/Visual")]
    public AudioClip unlockSound;

    /// <summary>
    /// Verifie si les prerequis sont remplis.
    /// </summary>
    public bool ArePrerequisitesMet(AchievementManager manager)
    {
        if (prerequisites == null || prerequisites.Length == 0)
            return true;

        foreach (var prereq in prerequisites)
        {
            if (prereq != null && !manager.IsUnlocked(prereq.achievementId))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Obtient la couleur de la rarete.
    /// </summary>
    public Color GetRarityColor()
    {
        return rarity switch
        {
            AchievementRarity.Common => Color.white,
            AchievementRarity.Uncommon => Color.green,
            AchievementRarity.Rare => Color.blue,
            AchievementRarity.Epic => new Color(0.6f, 0f, 0.8f),
            AchievementRarity.Legendary => new Color(1f, 0.5f, 0f),
            _ => Color.gray
        };
    }
}
