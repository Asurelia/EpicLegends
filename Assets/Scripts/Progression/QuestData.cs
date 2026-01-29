using UnityEngine;

/// <summary>
/// Donnees d'une quete.
/// Definit les objectifs, recompenses et conditions d'une quete.
/// </summary>
[CreateAssetMenu(fileName = "NewQuest", menuName = "EpicLegends/Progression/Quest Data")]
public class QuestData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("ID unique de la quete")]
    public string questId;

    [Tooltip("Nom de la quete")]
    public string questName;

    [Tooltip("Description courte")]
    [TextArea(2, 3)]
    public string shortDescription;

    [Tooltip("Description complete")]
    [TextArea(4, 8)]
    public string fullDescription;

    [Tooltip("Icone de la quete")]
    public Sprite questIcon;

    #endregion

    #region Classification

    [Header("Classification")]
    [Tooltip("Type de quete")]
    public QuestType questType = QuestType.Side;

    [Tooltip("Categorie")]
    public QuestCategory category = QuestCategory.Combat;

    [Tooltip("Niveau recommande")]
    public int recommendedLevel = 1;

    [Tooltip("Difficulte")]
    public QuestDifficulty difficulty = QuestDifficulty.Normal;

    #endregion

    #region Prerequisites

    [Header("Pre-requis")]
    [Tooltip("Quetes a completer avant")]
    public QuestData[] requiredQuests;

    [Tooltip("Niveau minimum")]
    public int requiredLevel = 1;

    [Tooltip("Reputation requise")]
    public int requiredReputation = 0;

    [Tooltip("Faction requise")]
    public string requiredFaction;

    #endregion

    #region Objectives

    [Header("Objectifs")]
    [Tooltip("Objectifs de la quete")]
    public QuestObjective[] objectives;

    [Tooltip("Objectifs optionnels")]
    public QuestObjective[] optionalObjectives;

    [Tooltip("Ordre strict des objectifs")]
    public bool sequentialObjectives = false;

    #endregion

    #region Timing

    [Header("Timing")]
    [Tooltip("Limite de temps (0 = illimite)")]
    public float timeLimit = 0f;

    [Tooltip("Est repetable")]
    public bool isRepeatable = false;

    [Tooltip("Cooldown avant repetition (secondes)")]
    public float repeatCooldown = 86400f; // 24 heures

    [Tooltip("Reset quotidien (pour Daily)")]
    public bool dailyReset = false;

    #endregion

    #region Rewards

    [Header("Recompenses")]
    [Tooltip("XP accordee")]
    public int xpReward = 0;

    [Tooltip("Or accorde")]
    public int goldReward = 0;

    [Tooltip("Items accordes")]
    public QuestItemReward[] itemRewards;

    [Tooltip("Reputation accordee")]
    public QuestReputationReward[] reputationRewards;

    [Tooltip("Recompenses bonus (objectifs optionnels)")]
    public QuestItemReward[] bonusRewards;

    #endregion

    #region Dialogue

    [Header("Dialogue")]
    [Tooltip("NPC donnant la quete")]
    public string questGiverNPCId;

    [Tooltip("NPC pour rendre la quete")]
    public string turnInNPCId;

    [Tooltip("Dialogue de debut")]
    public DialogueData startDialogue;

    [Tooltip("Dialogue de completion")]
    public DialogueData completionDialogue;

    #endregion

    #region Branches

    [Header("Branches")]
    [Tooltip("Quetes suivantes debloquees")]
    public QuestData[] unlocksQuests;

    [Tooltip("Est une quete de branchement")]
    public bool isBranchingPoint = false;

    [Tooltip("Choix possibles")]
    public QuestBranch[] branches;

    #endregion

    #region Public Methods

    /// <summary>
    /// Verifie si tous les objectifs sont completes.
    /// </summary>
    /// <param name="progress">Progression actuelle.</param>
    /// <returns>True si complete.</returns>
    public bool AreAllObjectivesComplete(QuestProgress progress)
    {
        if (objectives == null || objectives.Length == 0) return true;

        for (int i = 0; i < objectives.Length; i++)
        {
            if (!progress.IsObjectiveComplete(i))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Obtient le pourcentage de completion.
    /// </summary>
    /// <param name="progress">Progression actuelle.</param>
    /// <returns>Pourcentage (0-100).</returns>
    public float GetCompletionPercentage(QuestProgress progress)
    {
        if (objectives == null || objectives.Length == 0) return 100f;

        float total = 0f;
        float completed = 0f;

        for (int i = 0; i < objectives.Length; i++)
        {
            var obj = objectives[i];
            total += obj.requiredAmount;

            int current = progress.GetObjectiveProgress(i);
            completed += Mathf.Min(current, obj.requiredAmount);
        }

        return total > 0 ? (completed / total) * 100f : 0f;
    }

    /// <summary>
    /// Obtient l'index du prochain objectif non complete.
    /// </summary>
    /// <param name="progress">Progression actuelle.</param>
    /// <returns>Index ou -1 si tous completes.</returns>
    public int GetNextObjectiveIndex(QuestProgress progress)
    {
        if (objectives == null) return -1;

        for (int i = 0; i < objectives.Length; i++)
        {
            if (!progress.IsObjectiveComplete(i))
            {
                return i;
            }
        }

        return -1;
    }

    #endregion
}

/// <summary>
/// Types de quetes.
/// </summary>
public enum QuestType
{
    /// <summary>Quete principale.</summary>
    Main,

    /// <summary>Quete secondaire.</summary>
    Side,

    /// <summary>Quete quotidienne.</summary>
    Daily,

    /// <summary>Quete d'evenement.</summary>
    Event,

    /// <summary>Quete cachee.</summary>
    Hidden,

    /// <summary>Quete de guilde.</summary>
    Guild
}

/// <summary>
/// Categories de quetes.
/// </summary>
public enum QuestCategory
{
    /// <summary>Combat.</summary>
    Combat,

    /// <summary>Exploration.</summary>
    Exploration,

    /// <summary>Collecte.</summary>
    Gathering,

    /// <summary>Artisanat.</summary>
    Crafting,

    /// <summary>Social.</summary>
    Social,

    /// <summary>Creatures.</summary>
    Creatures,

    /// <summary>Construction.</summary>
    Building
}

/// <summary>
/// Difficulte de quete.
/// </summary>
public enum QuestDifficulty
{
    /// <summary>Facile.</summary>
    Easy,

    /// <summary>Normal.</summary>
    Normal,

    /// <summary>Difficile.</summary>
    Hard,

    /// <summary>Heroique.</summary>
    Heroic,

    /// <summary>Legendaire.</summary>
    Legendary
}

/// <summary>
/// Objectif de quete.
/// </summary>
[System.Serializable]
public struct QuestObjective
{
    [Tooltip("Type d'objectif")]
    public QuestObjectiveType type;

    [Tooltip("Description")]
    public string description;

    [Tooltip("Quantite requise")]
    public int requiredAmount;

    [Tooltip("Cible (ID d'ennemi, item, lieu, etc.)")]
    public string targetId;

    [Tooltip("Zone specifique")]
    public string zoneId;

    [Tooltip("Marqueur sur la carte")]
    public Vector3 mapMarkerPosition;

    [Tooltip("Afficher le marqueur")]
    public bool showMarker;
}

/// <summary>
/// Types d'objectifs de quete.
/// </summary>
public enum QuestObjectiveType
{
    /// <summary>Tuer des ennemis.</summary>
    Kill,

    /// <summary>Collecter des items.</summary>
    Collect,

    /// <summary>Escorter un PNJ.</summary>
    Escort,

    /// <summary>Livrer un item.</summary>
    Deliver,

    /// <summary>Parler a un PNJ.</summary>
    Talk,

    /// <summary>Explorer une zone.</summary>
    Explore,

    /// <summary>Capturer une creature.</summary>
    Capture,

    /// <summary>Construire.</summary>
    Build,

    /// <summary>Defendre.</summary>
    Defend,

    /// <summary>Interagir avec un objet.</summary>
    Interact,

    /// <summary>Atteindre un lieu.</summary>
    Reach,

    /// <summary>Vaincre un boss.</summary>
    DefeatBoss
}

/// <summary>
/// Recompense en item.
/// </summary>
[System.Serializable]
public struct QuestItemReward
{
    public ItemData item;
    public int amount;
    public float dropChance; // 1 = 100%
}

/// <summary>
/// Recompense en reputation.
/// </summary>
[System.Serializable]
public struct QuestReputationReward
{
    public string factionId;
    public int amount;
}

/// <summary>
/// Branche de quete.
/// </summary>
[System.Serializable]
public struct QuestBranch
{
    public string choiceText;
    public QuestData resultQuest;
    public QuestReputationReward[] reputationEffects;
}
