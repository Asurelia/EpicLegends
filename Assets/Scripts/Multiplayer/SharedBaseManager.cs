using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire de base partagee.
/// Gere les permissions, ressources partagees et defense co-op.
/// </summary>
public class SharedBaseManager : MonoBehaviour
{
    #region Singleton

    private static SharedBaseManager _instance;
    public static SharedBaseManager Instance
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

        _playerPermissions = new Dictionary<string, BuildPermission>();
        _sharedResources = new Dictionary<ResourceType, int>();
        _lentCreatures = new List<CreatureLendData>();
        _activeDefenseWaves = new List<ActiveDefenseWave>();
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
    [SerializeField] private BuildPermission _defaultPermission = BuildPermission.View;
    [SerializeField] private int _maxLentCreatures = 5;
    [SerializeField] private float _resourceShareRange = 100f;

    [Header("Defense")]
    [SerializeField] private float _waveInterval = 300f;
    [SerializeField] private int _baseWaveSize = 10;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;

    // Permissions des joueurs
    private Dictionary<string, BuildPermission> _playerPermissions;

    // Ressources partagees
    private Dictionary<ResourceType, int> _sharedResources;

    // Creatures pretees
    private List<CreatureLendData> _lentCreatures;

    // Vagues de defense actives
    private List<ActiveDefenseWave> _activeDefenseWaves;

    // ID du proprietaire de la base
    private string _baseOwnerId;

    #endregion

    #region Events

    /// <summary>Declenche lors d'un changement de permission.</summary>
    public event Action<string, BuildPermission> OnPermissionChanged;

    /// <summary>Declenche lors d'un depot de ressources.</summary>
    public event Action<string, ResourceType, int> OnResourceDeposited;

    /// <summary>Declenche lors d'un retrait de ressources.</summary>
    public event Action<string, ResourceType, int> OnResourceWithdrawn;

    /// <summary>Declenche lors du pret d'une creature.</summary>
    public event Action<CreatureLendData> OnCreatureLent;

    /// <summary>Declenche lors du retour d'une creature.</summary>
    public event Action<CreatureLendData> OnCreatureReturned;

    /// <summary>Declenche lors du debut d'une vague de defense.</summary>
    public event Action<CoopDefenseWave> OnDefenseWaveStarted;

    /// <summary>Declenche lors de la fin d'une vague de defense.</summary>
    public event Action<CoopDefenseWave, bool> OnDefenseWaveEnded;

    #endregion

    #region Properties

    /// <summary>ID du proprietaire de la base.</summary>
    public string BaseOwnerId => _baseOwnerId;

    /// <summary>Est proprietaire?</summary>
    public bool IsBaseOwner => _baseOwnerId == NetworkGameManager.Instance?.LocalPlayerId;

    /// <summary>Nombre de creatures pretees.</summary>
    public int LentCreatureCount => _lentCreatures?.Count ?? 0;

    /// <summary>Vague de defense active?</summary>
    public bool IsDefenseWaveActive => _activeDefenseWaves?.Count > 0;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        UpdateLentCreatures();
        UpdateDefenseWaves();
    }

    #endregion

    #region Public Methods - Permissions

    /// <summary>
    /// Definit le proprietaire de la base.
    /// </summary>
    /// <param name="playerId">ID du joueur.</param>
    public void SetBaseOwner(string playerId)
    {
        _baseOwnerId = playerId;

        // Le proprietaire a toujours les droits Admin
        if (_playerPermissions == null) _playerPermissions = new Dictionary<string, BuildPermission>();
        _playerPermissions[playerId] = BuildPermission.Admin;

        Log($"Base owner set: {playerId}");
    }

    /// <summary>
    /// Definit la permission d'un joueur.
    /// </summary>
    /// <param name="playerId">ID du joueur.</param>
    /// <param name="permission">Niveau de permission.</param>
    public void SetPlayerPermission(string playerId, BuildPermission permission)
    {
        if (!IsBaseOwner && !HasPermission(BuildPermission.Admin))
        {
            LogError("Not authorized to change permissions");
            return;
        }

        if (playerId == _baseOwnerId && permission != BuildPermission.Admin)
        {
            LogError("Cannot demote base owner");
            return;
        }

        if (_playerPermissions == null) _playerPermissions = new Dictionary<string, BuildPermission>();
        _playerPermissions[playerId] = permission;

        OnPermissionChanged?.Invoke(playerId, permission);
        Log($"Permission changed: {playerId} -> {permission}");
    }

    /// <summary>
    /// Obtient la permission d'un joueur.
    /// </summary>
    /// <param name="playerId">ID du joueur.</param>
    /// <returns>Niveau de permission.</returns>
    public BuildPermission GetPlayerPermission(string playerId)
    {
        if (playerId == _baseOwnerId) return BuildPermission.Admin;

        if (_playerPermissions == null) return _defaultPermission;
        return _playerPermissions.TryGetValue(playerId, out var perm) ? perm : _defaultPermission;
    }

    /// <summary>
    /// Verifie si le joueur local a une permission.
    /// </summary>
    /// <param name="required">Permission requise.</param>
    /// <returns>True si autorise.</returns>
    public bool HasPermission(BuildPermission required)
    {
        var localId = NetworkGameManager.Instance?.LocalPlayerId;
        if (string.IsNullOrEmpty(localId)) return false;

        var current = GetPlayerPermission(localId);
        return (int)current >= (int)required;
    }

    /// <summary>
    /// Verifie si une action est autorisee.
    /// </summary>
    /// <param name="action">Type d'action.</param>
    /// <returns>True si autorise.</returns>
    public bool CanPerformAction(BaseAction action)
    {
        BuildPermission required = action switch
        {
            BaseAction.View => BuildPermission.View,
            BaseAction.UseStorage => BuildPermission.Interact,
            BaseAction.UseMachines => BuildPermission.Interact,
            BaseAction.PlaceBuildings => BuildPermission.Build,
            BaseAction.RemoveBuildings => BuildPermission.Build,
            BaseAction.ManagePermissions => BuildPermission.Admin,
            BaseAction.ManageResources => BuildPermission.Admin,
            _ => BuildPermission.None
        };

        return HasPermission(required);
    }

    #endregion

    #region Public Methods - Shared Resources

    /// <summary>
    /// Depose des ressources dans le stockage partage.
    /// </summary>
    /// <param name="resourceType">Type de ressource.</param>
    /// <param name="amount">Quantite.</param>
    /// <returns>True si depose.</returns>
    public bool DepositResource(ResourceType resourceType, int amount)
    {
        if (amount <= 0) return false;
        if (!HasPermission(BuildPermission.Interact))
        {
            LogError("Not authorized to deposit resources");
            return false;
        }

        if (_sharedResources == null) _sharedResources = new Dictionary<ResourceType, int>();

        if (!_sharedResources.ContainsKey(resourceType))
        {
            _sharedResources[resourceType] = 0;
        }

        _sharedResources[resourceType] += amount;

        var localId = NetworkGameManager.Instance?.LocalPlayerId;
        OnResourceDeposited?.Invoke(localId, resourceType, amount);

        Log($"Deposited: {amount} {resourceType}");
        return true;
    }

    /// <summary>
    /// Retire des ressources du stockage partage.
    /// </summary>
    /// <param name="resourceType">Type de ressource.</param>
    /// <param name="amount">Quantite.</param>
    /// <returns>Quantite reellement retiree.</returns>
    public int WithdrawResource(ResourceType resourceType, int amount)
    {
        if (amount <= 0) return 0;
        if (!HasPermission(BuildPermission.Interact))
        {
            LogError("Not authorized to withdraw resources");
            return 0;
        }

        if (_sharedResources == null) return 0;

        if (!_sharedResources.TryGetValue(resourceType, out int current))
        {
            return 0;
        }

        int withdrawn = Mathf.Min(amount, current);
        _sharedResources[resourceType] -= withdrawn;

        var localId = NetworkGameManager.Instance?.LocalPlayerId;
        OnResourceWithdrawn?.Invoke(localId, resourceType, withdrawn);

        Log($"Withdrawn: {withdrawn} {resourceType}");
        return withdrawn;
    }

    /// <summary>
    /// Obtient la quantite d'une ressource partagee.
    /// </summary>
    /// <param name="resourceType">Type de ressource.</param>
    /// <returns>Quantite.</returns>
    public int GetSharedResourceAmount(ResourceType resourceType)
    {
        if (_sharedResources == null) return 0;
        return _sharedResources.TryGetValue(resourceType, out int amount) ? amount : 0;
    }

    /// <summary>
    /// Obtient toutes les ressources partagees.
    /// </summary>
    /// <returns>Dictionnaire des ressources.</returns>
    public Dictionary<ResourceType, int> GetAllSharedResources()
    {
        return _sharedResources ?? new Dictionary<ResourceType, int>();
    }

    #endregion

    #region Public Methods - Creature Lending

    /// <summary>
    /// Prete une creature a un autre joueur.
    /// </summary>
    /// <param name="creatureId">ID de la creature.</param>
    /// <param name="borrowerId">ID de l'emprunteur.</param>
    /// <param name="durationHours">Duree en heures.</param>
    /// <returns>Donnees du pret ou null.</returns>
    public CreatureLendData LendCreature(string creatureId, string borrowerId, int durationHours)
    {
        if (string.IsNullOrEmpty(creatureId) || string.IsNullOrEmpty(borrowerId))
        {
            return null;
        }

        var localId = NetworkGameManager.Instance?.LocalPlayerId;
        if (string.IsNullOrEmpty(localId)) return null;

        if (_lentCreatures == null) _lentCreatures = new List<CreatureLendData>();

        if (_lentCreatures.Count >= _maxLentCreatures)
        {
            LogError("Maximum lent creatures reached");
            return null;
        }

        // Verifier que la creature n'est pas deja pretee
        if (_lentCreatures.Exists(l => l.creatureId == creatureId))
        {
            LogError("Creature already lent");
            return null;
        }

        var lend = new CreatureLendData
        {
            lendId = GenerateLendId(),
            ownerId = localId,
            borrowerId = borrowerId,
            creatureId = creatureId,
            durationHours = durationHours,
            startedAt = DateTime.UtcNow,
            expiresAt = DateTime.UtcNow.AddHours(durationHours),
            isActive = true
        };

        _lentCreatures.Add(lend);
        OnCreatureLent?.Invoke(lend);

        Log($"Creature lent: {creatureId} to {borrowerId} for {durationHours}h");
        return lend;
    }

    /// <summary>
    /// Rappelle une creature pretee.
    /// </summary>
    /// <param name="lendId">ID du pret.</param>
    /// <returns>True si rappele.</returns>
    public bool RecallCreature(string lendId)
    {
        if (_lentCreatures == null) return false;

        var lend = _lentCreatures.Find(l => l.lendId == lendId);
        if (lend == null) return false;

        var localId = NetworkGameManager.Instance?.LocalPlayerId;
        if (lend.ownerId != localId)
        {
            LogError("Not the owner of this creature");
            return false;
        }

        lend.isActive = false;
        _lentCreatures.Remove(lend);
        OnCreatureReturned?.Invoke(lend);

        Log($"Creature recalled: {lend.creatureId}");
        return true;
    }

    /// <summary>
    /// Obtient les creatures pretees par le joueur local.
    /// </summary>
    /// <returns>Liste des prets.</returns>
    public List<CreatureLendData> GetLentCreatures()
    {
        var localId = NetworkGameManager.Instance?.LocalPlayerId;
        if (_lentCreatures == null) return new List<CreatureLendData>();

        return _lentCreatures.FindAll(l => l.ownerId == localId && l.isActive);
    }

    /// <summary>
    /// Obtient les creatures empruntees par le joueur local.
    /// </summary>
    /// <returns>Liste des emprunts.</returns>
    public List<CreatureLendData> GetBorrowedCreatures()
    {
        var localId = NetworkGameManager.Instance?.LocalPlayerId;
        if (_lentCreatures == null) return new List<CreatureLendData>();

        return _lentCreatures.FindAll(l => l.borrowerId == localId && l.isActive);
    }

    #endregion

    #region Public Methods - Co-op Defense

    /// <summary>
    /// Demarre une vague de defense.
    /// </summary>
    /// <param name="waveNumber">Numero de vague.</param>
    /// <returns>Vague demarree ou null.</returns>
    public CoopDefenseWave StartDefenseWave(int waveNumber = 1)
    {
        if (!IsBaseOwner && !HasPermission(BuildPermission.Admin))
        {
            LogError("Not authorized to start defense wave");
            return null;
        }

        int playerCount = PartyManager.Instance?.MemberCount ?? 1;

        var wave = new CoopDefenseWave
        {
            waveNumber = waveNumber,
            playerScaling = true,
            baseEnemyCount = _baseWaveSize + (waveNumber * 5),
            enemyCountPerPlayer = 3 + waveNumber,
            difficultyMultiplier = 1f + (waveNumber * 0.2f),
            bonusXP = 100 * waveNumber,
            bonusResources = 50 * waveNumber
        };

        var activeWave = new ActiveDefenseWave
        {
            wave = wave,
            playerCount = playerCount,
            startedAt = DateTime.UtcNow,
            remainingEnemies = wave.GetTotalEnemies(playerCount),
            totalEnemies = wave.GetTotalEnemies(playerCount)
        };

        if (_activeDefenseWaves == null) _activeDefenseWaves = new List<ActiveDefenseWave>();
        _activeDefenseWaves.Add(activeWave);

        OnDefenseWaveStarted?.Invoke(wave);
        Log($"Defense wave {waveNumber} started with {activeWave.totalEnemies} enemies");

        return wave;
    }

    /// <summary>
    /// Signale la mort d'un ennemi de defense.
    /// </summary>
    /// <param name="waveNumber">Numero de vague.</param>
    public void OnDefenseEnemyKilled(int waveNumber)
    {
        if (_activeDefenseWaves == null) return;

        var activeWave = _activeDefenseWaves.Find(w => w.wave.waveNumber == waveNumber);
        if (activeWave == null) return;

        activeWave.remainingEnemies--;

        if (activeWave.remainingEnemies <= 0)
        {
            _activeDefenseWaves.Remove(activeWave);
            OnDefenseWaveEnded?.Invoke(activeWave.wave, true);
            Log($"Defense wave {waveNumber} completed!");

            // Distribuer les recompenses
            DistributeDefenseRewards(activeWave);
        }
    }

    /// <summary>
    /// Obtient la vague de defense active.
    /// </summary>
    /// <returns>Vague active ou null.</returns>
    public ActiveDefenseWave GetActiveDefenseWave()
    {
        if (_activeDefenseWaves == null || _activeDefenseWaves.Count == 0) return null;
        return _activeDefenseWaves[0];
    }

    #endregion

    #region Private Methods

    private void UpdateLentCreatures()
    {
        if (_lentCreatures == null || _lentCreatures.Count == 0) return;

        var expired = _lentCreatures.FindAll(l => l.IsExpired && l.isActive);
        foreach (var lend in expired)
        {
            lend.isActive = false;
            OnCreatureReturned?.Invoke(lend);
            Log($"Creature loan expired: {lend.creatureId}");
        }

        _lentCreatures.RemoveAll(l => !l.isActive);
    }

    private void UpdateDefenseWaves()
    {
        // TODO: Verifier le timeout des vagues
    }

    private void DistributeDefenseRewards(ActiveDefenseWave activeWave)
    {
        if (activeWave == null) return;

        var members = PartyManager.Instance?.GetAllMembers();
        if (members == null || members.Count == 0) return;

        int xpPerPlayer = activeWave.wave.bonusXP / members.Count;
        int resourcesPerPlayer = activeWave.wave.bonusResources / members.Count;

        foreach (var member in members)
        {
            // TODO: Distribuer XP et ressources
            Log($"Defense rewards for {member.playerId}: {xpPerPlayer} XP, {resourcesPerPlayer} resources");
        }
    }

    private string GenerateLendId()
    {
        return $"LND{DateTime.UtcNow.Ticks:X}-{UnityEngine.Random.Range(1000, 9999)}";
    }

    private void Log(string message)
    {
        if (_debugMode)
        {
            Debug.Log($"[SharedBase] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[SharedBase] {message}");
    }

    #endregion
}

/// <summary>
/// Niveaux de permission pour la base.
/// </summary>
public enum BuildPermission
{
    /// <summary>Aucun acces.</summary>
    None = 0,

    /// <summary>Peut voir la base.</summary>
    View = 1,

    /// <summary>Peut interagir (stockage, machines).</summary>
    Interact = 2,

    /// <summary>Peut construire.</summary>
    Build = 3,

    /// <summary>Administrateur complet.</summary>
    Admin = 4
}

/// <summary>
/// Actions possibles sur la base.
/// </summary>
public enum BaseAction
{
    View,
    UseStorage,
    UseMachines,
    PlaceBuildings,
    RemoveBuildings,
    ManagePermissions,
    ManageResources
}

/// <summary>
/// Donnees de pret de creature.
/// </summary>
[System.Serializable]
public class CreatureLendData
{
    public string lendId;
    public string ownerId;
    public string borrowerId;
    public string creatureId;
    public string creatureName;
    public int durationHours;
    public DateTime startedAt;
    public DateTime expiresAt;
    public bool isActive;

    /// <summary>Pret expire?</summary>
    public bool IsExpired => DateTime.UtcNow >= expiresAt;

    /// <summary>Temps restant en heures.</summary>
    public float RemainingHours => (float)(expiresAt - DateTime.UtcNow).TotalHours;
}

/// <summary>
/// Vague de defense co-op.
/// </summary>
[System.Serializable]
public class CoopDefenseWave
{
    public int waveNumber;
    public bool playerScaling;
    public int baseEnemyCount;
    public int enemyCountPerPlayer;
    public float difficultyMultiplier;
    public int bonusXP;
    public int bonusResources;
    public GameObject[] enemyPrefabs;
    public float spawnInterval;

    /// <summary>
    /// Calcule le nombre total d'ennemis.
    /// </summary>
    /// <param name="playerCount">Nombre de joueurs.</param>
    /// <returns>Nombre d'ennemis.</returns>
    public int GetTotalEnemies(int playerCount)
    {
        if (!playerScaling) return baseEnemyCount;
        return baseEnemyCount + (enemyCountPerPlayer * playerCount);
    }
}

/// <summary>
/// Vague de defense active.
/// </summary>
[System.Serializable]
public class ActiveDefenseWave
{
    public CoopDefenseWave wave;
    public int playerCount;
    public DateTime startedAt;
    public int remainingEnemies;
    public int totalEnemies;

    /// <summary>Progression (0-1).</summary>
    public float Progress => 1f - ((float)remainingEnemies / totalEnemies);
}
