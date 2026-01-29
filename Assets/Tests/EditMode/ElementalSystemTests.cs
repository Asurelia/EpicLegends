using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Tests unitaires pour le systeme elementaire.
/// </summary>
public class ElementalSystemTests
{
    #region ElementType Tests

    [Test]
    public void ElementType_HasAllEightElements()
    {
        // Assert - verifie que tous les elements existent
        Assert.AreEqual(0, (int)ElementType.Fire);
        Assert.AreEqual(1, (int)ElementType.Water);
        Assert.AreEqual(2, (int)ElementType.Ice);
        Assert.AreEqual(3, (int)ElementType.Electric);
        Assert.AreEqual(4, (int)ElementType.Wind);
        Assert.AreEqual(5, (int)ElementType.Earth);
        Assert.AreEqual(6, (int)ElementType.Light);
        Assert.AreEqual(7, (int)ElementType.Dark);
    }

    #endregion

    #region ElementalReactionType Tests

    [Test]
    public void ElementalReactionType_HasAllReactions()
    {
        // Assert - verifie que toutes les reactions existent
        Assert.IsTrue(System.Enum.IsDefined(typeof(ElementalReactionType), "Vaporize"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ElementalReactionType), "Melt"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ElementalReactionType), "Overload"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ElementalReactionType), "Superconduct"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ElementalReactionType), "ElectroCharged"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ElementalReactionType), "Frozen"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ElementalReactionType), "Swirl"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ElementalReactionType), "Crystallize"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ElementalReactionType), "Radiance"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(ElementalReactionType), "Eclipse"));
    }

    #endregion

    #region ElementalStatus Tests

    private GameObject _testObject;
    private ElementalStatus _status;

    [SetUp]
    public void Setup()
    {
        _testObject = new GameObject("TestObject");
        _status = _testObject.AddComponent<ElementalStatus>();
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_testObject);
    }

    [Test]
    public void ElementalStatus_InitiallyNone()
    {
        // Assert
        Assert.IsFalse(_status.HasElement);
        Assert.AreEqual(ElementType.Fire, _status.CurrentElement); // valeur par defaut
        Assert.AreEqual(0f, _status.CurrentGauge);
    }

    [Test]
    public void ApplyElement_SetsElement()
    {
        // Act
        _status.ApplyElement(ElementType.Fire, 1f);

        // Assert
        Assert.IsTrue(_status.HasElement);
        Assert.AreEqual(ElementType.Fire, _status.CurrentElement);
        Assert.Greater(_status.CurrentGauge, 0f);
    }

    [Test]
    public void ApplyElement_SameElement_AddsGauge()
    {
        // Arrange - utiliser une valeur initiale plus petite
        _status.ApplyElement(ElementType.Fire, 0.5f);
        float initialGauge = _status.CurrentGauge;

        // Act
        _status.ApplyElement(ElementType.Fire, 0.5f);

        // Assert
        Assert.Greater(_status.CurrentGauge, initialGauge);
    }

    [Test]
    public void ClearElement_RemovesElement()
    {
        // Arrange
        _status.ApplyElement(ElementType.Water, 1f);

        // Act
        _status.ClearElement();

        // Assert
        Assert.IsFalse(_status.HasElement);
        Assert.AreEqual(0f, _status.CurrentGauge);
    }

    #endregion

    #region ElementalReactionCalculator Tests

    [Test]
    public void GetReaction_FireWater_ReturnsVaporize()
    {
        // Act
        var reaction = ElementalReactionCalculator.GetReaction(ElementType.Fire, ElementType.Water);

        // Assert
        Assert.AreEqual(ElementalReactionType.Vaporize, reaction);
    }

    [Test]
    public void GetReaction_FireIce_ReturnsMelt()
    {
        // Act
        var reaction = ElementalReactionCalculator.GetReaction(ElementType.Fire, ElementType.Ice);

        // Assert
        Assert.AreEqual(ElementalReactionType.Melt, reaction);
    }

    [Test]
    public void GetReaction_FireElectric_ReturnsOverload()
    {
        // Act
        var reaction = ElementalReactionCalculator.GetReaction(ElementType.Fire, ElementType.Electric);

        // Assert
        Assert.AreEqual(ElementalReactionType.Overload, reaction);
    }

    [Test]
    public void GetReaction_IceElectric_ReturnsSuperconduct()
    {
        // Act
        var reaction = ElementalReactionCalculator.GetReaction(ElementType.Ice, ElementType.Electric);

        // Assert
        Assert.AreEqual(ElementalReactionType.Superconduct, reaction);
    }

    [Test]
    public void GetReaction_WaterElectric_ReturnsElectroCharged()
    {
        // Act
        var reaction = ElementalReactionCalculator.GetReaction(ElementType.Water, ElementType.Electric);

        // Assert
        Assert.AreEqual(ElementalReactionType.ElectroCharged, reaction);
    }

    [Test]
    public void GetReaction_WaterIce_ReturnsFrozen()
    {
        // Act
        var reaction = ElementalReactionCalculator.GetReaction(ElementType.Water, ElementType.Ice);

        // Assert
        Assert.AreEqual(ElementalReactionType.Frozen, reaction);
    }

    [Test]
    public void GetReaction_WindWithElement_ReturnsSwirl()
    {
        // Act
        var reaction = ElementalReactionCalculator.GetReaction(ElementType.Wind, ElementType.Fire);

        // Assert
        Assert.AreEqual(ElementalReactionType.Swirl, reaction);
    }

    [Test]
    public void GetReaction_EarthWithElement_ReturnsCrystallize()
    {
        // Act
        var reaction = ElementalReactionCalculator.GetReaction(ElementType.Earth, ElementType.Fire);

        // Assert
        Assert.AreEqual(ElementalReactionType.Crystallize, reaction);
    }

    [Test]
    public void GetReaction_LightDark_ReturnsRadiance()
    {
        // Act
        var reaction = ElementalReactionCalculator.GetReaction(ElementType.Light, ElementType.Dark);

        // Assert
        Assert.AreEqual(ElementalReactionType.Radiance, reaction);
    }

    [Test]
    public void GetReaction_DarkLight_ReturnsEclipse()
    {
        // Act
        var reaction = ElementalReactionCalculator.GetReaction(ElementType.Dark, ElementType.Light);

        // Assert
        Assert.AreEqual(ElementalReactionType.Eclipse, reaction);
    }

    [Test]
    public void GetReaction_SameElement_ReturnsNone()
    {
        // Act
        var reaction = ElementalReactionCalculator.GetReaction(ElementType.Fire, ElementType.Fire);

        // Assert
        Assert.AreEqual(ElementalReactionType.None, reaction);
    }

    [Test]
    public void GetReactionMultiplier_Vaporize_Returns2x()
    {
        // Act
        float multiplier = ElementalReactionCalculator.GetReactionMultiplier(ElementalReactionType.Vaporize);

        // Assert
        Assert.AreEqual(2f, multiplier);
    }

    [Test]
    public void GetReactionMultiplier_Melt_Returns2x()
    {
        // Act
        float multiplier = ElementalReactionCalculator.GetReactionMultiplier(ElementalReactionType.Melt);

        // Assert
        Assert.AreEqual(2f, multiplier);
    }

    [Test]
    public void GetReactionMultiplier_Overload_Returns1_5x()
    {
        // Act
        float multiplier = ElementalReactionCalculator.GetReactionMultiplier(ElementalReactionType.Overload);

        // Assert
        Assert.AreEqual(1.5f, multiplier);
    }

    #endregion

    #region ElementalReactionData Tests

    [Test]
    public void ElementalReactionData_CanBeCreated()
    {
        // Act
        var data = ScriptableObject.CreateInstance<ElementalReactionData>();

        // Assert
        Assert.IsNotNull(data);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    #endregion

    #region ElementalReactionHandler Tests

    [Test]
    public void ElementalReactionHandler_CanBeAdded()
    {
        // Arrange
        var go = new GameObject();
        go.AddComponent<ElementalStatus>(); // RequireComponent

        // Act
        var handler = go.AddComponent<ElementalReactionHandler>();

        // Assert
        Assert.IsNotNull(handler);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void ElementalReactionHandler_TriggersReaction()
    {
        // Arrange
        var go = new GameObject();
        var status = go.AddComponent<ElementalStatus>();
        var handler = go.AddComponent<ElementalReactionHandler>();

        // Initialiser manuellement (EditMode n'appelle pas Awake)
        var awakeMethod = typeof(ElementalReactionHandler).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(handler, null);

        bool reactionTriggered = false;
        handler.OnReactionTriggered += (reaction, damage) => reactionTriggered = true;

        // Appliquer un element initial
        status.ApplyElement(ElementType.Water, 1f);

        // Act - appliquer un element qui reagit
        handler.TriggerReaction(ElementType.Fire, 100f);

        // Assert
        Assert.IsTrue(reactionTriggered);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    #endregion

    #region Integration Tests

    [Test]
    public void FullReactionFlow_ApplyTwoElements_TriggersReaction()
    {
        // Arrange
        var go = new GameObject();
        var status = go.AddComponent<ElementalStatus>();
        var handler = go.AddComponent<ElementalReactionHandler>();

        // Initialiser manuellement (EditMode n'appelle pas Awake)
        var awakeMethod = typeof(ElementalReactionHandler).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(handler, null);

        ElementalReactionType triggeredReaction = ElementalReactionType.None;
        handler.OnReactionTriggered += (reaction, damage) => triggeredReaction = reaction;

        // Act - appliquer Fire puis Water
        status.ApplyElement(ElementType.Fire, 1f);
        handler.TriggerReaction(ElementType.Water, 100f);

        // Assert - Vaporize (Water + Fire = Vaporize)
        Assert.AreEqual(ElementalReactionType.Vaporize, triggeredReaction);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Frozen_ApplyWaterThenIce_CreatesFreezeStatus()
    {
        // Arrange
        var go = new GameObject();
        var status = go.AddComponent<ElementalStatus>();
        var handler = go.AddComponent<ElementalReactionHandler>();

        // Initialiser manuellement (EditMode n'appelle pas Awake)
        var awakeMethod = typeof(ElementalReactionHandler).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(handler, null);

        bool frozenTriggered = false;
        handler.OnReactionTriggered += (reaction, damage) =>
        {
            if (reaction == ElementalReactionType.Frozen)
                frozenTriggered = true;
        };

        // Act
        status.ApplyElement(ElementType.Water, 1f);
        handler.TriggerReaction(ElementType.Ice, 100f);

        // Assert
        Assert.IsTrue(frozenTriggered);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    #endregion
}
