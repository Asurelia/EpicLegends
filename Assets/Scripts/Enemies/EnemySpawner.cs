using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawner d'ennemis.
/// Gere l'apparition et le recyclage des ennemis.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private EnemyData[] _enemyTypes;
    [SerializeField] private int _maxEnemies = 10;
    [SerializeField] private float _spawnRadius = 5f;

    [Header("Timing")]
    [SerializeField] private float _spawnInterval = 5f;
    [SerializeField] private float _initialDelay = 2f;
    [SerializeField] private bool _autoSpawn = true;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private bool _randomizeSpawnPoint = true;

    #endregion

    #region Private Fields

    private List<GameObject> _activeEnemies = new List<GameObject>();
    private float _spawnTimer;
    private bool _isSpawning;
    private int _nextSpawnPointIndex;

    #endregion

    #region Events

    /// <summary>
    /// Declenche quand un ennemi est spawn.
    /// </summary>
    public event Action<GameObject> OnEnemySpawned;

    /// <summary>
    /// Declenche quand un ennemi meurt.
    /// </summary>
    public event Action<GameObject> OnEnemyDied;

    /// <summary>
    /// Declenche quand tous les ennemis sont morts.
    /// </summary>
    public event Action OnAllEnemiesDefeated;

    #endregion

    #region Properties

    /// <summary>
    /// Nombre d'ennemis actifs.
    /// </summary>
    public int ActiveEnemyCount => _activeEnemies.Count;

    /// <summary>
    /// Peut-on spawner plus d'ennemis?
    /// </summary>
    public bool CanSpawn => _activeEnemies.Count < _maxEnemies;

    /// <summary>
    /// Le spawner est-il actif?
    /// </summary>
    public bool IsSpawning => _isSpawning;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        _spawnTimer = _initialDelay;

        if (_autoSpawn)
        {
            _isSpawning = true;
        }
    }

    private void Update()
    {
        if (!_isSpawning) return;

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0f && CanSpawn)
        {
            SpawnRandomEnemy();
            _spawnTimer = _spawnInterval;
        }

        // Nettoyer les ennemis detruits
        CleanupDeadEnemies();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Demarre le spawning.
    /// </summary>
    public void StartSpawning()
    {
        _isSpawning = true;
    }

    /// <summary>
    /// Arrete le spawning.
    /// </summary>
    public void StopSpawning()
    {
        _isSpawning = false;
    }

    /// <summary>
    /// Spawn un ennemi aleatoire.
    /// </summary>
    public GameObject SpawnRandomEnemy()
    {
        if (!CanSpawn || _enemyTypes == null || _enemyTypes.Length == 0) return null;

        int randomIndex = UnityEngine.Random.Range(0, _enemyTypes.Length);
        return SpawnEnemy(_enemyTypes[randomIndex]);
    }

    /// <summary>
    /// Spawn un ennemi specifique.
    /// </summary>
    public GameObject SpawnEnemy(EnemyData enemyData)
    {
        if (!CanSpawn || enemyData == null || enemyData.prefab == null) return null;

        Vector3 spawnPosition = GetSpawnPosition();
        Quaternion spawnRotation = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);

        GameObject enemy = Instantiate(enemyData.prefab, spawnPosition, spawnRotation);
        _activeEnemies.Add(enemy);

        // S'abonner a l'event de mort
        if (enemy.TryGetComponent<Health>(out var health))
        {
            health.OnDeath += () => HandleEnemyDeath(enemy);
        }

        OnEnemySpawned?.Invoke(enemy);
        return enemy;
    }

    /// <summary>
    /// Spawn une vague d'ennemis.
    /// </summary>
    public void SpawnWave(int count)
    {
        for (int i = 0; i < count && CanSpawn; i++)
        {
            SpawnRandomEnemy();
        }
    }

    /// <summary>
    /// Detruit tous les ennemis actifs.
    /// </summary>
    public void KillAllEnemies()
    {
        foreach (var enemy in _activeEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy);
            }
        }
        _activeEnemies.Clear();
        OnAllEnemiesDefeated?.Invoke();
    }

    #endregion

    #region Private Methods

    private Vector3 GetSpawnPosition()
    {
        // Utiliser les spawn points si disponibles
        if (_spawnPoints != null && _spawnPoints.Length > 0)
        {
            Transform point;
            if (_randomizeSpawnPoint)
            {
                point = _spawnPoints[UnityEngine.Random.Range(0, _spawnPoints.Length)];
            }
            else
            {
                point = _spawnPoints[_nextSpawnPointIndex];
                _nextSpawnPointIndex = (_nextSpawnPointIndex + 1) % _spawnPoints.Length;
            }
            return point.position;
        }

        // Sinon, position aleatoire dans le rayon
        Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * _spawnRadius;
        return transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        _activeEnemies.Remove(enemy);
        OnEnemyDied?.Invoke(enemy);

        if (_activeEnemies.Count == 0)
        {
            OnAllEnemiesDefeated?.Invoke();
        }
    }

    private void CleanupDeadEnemies()
    {
        _activeEnemies.RemoveAll(e => e == null);
    }

    #endregion

    #region Debug

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _spawnRadius);

        if (_spawnPoints != null)
        {
            Gizmos.color = Color.yellow;
            foreach (var point in _spawnPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawSphere(point.position, 0.5f);
                }
            }
        }
    }

    #endregion
}
