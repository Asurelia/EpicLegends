using UnityEngine;

/// <summary>
/// Donnees d'une region du monde.
/// Definit les zones, points d'interet et parametres d'une region.
/// </summary>
[CreateAssetMenu(fileName = "NewRegion", menuName = "EpicLegends/Progression/Region Data")]
public class RegionData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("ID unique de la region")]
    public string regionId;

    [Tooltip("Nom de la region")]
    public string regionName;

    [Tooltip("Description")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Icone sur la carte")]
    public Sprite mapIcon;

    #endregion

    #region Level

    [Header("Niveau")]
    [Tooltip("Niveau recommande")]
    public int recommendedLevel = 1;

    [Tooltip("Niveau minimum des ennemis")]
    public int minEnemyLevel = 1;

    [Tooltip("Niveau maximum des ennemis")]
    public int maxEnemyLevel = 10;

    #endregion

    #region Biome

    [Header("Biome")]
    [Tooltip("Type de biome")]
    public BiomeType biome = BiomeType.Forest;

    [Tooltip("Meteo par defaut")]
    public WeatherType defaultWeather = WeatherType.Clear;

    [Tooltip("Cycle jour/nuit")]
    public bool hasDayNightCycle = true;

    #endregion

    #region Map

    [Header("Carte")]
    [Tooltip("Position sur la carte du monde")]
    public Vector2 worldMapPosition;

    [Tooltip("Taille sur la carte")]
    public Vector2 mapSize = Vector2.one;

    [Tooltip("Texture de la carte")]
    public Texture2D mapTexture;

    [Tooltip("Brouillard de guerre initial")]
    public bool startsFogged = true;

    #endregion

    #region Points of Interest

    [Header("Points d'interet")]
    [Tooltip("Points d'interet de la region")]
    public PointOfInterest[] pointsOfInterest;

    [Tooltip("Points de voyage rapide")]
    public FastTravelPointData[] fastTravelPoints;

    #endregion

    #region Connections

    [Header("Connexions")]
    [Tooltip("Regions adjacentes")]
    public RegionData[] connectedRegions;

    [Tooltip("Conditions de deblocage")]
    public RegionUnlockCondition unlockCondition;

    #endregion

    #region Content

    [Header("Contenu")]
    [Tooltip("Ennemis de la region")]
    public EnemySpawnData[] enemySpawns;

    [Tooltip("Ressources de la region")]
    public ResourceNodeData[] resourceNodes;

    [Tooltip("Quetes de la region")]
    public QuestData[] regionQuests;

    [Tooltip("Creatures de la region")]
    public CreatureSpawnData[] creatureSpawns;

    #endregion

    #region Exploration

    [Header("Exploration")]
    [Tooltip("XP pour exploration complete")]
    public int explorationXP = 100;

    [Tooltip("Recompense exploration")]
    public ItemData explorationReward;

    [Tooltip("Secrets dans la region")]
    public int secretCount = 0;

    #endregion
}

// Note: BiomeType est deja defini dans Building/ResourceData.cs

/// <summary>
/// Types de meteo.
/// </summary>
public enum WeatherType
{
    Clear,
    Cloudy,
    Rain,
    Storm,
    Snow,
    Fog,
    Sandstorm,
    Blizzard
}

/// <summary>
/// Point d'interet.
/// </summary>
[System.Serializable]
public struct PointOfInterest
{
    public string poiId;
    public string poiName;
    public PointOfInterestType type;
    public Vector3 position;
    public Sprite icon;
    public bool isDiscovered;
    public string linkedQuestId;
}

/// <summary>
/// Types de points d'interet.
/// </summary>
public enum PointOfInterestType
{
    Landmark,
    Dungeon,
    Town,
    Camp,
    Shrine,
    Treasure,
    Boss,
    Secret,
    Resource,
    Quest
}

/// <summary>
/// Donnees de point de voyage rapide.
/// </summary>
[System.Serializable]
public struct FastTravelPointData
{
    public string pointId;
    public string pointName;
    public Vector3 spawnPosition;
    public Quaternion spawnRotation;
    public bool startsUnlocked;
    public int unlockCost;
}

/// <summary>
/// Condition de deblocage de region.
/// </summary>
[System.Serializable]
public struct RegionUnlockCondition
{
    public UnlockConditionType type;
    public string targetId;
    public int requiredValue;
}

/// <summary>
/// Types de conditions de deblocage.
/// </summary>
public enum UnlockConditionType
{
    None,
    QuestCompleted,
    PlayerLevel,
    ItemPossessed,
    RegionExplored
}

/// <summary>
/// Donnees de spawn d'ennemi.
/// </summary>
[System.Serializable]
public struct EnemySpawnData
{
    public GameObject enemyPrefab;
    public Vector3 spawnPosition;
    public float spawnRadius;
    public int maxCount;
    public float respawnTime;
}

/// <summary>
/// Donnees de noeud de ressource.
/// </summary>
[System.Serializable]
public struct ResourceNodeData
{
    public ResourceType resourceType;
    public Vector3 position;
    public int amount;
    public float respawnTime;
}

/// <summary>
/// Donnees de spawn de creature.
/// </summary>
[System.Serializable]
public struct CreatureSpawnData
{
    public CreatureData creatureData;
    public Vector3 spawnPosition;
    public float spawnRadius;
    public float spawnChance;
    public bool isRare;
}
