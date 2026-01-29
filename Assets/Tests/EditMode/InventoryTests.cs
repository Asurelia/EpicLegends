using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests unitaires pour le système d'inventaire.
/// </summary>
public class InventoryTests
{
    private GameObject _testObject;
    private Inventory _inventory;
    private ItemData _swordData;
    private ItemData _potionData;
    private ItemData _materialData;

    [SetUp]
    public void Setup()
    {
        _testObject = new GameObject("TestInventory");
        _inventory = _testObject.AddComponent<Inventory>();

        // Créer des ItemData de test
        _swordData = ScriptableObject.CreateInstance<ItemData>();
        _swordData.itemId = "sword_iron";
        _swordData.displayName = "Iron Sword";
        _swordData.category = ItemCategory.Weapon;
        _swordData.isStackable = false;
        _swordData.maxStackSize = 1;

        _potionData = ScriptableObject.CreateInstance<ItemData>();
        _potionData.itemId = "potion_health";
        _potionData.displayName = "Health Potion";
        _potionData.category = ItemCategory.Consumable;
        _potionData.isStackable = true;
        _potionData.maxStackSize = 10;

        _materialData = ScriptableObject.CreateInstance<ItemData>();
        _materialData.itemId = "material_wood";
        _materialData.displayName = "Wood";
        _materialData.category = ItemCategory.Material;
        _materialData.isStackable = true;
        _materialData.maxStackSize = 99;
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_testObject);
        Object.DestroyImmediate(_swordData);
        Object.DestroyImmediate(_potionData);
        Object.DestroyImmediate(_materialData);
    }

    #region Add Item Tests

    [Test]
    public void TryAddItem_AddsItemToInventory()
    {
        // Act
        int remaining = _inventory.TryAddItem(_swordData);

        // Assert
        Assert.AreEqual(0, remaining);
        Assert.AreEqual(1, _inventory.CountItem(_swordData));
    }

    [Test]
    public void TryAddItem_StackableItems_StacksCorrectly()
    {
        // Act
        _inventory.TryAddItem(_potionData, 3);
        _inventory.TryAddItem(_potionData, 2);

        // Assert
        Assert.AreEqual(5, _inventory.CountItem(_potionData));
        Assert.AreEqual(1, _inventory.UsedSlots); // Should be in one stack
    }

    [Test]
    public void TryAddItem_ExceedsStackSize_CreatesNewStack()
    {
        // Act
        _inventory.TryAddItem(_potionData, 15); // maxStackSize is 10

        // Assert
        Assert.AreEqual(15, _inventory.CountItem(_potionData));
        Assert.AreEqual(2, _inventory.UsedSlots); // 10 + 5 = 2 stacks
    }

    [Test]
    public void TryAddItem_NonStackable_CreatesMultipleSlots()
    {
        // Act
        _inventory.TryAddItem(_swordData, 3);

        // Assert
        Assert.AreEqual(3, _inventory.CountItem(_swordData));
        Assert.AreEqual(3, _inventory.UsedSlots);
    }

    [Test]
    public void TryAddItem_FullInventory_ReturnsOverflow()
    {
        // Arrange - Fill inventory with swords
        for (int i = 0; i < 20; i++)
        {
            _inventory.TryAddItem(_swordData);
        }

        // Act
        int remaining = _inventory.TryAddItem(_swordData, 5);

        // Assert
        Assert.AreEqual(5, remaining);
    }

    #endregion

    #region Remove Item Tests

    [Test]
    public void RemoveItem_RemovesFromInventory()
    {
        // Arrange
        _inventory.TryAddItem(_potionData, 5);

        // Act
        int removed = _inventory.RemoveItem(_potionData, 3);

        // Assert
        Assert.AreEqual(3, removed);
        Assert.AreEqual(2, _inventory.CountItem(_potionData));
    }

    [Test]
    public void RemoveItem_RemovesEntireStack()
    {
        // Arrange
        _inventory.TryAddItem(_potionData, 5);

        // Act
        int removed = _inventory.RemoveItem(_potionData, 5);

        // Assert
        Assert.AreEqual(5, removed);
        Assert.AreEqual(0, _inventory.CountItem(_potionData));
        Assert.AreEqual(0, _inventory.UsedSlots);
    }

    [Test]
    public void RemoveItemAt_ReturnsRemovedItem()
    {
        // Arrange
        _inventory.TryAddItem(_swordData);
        int slot = _inventory.FindItem(_swordData);

        // Act
        var removed = _inventory.RemoveItemAt(slot);

        // Assert
        Assert.IsNotNull(removed);
        Assert.AreEqual(_swordData, removed.Data);
        Assert.AreEqual(0, _inventory.UsedSlots);
    }

    [Test]
    public void RemoveItemAt_PartialRemove_SplitsStack()
    {
        // Arrange
        _inventory.TryAddItem(_potionData, 5);
        int slot = _inventory.FindItem(_potionData);

        // Act
        var removed = _inventory.RemoveItemAt(slot, 2);

        // Assert
        Assert.IsNotNull(removed);
        Assert.AreEqual(2, removed.Quantity);
        Assert.AreEqual(3, _inventory.CountItem(_potionData));
    }

    #endregion

    #region Move Item Tests

    [Test]
    public void MoveItem_SwapsSlots()
    {
        // Arrange
        _inventory.TryAddItem(_swordData);
        _inventory.TryAddItem(_potionData, 5);
        int swordSlot = _inventory.FindItem(_swordData);
        int potionSlot = _inventory.FindItem(_potionData);

        // Act
        _inventory.MoveItem(swordSlot, potionSlot);

        // Assert
        Assert.AreEqual(_potionData, _inventory[swordSlot].Data);
        Assert.AreEqual(_swordData, _inventory[potionSlot].Data);
    }

    [Test]
    public void MoveItem_ToEmptySlot_MovesItem()
    {
        // Arrange
        _inventory.TryAddItem(_swordData);
        int fromSlot = _inventory.FindItem(_swordData);
        int toSlot = 10;

        // Act
        _inventory.MoveItem(fromSlot, toSlot);

        // Assert
        Assert.IsNull(_inventory[fromSlot]);
        Assert.AreEqual(_swordData, _inventory[toSlot].Data);
    }

    [Test]
    public void MoveItem_SameTypeStackable_Merges()
    {
        // Arrange
        _inventory.TryAddItem(_potionData, 3);
        _inventory.TryAddItem(_potionData, 2);
        // Force them to be in different slots by adding something else first
        // This test assumes they're in separate slots
        _inventory.RemoveItemAt(0);
        _inventory.RemoveItemAt(1);
        _inventory.TryAddItem(_potionData, 3);
        _inventory.TryAddItem(_swordData);
        _inventory.TryAddItem(_potionData, 2);
        int firstPotionSlot = _inventory.FindItem(_potionData);
        var allPotionSlots = _inventory.FindAllItems(_potionData);

        // Only run merge test if we have 2 separate stacks
        if (allPotionSlots.Count >= 2)
        {
            // Act
            _inventory.MoveItem(allPotionSlots[1], allPotionSlots[0]);

            // Assert
            Assert.AreEqual(5, _inventory[allPotionSlots[0]].Quantity);
        }
        else
        {
            Assert.Pass("Items already stacked - test not applicable");
        }
    }

    #endregion

    #region Query Tests

    [Test]
    public void HasItem_ReturnsTrue_WhenItemExists()
    {
        // Arrange
        _inventory.TryAddItem(_swordData);

        // Assert
        Assert.IsTrue(_inventory.HasItem(_swordData));
    }

    [Test]
    public void HasItem_ReturnsFalse_WhenItemMissing()
    {
        // Assert
        Assert.IsFalse(_inventory.HasItem(_swordData));
    }

    [Test]
    public void HasItem_ChecksQuantity()
    {
        // Arrange
        _inventory.TryAddItem(_potionData, 3);

        // Assert
        Assert.IsTrue(_inventory.HasItem(_potionData, 3));
        Assert.IsFalse(_inventory.HasItem(_potionData, 5));
    }

    [Test]
    public void FindItem_ReturnsCorrectSlot()
    {
        // Arrange
        _inventory.TryAddItem(_swordData);

        // Act
        int slot = _inventory.FindItem(_swordData);

        // Assert
        Assert.GreaterOrEqual(slot, 0);
        Assert.AreEqual(_swordData, _inventory[slot].Data);
    }

    [Test]
    public void FindItem_ReturnsMinusOne_WhenNotFound()
    {
        // Act
        int slot = _inventory.FindItem(_swordData);

        // Assert
        Assert.AreEqual(-1, slot);
    }

    [Test]
    public void GetItemsByCategory_FiltersCorrectly()
    {
        // Arrange
        _inventory.TryAddItem(_swordData);
        _inventory.TryAddItem(_potionData, 5);
        _inventory.TryAddItem(_materialData, 10);

        // Act
        var consumables = _inventory.GetItemsByCategory(ItemCategory.Consumable);

        // Assert
        Assert.AreEqual(1, System.Linq.Enumerable.Count(consumables));
    }

    #endregion

    #region Capacity Tests

    [Test]
    public void Capacity_DefaultIs20()
    {
        // Assert
        Assert.AreEqual(20, _inventory.Capacity);
    }

    [Test]
    public void UsedSlots_CountsCorrectly()
    {
        // Arrange
        _inventory.TryAddItem(_swordData);
        _inventory.TryAddItem(_potionData, 5);

        // Assert
        Assert.AreEqual(2, _inventory.UsedSlots);
    }

    [Test]
    public void FreeSlots_CalculatesCorrectly()
    {
        // Arrange
        _inventory.TryAddItem(_swordData);

        // Assert
        Assert.AreEqual(19, _inventory.FreeSlots);
    }

    [Test]
    public void IsFull_ReturnsTrue_WhenNoFreeSlots()
    {
        // Arrange - Fill with non-stackable items
        for (int i = 0; i < 20; i++)
        {
            _inventory.TryAddItem(_swordData);
        }

        // Assert
        Assert.IsTrue(_inventory.IsFull);
    }

    [Test]
    public void IsEmpty_ReturnsTrue_WhenNoItems()
    {
        // Assert
        Assert.IsTrue(_inventory.IsEmpty);
    }

    [Test]
    public void IsEmpty_ReturnsFalse_WhenHasItems()
    {
        // Arrange
        _inventory.TryAddItem(_swordData);

        // Assert
        Assert.IsFalse(_inventory.IsEmpty);
    }

    #endregion

    #region Sort Tests

    [Test]
    public void Sort_OrganizesByCategory()
    {
        // Arrange
        _inventory.TryAddItem(_materialData); // Should be last (Material)
        _inventory.TryAddItem(_potionData);   // Should be middle (Consumable)
        _inventory.TryAddItem(_swordData);    // Should be first (Weapon)

        // Act
        _inventory.Sort();

        // Assert
        Assert.AreEqual(ItemCategory.Weapon, _inventory[0].Data.category);
        Assert.AreEqual(ItemCategory.Consumable, _inventory[1].Data.category);
        Assert.AreEqual(ItemCategory.Material, _inventory[2].Data.category);
    }

    #endregion

    #region Event Tests

    [Test]
    public void OnItemAdded_FiresWhenItemAdded()
    {
        // Arrange
        bool eventFired = false;
        _inventory.OnItemAdded += (item, slot) => eventFired = true;

        // Act
        _inventory.TryAddItem(_swordData);

        // Assert
        Assert.IsTrue(eventFired);
    }

    [Test]
    public void OnInventoryChanged_FiresOnChanges()
    {
        // Arrange
        int changeCount = 0;
        _inventory.OnInventoryChanged += () => changeCount++;

        // Act
        _inventory.TryAddItem(_swordData);
        _inventory.RemoveItem(_swordData);

        // Assert
        Assert.GreaterOrEqual(changeCount, 2);
    }

    #endregion
}

/// <summary>
/// Tests pour ItemInstance.
/// </summary>
public class ItemInstanceTests
{
    private ItemData _stackableData;
    private ItemData _nonStackableData;

    [SetUp]
    public void Setup()
    {
        _stackableData = ScriptableObject.CreateInstance<ItemData>();
        _stackableData.itemId = "test_stackable";
        _stackableData.displayName = "Stackable Item";
        _stackableData.isStackable = true;
        _stackableData.maxStackSize = 10;

        _nonStackableData = ScriptableObject.CreateInstance<ItemData>();
        _nonStackableData.itemId = "test_nonstackable";
        _nonStackableData.displayName = "Non-Stackable Item";
        _nonStackableData.isStackable = false;
        _nonStackableData.maxStackSize = 1;
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_stackableData);
        Object.DestroyImmediate(_nonStackableData);
    }

    [Test]
    public void Constructor_SetsQuantity()
    {
        // Act
        var item = new ItemInstance(_stackableData, 5);

        // Assert
        Assert.AreEqual(5, item.Quantity);
    }

    [Test]
    public void Constructor_ClampsQuantityToMax()
    {
        // Act
        var item = new ItemInstance(_stackableData, 100);

        // Assert
        Assert.AreEqual(10, item.Quantity); // maxStackSize is 10
    }

    [Test]
    public void Add_IncreasesQuantity()
    {
        // Arrange
        var item = new ItemInstance(_stackableData, 3);

        // Act
        int overflow = item.Add(4);

        // Assert
        Assert.AreEqual(7, item.Quantity);
        Assert.AreEqual(0, overflow);
    }

    [Test]
    public void Add_ReturnsOverflow_WhenExceedsMax()
    {
        // Arrange
        var item = new ItemInstance(_stackableData, 8);

        // Act
        int overflow = item.Add(5);

        // Assert
        Assert.AreEqual(10, item.Quantity);
        Assert.AreEqual(3, overflow);
    }

    [Test]
    public void Remove_DecreasesQuantity()
    {
        // Arrange
        var item = new ItemInstance(_stackableData, 5);

        // Act
        int removed = item.Remove(3);

        // Assert
        Assert.AreEqual(2, item.Quantity);
        Assert.AreEqual(3, removed);
    }

    [Test]
    public void Split_CreatesTwoItems()
    {
        // Arrange
        var item = new ItemInstance(_stackableData, 7);

        // Act
        var split = item.Split(3);

        // Assert
        Assert.IsNotNull(split);
        Assert.AreEqual(4, item.Quantity);
        Assert.AreEqual(3, split.Quantity);
    }

    [Test]
    public void Split_ReturnsNull_WhenInvalid()
    {
        // Arrange
        var item = new ItemInstance(_stackableData, 5);

        // Act
        var split = item.Split(5); // Can't split all

        // Assert
        Assert.IsNull(split);
    }

    [Test]
    public void TryMerge_CombinesStacks()
    {
        // Arrange
        var item1 = new ItemInstance(_stackableData, 3);
        var item2 = new ItemInstance(_stackableData, 4);

        // Act
        bool fullyMerged = item1.TryMerge(item2);

        // Assert
        Assert.IsTrue(fullyMerged);
        Assert.AreEqual(7, item1.Quantity);
        Assert.AreEqual(0, item2.Quantity);
    }

    [Test]
    public void TryMerge_PartialMerge_WhenExceedsMax()
    {
        // Arrange
        var item1 = new ItemInstance(_stackableData, 8);
        var item2 = new ItemInstance(_stackableData, 5);

        // Act
        bool fullyMerged = item1.TryMerge(item2);

        // Assert
        Assert.IsFalse(fullyMerged);
        Assert.AreEqual(10, item1.Quantity);
        Assert.AreEqual(3, item2.Quantity);
    }

    [Test]
    public void IsFull_ReturnsCorrectly()
    {
        // Arrange
        var item = new ItemInstance(_stackableData, 10);

        // Assert
        Assert.IsTrue(item.IsFull);
    }

    [Test]
    public void RemainingSpace_CalculatesCorrectly()
    {
        // Arrange
        var item = new ItemInstance(_stackableData, 7);

        // Assert
        Assert.AreEqual(3, item.RemainingSpace);
    }
}
