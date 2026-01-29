using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests unitaires pour le systeme multijoueur.
/// Phase 5: Network, Sync, Party, Loot, Shared Base, Raids.
/// </summary>
public class MultiplayerSystemTests
{
    #region NetworkManager Tests

    [Test]
    public void NetworkManager_CanBeCreated()
    {
        // Arrange
        var go = new GameObject("NetworkManager");
        var manager = go.AddComponent<NetworkGameManager>();

        // Assert
        Assert.IsNotNull(manager);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void NetworkManager_StartsDisconnected()
    {
        // Arrange
        var go = new GameObject("NetworkManager");
        var manager = go.AddComponent<NetworkGameManager>();

        // Assert
        Assert.AreEqual(ConnectionState.Disconnected, manager.CurrentState);
        Assert.IsFalse(manager.IsConnected);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void NetworkManager_HasMaxPlayerLimit()
    {
        // Arrange
        var go = new GameObject("NetworkManager");
        var manager = go.AddComponent<NetworkGameManager>();

        // Assert
        Assert.AreEqual(4, manager.MaxPlayers);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void ConnectionState_HasAllStates()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(ConnectionState), "Disconnected"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ConnectionState), "Connecting"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ConnectionState), "Connected"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ConnectionState), "Hosting"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ConnectionState), "Reconnecting"));
    }

    #endregion

    #region LobbyData Tests

    [Test]
    public void LobbyData_CanBeCreated()
    {
        // Act
        var lobby = new LobbyData();

        // Assert
        Assert.IsNotNull(lobby);
    }

    [Test]
    public void LobbyData_HasRequiredFields()
    {
        // Arrange
        var lobby = new LobbyData
        {
            lobbyId = "LOBBY001",
            lobbyName = "Test Lobby",
            hostPlayerId = "HOST001",
            maxPlayers = 4,
            isPublic = true
        };

        // Assert
        Assert.AreEqual("LOBBY001", lobby.lobbyId);
        Assert.AreEqual("Test Lobby", lobby.lobbyName);
        Assert.AreEqual(4, lobby.maxPlayers);
        Assert.IsTrue(lobby.isPublic);
    }

    [Test]
    public void LobbyData_TracksPlayers()
    {
        // Arrange
        var lobby = new LobbyData();
        lobby.players = new System.Collections.Generic.List<LobbyPlayer>
        {
            new LobbyPlayer { playerId = "P1", playerName = "Player1" },
            new LobbyPlayer { playerId = "P2", playerName = "Player2" }
        };

        // Assert
        Assert.AreEqual(2, lobby.PlayerCount);
    }

    #endregion

    #region LobbyManager Tests

    [Test]
    public void LobbyManager_CanBeCreated()
    {
        // Reset singleton
        var instanceField = typeof(LobbyManager).GetField("_instance",
            BindingFlags.NonPublic | BindingFlags.Static);
        instanceField?.SetValue(null, null);

        // Arrange
        var go = new GameObject("LobbyManager");
        var manager = go.AddComponent<LobbyManager>();

        var awakeMethod = typeof(LobbyManager).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(manager, null);

        // Assert
        Assert.IsNotNull(manager);

        // Cleanup
        instanceField?.SetValue(null, null);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void LobbyManager_CanCreateLobby()
    {
        // Reset singleton
        var instanceField = typeof(LobbyManager).GetField("_instance",
            BindingFlags.NonPublic | BindingFlags.Static);
        instanceField?.SetValue(null, null);

        // Arrange
        var go = new GameObject("LobbyManager");
        var manager = go.AddComponent<LobbyManager>();

        var awakeMethod = typeof(LobbyManager).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(manager, null);

        // Act
        var lobby = manager.CreateLobby("Test Lobby", "Host001", 4, true);

        // Assert
        Assert.IsNotNull(lobby);
        Assert.AreEqual("Test Lobby", lobby.lobbyName);

        // Cleanup
        instanceField?.SetValue(null, null);
        Object.DestroyImmediate(go);
    }

    #endregion

    #region PlayerNetworkData Tests

    [Test]
    public void PlayerNetworkData_CanBeCreated()
    {
        // Act
        var data = new PlayerNetworkData();

        // Assert
        Assert.IsNotNull(data);
    }

    [Test]
    public void PlayerNetworkData_HasSyncFields()
    {
        // Arrange
        var data = new PlayerNetworkData
        {
            playerId = "P001",
            position = new Vector3(10, 0, 20),
            rotation = Quaternion.identity,
            currentHealth = 100,
            isAlive = true
        };

        // Assert
        Assert.AreEqual("P001", data.playerId);
        Assert.AreEqual(new Vector3(10, 0, 20), data.position);
        Assert.AreEqual(100, data.currentHealth);
        Assert.IsTrue(data.isAlive);
    }

    #endregion

    #region NetworkSyncManager Tests

    [Test]
    public void NetworkSyncManager_CanBeCreated()
    {
        // Reset singleton
        var instanceField = typeof(NetworkSyncManager).GetField("_instance",
            BindingFlags.NonPublic | BindingFlags.Static);
        instanceField?.SetValue(null, null);

        // Arrange
        var go = new GameObject("NetworkSyncManager");
        var manager = go.AddComponent<NetworkSyncManager>();

        var awakeMethod = typeof(NetworkSyncManager).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(manager, null);

        // Assert
        Assert.IsNotNull(manager);

        // Cleanup
        instanceField?.SetValue(null, null);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void SyncType_HasAllTypes()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(SyncType), "Player"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(SyncType), "Creature"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(SyncType), "Enemy"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(SyncType), "Projectile"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(SyncType), "Building"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(SyncType), "WorldState"));
    }

    #endregion

    #region PartyManager Tests

    [Test]
    public void PartyManager_CanBeCreated()
    {
        // Reset singleton
        var instanceField = typeof(PartyManager).GetField("_instance",
            BindingFlags.NonPublic | BindingFlags.Static);
        instanceField?.SetValue(null, null);

        // Arrange
        var go = new GameObject("PartyManager");
        var manager = go.AddComponent<PartyManager>();

        var awakeMethod = typeof(PartyManager).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(manager, null);

        // Assert
        Assert.IsNotNull(manager);

        // Cleanup
        instanceField?.SetValue(null, null);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void PartyManager_HasMaxFourPlayers()
    {
        // Reset singleton
        var instanceField = typeof(PartyManager).GetField("_instance",
            BindingFlags.NonPublic | BindingFlags.Static);
        instanceField?.SetValue(null, null);

        // Arrange
        var go = new GameObject("PartyManager");
        var manager = go.AddComponent<PartyManager>();

        var awakeMethod = typeof(PartyManager).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(manager, null);

        // Assert
        Assert.AreEqual(4, manager.MaxPartySize);

        // Cleanup
        instanceField?.SetValue(null, null);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void PartyManager_CanCreateParty()
    {
        // Reset singleton
        var instanceField = typeof(PartyManager).GetField("_instance",
            BindingFlags.NonPublic | BindingFlags.Static);
        instanceField?.SetValue(null, null);

        // Arrange
        var go = new GameObject("PartyManager");
        var manager = go.AddComponent<PartyManager>();

        var awakeMethod = typeof(PartyManager).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(manager, null);

        // Act
        bool result = manager.CreateParty("Leader001");

        // Assert
        Assert.IsTrue(result);
        Assert.IsTrue(manager.IsInParty);

        // Cleanup
        instanceField?.SetValue(null, null);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void PartyRole_HasAllRoles()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(PartyRole), "Leader"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(PartyRole), "Member"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(PartyRole), "Tank"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(PartyRole), "Healer"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(PartyRole), "DPS"));
    }

    [Test]
    public void XPShareMode_HasAllModes()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(XPShareMode), "Equal"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(XPShareMode), "Contribution"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(XPShareMode), "Proximity"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(XPShareMode), "NoShare"));
    }

    #endregion

    #region PartyData Tests

    [Test]
    public void PartyData_CanBeCreated()
    {
        // Act
        var party = new PartyData();

        // Assert
        Assert.IsNotNull(party);
    }

    [Test]
    public void PartyData_HasMembers()
    {
        // Arrange
        var party = new PartyData();
        party.members = new System.Collections.Generic.List<PartyMember>
        {
            new PartyMember { playerId = "P1", role = PartyRole.Leader },
            new PartyMember { playerId = "P2", role = PartyRole.Member }
        };

        // Assert
        Assert.AreEqual(2, party.MemberCount);
    }

    [Test]
    public void PartyBuff_CanBeCreated()
    {
        // Arrange
        var buff = new PartyBuff
        {
            buffId = "BUFF001",
            buffName = "Party Power",
            statType = StatType.Strength,
            bonusValue = 10f,
            isPercentage = false
        };

        // Assert
        Assert.AreEqual("BUFF001", buff.buffId);
        Assert.AreEqual(10f, buff.bonusValue);
    }

    #endregion

    #region LootManager Tests

    [Test]
    public void LootManager_CanBeCreated()
    {
        // Reset singleton
        var instanceField = typeof(LootManager).GetField("_instance",
            BindingFlags.NonPublic | BindingFlags.Static);
        instanceField?.SetValue(null, null);

        // Arrange
        var go = new GameObject("LootManager");
        var manager = go.AddComponent<LootManager>();

        var awakeMethod = typeof(LootManager).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(manager, null);

        // Assert
        Assert.IsNotNull(manager);

        // Cleanup
        instanceField?.SetValue(null, null);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void LootMode_HasAllModes()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(LootMode), "FreeForAll"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(LootMode), "RoundRobin"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(LootMode), "NeedGreed"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(LootMode), "PersonalLoot"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(LootMode), "MasterLooter"));
    }

    [Test]
    public void LootManager_DefaultModeIsPersonalLoot()
    {
        // Reset singleton
        var instanceField = typeof(LootManager).GetField("_instance",
            BindingFlags.NonPublic | BindingFlags.Static);
        instanceField?.SetValue(null, null);

        // Arrange
        var go = new GameObject("LootManager");
        var manager = go.AddComponent<LootManager>();

        var awakeMethod = typeof(LootManager).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(manager, null);

        // Assert
        Assert.AreEqual(LootMode.PersonalLoot, manager.CurrentLootMode);

        // Cleanup
        instanceField?.SetValue(null, null);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void LootRoll_HasAllTypes()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(LootRollType), "Need"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(LootRollType), "Greed"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(LootRollType), "Pass"));
    }

    #endregion

    #region TradeSystem Tests

    [Test]
    public void TradeSession_CanBeCreated()
    {
        // Act
        var trade = new TradeSession();

        // Assert
        Assert.IsNotNull(trade);
    }

    [Test]
    public void TradeSession_HasRequiredFields()
    {
        // Arrange
        var trade = new TradeSession
        {
            tradeId = "TRADE001",
            initiatorId = "P1",
            targetId = "P2",
            state = TradeState.Pending
        };

        // Assert
        Assert.AreEqual("TRADE001", trade.tradeId);
        Assert.AreEqual("P1", trade.initiatorId);
        Assert.AreEqual(TradeState.Pending, trade.state);
    }

    [Test]
    public void TradeState_HasAllStates()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(TradeState), "Pending"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(TradeState), "Active"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(TradeState), "Confirmed"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(TradeState), "Completed"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(TradeState), "Cancelled"));
    }

    #endregion

    #region SharedBaseManager Tests

    [Test]
    public void SharedBaseManager_CanBeCreated()
    {
        // Reset singleton
        var instanceField = typeof(SharedBaseManager).GetField("_instance",
            BindingFlags.NonPublic | BindingFlags.Static);
        instanceField?.SetValue(null, null);

        // Arrange
        var go = new GameObject("SharedBaseManager");
        var manager = go.AddComponent<SharedBaseManager>();

        var awakeMethod = typeof(SharedBaseManager).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(manager, null);

        // Assert
        Assert.IsNotNull(manager);

        // Cleanup
        instanceField?.SetValue(null, null);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void BuildPermission_HasAllLevels()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(BuildPermission), "None"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(BuildPermission), "View"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(BuildPermission), "Interact"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(BuildPermission), "Build"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(BuildPermission), "Admin"));
    }

    [Test]
    public void CreatureLendData_CanBeCreated()
    {
        // Arrange
        var lend = new CreatureLendData
        {
            lendId = "LEND001",
            ownerId = "P1",
            borrowerId = "P2",
            creatureId = "C001",
            durationHours = 24
        };

        // Assert
        Assert.AreEqual("LEND001", lend.lendId);
        Assert.AreEqual("P1", lend.ownerId);
        Assert.AreEqual(24, lend.durationHours);
    }

    #endregion

    #region CoopDefenseWave Tests

    [Test]
    public void CoopDefenseWave_CanBeCreated()
    {
        // Act
        var wave = new CoopDefenseWave();

        // Assert
        Assert.IsNotNull(wave);
    }

    [Test]
    public void CoopDefenseWave_HasRequiredFields()
    {
        // Arrange
        var wave = new CoopDefenseWave
        {
            waveNumber = 1,
            playerScaling = true,
            baseEnemyCount = 10,
            enemyCountPerPlayer = 5
        };

        // Assert
        Assert.AreEqual(1, wave.waveNumber);
        Assert.IsTrue(wave.playerScaling);
        Assert.AreEqual(10, wave.baseEnemyCount);
    }

    [Test]
    public void CoopDefenseWave_CalculatesEnemyCount()
    {
        // Arrange
        var wave = new CoopDefenseWave
        {
            baseEnemyCount = 10,
            enemyCountPerPlayer = 5,
            playerScaling = true
        };

        // Act
        int enemies = wave.GetTotalEnemies(3);

        // Assert - 10 + (5 * 3) = 25
        Assert.AreEqual(25, enemies);
    }

    #endregion

    #region RaidData Tests

    [Test]
    public void RaidData_CanBeCreated()
    {
        // Act
        var data = ScriptableObject.CreateInstance<RaidData>();

        // Assert
        Assert.IsNotNull(data);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void RaidData_HasRequiredFields()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<RaidData>();
        data.raidId = "RAID001";
        data.raidName = "Dragon's Lair";
        data.requiredPlayers = 4;
        data.recommendedLevel = 50;

        // Assert
        Assert.AreEqual("RAID001", data.raidId);
        Assert.AreEqual("Dragon's Lair", data.raidName);
        Assert.AreEqual(4, data.requiredPlayers);
        Assert.AreEqual(50, data.recommendedLevel);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void RaidDifficulty_HasAllDifficulties()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(RaidDifficulty), "Normal"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(RaidDifficulty), "Heroic"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(RaidDifficulty), "Mythic"));
    }

    [Test]
    public void RaidData_HasBossPhases()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<RaidData>();
        data.bosses = new RaidBossData[]
        {
            new RaidBossData
            {
                bossName = "Dragon",
                phases = new RaidBossPhase[]
                {
                    new RaidBossPhase { phaseName = "Phase 1", healthThreshold = 1f },
                    new RaidBossPhase { phaseName = "Phase 2", healthThreshold = 0.5f }
                }
            }
        };

        // Assert
        Assert.AreEqual(1, data.bosses.Length);
        Assert.AreEqual(2, data.bosses[0].phases.Length);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    #endregion

    #region RaidManager Tests

    [Test]
    public void RaidManager_CanBeCreated()
    {
        // Reset singleton
        var instanceField = typeof(RaidManager).GetField("_instance",
            BindingFlags.NonPublic | BindingFlags.Static);
        instanceField?.SetValue(null, null);

        // Arrange
        var go = new GameObject("RaidManager");
        var manager = go.AddComponent<RaidManager>();

        var awakeMethod = typeof(RaidManager).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(manager, null);

        // Assert
        Assert.IsNotNull(manager);

        // Cleanup
        instanceField?.SetValue(null, null);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void RaidLockout_CanBeCreated()
    {
        // Arrange
        var lockout = new RaidLockout
        {
            raidId = "RAID001",
            difficulty = RaidDifficulty.Normal,
            completedBosses = new System.Collections.Generic.List<string> { "Boss1" },
            expiresAt = System.DateTime.UtcNow.AddDays(7)
        };

        // Assert
        Assert.AreEqual("RAID001", lockout.raidId);
        Assert.AreEqual(1, lockout.completedBosses.Count);
        Assert.IsFalse(lockout.IsExpired);
    }

    #endregion

    #region NetworkMessage Tests

    [Test]
    public void NetworkMessageType_HasAllTypes()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(NetworkMessageType), "PlayerJoin"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(NetworkMessageType), "PlayerLeave"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(NetworkMessageType), "PlayerSync"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(NetworkMessageType), "Chat"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(NetworkMessageType), "WorldEvent"));
    }

    #endregion

    #region ChatSystem Tests

    [Test]
    public void ChatMessage_CanBeCreated()
    {
        // Arrange
        var msg = new ChatMessage
        {
            senderId = "P1",
            senderName = "Player1",
            message = "Hello!",
            channel = ChatChannel.Party,
            timestamp = System.DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual("P1", msg.senderId);
        Assert.AreEqual("Hello!", msg.message);
        Assert.AreEqual(ChatChannel.Party, msg.channel);
    }

    [Test]
    public void ChatChannel_HasAllChannels()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(ChatChannel), "Global"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ChatChannel), "Party"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ChatChannel), "Whisper"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ChatChannel), "System"));
    }

    #endregion
}
