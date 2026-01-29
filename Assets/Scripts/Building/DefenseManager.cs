using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire central du systeme de defense.
/// Gere les tours, les zones de defense et les statistiques.
/// </summary>
public class DefenseManager : MonoBehaviour
{
    #region Singleton

    private static DefenseManager _instance;
    public static DefenseManager Instance
    {
        get => _instance;
        private set => _instance = value;
    }

    private void Awake()
    {
        _towers = new List<DefenseTower>();
        _zones = new List<DefenseZone>();
        _stats = new DefenseStats();

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
    [SerializeField] private float _updateInterval = 0.5f;
    [SerializeField] private int _maxTowers = 50;
    [SerializeField] private bool _autoTargetPriority = true;

    [Header("Reference")]
    [SerializeField] private TowerData[] _availableTowers;

    // Tours et zones
    private List<DefenseTower> _towers;
    private List<DefenseZone> _zones;
    private DefenseStats _stats;
    private float _updateTimer = 0f;

    #endregion

    #region Events

    public event Action<DefenseTower> OnTowerPlaced;
    public event Action<DefenseTower> OnTowerRemoved;
    public event Action<DefenseTower, int> OnTowerUpgraded;
    public event Action<float> OnDamageDealt;
    public event Action<int> OnEnemyKilled;

    #endregion

    #region Properties

    /// <summary>Nombre de tours.</summary>
    public int TowerCount => _towers?.Count ?? 0;

    /// <summary>Limite de tours.</summary>
    public int MaxTowers => _maxTowers;

    /// <summary>Peut placer plus de tours?</summary>
    public bool CanPlaceMore => TowerCount < _maxTowers;

    /// <summary>Statistiques de defense.</summary>
    public DefenseStats Stats => _stats;

    /// <summary>Tours disponibles.</summary>
    public TowerData[] AvailableTowers => _availableTowers;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        _updateTimer += Time.deltaTime;

        if (_updateTimer >= _updateInterval)
        {
            _updateTimer = 0f;
            UpdateDefenses();
        }
    }

    #endregion

    #region Public Methods - Tours

    /// <summary>
    /// Enregistre une tour.
    /// </summary>
    public void RegisterTower(DefenseTower tower)
    {
        if (tower == null) return;
        if (_towers == null) _towers = new List<DefenseTower>();
        if (_towers.Contains(tower)) return;

        _towers.Add(tower);
        OnTowerPlaced?.Invoke(tower);
    }

    /// <summary>
    /// Retire une tour.
    /// </summary>
    public void UnregisterTower(DefenseTower tower)
    {
        if (tower == null) return;
        if (_towers == null) return;

        if (_towers.Remove(tower))
        {
            OnTowerRemoved?.Invoke(tower);
        }
    }

    /// <summary>
    /// Obtient toutes les tours.
    /// </summary>
    public List<DefenseTower> GetAllTowers()
    {
        return _towers != null ? new List<DefenseTower>(_towers) : new List<DefenseTower>();
    }

    /// <summary>
    /// Obtient les tours par type.
    /// </summary>
    public List<DefenseTower> GetTowersByType(TowerType type)
    {
        var result = new List<DefenseTower>();

        if (_towers != null)
        {
            foreach (var tower in _towers)
            {
                if (tower != null && tower.TowerType == type)
                {
                    result.Add(tower);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Obtient les tours dans une zone.
    /// </summary>
    public List<DefenseTower> GetTowersInRange(Vector3 center, float radius)
    {
        var result = new List<DefenseTower>();

        if (_towers != null)
        {
            foreach (var tower in _towers)
            {
                if (tower != null)
                {
                    float dist = Vector3.Distance(center, tower.transform.position);
                    if (dist <= radius)
                    {
                        result.Add(tower);
                    }
                }
            }
        }

        return result;
    }

    #endregion

    #region Public Methods - Zones

    /// <summary>
    /// Enregistre une zone de defense.
    /// </summary>
    public void RegisterZone(DefenseZone zone)
    {
        if (zone == null) return;
        if (_zones == null) _zones = new List<DefenseZone>();
        if (_zones.Contains(zone)) return;

        _zones.Add(zone);
    }

    /// <summary>
    /// Retire une zone.
    /// </summary>
    public void UnregisterZone(DefenseZone zone)
    {
        if (zone == null) return;
        if (_zones != null)
        {
            _zones.Remove(zone);
        }
    }

    /// <summary>
    /// Verifie si un point est dans une zone de defense.
    /// </summary>
    public bool IsInDefendedZone(Vector3 point)
    {
        if (_zones == null) return false;

        foreach (var zone in _zones)
        {
            if (zone != null && zone.ContainsPoint(point))
            {
                return true;
            }
        }
        return false;
    }

    #endregion

    #region Public Methods - Statistiques

    /// <summary>
    /// Enregistre des degats infliges.
    /// </summary>
    public void RecordDamage(float damage)
    {
        if (_stats == null) _stats = new DefenseStats();
        _stats.totalDamageDealt += damage;
        OnDamageDealt?.Invoke(damage);
    }

    /// <summary>
    /// Enregistre un kill.
    /// </summary>
    public void RecordKill()
    {
        if (_stats == null) _stats = new DefenseStats();
        _stats.totalKills++;
        OnEnemyKilled?.Invoke(_stats.totalKills);
    }

    /// <summary>
    /// Calcule le DPS total.
    /// </summary>
    public float GetTotalDPS()
    {
        float total = 0f;

        if (_towers != null)
        {
            foreach (var tower in _towers)
            {
                if (tower != null && tower.IsActive)
                {
                    total += tower.Damage * tower.FireRate;
                }
            }
        }

        return total;
    }

    /// <summary>
    /// Reinitialise les statistiques.
    /// </summary>
    public void ResetStats()
    {
        _stats = new DefenseStats();
    }

    #endregion

    #region Public Methods - Commandes

    /// <summary>
    /// Active/desactive toutes les tours.
    /// </summary>
    public void SetAllTowersActive(bool active)
    {
        if (_towers == null) return;

        foreach (var tower in _towers)
        {
            if (tower != null)
            {
                tower.SetActive(active);
            }
        }
    }

    /// <summary>
    /// Force toutes les tours a rechoisir leur cible.
    /// </summary>
    public void ForceRetargetAll()
    {
        if (_towers == null) return;

        foreach (var tower in _towers)
        {
            if (tower != null)
            {
                tower.ForceRetarget();
            }
        }
    }

    #endregion

    #region Private Methods

    private void UpdateDefenses()
    {
        if (_towers == null) return;

        // Nettoyer les tours detruites
        _towers.RemoveAll(t => t == null);

        // Mettre a jour les statistiques
        _stats.activeTowers = 0;
        foreach (var tower in _towers)
        {
            if (tower != null && tower.IsActive)
            {
                _stats.activeTowers++;
            }
        }
    }

    #endregion
}

/// <summary>
/// Statistiques de defense.
/// </summary>
[System.Serializable]
public class DefenseStats
{
    public float totalDamageDealt;
    public int totalKills;
    public int activeTowers;
    public int wavesDefended;
}
