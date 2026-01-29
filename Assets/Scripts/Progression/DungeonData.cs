using UnityEngine;

/// <summary>
/// Donnees d'un donjon.
/// Definit la structure, difficulte et recompenses d'un donjon.
/// </summary>
[CreateAssetMenu(fileName = "NewDungeon", menuName = "EpicLegends/Progression/Dungeon Data")]
public class DungeonData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("ID unique du donjon")]
    public string dungeonId;

    [Tooltip("Nom du donjon")]
    public string dungeonName;

    [Tooltip("Description")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Icone")]
    public Sprite dungeonIcon;

    [Tooltip("Image de fond")]
    public Sprite backgroundImage;

    #endregion

    #region Level

    [Header("Niveau")]
    [Tooltip("Niveau recommande")]
    public int recommendedLevel = 10;

    [Tooltip("Niveau minimum")]
    public int minimumLevel = 1;

    [Tooltip("Joueurs maximum")]
    [Range(1, 4)]
    public int maxPlayers = 4;

    #endregion

    #region Difficulty

    [Header("Difficulte")]
    [Tooltip("Difficultes disponibles")]
    public DungeonDifficultyConfig[] difficulties;

    [Tooltip("Difficulte par defaut")]
    public DungeonDifficulty defaultDifficulty = DungeonDifficulty.Normal;

    #endregion

    #region Structure

    [Header("Structure")]
    [Tooltip("Scene du donjon")]
    public string sceneToLoad;

    [Tooltip("Etages du donjon")]
    public DungeonFloor[] floors;

    [Tooltip("Duree estimee (minutes)")]
    public int estimatedDuration = 30;

    [Tooltip("Est procedurale?")]
    public bool isProcedural = false;

    #endregion

    #region Bosses

    [Header("Boss")]
    [Tooltip("Boss final")]
    public BossData finalBoss;

    [Tooltip("Mini-boss")]
    public BossData[] miniBosses;

    #endregion

    #region Rewards

    [Header("Recompenses")]
    [Tooltip("Table de loot")]
    public LootTable lootTable;

    [Tooltip("XP de completion")]
    public int completionXP = 500;

    [Tooltip("Or de completion")]
    public int completionGold = 100;

    [Tooltip("Recompenses garanties")]
    public ItemData[] guaranteedRewards;

    #endregion

    #region Reset

    [Header("Reset")]
    [Tooltip("Type de reset")]
    public DungeonResetType resetType = DungeonResetType.Daily;

    [Tooltip("Tentatives par reset")]
    public int attemptsPerReset = 3;

    [Tooltip("Heure de reset (UTC)")]
    public int resetHourUTC = 4;

    #endregion

    #region Mechanics

    [Header("Mecaniques")]
    [Tooltip("Puzzles dans le donjon")]
    public PuzzleData[] puzzles;

    [Tooltip("Checkpoints")]
    public int checkpointCount = 0;

    [Tooltip("Respawn autorise")]
    public bool allowRespawn = true;

    [Tooltip("Cout de respawn")]
    public int respawnCost = 0;

    #endregion

    #region Public Methods

    /// <summary>
    /// Obtient la configuration de difficulte.
    /// </summary>
    /// <param name="difficulty">Niveau de difficulte.</param>
    /// <returns>Configuration ou null.</returns>
    public DungeonDifficultyConfig? GetDifficultyConfig(DungeonDifficulty difficulty)
    {
        if (difficulties == null) return null;

        foreach (var config in difficulties)
        {
            if (config.difficulty == difficulty) return config;
        }

        return null;
    }

    /// <summary>
    /// Verifie si une configuration existe pour une difficulte.
    /// </summary>
    /// <param name="difficulty">Niveau de difficulte.</param>
    /// <param name="config">Configuration trouvee.</param>
    /// <returns>True si trouvee.</returns>
    public bool TryGetDifficultyConfig(DungeonDifficulty difficulty, out DungeonDifficultyConfig config)
    {
        config = default;
        if (difficulties == null) return false;

        foreach (var c in difficulties)
        {
            if (c.difficulty == difficulty)
            {
                config = c;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Calcule le niveau d'ennemi scale.
    /// </summary>
    /// <param name="baseLevel">Niveau de base.</param>
    /// <param name="difficulty">Difficulte.</param>
    /// <returns>Niveau scale.</returns>
    public int GetScaledEnemyLevel(int baseLevel, DungeonDifficulty difficulty)
    {
        if (TryGetDifficultyConfig(difficulty, out var config))
        {
            return Mathf.RoundToInt(baseLevel * config.levelScale);
        }
        return baseLevel;
    }

    /// <summary>
    /// Calcule les recompenses scalees.
    /// </summary>
    /// <param name="baseReward">Recompense de base.</param>
    /// <param name="difficulty">Difficulte.</param>
    /// <returns>Recompense scalee.</returns>
    public int GetScaledReward(int baseReward, DungeonDifficulty difficulty)
    {
        if (TryGetDifficultyConfig(difficulty, out var config))
        {
            return Mathf.RoundToInt(baseReward * config.rewardScale);
        }
        return baseReward;
    }

    #endregion
}

/// <summary>
/// Niveaux de difficulte de donjon.
/// </summary>
public enum DungeonDifficulty
{
    /// <summary>Normal.</summary>
    Normal,

    /// <summary>Difficile.</summary>
    Hard,

    /// <summary>Cauchemar.</summary>
    Nightmare,

    /// <summary>Infernal (NG+ seulement).</summary>
    Inferno
}

/// <summary>
/// Configuration de difficulte.
/// </summary>
[System.Serializable]
public struct DungeonDifficultyConfig
{
    public DungeonDifficulty difficulty;
    public string displayName;
    public float levelScale;
    public float healthScale;
    public float damageScale;
    public float rewardScale;
    public float dropRateBonus;
    public int requiredLevel;
    public bool requiresCompletion; // Doit avoir complete la difficulte precedente
}

/// <summary>
/// Etage de donjon.
/// </summary>
[System.Serializable]
public struct DungeonFloor
{
    public string floorName;
    public int floorNumber;
    public EnemySpawnData[] enemies;
    public TreasureData[] treasures;
    public bool hasBoss;
    public BossData floorBoss;
}

/// <summary>
/// Donnees de boss.
/// </summary>
[System.Serializable]
public class BossData
{
    public string bossName;
    public GameObject bossPrefab;
    public int baseHealth;
    public int baseDamage;
    public LootTable bossLoot;
    public BossPhase[] phases;
}

/// <summary>
/// Phase de boss.
/// </summary>
[System.Serializable]
public struct BossPhase
{
    public string phaseName;
    public float healthThreshold; // Pourcentage de vie pour declencher
    public string[] abilities;
    public float damageMultiplier;
}

/// <summary>
/// Type de reset de donjon.
/// </summary>
public enum DungeonResetType
{
    /// <summary>Reset quotidien.</summary>
    Daily,

    /// <summary>Reset hebdomadaire.</summary>
    Weekly,

    /// <summary>Pas de reset (une fois par personnage).</summary>
    Never,

    /// <summary>Reset instantane (farmable).</summary>
    Instant
}

/// <summary>
/// Donnees de puzzle.
/// </summary>
[System.Serializable]
public struct PuzzleData
{
    public string puzzleId;
    public string puzzleName;
    public PuzzleType type;
    public bool isOptional;
    public int rewardXP;
    public ItemData rewardItem;
}

/// <summary>
/// Types de puzzles.
/// </summary>
public enum PuzzleType
{
    Switch,
    Pressure,
    Elemental,
    Pattern,
    Combat,
    Riddle,
    Timing
}

/// <summary>
/// Donnees de tresor.
/// </summary>
[System.Serializable]
public struct TreasureData
{
    public string treasureId;
    public Vector3 position;
    public TreasureType type;
    public LootTable loot;
    public bool requiresKey;
    public string keyItemId;
}

/// <summary>
/// Types de tresors.
/// </summary>
public enum TreasureType
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary,
    Boss,
    Secret
}

// Note: LootTable et LootEntry sont deja definis dans Enemies/EnemyData.cs
