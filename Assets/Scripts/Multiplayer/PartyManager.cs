using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire de groupe/party.
/// Gere la formation, roles, buffs et partage d'XP.
/// </summary>
public class PartyManager : MonoBehaviour
{
    #region Singleton

    private static PartyManager _instance;
    public static PartyManager Instance
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

        _currentParty = null;
        _activeBuffs = new List<PartyBuff>();
        _invitations = new Dictionary<string, PartyInvitation>();
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

    /// <summary>Taille maximum du groupe.</summary>
    public const int MAX_PARTY_SIZE = 4;

    /// <summary>Duree d'une invitation (secondes).</summary>
    private const float INVITATION_TIMEOUT = 60f;

    /// <summary>Distance max pour XP de proximite.</summary>
    private const float PROXIMITY_XP_RANGE = 50f;

    #endregion

    #region Fields

    [Header("Configuration")]
    [SerializeField] private XPShareMode _defaultXPMode = XPShareMode.Equal;
    [SerializeField] private float _partyBuffRadius = 30f;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;

    // Groupe actuel
    private PartyData _currentParty;

    // Buffs actifs
    private List<PartyBuff> _activeBuffs;

    // Invitations en attente
    private Dictionary<string, PartyInvitation> _invitations;

    #endregion

    #region Events

    /// <summary>Declenche lors de la creation d'un groupe.</summary>
    public event Action<PartyData> OnPartyCreated;

    /// <summary>Declenche lors de la dissolution du groupe.</summary>
    public event Action OnPartyDisbanded;

    /// <summary>Declenche lors de l'arrivee d'un membre.</summary>
    public event Action<PartyMember> OnMemberJoined;

    /// <summary>Declenche lors du depart d'un membre.</summary>
    public event Action<PartyMember> OnMemberLeft;

    /// <summary>Declenche lors d'un changement de role.</summary>
    public event Action<PartyMember, PartyRole> OnRoleChanged;

    /// <summary>Declenche lors d'un changement de leader.</summary>
    public event Action<PartyMember> OnLeaderChanged;

    /// <summary>Declenche lors de la reception d'une invitation.</summary>
    public event Action<PartyInvitation> OnInvitationReceived;

    /// <summary>Declenche lors du gain d'XP partage.</summary>
    public event Action<string, int, XPSource> OnSharedXPGained;

    #endregion

    #region Properties

    /// <summary>Groupe actuel.</summary>
    public PartyData CurrentParty => _currentParty;

    /// <summary>Est dans un groupe?</summary>
    public bool IsInParty => _currentParty != null;

    /// <summary>Est le leader?</summary>
    public bool IsPartyLeader
    {
        get
        {
            if (_currentParty == null) return false;
            var localId = NetworkGameManager.Instance?.LocalPlayerId;
            return _currentParty.leaderId == localId;
        }
    }

    /// <summary>Taille maximum du groupe.</summary>
    public int MaxPartySize => MAX_PARTY_SIZE;

    /// <summary>Nombre de membres.</summary>
    public int MemberCount => _currentParty?.MemberCount ?? 0;

    /// <summary>Buffs actifs.</summary>
    public IReadOnlyList<PartyBuff> ActiveBuffs => _activeBuffs;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        CleanupExpiredInvitations();
        UpdatePartyBuffs();
    }

    #endregion

    #region Public Methods - Party Management

    /// <summary>
    /// Cree un nouveau groupe.
    /// </summary>
    /// <param name="leaderId">ID du leader.</param>
    /// <returns>True si cree.</returns>
    public bool CreateParty(string leaderId)
    {
        if (_currentParty != null)
        {
            LogError("Already in a party");
            return false;
        }

        if (string.IsNullOrEmpty(leaderId))
        {
            LogError("Invalid leader ID");
            return false;
        }

        _currentParty = new PartyData
        {
            partyId = GeneratePartyId(),
            leaderId = leaderId,
            createdAt = DateTime.UtcNow,
            xpShareMode = _defaultXPMode,
            members = new List<PartyMember>()
        };

        // Ajouter le leader
        var leader = new PartyMember
        {
            playerId = leaderId,
            playerName = "Leader",
            role = PartyRole.Leader,
            joinedAt = DateTime.UtcNow,
            isOnline = true
        };
        _currentParty.members.Add(leader);

        OnPartyCreated?.Invoke(_currentParty);

        Log($"Party created: {_currentParty.partyId}");
        return true;
    }

    /// <summary>
    /// Dissout le groupe (leader seulement).
    /// </summary>
    public void DisbandParty()
    {
        if (_currentParty == null) return;
        if (!IsPartyLeader)
        {
            LogError("Only leader can disband party");
            return;
        }

        OnPartyDisbanded?.Invoke();
        _currentParty = null;
        _activeBuffs?.Clear();

        Log("Party disbanded");
    }

    /// <summary>
    /// Quitte le groupe.
    /// </summary>
    public void LeaveParty()
    {
        if (_currentParty == null) return;

        var localId = NetworkGameManager.Instance?.LocalPlayerId;
        var member = GetMember(localId);

        if (member != null)
        {
            _currentParty.members.Remove(member);
            OnMemberLeft?.Invoke(member);
        }

        // Si leader part, transferer ou dissoudre
        if (IsPartyLeader)
        {
            if (_currentParty.MemberCount > 0)
            {
                PromoteToLeader(_currentParty.members[0].playerId);
            }
            else
            {
                DisbandParty();
                return;
            }
        }

        _currentParty = null;
        Log("Left party");
    }

    #endregion

    #region Public Methods - Invitations

    /// <summary>
    /// Invite un joueur au groupe.
    /// </summary>
    /// <param name="targetPlayerId">ID du joueur cible.</param>
    /// <returns>True si invitation envoyee.</returns>
    public bool InvitePlayer(string targetPlayerId)
    {
        if (_currentParty == null)
        {
            // Creer un groupe si necessaire
            var localId = NetworkGameManager.Instance?.LocalPlayerId;
            if (!CreateParty(localId))
            {
                return false;
            }
        }

        if (_currentParty.MemberCount >= MAX_PARTY_SIZE)
        {
            LogError("Party is full");
            return false;
        }

        if (IsMember(targetPlayerId))
        {
            LogError("Player already in party");
            return false;
        }

        var invitation = new PartyInvitation
        {
            invitationId = GenerateInvitationId(),
            partyId = _currentParty.partyId,
            inviterId = NetworkGameManager.Instance?.LocalPlayerId,
            targetPlayerId = targetPlayerId,
            createdAt = DateTime.UtcNow,
            expiresAt = DateTime.UtcNow.AddSeconds(INVITATION_TIMEOUT)
        };

        if (_invitations == null) _invitations = new Dictionary<string, PartyInvitation>();
        _invitations[invitation.invitationId] = invitation;

        // TODO: Envoyer via reseau
        OnInvitationReceived?.Invoke(invitation);

        Log($"Invitation sent to {targetPlayerId}");
        return true;
    }

    /// <summary>
    /// Accepte une invitation.
    /// </summary>
    /// <param name="invitationId">ID de l'invitation.</param>
    /// <returns>True si acceptee.</returns>
    public bool AcceptInvitation(string invitationId)
    {
        if (_invitations == null || !_invitations.TryGetValue(invitationId, out var invitation))
        {
            LogError("Invitation not found");
            return false;
        }

        if (invitation.IsExpired)
        {
            _invitations.Remove(invitationId);
            LogError("Invitation expired");
            return false;
        }

        // Quitter le groupe actuel si necessaire
        if (_currentParty != null)
        {
            LeaveParty();
        }

        // TODO: Rejoindre le groupe via reseau
        _invitations.Remove(invitationId);

        Log($"Accepted invitation: {invitationId}");
        return true;
    }

    /// <summary>
    /// Decline une invitation.
    /// </summary>
    /// <param name="invitationId">ID de l'invitation.</param>
    public void DeclineInvitation(string invitationId)
    {
        _invitations?.Remove(invitationId);
        Log($"Declined invitation: {invitationId}");
    }

    #endregion

    #region Public Methods - Members

    /// <summary>
    /// Expulse un membre (leader seulement).
    /// </summary>
    /// <param name="playerId">ID du joueur.</param>
    public void KickMember(string playerId)
    {
        if (_currentParty == null || !IsPartyLeader) return;
        if (playerId == _currentParty.leaderId) return;

        var member = GetMember(playerId);
        if (member != null)
        {
            _currentParty.members.Remove(member);
            OnMemberLeft?.Invoke(member);
            Log($"Kicked member: {playerId}");
        }
    }

    /// <summary>
    /// Promeut un membre au rang de leader.
    /// </summary>
    /// <param name="playerId">ID du joueur.</param>
    public void PromoteToLeader(string playerId)
    {
        if (_currentParty == null || !IsPartyLeader) return;
        if (playerId == _currentParty.leaderId) return;

        var member = GetMember(playerId);
        if (member == null) return;

        // Retrograder l'ancien leader
        var oldLeader = GetMember(_currentParty.leaderId);
        if (oldLeader != null)
        {
            oldLeader.role = PartyRole.Member;
        }

        // Promouvoir le nouveau
        member.role = PartyRole.Leader;
        _currentParty.leaderId = playerId;

        OnLeaderChanged?.Invoke(member);
        Log($"New leader: {playerId}");
    }

    /// <summary>
    /// Definit le role d'un membre.
    /// </summary>
    /// <param name="playerId">ID du joueur.</param>
    /// <param name="role">Nouveau role.</param>
    public void SetMemberRole(string playerId, PartyRole role)
    {
        if (_currentParty == null) return;
        if (role == PartyRole.Leader && !IsPartyLeader) return;

        var member = GetMember(playerId);
        if (member == null) return;

        var oldRole = member.role;
        member.role = role;

        OnRoleChanged?.Invoke(member, oldRole);
        Log($"Role changed: {playerId} -> {role}");
    }

    /// <summary>
    /// Obtient un membre du groupe.
    /// </summary>
    /// <param name="playerId">ID du joueur.</param>
    /// <returns>Membre ou null.</returns>
    public PartyMember GetMember(string playerId)
    {
        return _currentParty?.members?.Find(m => m.playerId == playerId);
    }

    /// <summary>
    /// Verifie si un joueur est membre.
    /// </summary>
    /// <param name="playerId">ID du joueur.</param>
    /// <returns>True si membre.</returns>
    public bool IsMember(string playerId)
    {
        return GetMember(playerId) != null;
    }

    /// <summary>
    /// Obtient tous les membres.
    /// </summary>
    /// <returns>Liste des membres.</returns>
    public List<PartyMember> GetAllMembers()
    {
        return _currentParty?.members ?? new List<PartyMember>();
    }

    #endregion

    #region Public Methods - XP Sharing

    /// <summary>
    /// Distribue de l'XP au groupe.
    /// </summary>
    /// <param name="totalXP">XP totale.</param>
    /// <param name="source">Source de l'XP.</param>
    /// <param name="sourcePosition">Position de la source.</param>
    public void DistributeXP(int totalXP, XPSource source, Vector3 sourcePosition)
    {
        if (_currentParty == null || totalXP <= 0) return;

        var eligibleMembers = GetEligibleMembersForXP(sourcePosition);
        if (eligibleMembers.Count == 0) return;

        switch (_currentParty.xpShareMode)
        {
            case XPShareMode.Equal:
                DistributeXPEqual(totalXP, source, eligibleMembers);
                break;

            case XPShareMode.Contribution:
                // TODO: Tracker les contributions
                DistributeXPEqual(totalXP, source, eligibleMembers);
                break;

            case XPShareMode.Proximity:
                DistributeXPProximity(totalXP, source, sourcePosition, eligibleMembers);
                break;

            case XPShareMode.NoShare:
                // XP va au joueur qui a tue
                break;
        }
    }

    /// <summary>
    /// Definit le mode de partage d'XP (leader seulement).
    /// </summary>
    /// <param name="mode">Mode de partage.</param>
    public void SetXPShareMode(XPShareMode mode)
    {
        if (_currentParty == null || !IsPartyLeader) return;

        _currentParty.xpShareMode = mode;
        Log($"XP share mode: {mode}");
    }

    #endregion

    #region Public Methods - Buffs

    /// <summary>
    /// Ajoute un buff de groupe.
    /// </summary>
    /// <param name="buff">Buff a ajouter.</param>
    public void AddPartyBuff(PartyBuff buff)
    {
        if (buff == null) return;
        if (_activeBuffs == null) _activeBuffs = new List<PartyBuff>();

        // Verifier si le buff existe deja
        var existing = _activeBuffs.Find(b => b.buffId == buff.buffId);
        if (existing != null)
        {
            // Rafraichir la duree
            existing.expiresAt = buff.expiresAt;
        }
        else
        {
            _activeBuffs.Add(buff);
        }

        Log($"Party buff added: {buff.buffName}");
    }

    /// <summary>
    /// Retire un buff de groupe.
    /// </summary>
    /// <param name="buffId">ID du buff.</param>
    public void RemovePartyBuff(string buffId)
    {
        if (_activeBuffs == null) return;

        _activeBuffs.RemoveAll(b => b.buffId == buffId);
        Log($"Party buff removed: {buffId}");
    }

    /// <summary>
    /// Obtient le bonus total d'une stat.
    /// </summary>
    /// <param name="stat">Type de stat.</param>
    /// <returns>Bonus total.</returns>
    public float GetTotalStatBonus(StatType stat)
    {
        if (_activeBuffs == null) return 0f;

        float total = 0f;
        foreach (var buff in _activeBuffs)
        {
            if (buff.statType == stat && !buff.IsExpired)
            {
                total += buff.isPercentage ? buff.bonusValue / 100f : buff.bonusValue;
            }
        }

        return total;
    }

    #endregion

    #region Private Methods

    private void CleanupExpiredInvitations()
    {
        if (_invitations == null || _invitations.Count == 0) return;

        var toRemove = new List<string>();
        foreach (var kvp in _invitations)
        {
            if (kvp.Value.IsExpired)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var id in toRemove)
        {
            _invitations.Remove(id);
        }
    }

    private void UpdatePartyBuffs()
    {
        if (_activeBuffs == null) return;

        _activeBuffs.RemoveAll(b => b.IsExpired);
    }

    private List<PartyMember> GetEligibleMembersForXP(Vector3 sourcePosition)
    {
        var eligible = new List<PartyMember>();

        if (_currentParty?.members == null) return eligible;

        foreach (var member in _currentParty.members)
        {
            if (!member.isOnline) continue;

            // TODO: Verifier la distance via NetworkSyncManager
            eligible.Add(member);
        }

        return eligible;
    }

    private void DistributeXPEqual(int totalXP, XPSource source, List<PartyMember> members)
    {
        if (members.Count == 0) return;

        // Bonus de groupe
        float groupBonus = 1f + (members.Count - 1) * 0.1f;
        int xpPerMember = Mathf.RoundToInt((totalXP * groupBonus) / members.Count);

        foreach (var member in members)
        {
            OnSharedXPGained?.Invoke(member.playerId, xpPerMember, source);
        }
    }

    private void DistributeXPProximity(int totalXP, XPSource source, Vector3 position, List<PartyMember> members)
    {
        // TODO: Calculer XP base sur la distance
        DistributeXPEqual(totalXP, source, members);
    }

    private string GeneratePartyId()
    {
        return $"PTY{DateTime.UtcNow.Ticks:X}-{UnityEngine.Random.Range(1000, 9999)}";
    }

    private string GenerateInvitationId()
    {
        return $"INV{DateTime.UtcNow.Ticks:X}-{UnityEngine.Random.Range(1000, 9999)}";
    }

    private void Log(string message)
    {
        if (_debugMode)
        {
            Debug.Log($"[Party] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[Party] {message}");
    }

    #endregion
}

/// <summary>
/// Donnees d'un groupe.
/// </summary>
[System.Serializable]
public class PartyData
{
    public string partyId;
    public string leaderId;
    public DateTime createdAt;
    public XPShareMode xpShareMode;
    public List<PartyMember> members;

    /// <summary>Nombre de membres.</summary>
    public int MemberCount => members?.Count ?? 0;

    /// <summary>Groupe complet?</summary>
    public bool IsFull => MemberCount >= PartyManager.MAX_PARTY_SIZE;
}

/// <summary>
/// Membre d'un groupe.
/// </summary>
[System.Serializable]
public class PartyMember
{
    public string playerId;
    public string playerName;
    public PartyRole role;
    public DateTime joinedAt;
    public bool isOnline;
    public int level;
    public float healthPercent;
    public float manaPercent;
    public Vector3 position;
}

/// <summary>
/// Roles dans un groupe.
/// </summary>
public enum PartyRole
{
    /// <summary>Leader du groupe.</summary>
    Leader,

    /// <summary>Membre standard.</summary>
    Member,

    /// <summary>Tank.</summary>
    Tank,

    /// <summary>Soigneur.</summary>
    Healer,

    /// <summary>DPS.</summary>
    DPS,

    /// <summary>Support.</summary>
    Support
}

/// <summary>
/// Modes de partage d'XP.
/// </summary>
public enum XPShareMode
{
    /// <summary>XP egale pour tous.</summary>
    Equal,

    /// <summary>XP basee sur la contribution.</summary>
    Contribution,

    /// <summary>XP basee sur la proximite.</summary>
    Proximity,

    /// <summary>Pas de partage.</summary>
    NoShare
}

/// <summary>
/// Buff de groupe.
/// </summary>
[System.Serializable]
public class PartyBuff
{
    public string buffId;
    public string buffName;
    public string description;
    public StatType statType;
    public float bonusValue;
    public bool isPercentage;
    public DateTime expiresAt;
    public string sourcePlayerId;
    public Sprite icon;

    /// <summary>Buff expire?</summary>
    public bool IsExpired => DateTime.UtcNow >= expiresAt;
}

/// <summary>
/// Invitation a un groupe.
/// </summary>
[System.Serializable]
public class PartyInvitation
{
    public string invitationId;
    public string partyId;
    public string inviterId;
    public string inviterName;
    public string targetPlayerId;
    public DateTime createdAt;
    public DateTime expiresAt;

    /// <summary>Invitation expiree?</summary>
    public bool IsExpired => DateTime.UtcNow >= expiresAt;
}
