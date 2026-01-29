using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests unitaires pour le systeme de progression.
/// Phase 4: Leveling, Classes, Equipment, Quests, etc.
/// </summary>
public class ProgressionSystemTests
{
    #region LevelSystem Tests

    [Test]
    public void LevelSystem_CanBeCreated()
    {
        // Arrange
        var go = new GameObject("LevelSystem");
        var system = go.AddComponent<LevelSystem>();

        // Assert
        Assert.IsNotNull(system);
        Assert.AreEqual(1, system.CurrentLevel);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void LevelSystem_StartsAtLevelOne()
    {
        // Arrange
        var go = new GameObject("LevelSystem");
        var system = go.AddComponent<LevelSystem>();

        // Assert
        Assert.AreEqual(1, system.CurrentLevel);
        Assert.AreEqual(0, system.CurrentXP);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void LevelSystem_CanGainXP()
    {
        // Arrange
        var go = new GameObject("LevelSystem");
        var system = go.AddComponent<LevelSystem>();

        // Act
        system.AddXP(50);

        // Assert
        Assert.AreEqual(50, system.CurrentXP);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void LevelSystem_LevelsUpWhenXPThresholdReached()
    {
        // Arrange
        var go = new GameObject("LevelSystem");
        var system = go.AddComponent<LevelSystem>();
        int xpForLevel2 = system.GetXPForLevel(2);

        // Act
        system.AddXP(xpForLevel2);

        // Assert
        Assert.AreEqual(2, system.CurrentLevel);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void LevelSystem_MaxLevelIs100()
    {
        // Arrange
        var go = new GameObject("LevelSystem");
        var system = go.AddComponent<LevelSystem>();

        // Assert
        Assert.AreEqual(100, LevelSystem.MAX_LEVEL);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void LevelSystem_CannotExceedMaxLevel()
    {
        // Arrange
        var go = new GameObject("LevelSystem");
        var system = go.AddComponent<LevelSystem>();

        // Appeler Awake manuellement (non appele en EditMode)
        var awakeMethod = typeof(LevelSystem).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(system, null);

        // Act - Utiliser SetLevel pour atteindre le max puis ajouter XP
        system.SetLevel(LevelSystem.MAX_LEVEL, false);
        system.AddXP(10000); // XP supplementaire ne devrait rien faire

        // Assert
        Assert.AreEqual(LevelSystem.MAX_LEVEL, system.CurrentLevel);
        Assert.IsTrue(system.IsMaxLevel);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void LevelSystem_XPCurveIsExponential()
    {
        // Arrange
        var go = new GameObject("LevelSystem");
        var system = go.AddComponent<LevelSystem>();

        // Act
        int xp2 = system.GetXPForLevel(2);
        int xp10 = system.GetXPForLevel(10);
        int xp50 = system.GetXPForLevel(50);

        // Assert - XP should increase exponentially
        Assert.Greater(xp10, xp2 * 2);
        Assert.Greater(xp50, xp10 * 2);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void LevelSystem_AwardsStatPointsOnLevelUp()
    {
        // Arrange
        var go = new GameObject("LevelSystem");
        var system = go.AddComponent<LevelSystem>();
        int initialStatPoints = system.AvailableStatPoints;

        // Act
        int xpForLevel2 = system.GetXPForLevel(2);
        system.AddXP(xpForLevel2);

        // Assert
        Assert.Greater(system.AvailableStatPoints, initialStatPoints);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void LevelSystem_AwardsSkillPointsOnLevelUp()
    {
        // Arrange
        var go = new GameObject("LevelSystem");
        var system = go.AddComponent<LevelSystem>();
        int initialSkillPoints = system.AvailableSkillPoints;

        // Act
        int xpForLevel2 = system.GetXPForLevel(2);
        system.AddXP(xpForLevel2);

        // Assert
        Assert.Greater(system.AvailableSkillPoints, initialSkillPoints);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void LevelSystem_CanAllocateStatPoints()
    {
        // Arrange
        var go = new GameObject("LevelSystem");
        var system = go.AddComponent<LevelSystem>();

        // Appeler Awake manuellement (non appele en EditMode)
        var awakeMethod = typeof(LevelSystem).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(system, null);

        int xpForLevel2 = system.GetXPForLevel(2);
        system.AddXP(xpForLevel2);
        int points = system.AvailableStatPoints;

        // Act
        bool result = system.AllocateStatPoint(StatType.Strength);

        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(points - 1, system.AvailableStatPoints);
        Assert.AreEqual(1, system.GetAllocatedPoints(StatType.Strength));

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void LevelSystem_CannotAllocateWithNoPoints()
    {
        // Arrange
        var go = new GameObject("LevelSystem");
        var system = go.AddComponent<LevelSystem>();

        // Act - No points at level 1
        bool result = system.AllocateStatPoint(StatType.Strength);

        // Assert
        Assert.IsFalse(result);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    #endregion

    #region XPSource Tests

    [Test]
    public void XPSource_HasAllTypes()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(XPSource), "Combat"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(XPSource), "Quest"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(XPSource), "Exploration"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(XPSource), "Crafting"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(XPSource), "Discovery"));
    }

    #endregion

    #region StatType Tests

    [Test]
    public void StatType_HasAllTypes()
    {
        // Assert - Primary stats
        Assert.IsTrue(System.Enum.IsDefined(typeof(StatType), "Strength"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(StatType), "Dexterity"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(StatType), "Intelligence"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(StatType), "Vitality"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(StatType), "Wisdom"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(StatType), "Luck"));
    }

    #endregion

    #region LevelScaling Tests

    [Test]
    public void LevelScaling_CalculatesEnemyLevel()
    {
        // Arrange & Act
        int enemyLevel = LevelScaling.GetScaledEnemyLevel(10, 5, 15, 0.5f);

        // Assert - Should be between min and max
        Assert.GreaterOrEqual(enemyLevel, 5);
        Assert.LessOrEqual(enemyLevel, 15);

        // Cleanup
    }

    [Test]
    public void LevelScaling_CalculatesXPReward()
    {
        // Arrange & Act
        int xpBase = 100;
        int xpScaled = LevelScaling.GetScaledXPReward(xpBase, 10, 5);

        // Assert - Higher level player vs lower enemy = less XP
        Assert.Less(xpScaled, xpBase);
    }

    [Test]
    public void LevelScaling_NoPenaltyForSameLevelEnemy()
    {
        // Arrange & Act
        int xpBase = 100;
        int xpScaled = LevelScaling.GetScaledXPReward(xpBase, 10, 10);

        // Assert - Same level = base XP
        Assert.AreEqual(xpBase, xpScaled);
    }

    #endregion

    #region ClassData Tests

    [Test]
    public void ClassData_CanBeCreated()
    {
        // Act
        var data = ScriptableObject.CreateInstance<ClassData>();

        // Assert
        Assert.IsNotNull(data);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void ClassData_HasRequiredFields()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<ClassData>();
        data.className = "Warrior";
        data.classType = ClassType.Warrior;
        data.tier = ClassTier.Base;

        // Assert
        Assert.AreEqual("Warrior", data.className);
        Assert.AreEqual(ClassType.Warrior, data.classType);
        Assert.AreEqual(ClassTier.Base, data.tier);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void ClassType_HasAllBaseClasses()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(ClassType), "Warrior"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ClassType), "Mage"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ClassType), "Rogue"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ClassType), "Ranger"));
    }

    [Test]
    public void ClassTier_HasAllTiers()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(ClassTier), "Base"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ClassTier), "Advanced"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ClassTier), "Master"));
    }

    [Test]
    public void ClassData_HasStatBonuses()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<ClassData>();
        data.statBonuses = new StatBonus[]
        {
            new StatBonus { statType = StatType.Strength, bonusPerLevel = 2f }
        };

        // Assert
        Assert.AreEqual(1, data.statBonuses.Length);
        Assert.AreEqual(StatType.Strength, data.statBonuses[0].statType);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    #endregion

    #region ClassSystem Tests

    [Test]
    public void ClassSystem_CanBeCreated()
    {
        // Arrange
        var go = new GameObject("ClassSystem");
        var system = go.AddComponent<ClassSystem>();

        // Assert
        Assert.IsNotNull(system);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void ClassSystem_CanSetClass()
    {
        // Arrange
        var go = new GameObject("ClassSystem");
        var system = go.AddComponent<ClassSystem>();
        var classData = ScriptableObject.CreateInstance<ClassData>();
        classData.className = "Warrior";
        classData.classType = ClassType.Warrior;

        // Act
        bool result = system.SetClass(classData);

        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(classData, system.CurrentClass);

        // Cleanup
        Object.DestroyImmediate(classData);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void ClassSystem_TracksClassLevel()
    {
        // Arrange
        var go = new GameObject("ClassSystem");
        var system = go.AddComponent<ClassSystem>();
        var classData = ScriptableObject.CreateInstance<ClassData>();
        classData.classType = ClassType.Warrior;
        system.SetClass(classData);

        // Act
        system.AddClassXP(1000);

        // Assert
        Assert.GreaterOrEqual(system.CurrentClassLevel, 1);

        // Cleanup
        Object.DestroyImmediate(classData);
        Object.DestroyImmediate(go);
    }

    #endregion

    #region EquipmentSlot Tests

    [Test]
    public void EquipmentSlot_HasAllSlots()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(EquipmentSlot), "Head"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(EquipmentSlot), "Body"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(EquipmentSlot), "Hands"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(EquipmentSlot), "Legs"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(EquipmentSlot), "Feet"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(EquipmentSlot), "Accessory1"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(EquipmentSlot), "Accessory2"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(EquipmentSlot), "MainHand"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(EquipmentSlot), "OffHand"));
    }

    #endregion

    #region EquipmentData Tests

    [Test]
    public void EquipmentData_CanBeCreated()
    {
        // Act
        var data = ScriptableObject.CreateInstance<EquipmentData>();

        // Assert
        Assert.IsNotNull(data);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void EquipmentData_HasRequiredFields()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<EquipmentData>();
        data.equipmentName = "Iron Sword";
        data.slot = EquipmentSlot.MainHand;
        data.rarity = EquipmentRarity.Common;
        data.requiredLevel = 5;

        // Assert
        Assert.AreEqual("Iron Sword", data.equipmentName);
        Assert.AreEqual(EquipmentSlot.MainHand, data.slot);
        Assert.AreEqual(EquipmentRarity.Common, data.rarity);
        Assert.AreEqual(5, data.requiredLevel);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void EquipmentRarity_HasAllRarities()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(EquipmentRarity), "Common"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(EquipmentRarity), "Uncommon"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(EquipmentRarity), "Rare"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(EquipmentRarity), "Epic"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(EquipmentRarity), "Legendary"));
    }

    [Test]
    public void EquipmentData_CanHaveRandomStats()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<EquipmentData>();
        data.randomStatSlots = 3;
        data.possibleRandomStats = new RandomStatDefinition[]
        {
            new RandomStatDefinition { statType = StatType.Strength, minValue = 1, maxValue = 10 }
        };

        // Assert
        Assert.AreEqual(3, data.randomStatSlots);
        Assert.AreEqual(1, data.possibleRandomStats.Length);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void EquipmentData_CanHaveSockets()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<EquipmentData>();
        data.socketCount = 2;

        // Assert
        Assert.AreEqual(2, data.socketCount);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    #endregion

    #region EquipmentInstance Tests

    [Test]
    public void EquipmentInstance_CanBeCreated()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<EquipmentData>();
        data.equipmentName = "Test Sword";

        // Act
        var instance = new EquipmentInstance(data);

        // Assert
        Assert.IsNotNull(instance);
        Assert.AreEqual(data, instance.BaseData);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void EquipmentInstance_GeneratesRandomStats()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<EquipmentData>();
        data.randomStatSlots = 2;
        data.possibleRandomStats = new RandomStatDefinition[]
        {
            new RandomStatDefinition { statType = StatType.Strength, minValue = 1, maxValue = 10 },
            new RandomStatDefinition { statType = StatType.Dexterity, minValue = 1, maxValue = 10 }
        };

        // Act
        var instance = new EquipmentInstance(data);

        // Assert
        Assert.IsNotNull(instance.RandomStats);
        Assert.LessOrEqual(instance.RandomStats.Count, data.randomStatSlots);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    #endregion

    #region EquipmentSetData Tests

    [Test]
    public void EquipmentSetData_CanBeCreated()
    {
        // Act
        var data = ScriptableObject.CreateInstance<EquipmentSetData>();

        // Assert
        Assert.IsNotNull(data);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void EquipmentSetData_HasSetBonuses()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<EquipmentSetData>();
        data.setName = "Iron Set";
        data.setBonuses = new SetBonus[]
        {
            new SetBonus { piecesRequired = 2, bonusDescription = "+10% Defense" }
        };

        // Assert
        Assert.AreEqual("Iron Set", data.setName);
        Assert.AreEqual(1, data.setBonuses.Length);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    #endregion

    #region EquipmentManager Tests

    [Test]
    public void EquipmentManager_CanBeCreated()
    {
        // Arrange
        var go = new GameObject("EquipmentManager");
        var manager = go.AddComponent<EquipmentManager>();

        // Assert
        Assert.IsNotNull(manager);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void EquipmentManager_CanEquipItem()
    {
        // Arrange
        var go = new GameObject("EquipmentManager");
        var manager = go.AddComponent<EquipmentManager>();

        // Appeler Awake manuellement (non appele en EditMode)
        var awakeMethod = typeof(EquipmentManager).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(manager, null);

        var data = ScriptableObject.CreateInstance<EquipmentData>();
        data.slot = EquipmentSlot.MainHand;
        var instance = new EquipmentInstance(data);

        // Act
        bool result = manager.Equip(instance);

        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(instance, manager.GetEquipped(EquipmentSlot.MainHand));

        // Cleanup
        Object.DestroyImmediate(data);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void EquipmentManager_CanUnequipItem()
    {
        // Arrange
        var go = new GameObject("EquipmentManager");
        var manager = go.AddComponent<EquipmentManager>();

        // Appeler Awake manuellement (non appele en EditMode)
        var awakeMethod = typeof(EquipmentManager).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(manager, null);

        var data = ScriptableObject.CreateInstance<EquipmentData>();
        data.slot = EquipmentSlot.MainHand;
        var instance = new EquipmentInstance(data);
        manager.Equip(instance);

        // Act
        var unequipped = manager.Unequip(EquipmentSlot.MainHand);

        // Assert
        Assert.AreEqual(instance, unequipped);
        Assert.IsNull(manager.GetEquipped(EquipmentSlot.MainHand));

        // Cleanup
        Object.DestroyImmediate(data);
        Object.DestroyImmediate(go);
    }

    #endregion

    #region QuestData Tests

    [Test]
    public void QuestData_CanBeCreated()
    {
        // Act
        var data = ScriptableObject.CreateInstance<QuestData>();

        // Assert
        Assert.IsNotNull(data);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void QuestData_HasRequiredFields()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<QuestData>();
        data.questId = "Q001";
        data.questName = "First Steps";
        data.questType = QuestType.Main;

        // Assert
        Assert.AreEqual("Q001", data.questId);
        Assert.AreEqual("First Steps", data.questName);
        Assert.AreEqual(QuestType.Main, data.questType);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void QuestType_HasAllTypes()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(QuestType), "Main"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(QuestType), "Side"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(QuestType), "Daily"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(QuestType), "Event"));
    }

    [Test]
    public void QuestObjectiveType_HasAllTypes()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(QuestObjectiveType), "Kill"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(QuestObjectiveType), "Collect"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(QuestObjectiveType), "Escort"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(QuestObjectiveType), "Deliver"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(QuestObjectiveType), "Talk"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(QuestObjectiveType), "Explore"));
    }

    #endregion

    #region QuestManager Tests

    [Test]
    public void QuestManager_CanBeCreated()
    {
        // Reset singleton
        var instanceField = typeof(QuestManager).GetField("_instance",
            BindingFlags.NonPublic | BindingFlags.Static);
        instanceField?.SetValue(null, null);

        // Arrange
        var go = new GameObject("QuestManager");
        var manager = go.AddComponent<QuestManager>();

        var awakeMethod = typeof(QuestManager).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(manager, null);

        // Assert
        Assert.IsNotNull(manager);

        // Cleanup
        instanceField?.SetValue(null, null);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void QuestManager_CanAcceptQuest()
    {
        // Reset singleton
        var instanceField = typeof(QuestManager).GetField("_instance",
            BindingFlags.NonPublic | BindingFlags.Static);
        instanceField?.SetValue(null, null);

        // Arrange
        var go = new GameObject("QuestManager");
        var manager = go.AddComponent<QuestManager>();

        var awakeMethod = typeof(QuestManager).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(manager, null);

        var quest = ScriptableObject.CreateInstance<QuestData>();
        quest.questId = "Q001";

        // Act
        bool result = manager.AcceptQuest(quest);

        // Assert
        Assert.IsTrue(result);
        Assert.IsTrue(manager.IsQuestActive("Q001"));

        // Cleanup
        instanceField?.SetValue(null, null);
        Object.DestroyImmediate(quest);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void QuestManager_TracksActiveQuests()
    {
        // Reset singleton
        var instanceField = typeof(QuestManager).GetField("_instance",
            BindingFlags.NonPublic | BindingFlags.Static);
        instanceField?.SetValue(null, null);

        // Arrange
        var go = new GameObject("QuestManager");
        var manager = go.AddComponent<QuestManager>();

        var awakeMethod = typeof(QuestManager).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(manager, null);

        var quest = ScriptableObject.CreateInstance<QuestData>();
        quest.questId = "Q001";
        manager.AcceptQuest(quest);

        // Assert
        Assert.AreEqual(1, manager.ActiveQuestCount);

        // Cleanup
        instanceField?.SetValue(null, null);
        Object.DestroyImmediate(quest);
        Object.DestroyImmediate(go);
    }

    #endregion

    #region DialogueData Tests

    [Test]
    public void DialogueData_CanBeCreated()
    {
        // Act
        var data = ScriptableObject.CreateInstance<DialogueData>();

        // Assert
        Assert.IsNotNull(data);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void DialogueData_HasDialogueNodes()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<DialogueData>();
        data.dialogueId = "D001";
        data.nodes = new DialogueNode[]
        {
            new DialogueNode { nodeId = "N1", speakerName = "NPC", dialogueText = "Hello!" }
        };

        // Assert
        Assert.AreEqual("D001", data.dialogueId);
        Assert.AreEqual(1, data.nodes.Length);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void DialogueNode_CanHaveChoices()
    {
        // Arrange
        var node = new DialogueNode
        {
            nodeId = "N1",
            dialogueText = "What do you want?",
            choices = new DialogueChoice[]
            {
                new DialogueChoice { choiceText = "Help me", nextNodeId = "N2" },
                new DialogueChoice { choiceText = "Goodbye", nextNodeId = null }
            }
        };

        // Assert
        Assert.AreEqual(2, node.choices.Length);
        Assert.AreEqual("Help me", node.choices[0].choiceText);
    }

    #endregion

    #region RegionData Tests

    [Test]
    public void RegionData_CanBeCreated()
    {
        // Act
        var data = ScriptableObject.CreateInstance<RegionData>();

        // Assert
        Assert.IsNotNull(data);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void RegionData_HasRequiredFields()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<RegionData>();
        data.regionId = "R001";
        data.regionName = "Starter Valley";
        data.recommendedLevel = 1;

        // Assert
        Assert.AreEqual("R001", data.regionId);
        Assert.AreEqual("Starter Valley", data.regionName);
        Assert.AreEqual(1, data.recommendedLevel);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    #endregion

    #region FastTravelPoint Tests

    [Test]
    public void FastTravelPoint_CanBeCreated()
    {
        // Arrange
        var go = new GameObject("FastTravelPoint");
        var point = go.AddComponent<FastTravelPoint>();

        // Assert
        Assert.IsNotNull(point);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void FastTravelPoint_StartsLocked()
    {
        // Arrange
        var go = new GameObject("FastTravelPoint");
        var point = go.AddComponent<FastTravelPoint>();

        // Assert
        Assert.IsFalse(point.IsUnlocked);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void FastTravelPoint_CanBeUnlocked()
    {
        // Arrange
        var go = new GameObject("FastTravelPoint");
        var point = go.AddComponent<FastTravelPoint>();

        // Act
        point.Unlock();

        // Assert
        Assert.IsTrue(point.IsUnlocked);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    #endregion

    #region DungeonData Tests

    [Test]
    public void DungeonData_CanBeCreated()
    {
        // Act
        var data = ScriptableObject.CreateInstance<DungeonData>();

        // Assert
        Assert.IsNotNull(data);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void DungeonData_HasRequiredFields()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<DungeonData>();
        data.dungeonId = "DNG001";
        data.dungeonName = "Forgotten Caves";
        data.recommendedLevel = 10;
        data.maxPlayers = 4;

        // Assert
        Assert.AreEqual("DNG001", data.dungeonId);
        Assert.AreEqual("Forgotten Caves", data.dungeonName);
        Assert.AreEqual(10, data.recommendedLevel);
        Assert.AreEqual(4, data.maxPlayers);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void DungeonDifficulty_HasAllDifficulties()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(DungeonDifficulty), "Normal"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(DungeonDifficulty), "Hard"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(DungeonDifficulty), "Nightmare"));
    }

    #endregion

    #region WorldEventData Tests

    [Test]
    public void WorldEventData_CanBeCreated()
    {
        // Act
        var data = ScriptableObject.CreateInstance<WorldEventData>();

        // Assert
        Assert.IsNotNull(data);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void WorldEventData_HasRequiredFields()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<WorldEventData>();
        data.eventId = "EVT001";
        data.eventName = "World Boss Awakens";
        data.eventType = WorldEventType.WorldBoss;

        // Assert
        Assert.AreEqual("EVT001", data.eventId);
        Assert.AreEqual("World Boss Awakens", data.eventName);
        Assert.AreEqual(WorldEventType.WorldBoss, data.eventType);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void WorldEventType_HasAllTypes()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(WorldEventType), "WorldBoss"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(WorldEventType), "Invasion"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(WorldEventType), "Seasonal"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(WorldEventType), "ResourceNode"));
    }

    #endregion

    #region WorldEventManager Tests

    [Test]
    public void WorldEventManager_CanBeCreated()
    {
        // Reset singleton
        var instanceField = typeof(WorldEventManager).GetField("_instance",
            BindingFlags.NonPublic | BindingFlags.Static);
        instanceField?.SetValue(null, null);

        // Arrange
        var go = new GameObject("WorldEventManager");
        var manager = go.AddComponent<WorldEventManager>();

        var awakeMethod = typeof(WorldEventManager).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(manager, null);

        // Assert
        Assert.IsNotNull(manager);

        // Cleanup
        instanceField?.SetValue(null, null);
        Object.DestroyImmediate(go);
    }

    #endregion
}
