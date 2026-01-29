using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire des ameliorations de batiments.
/// Gere les couts, les conditions et les animations d'upgrade.
/// </summary>
public class BuildingUpgradeManager : MonoBehaviour
{
    #region Singleton

    public static BuildingUpgradeManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    #endregion

    #region Fields

    [Header("Configuration")]
    [SerializeField] private float _upgradeTimeMultiplier = 1f;
    [SerializeField] private bool _instantUpgrade = false;

    [Header("Effets")]
    [SerializeField] private GameObject _upgradeVFXPrefab;
    [SerializeField] private AudioClip _upgradeStartSound;
    [SerializeField] private AudioClip _upgradeCompleteSound;

    // Upgrades en cours
    private Dictionary<Building, UpgradeProgress> _activeUpgrades = new Dictionary<Building, UpgradeProgress>();

    #endregion

    #region Events

    public event Action<Building, BuildingTier> OnUpgradeStarted;
    public event Action<Building, BuildingTier> OnUpgradeCompleted;
    public event Action<Building, float> OnUpgradeProgress;
    public event Action<Building> OnUpgradeCancelled;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        UpdateActiveUpgrades();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Verifie si un batiment peut etre ameliore.
    /// </summary>
    public bool CanUpgrade(Building building, BuildingTier targetTier)
    {
        if (building == null) return false;
        if (!building.CanUpgrade(targetTier)) return false;
        if (_activeUpgrades.ContainsKey(building)) return false;

        return true;
    }

    /// <summary>
    /// Verifie si on a les ressources pour l'upgrade.
    /// </summary>
    public bool HasUpgradeResources(Building building, BuildingTier targetTier, IResourceContainer resources)
    {
        if (building == null || building.Data == null || resources == null) return false;

        var costs = building.Data.GetUpgradeCost(targetTier);

        foreach (var cost in costs)
        {
            if (!resources.HasResource(cost.resourceType, cost.amount))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Demarre une amelioration.
    /// </summary>
    public bool StartUpgrade(Building building, BuildingTier targetTier, IResourceContainer resources)
    {
        if (!CanUpgrade(building, targetTier)) return false;
        if (!HasUpgradeResources(building, targetTier, resources)) return false;

        // Consommer les ressources
        var costs = building.Data.GetUpgradeCost(targetTier);
        foreach (var cost in costs)
        {
            resources.RemoveResource(cost.resourceType, cost.amount);
        }

        // Calculer le temps d'upgrade
        float upgradeTime = CalculateUpgradeTime(building.Data, targetTier);

        if (_instantUpgrade || upgradeTime <= 0)
        {
            // Upgrade instantane
            CompleteUpgrade(building, targetTier);
        }
        else
        {
            // Creer la progression
            var progress = new UpgradeProgress
            {
                targetTier = targetTier,
                totalTime = upgradeTime,
                currentTime = 0f
            };

            _activeUpgrades[building] = progress;

            // Effets de debut
            PlayUpgradeStartEffects(building);
        }

        OnUpgradeStarted?.Invoke(building, targetTier);

        return true;
    }

    /// <summary>
    /// Annule une amelioration en cours.
    /// </summary>
    public bool CancelUpgrade(Building building, IResourceContainer resources, float refundRatio = 0.5f)
    {
        if (!_activeUpgrades.TryGetValue(building, out var progress))
            return false;

        // Rembourser partiellement les ressources
        if (resources != null)
        {
            var costs = building.Data.GetUpgradeCost(progress.targetTier);
            float progressRatio = 1f - (progress.currentTime / progress.totalTime);
            float finalRefund = refundRatio * progressRatio;

            foreach (var cost in costs)
            {
                int refundAmount = Mathf.FloorToInt(cost.amount * finalRefund);
                if (refundAmount > 0)
                {
                    resources.AddResource(cost.resourceType, refundAmount);
                }
            }
        }

        _activeUpgrades.Remove(building);
        OnUpgradeCancelled?.Invoke(building);

        return true;
    }

    /// <summary>
    /// Obtient la progression d'une amelioration.
    /// </summary>
    public float GetUpgradeProgress(Building building)
    {
        if (_activeUpgrades.TryGetValue(building, out var progress))
        {
            return progress.totalTime > 0 ? progress.currentTime / progress.totalTime : 0f;
        }
        return 0f;
    }

    /// <summary>
    /// Verifie si un batiment est en cours d'amelioration.
    /// </summary>
    public bool IsUpgrading(Building building)
    {
        return _activeUpgrades.ContainsKey(building);
    }

    /// <summary>
    /// Calcule le cout d'une amelioration.
    /// </summary>
    public ResourceCost[] GetUpgradeCost(Building building, BuildingTier targetTier)
    {
        if (building == null || building.Data == null)
            return new ResourceCost[0];

        return building.Data.GetUpgradeCost(targetTier);
    }

    /// <summary>
    /// Calcule le temps d'une amelioration.
    /// </summary>
    public float GetUpgradeTime(Building building, BuildingTier targetTier)
    {
        if (building == null || building.Data == null)
            return 0f;

        return CalculateUpgradeTime(building.Data, targetTier);
    }

    #endregion

    #region Private Methods

    private void UpdateActiveUpgrades()
    {
        var completed = new List<Building>();

        foreach (var kvp in _activeUpgrades)
        {
            var building = kvp.Key;
            var progress = kvp.Value;

            if (building == null)
            {
                completed.Add(building);
                continue;
            }

            progress.currentTime += Time.deltaTime;
            _activeUpgrades[building] = progress;

            float progressRatio = progress.currentTime / progress.totalTime;
            OnUpgradeProgress?.Invoke(building, progressRatio);

            if (progress.currentTime >= progress.totalTime)
            {
                CompleteUpgrade(building, progress.targetTier);
                completed.Add(building);
            }
        }

        foreach (var building in completed)
        {
            _activeUpgrades.Remove(building);
        }
    }

    private void CompleteUpgrade(Building building, BuildingTier targetTier)
    {
        if (building == null) return;

        building.Upgrade(targetTier);

        // Effets de completion
        PlayUpgradeCompleteEffects(building);

        OnUpgradeCompleted?.Invoke(building, targetTier);
    }

    private float CalculateUpgradeTime(BuildingData data, BuildingTier targetTier)
    {
        // Temps de base * multiplicateur de tier * multiplicateur global
        float baseTime = data.buildTime;

        float tierMultiplier = targetTier switch
        {
            BuildingTier.Stone => 1.5f,
            BuildingTier.Metal => 2.5f,
            BuildingTier.Tech => 4f,
            _ => 1f
        };

        return baseTime * tierMultiplier * _upgradeTimeMultiplier;
    }

    private void PlayUpgradeStartEffects(Building building)
    {
        if (_upgradeVFXPrefab != null)
        {
            Instantiate(_upgradeVFXPrefab, building.transform.position, Quaternion.identity);
        }

        if (_upgradeStartSound != null)
        {
            AudioSource.PlayClipAtPoint(_upgradeStartSound, building.transform.position);
        }
    }

    private void PlayUpgradeCompleteEffects(Building building)
    {
        if (_upgradeVFXPrefab != null)
        {
            Instantiate(_upgradeVFXPrefab, building.transform.position, Quaternion.identity);
        }

        if (_upgradeCompleteSound != null)
        {
            AudioSource.PlayClipAtPoint(_upgradeCompleteSound, building.transform.position);
        }
    }

    #endregion

    #region Structs

    private struct UpgradeProgress
    {
        public BuildingTier targetTier;
        public float totalTime;
        public float currentTime;
    }

    #endregion
}
