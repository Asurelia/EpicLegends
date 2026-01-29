using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests unitaires pour le systeme d'armes.
/// </summary>
public class WeaponSystemTests
{
    #region WeaponType Tests

    [Test]
    public void WeaponType_HasAllFiveTypes()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(WeaponType), "Sword"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(WeaponType), "Greatsword"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(WeaponType), "DualBlades"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(WeaponType), "Spear"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(WeaponType), "Bow"));
    }

    #endregion

    #region WeaponData Tests

    [Test]
    public void WeaponData_CanBeCreated()
    {
        // Act
        var data = ScriptableObject.CreateInstance<WeaponData>();

        // Assert
        Assert.IsNotNull(data);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void WeaponData_HasCorrectDefaults()
    {
        // Act
        var data = ScriptableObject.CreateInstance<WeaponData>();

        // Assert
        Assert.Greater(data.baseDamage, 0f);
        Assert.Greater(data.attackSpeed, 0f);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void WeaponData_CalculateDamage_ReturnsCorrectValue()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<WeaponData>();
        SetField(data, "baseDamage", 50f);

        // Act
        float damage = data.GetDamageWithStats(100f); // 100 attack

        // Assert
        Assert.Greater(damage, 50f); // Devrait etre augmente par l'attaque

        // Cleanup
        Object.DestroyImmediate(data);
    }

    #endregion

    #region WeaponController Tests

    private GameObject _testObject;
    private WeaponController _weaponController;

    [SetUp]
    public void Setup()
    {
        _testObject = new GameObject("TestWeapon");
        _weaponController = _testObject.AddComponent<WeaponController>();

        // Creer une arme de test
        var weaponData = ScriptableObject.CreateInstance<WeaponData>();
        SetField(weaponData, "weaponName", "TestSword");
        SetField(weaponData, "weaponType", WeaponType.Sword);
        SetField(weaponData, "baseDamage", 50f);
        SetField(weaponData, "attackSpeed", 1f);

        SetField(_weaponController, "_currentWeaponData", weaponData);
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_testObject);
    }

    private void SetField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        field?.SetValue(obj, value);
    }

    [Test]
    public void WeaponController_HasWeaponEquipped()
    {
        // Assert
        Assert.IsTrue(_weaponController.HasWeaponEquipped);
    }

    [Test]
    public void WeaponController_CanAttack_WhenIdle()
    {
        // Assert
        Assert.IsTrue(_weaponController.CanAttack);
    }

    [Test]
    public void WeaponController_GetCurrentDamage_ReturnsPositive()
    {
        // Act
        float damage = _weaponController.GetCurrentDamage();

        // Assert
        Assert.Greater(damage, 0f);
    }

    #endregion

    #region Sword Tests

    [Test]
    public void Sword_HasBalancedStats()
    {
        // Arrange
        var sword = ScriptableObject.CreateInstance<WeaponData>();
        SetField(sword, "weaponType", WeaponType.Sword);
        SetField(sword, "baseDamage", 50f);
        SetField(sword, "attackSpeed", 1f);
        SetField(sword, "range", 2f);

        // Assert - epee equilibree
        Assert.AreEqual(50f, sword.baseDamage);
        Assert.AreEqual(1f, sword.attackSpeed);
        Assert.AreEqual(2f, sword.range);

        // Cleanup
        Object.DestroyImmediate(sword);
    }

    #endregion

    #region Greatsword Tests

    [Test]
    public void Greatsword_HasHighDamageLowSpeed()
    {
        // Arrange
        var greatsword = ScriptableObject.CreateInstance<WeaponData>();
        SetField(greatsword, "weaponType", WeaponType.Greatsword);
        SetField(greatsword, "baseDamage", 100f);
        SetField(greatsword, "attackSpeed", 0.6f);
        SetField(greatsword, "range", 2.5f);

        // Assert - grande epee = degats eleves, vitesse lente
        Assert.AreEqual(100f, greatsword.baseDamage);
        Assert.Less(greatsword.attackSpeed, 1f);
        Assert.Greater(greatsword.range, 2f);

        // Cleanup
        Object.DestroyImmediate(greatsword);
    }

    #endregion

    #region DualBlades Tests

    [Test]
    public void DualBlades_HasLowDamageHighSpeed()
    {
        // Arrange
        var dualBlades = ScriptableObject.CreateInstance<WeaponData>();
        SetField(dualBlades, "weaponType", WeaponType.DualBlades);
        SetField(dualBlades, "baseDamage", 30f);
        SetField(dualBlades, "attackSpeed", 1.5f);
        SetField(dualBlades, "range", 1.5f);

        // Assert - doubles lames = degats faibles, vitesse elevee
        Assert.Less(dualBlades.baseDamage, 50f);
        Assert.Greater(dualBlades.attackSpeed, 1f);

        // Cleanup
        Object.DestroyImmediate(dualBlades);
    }

    #endregion

    #region Spear Tests

    [Test]
    public void Spear_HasLongRange()
    {
        // Arrange
        var spear = ScriptableObject.CreateInstance<WeaponData>();
        SetField(spear, "weaponType", WeaponType.Spear);
        SetField(spear, "baseDamage", 45f);
        SetField(spear, "attackSpeed", 0.9f);
        SetField(spear, "range", 3.5f);

        // Assert - lance = longue portee
        Assert.Greater(spear.range, 3f);

        // Cleanup
        Object.DestroyImmediate(spear);
    }

    #endregion

    #region Bow Tests

    [Test]
    public void Bow_HasRangedType()
    {
        // Arrange
        var bow = ScriptableObject.CreateInstance<WeaponData>();
        SetField(bow, "weaponType", WeaponType.Bow);
        SetField(bow, "baseDamage", 40f);
        SetField(bow, "attackSpeed", 0.8f);
        SetField(bow, "range", 20f);
        SetField(bow, "isRanged", true);

        // Assert - arc = distance
        Assert.IsTrue(bow.isRanged);
        Assert.Greater(bow.range, 10f);

        // Cleanup
        Object.DestroyImmediate(bow);
    }

    [Test]
    public void Bow_CanChargeShot()
    {
        // Arrange
        var bow = ScriptableObject.CreateInstance<WeaponData>();
        SetField(bow, "weaponType", WeaponType.Bow);
        SetField(bow, "canCharge", true);
        SetField(bow, "chargeMultiplier", 2f);

        // Assert
        Assert.IsTrue(bow.canCharge);
        Assert.AreEqual(2f, bow.chargeMultiplier);

        // Cleanup
        Object.DestroyImmediate(bow);
    }

    #endregion

    #region WeaponStats Tests

    [Test]
    public void WeaponStats_AppliesCorrectly()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<WeaponData>();
        SetField(data, "baseDamage", 50f);
        SetField(data, "critChance", 0.1f);
        SetField(data, "critMultiplier", 1.5f);

        // Assert
        Assert.AreEqual(50f, data.baseDamage);
        Assert.AreEqual(0.1f, data.critChance);
        Assert.AreEqual(1.5f, data.critMultiplier);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    #endregion

    #region WeaponCombo Tests

    [Test]
    public void WeaponData_HasComboData()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<WeaponData>();
        var combo = ScriptableObject.CreateInstance<ComboData>();
        SetField(data, "lightCombo", combo);

        // Assert
        Assert.IsNotNull(data.lightCombo);

        // Cleanup
        Object.DestroyImmediate(combo);
        Object.DestroyImmediate(data);
    }

    #endregion
}
