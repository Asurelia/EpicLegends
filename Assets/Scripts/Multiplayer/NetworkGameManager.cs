using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire reseau principal.
/// Gere les connexions host/client, lobby et matchmaking.
/// </summary>
public class NetworkGameManager : MonoBehaviour
{
    #region Singleton

    private static NetworkGameManager _instance;
    public static NetworkGameManager Instance
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

        _connectedPlayers = new Dictionary<string, NetworkPlayerInfo>();
        _pendingReconnections = new Dictionary<string, float>();
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

    /// <summary>Nombre maximum de joueurs.</summary>
    public const int DEFAULT_MAX_PLAYERS = 4;

    /// <summary>Timeout de connexion (secondes).</summary>
    private const float CONNECTION_TIMEOUT = 30f;

    /// <summary>Temps avant abandon de reconnexion.</summary>
    private const float RECONNECTION_TIMEOUT = 60f;

    /// <summary>Port par defaut.</summary>
    private const int DEFAULT_PORT = 7777;

    #endregion

    #region Fields

    [Header("Configuration")]
    [SerializeField] private int _maxPlayers = DEFAULT_MAX_PLAYERS;
    [SerializeField] private int _port = DEFAULT_PORT;
    [SerializeField] private float _tickRate = 60f;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;

    // Etat de connexion
    private ConnectionState _currentState = ConnectionState.Disconnected;

    // Joueurs connectes
    private Dictionary<string, NetworkPlayerInfo> _connectedPlayers;

    // Reconnexions en attente
    private Dictionary<string, float> _pendingReconnections;

    // ID local
    private string _localPlayerId;

    // Info serveur
    private string _serverAddress;
    private bool _isHost = false;

    #endregion

    #region Events

    /// <summary>Declenche lors d'un changement d'etat.</summary>
    public event Action<ConnectionState, ConnectionState> OnStateChanged;

    /// <summary>Declenche lors de la connexion d'un joueur.</summary>
    public event Action<NetworkPlayerInfo> OnPlayerConnected;

    /// <summary>Declenche lors de la deconnexion d'un joueur.</summary>
    public event Action<NetworkPlayerInfo, DisconnectReason> OnPlayerDisconnected;

    /// <summary>Declenche lors d'une erreur reseau.</summary>
    public event Action<NetworkError> OnNetworkError;

    /// <summary>Declenche lors d'une tentative de reconnexion.</summary>
    public event Action<string, int> OnReconnectionAttempt;

    #endregion

    #region Properties

    /// <summary>Etat de connexion actuel.</summary>
    public ConnectionState CurrentState => _currentState;

    /// <summary>Est connecte?</summary>
    public bool IsConnected => _currentState == ConnectionState.Connected ||
                                _currentState == ConnectionState.Hosting;

    /// <summary>Est l'hote?</summary>
    public bool IsHost => _isHost;

    /// <summary>Nombre maximum de joueurs.</summary>
    public int MaxPlayers => _maxPlayers;

    /// <summary>Nombre de joueurs connectes.</summary>
    public int ConnectedPlayerCount => _connectedPlayers?.Count ?? 0;

    /// <summary>ID du joueur local.</summary>
    public string LocalPlayerId => _localPlayerId;

    /// <summary>Adresse du serveur.</summary>
    public string ServerAddress => _serverAddress;

    /// <summary>Tick rate.</summary>
    public float TickRate => _tickRate;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        if (_currentState == ConnectionState.Reconnecting)
        {
            UpdateReconnections();
        }
    }

    private void OnDestroy()
    {
        if (_currentState != ConnectionState.Disconnected)
        {
            Disconnect(DisconnectReason.Shutdown);
        }
    }

    #endregion

    #region Public Methods - Host

    /// <summary>
    /// Demarre un serveur en tant qu'hote.
    /// </summary>
    /// <param name="lobbyName">Nom du lobby.</param>
    /// <returns>True si demarre.</returns>
    public bool StartHost(string lobbyName = "Game Session")
    {
        if (_currentState != ConnectionState.Disconnected)
        {
            LogError("Cannot start host: already connected");
            return false;
        }

        try
        {
            SetState(ConnectionState.Connecting);

            // Generer ID local
            _localPlayerId = GeneratePlayerId();
            _isHost = true;
            _serverAddress = "localhost";

            // Simuler le demarrage du serveur
            // TODO: Integration avec Unity Netcode for GameObjects

            // Ajouter l'hote comme premier joueur
            var hostInfo = new NetworkPlayerInfo
            {
                playerId = _localPlayerId,
                playerName = "Host",
                isHost = true,
                isLocal = true,
                connectionTime = DateTime.UtcNow
            };

            _connectedPlayers[_localPlayerId] = hostInfo;

            SetState(ConnectionState.Hosting);

            Log($"Host started: {lobbyName}");
            OnPlayerConnected?.Invoke(hostInfo);

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to start host: {ex.Message}");
            SetState(ConnectionState.Disconnected);
            OnNetworkError?.Invoke(new NetworkError
            {
                errorType = NetworkErrorType.HostFailed,
                message = ex.Message
            });
            return false;
        }
    }

    /// <summary>
    /// Arrete le serveur hote.
    /// </summary>
    public void StopHost()
    {
        if (!_isHost) return;

        // Deconnecter tous les clients
        var playersCopy = new List<string>(_connectedPlayers.Keys);
        foreach (var playerId in playersCopy)
        {
            if (playerId != _localPlayerId)
            {
                KickPlayer(playerId, DisconnectReason.HostLeft);
            }
        }

        Disconnect(DisconnectReason.Normal);
        Log("Host stopped");
    }

    #endregion

    #region Public Methods - Client

    /// <summary>
    /// Connecte a un serveur.
    /// </summary>
    /// <param name="address">Adresse du serveur.</param>
    /// <param name="port">Port.</param>
    /// <returns>True si connexion initiee.</returns>
    public bool Connect(string address, int port = DEFAULT_PORT)
    {
        if (_currentState != ConnectionState.Disconnected)
        {
            LogError("Cannot connect: already connected");
            return false;
        }

        if (string.IsNullOrEmpty(address))
        {
            LogError("Cannot connect: invalid address");
            return false;
        }

        try
        {
            SetState(ConnectionState.Connecting);

            _localPlayerId = GeneratePlayerId();
            _serverAddress = address;
            _isHost = false;

            // TODO: Integration avec Unity Netcode for GameObjects
            // Simuler connexion reussie

            var localInfo = new NetworkPlayerInfo
            {
                playerId = _localPlayerId,
                playerName = "Player",
                isHost = false,
                isLocal = true,
                connectionTime = DateTime.UtcNow
            };

            _connectedPlayers[_localPlayerId] = localInfo;

            SetState(ConnectionState.Connected);

            Log($"Connected to {address}:{port}");
            OnPlayerConnected?.Invoke(localInfo);

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to connect: {ex.Message}");
            SetState(ConnectionState.Disconnected);
            OnNetworkError?.Invoke(new NetworkError
            {
                errorType = NetworkErrorType.ConnectionFailed,
                message = ex.Message
            });
            return false;
        }
    }

    /// <summary>
    /// Deconnecte du serveur.
    /// </summary>
    /// <param name="reason">Raison de deconnexion.</param>
    public void Disconnect(DisconnectReason reason = DisconnectReason.Normal)
    {
        if (_currentState == ConnectionState.Disconnected) return;

        var previousState = _currentState;

        // Notifier les autres joueurs
        var localInfo = GetLocalPlayerInfo();
        if (localInfo != null)
        {
            OnPlayerDisconnected?.Invoke(localInfo, reason);
        }

        // Nettoyer
        _connectedPlayers?.Clear();
        _pendingReconnections?.Clear();
        _localPlayerId = null;
        _serverAddress = null;
        _isHost = false;

        SetState(ConnectionState.Disconnected);

        Log($"Disconnected: {reason}");
    }

    /// <summary>
    /// Tente une reconnexion.
    /// </summary>
    /// <returns>True si reconnexion initiee.</returns>
    public bool AttemptReconnect()
    {
        if (string.IsNullOrEmpty(_serverAddress))
        {
            LogError("Cannot reconnect: no previous server");
            return false;
        }

        SetState(ConnectionState.Reconnecting);
        _pendingReconnections[_localPlayerId] = Time.time;

        OnReconnectionAttempt?.Invoke(_localPlayerId, 1);

        return true;
    }

    #endregion

    #region Public Methods - Player Management

    /// <summary>
    /// Obtient les infos d'un joueur.
    /// </summary>
    /// <param name="playerId">ID du joueur.</param>
    /// <returns>Infos ou null.</returns>
    public NetworkPlayerInfo GetPlayerInfo(string playerId)
    {
        if (_connectedPlayers == null) return null;
        return _connectedPlayers.TryGetValue(playerId, out var info) ? info : null;
    }

    /// <summary>
    /// Obtient les infos du joueur local.
    /// </summary>
    /// <returns>Infos ou null.</returns>
    public NetworkPlayerInfo GetLocalPlayerInfo()
    {
        return GetPlayerInfo(_localPlayerId);
    }

    /// <summary>
    /// Obtient tous les joueurs connectes.
    /// </summary>
    /// <returns>Liste des joueurs.</returns>
    public List<NetworkPlayerInfo> GetAllPlayers()
    {
        if (_connectedPlayers == null) return new List<NetworkPlayerInfo>();
        return new List<NetworkPlayerInfo>(_connectedPlayers.Values);
    }

    /// <summary>
    /// Expulse un joueur (hote seulement).
    /// </summary>
    /// <param name="playerId">ID du joueur.</param>
    /// <param name="reason">Raison.</param>
    public void KickPlayer(string playerId, DisconnectReason reason = DisconnectReason.Kicked)
    {
        if (!_isHost)
        {
            LogError("Only host can kick players");
            return;
        }

        if (playerId == _localPlayerId)
        {
            LogError("Cannot kick self");
            return;
        }

        if (_connectedPlayers != null && _connectedPlayers.TryGetValue(playerId, out var info))
        {
            _connectedPlayers.Remove(playerId);
            OnPlayerDisconnected?.Invoke(info, reason);
            Log($"Kicked player: {playerId}");
        }
    }

    /// <summary>
    /// Met a jour le nom du joueur.
    /// </summary>
    /// <param name="newName">Nouveau nom.</param>
    public void SetPlayerName(string newName)
    {
        if (string.IsNullOrEmpty(newName)) return;

        var info = GetLocalPlayerInfo();
        if (info != null)
        {
            info.playerName = newName;
        }
    }

    #endregion

    #region Private Methods

    private void SetState(ConnectionState newState)
    {
        if (_currentState == newState) return;

        var oldState = _currentState;
        _currentState = newState;

        OnStateChanged?.Invoke(oldState, newState);
    }

    private void UpdateReconnections()
    {
        if (_pendingReconnections == null || _pendingReconnections.Count == 0) return;

        var toRemove = new List<string>();

        foreach (var kvp in _pendingReconnections)
        {
            if (Time.time - kvp.Value > RECONNECTION_TIMEOUT)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var id in toRemove)
        {
            _pendingReconnections.Remove(id);
            LogError($"Reconnection timeout for {id}");
        }

        if (_pendingReconnections.Count == 0 && _currentState == ConnectionState.Reconnecting)
        {
            SetState(ConnectionState.Disconnected);
            OnNetworkError?.Invoke(new NetworkError
            {
                errorType = NetworkErrorType.ReconnectionFailed,
                message = "Reconnection timeout"
            });
        }
    }

    private string GeneratePlayerId()
    {
        return $"P{DateTime.UtcNow.Ticks:X}-{UnityEngine.Random.Range(1000, 9999)}";
    }

    private void Log(string message)
    {
        if (_debugMode)
        {
            Debug.Log($"[Network] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[Network] {message}");
    }

    #endregion
}

/// <summary>
/// Etats de connexion reseau.
/// </summary>
public enum ConnectionState
{
    /// <summary>Deconnecte.</summary>
    Disconnected,

    /// <summary>En cours de connexion.</summary>
    Connecting,

    /// <summary>Connecte en tant que client.</summary>
    Connected,

    /// <summary>Connecte en tant qu'hote.</summary>
    Hosting,

    /// <summary>En reconnexion.</summary>
    Reconnecting
}

/// <summary>
/// Raisons de deconnexion.
/// </summary>
public enum DisconnectReason
{
    /// <summary>Deconnexion normale.</summary>
    Normal,

    /// <summary>Timeout.</summary>
    Timeout,

    /// <summary>Expulse.</summary>
    Kicked,

    /// <summary>Banni.</summary>
    Banned,

    /// <summary>L'hote a quitte.</summary>
    HostLeft,

    /// <summary>Erreur reseau.</summary>
    NetworkError,

    /// <summary>Fermeture du jeu.</summary>
    Shutdown
}

/// <summary>
/// Types d'erreurs reseau.
/// </summary>
public enum NetworkErrorType
{
    /// <summary>Echec de connexion.</summary>
    ConnectionFailed,

    /// <summary>Echec de l'hote.</summary>
    HostFailed,

    /// <summary>Echec de reconnexion.</summary>
    ReconnectionFailed,

    /// <summary>Timeout.</summary>
    Timeout,

    /// <summary>Erreur de synchronisation.</summary>
    SyncError,

    /// <summary>Serveur complet.</summary>
    ServerFull,

    /// <summary>Version incompatible.</summary>
    VersionMismatch
}

/// <summary>
/// Erreur reseau.
/// </summary>
[System.Serializable]
public class NetworkError
{
    public NetworkErrorType errorType;
    public string message;
    public int errorCode;
}

/// <summary>
/// Informations d'un joueur connecte.
/// </summary>
[System.Serializable]
public class NetworkPlayerInfo
{
    public string playerId;
    public string playerName;
    public bool isHost;
    public bool isLocal;
    public DateTime connectionTime;
    public int ping;
    public PlayerNetworkState state;
}

/// <summary>
/// Etat reseau d'un joueur.
/// </summary>
public enum PlayerNetworkState
{
    Connecting,
    Connected,
    Loading,
    Ready,
    InGame,
    Disconnecting
}
