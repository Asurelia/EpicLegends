using NUnit.Framework;
using UnityEngine;
using System.Reflection;

/// <summary>
/// Tests pour le CraftingManager.
/// </summary>
[TestFixture]
public class CraftingManagerTests
{
    private GameObject _managerObj;
    private CraftingManager _manager;

    [SetUp]
    public void SetUp()
    {
        // Reset singleton
        var instanceProp = typeof(CraftingManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        instanceProp?.SetValue(null, null);

        _managerObj = new GameObject("CraftingManager");
        _manager = _managerObj.AddComponent<CraftingManager>();

        // Set singleton manually
        instanceProp?.SetValue(null, _manager);

        // Initialize internal fields via reflection
        var unlockedField = typeof(CraftingManager).GetField("_unlockedRecipes", BindingFlags.NonPublic | BindingFlags.Instance);
        unlockedField?.SetValue(_manager, new System.Collections.Generic.HashSet<string>());

        var craftedField = typeof(CraftingManager).GetField("_craftedCounts", BindingFlags.NonPublic | BindingFlags.Instance);
        craftedField?.SetValue(_manager, new System.Collections.Generic.Dictionary<string, int>());

        var queueField = typeof(CraftingManager).GetField("_craftingQueue", BindingFlags.NonPublic | BindingFlags.Instance);
        queueField?.SetValue(_manager, new System.Collections.Generic.Queue<CraftingJob>());
    }

    [TearDown]
    public void TearDown()
    {
        // Reset singleton
        var instanceProp = typeof(CraftingManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        instanceProp?.SetValue(null, null);

        if (_managerObj != null)
        {
            Object.DestroyImmediate(_managerObj);
        }
    }

    [Test]
    public void CraftingManager_Singleton_IsSet()
    {
        Assert.IsNotNull(CraftingManager.Instance);
        Assert.AreEqual(_manager, CraftingManager.Instance);
    }

    [Test]
    public void CraftingManager_InitialState_NotCrafting()
    {
        Assert.IsFalse(_manager.IsCrafting);
        Assert.IsNull(_manager.CurrentJob);
        Assert.AreEqual(0, _manager.QueuedJobCount);
    }

    [Test]
    public void CraftingManager_GetCraftingProgress_ReturnsZero_WhenNotCrafting()
    {
        float progress = _manager.GetCraftingProgress();
        Assert.AreEqual(0f, progress);
    }

    [Test]
    public void CraftingManager_CanCraft_ReturnsFalse_WithNullRecipe()
    {
        bool canCraft = _manager.CanCraft(null, null);
        Assert.IsFalse(canCraft);
    }

    [Test]
    public void CraftingManager_CanCraft_ReturnsFalse_WithNullResources()
    {
        var recipe = ScriptableObject.CreateInstance<CraftingRecipeData>();
        recipe.unlockedByDefault = true;

        bool canCraft = _manager.CanCraft(recipe, null);

        Assert.IsFalse(canCraft);

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void CraftingManager_IsRecipeUnlocked_ReturnsFalse_WithNullRecipe()
    {
        bool isUnlocked = _manager.IsRecipeUnlocked(null);
        Assert.IsFalse(isUnlocked);
    }

    [Test]
    public void CraftingManager_IsRecipeUnlocked_ReturnsTrue_WhenUnlockedByDefault()
    {
        var recipe = ScriptableObject.CreateInstance<CraftingRecipeData>();
        recipe.unlockedByDefault = true;

        bool isUnlocked = _manager.IsRecipeUnlocked(recipe);

        Assert.IsTrue(isUnlocked);

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void CraftingManager_UnlockRecipe_UnlocksRecipe()
    {
        var recipe = ScriptableObject.CreateInstance<CraftingRecipeData>();
        recipe.recipeName = "TestRecipe";
        recipe.unlockedByDefault = false;

        Assert.IsFalse(_manager.IsRecipeUnlocked(recipe));

        _manager.UnlockRecipe(recipe);

        Assert.IsTrue(_manager.IsRecipeUnlocked(recipe));

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void CraftingManager_GetCraftedCount_ReturnsZero_ForNewRecipe()
    {
        var recipe = ScriptableObject.CreateInstance<CraftingRecipeData>();
        recipe.recipeName = "TestRecipe";

        int count = _manager.GetCraftedCount(recipe);

        Assert.AreEqual(0, count);

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void CraftingManager_GetCraftedCount_ReturnsZero_WithNullRecipe()
    {
        int count = _manager.GetCraftedCount(null);
        Assert.AreEqual(0, count);
    }

    [Test]
    public void CraftingManager_CancelCrafting_DoesNothing_WhenNotCrafting()
    {
        // Should not throw
        _manager.CancelCrafting();

        Assert.IsFalse(_manager.IsCrafting);
    }

    [Test]
    public void CraftingManager_GetSaveData_ReturnsValidData()
    {
        var saveData = _manager.GetSaveData();

        Assert.IsNotNull(saveData);
        Assert.IsNotNull(saveData.unlockedRecipes);
        Assert.IsNotNull(saveData.craftedCounts);
    }

    [Test]
    public void CraftingManager_LoadSaveData_HandlesNull()
    {
        // Should not throw
        _manager.LoadSaveData(null);
    }

    [Test]
    public void CraftingManager_LoadSaveData_RestoresState()
    {
        var recipe = ScriptableObject.CreateInstance<CraftingRecipeData>();
        recipe.recipeName = "SavedRecipe";
        recipe.unlockedByDefault = false;

        var saveData = new CraftingSaveData
        {
            unlockedRecipes = new System.Collections.Generic.List<string> { "SavedRecipe" },
            craftedCounts = new System.Collections.Generic.Dictionary<string, int> { { "SavedRecipe", 5 } }
        };

        _manager.LoadSaveData(saveData);

        Assert.IsTrue(_manager.IsRecipeUnlocked(recipe));
        Assert.AreEqual(5, _manager.GetCraftedCount(recipe));

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void CraftingManager_CanCraft_NoArg_ReturnsFalse_WhenNoResourceManager()
    {
        var recipe = ScriptableObject.CreateInstance<CraftingRecipeData>();
        recipe.recipeName = "TestRecipe";
        recipe.unlockedByDefault = true;

        bool canCraft = _manager.CanCraft(recipe);

        Assert.IsFalse(canCraft);

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void CraftingManager_GetMissingIngredients_ReturnsEmptyArray_ForNullRecipe()
    {
        var missing = _manager.GetMissingIngredients(null);

        Assert.IsNotNull(missing);
        Assert.AreEqual(0, missing.Length);
    }

    [Test]
    public void CraftingManager_GetMissingIngredients_ReturnsAllIngredients_WhenNoResourceManager()
    {
        var recipe = ScriptableObject.CreateInstance<CraftingRecipeData>();
        recipe.recipeName = "TestRecipe";
        recipe.ingredients = new ResourceCost[]
        {
            new ResourceCost { resourceType = ResourceType.Wood, amount = 10 }
        };

        var missing = _manager.GetMissingIngredients(recipe);

        Assert.IsNotNull(missing);
        Assert.AreEqual(1, missing.Length);
        Assert.AreEqual(ResourceType.Wood, missing[0].resourceType);
        Assert.AreEqual(10, missing[0].amount);

        Object.DestroyImmediate(recipe);
    }

    [Test]
    public void CraftingManager_StartCrafting_NoArg_ReturnsFalse_WhenNoResourceManager()
    {
        var recipe = ScriptableObject.CreateInstance<CraftingRecipeData>();
        recipe.recipeName = "TestRecipe";
        recipe.unlockedByDefault = true;

        bool result = _manager.StartCrafting(recipe);

        Assert.IsFalse(result);

        Object.DestroyImmediate(recipe);
    }
}
