using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire des vagues d'ennemis.
/// Gere le spawn, la progression et les recompenses.
/// </summary>
public class WaveManager : MonoBehaviour
{
    #region Singleton

    private static WaveManager _instance;
    public static WaveManager Instance
    {
        get => _instance;
        private set => _instance = value;
    }

    private void Awake()
    {
        _spawnPoints = new List<WaveSpawnPoint>();
        _activeEnemies = new List<GameObject>();
        _stats = new WaveStats();

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
    [SerializeField] private WaveData[] _waves;
    [SerializeField] private bool _autoStart = false;
    [SerializeField] private float _difficultyScaling = 0.1f;

    [Header("Spawning")]
    [SerializeField] private float _spawnHeight = 0f;
    [SerializeField] private LayerMask _enemyLayer;

    // Etat
    private List<WaveSpawnPoint> _spawnPoints;
    private List<GameObject> _activeEnemies;
    private WaveStats _stats;

    private int _currentWaveIndex = -1;
    private WaveState _state = WaveState.Idle;
    private float _waveTimer = 0f;
    private float _waveStartTime = 0f;
    private int _enemiesSpawned = 0;
    private int _enemiesRemaining = 0;

    private int _currentGroupIndex = 0;
    private int _spawnsInCurrentGroup = 0;
    private float _groupTimer = 0f;

    #endregion

    #region Events

    public event Action<int> OnWaveStarted;
    public event Action<int, bool> OnWaveCompleted;
    public event Action<GameObject> OnEnemySpawned;
    public event Action<GameObject> OnEnemyKilled;
    public event Action OnAllWavesCompleted;
    public event Action<int, int> OnEnemyCountChanged;

    #endregion

    #region Properties

    /// <summary>Vague actuelle (1-indexed).</summary>
    public int CurrentWave => _currentWaveIndex + 1;

    /// <summary>Nombre total de vagues.</summary>
    public int TotalWaves => _waves?.Length ?? 0;

    /// <summary>Etat de la vague.</summary>
    public WaveState State => _state;

    /// <summary>Vague en cours?</summary>
    public bool IsWaveActive => _state == WaveState.Spawning || _state == WaveState.InProgress;

    /// <summary>Ennemis restants.</summary>
    public int EnemiesRemaining => _enemiesRemaining;

    /// <summary>Temps depuis le debut de la vague.</summary>
    public float WaveTime => Time.time - _waveStartTime;

    /// <summary>Statistiques.</summary>
    public WaveStats Stats => _stats;

    /// <summary>Donnees de la vague actuelle.</summary>
    public WaveData CurrentWaveData =>
        (_currentWaveIndex >= 0 && _currentWaveIndex < TotalWaves)
            ? _waves[_currentWaveIndex]
            : null;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (_autoStart && TotalWaves > 0)
        {
            StartNextWave();
        }
    }

    private void Update()
    {
        switch (_state)
        {
            case WaveState.Preparing:
                UpdatePreparing();
                break;
            case WaveState.Spawning:
                UpdateSpawning();
                break;
            case WaveState.InProgress:
                UpdateInProgress();
                break;
        }

        // Nettoyer les ennemis detruits
        CleanupDeadEnemies();
    }

    #endregion

    #region Public Methods - Vagues

    /// <summary>
    /// Demarre la vague suivante.
    /// </summary>
    public bool StartNextWave()
    {
        if (_state != WaveState.Idle && _state != WaveState.Completed)
            return false;

        if (_currentWaveIndex >= TotalWaves - 1)
            return false;

        _currentWaveIndex++;
        StartWave(_currentWaveIndex);
        return true;
    }

    /// <summary>
    /// Demarre une vague specifique.
    /// </summary>
    public bool StartWave(int waveIndex)
    {
        if (waveIndex < 0 || waveIndex >= TotalWaves)
            return false;

        _currentWaveIndex = waveIndex;
        var waveData = _waves[waveIndex];

        // Reinitialiser
        _enemiesSpawned = 0;
        _enemiesRemaining = waveData.GetTotalEnemyCount();
        _currentGroupIndex = 0;
        _spawnsInCurrentGroup = 0;
        _groupTimer = 0f;

        // Demarrer le delai
        _waveTimer = waveData.startDelay;
        _state = WaveState.Preparing;

        return true;
    }

    /// <summary>
    /// Force la fin de la vague.
    /// </summary>
    public void ForceEndWave()
    {
        // Tuer tous les ennemis actifs
        if (_activeEnemies != null)
        {
            foreach (var enemy in _activeEnemies)
            {
                if (enemy != null)
                {
                    SafeDestroy(enemy);
                }
            }
            _activeEnemies.Clear();
        }
        _enemiesRemaining = 0;

        // Ne completer la vague que si une est en cours
        if (_state != WaveState.Idle && _state != WaveState.Completed)
        {
            CompleteWave(false);
        }
    }

    /// <summary>
    /// Met en pause les vagues.
    /// </summary>
    public void Pause()
    {
        if (_state == WaveState.Spawning || _state == WaveState.InProgress)
        {
            _state = WaveState.Paused;
        }
    }

    /// <summary>
    /// Reprend les vagues.
    /// </summary>
    public void Resume()
    {
        if (_state == WaveState.Paused)
        {
            _state = _enemiesSpawned < CurrentWaveData?.GetTotalEnemyCount()
                ? WaveState.Spawning
                : WaveState.InProgress;
        }
    }

    #endregion

    #region Public Methods - Spawn Points

    /// <summary>
    /// Enregistre un point de spawn.
    /// </summary>
    public void RegisterSpawnPoint(WaveSpawnPoint spawner)
    {
        if (spawner == null) return;
        if (_spawnPoints == null) _spawnPoints = new List<WaveSpawnPoint>();
        if (_spawnPoints.Contains(spawner)) return;

        _spawnPoints.Add(spawner);
    }

    /// <summary>
    /// Retire un point de spawn.
    /// </summary>
    public void UnregisterSpawnPoint(WaveSpawnPoint spawner)
    {
        if (spawner == null) return;
        if (_spawnPoints != null)
        {
            _spawnPoints.Remove(spawner);
        }
    }

    #endregion

    #region Public Methods - Ennemis

    /// <summary>
    /// Signale qu'un ennemi a ete tue.
    /// </summary>
    public void ReportEnemyKilled(GameObject enemy)
    {
        if (_activeEnemies != null)
        {
            _activeEnemies.Remove(enemy);
        }

        _enemiesRemaining--;
        _stats.totalKills++;

        OnEnemyKilled?.Invoke(enemy);
        OnEnemyCountChanged?.Invoke(_enemiesRemaining, _enemiesSpawned);

        // Enregistrer dans le DefenseManager
        if (DefenseManager.Instance != null)
        {
            DefenseManager.Instance.RecordKill();
        }
    }

    /// <summary>
    /// Obtient tous les ennemis actifs.
    /// </summary>
    public List<GameObject> GetActiveEnemies()
    {
        return _activeEnemies != null ? new List<GameObject>(_activeEnemies) : new List<GameObject>();
    }

    #endregion

    #region Private Methods

    private void UpdatePreparing()
    {
        _waveTimer -= Time.deltaTime;

        if (_waveTimer <= 0f)
        {
            _waveStartTime = Time.time;
            _state = WaveState.Spawning;
            OnWaveStarted?.Invoke(CurrentWave);
        }
    }

    private void UpdateSpawning()
    {
        var waveData = CurrentWaveData;
        if (waveData == null || waveData.enemyGroups == null) return;

        _groupTimer += Time.deltaTime;

        // Verifier si tous les groupes sont termines
        if (_currentGroupIndex >= waveData.enemyGroups.Length)
        {
            // Spawn du boss si necessaire
            if (waveData.isBossWave && waveData.bossPrefab != null)
            {
                SpawnBoss(waveData.bossPrefab);
            }

            _state = WaveState.InProgress;
            return;
        }

        var currentGroup = waveData.enemyGroups[_currentGroupIndex];

        // Verifier le delai du groupe
        if (_spawnsInCurrentGroup == 0 && _groupTimer < currentGroup.spawnDelay)
        {
            return;
        }

        // Spawn
        float interval = currentGroup.spawnInterval > 0 ? currentGroup.spawnInterval : waveData.spawnInterval;
        if (_groupTimer >= currentGroup.spawnDelay + (_spawnsInCurrentGroup * interval))
        {
            if (_spawnsInCurrentGroup < currentGroup.count)
            {
                SpawnEnemy(currentGroup.enemyPrefab, currentGroup.spawnPoint);
                _spawnsInCurrentGroup++;
            }
            else
            {
                // Passer au groupe suivant
                _currentGroupIndex++;
                _spawnsInCurrentGroup = 0;
                _groupTimer = 0f;
            }
        }
    }

    private void UpdateInProgress()
    {
        // Verifier si tous les ennemis sont morts
        if (_enemiesRemaining <= 0)
        {
            bool bonusEarned = WaveTime <= CurrentWaveData?.bonusTimeLimit;
            CompleteWave(bonusEarned);
        }
    }

    private void SpawnEnemy(GameObject prefab, Transform spawnPoint)
    {
        if (prefab == null) return;

        Vector3 position;

        if (spawnPoint != null)
        {
            position = spawnPoint.position;
        }
        else if (_spawnPoints != null && _spawnPoints.Count > 0)
        {
            var randomSpawner = _spawnPoints[UnityEngine.Random.Range(0, _spawnPoints.Count)];
            position = randomSpawner != null ? randomSpawner.GetSpawnPosition() : transform.position;
        }
        else
        {
            position = transform.position;
        }

        position.y = _spawnHeight;

        var enemy = Instantiate(prefab, position, Quaternion.identity);

        // Appliquer les multiplicateurs
        ApplyWaveModifiers(enemy);

        if (_activeEnemies == null) _activeEnemies = new List<GameObject>();
        _activeEnemies.Add(enemy);
        _enemiesSpawned++;

        OnEnemySpawned?.Invoke(enemy);
        OnEnemyCountChanged?.Invoke(_enemiesRemaining, _enemiesSpawned);
    }

    private void SpawnBoss(GameObject bossPrefab)
    {
        SpawnEnemy(bossPrefab, null);
    }

    private void ApplyWaveModifiers(GameObject enemy)
    {
        var waveData = CurrentWaveData;
        if (waveData == null) return;

        // Appliquer le scaling de difficulte global + multiplicateurs de vague
        float difficultyScale = 1f + (_difficultyScaling * _currentWaveIndex);

        float healthMult = waveData.healthMultiplier * difficultyScale;
        float damageMult = waveData.damageMultiplier * difficultyScale;
        float speedMult = waveData.speedMultiplier;

        // Appliquer les modificateurs d'evenements speciaux
        ApplySpecialEventModifiers(waveData, ref healthMult, ref damageMult, ref speedMult);

        // Appliquer via EnemyAI si disponible
        var enemyAI = enemy.GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.ApplyWaveModifiers(healthMult, damageMult, speedMult);
        }
        else
        {
            // Fallback: modifier Health directement
            var health = enemy.GetComponent<Health>();
            if (health != null)
            {
                float newMaxHealth = health.MaxHealth * healthMult;
                health.SetMaxHealth(newMaxHealth);
                health.ResetHealth();
            }
        }
    }

    private void ApplySpecialEventModifiers(WaveData waveData, ref float healthMult, ref float damageMult, ref float speedMult)
    {
        if (!waveData.hasSpecialEvent) return;

        switch (waveData.specialEvent)
        {
            case SpecialEventType.FastEnemies:
                speedMult *= 1.5f;
                break;

            case SpecialEventType.ArmoredEnemies:
                healthMult *= 2f;
                break;

            case SpecialEventType.HealingEnemies:
                healthMult *= 1.25f;
                break;

            case SpecialEventType.ExplosiveEnemies:
                damageMult *= 1.5f;
                healthMult *= 0.75f;
                break;

            case SpecialEventType.DoubleRewards:
                // Gere dans GiveRewards
                break;

            case SpecialEventType.InvisibleEnemies:
                // Gere par le rendu de l'ennemi
                break;
        }
    }

    private void CompleteWave(bool bonusEarned)
    {
        _stats.wavesCompleted++;
        _state = WaveState.Completed;

        // Donner les recompenses
        var waveData = CurrentWaveData;
        if (waveData != null)
        {
            GiveRewards(waveData.completionRewards);

            if (bonusEarned && waveData.bonusRewards != null)
            {
                GiveRewards(waveData.bonusRewards);
            }
        }

        OnWaveCompleted?.Invoke(CurrentWave, bonusEarned);

        // Verifier si toutes les vagues sont terminees
        if (_currentWaveIndex >= TotalWaves - 1)
        {
            OnAllWavesCompleted?.Invoke();
        }
        else
        {
            _state = WaveState.Idle;
        }
    }

    private void GiveRewards(ResourceCost[] rewards)
    {
        if (rewards == null) return;
        if (ResourceManager.Instance == null) return;

        var waveData = CurrentWaveData;
        float rewardMult = waveData != null ? waveData.rewardMultiplier : 1f;

        // Appliquer le bonus de double recompenses
        if (waveData != null && waveData.hasSpecialEvent && waveData.specialEvent == SpecialEventType.DoubleRewards)
        {
            rewardMult *= 2f;
        }

        foreach (var reward in rewards)
        {
            int amount = Mathf.RoundToInt(reward.amount * rewardMult);
            ResourceManager.Instance.AddResource(reward.resourceType, amount);
        }
    }

    private void CleanupDeadEnemies()
    {
        if (_activeEnemies == null) return;
        _activeEnemies.RemoveAll(e => e == null);
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Configure les vagues.
    /// </summary>
    public void SetWaves(WaveData[] waves)
    {
        _waves = waves;
        _currentWaveIndex = -1;
        _state = WaveState.Idle;
    }

    /// <summary>
    /// Reinitialise le manager.
    /// </summary>
    public void ResetWaves()
    {
        ForceEndWave();
        _currentWaveIndex = -1;
        _state = WaveState.Idle;
        _stats = new WaveStats();
    }

    #endregion
}

/// <summary>
/// Etats d'une vague.
/// </summary>
public enum WaveState
{
    /// <summary>En attente.</summary>
    Idle,

    /// <summary>Preparation (delai avant spawn).</summary>
    Preparing,

    /// <summary>Spawn en cours.</summary>
    Spawning,

    /// <summary>Tous spawnes, en combat.</summary>
    InProgress,

    /// <summary>En pause.</summary>
    Paused,

    /// <summary>Terminee.</summary>
    Completed
}

/// <summary>
/// Statistiques des vagues.
/// </summary>
[System.Serializable]
public class WaveStats
{
    public int wavesCompleted;
    public int totalKills;
    public float totalTime;
    public int bonusesEarned;
}
