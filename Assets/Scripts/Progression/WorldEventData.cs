using UnityEngine;

/// <summary>
/// Donnees d'un evenement mondial.
/// Definit les conditions, recompenses et mecaniques d'un evenement.
/// </summary>
[CreateAssetMenu(fileName = "NewWorldEvent", menuName = "EpicLegends/Progression/World Event Data")]
public class WorldEventData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("ID unique de l'evenement")]
    public string eventId;

    [Tooltip("Nom de l'evenement")]
    public string eventName;

    [Tooltip("Description")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Icone")]
    public Sprite eventIcon;

    [Tooltip("Type d'evenement")]
    public WorldEventType eventType;

    #endregion

    #region Timing

    [Header("Timing")]
    [Tooltip("Duree de l'evenement (secondes)")]
    public float duration = 600f;

    [Tooltip("Delai de preparation (secondes)")]
    public float preparationTime = 60f;

    [Tooltip("Intervalle de spawn (heures)")]
    public float spawnInterval = 4f;

    [Tooltip("Heures de spawn possibles (0-23)")]
    public int[] possibleSpawnHours;

    #endregion

    #region Location

    [Header("Emplacement")]
    [Tooltip("Regions possibles")]
    public RegionData[] possibleRegions;

    [Tooltip("Positions fixes (si applicable)")]
    public Vector3[] fixedPositions;

    [Tooltip("Rayon de l'evenement")]
    public float eventRadius = 50f;

    #endregion

    #region Requirements

    [Header("Pre-requis")]
    [Tooltip("Niveau minimum")]
    public int minimumLevel = 1;

    [Tooltip("Niveau recommande")]
    public int recommendedLevel = 10;

    [Tooltip("Joueurs minimum")]
    public int minPlayers = 1;

    [Tooltip("Joueurs maximum")]
    public int maxPlayers = 20;

    #endregion

    #region Scaling

    [Header("Scaling")]
    [Tooltip("Scale avec le nombre de joueurs")]
    public bool scalesWithPlayerCount = true;

    [Tooltip("Multiplicateur de difficulte par joueur")]
    public float difficultyPerPlayer = 0.2f;

    [Tooltip("Multiplicateur max")]
    public float maxDifficultyScale = 3f;

    #endregion

    #region Content

    [Header("Contenu")]
    [Tooltip("Boss de l'evenement")]
    public BossData eventBoss;

    [Tooltip("Vagues d'ennemis")]
    public WaveData[] enemyWaves;

    [Tooltip("Objectifs de l'evenement")]
    public EventObjective[] objectives;

    #endregion

    #region Rewards

    [Header("Recompenses")]
    [Tooltip("XP de participation")]
    public int participationXP = 100;

    [Tooltip("XP de completion")]
    public int completionXP = 500;

    [Tooltip("Recompenses de participation")]
    public ItemData[] participationRewards;

    [Tooltip("Recompenses de completion")]
    public ItemData[] completionRewards;

    [Tooltip("Table de loot rare")]
    public LootTable rareLootTable;

    [Tooltip("Recompense de rang (top contributors)")]
    public RankReward[] rankRewards;

    #endregion

    #region Seasonal

    [Header("Saisonnier")]
    [Tooltip("Est saisonnier")]
    public bool isSeasonal = false;

    [Tooltip("Saison")]
    public SeasonType season;

    [Tooltip("Date de debut (jour du mois)")]
    public int startDay = 1;

    [Tooltip("Date de fin (jour du mois)")]
    public int endDay = 31;

    #endregion

    #region Effects

    [Header("Effets")]
    [Tooltip("Effets visuels au spawn")]
    public GameObject spawnEffect;

    [Tooltip("Effets visuels en cours")]
    public GameObject activeEffect;

    [Tooltip("Musique de l'evenement")]
    public AudioClip eventMusic;

    [Tooltip("Annonce sonore")]
    public AudioClip announcementSound;

    #endregion

    #region Public Methods

    /// <summary>
    /// Calcule le multiplicateur de difficulte.
    /// </summary>
    /// <param name="playerCount">Nombre de joueurs.</param>
    /// <returns>Multiplicateur.</returns>
    public float GetDifficultyMultiplier(int playerCount)
    {
        if (!scalesWithPlayerCount) return 1f;

        float scale = 1f + ((playerCount - 1) * difficultyPerPlayer);
        return Mathf.Min(scale, maxDifficultyScale);
    }

    /// <summary>
    /// Verifie si l'evenement est actuellement actif (saisonnier).
    /// </summary>
    /// <param name="currentDay">Jour actuel du mois.</param>
    /// <param name="currentSeason">Saison actuelle.</param>
    /// <returns>True si actif.</returns>
    public bool IsSeasonallyActive(int currentDay, SeasonType currentSeason)
    {
        if (!isSeasonal) return true;

        if (season != currentSeason) return false;

        return currentDay >= startDay && currentDay <= endDay;
    }

    /// <summary>
    /// Verifie si l'evenement peut spawn a une heure donnee.
    /// </summary>
    /// <param name="hour">Heure (0-23).</param>
    /// <returns>True si peut spawn.</returns>
    public bool CanSpawnAtHour(int hour)
    {
        if (possibleSpawnHours == null || possibleSpawnHours.Length == 0)
        {
            return true;
        }

        foreach (int h in possibleSpawnHours)
        {
            if (h == hour) return true;
        }

        return false;
    }

    #endregion
}

/// <summary>
/// Types d'evenements mondiaux.
/// </summary>
public enum WorldEventType
{
    /// <summary>Boss mondial.</summary>
    WorldBoss,

    /// <summary>Invasion d'ennemis.</summary>
    Invasion,

    /// <summary>Evenement saisonnier.</summary>
    Seasonal,

    /// <summary>Noeud de ressources rare.</summary>
    ResourceNode,

    /// <summary>Rift dimensional.</summary>
    DimensionalRift,

    /// <summary>Tempete elementaire.</summary>
    ElementalStorm,

    /// <summary>Chasse au tresor.</summary>
    TreasureHunt,

    /// <summary>Defi communautaire.</summary>
    CommunityChallenge
}

/// <summary>
/// Objectif d'evenement.
/// </summary>
[System.Serializable]
public struct EventObjective
{
    public string objectiveId;
    public string description;
    public EventObjectiveType type;
    public int targetAmount;
    public bool isOptional;
}

/// <summary>
/// Types d'objectifs d'evenement.
/// </summary>
public enum EventObjectiveType
{
    DefeatBoss,
    DefeatEnemies,
    SurviveTime,
    CollectItems,
    DefendLocation,
    EscortNPC,
    ActivatePoints,
    DealDamage
}

/// <summary>
/// Recompense par rang.
/// </summary>
[System.Serializable]
public struct RankReward
{
    public int rank;
    public string rankName;
    public ItemData[] rewards;
    public int bonusXP;
}

/// <summary>
/// Types de saisons.
/// </summary>
public enum SeasonType
{
    Spring,
    Summer,
    Autumn,
    Winter,
    Special // Evenements speciaux hors saison
}
