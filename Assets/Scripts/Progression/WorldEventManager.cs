using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire des evenements mondiaux.
/// Gere le spawn, le tracking et la completion des evenements.
/// </summary>
public class WorldEventManager : MonoBehaviour
{
    #region Singleton

    private static WorldEventManager _instance;
    public static WorldEventManager Instance
    {
        get => _instance;
        private set => _instance = value;
    }

    private void Awake()
    {
        _activeEvents = new Dictionary<string, ActiveWorldEvent>();
        _eventCooldowns = new Dictionary<string, float>();
        _scheduledEvents = new List<ScheduledEvent>();

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
    [SerializeField] private WorldEventData[] _availableEvents;
    [SerializeField] private int _maxConcurrentEvents = 3;
    [SerializeField] private float _eventCheckInterval = 60f;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;

    // Evenements actifs
    private Dictionary<string, ActiveWorldEvent> _activeEvents;

    // Cooldowns des evenements
    private Dictionary<string, float> _eventCooldowns;

    // Evenements programmes
    private List<ScheduledEvent> _scheduledEvents;

    // Timer
    private float _nextCheckTime;

    #endregion

    #region Events

    /// <summary>Declenche lors du spawn d'un evenement.</summary>
    public event Action<WorldEventData, Vector3> OnEventSpawned;

    /// <summary>Declenche lors de la fin d'un evenement.</summary>
    public event Action<WorldEventData, bool> OnEventEnded;

    /// <summary>Declenche lors d'une mise a jour d'objectif.</summary>
    public event Action<WorldEventData, int, int> OnEventObjectiveUpdated;

    /// <summary>Declenche lors d'une participation.</summary>
    public event Action<WorldEventData, string> OnPlayerParticipated;

    #endregion

    #region Properties

    /// <summary>Nombre d'evenements actifs.</summary>
    public int ActiveEventCount => _activeEvents?.Count ?? 0;

    /// <summary>Peut spawner plus d'evenements?</summary>
    public bool CanSpawnMoreEvents => (_activeEvents?.Count ?? 0) < _maxConcurrentEvents;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        if (Time.time >= _nextCheckTime)
        {
            CheckScheduledEvents();
            _nextCheckTime = Time.time + _eventCheckInterval;
        }

        UpdateActiveEvents();
    }

    #endregion

    #region Public Methods - Spawn

    /// <summary>
    /// Spawn un evenement specifique.
    /// </summary>
    /// <param name="eventData">Donnees de l'evenement.</param>
    /// <param name="position">Position de spawn.</param>
    /// <returns>True si spawne.</returns>
    public bool SpawnEvent(WorldEventData eventData, Vector3 position)
    {
        if (eventData == null) return false;
        if (!CanSpawnMoreEvents) return false;
        if (IsEventOnCooldown(eventData)) return false;
        if (IsEventActive(eventData.eventId)) return false;

        var activeEvent = new ActiveWorldEvent
        {
            eventData = eventData,
            spawnPosition = position,
            startTime = Time.time,
            endTime = Time.time + eventData.duration,
            participants = new HashSet<string>(),
            objectiveProgress = new int[eventData.objectives?.Length ?? 0],
            state = EventState.Preparing
        };

        if (_activeEvents == null) _activeEvents = new Dictionary<string, ActiveWorldEvent>();
        _activeEvents[eventData.eventId] = activeEvent;

        // Demarre la preparation
        if (eventData.preparationTime > 0)
        {
            activeEvent.preparationEndTime = Time.time + eventData.preparationTime;
        }
        else
        {
            activeEvent.state = EventState.Active;
        }

        OnEventSpawned?.Invoke(eventData, position);

        if (_debugMode)
        {
            Debug.Log($"[WorldEvent] Spawned: {eventData.eventName} at {position}");
        }

        return true;
    }

    /// <summary>
    /// Spawn un evenement aleatoire.
    /// </summary>
    /// <returns>True si spawne.</returns>
    public bool SpawnRandomEvent()
    {
        if (_availableEvents == null || _availableEvents.Length == 0) return false;

        // Filtrer les evenements disponibles
        var available = new List<WorldEventData>();
        foreach (var evt in _availableEvents)
        {
            if (!IsEventOnCooldown(evt) && !IsEventActive(evt.eventId))
            {
                available.Add(evt);
            }
        }

        if (available.Count == 0) return false;

        // Selection aleatoire
        var selected = available[UnityEngine.Random.Range(0, available.Count)];

        // Position aleatoire
        Vector3 position = GetRandomEventPosition(selected);

        return SpawnEvent(selected, position);
    }

    /// <summary>
    /// Programme un evenement pour plus tard.
    /// </summary>
    /// <param name="eventData">Donnees de l'evenement.</param>
    /// <param name="spawnTime">Temps de spawn.</param>
    /// <param name="position">Position.</param>
    public void ScheduleEvent(WorldEventData eventData, float spawnTime, Vector3 position)
    {
        if (eventData == null) return;

        if (_scheduledEvents == null) _scheduledEvents = new List<ScheduledEvent>();

        _scheduledEvents.Add(new ScheduledEvent
        {
            eventData = eventData,
            spawnTime = spawnTime,
            position = position
        });
    }

    #endregion

    #region Public Methods - Participation

    /// <summary>
    /// Enregistre la participation d'un joueur.
    /// </summary>
    /// <param name="eventId">ID de l'evenement.</param>
    /// <param name="playerId">ID du joueur.</param>
    public void RegisterParticipation(string eventId, string playerId)
    {
        if (string.IsNullOrEmpty(eventId) || string.IsNullOrEmpty(playerId)) return;
        if (_activeEvents == null) return;

        if (!_activeEvents.TryGetValue(eventId, out var activeEvent)) return;

        if (activeEvent.participants == null)
        {
            activeEvent.participants = new HashSet<string>();
        }

        if (activeEvent.participants.Add(playerId))
        {
            OnPlayerParticipated?.Invoke(activeEvent.eventData, playerId);
        }
    }

    /// <summary>
    /// Met a jour un objectif d'evenement.
    /// </summary>
    /// <param name="eventId">ID de l'evenement.</param>
    /// <param name="objectiveIndex">Index de l'objectif.</param>
    /// <param name="amount">Quantite.</param>
    public void UpdateEventObjective(string eventId, int objectiveIndex, int amount = 1)
    {
        if (_activeEvents == null) return;

        if (!_activeEvents.TryGetValue(eventId, out var activeEvent)) return;
        if (activeEvent.state != EventState.Active) return;

        var objectives = activeEvent.eventData.objectives;
        if (objectives == null || objectiveIndex < 0 || objectiveIndex >= objectives.Length)
        {
            return;
        }

        if (activeEvent.objectiveProgress == null)
        {
            activeEvent.objectiveProgress = new int[objectives.Length];
        }

        activeEvent.objectiveProgress[objectiveIndex] += amount;

        // Capper
        int max = objectives[objectiveIndex].targetAmount;
        activeEvent.objectiveProgress[objectiveIndex] =
            Mathf.Min(activeEvent.objectiveProgress[objectiveIndex], max);

        OnEventObjectiveUpdated?.Invoke(
            activeEvent.eventData,
            objectiveIndex,
            activeEvent.objectiveProgress[objectiveIndex]
        );

        // Verifier completion
        CheckEventCompletion(activeEvent);
    }

    #endregion

    #region Public Methods - Queries

    /// <summary>
    /// Verifie si un evenement est actif.
    /// </summary>
    /// <param name="eventId">ID de l'evenement.</param>
    /// <returns>True si actif.</returns>
    public bool IsEventActive(string eventId)
    {
        return _activeEvents != null && _activeEvents.ContainsKey(eventId);
    }

    /// <summary>
    /// Verifie si un evenement est en cooldown.
    /// </summary>
    /// <param name="eventData">Donnees de l'evenement.</param>
    /// <returns>True si en cooldown.</returns>
    public bool IsEventOnCooldown(WorldEventData eventData)
    {
        if (eventData == null || _eventCooldowns == null) return false;

        if (_eventCooldowns.TryGetValue(eventData.eventId, out float endTime))
        {
            return Time.time < endTime;
        }

        return false;
    }

    /// <summary>
    /// Obtient un evenement actif.
    /// </summary>
    /// <param name="eventId">ID de l'evenement.</param>
    /// <returns>Evenement actif ou null.</returns>
    public ActiveWorldEvent GetActiveEvent(string eventId)
    {
        if (_activeEvents == null) return null;
        return _activeEvents.TryGetValue(eventId, out var evt) ? evt : null;
    }

    /// <summary>
    /// Obtient tous les evenements actifs.
    /// </summary>
    /// <returns>Liste des evenements.</returns>
    public List<ActiveWorldEvent> GetAllActiveEvents()
    {
        var list = new List<ActiveWorldEvent>();
        if (_activeEvents != null)
        {
            list.AddRange(_activeEvents.Values);
        }
        return list;
    }

    /// <summary>
    /// Obtient les evenements actifs dans un rayon.
    /// </summary>
    /// <param name="position">Position centre.</param>
    /// <param name="radius">Rayon.</param>
    /// <returns>Evenements dans le rayon.</returns>
    public List<ActiveWorldEvent> GetEventsInRadius(Vector3 position, float radius)
    {
        var list = new List<ActiveWorldEvent>();
        if (_activeEvents == null) return list;

        foreach (var evt in _activeEvents.Values)
        {
            if (Vector3.Distance(evt.spawnPosition, position) <= radius)
            {
                list.Add(evt);
            }
        }

        return list;
    }

    #endregion

    #region Public Methods - Control

    /// <summary>
    /// Force la fin d'un evenement.
    /// </summary>
    /// <param name="eventId">ID de l'evenement.</param>
    /// <param name="success">Complete avec succes?</param>
    public void ForceEndEvent(string eventId, bool success = false)
    {
        if (_activeEvents == null) return;

        if (_activeEvents.TryGetValue(eventId, out var activeEvent))
        {
            EndEvent(activeEvent, success);
        }
    }

    #endregion

    #region Private Methods

    private void UpdateActiveEvents()
    {
        if (_activeEvents == null || _activeEvents.Count == 0) return;

        var toRemove = new List<string>();

        foreach (var kvp in _activeEvents)
        {
            var evt = kvp.Value;

            // Verifier la preparation
            if (evt.state == EventState.Preparing)
            {
                if (Time.time >= evt.preparationEndTime)
                {
                    evt.state = EventState.Active;
                }
            }

            // Verifier le timeout
            if (Time.time >= evt.endTime)
            {
                toRemove.Add(kvp.Key);
            }
        }

        // Terminer les evenements expires
        foreach (var eventId in toRemove)
        {
            var evt = _activeEvents[eventId];
            EndEvent(evt, false);
        }
    }

    private void CheckScheduledEvents()
    {
        if (_scheduledEvents == null || _scheduledEvents.Count == 0) return;

        var toSpawn = new List<ScheduledEvent>();

        foreach (var scheduled in _scheduledEvents)
        {
            if (Time.time >= scheduled.spawnTime)
            {
                toSpawn.Add(scheduled);
            }
        }

        foreach (var scheduled in toSpawn)
        {
            _scheduledEvents.Remove(scheduled);
            SpawnEvent(scheduled.eventData, scheduled.position);
        }
    }

    private void CheckEventCompletion(ActiveWorldEvent activeEvent)
    {
        if (activeEvent.eventData.objectives == null) return;

        bool allComplete = true;

        for (int i = 0; i < activeEvent.eventData.objectives.Length; i++)
        {
            var obj = activeEvent.eventData.objectives[i];
            if (obj.isOptional) continue;

            int progress = activeEvent.objectiveProgress?.Length > i
                ? activeEvent.objectiveProgress[i]
                : 0;

            if (progress < obj.targetAmount)
            {
                allComplete = false;
                break;
            }
        }

        if (allComplete)
        {
            EndEvent(activeEvent, true);
        }
    }

    private void EndEvent(ActiveWorldEvent activeEvent, bool success)
    {
        if (activeEvent == null) return;

        // Retirer des actifs
        _activeEvents?.Remove(activeEvent.eventData.eventId);

        // Mettre en cooldown
        if (_eventCooldowns == null) _eventCooldowns = new Dictionary<string, float>();
        _eventCooldowns[activeEvent.eventData.eventId] =
            Time.time + (activeEvent.eventData.spawnInterval * 3600f);

        // Distribuer les recompenses si succes
        if (success)
        {
            DistributeRewards(activeEvent);
        }

        OnEventEnded?.Invoke(activeEvent.eventData, success);

        if (_debugMode)
        {
            Debug.Log($"[WorldEvent] Ended: {activeEvent.eventData.eventName} - Success: {success}");
        }
    }

    private void DistributeRewards(ActiveWorldEvent activeEvent)
    {
        if (activeEvent.participants == null) return;

        foreach (var playerId in activeEvent.participants)
        {
            // Donner XP et recompenses
            // TODO: Integration avec le systeme de joueur
        }
    }

    private Vector3 GetRandomEventPosition(WorldEventData eventData)
    {
        if (eventData.fixedPositions != null && eventData.fixedPositions.Length > 0)
        {
            return eventData.fixedPositions[UnityEngine.Random.Range(0, eventData.fixedPositions.Length)];
        }

        // Position aleatoire dans le monde
        return new Vector3(
            UnityEngine.Random.Range(-500f, 500f),
            0f,
            UnityEngine.Random.Range(-500f, 500f)
        );
    }

    #endregion
}

/// <summary>
/// Evenement mondial actif.
/// </summary>
[System.Serializable]
public class ActiveWorldEvent
{
    public WorldEventData eventData;
    public Vector3 spawnPosition;
    public float startTime;
    public float endTime;
    public float preparationEndTime;
    public EventState state;
    public HashSet<string> participants;
    public int[] objectiveProgress;

    public float RemainingTime => Mathf.Max(0, endTime - Time.time);
    public float ElapsedTime => Time.time - startTime;
    public int ParticipantCount => participants?.Count ?? 0;
}

/// <summary>
/// Etat d'un evenement.
/// </summary>
public enum EventState
{
    Preparing,
    Active,
    Ending,
    Completed,
    Failed
}

/// <summary>
/// Evenement programme.
/// </summary>
[System.Serializable]
public struct ScheduledEvent
{
    public WorldEventData eventData;
    public float spawnTime;
    public Vector3 position;
}
