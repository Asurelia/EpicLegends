using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire de lobby.
/// Gere la creation, recherche et gestion des lobbies.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    #region Singleton

    private static LobbyManager _instance;
    public static LobbyManager Instance
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

        _lobbies = new Dictionary<string, LobbyData>();
        _currentLobby = null;
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
    [SerializeField] private int _maxLobbies = 100;
    [SerializeField] private float _lobbyRefreshInterval = 5f;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;

    // Lobbies disponibles
    private Dictionary<string, LobbyData> _lobbies;

    // Lobby actuel
    private LobbyData _currentLobby;

    // Timer de refresh
    private float _nextRefreshTime;

    #endregion

    #region Events

    /// <summary>Declenche lors de la creation d'un lobby.</summary>
    public event Action<LobbyData> OnLobbyCreated;

    /// <summary>Declenche lors de la jonction d'un lobby.</summary>
    public event Action<LobbyData> OnLobbyJoined;

    /// <summary>Declenche lors du depart d'un lobby.</summary>
    public event Action OnLobbyLeft;

    /// <summary>Declenche lors de la mise a jour de la liste.</summary>
    public event Action<List<LobbyData>> OnLobbyListUpdated;

    /// <summary>Declenche lors de la mise a jour d'un lobby.</summary>
    public event Action<LobbyData> OnLobbyUpdated;

    /// <summary>Declenche lors de l'arrivee d'un joueur.</summary>
    public event Action<LobbyPlayer> OnPlayerJoined;

    /// <summary>Declenche lors du depart d'un joueur.</summary>
    public event Action<LobbyPlayer> OnPlayerLeft;

    #endregion

    #region Properties

    /// <summary>Lobby actuel.</summary>
    public LobbyData CurrentLobby => _currentLobby;

    /// <summary>Est dans un lobby?</summary>
    public bool IsInLobby => _currentLobby != null;

    /// <summary>Est l'hote du lobby?</summary>
    public bool IsLobbyHost => _currentLobby != null &&
        _currentLobby.hostPlayerId == NetworkGameManager.Instance?.LocalPlayerId;

    /// <summary>Nombre de lobbies disponibles.</summary>
    public int AvailableLobbyCount => _lobbies?.Count ?? 0;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        if (Time.time >= _nextRefreshTime)
        {
            RefreshLobbyList();
            _nextRefreshTime = Time.time + _lobbyRefreshInterval;
        }
    }

    #endregion

    #region Public Methods - Lobby Management

    /// <summary>
    /// Cree un nouveau lobby.
    /// </summary>
    /// <param name="lobbyName">Nom du lobby.</param>
    /// <param name="hostPlayerId">ID de l'hote.</param>
    /// <param name="maxPlayers">Nombre max de joueurs.</param>
    /// <param name="isPublic">Lobby public?</param>
    /// <returns>Lobby cree ou null.</returns>
    public LobbyData CreateLobby(string lobbyName, string hostPlayerId, int maxPlayers = 4, bool isPublic = true)
    {
        if (string.IsNullOrEmpty(lobbyName) || string.IsNullOrEmpty(hostPlayerId))
        {
            Debug.LogError("[Lobby] Invalid lobby name or host ID");
            return null;
        }

        if (_currentLobby != null)
        {
            Debug.LogError("[Lobby] Already in a lobby");
            return null;
        }

        var lobby = new LobbyData
        {
            lobbyId = GenerateLobbyId(),
            lobbyName = lobbyName,
            hostPlayerId = hostPlayerId,
            maxPlayers = Mathf.Clamp(maxPlayers, 2, 4),
            isPublic = isPublic,
            createdAt = DateTime.UtcNow,
            state = LobbyState.Waiting,
            players = new List<LobbyPlayer>()
        };

        // Ajouter l'hote
        var hostPlayer = new LobbyPlayer
        {
            playerId = hostPlayerId,
            playerName = "Host",
            isHost = true,
            isReady = false,
            joinedAt = DateTime.UtcNow
        };
        lobby.players.Add(hostPlayer);

        // Stocker le lobby
        if (_lobbies == null) _lobbies = new Dictionary<string, LobbyData>();
        _lobbies[lobby.lobbyId] = lobby;
        _currentLobby = lobby;

        OnLobbyCreated?.Invoke(lobby);

        if (_debugMode)
        {
            Debug.Log($"[Lobby] Created: {lobbyName} ({lobby.lobbyId})");
        }

        return lobby;
    }

    /// <summary>
    /// Rejoint un lobby existant.
    /// </summary>
    /// <param name="lobbyId">ID du lobby.</param>
    /// <param name="playerId">ID du joueur.</param>
    /// <param name="playerName">Nom du joueur.</param>
    /// <returns>True si rejoint.</returns>
    public bool JoinLobby(string lobbyId, string playerId, string playerName)
    {
        if (_currentLobby != null)
        {
            Debug.LogError("[Lobby] Already in a lobby");
            return false;
        }

        if (_lobbies == null || !_lobbies.TryGetValue(lobbyId, out var lobby))
        {
            Debug.LogError("[Lobby] Lobby not found");
            return false;
        }

        if (lobby.IsFull)
        {
            Debug.LogError("[Lobby] Lobby is full");
            return false;
        }

        if (lobby.state != LobbyState.Waiting)
        {
            Debug.LogError("[Lobby] Lobby is not accepting players");
            return false;
        }

        var player = new LobbyPlayer
        {
            playerId = playerId,
            playerName = playerName,
            isHost = false,
            isReady = false,
            joinedAt = DateTime.UtcNow
        };

        lobby.players.Add(player);
        _currentLobby = lobby;

        OnLobbyJoined?.Invoke(lobby);
        OnPlayerJoined?.Invoke(player);

        if (_debugMode)
        {
            Debug.Log($"[Lobby] Joined: {lobby.lobbyName}");
        }

        return true;
    }

    /// <summary>
    /// Rejoint un lobby par code.
    /// </summary>
    /// <param name="joinCode">Code d'invitation.</param>
    /// <param name="playerId">ID du joueur.</param>
    /// <param name="playerName">Nom du joueur.</param>
    /// <returns>True si rejoint.</returns>
    public bool JoinLobbyByCode(string joinCode, string playerId, string playerName)
    {
        if (_lobbies == null) return false;

        foreach (var lobby in _lobbies.Values)
        {
            if (lobby.joinCode == joinCode)
            {
                return JoinLobby(lobby.lobbyId, playerId, playerName);
            }
        }

        Debug.LogError("[Lobby] Invalid join code");
        return false;
    }

    /// <summary>
    /// Quitte le lobby actuel.
    /// </summary>
    public void LeaveLobby()
    {
        if (_currentLobby == null) return;

        var localPlayerId = NetworkGameManager.Instance?.LocalPlayerId;

        // Retirer le joueur
        if (localPlayerId != null && _currentLobby.players != null)
        {
            var player = _currentLobby.players.Find(p => p.playerId == localPlayerId);
            if (player != null)
            {
                _currentLobby.players.Remove(player);
                OnPlayerLeft?.Invoke(player);
            }
        }

        // Si hote, supprimer le lobby
        if (_currentLobby.hostPlayerId == localPlayerId)
        {
            _lobbies?.Remove(_currentLobby.lobbyId);
        }

        _currentLobby = null;
        OnLobbyLeft?.Invoke();

        if (_debugMode)
        {
            Debug.Log("[Lobby] Left lobby");
        }
    }

    /// <summary>
    /// Demarre la partie (hote seulement).
    /// </summary>
    /// <returns>True si demarre.</returns>
    public bool StartGame()
    {
        if (_currentLobby == null || !IsLobbyHost)
        {
            Debug.LogError("[Lobby] Cannot start: not host");
            return false;
        }

        if (!AreAllPlayersReady())
        {
            Debug.LogError("[Lobby] Cannot start: not all players ready");
            return false;
        }

        _currentLobby.state = LobbyState.Starting;
        OnLobbyUpdated?.Invoke(_currentLobby);

        if (_debugMode)
        {
            Debug.Log("[Lobby] Game starting...");
        }

        return true;
    }

    #endregion

    #region Public Methods - Player Management

    /// <summary>
    /// Definit l'etat pret du joueur local.
    /// </summary>
    /// <param name="isReady">Est pret?</param>
    public void SetReady(bool isReady)
    {
        if (_currentLobby == null) return;

        var localPlayerId = NetworkGameManager.Instance?.LocalPlayerId;
        if (localPlayerId == null) return;

        var player = _currentLobby.players?.Find(p => p.playerId == localPlayerId);
        if (player != null)
        {
            player.isReady = isReady;
            OnLobbyUpdated?.Invoke(_currentLobby);
        }
    }

    /// <summary>
    /// Expulse un joueur (hote seulement).
    /// </summary>
    /// <param name="playerId">ID du joueur.</param>
    public void KickPlayer(string playerId)
    {
        if (_currentLobby == null || !IsLobbyHost) return;
        if (playerId == _currentLobby.hostPlayerId) return;

        var player = _currentLobby.players?.Find(p => p.playerId == playerId);
        if (player != null)
        {
            _currentLobby.players.Remove(player);
            OnPlayerLeft?.Invoke(player);
            OnLobbyUpdated?.Invoke(_currentLobby);
        }
    }

    /// <summary>
    /// Verifie si tous les joueurs sont prets.
    /// </summary>
    /// <returns>True si tous prets.</returns>
    public bool AreAllPlayersReady()
    {
        if (_currentLobby?.players == null) return false;

        foreach (var player in _currentLobby.players)
        {
            if (!player.isReady && !player.isHost) return false;
        }

        return _currentLobby.PlayerCount >= 1;
    }

    #endregion

    #region Public Methods - Lobby Search

    /// <summary>
    /// Rafraichit la liste des lobbies.
    /// </summary>
    public void RefreshLobbyList()
    {
        // TODO: Integration avec un service de matchmaking
        var publicLobbies = GetPublicLobbies();
        OnLobbyListUpdated?.Invoke(publicLobbies);
    }

    /// <summary>
    /// Obtient les lobbies publics.
    /// </summary>
    /// <returns>Liste des lobbies publics.</returns>
    public List<LobbyData> GetPublicLobbies()
    {
        var result = new List<LobbyData>();

        if (_lobbies != null)
        {
            foreach (var lobby in _lobbies.Values)
            {
                if (lobby.isPublic && lobby.state == LobbyState.Waiting && !lobby.IsFull)
                {
                    result.Add(lobby);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Recherche des lobbies.
    /// </summary>
    /// <param name="filter">Filtre de recherche.</param>
    /// <returns>Lobbies correspondants.</returns>
    public List<LobbyData> SearchLobbies(LobbySearchFilter filter)
    {
        var result = new List<LobbyData>();

        if (_lobbies == null) return result;

        foreach (var lobby in _lobbies.Values)
        {
            if (!lobby.isPublic) continue;
            if (lobby.state != LobbyState.Waiting) continue;
            if (lobby.IsFull) continue;

            // Appliquer filtres
            if (!string.IsNullOrEmpty(filter.nameContains) &&
                !lobby.lobbyName.ToLower().Contains(filter.nameContains.ToLower()))
            {
                continue;
            }

            if (filter.minPlayers > 0 && lobby.PlayerCount < filter.minPlayers)
            {
                continue;
            }

            if (filter.maxPlayers > 0 && lobby.maxPlayers > filter.maxPlayers)
            {
                continue;
            }

            result.Add(lobby);
        }

        return result;
    }

    #endregion

    #region Private Methods

    private string GenerateLobbyId()
    {
        return $"L{DateTime.UtcNow.Ticks:X}-{UnityEngine.Random.Range(1000, 9999)}";
    }

    private string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var code = new char[6];

        for (int i = 0; i < 6; i++)
        {
            code[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
        }

        return new string(code);
    }

    #endregion
}

/// <summary>
/// Donnees d'un lobby.
/// </summary>
[System.Serializable]
public class LobbyData
{
    public string lobbyId;
    public string lobbyName;
    public string hostPlayerId;
    public int maxPlayers = 4;
    public bool isPublic = true;
    public string joinCode;
    public DateTime createdAt;
    public LobbyState state;
    public List<LobbyPlayer> players;
    public LobbySettings settings;

    /// <summary>Nombre de joueurs.</summary>
    public int PlayerCount => players?.Count ?? 0;

    /// <summary>Lobby plein?</summary>
    public bool IsFull => PlayerCount >= maxPlayers;
}

/// <summary>
/// Joueur dans un lobby.
/// </summary>
[System.Serializable]
public class LobbyPlayer
{
    public string playerId;
    public string playerName;
    public bool isHost;
    public bool isReady;
    public DateTime joinedAt;
    public int characterLevel;
    public string selectedClass;
}

/// <summary>
/// Etats du lobby.
/// </summary>
public enum LobbyState
{
    /// <summary>En attente de joueurs.</summary>
    Waiting,

    /// <summary>Demarrage en cours.</summary>
    Starting,

    /// <summary>Partie en cours.</summary>
    InProgress,

    /// <summary>Termine.</summary>
    Finished,

    /// <summary>Ferme.</summary>
    Closed
}

/// <summary>
/// Parametres du lobby.
/// </summary>
[System.Serializable]
public class LobbySettings
{
    public bool allowLatejoin = true;
    public bool friendsOnly = false;
    public int minLevel = 1;
    public int maxLevel = 100;
    public LootMode lootMode = LootMode.PersonalLoot;
    public XPShareMode xpShareMode = XPShareMode.Equal;
}

/// <summary>
/// Filtre de recherche de lobby.
/// </summary>
[System.Serializable]
public struct LobbySearchFilter
{
    public string nameContains;
    public int minPlayers;
    public int maxPlayers;
    public bool hasOpenSlots;
}
