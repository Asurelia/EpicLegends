using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Systeme de controle de territoire.
/// Gere les zones captables, leur etat et les bonus associes.
/// </summary>
public class TerritoryControl : MonoBehaviour
{
    #region Singleton

    private static TerritoryControl _instance;
    public static TerritoryControl Instance
    {
        get => _instance;
        private set => _instance = value;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _territories = new List<Territory>();
        _captureProgress = new Dictionary<string, float>();

        if (Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    #endregion

    #region Events

    /// <summary>Declenche quand un territoire change de proprietaire.</summary>
    public event Action<Territory, TerritoryOwner, TerritoryOwner> OnTerritoryOwnerChanged;

    /// <summary>Declenche quand la capture d'un territoire progresse.</summary>
    public event Action<Territory, float> OnCaptureProgress;

    /// <summary>Declenche quand un territoire est capture.</summary>
    public event Action<Territory, TerritoryOwner> OnTerritoryCaptured;

    /// <summary>Declenche quand un territoire est conteste.</summary>
    public event Action<Territory> OnTerritoryContested;

    #endregion

    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private float _captureSpeed = 10f;
    [SerializeField] private float _contestedDecaySpeed = 5f;
    [SerializeField] private float _captureRadius = 15f;
    [SerializeField] private float _updateInterval = 0.2f;

    [Header("Bonuses")]
    [SerializeField] private float _resourceBonusPerTerritory = 0.1f;
    [SerializeField] private float _xpBonusPerTerritory = 0.05f;
    [SerializeField] private float _defenseBonus = 0.15f;

    #endregion

    #region Private Fields

    private List<Territory> _territories;
    private Dictionary<string, float> _captureProgress;
    private float _updateTimer;

    #endregion

    #region Properties

    /// <summary>Nombre total de territoires.</summary>
    public int TotalTerritories => _territories?.Count ?? 0;

    /// <summary>Nombre de territoires controles par le joueur.</summary>
    public int PlayerTerritories => CountTerritoriesOwnedBy(TerritoryOwner.Player);

    /// <summary>Nombre de territoires controles par les ennemis.</summary>
    public int EnemyTerritories => CountTerritoriesOwnedBy(TerritoryOwner.Enemy);

    /// <summary>Nombre de territoires neutres.</summary>
    public int NeutralTerritories => CountTerritoriesOwnedBy(TerritoryOwner.Neutral);

    /// <summary>Pourcentage de controle du joueur.</summary>
    public float PlayerControlPercent => TotalTerritories > 0 ? (float)PlayerTerritories / TotalTerritories : 0f;

    /// <summary>Bonus de ressources actuel.</summary>
    public float ResourceBonus => PlayerTerritories * _resourceBonusPerTerritory;

    /// <summary>Bonus d'XP actuel.</summary>
    public float XPBonus => PlayerTerritories * _xpBonusPerTerritory;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        _updateTimer += Time.deltaTime;
        if (_updateTimer >= _updateInterval)
        {
            _updateTimer = 0f;
            UpdateAllTerritories();
        }
    }

    #endregion

    #region Public Methods - Registration

    /// <summary>
    /// Enregistre un territoire.
    /// </summary>
    public void RegisterTerritory(Territory territory)
    {
        if (territory == null) return;
        if (_territories == null) _territories = new List<Territory>();
        if (_territories.Contains(territory)) return;

        _territories.Add(territory);

        if (!string.IsNullOrEmpty(territory.TerritoryId))
        {
            if (_captureProgress == null) _captureProgress = new Dictionary<string, float>();
            if (!_captureProgress.ContainsKey(territory.TerritoryId))
            {
                _captureProgress[territory.TerritoryId] = GetInitialProgress(territory.Owner);
            }
        }
    }

    /// <summary>
    /// Retire un territoire.
    /// </summary>
    public void UnregisterTerritory(Territory territory)
    {
        if (territory == null || _territories == null) return;
        _territories.Remove(territory);
    }

    #endregion

    #region Public Methods - Queries

    /// <summary>
    /// Obtient tous les territoires.
    /// </summary>
    public List<Territory> GetAllTerritories()
    {
        return _territories != null ? new List<Territory>(_territories) : new List<Territory>();
    }

    /// <summary>
    /// Obtient les territoires d'un proprietaire.
    /// </summary>
    public List<Territory> GetTerritoriesOwnedBy(TerritoryOwner owner)
    {
        var result = new List<Territory>();
        if (_territories == null) return result;

        foreach (var territory in _territories)
        {
            if (territory != null && territory.Owner == owner)
            {
                result.Add(territory);
            }
        }
        return result;
    }

    /// <summary>
    /// Obtient le territoire le plus proche.
    /// </summary>
    public Territory GetNearestTerritory(Vector3 position, TerritoryOwner? ownerFilter = null)
    {
        if (_territories == null) return null;

        Territory nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var territory in _territories)
        {
            if (territory == null) continue;
            if (ownerFilter.HasValue && territory.Owner != ownerFilter.Value) continue;

            float dist = Vector3.Distance(position, territory.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = territory;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Verifie si un point est dans un territoire du joueur.
    /// </summary>
    public bool IsInPlayerTerritory(Vector3 point)
    {
        return GetTerritoryAt(point, TerritoryOwner.Player) != null;
    }

    /// <summary>
    /// Obtient le territoire a une position.
    /// </summary>
    public Territory GetTerritoryAt(Vector3 point, TerritoryOwner? ownerFilter = null)
    {
        if (_territories == null) return null;

        foreach (var territory in _territories)
        {
            if (territory == null) continue;
            if (ownerFilter.HasValue && territory.Owner != ownerFilter.Value) continue;

            if (territory.ContainsPoint(point))
            {
                return territory;
            }
        }
        return null;
    }

    /// <summary>
    /// Compte les territoires d'un proprietaire.
    /// </summary>
    public int CountTerritoriesOwnedBy(TerritoryOwner owner)
    {
        if (_territories == null) return 0;

        int count = 0;
        foreach (var territory in _territories)
        {
            if (territory != null && territory.Owner == owner)
            {
                count++;
            }
        }
        return count;
    }

    #endregion

    #region Public Methods - Capture

    /// <summary>
    /// Demarre la capture d'un territoire.
    /// </summary>
    public void StartCapture(Territory territory, TerritoryOwner capturer)
    {
        if (territory == null) return;
        if (territory.Owner == capturer) return;
        if (!territory.IsCaptureable) return;

        territory.SetContested(true, capturer);
        OnTerritoryContested?.Invoke(territory);
    }

    /// <summary>
    /// Force le changement de proprietaire d'un territoire.
    /// </summary>
    public void ForceCapture(Territory territory, TerritoryOwner newOwner)
    {
        if (territory == null) return;

        var oldOwner = territory.Owner;
        territory.SetOwner(newOwner);

        if (_captureProgress != null && !string.IsNullOrEmpty(territory.TerritoryId))
        {
            _captureProgress[territory.TerritoryId] = GetInitialProgress(newOwner);
        }

        territory.SetContested(false, TerritoryOwner.Neutral);
        OnTerritoryOwnerChanged?.Invoke(territory, oldOwner, newOwner);
        OnTerritoryCaptured?.Invoke(territory, newOwner);

        // Achievement integration
        if (newOwner == TerritoryOwner.Player && AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnTerritoryCaptured();
        }
    }

    /// <summary>
    /// Obtient la progression de capture d'un territoire.
    /// </summary>
    public float GetCaptureProgress(string territoryId)
    {
        if (string.IsNullOrEmpty(territoryId)) return 0f;
        if (_captureProgress == null) return 0f;

        return _captureProgress.TryGetValue(territoryId, out float progress) ? progress : 0f;
    }

    #endregion

    #region Public Methods - Bonuses

    /// <summary>
    /// Applique le bonus de ressources.
    /// </summary>
    public int ApplyResourceBonus(int baseAmount)
    {
        return Mathf.RoundToInt(baseAmount * (1f + ResourceBonus));
    }

    /// <summary>
    /// Applique le bonus d'XP.
    /// </summary>
    public int ApplyXPBonus(int baseXP)
    {
        return Mathf.RoundToInt(baseXP * (1f + XPBonus));
    }

    /// <summary>
    /// Obtient le bonus de defense dans un territoire du joueur.
    /// </summary>
    public float GetDefenseBonus(Vector3 position)
    {
        if (IsInPlayerTerritory(position))
        {
            return _defenseBonus;
        }
        return 0f;
    }

    #endregion

    #region Public Methods - Save/Load

    /// <summary>
    /// Obtient les donnees de sauvegarde.
    /// </summary>
    public TerritorySaveData GetSaveData()
    {
        var data = new TerritorySaveData();

        if (_territories != null)
        {
            foreach (var territory in _territories)
            {
                if (territory == null) continue;
                if (string.IsNullOrEmpty(territory.TerritoryId)) continue;

                data.territoryStates.Add(new TerritoryStateData
                {
                    territoryId = territory.TerritoryId,
                    owner = territory.Owner,
                    captureProgress = GetCaptureProgress(territory.TerritoryId),
                    isContested = territory.IsContested
                });
            }
        }

        return data;
    }

    /// <summary>
    /// Charge les donnees de sauvegarde.
    /// </summary>
    public void LoadSaveData(TerritorySaveData data)
    {
        if (data == null) return;

        if (_captureProgress == null) _captureProgress = new Dictionary<string, float>();

        foreach (var state in data.territoryStates)
        {
            if (string.IsNullOrEmpty(state.territoryId)) continue;

            _captureProgress[state.territoryId] = state.captureProgress;

            // Trouver et mettre a jour le territoire
            var territory = FindTerritoryById(state.territoryId);
            if (territory != null)
            {
                territory.SetOwner(state.owner);
                if (state.isContested)
                {
                    territory.SetContested(true, TerritoryOwner.Neutral);
                }
            }
        }
    }

    #endregion

    #region Private Methods

    private void UpdateAllTerritories()
    {
        if (_territories == null) return;

        var player = GameManager.Instance?.Player;
        Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
        bool playerExists = player != null;

        foreach (var territory in _territories)
        {
            if (territory == null) continue;
            if (!territory.IsCaptureable) continue;

            UpdateTerritoryCapture(territory, playerPos, playerExists);
        }

        // Nettoyer les territoires detruits
        _territories.RemoveAll(t => t == null);
    }

    private void UpdateTerritoryCapture(Territory territory, Vector3 playerPos, bool playerExists)
    {
        string id = territory.TerritoryId;
        if (string.IsNullOrEmpty(id)) return;

        if (!_captureProgress.ContainsKey(id))
        {
            _captureProgress[id] = GetInitialProgress(territory.Owner);
        }

        bool playerInRange = playerExists && territory.ContainsPoint(playerPos);
        bool enemiesInRange = territory.HasEnemiesInRange();

        float progress = _captureProgress[id];
        float oldProgress = progress;

        // Logique de capture
        if (playerInRange && !enemiesInRange)
        {
            // Joueur seul - capture vers joueur
            if (territory.Owner != TerritoryOwner.Player)
            {
                progress += _captureSpeed * _updateInterval;
                territory.SetContested(true, TerritoryOwner.Player);
            }
        }
        else if (enemiesInRange && !playerInRange)
        {
            // Ennemis seuls - capture vers ennemi
            if (territory.Owner != TerritoryOwner.Enemy)
            {
                progress -= _captureSpeed * _updateInterval;
                territory.SetContested(true, TerritoryOwner.Enemy);
            }
        }
        else if (playerInRange && enemiesInRange)
        {
            // Conteste - decroissance lente
            if (territory.Owner == TerritoryOwner.Player)
            {
                progress -= _contestedDecaySpeed * _updateInterval;
            }
            else if (territory.Owner == TerritoryOwner.Enemy)
            {
                progress += _contestedDecaySpeed * _updateInterval;
            }
            territory.SetContested(true, TerritoryOwner.Neutral);
        }
        else
        {
            // Personne - stabilisation vers le proprietaire
            territory.SetContested(false, TerritoryOwner.Neutral);
            float target = GetInitialProgress(territory.Owner);
            progress = Mathf.MoveTowards(progress, target, _contestedDecaySpeed * _updateInterval * 0.5f);
        }

        // Clamp et appliquer
        progress = Mathf.Clamp(progress, -100f, 100f);
        _captureProgress[id] = progress;

        // Notifier progression
        if (Mathf.Abs(progress - oldProgress) > 0.01f)
        {
            OnCaptureProgress?.Invoke(territory, progress);
        }

        // Verifier changement de proprietaire
        CheckOwnershipChange(territory, progress);
    }

    private void CheckOwnershipChange(Territory territory, float progress)
    {
        TerritoryOwner newOwner = territory.Owner;

        if (progress >= 100f && territory.Owner != TerritoryOwner.Player)
        {
            newOwner = TerritoryOwner.Player;
        }
        else if (progress <= -100f && territory.Owner != TerritoryOwner.Enemy)
        {
            newOwner = TerritoryOwner.Enemy;
        }
        else if (progress > -50f && progress < 50f && territory.Owner != TerritoryOwner.Neutral)
        {
            // Zone devient neutre si la progression se stabilise autour de 0
            if (!territory.IsContested && Mathf.Abs(progress) < 10f)
            {
                newOwner = TerritoryOwner.Neutral;
            }
        }

        if (newOwner != territory.Owner)
        {
            var oldOwner = territory.Owner;
            territory.SetOwner(newOwner);
            _captureProgress[territory.TerritoryId] = GetInitialProgress(newOwner);
            territory.SetContested(false, TerritoryOwner.Neutral);

            OnTerritoryOwnerChanged?.Invoke(territory, oldOwner, newOwner);
            OnTerritoryCaptured?.Invoke(territory, newOwner);

            // Achievement
            if (newOwner == TerritoryOwner.Player && AchievementManager.Instance != null)
            {
                AchievementManager.Instance.OnTerritoryCaptured();
            }

            Debug.Log($"[TerritoryControl] {territory.TerritoryId} captured by {newOwner}");
        }
    }

    private float GetInitialProgress(TerritoryOwner owner)
    {
        switch (owner)
        {
            case TerritoryOwner.Player: return 100f;
            case TerritoryOwner.Enemy: return -100f;
            default: return 0f;
        }
    }

    private Territory FindTerritoryById(string id)
    {
        if (_territories == null || string.IsNullOrEmpty(id)) return null;

        foreach (var territory in _territories)
        {
            if (territory != null && territory.TerritoryId == id)
            {
                return territory;
            }
        }
        return null;
    }

    #endregion
}

// TerritoryOwner enum is defined in SaveData.cs
