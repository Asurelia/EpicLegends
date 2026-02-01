using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests pour les fonctionnalites d'or de l'Inventory.
/// </summary>
[TestFixture]
public class InventoryGoldTests
{
    private GameObject _inventoryObj;
    private Inventory _inventory;

    [SetUp]
    public void SetUp()
    {
        _inventoryObj = new GameObject("Inventory");
        _inventory = _inventoryObj.AddComponent<Inventory>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_inventoryObj != null)
        {
            Object.DestroyImmediate(_inventoryObj);
        }
    }

    [Test]
    public void Inventory_Gold_InitiallyZero()
    {
        Assert.AreEqual(0, _inventory.Gold);
    }

    [Test]
    public void Inventory_AddGold_IncreasesGold()
    {
        _inventory.AddGold(100);
        Assert.AreEqual(100, _inventory.Gold);
    }

    [Test]
    public void Inventory_AddGold_MultipleTimes()
    {
        _inventory.AddGold(50);
        _inventory.AddGold(30);
        _inventory.AddGold(20);
        Assert.AreEqual(100, _inventory.Gold);
    }

    [Test]
    public void Inventory_AddGold_NegativeAmount_NoEffect()
    {
        _inventory.AddGold(100);
        _inventory.AddGold(-50);
        Assert.AreEqual(100, _inventory.Gold);
    }

    [Test]
    public void Inventory_AddGold_ZeroAmount_NoEffect()
    {
        _inventory.AddGold(100);
        _inventory.AddGold(0);
        Assert.AreEqual(100, _inventory.Gold);
    }

    [Test]
    public void Inventory_RemoveGold_DecreasesGold()
    {
        _inventory.Gold = 100;
        bool result = _inventory.RemoveGold(30);
        Assert.IsTrue(result);
        Assert.AreEqual(70, _inventory.Gold);
    }

    [Test]
    public void Inventory_RemoveGold_ExactAmount()
    {
        _inventory.Gold = 50;
        bool result = _inventory.RemoveGold(50);
        Assert.IsTrue(result);
        Assert.AreEqual(0, _inventory.Gold);
    }

    [Test]
    public void Inventory_RemoveGold_InsufficientFunds_ReturnsFalse()
    {
        _inventory.Gold = 30;
        bool result = _inventory.RemoveGold(50);
        Assert.IsFalse(result);
        Assert.AreEqual(30, _inventory.Gold); // Gold inchange
    }

    [Test]
    public void Inventory_RemoveGold_ZeroAmount_ReturnsTrue()
    {
        _inventory.Gold = 100;
        bool result = _inventory.RemoveGold(0);
        Assert.IsTrue(result);
        Assert.AreEqual(100, _inventory.Gold);
    }

    [Test]
    public void Inventory_RemoveGold_NegativeAmount_ReturnsTrue()
    {
        _inventory.Gold = 100;
        bool result = _inventory.RemoveGold(-10);
        Assert.IsTrue(result);
        Assert.AreEqual(100, _inventory.Gold);
    }

    [Test]
    public void Inventory_Gold_SetterPreventsNegative()
    {
        _inventory.Gold = -100;
        Assert.AreEqual(0, _inventory.Gold);
    }

    [Test]
    public void Inventory_Gold_SetterAcceptsPositive()
    {
        _inventory.Gold = 500;
        Assert.AreEqual(500, _inventory.Gold);
    }

    [Test]
    public void Inventory_Clear_ResetsGold()
    {
        _inventory.Gold = 1000;
        _inventory.Clear();
        Assert.AreEqual(0, _inventory.Gold);
    }
}
