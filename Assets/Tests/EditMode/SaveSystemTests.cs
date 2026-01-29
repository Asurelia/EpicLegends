using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Tests unitaires pour le système de sauvegarde.
/// </summary>
public class SaveSystemTests
{
    private string _testSavePath;

    [SetUp]
    public void Setup()
    {
        // Utiliser un dossier temporaire pour les tests
        _testSavePath = Path.Combine(Application.temporaryCachePath, "TestSaves");
        if (!Directory.Exists(_testSavePath))
        {
            Directory.CreateDirectory(_testSavePath);
        }
    }

    [TearDown]
    public void Teardown()
    {
        // Nettoyer les fichiers de test
        if (Directory.Exists(_testSavePath))
        {
            Directory.Delete(_testSavePath, true);
        }
    }

    #region SaveData Tests

    [Test]
    public void SaveData_Constructor_InitializesWithDefaults()
    {
        // Act
        var saveData = new SaveData();

        // Assert
        Assert.IsNotNull(saveData);
        Assert.IsNotNull(saveData.playerData);
        Assert.IsNotNull(saveData.inventoryData);
        Assert.IsNotNull(saveData.gameProgress);
    }

    [Test]
    public void SaveData_Version_IsSet()
    {
        // Act
        var saveData = new SaveData();

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(saveData.version));
    }

    [Test]
    public void SaveData_Timestamp_IsSet()
    {
        // Act
        var saveData = new SaveData();

        // Assert
        Assert.Greater(saveData.timestamp, 0);
    }

    #endregion

    #region PlayerSaveData Tests

    [Test]
    public void PlayerSaveData_StoresPosition()
    {
        // Arrange
        var playerData = new PlayerSaveData();
        var position = new Vector3(10f, 5f, 20f);

        // Act
        playerData.positionX = position.x;
        playerData.positionY = position.y;
        playerData.positionZ = position.z;

        // Assert
        Assert.AreEqual(10f, playerData.positionX);
        Assert.AreEqual(5f, playerData.positionY);
        Assert.AreEqual(20f, playerData.positionZ);
    }

    [Test]
    public void PlayerSaveData_StoresStats()
    {
        // Arrange
        var playerData = new PlayerSaveData();

        // Act
        playerData.level = 10;
        playerData.experience = 5000;
        playerData.currentHealth = 80f;
        playerData.currentMana = 50f;

        // Assert
        Assert.AreEqual(10, playerData.level);
        Assert.AreEqual(5000, playerData.experience);
        Assert.AreEqual(80f, playerData.currentHealth);
        Assert.AreEqual(50f, playerData.currentMana);
    }

    [Test]
    public void PlayerSaveData_StoresBaseStats()
    {
        // Arrange
        var playerData = new PlayerSaveData();
        playerData.baseStats = new Dictionary<StatType, float>
        {
            { StatType.Strength, 15f },
            { StatType.Dexterity, 12f },
            { StatType.Intelligence, 10f }
        };

        // Assert
        Assert.AreEqual(15f, playerData.baseStats[StatType.Strength]);
        Assert.AreEqual(12f, playerData.baseStats[StatType.Dexterity]);
        Assert.AreEqual(10f, playerData.baseStats[StatType.Intelligence]);
    }

    #endregion

    #region InventorySaveData Tests

    [Test]
    public void InventorySaveData_StoresItems()
    {
        // Arrange
        var inventoryData = new InventorySaveData();
        inventoryData.items = new List<ItemSaveData>
        {
            new ItemSaveData { itemId = "sword_iron", quantity = 1, slotIndex = 0 },
            new ItemSaveData { itemId = "potion_health", quantity = 5, slotIndex = 1 }
        };

        // Assert
        Assert.AreEqual(2, inventoryData.items.Count);
        Assert.AreEqual("sword_iron", inventoryData.items[0].itemId);
        Assert.AreEqual(5, inventoryData.items[1].quantity);
    }

    [Test]
    public void InventorySaveData_StoresGold()
    {
        // Arrange
        var inventoryData = new InventorySaveData();

        // Act
        inventoryData.gold = 1500;

        // Assert
        Assert.AreEqual(1500, inventoryData.gold);
    }

    #endregion

    #region GameProgressData Tests

    [Test]
    public void GameProgressData_StoresCurrentScene()
    {
        // Arrange
        var progress = new GameProgressData();

        // Act
        progress.currentSceneName = "Village";

        // Assert
        Assert.AreEqual("Village", progress.currentSceneName);
    }

    [Test]
    public void GameProgressData_StoresPlayTime()
    {
        // Arrange
        var progress = new GameProgressData();

        // Act
        progress.totalPlayTimeSeconds = 3600f;

        // Assert
        Assert.AreEqual(3600f, progress.totalPlayTimeSeconds);
    }

    [Test]
    public void GameProgressData_StoresCompletedQuests()
    {
        // Arrange
        var progress = new GameProgressData();
        progress.completedQuestIds = new List<string> { "quest_001", "quest_002" };

        // Assert
        Assert.AreEqual(2, progress.completedQuestIds.Count);
        Assert.Contains("quest_001", progress.completedQuestIds);
    }

    [Test]
    public void GameProgressData_StoresUnlockedAreas()
    {
        // Arrange
        var progress = new GameProgressData();
        progress.unlockedAreas = new List<string> { "Village", "Forest", "Cave" };

        // Assert
        Assert.AreEqual(3, progress.unlockedAreas.Count);
    }

    #endregion

    #region SaveManager Tests

    [Test]
    public void SaveManager_SerializeSaveData_ReturnsJson()
    {
        // Arrange
        var saveData = new SaveData();
        saveData.playerData.level = 5;

        // Act
        string json = SaveManager.SerializeToJson(saveData);

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(json));
        Assert.IsTrue(json.Contains("level"));
    }

    [Test]
    public void SaveManager_DeserializeSaveData_RestoresData()
    {
        // Arrange
        var originalData = new SaveData();
        originalData.playerData.level = 15;
        originalData.playerData.experience = 12345;
        originalData.inventoryData.gold = 9999;
        string json = SaveManager.SerializeToJson(originalData);

        // Act
        var restoredData = SaveManager.DeserializeFromJson(json);

        // Assert
        Assert.IsNotNull(restoredData);
        Assert.AreEqual(15, restoredData.playerData.level);
        Assert.AreEqual(12345, restoredData.playerData.experience);
        Assert.AreEqual(9999, restoredData.inventoryData.gold);
    }

    [Test]
    public void SaveManager_SaveToFile_CreatesFile()
    {
        // Arrange
        var saveData = new SaveData();
        string filePath = Path.Combine(_testSavePath, "test_save.json");

        // Act
        bool success = SaveManager.SaveToFile(saveData, filePath);

        // Assert
        Assert.IsTrue(success);
        Assert.IsTrue(File.Exists(filePath));
    }

    [Test]
    public void SaveManager_LoadFromFile_RestoresData()
    {
        // Arrange
        var originalData = new SaveData();
        originalData.playerData.level = 20;
        originalData.playerData.positionX = 100f;
        string filePath = Path.Combine(_testSavePath, "test_load.json");
        SaveManager.SaveToFile(originalData, filePath);

        // Act
        var loadedData = SaveManager.LoadFromFile(filePath);

        // Assert
        Assert.IsNotNull(loadedData);
        Assert.AreEqual(20, loadedData.playerData.level);
        Assert.AreEqual(100f, loadedData.playerData.positionX);
    }

    [Test]
    public void SaveManager_LoadFromFile_ReturnsNull_WhenFileNotExists()
    {
        // Arrange
        string filePath = Path.Combine(_testSavePath, "nonexistent.json");

        // Act
        var loadedData = SaveManager.LoadFromFile(filePath);

        // Assert
        Assert.IsNull(loadedData);
    }

    [Test]
    public void SaveManager_DeleteSave_RemovesFile()
    {
        // Arrange
        var saveData = new SaveData();
        string filePath = Path.Combine(_testSavePath, "test_delete.json");
        SaveManager.SaveToFile(saveData, filePath);
        Assert.IsTrue(File.Exists(filePath));

        // Act
        bool success = SaveManager.DeleteSave(filePath);

        // Assert
        Assert.IsTrue(success);
        Assert.IsFalse(File.Exists(filePath));
    }

    [Test]
    public void SaveManager_GetSaveSlotPath_ReturnsValidPath()
    {
        // Act
        string path = SaveManager.GetSaveSlotPath(1);

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(path));
        Assert.IsTrue(path.Contains("save_slot_1"));
    }

    [Test]
    public void SaveManager_SaveExists_ReturnsTrue_WhenFileExists()
    {
        // Arrange
        var saveData = new SaveData();
        string filePath = Path.Combine(_testSavePath, "exists_test.json");
        SaveManager.SaveToFile(saveData, filePath);

        // Act
        bool exists = SaveManager.SaveExists(filePath);

        // Assert
        Assert.IsTrue(exists);
    }

    [Test]
    public void SaveManager_SaveExists_ReturnsFalse_WhenFileNotExists()
    {
        // Arrange
        string filePath = Path.Combine(_testSavePath, "not_exists.json");

        // Act
        bool exists = SaveManager.SaveExists(filePath);

        // Assert
        Assert.IsFalse(exists);
    }

    #endregion

    #region SaveSlotInfo Tests

    [Test]
    public void SaveSlotInfo_FromSaveData_ExtractsInfo()
    {
        // Arrange
        var saveData = new SaveData();
        saveData.playerData.level = 25;
        saveData.gameProgress.totalPlayTimeSeconds = 7200f;
        saveData.gameProgress.currentSceneName = "Donjon";

        // Act
        var slotInfo = SaveSlotInfo.FromSaveData(saveData, 1);

        // Assert
        Assert.AreEqual(1, slotInfo.slotIndex);
        Assert.AreEqual(25, slotInfo.playerLevel);
        Assert.AreEqual(7200f, slotInfo.playTimeSeconds);
        Assert.AreEqual("Donjon", slotInfo.locationName);
    }

    [Test]
    public void SaveSlotInfo_GetFormattedPlayTime_FormatsCorrectly()
    {
        // Arrange
        var slotInfo = new SaveSlotInfo { playTimeSeconds = 3661f }; // 1h 1m 1s

        // Act
        string formatted = slotInfo.GetFormattedPlayTime();

        // Assert
        Assert.AreEqual("01:01:01", formatted);
    }

    #endregion

    #region Encryption Tests

    [Test]
    public void SaveManager_EncryptedSave_CanBeDecrypted()
    {
        // Arrange
        var saveData = new SaveData();
        saveData.playerData.level = 30;
        saveData.inventoryData.gold = 50000;
        string filePath = Path.Combine(_testSavePath, "encrypted_test.sav");

        // Act
        bool saved = SaveManager.SaveToFile(saveData, filePath, encrypt: true);
        var loadedData = SaveManager.LoadFromFile(filePath, encrypted: true);

        // Assert
        Assert.IsTrue(saved);
        Assert.IsNotNull(loadedData);
        Assert.AreEqual(30, loadedData.playerData.level);
        Assert.AreEqual(50000, loadedData.inventoryData.gold);
    }

    [Test]
    public void SaveManager_EncryptedFile_IsNotReadableAsPlainText()
    {
        // Arrange
        var saveData = new SaveData();
        saveData.playerData.level = 99;
        string filePath = Path.Combine(_testSavePath, "encrypted_check.sav");
        SaveManager.SaveToFile(saveData, filePath, encrypt: true);

        // Act
        string fileContent = File.ReadAllText(filePath);

        // Assert - Le contenu ne devrait pas etre du JSON lisible
        Assert.IsFalse(fileContent.StartsWith("{"));
        Assert.IsFalse(fileContent.Contains("\"level\":99"));
    }

    #endregion

    #region Backup Tests

    [Test]
    public void SaveManager_CreateBackup_CreatesBackupFile()
    {
        // Arrange
        var saveData = new SaveData();
        string filePath = Path.Combine(_testSavePath, "backup_test.json");
        SaveManager.SaveToFile(saveData, filePath);

        // Act
        string backupPath = SaveManager.CreateBackup(filePath);

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(backupPath));
        Assert.IsTrue(File.Exists(backupPath));
        Assert.IsTrue(backupPath.Contains(".backup"));
    }

    #endregion
}
