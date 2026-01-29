using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire de synchronisation reseau.
/// Synchronise joueurs, creatures, ennemis, projectiles et batiments.
/// </summary>
public class NetworkSyncManager : MonoBehaviour
{
    #region Singleton

    private static NetworkSyncManager _instance;
    public static NetworkSyncManager Instance
    {
        get => _instance;
        private set => _instance = value;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            SafeDestroy(gameObject);
            return;
        }
        Instance = this;

        _syncedObjects = new Dictionary<string, NetworkSyncObject>();
        _pendingUpdates = new Queue<SyncUpdate>();
        _interpolationBuffers = new Dictionary<string, Queue<SyncSnapshot>>();
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

    #region Constants

    /// <summary>Taille du buffer d'interpolation.</summary>
    private const int INTERPOLATION_BUFFER_SIZE = 10;

    /// <summary>Delai d'interpolation (ms).</summary>
    private const float INTERPOLATION_DELAY = 100f;

    #endregion

    #region Fields

    [Header("Configuration")]
    [SerializeField] private float _syncRate = 20f; // Updates par seconde
    [SerializeField] private float _positionThreshold = 0.01f;
    [SerializeField] private float _rotationThreshold = 0.1f;
    [SerializeField] private bool _useInterpolation = true;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;

    // Objets synchronises
    private Dictionary<string, NetworkSyncObject> _syncedObjects;

    // Mises a jour en attente
    private Queue<SyncUpdate> _pendingUpdates;

    // Buffers d'interpolation
    private Dictionary<string, Queue<SyncSnapshot>> _interpolationBuffers;

    // Timer de sync
    private float _nextSyncTime;

    #endregion

    #region Events

    /// <summary>Declenche lors de l'enregistrement d'un objet.</summary>
    public event Action<NetworkSyncObject> OnObjectRegistered;

    /// <summary>Declenche lors du desenregistrement d'un objet.</summary>
    public event Action<string> OnObjectUnregistered;

    /// <summary>Declenche lors d'une mise a jour sync.</summary>
    public event Action<SyncUpdate> OnSyncUpdate;

    #endregion

    #region Properties

    /// <summary>Nombre d'objets synchronises.</summary>
    public int SyncedObjectCount => _syncedObjects?.Count ?? 0;

    /// <summary>Taux de synchronisation.</summary>
    public float SyncRate => _syncRate;

    /// <summary>Utilise l'interpolation?</summary>
    public bool UseInterpolation => _useInterpolation;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        if (Time.time >= _nextSyncTime)
        {
            ProcessSyncUpdates();
            _nextSyncTime = Time.time + (1f / _syncRate);
        }

        if (_useInterpolation)
        {
            InterpolateObjects();
        }
    }

    #endregion

    #region Public Methods - Registration

    /// <summary>
    /// Enregistre un objet pour synchronisation.
    /// </summary>
    /// <param name="syncObject">Objet a synchroniser.</param>
    public void RegisterObject(NetworkSyncObject syncObject)
    {
        if (syncObject == null) return;
        if (string.IsNullOrEmpty(syncObject.NetworkId))
        {
            syncObject.NetworkId = GenerateNetworkId();
        }

        if (_syncedObjects == null)
        {
            _syncedObjects = new Dictionary<string, NetworkSyncObject>();
        }

        _syncedObjects[syncObject.NetworkId] = syncObject;

        // Creer buffer d'interpolation
        if (_interpolationBuffers == null)
        {
            _interpolationBuffers = new Dictionary<string, Queue<SyncSnapshot>>();
        }
        _interpolationBuffers[syncObject.NetworkId] = new Queue<SyncSnapshot>();

        OnObjectRegistered?.Invoke(syncObject);

        if (_debugMode)
        {
            Debug.Log($"[Sync] Registered: {syncObject.NetworkId} ({syncObject.SyncType})");
        }
    }

    /// <summary>
    /// Desenregistre un objet.
    /// </summary>
    /// <param name="networkId">ID reseau.</param>
    public void UnregisterObject(string networkId)
    {
        if (string.IsNullOrEmpty(networkId)) return;

        _syncedObjects?.Remove(networkId);
        _interpolationBuffers?.Remove(networkId);

        OnObjectUnregistered?.Invoke(networkId);

        if (_debugMode)
        {
            Debug.Log($"[Sync] Unregistered: {networkId}");
        }
    }

    /// <summary>
    /// Obtient un objet synchronise.
    /// </summary>
    /// <param name="networkId">ID reseau.</param>
    /// <returns>Objet ou null.</returns>
    public NetworkSyncObject GetSyncedObject(string networkId)
    {
        if (_syncedObjects == null) return null;
        return _syncedObjects.TryGetValue(networkId, out var obj) ? obj : null;
    }

    /// <summary>
    /// Obtient tous les objets d'un type.
    /// </summary>
    /// <param name="syncType">Type de sync.</param>
    /// <returns>Liste des objets.</returns>
    public List<NetworkSyncObject> GetObjectsByType(SyncType syncType)
    {
        var result = new List<NetworkSyncObject>();

        if (_syncedObjects != null)
        {
            foreach (var obj in _syncedObjects.Values)
            {
                if (obj.SyncType == syncType)
                {
                    result.Add(obj);
                }
            }
        }

        return result;
    }

    #endregion

    #region Public Methods - Sync

    /// <summary>
    /// Envoie une mise a jour de synchronisation.
    /// </summary>
    /// <param name="update">Mise a jour.</param>
    public void SendSyncUpdate(SyncUpdate update)
    {
        if (update == null) return;

        if (_pendingUpdates == null)
        {
            _pendingUpdates = new Queue<SyncUpdate>();
        }

        _pendingUpdates.Enqueue(update);

        // TODO: Envoyer via reseau
        OnSyncUpdate?.Invoke(update);
    }

    /// <summary>
    /// Recoit une mise a jour de synchronisation.
    /// </summary>
    /// <param name="update">Mise a jour.</param>
    public void ReceiveSyncUpdate(SyncUpdate update)
    {
        if (update == null) return;

        var syncObject = GetSyncedObject(update.networkId);
        if (syncObject == null) return;

        // Ajouter au buffer d'interpolation
        if (_useInterpolation && _interpolationBuffers != null)
        {
            if (_interpolationBuffers.TryGetValue(update.networkId, out var buffer))
            {
                var snapshot = new SyncSnapshot
                {
                    timestamp = update.timestamp,
                    position = update.position,
                    rotation = update.rotation,
                    velocity = update.velocity
                };

                buffer.Enqueue(snapshot);

                // Limiter la taille du buffer
                while (buffer.Count > INTERPOLATION_BUFFER_SIZE)
                {
                    buffer.Dequeue();
                }
            }
        }
        else
        {
            // Appliquer immediatement
            ApplySyncUpdate(syncObject, update);
        }
    }

    /// <summary>
    /// Force une synchronisation complete.
    /// </summary>
    public void ForceSyncAll()
    {
        if (_syncedObjects == null) return;

        foreach (var syncObject in _syncedObjects.Values)
        {
            if (syncObject.IsOwner)
            {
                var update = syncObject.CreateSyncUpdate();
                SendSyncUpdate(update);
            }
        }
    }

    #endregion

    #region Public Methods - World State

    /// <summary>
    /// Synchronise l'etat du monde.
    /// </summary>
    /// <param name="worldState">Etat du monde.</param>
    public void SyncWorldState(WorldStateData worldState)
    {
        if (worldState == null) return;

        // TODO: Envoyer via reseau

        if (_debugMode)
        {
            Debug.Log($"[Sync] World state synced: {worldState.timestamp}");
        }
    }

    /// <summary>
    /// Obtient l'etat du monde actuel.
    /// </summary>
    /// <returns>Etat du monde.</returns>
    public WorldStateData GetCurrentWorldState()
    {
        var state = new WorldStateData
        {
            timestamp = DateTime.UtcNow,
            timeOfDay = 0f, // TODO: Integration avec systeme jour/nuit
            weatherType = WeatherType.Clear
        };

        // Collecter les donnees des joueurs
        state.playerStates = new List<PlayerNetworkData>();
        foreach (var obj in GetObjectsByType(SyncType.Player))
        {
            if (obj != null)
            {
                state.playerStates.Add(obj.GetPlayerData());
            }
        }

        return state;
    }

    #endregion

    #region Private Methods

    private void ProcessSyncUpdates()
    {
        if (_pendingUpdates == null || _pendingUpdates.Count == 0) return;

        int processed = 0;
        int maxPerFrame = 100;

        while (_pendingUpdates.Count > 0 && processed < maxPerFrame)
        {
            var update = _pendingUpdates.Dequeue();
            // TODO: Envoyer via reseau
            processed++;
        }
    }

    private void InterpolateObjects()
    {
        if (_syncedObjects == null || _interpolationBuffers == null) return;

        float targetTime = Time.time - (INTERPOLATION_DELAY / 1000f);

        foreach (var kvp in _interpolationBuffers)
        {
            var networkId = kvp.Key;
            var buffer = kvp.Value;

            if (buffer.Count < 2) continue;

            var syncObject = GetSyncedObject(networkId);
            if (syncObject == null || syncObject.IsOwner) continue;

            // Trouver les snapshots pour interpolation
            SyncSnapshot? from = null;
            SyncSnapshot? to = null;

            var snapshots = buffer.ToArray();
            for (int i = 0; i < snapshots.Length - 1; i++)
            {
                float t1 = (float)(snapshots[i].timestamp - DateTime.UtcNow).TotalSeconds;
                float t2 = (float)(snapshots[i + 1].timestamp - DateTime.UtcNow).TotalSeconds;

                if (t1 <= targetTime && t2 >= targetTime)
                {
                    from = snapshots[i];
                    to = snapshots[i + 1];
                    break;
                }
            }

            if (from.HasValue && to.HasValue)
            {
                float t = CalculateInterpolationFactor(from.Value, to.Value, targetTime);
                syncObject.Interpolate(from.Value, to.Value, t);
            }
        }
    }

    private float CalculateInterpolationFactor(SyncSnapshot from, SyncSnapshot to, float targetTime)
    {
        float fromTime = (float)(from.timestamp - DateTime.UtcNow).TotalSeconds;
        float toTime = (float)(to.timestamp - DateTime.UtcNow).TotalSeconds;

        if (Mathf.Approximately(fromTime, toTime)) return 0f;

        return Mathf.Clamp01((targetTime - fromTime) / (toTime - fromTime));
    }

    private void ApplySyncUpdate(NetworkSyncObject syncObject, SyncUpdate update)
    {
        if (syncObject == null || update == null) return;

        syncObject.ApplySync(update);
    }

    private string GenerateNetworkId()
    {
        return $"NET{DateTime.UtcNow.Ticks:X}-{UnityEngine.Random.Range(1000, 9999)}";
    }

    #endregion
}

/// <summary>
/// Types de synchronisation.
/// </summary>
public enum SyncType
{
    /// <summary>Joueur.</summary>
    Player,

    /// <summary>Creature capturee.</summary>
    Creature,

    /// <summary>Ennemi.</summary>
    Enemy,

    /// <summary>Projectile.</summary>
    Projectile,

    /// <summary>Batiment.</summary>
    Building,

    /// <summary>Etat du monde.</summary>
    WorldState,

    /// <summary>Item au sol.</summary>
    DroppedItem,

    /// <summary>Effet visuel.</summary>
    VFX
}

/// <summary>
/// Mise a jour de synchronisation.
/// </summary>
[System.Serializable]
public class SyncUpdate
{
    public string networkId;
    public SyncType syncType;
    public DateTime timestamp;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public byte[] customData;
    public bool isReliable;
}

/// <summary>
/// Snapshot pour interpolation.
/// </summary>
[System.Serializable]
public struct SyncSnapshot
{
    public DateTime timestamp;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
}

/// <summary>
/// Donnees reseau d'un joueur.
/// </summary>
[System.Serializable]
public class PlayerNetworkData
{
    public string playerId;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public int currentHealth;
    public int maxHealth;
    public int currentMana;
    public int level;
    public bool isAlive;
    public string currentAnimation;
    public int equippedWeaponId;
}

/// <summary>
/// Etat du monde synchronise.
/// </summary>
[System.Serializable]
public class WorldStateData
{
    public DateTime timestamp;
    public float timeOfDay;
    public WeatherType weatherType;
    public List<PlayerNetworkData> playerStates;
    public List<string> activeWorldEvents;
    public List<string> completedQuests;
}

/// <summary>
/// Objet synchronise sur le reseau.
/// </summary>
public abstract class NetworkSyncObject : MonoBehaviour
{
    [SerializeField] protected SyncType _syncType;
    [SerializeField] protected string _networkId;
    [SerializeField] protected bool _isOwner;

    public SyncType SyncType => _syncType;
    public string NetworkId
    {
        get => _networkId;
        set => _networkId = value;
    }
    public bool IsOwner => _isOwner;

    protected virtual void Start()
    {
        NetworkSyncManager.Instance?.RegisterObject(this);
    }

    protected virtual void OnDestroy()
    {
        NetworkSyncManager.Instance?.UnregisterObject(_networkId);
    }

    /// <summary>Cree une mise a jour de sync.</summary>
    public abstract SyncUpdate CreateSyncUpdate();

    /// <summary>Applique une mise a jour de sync.</summary>
    public abstract void ApplySync(SyncUpdate update);

    /// <summary>Interpole entre deux snapshots.</summary>
    public abstract void Interpolate(SyncSnapshot from, SyncSnapshot to, float t);

    /// <summary>Obtient les donnees joueur.</summary>
    public virtual PlayerNetworkData GetPlayerData() { return null; }
}
