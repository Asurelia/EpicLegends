using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire de pool d'objets.
/// Optimise les performances en reutilisant les objets.
/// </summary>
public class ObjectPoolManager : MonoBehaviour
{
    #region Singleton

    private static ObjectPoolManager _instance;
    public static ObjectPoolManager Instance
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

        _pools = new Dictionary<string, ObjectPool>();
        _pooledObjectCache = new Dictionary<GameObject, IPooledObject>();
        InitializePools();
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
    [SerializeField] private ObjectPoolData[] _poolConfigs;
    [SerializeField] private Transform _poolContainer;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;

    // Pools actifs
    private Dictionary<string, ObjectPool> _pools;

    // PERF FIX: Cache IPooledObject components to avoid GetComponent calls in Spawn/Return
    private Dictionary<GameObject, IPooledObject> _pooledObjectCache;

    #endregion

    #region Events

    /// <summary>Declenche lors de la creation d'un pool.</summary>
    public event Action<string> OnPoolCreated;

    /// <summary>Declenche lors du spawn d'un objet.</summary>
    public event Action<string, GameObject> OnObjectSpawned;

    /// <summary>Declenche lors du retour d'un objet.</summary>
    public event Action<string, GameObject> OnObjectReturned;

    #endregion

    #region Properties

    /// <summary>Nombre de pools actifs.</summary>
    public int ActivePoolCount => _pools?.Count ?? 0;

    #endregion

    #region Public Methods

    /// <summary>
    /// Cree un nouveau pool.
    /// </summary>
    /// <param name="config">Configuration du pool.</param>
    public void CreatePool(ObjectPoolData config)
    {
        if (config == null || config.prefab == null) return;
        if (_pools == null) _pools = new Dictionary<string, ObjectPool>();

        if (_pools.ContainsKey(config.poolName))
        {
            LogWarning($"Pool already exists: {config.poolName}");
            return;
        }

        var pool = new ObjectPool
        {
            config = config,
            available = new Queue<GameObject>(),
            active = new List<GameObject>()
        };

        // Pre-instancier les objets
        for (int i = 0; i < config.initialSize; i++)
        {
            var obj = CreatePooledObject(config);
            pool.available.Enqueue(obj);
        }

        _pools[config.poolName] = pool;
        OnPoolCreated?.Invoke(config.poolName);

        Log($"Pool created: {config.poolName} ({config.initialSize} objects)");
    }

    /// <summary>
    /// Obtient un objet du pool.
    /// </summary>
    /// <param name="poolName">Nom du pool.</param>
    /// <param name="position">Position.</param>
    /// <param name="rotation">Rotation.</param>
    /// <returns>Objet ou null.</returns>
    public GameObject Spawn(string poolName, Vector3 position, Quaternion rotation)
    {
        if (_pools == null || !_pools.TryGetValue(poolName, out var pool))
        {
            LogError($"Pool not found: {poolName}");
            return null;
        }

        GameObject obj;

        if (pool.available.Count > 0)
        {
            obj = pool.available.Dequeue();
        }
        else if (pool.config.canExpand && pool.active.Count < pool.config.maxSize)
        {
            obj = CreatePooledObject(pool.config);
            Log($"Pool expanded: {poolName}");
        }
        else
        {
            LogWarning($"Pool exhausted: {poolName}");
            return null;
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);

        pool.active.Add(obj);

        // Notifier le composant (utilise le cache pour eviter GetComponent)
        var pooled = GetCachedPooledObject(obj);
        pooled?.OnSpawn();

        OnObjectSpawned?.Invoke(poolName, obj);

        return obj;
    }

    /// <summary>
    /// Retourne un objet au pool.
    /// </summary>
    /// <param name="poolName">Nom du pool.</param>
    /// <param name="obj">Objet a retourner.</param>
    public void Return(string poolName, GameObject obj)
    {
        if (obj == null) return;
        if (_pools == null || !_pools.TryGetValue(poolName, out var pool))
        {
            SafeDestroy(obj);
            return;
        }

        // Notifier le composant (utilise le cache pour eviter GetComponent)
        var pooled = GetCachedPooledObject(obj);
        pooled?.OnDespawn();

        obj.SetActive(false);
        obj.transform.SetParent(_poolContainer);

        pool.active.Remove(obj);
        pool.available.Enqueue(obj);

        OnObjectReturned?.Invoke(poolName, obj);
    }

    /// <summary>
    /// Retourne un objet apres un delai.
    /// </summary>
    /// <param name="poolName">Nom du pool.</param>
    /// <param name="obj">Objet a retourner.</param>
    /// <param name="delay">Delai en secondes.</param>
    public void ReturnDelayed(string poolName, GameObject obj, float delay)
    {
        if (obj == null) return;
        StartCoroutine(ReturnDelayedCoroutine(poolName, obj, delay));
    }

    /// <summary>
    /// Precharge un pool.
    /// </summary>
    /// <param name="poolName">Nom du pool.</param>
    /// <param name="count">Nombre d'objets.</param>
    public void Warmup(string poolName, int count)
    {
        if (_pools == null || !_pools.TryGetValue(poolName, out var pool)) return;

        int toCreate = Mathf.Min(count, pool.config.maxSize - pool.available.Count - pool.active.Count);

        for (int i = 0; i < toCreate; i++)
        {
            var obj = CreatePooledObject(pool.config);
            pool.available.Enqueue(obj);
        }

        Log($"Pool warmed up: {poolName} (+{toCreate})");
    }

    /// <summary>
    /// Nettoie un pool (detruit les objets inactifs).
    /// </summary>
    /// <param name="poolName">Nom du pool.</param>
    /// <param name="keepCount">Nombre a conserver.</param>
    public void Cleanup(string poolName, int keepCount = 0)
    {
        if (_pools == null || !_pools.TryGetValue(poolName, out var pool)) return;

        while (pool.available.Count > keepCount)
        {
            var obj = pool.available.Dequeue();
            // PERF FIX: Remove from cache before destroying
            _pooledObjectCache?.Remove(obj);
            SafeDestroy(obj);
        }

        Log($"Pool cleaned: {poolName} (kept {keepCount})");
    }

    /// <summary>
    /// Obtient les statistiques d'un pool.
    /// </summary>
    /// <param name="poolName">Nom du pool.</param>
    /// <returns>Statistiques.</returns>
    public PoolStats GetPoolStats(string poolName)
    {
        if (_pools == null || !_pools.TryGetValue(poolName, out var pool))
        {
            return new PoolStats();
        }

        return new PoolStats
        {
            poolName = poolName,
            availableCount = pool.available.Count,
            activeCount = pool.active.Count,
            maxSize = pool.config.maxSize
        };
    }

    /// <summary>
    /// Obtient les statistiques globales.
    /// </summary>
    /// <returns>Statistiques.</returns>
    public List<PoolStats> GetAllPoolStats()
    {
        var result = new List<PoolStats>();

        if (_pools != null)
        {
            foreach (var pool in _pools.Values)
            {
                result.Add(new PoolStats
                {
                    poolName = pool.config.poolName,
                    availableCount = pool.available.Count,
                    activeCount = pool.active.Count,
                    maxSize = pool.config.maxSize
                });
            }
        }

        return result;
    }

    #endregion

    #region Private Methods

    private void InitializePools()
    {
        if (_poolConfigs == null) return;

        foreach (var config in _poolConfigs)
        {
            CreatePool(config);
        }
    }

    private GameObject CreatePooledObject(ObjectPoolData config)
    {
        var obj = Instantiate(config.prefab, _poolContainer);
        obj.name = $"{config.poolName}_{obj.GetInstanceID()}";
        obj.SetActive(false);

        // PERF FIX: Pre-cache IPooledObject component at creation time
        var pooledComponent = obj.GetComponent<IPooledObject>();
        if (pooledComponent != null)
        {
            _pooledObjectCache[obj] = pooledComponent;
        }

        return obj;
    }

    /// <summary>
    /// Recupere le composant IPooledObject depuis le cache.
    /// </summary>
    private IPooledObject GetCachedPooledObject(GameObject obj)
    {
        if (obj == null) return null;

        // Try cache first
        if (_pooledObjectCache.TryGetValue(obj, out var cached))
        {
            return cached;
        }

        // Fallback: GetComponent and cache for future use
        var pooled = obj.GetComponent<IPooledObject>();
        if (pooled != null)
        {
            _pooledObjectCache[obj] = pooled;
        }
        return pooled;
    }

    private System.Collections.IEnumerator ReturnDelayedCoroutine(string poolName, GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        Return(poolName, obj);
    }

    private void Log(string message)
    {
        if (_debugMode)
        {
            Debug.Log($"[Pool] {message}");
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[Pool] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[Pool] {message}");
    }

    private void OnDestroy()
    {
        // MAJOR FIX: Stop all coroutines to prevent memory leaks
        StopAllCoroutines();
    }

    #endregion
}

/// <summary>
/// Interface pour les objets poolables.
/// </summary>
public interface IPooledObject
{
    /// <summary>Appele lors du spawn.</summary>
    void OnSpawn();

    /// <summary>Appele lors du despawn.</summary>
    void OnDespawn();
}

/// <summary>
/// Donnees de configuration d'un pool.
/// </summary>
[System.Serializable]
public class ObjectPoolData
{
    [Tooltip("Nom unique du pool")]
    public string poolName;

    [Tooltip("Prefab a instancier")]
    public GameObject prefab;

    [Tooltip("Taille initiale du pool")]
    [Range(1, 100)]
    public int initialSize = 10;

    [Tooltip("Le pool peut-il s'etendre?")]
    public bool canExpand = true;

    [Tooltip("Taille maximale du pool")]
    [Range(1, 500)]
    public int maxSize = 100;
}

/// <summary>
/// Objet dans un pool.
/// </summary>
[System.Serializable]
public class PooledObject
{
    public GameObject gameObject;
    public bool isActive;
    public float spawnTime;
}

/// <summary>
/// Pool d'objets interne.
/// </summary>
internal class ObjectPool
{
    public ObjectPoolData config;
    public Queue<GameObject> available;
    public List<GameObject> active;
}

/// <summary>
/// Statistiques d'un pool.
/// </summary>
[System.Serializable]
public struct PoolStats
{
    public string poolName;
    public int availableCount;
    public int activeCount;
    public int maxSize;

    public int TotalCount => availableCount + activeCount;
    public float UsagePercent => maxSize > 0 ? (float)activeCount / maxSize : 0f;
}
