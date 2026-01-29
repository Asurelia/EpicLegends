using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests unitaires pour le systeme de capture.
/// </summary>
public class CaptureSystemTests
{
    #region CaptureItem Tests

    [Test]
    public void CaptureItem_CanBeCreated()
    {
        // Act
        var item = ScriptableObject.CreateInstance<CaptureItemData>();

        // Assert
        Assert.IsNotNull(item);

        // Cleanup
        Object.DestroyImmediate(item);
    }

    [Test]
    public void CaptureItem_HasCaptureRateBonus()
    {
        // Arrange
        var item = ScriptableObject.CreateInstance<CaptureItemData>();
        SetField(item, "captureRateBonus", 0.2f);

        // Assert
        Assert.AreEqual(0.2f, item.captureRateBonus);

        // Cleanup
        Object.DestroyImmediate(item);
    }

    [Test]
    public void CaptureItem_HasTypeBonus()
    {
        // Arrange
        var item = ScriptableObject.CreateInstance<CaptureItemData>();
        SetField(item, "bonusAgainstType", CreatureType.Dragon);
        SetField(item, "typeBonusMultiplier", 1.5f);

        // Assert
        Assert.AreEqual(CreatureType.Dragon, item.bonusAgainstType);
        Assert.AreEqual(1.5f, item.typeBonusMultiplier);

        // Cleanup
        Object.DestroyImmediate(item);
    }

    #endregion

    #region CaptureCalculator Tests

    [Test]
    public void CaptureCalculator_BaseCapture_ReturnsValidRate()
    {
        // Arrange
        var creatureData = ScriptableObject.CreateInstance<CreatureData>();
        SetField(creatureData, "baseCaptureRate", 0.3f);
        var instance = new CreatureInstance(creatureData, 10);

        // Act
        float rate = CaptureCalculator.CalculateCaptureRate(instance, null, 1f);

        // Assert
        Assert.Greater(rate, 0f);
        Assert.LessOrEqual(rate, 1f);

        // Cleanup
        Object.DestroyImmediate(creatureData);
    }

    [Test]
    public void CaptureCalculator_LowHealth_IncreasesCaptureRate()
    {
        // Arrange
        var creatureData = ScriptableObject.CreateInstance<CreatureData>();
        SetField(creatureData, "baseCaptureRate", 0.3f);
        SetField(creatureData, "baseHealth", 100f);
        var instance = new CreatureInstance(creatureData, 10);

        // Full health
        float fullHealthRate = CaptureCalculator.CalculateCaptureRate(instance, null, 1f);

        // Low health
        instance.TakeDamage(instance.MaxHealth * 0.8f);
        float lowHealthRate = CaptureCalculator.CalculateCaptureRate(instance, null, 1f);

        // Assert
        Assert.Greater(lowHealthRate, fullHealthRate);

        // Cleanup
        Object.DestroyImmediate(creatureData);
    }

    [Test]
    public void CaptureCalculator_BetterItem_IncreasesCaptureRate()
    {
        // Arrange
        var creatureData = ScriptableObject.CreateInstance<CreatureData>();
        SetField(creatureData, "baseCaptureRate", 0.3f);
        var instance = new CreatureInstance(creatureData, 10);

        var basicItem = ScriptableObject.CreateInstance<CaptureItemData>();
        SetField(basicItem, "captureRateBonus", 0f);

        var betterItem = ScriptableObject.CreateInstance<CaptureItemData>();
        SetField(betterItem, "captureRateBonus", 0.3f);

        // Act
        float basicRate = CaptureCalculator.CalculateCaptureRate(instance, basicItem, 1f);
        float betterRate = CaptureCalculator.CalculateCaptureRate(instance, betterItem, 1f);

        // Assert
        Assert.Greater(betterRate, basicRate);

        // Cleanup
        Object.DestroyImmediate(creatureData);
        Object.DestroyImmediate(basicItem);
        Object.DestroyImmediate(betterItem);
    }

    [Test]
    public void CaptureCalculator_TypeBonus_Applied()
    {
        // Arrange
        var creatureData = ScriptableObject.CreateInstance<CreatureData>();
        SetField(creatureData, "baseCaptureRate", 0.3f);
        SetField(creatureData, "creatureType", CreatureType.Dragon);
        var instance = new CreatureInstance(creatureData, 10);

        var normalItem = ScriptableObject.CreateInstance<CaptureItemData>();
        SetField(normalItem, "captureRateBonus", 0f);
        SetField(normalItem, "hasBonusAgainstType", false);

        var dragonItem = ScriptableObject.CreateInstance<CaptureItemData>();
        SetField(dragonItem, "captureRateBonus", 0f);
        SetField(dragonItem, "hasBonusAgainstType", true);
        SetField(dragonItem, "bonusAgainstType", CreatureType.Dragon);
        SetField(dragonItem, "typeBonusMultiplier", 2f);

        // Act
        float normalRate = CaptureCalculator.CalculateCaptureRate(instance, normalItem, 1f);
        float dragonRate = CaptureCalculator.CalculateCaptureRate(instance, dragonItem, 1f);

        // Assert
        Assert.Greater(dragonRate, normalRate);

        // Cleanup
        Object.DestroyImmediate(creatureData);
        Object.DestroyImmediate(normalItem);
        Object.DestroyImmediate(dragonItem);
    }

    [Test]
    public void CaptureCalculator_HigherLevel_DecreasesRate()
    {
        // Arrange
        var creatureData = ScriptableObject.CreateInstance<CreatureData>();
        SetField(creatureData, "baseCaptureRate", 0.5f);
        SetField(creatureData, "maxLevel", 100);

        var lowLevel = new CreatureInstance(creatureData, 5);
        var highLevel = new CreatureInstance(creatureData, 50);

        // Act
        float lowLevelRate = CaptureCalculator.CalculateCaptureRate(lowLevel, null, 1f);
        float highLevelRate = CaptureCalculator.CalculateCaptureRate(highLevel, null, 1f);

        // Assert
        Assert.Greater(lowLevelRate, highLevelRate);

        // Cleanup
        Object.DestroyImmediate(creatureData);
    }

    [Test]
    public void CaptureCalculator_RarityAffectsRate()
    {
        // Arrange
        var commonData = ScriptableObject.CreateInstance<CreatureData>();
        SetField(commonData, "baseCaptureRate", 0.5f);
        SetField(commonData, "rarity", CreatureRarity.Common);

        var legendaryData = ScriptableObject.CreateInstance<CreatureData>();
        SetField(legendaryData, "baseCaptureRate", 0.5f);
        SetField(legendaryData, "rarity", CreatureRarity.Legendary);

        var common = new CreatureInstance(commonData, 10);
        var legendary = new CreatureInstance(legendaryData, 10);

        // Act
        float commonRate = CaptureCalculator.CalculateCaptureRate(common, null, 1f);
        float legendaryRate = CaptureCalculator.CalculateCaptureRate(legendary, null, 1f);

        // Assert
        Assert.Greater(commonRate, legendaryRate);

        // Cleanup
        Object.DestroyImmediate(commonData);
        Object.DestroyImmediate(legendaryData);
    }

    #endregion

    #region CaptureController Tests

    private GameObject _testObject;
    private CaptureController _captureController;

    [SetUp]
    public void Setup()
    {
        _testObject = new GameObject("CaptureController");
        _captureController = _testObject.AddComponent<CaptureController>();

        // Appeler Awake
        var awakeMethod = typeof(CaptureController).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(_captureController, null);
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_testObject);
    }

    [Test]
    public void CaptureController_CanBeAdded()
    {
        // Assert
        Assert.IsNotNull(_captureController);
    }

    [Test]
    public void CaptureController_HasCaptureRange()
    {
        // Arrange
        var rangeField = typeof(CaptureController).GetField("_captureRange",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        float range = (float)rangeField.GetValue(_captureController);

        // Assert
        Assert.Greater(range, 0f);
    }

    #endregion

    #region CaptureAttempt Tests

    [Test]
    public void CaptureAttempt_SuccessOrFail()
    {
        // Arrange
        var creatureData = ScriptableObject.CreateInstance<CreatureData>();
        SetField(creatureData, "baseCaptureRate", 1f); // 100% chance
        var instance = new CreatureInstance(creatureData, 1);

        // Reduire la vie pour maximiser le taux
        instance.TakeDamage(instance.MaxHealth * 0.9f);

        // Act - avec taux tres eleve, devrait reussir
        bool success = false;
        for (int i = 0; i < 100; i++)
        {
            if (CaptureCalculator.AttemptCapture(instance, null, 1f))
            {
                success = true;
                break;
            }
        }

        // Assert - avec 100 essais et haut taux, devrait reussir au moins une fois
        Assert.IsTrue(success);

        // Cleanup
        Object.DestroyImmediate(creatureData);
    }

    #endregion

    #region Helper Methods

    private void SetField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        field?.SetValue(obj, value);
    }

    #endregion
}
