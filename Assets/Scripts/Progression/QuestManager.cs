using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire de quetes.
/// Gere l'acceptation, le suivi et la completion des quetes.
/// </summary>
public class QuestManager : MonoBehaviour
{
    #region Singleton

    private static QuestManager _instance;
    public static QuestManager Instance
    {
        get => _instance;
        private set => _instance = value;
    }

    private void Awake()
    {
        _activeQuests = new Dictionary<string, QuestProgress>();
        _completedQuests = new HashSet<string>();
        _questCooldowns = new Dictionary<string, float>();

        if (Instance != null && Instance != this)
        {
            SafeDestroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(obj);
        }
        else
        {
            Destroy(obj);
        }
#else
        Destroy(obj);
#endif
    }

    #endregion

    #region Fields

    [Header("Configuration")]
    [SerializeField] private int _maxActiveQuests = 20;
    [SerializeField] private int _maxTrackedQuests = 3;

    // Quetes actives
    private Dictionary<string, QuestProgress> _activeQuests;

    // Quetes completees
    private HashSet<string> _completedQuests;

    // Cooldowns des quetes repetables
    private Dictionary<string, float> _questCooldowns;

    // Quetes suivies
    private List<string> _trackedQuests = new List<string>();

    #endregion

    #region Events

    /// <summary>Declenche lors de l'acceptation d'une quete.</summary>
    public event Action<QuestData> OnQuestAccepted;

    /// <summary>Declenche lors d'une mise a jour d'objectif.</summary>
    public event Action<QuestData, int, int> OnObjectiveUpdated;

    /// <summary>Declenche lors de la completion d'une quete.</summary>
    public event Action<QuestData> OnQuestCompleted;

    /// <summary>Declenche lors de l'abandon d'une quete.</summary>
    public event Action<QuestData> OnQuestAbandoned;

    /// <summary>Declenche lors de l'echec d'une quete.</summary>
    public event Action<QuestData> OnQuestFailed;

    #endregion

    #region Properties

    /// <summary>Nombre de quetes actives.</summary>
    public int ActiveQuestCount => _activeQuests?.Count ?? 0;

    /// <summary>Nombre de quetes completees.</summary>
    public int CompletedQuestCount => _completedQuests?.Count ?? 0;

    /// <summary>Peut accepter plus de quetes?</summary>
    public bool CanAcceptMoreQuests => (_activeQuests?.Count ?? 0) < _maxActiveQuests;

    #endregion

    #region Public Methods - Quete Actions

    /// <summary>
    /// Accepte une quete.
    /// </summary>
    /// <param name="quest">Quete a accepter.</param>
    /// <returns>True si acceptee.</returns>
    public bool AcceptQuest(QuestData quest)
    {
        if (quest == null) return false;
        if (_activeQuests == null) _activeQuests = new Dictionary<string, QuestProgress>();

        // Verifier si deja active
        if (_activeQuests.ContainsKey(quest.questId)) return false;

        // Verifier le maximum
        if (!CanAcceptMoreQuests) return false;

        // Verifier les pre-requis
        if (!MeetsPrerequisites(quest)) return false;

        // Verifier le cooldown
        if (IsOnCooldown(quest)) return false;

        // Creer la progression
        var progress = new QuestProgress(quest);
        _activeQuests[quest.questId] = progress;

        OnQuestAccepted?.Invoke(quest);

        return true;
    }

    /// <summary>
    /// Abandonne une quete.
    /// </summary>
    /// <param name="questId">ID de la quete.</param>
    /// <returns>True si abandonnee.</returns>
    public bool AbandonQuest(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return false;
        if (_activeQuests == null) return false;

        if (!_activeQuests.TryGetValue(questId, out var progress)) return false;

        _activeQuests.Remove(questId);
        _trackedQuests?.Remove(questId);

        OnQuestAbandoned?.Invoke(progress.QuestData);

        return true;
    }

    /// <summary>
    /// Complete une quete.
    /// </summary>
    /// <param name="questId">ID de la quete.</param>
    /// <returns>True si completee.</returns>
    public bool CompleteQuest(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return false;
        if (_activeQuests == null) return false;

        if (!_activeQuests.TryGetValue(questId, out var progress)) return false;
        if (!progress.QuestData.AreAllObjectivesComplete(progress)) return false;

        var quest = progress.QuestData;

        // Marquer comme complete
        _activeQuests.Remove(questId);
        _trackedQuests?.Remove(questId);

        if (_completedQuests == null) _completedQuests = new HashSet<string>();
        _completedQuests.Add(questId);

        // Cooldown si repetable
        if (quest.isRepeatable)
        {
            if (_questCooldowns == null) _questCooldowns = new Dictionary<string, float>();
            _questCooldowns[questId] = Time.time + quest.repeatCooldown;
            _completedQuests.Remove(questId); // Permettre la re-acceptation apres cooldown
        }

        // Donner les recompenses
        GiveQuestRewards(quest, progress);

        // Debloquer les quetes suivantes
        UnlockFollowUpQuests(quest);

        OnQuestCompleted?.Invoke(quest);

        return true;
    }

    #endregion

    #region Public Methods - Objectives

    /// <summary>
    /// Met a jour la progression d'un objectif.
    /// </summary>
    /// <param name="objectiveType">Type d'objectif.</param>
    /// <param name="targetId">ID de la cible.</param>
    /// <param name="amount">Quantite.</param>
    public void UpdateObjective(QuestObjectiveType objectiveType, string targetId, int amount = 1)
    {
        if (_activeQuests == null) return;

        foreach (var kvp in _activeQuests)
        {
            var progress = kvp.Value;
            var quest = progress.QuestData;

            if (quest.objectives == null) continue;

            for (int i = 0; i < quest.objectives.Length; i++)
            {
                var obj = quest.objectives[i];

                // Verifier le type et la cible
                if (obj.type == objectiveType &&
                    (string.IsNullOrEmpty(obj.targetId) || obj.targetId == targetId))
                {
                    // Verifier l'ordre si sequentiel
                    if (quest.sequentialObjectives)
                    {
                        int nextIndex = quest.GetNextObjectiveIndex(progress);
                        if (i != nextIndex) continue;
                    }

                    // Mettre a jour
                    int newProgress = progress.UpdateObjective(i, amount);

                    OnObjectiveUpdated?.Invoke(quest, i, newProgress);

                    // Verifier auto-completion
                    if (quest.AreAllObjectivesComplete(progress))
                    {
                        // Notifier mais ne pas auto-completer
                        // Le joueur doit rendre la quete
                    }
                }
            }
        }
    }

    /// <summary>
    /// Signale une mise a jour de type Kill.
    /// </summary>
    /// <param name="enemyId">ID de l'ennemi tue.</param>
    public void ReportKill(string enemyId)
    {
        UpdateObjective(QuestObjectiveType.Kill, enemyId, 1);
    }

    /// <summary>
    /// Signale une mise a jour de type Collect.
    /// </summary>
    /// <param name="itemId">ID de l'item collecte.</param>
    /// <param name="amount">Quantite.</param>
    public void ReportCollect(string itemId, int amount = 1)
    {
        UpdateObjective(QuestObjectiveType.Collect, itemId, amount);
    }

    /// <summary>
    /// Signale une mise a jour de type Explore.
    /// </summary>
    /// <param name="zoneId">ID de la zone exploree.</param>
    public void ReportExplore(string zoneId)
    {
        UpdateObjective(QuestObjectiveType.Explore, zoneId, 1);
    }

    /// <summary>
    /// Signale une mise a jour de type Talk.
    /// </summary>
    /// <param name="npcId">ID du NPC.</param>
    public void ReportTalk(string npcId)
    {
        UpdateObjective(QuestObjectiveType.Talk, npcId, 1);
    }

    #endregion

    #region Public Methods - Queries

    /// <summary>
    /// Verifie si une quete est active.
    /// </summary>
    /// <param name="questId">ID de la quete.</param>
    /// <returns>True si active.</returns>
    public bool IsQuestActive(string questId)
    {
        return _activeQuests != null && _activeQuests.ContainsKey(questId);
    }

    /// <summary>
    /// Verifie si une quete est completee.
    /// </summary>
    /// <param name="questId">ID de la quete.</param>
    /// <returns>True si completee.</returns>
    public bool IsQuestCompleted(string questId)
    {
        return _completedQuests != null && _completedQuests.Contains(questId);
    }

    /// <summary>
    /// Obtient la progression d'une quete.
    /// </summary>
    /// <param name="questId">ID de la quete.</param>
    /// <returns>Progression ou null.</returns>
    public QuestProgress GetQuestProgress(string questId)
    {
        if (string.IsNullOrEmpty(questId) || _activeQuests == null) return null;
        return _activeQuests.TryGetValue(questId, out var progress) ? progress : null;
    }

    /// <summary>
    /// Obtient toutes les quetes actives.
    /// </summary>
    /// <returns>Liste des progressions.</returns>
    public List<QuestProgress> GetActiveQuests()
    {
        var list = new List<QuestProgress>();
        if (_activeQuests != null)
        {
            list.AddRange(_activeQuests.Values);
        }
        return list;
    }

    /// <summary>
    /// Obtient les quetes actives d'un type.
    /// </summary>
    /// <param name="type">Type de quete.</param>
    /// <returns>Liste des progressions.</returns>
    public List<QuestProgress> GetActiveQuestsByType(QuestType type)
    {
        var list = new List<QuestProgress>();
        if (_activeQuests == null) return list;

        foreach (var progress in _activeQuests.Values)
        {
            if (progress.QuestData.questType == type)
            {
                list.Add(progress);
            }
        }

        return list;
    }

    /// <summary>
    /// Verifie si les pre-requis d'une quete sont remplis.
    /// </summary>
    /// <param name="quest">Quete a verifier.</param>
    /// <returns>True si pre-requis remplis.</returns>
    public bool MeetsPrerequisites(QuestData quest)
    {
        if (quest == null) return false;

        // Verifier les quetes requises
        if (quest.requiredQuests != null)
        {
            foreach (var required in quest.requiredQuests)
            {
                if (required != null && !IsQuestCompleted(required.questId))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Verifie si une quete est en cooldown.
    /// </summary>
    /// <param name="quest">Quete a verifier.</param>
    /// <returns>True si en cooldown.</returns>
    public bool IsOnCooldown(QuestData quest)
    {
        if (quest == null || !quest.isRepeatable) return false;
        if (_questCooldowns == null) return false;

        if (_questCooldowns.TryGetValue(quest.questId, out float endTime))
        {
            return Time.time < endTime;
        }

        return false;
    }

    #endregion

    #region Public Methods - Tracking

    /// <summary>
    /// Suit une quete sur l'UI.
    /// </summary>
    /// <param name="questId">ID de la quete.</param>
    /// <returns>True si suivie.</returns>
    public bool TrackQuest(string questId)
    {
        if (!IsQuestActive(questId)) return false;
        if (_trackedQuests == null) _trackedQuests = new List<string>();
        if (_trackedQuests.Count >= _maxTrackedQuests) return false;
        if (_trackedQuests.Contains(questId)) return false;

        _trackedQuests.Add(questId);
        return true;
    }

    /// <summary>
    /// Arrete de suivre une quete.
    /// </summary>
    /// <param name="questId">ID de la quete.</param>
    public void UntrackQuest(string questId)
    {
        _trackedQuests?.Remove(questId);
    }

    /// <summary>
    /// Obtient les quetes suivies.
    /// </summary>
    /// <returns>Liste des progressions.</returns>
    public List<QuestProgress> GetTrackedQuests()
    {
        var list = new List<QuestProgress>();

        if (_trackedQuests == null || _activeQuests == null) return list;

        foreach (var questId in _trackedQuests)
        {
            if (_activeQuests.TryGetValue(questId, out var progress))
            {
                list.Add(progress);
            }
        }

        return list;
    }

    #endregion

    #region Private Methods

    private void GiveQuestRewards(QuestData quest, QuestProgress progress)
    {
        // XP
        if (quest.xpReward > 0)
        {
            var levelSystem = FindFirstObjectByType<LevelSystem>();
            if (levelSystem != null)
            {
                levelSystem.AddXP(quest.xpReward, XPSource.Quest);
            }
        }

        // Or
        if (quest.goldReward > 0)
        {
            // TODO: Integration avec un systeme de monnaie
            // Le systeme de ressources ne gere pas la monnaie directement
            Debug.Log($"[Quest] Reward: {quest.goldReward} gold");
        }

        // Items
        if (quest.itemRewards != null)
        {
            foreach (var reward in quest.itemRewards)
            {
                if (reward.item != null)
                {
                    float roll = UnityEngine.Random.value;
                    if (roll <= reward.dropChance)
                    {
                        // Ajouter a l'inventaire
                        // TODO: Integration avec Inventory system
                    }
                }
            }
        }
    }

    private void UnlockFollowUpQuests(QuestData quest)
    {
        if (quest.unlocksQuests == null) return;

        foreach (var nextQuest in quest.unlocksQuests)
        {
            if (nextQuest != null)
            {
                // Les quetes sont maintenant disponibles
                // Le joueur peut les accepter
            }
        }
    }

    #endregion
}

/// <summary>
/// Progression d'une quete active.
/// </summary>
[System.Serializable]
public class QuestProgress
{
    [SerializeField] private QuestData _questData;
    [SerializeField] private int[] _objectiveProgress;
    [SerializeField] private float _startTime;
    [SerializeField] private bool _isFailed;

    public QuestData QuestData => _questData;
    public float StartTime => _startTime;
    public bool IsFailed => _isFailed;
    public float ElapsedTime => Time.time - _startTime;

    public QuestProgress(QuestData quest)
    {
        _questData = quest;
        _startTime = Time.time;
        _isFailed = false;

        int objectiveCount = quest.objectives?.Length ?? 0;
        _objectiveProgress = new int[objectiveCount];
    }

    public int GetObjectiveProgress(int index)
    {
        if (_objectiveProgress == null || index < 0 || index >= _objectiveProgress.Length)
            return 0;

        return _objectiveProgress[index];
    }

    public bool IsObjectiveComplete(int index)
    {
        if (_questData?.objectives == null || index < 0 || index >= _questData.objectives.Length)
            return false;

        return GetObjectiveProgress(index) >= _questData.objectives[index].requiredAmount;
    }

    public int UpdateObjective(int index, int amount)
    {
        if (_objectiveProgress == null || index < 0 || index >= _objectiveProgress.Length)
            return 0;

        _objectiveProgress[index] += amount;

        // Capper a la valeur requise
        if (_questData?.objectives != null && index < _questData.objectives.Length)
        {
            _objectiveProgress[index] = Mathf.Min(
                _objectiveProgress[index],
                _questData.objectives[index].requiredAmount
            );
        }

        return _objectiveProgress[index];
    }

    public void MarkFailed()
    {
        _isFailed = true;
    }
}
