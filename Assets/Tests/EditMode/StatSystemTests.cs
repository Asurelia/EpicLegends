using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests unitaires pour le système de statistiques.
/// </summary>
public class StatSystemTests
{
    private GameObject _testObject;
    private StatContainer _stats;

    [SetUp]
    public void Setup()
    {
        _testObject = new GameObject("TestEntity");
        _stats = _testObject.AddComponent<StatContainer>();
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_testObject);
    }

    #region Base Value Tests

    [Test]
    public void GetBaseValue_ReturnsCorrectValue()
    {
        // Act
        _stats.SetBaseValue(StatType.Strength, 50f);

        // Assert
        Assert.AreEqual(50f, _stats.GetBaseValue(StatType.Strength));
    }

    [Test]
    public void SetBaseValue_UpdatesFinalValue()
    {
        // Act
        _stats.SetBaseValue(StatType.Strength, 100f);

        // Assert
        Assert.AreEqual(100f, _stats.GetFinalValue(StatType.Strength));
    }

    [Test]
    public void ModifyBaseValue_AddsToExisting()
    {
        // Arrange
        _stats.SetBaseValue(StatType.Strength, 10f);

        // Act
        _stats.ModifyBaseValue(StatType.Strength, 5f);

        // Assert
        Assert.AreEqual(15f, _stats.GetBaseValue(StatType.Strength));
    }

    [Test]
    public void ModifyBaseValue_CanSubtract()
    {
        // Arrange
        _stats.SetBaseValue(StatType.Strength, 20f);

        // Act
        _stats.ModifyBaseValue(StatType.Strength, -5f);

        // Assert
        Assert.AreEqual(15f, _stats.GetBaseValue(StatType.Strength));
    }

    #endregion

    #region Modifier Tests

    [Test]
    public void FlatModifier_AddsToBaseValue()
    {
        // Arrange
        _stats.SetBaseValue(StatType.Strength, 10f);

        // Act
        _stats.AddModifier(StatType.Strength, StatModifier.Flat(5f));

        // Assert
        Assert.AreEqual(15f, _stats.GetFinalValue(StatType.Strength));
    }

    [Test]
    public void PercentAddModifier_AppliesPercentage()
    {
        // Arrange
        _stats.SetBaseValue(StatType.Strength, 100f);

        // Act
        _stats.AddModifier(StatType.Strength, StatModifier.PercentAdd(0.2f)); // +20%

        // Assert
        Assert.AreEqual(120f, _stats.GetFinalValue(StatType.Strength));
    }

    [Test]
    public void MultiplePercentAddModifiers_StackAdditively()
    {
        // Arrange
        _stats.SetBaseValue(StatType.Strength, 100f);

        // Act - Two +20% modifiers = +40%
        _stats.AddModifier(StatType.Strength, StatModifier.PercentAdd(0.2f));
        _stats.AddModifier(StatType.Strength, StatModifier.PercentAdd(0.2f));

        // Assert
        Assert.AreEqual(140f, _stats.GetFinalValue(StatType.Strength));
    }

    [Test]
    public void PercentMultModifier_MultipliesFinalValue()
    {
        // Arrange
        _stats.SetBaseValue(StatType.Strength, 100f);

        // Act
        _stats.AddModifier(StatType.Strength, StatModifier.PercentMult(0.5f)); // x1.5

        // Assert
        Assert.AreEqual(150f, _stats.GetFinalValue(StatType.Strength));
    }

    [Test]
    public void MultiplePercentMultModifiers_StackMultiplicatively()
    {
        // Arrange
        _stats.SetBaseValue(StatType.Strength, 100f);

        // Act - x1.5 * x1.5 = x2.25
        _stats.AddModifier(StatType.Strength, StatModifier.PercentMult(0.5f));
        _stats.AddModifier(StatType.Strength, StatModifier.PercentMult(0.5f));

        // Assert - 100 * 1.5 * 1.5 = 225
        Assert.AreEqual(225f, _stats.GetFinalValue(StatType.Strength));
    }

    [Test]
    public void ModifiersApplyInCorrectOrder()
    {
        // Arrange: base = 100
        _stats.SetBaseValue(StatType.Strength, 100f);

        // Act: +10 flat, then +20%, then x1.5
        // Order: 100 + 10 = 110, 110 * 1.2 = 132, 132 * 1.5 = 198
        _stats.AddModifier(StatType.Strength, StatModifier.Flat(10f));
        _stats.AddModifier(StatType.Strength, StatModifier.PercentAdd(0.2f));
        _stats.AddModifier(StatType.Strength, StatModifier.PercentMult(0.5f));

        // Assert
        Assert.AreEqual(198f, _stats.GetFinalValue(StatType.Strength));
    }

    [Test]
    public void RemoveModifier_UpdatesFinalValue()
    {
        // Arrange
        _stats.SetBaseValue(StatType.Strength, 100f);
        var modifier = StatModifier.Flat(50f);
        _stats.AddModifier(StatType.Strength, modifier);
        Assert.AreEqual(150f, _stats.GetFinalValue(StatType.Strength));

        // Act
        _stats.RemoveModifier(StatType.Strength, modifier);

        // Assert
        Assert.AreEqual(100f, _stats.GetFinalValue(StatType.Strength));
    }

    [Test]
    public void RemoveAllModifiersFromSource_RemovesCorrectModifiers()
    {
        // Arrange
        object source1 = new object();
        object source2 = new object();

        _stats.SetBaseValue(StatType.Strength, 100f);
        _stats.AddModifier(StatType.Strength, StatModifier.Flat(10f, source1));
        _stats.AddModifier(StatType.Strength, StatModifier.Flat(20f, source2));
        Assert.AreEqual(130f, _stats.GetFinalValue(StatType.Strength));

        // Act
        _stats.RemoveAllModifiersFromSource(source1);

        // Assert - Only source2's modifier remains
        Assert.AreEqual(120f, _stats.GetFinalValue(StatType.Strength));
    }

    [Test]
    public void ClearModifiers_RemovesAllModifiers()
    {
        // Arrange
        _stats.SetBaseValue(StatType.Strength, 100f);
        _stats.AddModifier(StatType.Strength, StatModifier.Flat(10f));
        _stats.AddModifier(StatType.Strength, StatModifier.PercentAdd(0.2f));

        // Act
        _stats.ClearModifiers(StatType.Strength);

        // Assert
        Assert.AreEqual(100f, _stats.GetFinalValue(StatType.Strength));
    }

    #endregion

    #region Event Tests

    [Test]
    public void OnStatChanged_FiresWhenBaseValueChanges()
    {
        // Arrange
        bool strengthChanged = false;
        float strengthNewValue = 0f;
        _stats.OnStatChanged += (stat, oldVal, newVal) =>
        {
            if (stat == StatType.Strength)
            {
                strengthChanged = true;
                strengthNewValue = newVal;
            }
        };

        // Act
        _stats.SetBaseValue(StatType.Strength, 50f);

        // Assert
        Assert.IsTrue(strengthChanged);
        Assert.AreEqual(50f, strengthNewValue);
    }

    [Test]
    public void OnStatChanged_FiresWhenModifierAdded()
    {
        // Arrange
        _stats.SetBaseValue(StatType.Strength, 100f);
        float newValue = 0f;
        _stats.OnStatChanged += (stat, oldVal, newVal) =>
        {
            if (stat == StatType.Strength)
                newValue = newVal;
        };

        // Act
        _stats.AddModifier(StatType.Strength, StatModifier.Flat(25f));

        // Assert
        Assert.AreEqual(125f, newValue);
    }

    [Test]
    public void OnLevelChanged_FiresWhenLevelChanges()
    {
        // Arrange
        int newLevel = 0;
        _stats.OnLevelChanged += (oldLvl, newLvl) => newLevel = newLvl;

        // Act
        _stats.Level = 5;

        // Assert
        Assert.AreEqual(5, newLevel);
    }

    #endregion

    #region Indexer Tests

    [Test]
    public void Indexer_ReturnsCorrectValue()
    {
        // Arrange
        _stats.SetBaseValue(StatType.Intelligence, 75f);

        // Assert
        Assert.AreEqual(75f, _stats[StatType.Intelligence]);
    }

    #endregion

    #region Level Tests

    [Test]
    public void Level_DefaultIsOne()
    {
        // Assert
        Assert.AreEqual(1, _stats.Level);
    }

    [Test]
    public void Level_CannotBeLessThanOne()
    {
        // Act
        _stats.Level = -5;

        // Assert
        Assert.AreEqual(1, _stats.Level);
    }

    #endregion

    #region GetModifiers Tests

    [Test]
    public void GetModifiers_ReturnsAddedModifiers()
    {
        // Arrange
        var mod1 = StatModifier.Flat(10f);
        var mod2 = StatModifier.PercentAdd(0.1f);
        _stats.AddModifier(StatType.Vitality, mod1);
        _stats.AddModifier(StatType.Vitality, mod2);

        // Act
        var modifiers = _stats.GetModifiers(StatType.Vitality);

        // Assert
        Assert.AreEqual(2, modifiers.Count);
    }

    [Test]
    public void GetModifiers_ReturnsEmptyForNoModifiers()
    {
        // Act
        var modifiers = _stats.GetModifiers(StatType.Speed);

        // Assert
        Assert.AreEqual(0, modifiers.Count);
    }

    #endregion
}

/// <summary>
/// Tests pour StatModifier struct.
/// </summary>
public class StatModifierTests
{
    [Test]
    public void Flat_CreatesCorrectModifier()
    {
        // Act
        var mod = StatModifier.Flat(10f);

        // Assert
        Assert.AreEqual(10f, mod.Value);
        Assert.AreEqual(ModifierType.Flat, mod.Type);
    }

    [Test]
    public void PercentAdd_CreatesCorrectModifier()
    {
        // Act
        var mod = StatModifier.PercentAdd(0.25f);

        // Assert
        Assert.AreEqual(0.25f, mod.Value);
        Assert.AreEqual(ModifierType.PercentAdd, mod.Type);
    }

    [Test]
    public void PercentMult_CreatesCorrectModifier()
    {
        // Act
        var mod = StatModifier.PercentMult(0.5f);

        // Assert
        Assert.AreEqual(0.5f, mod.Value);
        Assert.AreEqual(ModifierType.PercentMult, mod.Type);
    }

    [Test]
    public void ToString_FormatsCorrectly_Flat()
    {
        // Arrange
        var mod = StatModifier.Flat(10f);

        // Assert
        Assert.AreEqual("+10", mod.ToString());
    }

    [Test]
    public void ToString_FormatsCorrectly_PercentAdd()
    {
        // Arrange
        var mod = StatModifier.PercentAdd(0.25f);

        // Assert
        Assert.AreEqual("+25%", mod.ToString());
    }

    [Test]
    public void Equals_ReturnsTrueForSameValues()
    {
        // Arrange
        var mod1 = new StatModifier(10f, ModifierType.Flat, 0, null);
        var mod2 = new StatModifier(10f, ModifierType.Flat, 0, null);

        // Assert
        Assert.IsTrue(mod1.Equals(mod2));
    }

    [Test]
    public void Equals_ReturnsFalseForDifferentValues()
    {
        // Arrange
        var mod1 = StatModifier.Flat(10f);
        var mod2 = StatModifier.Flat(20f);

        // Assert
        Assert.IsFalse(mod1.Equals(mod2));
    }
}

/// <summary>
/// Tests pour StatDefinition ScriptableObject.
/// </summary>
public class StatDefinitionTests
{
    [Test]
    public void CalculateValueForLevel_ReturnsBaseForLevel1()
    {
        // Arrange
        var definition = ScriptableObject.CreateInstance<StatDefinition>();
        definition.baseValue = 100f;
        definition.growthPerLevel = 10f;
        definition.growthMultiplier = 1f;

        // Act
        float value = definition.CalculateValueForLevel(1);

        // Assert
        Assert.AreEqual(100f, value);

        // Cleanup
        Object.DestroyImmediate(definition);
    }

    [Test]
    public void CalculateValueForLevel_AddsGrowthForHigherLevels()
    {
        // Arrange
        var definition = ScriptableObject.CreateInstance<StatDefinition>();
        definition.baseValue = 100f;
        definition.growthPerLevel = 10f;
        definition.growthMultiplier = 1f;

        // Act - Level 5: 100 + (5-1) * 10 = 140
        float value = definition.CalculateValueForLevel(5);

        // Assert
        Assert.AreEqual(140f, value);

        // Cleanup
        Object.DestroyImmediate(definition);
    }

    [Test]
    public void ClampValue_RespectsMinMax()
    {
        // Arrange
        var definition = ScriptableObject.CreateInstance<StatDefinition>();
        definition.minValue = 0f;
        definition.maxValue = 100f;

        // Assert
        Assert.AreEqual(0f, definition.ClampValue(-50f));
        Assert.AreEqual(100f, definition.ClampValue(150f));
        Assert.AreEqual(50f, definition.ClampValue(50f));

        // Cleanup
        Object.DestroyImmediate(definition);
    }

    [Test]
    public void FormatValue_AsPercent()
    {
        // Arrange
        var definition = ScriptableObject.CreateInstance<StatDefinition>();
        definition.showAsPercent = true;
        definition.decimalPlaces = 1;

        // Act
        string formatted = definition.FormatValue(0.255f);

        // Assert - Vérifie que le format contient 25 et 5 (indépendant de la locale)
        Assert.IsTrue(formatted.Contains("25") && formatted.Contains("5") && formatted.EndsWith("%"));

        // Cleanup
        Object.DestroyImmediate(definition);
    }
}
