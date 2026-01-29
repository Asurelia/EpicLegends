using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire de raids.
/// Gere les instances de raid, progression et lockouts.
/// </summary>
public class RaidManager : MonoBehaviour
{
    #region Singleton

    private static RaidManager _instance;
    public static RaidManager Instance
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

        _lockouts = new Dictionary<string, RaidLockout>();
        _activeRaid = null;
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
    [SerializeField] private RaidData[] _availableRaids;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;

    // Raid actif
    private ActiveRaid _activeRaid;

    // Lockouts par raid
    private Dictionary<string, RaidLockout> _lockouts;

    #endregion

    #region Events

    /// <summary>Declenche au debut du raid.</summary>
    public event Action<RaidData, RaidDifficulty> OnRaidStarted;

    /// <summary>Declenche a la fin du raid.</summary>
    public event Action<RaidData, bool> OnRaidEnded;

    /// <summary>Declenche lors de la mort d'un boss.</summary>
    public event Action<RaidBossData> OnBossDefeated;

    /// <summary>Declenche lors d'un changement de phase.</summary>
    public event Action<RaidBossData, int> OnBossPhaseChanged;

    /// <summary>Declenche lors d'un wipe.</summary>
    public event Action<int> OnRaidWipe;

    /// <summary>Declenche lors du reset des lockouts.</summary>
    public event Action OnLockoutsReset;

    #endregion

    #region Properties

    /// <summary>Raid actif.</summary>
    public ActiveRaid CurrentRaid => _activeRaid;

    /// <summary>Est dans un raid?</summary>
    public bool IsInRaid => _activeRaid != null;

    /// <summary>Raids disponibles.</summary>
    public IReadOnlyList<RaidData> AvailableRaids => _availableRaids;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        if (_activeRaid != null)
        {
            UpdateActiveRaid();
        }

        CheckLockoutExpirations();
    }

    #endregion

    #region Public Methods - Raid Management

    /// <summary>
    /// Demarre un raid.
    /// </summary>
    /// <param name="raidData">Donnees du raid.</param>
    /// <param name="difficulty">Difficulte.</param>
    /// <returns>True si demarre.</returns>
    public bool StartRaid(RaidData raidData, RaidDifficulty difficulty = RaidDifficulty.Normal)
    {
        if (raidData == null)
        {
            LogError("Invalid raid data");
            return false;
        }

        if (_activeRaid != null)
        {
            LogError("Already in a raid");
            return false;
        }

        // Verifier le groupe
        var partySize = PartyManager.Instance?.MemberCount ?? 1;
        if (partySize < raidData.requiredPlayers)
        {
            LogError($"Not enough players ({partySize}/{raidData.requiredPlayers})");
            return false;
        }

        // Verifier le lockout
        if (IsLockedOut(raidData.raidId, difficulty))
        {
            LogError("Raid is locked out");
            return false;
        }

        _activeRaid = new ActiveRaid
        {
            raidData = raidData,
            difficulty = difficulty,
            startedAt = DateTime.UtcNow,
            currentBossIndex = 0,
            wipeCount = 0,
            completedBosses = new List<string>(),
            participants = new List<string>()
        };

        // Ajouter les participants
        var members = PartyManager.Instance?.GetAllMembers();
        if (members != null)
        {
            foreach (var member in members)
            {
                _activeRaid.participants.Add(member.playerId);
            }
        }

        OnRaidStarted?.Invoke(raidData, difficulty);
        Log($"Raid started: {raidData.raidName} ({difficulty})");

        return true;
    }

    /// <summary>
    /// Termine le raid.
    /// </summary>
    /// <param name="success">Complete avec succes?</param>
    public void EndRaid(bool success)
    {
        if (_activeRaid == null) return;

        if (success)
        {
            // Creer le lockout
            CreateLockout(_activeRaid);

            // Distribuer les recompenses
            DistributeRaidRewards(_activeRaid);
        }

        OnRaidEnded?.Invoke(_activeRaid.raidData, success);
        Log($"Raid ended: {(success ? "Success" : "Failed")}");

        _activeRaid = null;
    }

    /// <summary>
    /// Signale un wipe.
    /// </summary>
    public void ReportWipe()
    {
        if (_activeRaid == null) return;

        _activeRaid.wipeCount++;
        OnRaidWipe?.Invoke(_activeRaid.wipeCount);

        Log($"Raid wipe #{_activeRaid.wipeCount}");

        // Verifier la limite de wipes
        if (_activeRaid.raidData.wipeEndsRaid &&
            _activeRaid.wipeCount >= _activeRaid.raidData.maxWipes)
        {
            EndRaid(false);
        }
    }

    #endregion

    #region Public Methods - Boss Management

    /// <summary>
    /// Signale la defaite d'un boss.
    /// </summary>
    /// <param name="bossId">ID du boss.</param>
    public void ReportBossDefeated(string bossId)
    {
        if (_activeRaid == null) return;

        var boss = GetBossData(bossId);
        if (boss == null) return;

        if (!_activeRaid.completedBosses.Contains(bossId))
        {
            _activeRaid.completedBosses.Add(bossId);
            _activeRaid.currentBossIndex++;

            OnBossDefeated?.Invoke(boss);
            Log($"Boss defeated: {boss.bossName}");

            // Verifier si tous les boss sont vaincus
            if (_activeRaid.completedBosses.Count >= _activeRaid.raidData.bosses.Length)
            {
                EndRaid(true);
            }
        }
    }

    /// <summary>
    /// Signale un changement de phase.
    /// </summary>
    /// <param name="bossId">ID du boss.</param>
    /// <param name="phaseIndex">Index de la nouvelle phase.</param>
    public void ReportPhaseChange(string bossId, int phaseIndex)
    {
        if (_activeRaid == null) return;

        var boss = GetBossData(bossId);
        if (boss == null) return;

        _activeRaid.currentBossPhase = phaseIndex;
        OnBossPhaseChanged?.Invoke(boss, phaseIndex);

        Log($"Boss {boss.bossName} entered phase {phaseIndex + 1}");
    }

    /// <summary>
    /// Obtient les donnees d'un boss.
    /// </summary>
    /// <param name="bossId">ID du boss.</param>
    /// <returns>Donnees du boss ou null.</returns>
    public RaidBossData GetBossData(string bossId)
    {
        if (_activeRaid?.raidData?.bosses == null) return null;

        foreach (var boss in _activeRaid.raidData.bosses)
        {
            if (boss.bossId == bossId) return boss;
        }

        return null;
    }

    /// <summary>
    /// Obtient le boss actuel.
    /// </summary>
    /// <returns>Boss actuel ou null.</returns>
    public RaidBossData GetCurrentBoss()
    {
        if (_activeRaid?.raidData?.bosses == null) return null;
        if (_activeRaid.currentBossIndex >= _activeRaid.raidData.bosses.Length) return null;

        return _activeRaid.raidData.bosses[_activeRaid.currentBossIndex];
    }

    #endregion

    #region Public Methods - Lockouts

    /// <summary>
    /// Verifie si un raid est lockout.
    /// </summary>
    /// <param name="raidId">ID du raid.</param>
    /// <param name="difficulty">Difficulte.</param>
    /// <returns>True si lockout.</returns>
    public bool IsLockedOut(string raidId, RaidDifficulty difficulty)
    {
        string key = GetLockoutKey(raidId, difficulty);
        if (_lockouts == null) return false;

        if (_lockouts.TryGetValue(key, out var lockout))
        {
            return !lockout.IsExpired;
        }

        return false;
    }

    /// <summary>
    /// Obtient le lockout d'un raid.
    /// </summary>
    /// <param name="raidId">ID du raid.</param>
    /// <param name="difficulty">Difficulte.</param>
    /// <returns>Lockout ou null.</returns>
    public RaidLockout GetLockout(string raidId, RaidDifficulty difficulty)
    {
        string key = GetLockoutKey(raidId, difficulty);
        if (_lockouts == null) return null;

        return _lockouts.TryGetValue(key, out var lockout) ? lockout : null;
    }

    /// <summary>
    /// Obtient tous les lockouts actifs.
    /// </summary>
    /// <returns>Liste des lockouts.</returns>
    public List<RaidLockout> GetAllLockouts()
    {
        var result = new List<RaidLockout>();

        if (_lockouts != null)
        {
            foreach (var lockout in _lockouts.Values)
            {
                if (!lockout.IsExpired)
                {
                    result.Add(lockout);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Reset tous les lockouts (debug/admin).
    /// </summary>
    public void ResetAllLockouts()
    {
        _lockouts?.Clear();
        OnLockoutsReset?.Invoke();
        Log("All lockouts reset");
    }

    #endregion

    #region Public Methods - Queries

    /// <summary>
    /// Obtient les raids disponibles pour un niveau.
    /// </summary>
    /// <param name="playerLevel">Niveau du joueur.</param>
    /// <returns>Raids accessibles.</returns>
    public List<RaidData> GetAvailableRaidsForLevel(int playerLevel)
    {
        var result = new List<RaidData>();

        if (_availableRaids != null)
        {
            foreach (var raid in _availableRaids)
            {
                if (raid.CanPlayerEnter(playerLevel))
                {
                    result.Add(raid);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Obtient la progression du raid actif.
    /// </summary>
    /// <returns>Progression (0-1).</returns>
    public float GetRaidProgress()
    {
        if (_activeRaid?.raidData?.bosses == null) return 0f;
        if (_activeRaid.raidData.bosses.Length == 0) return 0f;

        return (float)_activeRaid.completedBosses.Count / _activeRaid.raidData.bosses.Length;
    }

    /// <summary>
    /// Obtient le temps ecoule dans le raid.
    /// </summary>
    /// <returns>Temps en secondes.</returns>
    public float GetRaidDuration()
    {
        if (_activeRaid == null) return 0f;
        return (float)(DateTime.UtcNow - _activeRaid.startedAt).TotalSeconds;
    }

    #endregion

    #region Private Methods

    private void UpdateActiveRaid()
    {
        // TODO: Verifier le timer d'enrage, mecaniques, etc.
    }

    private void CheckLockoutExpirations()
    {
        if (_lockouts == null || _lockouts.Count == 0) return;

        var expired = new List<string>();
        foreach (var kvp in _lockouts)
        {
            if (kvp.Value.IsExpired)
            {
                expired.Add(kvp.Key);
            }
        }

        foreach (var key in expired)
        {
            _lockouts.Remove(key);
            Log($"Lockout expired: {key}");
        }
    }

    private void CreateLockout(ActiveRaid raid)
    {
        if (raid?.raidData == null) return;

        string key = GetLockoutKey(raid.raidData.raidId, raid.difficulty);

        var lockout = new RaidLockout
        {
            raidId = raid.raidData.raidId,
            difficulty = raid.difficulty,
            completedBosses = new List<string>(raid.completedBosses),
            createdAt = DateTime.UtcNow,
            expiresAt = CalculateLockoutExpiration(raid.raidData)
        };

        if (_lockouts == null) _lockouts = new Dictionary<string, RaidLockout>();
        _lockouts[key] = lockout;

        Log($"Lockout created: {key} (expires {lockout.expiresAt})");
    }

    private DateTime CalculateLockoutExpiration(RaidData raidData)
    {
        var now = DateTime.UtcNow;

        switch (raidData.lockoutType)
        {
            case RaidLockoutType.Daily:
                return now.Date.AddDays(1).AddHours(raidData.resetHourUTC);

            case RaidLockoutType.Weekly:
                int daysUntilReset = ((int)raidData.resetDayOfWeek - (int)now.DayOfWeek + 7) % 7;
                if (daysUntilReset == 0 && now.Hour >= raidData.resetHourUTC)
                {
                    daysUntilReset = 7;
                }
                return now.Date.AddDays(daysUntilReset).AddHours(raidData.resetHourUTC);

            case RaidLockoutType.Never:
                return DateTime.MaxValue;

            default:
                return now.AddDays(7);
        }
    }

    private void DistributeRaidRewards(ActiveRaid raid)
    {
        if (raid?.participants == null) return;

        float multiplier = raid.raidData.GetRewardMultiplier(raid.difficulty);
        int xpPerPlayer = Mathf.RoundToInt(raid.raidData.completionXP * multiplier);

        foreach (var playerId in raid.participants)
        {
            // TODO: Distribuer XP et items
            Log($"Raid rewards for {playerId}: {xpPerPlayer} XP");
        }
    }

    private string GetLockoutKey(string raidId, RaidDifficulty difficulty)
    {
        return $"{raidId}_{difficulty}";
    }

    private void Log(string message)
    {
        if (_debugMode)
        {
            Debug.Log($"[Raid] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[Raid] {message}");
    }

    #endregion
}

/// <summary>
/// Raid actif.
/// </summary>
[System.Serializable]
public class ActiveRaid
{
    public RaidData raidData;
    public RaidDifficulty difficulty;
    public DateTime startedAt;
    public int currentBossIndex;
    public int currentBossPhase;
    public int wipeCount;
    public List<string> completedBosses;
    public List<string> participants;
}

/// <summary>
/// Lockout de raid.
/// </summary>
[System.Serializable]
public class RaidLockout
{
    public string raidId;
    public RaidDifficulty difficulty;
    public List<string> completedBosses;
    public DateTime createdAt;
    public DateTime expiresAt;

    /// <summary>Lockout expire?</summary>
    public bool IsExpired => DateTime.UtcNow >= expiresAt;

    /// <summary>Temps restant avant expiration.</summary>
    public TimeSpan TimeRemaining => expiresAt - DateTime.UtcNow;
}
