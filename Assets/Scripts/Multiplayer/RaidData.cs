using UnityEngine;

/// <summary>
/// Donnees d'un raid.
/// Definit la structure, boss et recompenses d'un raid.
/// </summary>
[CreateAssetMenu(fileName = "NewRaid", menuName = "EpicLegends/Multiplayer/Raid Data")]
public class RaidData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("ID unique du raid")]
    public string raidId;

    [Tooltip("Nom du raid")]
    public string raidName;

    [Tooltip("Description")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Icone")]
    public Sprite raidIcon;

    [Tooltip("Image de fond")]
    public Sprite backgroundImage;

    #endregion

    #region Requirements

    [Header("Pre-requis")]
    [Tooltip("Nombre de joueurs requis")]
    [Range(2, 4)]
    public int requiredPlayers = 4;

    [Tooltip("Niveau recommande")]
    public int recommendedLevel = 50;

    [Tooltip("Niveau minimum")]
    public int minimumLevel = 40;

    [Tooltip("Quete prerequise")]
    public QuestData prerequisiteQuest;

    #endregion

    #region Difficulty

    [Header("Difficulte")]
    [Tooltip("Difficultes disponibles")]
    public RaidDifficultyConfig[] difficulties;

    [Tooltip("Difficulte par defaut")]
    public RaidDifficulty defaultDifficulty = RaidDifficulty.Normal;

    #endregion

    #region Structure

    [Header("Structure")]
    [Tooltip("Scene du raid")]
    public string sceneToLoad;

    [Tooltip("Boss du raid")]
    public RaidBossData[] bosses;

    [Tooltip("Duree estimee (minutes)")]
    public int estimatedDuration = 60;

    [Tooltip("Checkpoints")]
    public int checkpointCount = 2;

    #endregion

    #region Rewards

    [Header("Recompenses")]
    [Tooltip("XP de completion")]
    public int completionXP = 2000;

    [Tooltip("Table de loot")]
    public LootTable lootTable;

    [Tooltip("Recompenses garanties")]
    public ItemData[] guaranteedRewards;

    [Tooltip("Recompense premiere completion")]
    public ItemData firstClearReward;

    #endregion

    #region Lockout

    [Header("Lockout")]
    [Tooltip("Type de lockout")]
    public RaidLockoutType lockoutType = RaidLockoutType.Weekly;

    [Tooltip("Jour de reset (0=Dimanche)")]
    [Range(0, 6)]
    public int resetDayOfWeek = 2; // Mardi

    [Tooltip("Heure de reset (UTC)")]
    [Range(0, 23)]
    public int resetHourUTC = 4;

    #endregion

    #region Mechanics

    [Header("Mecaniques")]
    [Tooltip("Permet le respawn?")]
    public bool allowRespawn = true;

    [Tooltip("Wipes = fin du raid?")]
    public bool wipeEndsRaid = false;

    [Tooltip("Nombre max de wipes")]
    public int maxWipes = 3;

    [Tooltip("Penalite de temps par wipe (secondes)")]
    public float wipePenalty = 60f;

    #endregion

    #region Public Methods

    /// <summary>
    /// Obtient la configuration de difficulte.
    /// </summary>
    /// <param name="difficulty">Difficulte.</param>
    /// <returns>Configuration ou null.</returns>
    public RaidDifficultyConfig? GetDifficultyConfig(RaidDifficulty difficulty)
    {
        if (difficulties == null) return null;

        foreach (var config in difficulties)
        {
            if (config.difficulty == difficulty) return config;
        }

        return null;
    }

    /// <summary>
    /// Verifie si un joueur peut entrer.
    /// </summary>
    /// <param name="playerLevel">Niveau du joueur.</param>
    /// <returns>True si autorise.</returns>
    public bool CanPlayerEnter(int playerLevel)
    {
        return playerLevel >= minimumLevel;
    }

    /// <summary>
    /// Calcule les recompenses scalees.
    /// </summary>
    /// <param name="difficulty">Difficulte.</param>
    /// <returns>Multiplicateur.</returns>
    public float GetRewardMultiplier(RaidDifficulty difficulty)
    {
        var config = GetDifficultyConfig(difficulty);
        return config?.rewardMultiplier ?? 1f;
    }

    #endregion
}

/// <summary>
/// Niveaux de difficulte de raid.
/// </summary>
public enum RaidDifficulty
{
    /// <summary>Normal.</summary>
    Normal,

    /// <summary>Heroique.</summary>
    Heroic,

    /// <summary>Mythique.</summary>
    Mythic
}

/// <summary>
/// Configuration de difficulte de raid.
/// </summary>
[System.Serializable]
public struct RaidDifficultyConfig
{
    public RaidDifficulty difficulty;
    public string displayName;
    public float healthMultiplier;
    public float damageMultiplier;
    public float rewardMultiplier;
    public int minimumLevel;
    public bool requiresPreviousDifficulty;
    public Color difficultyColor;
}

/// <summary>
/// Donnees d'un boss de raid.
/// </summary>
[System.Serializable]
public class RaidBossData
{
    [Header("Identification")]
    public string bossId;
    public string bossName;
    public string description;
    public Sprite bossPortrait;
    public GameObject bossPrefab;

    [Header("Stats")]
    public int baseHealth;
    public int baseDamage;
    public int baseArmor;

    [Header("Phases")]
    public RaidBossPhase[] phases;

    [Header("Recompenses")]
    public LootTable bossLoot;
    public ItemData[] guaranteedDrops;

    [Header("Mecaniques")]
    public RaidMechanic[] mechanics;
    public float enrageTimer = 600f;
}

/// <summary>
/// Phase d'un boss de raid.
/// </summary>
[System.Serializable]
public struct RaidBossPhase
{
    public string phaseName;
    public float healthThreshold; // Pourcentage (0-1)
    public string[] abilities;
    public float damageMultiplier;
    public float attackSpeedMultiplier;
    public GameObject phaseEffect;
    public AudioClip phaseMusic;
    public string phaseAnnouncement;
}

/// <summary>
/// Mecanique de raid.
/// </summary>
[System.Serializable]
public struct RaidMechanic
{
    public string mechanicId;
    public string mechanicName;
    public string description;
    public RaidMechanicType type;
    public float interval;
    public float duration;
    public int targetCount;
    public float damage;
    public GameObject visualIndicator;
}

/// <summary>
/// Types de mecaniques de raid.
/// </summary>
public enum RaidMechanicType
{
    /// <summary>Zone a eviter.</summary>
    AvoidZone,

    /// <summary>Zone safe a rejoindre.</summary>
    SafeZone,

    /// <summary>Stack ensemble.</summary>
    StackUp,

    /// <summary>Se disperser.</summary>
    SpreadOut,

    /// <summary>Interrompre le cast.</summary>
    Interrupt,

    /// <summary>Tuer les adds.</summary>
    KillAdds,

    /// <summary>Soigner/Dispel.</summary>
    HealDispel,

    /// <summary>Kiter le boss.</summary>
    Kite,

    /// <summary>Tank swap.</summary>
    TankSwap
}

/// <summary>
/// Types de lockout de raid.
/// </summary>
public enum RaidLockoutType
{
    /// <summary>Reset quotidien.</summary>
    Daily,

    /// <summary>Reset hebdomadaire.</summary>
    Weekly,

    /// <summary>Pas de reset (une fois par personnage).</summary>
    Never
}
