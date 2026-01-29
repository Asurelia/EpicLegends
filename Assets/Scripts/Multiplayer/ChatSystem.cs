using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Systeme de chat multijoueur.
/// Gere les messages, canaux et historique.
/// </summary>
public class ChatSystem : MonoBehaviour
{
    #region Singleton

    private static ChatSystem _instance;
    public static ChatSystem Instance
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

        _messageHistory = new Dictionary<ChatChannel, List<ChatMessage>>();
        foreach (ChatChannel channel in Enum.GetValues(typeof(ChatChannel)))
        {
            _messageHistory[channel] = new List<ChatMessage>();
        }

        _mutedPlayers = new HashSet<string>();
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

    /// <summary>Taille max de l'historique par canal.</summary>
    private const int MAX_HISTORY_SIZE = 100;

    /// <summary>Longueur max d'un message.</summary>
    private const int MAX_MESSAGE_LENGTH = 500;

    /// <summary>Cooldown entre messages (secondes).</summary>
    private const float MESSAGE_COOLDOWN = 0.5f;

    #endregion

    #region Fields

    [Header("Configuration")]
    [SerializeField] private bool _enableProfanityFilter = true;
    [SerializeField] private string[] _filteredWords;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;

    // Historique des messages
    private Dictionary<ChatChannel, List<ChatMessage>> _messageHistory;

    // Joueurs mutes
    private HashSet<string> _mutedPlayers;

    // Timer de cooldown
    private float _lastMessageTime;

    #endregion

    #region Events

    /// <summary>Declenche lors de la reception d'un message.</summary>
    public event Action<ChatMessage> OnMessageReceived;

    /// <summary>Declenche lors de l'envoi d'un message.</summary>
    public event Action<ChatMessage> OnMessageSent;

    /// <summary>Declenche lors du mute d'un joueur.</summary>
    public event Action<string> OnPlayerMuted;

    /// <summary>Declenche lors du unmute d'un joueur.</summary>
    public event Action<string> OnPlayerUnmuted;

    #endregion

    #region Properties

    /// <summary>Profanity filter active?</summary>
    public bool IsProfanityFilterEnabled => _enableProfanityFilter;

    #endregion

    #region Public Methods - Sending

    /// <summary>
    /// Envoie un message.
    /// </summary>
    /// <param name="message">Contenu du message.</param>
    /// <param name="channel">Canal.</param>
    /// <param name="targetId">ID cible (pour whisper).</param>
    /// <returns>True si envoye.</returns>
    public bool SendMessage(string message, ChatChannel channel, string targetId = null)
    {
        if (string.IsNullOrEmpty(message))
        {
            return false;
        }

        // Cooldown
        if (Time.time - _lastMessageTime < MESSAGE_COOLDOWN)
        {
            LogError("Message cooldown active");
            return false;
        }

        // Limiter la longueur
        if (message.Length > MAX_MESSAGE_LENGTH)
        {
            message = message.Substring(0, MAX_MESSAGE_LENGTH);
        }

        // Filtrer les grossieretes
        if (_enableProfanityFilter)
        {
            message = FilterProfanity(message);
        }

        var localId = NetworkGameManager.Instance?.LocalPlayerId;
        var localInfo = NetworkGameManager.Instance?.GetLocalPlayerInfo();

        var chatMessage = new ChatMessage
        {
            messageId = GenerateMessageId(),
            senderId = localId,
            senderName = localInfo?.playerName ?? "Unknown",
            message = message,
            channel = channel,
            targetId = targetId,
            timestamp = DateTime.UtcNow
        };

        // Ajouter a l'historique local
        AddToHistory(chatMessage);

        // TODO: Envoyer via reseau
        OnMessageSent?.Invoke(chatMessage);

        _lastMessageTime = Time.time;

        Log($"[{channel}] {chatMessage.senderName}: {message}");
        return true;
    }

    /// <summary>
    /// Envoie un message systeme.
    /// </summary>
    /// <param name="message">Contenu.</param>
    /// <param name="channel">Canal.</param>
    public void SendSystemMessage(string message, ChatChannel channel = ChatChannel.System)
    {
        var chatMessage = new ChatMessage
        {
            messageId = GenerateMessageId(),
            senderId = "SYSTEM",
            senderName = "System",
            message = message,
            channel = channel,
            timestamp = DateTime.UtcNow,
            isSystemMessage = true
        };

        AddToHistory(chatMessage);
        OnMessageReceived?.Invoke(chatMessage);
    }

    #endregion

    #region Public Methods - Receiving

    /// <summary>
    /// Recoit un message du reseau.
    /// </summary>
    /// <param name="message">Message recu.</param>
    public void ReceiveMessage(ChatMessage message)
    {
        if (message == null) return;

        // Ignorer si mute
        if (_mutedPlayers != null && _mutedPlayers.Contains(message.senderId))
        {
            return;
        }

        // Filtrer les grossieretes
        if (_enableProfanityFilter && !message.isSystemMessage)
        {
            message.message = FilterProfanity(message.message);
        }

        AddToHistory(message);
        OnMessageReceived?.Invoke(message);
    }

    #endregion

    #region Public Methods - History

    /// <summary>
    /// Obtient l'historique d'un canal.
    /// </summary>
    /// <param name="channel">Canal.</param>
    /// <param name="count">Nombre de messages.</param>
    /// <returns>Messages.</returns>
    public List<ChatMessage> GetHistory(ChatChannel channel, int count = 50)
    {
        if (_messageHistory == null || !_messageHistory.TryGetValue(channel, out var history))
        {
            return new List<ChatMessage>();
        }

        int start = Mathf.Max(0, history.Count - count);
        return history.GetRange(start, Mathf.Min(count, history.Count - start));
    }

    /// <summary>
    /// Efface l'historique d'un canal.
    /// </summary>
    /// <param name="channel">Canal.</param>
    public void ClearHistory(ChatChannel channel)
    {
        if (_messageHistory != null && _messageHistory.ContainsKey(channel))
        {
            _messageHistory[channel].Clear();
        }
    }

    #endregion

    #region Public Methods - Muting

    /// <summary>
    /// Mute un joueur.
    /// </summary>
    /// <param name="playerId">ID du joueur.</param>
    public void MutePlayer(string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return;

        if (_mutedPlayers == null) _mutedPlayers = new HashSet<string>();
        _mutedPlayers.Add(playerId);

        OnPlayerMuted?.Invoke(playerId);
        Log($"Muted player: {playerId}");
    }

    /// <summary>
    /// Unmute un joueur.
    /// </summary>
    /// <param name="playerId">ID du joueur.</param>
    public void UnmutePlayer(string playerId)
    {
        if (_mutedPlayers != null && _mutedPlayers.Remove(playerId))
        {
            OnPlayerUnmuted?.Invoke(playerId);
            Log($"Unmuted player: {playerId}");
        }
    }

    /// <summary>
    /// Verifie si un joueur est mute.
    /// </summary>
    /// <param name="playerId">ID du joueur.</param>
    /// <returns>True si mute.</returns>
    public bool IsPlayerMuted(string playerId)
    {
        return _mutedPlayers != null && _mutedPlayers.Contains(playerId);
    }

    #endregion

    #region Private Methods

    private void AddToHistory(ChatMessage message)
    {
        if (_messageHistory == null) return;

        if (!_messageHistory.ContainsKey(message.channel))
        {
            _messageHistory[message.channel] = new List<ChatMessage>();
        }

        var history = _messageHistory[message.channel];
        history.Add(message);

        // Limiter la taille
        while (history.Count > MAX_HISTORY_SIZE)
        {
            history.RemoveAt(0);
        }
    }

    private string FilterProfanity(string message)
    {
        if (_filteredWords == null || _filteredWords.Length == 0)
        {
            return message;
        }

        string filtered = message;
        foreach (var word in _filteredWords)
        {
            if (string.IsNullOrEmpty(word)) continue;

            string replacement = new string('*', word.Length);
            filtered = System.Text.RegularExpressions.Regex.Replace(
                filtered,
                word,
                replacement,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
        }

        return filtered;
    }

    private string GenerateMessageId()
    {
        return $"MSG{DateTime.UtcNow.Ticks:X}-{UnityEngine.Random.Range(1000, 9999)}";
    }

    private void Log(string message)
    {
        if (_debugMode)
        {
            Debug.Log($"[Chat] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[Chat] {message}");
    }

    #endregion
}

/// <summary>
/// Message de chat.
/// </summary>
[System.Serializable]
public class ChatMessage
{
    public string messageId;
    public string senderId;
    public string senderName;
    public string message;
    public ChatChannel channel;
    public string targetId;
    public DateTime timestamp;
    public bool isSystemMessage;
    public Color senderColor;
}

/// <summary>
/// Canaux de chat.
/// </summary>
public enum ChatChannel
{
    /// <summary>Chat global.</summary>
    Global,

    /// <summary>Chat de groupe.</summary>
    Party,

    /// <summary>Message prive.</summary>
    Whisper,

    /// <summary>Messages systeme.</summary>
    System,

    /// <summary>Chat de raid.</summary>
    Raid,

    /// <summary>Chat local (proximite).</summary>
    Local,

    /// <summary>Chat de commerce.</summary>
    Trade
}

/// <summary>
/// Types de messages reseau.
/// </summary>
public enum NetworkMessageType
{
    /// <summary>Joueur rejoint.</summary>
    PlayerJoin,

    /// <summary>Joueur quitte.</summary>
    PlayerLeave,

    /// <summary>Synchronisation joueur.</summary>
    PlayerSync,

    /// <summary>Message de chat.</summary>
    Chat,

    /// <summary>Evenement mondial.</summary>
    WorldEvent,

    /// <summary>Action de combat.</summary>
    CombatAction,

    /// <summary>Mise a jour d'inventaire.</summary>
    InventoryUpdate,

    /// <summary>Mise a jour de quete.</summary>
    QuestUpdate,

    /// <summary>Commande de groupe.</summary>
    PartyCommand,

    /// <summary>Commande de loot.</summary>
    LootCommand,

    /// <summary>Commande de raid.</summary>
    RaidCommand
}
