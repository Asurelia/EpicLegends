using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire de distribution de loot.
/// Gere les differents modes de loot et le trading.
/// </summary>
public class LootManager : MonoBehaviour
{
    #region Singleton

    private static LootManager _instance;
    public static LootManager Instance
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

        _pendingRolls = new Dictionary<string, LootRollSession>();
        _lootHistory = new List<LootHistoryEntry>();
        _activeTrades = new Dictionary<string, TradeSession>();
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

    /// <summary>Duree d'un roll (secondes).</summary>
    private const float ROLL_TIMEOUT = 30f;

    /// <summary>Taille max de l'historique.</summary>
    private const int MAX_HISTORY_SIZE = 100;

    /// <summary>Duree avant expiration du loot au sol (secondes).</summary>
    private const float LOOT_EXPIRATION_TIME = 300f;

    #endregion

    #region Fields

    [Header("Configuration")]
    [SerializeField] private LootMode _currentLootMode = LootMode.PersonalLoot;
    [SerializeField] private float _lootRange = 10f;
    [SerializeField] private bool _allowNinjaPrevention = true;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;

    // Sessions de roll en cours
    private Dictionary<string, LootRollSession> _pendingRolls;

    // Historique des loots
    private List<LootHistoryEntry> _lootHistory;

    // Index Round Robin
    private int _roundRobinIndex = 0;

    // Trades actifs
    private Dictionary<string, TradeSession> _activeTrades;

    #endregion

    #region Events

    /// <summary>Declenche lors du drop d'un loot.</summary>
    public event Action<DroppedLoot> OnLootDropped;

    /// <summary>Declenche lors du ramassage d'un loot.</summary>
    public event Action<string, ItemData, int> OnLootPickedUp;

    /// <summary>Declenche lors du debut d'un roll.</summary>
    public event Action<LootRollSession> OnRollStarted;

    /// <summary>Declenche lors de la fin d'un roll.</summary>
    public event Action<LootRollSession, string> OnRollEnded;

    /// <summary>Declenche lors d'un changement de mode.</summary>
    public event Action<LootMode> OnLootModeChanged;

    /// <summary>Declenche lors d'une demande de trade.</summary>
    public event Action<TradeSession> OnTradeRequested;

    /// <summary>Declenche lors de la completion d'un trade.</summary>
    public event Action<TradeSession> OnTradeCompleted;

    #endregion

    #region Properties

    /// <summary>Mode de loot actuel.</summary>
    public LootMode CurrentLootMode => _currentLootMode;

    /// <summary>Distance de loot.</summary>
    public float LootRange => _lootRange;

    /// <summary>Historique des loots.</summary>
    public IReadOnlyList<LootHistoryEntry> LootHistory => _lootHistory;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        UpdatePendingRolls();
    }

    #endregion

    #region Public Methods - Loot Mode

    /// <summary>
    /// Definit le mode de loot (leader seulement).
    /// </summary>
    /// <param name="mode">Mode de loot.</param>
    public void SetLootMode(LootMode mode)
    {
        if (!PartyManager.Instance?.IsPartyLeader ?? false)
        {
            LogError("Only party leader can change loot mode");
            return;
        }

        _currentLootMode = mode;
        OnLootModeChanged?.Invoke(mode);

        Log($"Loot mode changed to: {mode}");
    }

    #endregion

    #region Public Methods - Loot Distribution

    /// <summary>
    /// Genere et distribue le loot.
    /// </summary>
    /// <param name="source">Source du loot.</param>
    /// <param name="lootTable">Table de loot.</param>
    /// <param name="position">Position du loot.</param>
    public void GenerateLoot(GameObject source, LootTable lootTable, Vector3 position)
    {
        if (lootTable == null) return;

        var items = RollLootTable(lootTable);

        foreach (var item in items)
        {
            DistributeLoot(item.item, item.amount, position, source);
        }
    }

    /// <summary>
    /// Distribue un item selon le mode de loot.
    /// </summary>
    /// <param name="item">Item a distribuer.</param>
    /// <param name="amount">Quantite.</param>
    /// <param name="position">Position.</param>
    /// <param name="source">Source du loot.</param>
    public void DistributeLoot(ItemData item, int amount, Vector3 position, GameObject source = null)
    {
        if (item == null) return;

        switch (_currentLootMode)
        {
            case LootMode.FreeForAll:
                DropLootOnGround(item, amount, position);
                break;

            case LootMode.RoundRobin:
                AssignToNextPlayer(item, amount, position);
                break;

            case LootMode.NeedGreed:
                StartNeedGreedRoll(item, amount, position);
                break;

            case LootMode.PersonalLoot:
                GeneratePersonalLoot(item, amount, position);
                break;

            case LootMode.MasterLooter:
                NotifyMasterLooter(item, amount, position);
                break;
        }
    }

    /// <summary>
    /// Tente de ramasser un loot.
    /// </summary>
    /// <param name="lootId">ID du loot.</param>
    /// <param name="playerId">ID du joueur.</param>
    /// <returns>True si ramasse.</returns>
    public bool TryPickupLoot(string lootId, string playerId)
    {
        // TODO: Verifier la distance et la propriete
        Log($"Loot {lootId} picked up by {playerId}");
        return true;
    }

    #endregion

    #region Public Methods - Need/Greed Rolling

    /// <summary>
    /// Soumet un roll pour un loot.
    /// </summary>
    /// <param name="rollSessionId">ID de la session.</param>
    /// <param name="playerId">ID du joueur.</param>
    /// <param name="rollType">Type de roll.</param>
    public void SubmitRoll(string rollSessionId, string playerId, LootRollType rollType)
    {
        if (_pendingRolls == null || !_pendingRolls.TryGetValue(rollSessionId, out var session))
        {
            LogError("Roll session not found");
            return;
        }

        if (session.hasRolled.Contains(playerId))
        {
            LogError("Already rolled");
            return;
        }

        var roll = new LootRoll
        {
            playerId = playerId,
            rollType = rollType,
            rollValue = rollType == LootRollType.Pass ? 0 : UnityEngine.Random.Range(1, 101)
        };

        session.rolls.Add(roll);
        session.hasRolled.Add(playerId);

        Log($"Roll submitted: {playerId} -> {rollType} ({roll.rollValue})");

        // Verifier si tous ont vote
        if (session.hasRolled.Count >= session.eligiblePlayers.Count)
        {
            ResolveRoll(session);
        }
    }

    #endregion

    #region Public Methods - Trading

    /// <summary>
    /// Initie un trade avec un joueur.
    /// </summary>
    /// <param name="targetPlayerId">ID du joueur cible.</param>
    /// <returns>Session de trade ou null.</returns>
    public TradeSession InitiateTrade(string targetPlayerId)
    {
        var localId = NetworkGameManager.Instance?.LocalPlayerId;
        if (string.IsNullOrEmpty(localId) || string.IsNullOrEmpty(targetPlayerId))
        {
            return null;
        }

        if (localId == targetPlayerId)
        {
            LogError("Cannot trade with self");
            return null;
        }

        var trade = new TradeSession
        {
            tradeId = GenerateTradeId(),
            initiatorId = localId,
            targetId = targetPlayerId,
            state = TradeState.Pending,
            createdAt = DateTime.UtcNow,
            initiatorItems = new List<TradeItem>(),
            targetItems = new List<TradeItem>()
        };

        if (_activeTrades == null) _activeTrades = new Dictionary<string, TradeSession>();
        _activeTrades[trade.tradeId] = trade;

        OnTradeRequested?.Invoke(trade);

        Log($"Trade initiated with {targetPlayerId}");
        return trade;
    }

    /// <summary>
    /// Accepte une demande de trade.
    /// </summary>
    /// <param name="tradeId">ID du trade.</param>
    /// <returns>True si accepte.</returns>
    public bool AcceptTrade(string tradeId)
    {
        if (_activeTrades == null || !_activeTrades.TryGetValue(tradeId, out var trade))
        {
            return false;
        }

        trade.state = TradeState.Active;
        Log($"Trade accepted: {tradeId}");
        return true;
    }

    /// <summary>
    /// Decline une demande de trade.
    /// </summary>
    /// <param name="tradeId">ID du trade.</param>
    public void DeclineTrade(string tradeId)
    {
        if (_activeTrades == null) return;

        if (_activeTrades.TryGetValue(tradeId, out var trade))
        {
            trade.state = TradeState.Cancelled;
            _activeTrades.Remove(tradeId);
        }

        Log($"Trade declined: {tradeId}");
    }

    /// <summary>
    /// Ajoute un item au trade.
    /// </summary>
    /// <param name="tradeId">ID du trade.</param>
    /// <param name="item">Item a ajouter.</param>
    /// <param name="amount">Quantite.</param>
    /// <returns>True si ajoute.</returns>
    public bool AddItemToTrade(string tradeId, ItemData item, int amount)
    {
        if (_activeTrades == null || !_activeTrades.TryGetValue(tradeId, out var trade))
        {
            return false;
        }

        if (trade.state != TradeState.Active)
        {
            return false;
        }

        var localId = NetworkGameManager.Instance?.LocalPlayerId;
        var tradeItem = new TradeItem { item = item, amount = amount };

        if (trade.initiatorId == localId)
        {
            trade.initiatorItems.Add(tradeItem);
            trade.initiatorConfirmed = false;
        }
        else if (trade.targetId == localId)
        {
            trade.targetItems.Add(tradeItem);
            trade.targetConfirmed = false;
        }

        Log($"Item added to trade: {item?.displayName}");
        return true;
    }

    /// <summary>
    /// Confirme le trade.
    /// </summary>
    /// <param name="tradeId">ID du trade.</param>
    public void ConfirmTrade(string tradeId)
    {
        if (_activeTrades == null || !_activeTrades.TryGetValue(tradeId, out var trade))
        {
            return;
        }

        var localId = NetworkGameManager.Instance?.LocalPlayerId;

        if (trade.initiatorId == localId)
        {
            trade.initiatorConfirmed = true;
        }
        else if (trade.targetId == localId)
        {
            trade.targetConfirmed = true;
        }

        // Verifier si les deux ont confirme
        if (trade.initiatorConfirmed && trade.targetConfirmed)
        {
            CompleteTrade(trade);
        }
    }

    /// <summary>
    /// Annule un trade.
    /// </summary>
    /// <param name="tradeId">ID du trade.</param>
    public void CancelTrade(string tradeId)
    {
        if (_activeTrades == null) return;

        if (_activeTrades.TryGetValue(tradeId, out var trade))
        {
            trade.state = TradeState.Cancelled;
            _activeTrades.Remove(tradeId);
        }

        Log($"Trade cancelled: {tradeId}");
    }

    #endregion

    #region Public Methods - History

    /// <summary>
    /// Obtient l'historique recent.
    /// </summary>
    /// <param name="count">Nombre d'entrees.</param>
    /// <returns>Historique.</returns>
    public List<LootHistoryEntry> GetRecentHistory(int count = 10)
    {
        if (_lootHistory == null) return new List<LootHistoryEntry>();

        int start = Mathf.Max(0, _lootHistory.Count - count);
        return _lootHistory.GetRange(start, Mathf.Min(count, _lootHistory.Count - start));
    }

    #endregion

    #region Private Methods - Loot Distribution

    private void DropLootOnGround(ItemData item, int amount, Vector3 position)
    {
        var loot = new DroppedLoot
        {
            lootId = GenerateLootId(),
            item = item,
            amount = amount,
            position = position,
            droppedAt = DateTime.UtcNow,
            expiresAt = DateTime.UtcNow.AddSeconds(LOOT_EXPIRATION_TIME),
            ownerId = null // Accessible a tous
        };

        OnLootDropped?.Invoke(loot);
        AddToHistory(null, item, amount, "Free for all");
    }

    private void AssignToNextPlayer(ItemData item, int amount, Vector3 position)
    {
        var members = PartyManager.Instance?.GetAllMembers();
        if (members == null || members.Count == 0)
        {
            DropLootOnGround(item, amount, position);
            return;
        }

        _roundRobinIndex = (_roundRobinIndex + 1) % members.Count;
        var assignee = members[_roundRobinIndex];

        var loot = new DroppedLoot
        {
            lootId = GenerateLootId(),
            item = item,
            amount = amount,
            position = position,
            droppedAt = DateTime.UtcNow,
            expiresAt = DateTime.UtcNow.AddSeconds(LOOT_EXPIRATION_TIME),
            ownerId = assignee.playerId
        };

        OnLootDropped?.Invoke(loot);
        AddToHistory(assignee.playerId, item, amount, "Round Robin");
    }

    private void StartNeedGreedRoll(ItemData item, int amount, Vector3 position)
    {
        var members = PartyManager.Instance?.GetAllMembers();
        if (members == null || members.Count <= 1)
        {
            DropLootOnGround(item, amount, position);
            return;
        }

        var session = new LootRollSession
        {
            sessionId = GenerateRollSessionId(),
            item = item,
            amount = amount,
            position = position,
            startedAt = DateTime.UtcNow,
            expiresAt = DateTime.UtcNow.AddSeconds(ROLL_TIMEOUT),
            eligiblePlayers = new List<string>(),
            rolls = new List<LootRoll>(),
            hasRolled = new HashSet<string>()
        };

        foreach (var member in members)
        {
            session.eligiblePlayers.Add(member.playerId);
        }

        if (_pendingRolls == null) _pendingRolls = new Dictionary<string, LootRollSession>();
        _pendingRolls[session.sessionId] = session;

        OnRollStarted?.Invoke(session);
        Log($"Need/Greed roll started for {item.displayName}");
    }

    private void GeneratePersonalLoot(ItemData item, int amount, Vector3 position)
    {
        var members = PartyManager.Instance?.GetAllMembers();
        if (members == null || members.Count == 0)
        {
            DropLootOnGround(item, amount, position);
            return;
        }

        // Chaque joueur a une chance de recevoir le loot
        foreach (var member in members)
        {
            float chance = 1f / members.Count;
            if (UnityEngine.Random.value <= chance)
            {
                var loot = new DroppedLoot
                {
                    lootId = GenerateLootId(),
                    item = item,
                    amount = amount,
                    position = position + UnityEngine.Random.insideUnitSphere * 0.5f,
                    droppedAt = DateTime.UtcNow,
                    expiresAt = DateTime.UtcNow.AddSeconds(LOOT_EXPIRATION_TIME),
                    ownerId = member.playerId,
                    isPersonal = true
                };

                OnLootDropped?.Invoke(loot);
                AddToHistory(member.playerId, item, amount, "Personal Loot");
            }
        }
    }

    private void NotifyMasterLooter(ItemData item, int amount, Vector3 position)
    {
        var leaderId = PartyManager.Instance?.CurrentParty?.leaderId;
        if (string.IsNullOrEmpty(leaderId))
        {
            DropLootOnGround(item, amount, position);
            return;
        }

        var loot = new DroppedLoot
        {
            lootId = GenerateLootId(),
            item = item,
            amount = amount,
            position = position,
            droppedAt = DateTime.UtcNow,
            expiresAt = DateTime.UtcNow.AddSeconds(LOOT_EXPIRATION_TIME),
            ownerId = leaderId,
            isMasterLoot = true
        };

        OnLootDropped?.Invoke(loot);
        Log($"Master loot: {item.displayName} assigned to leader");
    }

    #endregion

    #region Private Methods - Rolling

    private void UpdatePendingRolls()
    {
        if (_pendingRolls == null || _pendingRolls.Count == 0) return;

        var expired = new List<string>();

        foreach (var kvp in _pendingRolls)
        {
            if (DateTime.UtcNow >= kvp.Value.expiresAt)
            {
                expired.Add(kvp.Key);
            }
        }

        foreach (var id in expired)
        {
            var session = _pendingRolls[id];
            ResolveRoll(session);
        }
    }

    private void ResolveRoll(LootRollSession session)
    {
        if (session == null) return;

        string winnerId = null;
        int highestRoll = 0;
        LootRollType winnerType = LootRollType.Pass;

        // Priorite: Need > Greed > Pass
        foreach (var roll in session.rolls)
        {
            bool isBetter = false;

            if (roll.rollType == LootRollType.Need)
            {
                if (winnerType != LootRollType.Need || roll.rollValue > highestRoll)
                {
                    isBetter = true;
                }
            }
            else if (roll.rollType == LootRollType.Greed && winnerType != LootRollType.Need)
            {
                if (winnerType != LootRollType.Greed || roll.rollValue > highestRoll)
                {
                    isBetter = true;
                }
            }

            if (isBetter)
            {
                winnerId = roll.playerId;
                highestRoll = roll.rollValue;
                winnerType = roll.rollType;
            }
        }

        _pendingRolls.Remove(session.sessionId);

        if (winnerId != null)
        {
            OnRollEnded?.Invoke(session, winnerId);
            AddToHistory(winnerId, session.item, session.amount, $"Roll ({winnerType}: {highestRoll})");
            Log($"Roll won by {winnerId} with {winnerType} {highestRoll}");
        }
        else
        {
            // Personne n'a voulu - drop au sol
            DropLootOnGround(session.item, session.amount, session.position);
        }
    }

    #endregion

    #region Private Methods - Trading

    private void CompleteTrade(TradeSession trade)
    {
        if (trade == null) return;

        trade.state = TradeState.Completed;

        // TODO: Transferer les items entre inventaires

        _activeTrades?.Remove(trade.tradeId);
        OnTradeCompleted?.Invoke(trade);

        Log($"Trade completed: {trade.tradeId}");
    }

    #endregion

    #region Private Methods - Utility

    private List<(ItemData item, int amount)> RollLootTable(LootTable table)
    {
        var result = new List<(ItemData, int)>();

        if (table?.entries == null) return result;

        foreach (var entry in table.entries)
        {
            if (UnityEngine.Random.value <= entry.dropChance)
            {
                int amount = UnityEngine.Random.Range(entry.minAmount, entry.maxAmount + 1);
                result.Add((entry.item, amount));
            }
        }

        return result;
    }

    private void AddToHistory(string playerId, ItemData item, int amount, string method)
    {
        if (_lootHistory == null) _lootHistory = new List<LootHistoryEntry>();

        _lootHistory.Add(new LootHistoryEntry
        {
            playerId = playerId,
            playerName = GetPlayerName(playerId),
            item = item,
            amount = amount,
            method = method,
            timestamp = DateTime.UtcNow
        });

        // Limiter la taille
        while (_lootHistory.Count > MAX_HISTORY_SIZE)
        {
            _lootHistory.RemoveAt(0);
        }
    }

    private string GetPlayerName(string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return "Unknown";
        var info = NetworkGameManager.Instance?.GetPlayerInfo(playerId);
        return info?.playerName ?? playerId;
    }

    private string GenerateLootId()
    {
        return $"LOOT{DateTime.UtcNow.Ticks:X}-{UnityEngine.Random.Range(1000, 9999)}";
    }

    private string GenerateRollSessionId()
    {
        return $"ROLL{DateTime.UtcNow.Ticks:X}-{UnityEngine.Random.Range(1000, 9999)}";
    }

    private string GenerateTradeId()
    {
        return $"TRD{DateTime.UtcNow.Ticks:X}-{UnityEngine.Random.Range(1000, 9999)}";
    }

    private void Log(string message)
    {
        if (_debugMode)
        {
            Debug.Log($"[Loot] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[Loot] {message}");
    }

    #endregion
}

/// <summary>
/// Modes de distribution de loot.
/// </summary>
public enum LootMode
{
    /// <summary>Premier arrive, premier servi.</summary>
    FreeForAll,

    /// <summary>Distribution a tour de role.</summary>
    RoundRobin,

    /// <summary>Systeme de votes Need/Greed.</summary>
    NeedGreed,

    /// <summary>Loot personnel pour chaque joueur.</summary>
    PersonalLoot,

    /// <summary>Le leader distribue.</summary>
    MasterLooter
}

/// <summary>
/// Types de roll pour Need/Greed.
/// </summary>
public enum LootRollType
{
    /// <summary>Besoin (priorite haute).</summary>
    Need,

    /// <summary>Cupidite (priorite moyenne).</summary>
    Greed,

    /// <summary>Passer (ne veut pas).</summary>
    Pass
}

/// <summary>
/// Loot au sol.
/// </summary>
[System.Serializable]
public class DroppedLoot
{
    public string lootId;
    public ItemData item;
    public int amount;
    public Vector3 position;
    public DateTime droppedAt;
    public DateTime expiresAt;
    public string ownerId;
    public bool isPersonal;
    public bool isMasterLoot;

    public bool IsExpired => DateTime.UtcNow >= expiresAt;
}

/// <summary>
/// Session de roll Need/Greed.
/// </summary>
[System.Serializable]
public class LootRollSession
{
    public string sessionId;
    public ItemData item;
    public int amount;
    public Vector3 position;
    public DateTime startedAt;
    public DateTime expiresAt;
    public List<string> eligiblePlayers;
    public List<LootRoll> rolls;
    public HashSet<string> hasRolled;
}

/// <summary>
/// Roll d'un joueur.
/// </summary>
[System.Serializable]
public class LootRoll
{
    public string playerId;
    public LootRollType rollType;
    public int rollValue;
}

/// <summary>
/// Entree d'historique de loot.
/// </summary>
[System.Serializable]
public class LootHistoryEntry
{
    public string playerId;
    public string playerName;
    public ItemData item;
    public int amount;
    public string method;
    public DateTime timestamp;
}

/// <summary>
/// Session de trade.
/// </summary>
[System.Serializable]
public class TradeSession
{
    public string tradeId;
    public string initiatorId;
    public string targetId;
    public TradeState state;
    public DateTime createdAt;
    public List<TradeItem> initiatorItems;
    public List<TradeItem> targetItems;
    public int initiatorGold;
    public int targetGold;
    public bool initiatorConfirmed;
    public bool targetConfirmed;
}

/// <summary>
/// Item dans un trade.
/// </summary>
[System.Serializable]
public class TradeItem
{
    public ItemData item;
    public int amount;
}

/// <summary>
/// Etats d'un trade.
/// </summary>
public enum TradeState
{
    /// <summary>En attente d'acceptation.</summary>
    Pending,

    /// <summary>Trade actif.</summary>
    Active,

    /// <summary>Les deux ont confirme.</summary>
    Confirmed,

    /// <summary>Trade complete.</summary>
    Completed,

    /// <summary>Trade annule.</summary>
    Cancelled
}
