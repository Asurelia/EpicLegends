using System;
using System.Collections.Generic;

/// <summary>
/// Conteneur principal des donnees de sauvegarde.
/// </summary>
[Serializable]
public class SaveData
{
    /// <summary>
    /// Version du format de sauvegarde.
    /// </summary>
    public string version = "1.0.0";

    /// <summary>
    /// Timestamp Unix de la sauvegarde.
    /// </summary>
    public long timestamp;

    /// <summary>
    /// Donnees du joueur.
    /// </summary>
    public PlayerSaveData playerData;

    /// <summary>
    /// Donnees de l'inventaire.
    /// </summary>
    public InventorySaveData inventoryData;

    /// <summary>
    /// Progression du jeu.
    /// </summary>
    public GameProgressData gameProgress;

    /// <summary>
    /// Donnees de l'arbre de competences.
    /// </summary>
    public SkillTreeSaveData skillTreeData;

    /// <summary>
    /// Donnees des achievements.
    /// </summary>
    public AchievementSaveData achievementData;

    /// <summary>
    /// Donnees de l'equipement.
    /// </summary>
    public EquipmentSaveData equipmentData;

    /// <summary>
    /// Donnees des quetes.
    /// </summary>
    public QuestSaveData questData;

    /// <summary>
    /// Donnees des nodes de ressources.
    /// </summary>
    public ResourceNodeSaveData resourceNodeData;

    /// <summary>
    /// Donnees du systeme NG+.
    /// </summary>
    public NGPlusSaveData ngPlusData;

    /// <summary>
    /// Donnees des territoires.
    /// </summary>
    public TerritorySaveData territoryData;

    /// <summary>
    /// Donnees du donjon procedural actuel.
    /// </summary>
    public ProceduralDungeonSaveData dungeonData;

    public SaveData()
    {
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        playerData = new PlayerSaveData();
        inventoryData = new InventorySaveData();
        gameProgress = new GameProgressData();
        skillTreeData = new SkillTreeSaveData();
        achievementData = new AchievementSaveData();
        equipmentData = new EquipmentSaveData();
        questData = new QuestSaveData();
        resourceNodeData = new ResourceNodeSaveData();
        ngPlusData = new NGPlusSaveData();
        territoryData = new TerritorySaveData();
    }
}

/// <summary>
/// Donnees du joueur a sauvegarder.
/// </summary>
[Serializable]
public class PlayerSaveData
{
    // Position
    public float positionX;
    public float positionY;
    public float positionZ;
    public float rotationY;

    // Stats de niveau
    public int level = 1;
    public int experience;
    public int experienceToNextLevel = 100;

    // Stats actuelles
    public float currentHealth = 100f;
    public float maxHealth = 100f;
    public float currentMana = 50f;
    public float maxMana = 50f;
    public float currentStamina = 100f;
    public float maxStamina = 100f;

    // Stats de base (points distribues)
    public Dictionary<StatType, float> baseStats = new Dictionary<StatType, float>();

    // Points de stats non distribues
    public int unspentStatPoints;

    // Equipement (IDs des items equipes par slot)
    public Dictionary<string, string> equippedItems = new Dictionary<string, string>();

    public PlayerSaveData()
    {
        // Initialiser les stats de base par defaut
        foreach (StatType statType in Enum.GetValues(typeof(StatType)))
        {
            if (!baseStats.ContainsKey(statType))
            {
                baseStats[statType] = 10f; // Valeur de base
            }
        }
    }
}

/// <summary>
/// Donnees d'un objet individuel a sauvegarder.
/// </summary>
[Serializable]
public class ItemSaveData
{
    /// <summary>
    /// ID de l'objet (reference vers ItemData).
    /// </summary>
    public string itemId;

    /// <summary>
    /// Quantite dans le slot.
    /// </summary>
    public int quantity = 1;

    /// <summary>
    /// Index du slot dans l'inventaire.
    /// </summary>
    public int slotIndex;

    /// <summary>
    /// ID unique de l'instance (pour tracer les objets uniques).
    /// </summary>
    public string instanceId;
}

/// <summary>
/// Donnees de l'inventaire a sauvegarder.
/// </summary>
[Serializable]
public class InventorySaveData
{
    /// <summary>
    /// Capacite de l'inventaire.
    /// </summary>
    public int capacity = 20;

    /// <summary>
    /// Or du joueur.
    /// </summary>
    public int gold;

    /// <summary>
    /// Liste des objets dans l'inventaire.
    /// </summary>
    public List<ItemSaveData> items = new List<ItemSaveData>();
}

/// <summary>
/// Donnees de progression du jeu.
/// </summary>
[Serializable]
public class GameProgressData
{
    /// <summary>
    /// Nom de la scene actuelle.
    /// </summary>
    public string currentSceneName = "MainMenu";

    /// <summary>
    /// Temps de jeu total en secondes.
    /// </summary>
    public float totalPlayTimeSeconds;

    /// <summary>
    /// IDs des quetes completees.
    /// </summary>
    public List<string> completedQuestIds = new List<string>();

    /// <summary>
    /// IDs des quetes actives.
    /// </summary>
    public List<string> activeQuestIds = new List<string>();

    /// <summary>
    /// Zones debloquees.
    /// </summary>
    public List<string> unlockedAreas = new List<string>();

    /// <summary>
    /// Drapeaux de progression (cles diverses).
    /// </summary>
    public Dictionary<string, bool> progressFlags = new Dictionary<string, bool>();

    /// <summary>
    /// Valeurs numeriques de progression.
    /// </summary>
    public Dictionary<string, int> progressValues = new Dictionary<string, int>();

    /// <summary>
    /// Ennemis vaincus (par type).
    /// </summary>
    public Dictionary<string, int> enemiesDefeated = new Dictionary<string, int>();
}

/// <summary>
/// Information resumee d'un slot de sauvegarde.
/// </summary>
[Serializable]
public class SaveSlotInfo
{
    public int slotIndex;
    public string saveName;
    public int playerLevel;
    public float playTimeSeconds;
    public string locationName;
    public long timestamp;
    public bool isEmpty = true;

    /// <summary>
    /// Cree un SaveSlotInfo a partir des donnees de sauvegarde.
    /// </summary>
    public static SaveSlotInfo FromSaveData(SaveData saveData, int slotIndex)
    {
        return new SaveSlotInfo
        {
            slotIndex = slotIndex,
            saveName = $"Sauvegarde {slotIndex}",
            playerLevel = saveData.playerData.level,
            playTimeSeconds = saveData.gameProgress.totalPlayTimeSeconds,
            locationName = saveData.gameProgress.currentSceneName,
            timestamp = saveData.timestamp,
            isEmpty = false
        };
    }

    /// <summary>
    /// Formate le temps de jeu en HH:MM:SS.
    /// </summary>
    public string GetFormattedPlayTime()
    {
        int totalSeconds = (int)playTimeSeconds;
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
    }

    /// <summary>
    /// Retourne la date de sauvegarde formatee.
    /// </summary>
    public string GetFormattedDate()
    {
        var dateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
        return dateTime.ToString("dd/MM/yyyy HH:mm");
    }
}

/// <summary>
/// Donnees de sauvegarde des quetes.
/// </summary>
[Serializable]
public class QuestSaveData
{
    /// <summary>
    /// Quetes actives avec leur progression.
    /// </summary>
    public List<ActiveQuestData> activeQuests = new List<ActiveQuestData>();

    /// <summary>
    /// IDs des quetes completees.
    /// </summary>
    public List<string> completedQuestIds = new List<string>();

    /// <summary>
    /// IDs des quetes echouees.
    /// </summary>
    public List<string> failedQuestIds = new List<string>();
}

/// <summary>
/// Donnees d'une quete active.
/// </summary>
[Serializable]
public class ActiveQuestData
{
    public string questId;
    public List<ObjectiveProgress> objectiveProgress = new List<ObjectiveProgress>();
    public long startTimestamp;
}

/// <summary>
/// Progression d'un objectif de quete.
/// </summary>
[Serializable]
public class ObjectiveProgress
{
    public int objectiveIndex;
    public int currentProgress;
    public bool isCompleted;
}

/// <summary>
/// Donnees de sauvegarde de l'equipement.
/// </summary>
[Serializable]
public class EquipmentSaveData
{
    /// <summary>
    /// IDs des items equipes par slot.
    /// </summary>
    public Dictionary<EquipmentSlot, string> equippedItemIds = new Dictionary<EquipmentSlot, string>();
}

/// <summary>
/// Proprietaire d'un territoire.
/// </summary>
public enum TerritoryOwner
{
    Neutral,
    Player,
    Enemy
}

/// <summary>
/// Donnees de sauvegarde des territoires.
/// </summary>
[Serializable]
public class TerritorySaveData
{
    public List<TerritoryStateData> territoryStates = new List<TerritoryStateData>();
}

/// <summary>
/// Etat d'un territoire sauvegarde.
/// </summary>
[Serializable]
public class TerritoryStateData
{
    public string territoryId;
    public TerritoryOwner owner;
    public float captureProgress;
    public bool isContested;
}
