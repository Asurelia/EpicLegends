using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests unitaires pour le systeme de creatures.
/// </summary>
public class CreatureSystemTests
{
    #region CreatureType Tests

    [Test]
    public void CreatureType_HasAllTypes()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(CreatureType), "Beast"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CreatureType), "Dragon"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CreatureType), "Spirit"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CreatureType), "Elemental"));
    }

    [Test]
    public void CreatureRarity_HasAllRarities()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(CreatureRarity), "Common"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CreatureRarity), "Uncommon"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CreatureRarity), "Rare"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CreatureRarity), "Epic"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CreatureRarity), "Legendary"));
    }

    #endregion

    #region CreatureData Tests

    [Test]
    public void CreatureData_CanBeCreated()
    {
        // Act
        var data = ScriptableObject.CreateInstance<CreatureData>();

        // Assert
        Assert.IsNotNull(data);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void CreatureData_HasStats()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<CreatureData>();
        SetField(data, "baseHealth", 100f);
        SetField(data, "baseAttack", 50f);
        SetField(data, "baseDefense", 30f);

        // Assert
        Assert.AreEqual(100f, data.baseHealth);
        Assert.AreEqual(50f, data.baseAttack);
        Assert.AreEqual(30f, data.baseDefense);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void CreatureData_CalculatesLevelStats()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<CreatureData>();
        SetField(data, "baseHealth", 100f);
        SetField(data, "healthPerLevel", 10f);

        // Act
        float health = data.GetHealthAtLevel(10);

        // Assert
        Assert.AreEqual(190f, health); // 100 + 9*10

        // Cleanup
        Object.DestroyImmediate(data);
    }

    private void SetField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        field?.SetValue(obj, value);
    }

    #endregion

    #region CreatureInstance Tests

    [Test]
    public void CreatureInstance_CanBeCreated()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<CreatureData>();
        SetField(data, "creatureName", "TestCreature");
        SetField(data, "baseHealth", 100f);

        // Act
        var instance = new CreatureInstance(data);

        // Assert
        Assert.IsNotNull(instance);
        Assert.AreEqual(1, instance.Level);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void CreatureInstance_TracksExperience()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<CreatureData>();
        SetField(data, "baseHealth", 100f);
        var instance = new CreatureInstance(data);

        // Act
        instance.AddExperience(100);

        // Assert
        Assert.Greater(instance.Experience, 0);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void CreatureInstance_CanLevelUp()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<CreatureData>();
        SetField(data, "baseHealth", 100f);
        var instance = new CreatureInstance(data);

        // Act
        instance.AddExperience(1000);

        // Assert
        Assert.Greater(instance.Level, 1);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    #endregion

    #region CreatureController Tests

    private GameObject _testObject;
    private CreatureController _controller;

    [SetUp]
    public void Setup()
    {
        _testObject = new GameObject("TestCreature");
        _controller = _testObject.AddComponent<CreatureController>();
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_testObject);
    }

    [Test]
    public void CreatureController_CanBeAdded()
    {
        // Assert
        Assert.IsNotNull(_controller);
    }

    #endregion

    #region CreatureParty Tests

    [Test]
    public void CreatureParty_CanAddCreature()
    {
        // Arrange
        var go = new GameObject();
        var party = go.AddComponent<CreatureParty>();
        var data = ScriptableObject.CreateInstance<CreatureData>();
        var instance = new CreatureInstance(data);

        // Appeler Awake
        var awakeMethod = typeof(CreatureParty).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(party, null);

        // Act
        bool added = party.AddCreature(instance);

        // Assert
        Assert.IsTrue(added);
        Assert.AreEqual(1, party.CreatureCount);

        // Cleanup
        Object.DestroyImmediate(data);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void CreatureParty_HasMaxSize()
    {
        // Arrange
        var go = new GameObject();
        var party = go.AddComponent<CreatureParty>();

        // Appeler Awake
        var awakeMethod = typeof(CreatureParty).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(party, null);

        // Assert
        Assert.AreEqual(6, party.MaxPartySize);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    #endregion

    #region Creature Abilities Tests

    [Test]
    public void CreatureData_HasAbilities()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<CreatureData>();
        var skill = ScriptableObject.CreateInstance<SkillData>();
        SetField(data, "abilities", new SkillData[] { skill });

        // Assert
        Assert.IsNotNull(data.abilities);
        Assert.AreEqual(1, data.abilities.Length);

        // Cleanup
        Object.DestroyImmediate(skill);
        Object.DestroyImmediate(data);
    }

    #endregion
}
