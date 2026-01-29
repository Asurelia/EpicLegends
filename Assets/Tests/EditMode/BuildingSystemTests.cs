using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests unitaires pour le systeme de construction.
/// </summary>
public class BuildingSystemTests
{
    #region BuildingData Tests

    [Test]
    public void BuildingData_CanBeCreated()
    {
        // Act
        var data = ScriptableObject.CreateInstance<BuildingData>();

        // Assert
        Assert.IsNotNull(data);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void BuildingData_HasRequiredFields()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.buildingName = "Test Building";
        data.category = BuildingCategory.Structure;
        data.gridSize = new Vector2Int(2, 2);

        // Assert
        Assert.AreEqual("Test Building", data.buildingName);
        Assert.AreEqual(BuildingCategory.Structure, data.category);
        Assert.AreEqual(new Vector2Int(2, 2), data.gridSize);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void BuildingCategory_HasAllCategories()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(BuildingCategory), "Structure"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(BuildingCategory), "Production"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(BuildingCategory), "Storage"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(BuildingCategory), "Defense"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(BuildingCategory), "Utility"));
    }

    [Test]
    public void BuildingTier_HasAllTiers()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(BuildingTier), "Wood"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(BuildingTier), "Stone"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(BuildingTier), "Metal"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(BuildingTier), "Tech"));
    }

    #endregion

    #region BuildingGrid Tests

    private GameObject _gridObject;
    private BuildingGrid _grid;

    [SetUp]
    public void SetupGrid()
    {
        _gridObject = new GameObject("BuildingGrid");
        _grid = _gridObject.AddComponent<BuildingGrid>();

        // Appeler Awake
        var awakeMethod = typeof(BuildingGrid).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(_grid, null);
    }

    [TearDown]
    public void TeardownGrid()
    {
        Object.DestroyImmediate(_gridObject);
    }

    [Test]
    public void BuildingGrid_CanBeCreated()
    {
        // Assert
        Assert.IsNotNull(_grid);
    }

    [Test]
    public void BuildingGrid_HasCellSize()
    {
        // Assert
        Assert.Greater(_grid.CellSize, 0f);
    }

    [Test]
    public void BuildingGrid_CanConvertWorldToGrid()
    {
        // Arrange
        Vector3 worldPos = new Vector3(5f, 0f, 5f);

        // Act
        Vector2Int gridPos = _grid.WorldToGrid(worldPos);

        // Assert
        Assert.IsNotNull(gridPos);
    }

    [Test]
    public void BuildingGrid_CanConvertGridToWorld()
    {
        // Arrange
        Vector2Int gridPos = new Vector2Int(2, 3);

        // Act
        Vector3 worldPos = _grid.GridToWorld(gridPos);

        // Assert
        Assert.IsNotNull(worldPos);
    }

    [Test]
    public void BuildingGrid_CanCheckOccupancy()
    {
        // Arrange
        Vector2Int gridPos = new Vector2Int(0, 0);

        // Act
        bool isOccupied = _grid.IsCellOccupied(gridPos);

        // Assert - initialement vide
        Assert.IsFalse(isOccupied);
    }

    [Test]
    public void BuildingGrid_CanOccupyCells()
    {
        // Arrange
        Vector2Int gridPos = new Vector2Int(0, 0);
        Vector2Int size = new Vector2Int(2, 2);
        var building = new GameObject("TestBuilding");

        // Act
        _grid.OccupyCells(gridPos, size, building);
        bool isOccupied = _grid.IsCellOccupied(gridPos);

        // Assert
        Assert.IsTrue(isOccupied);

        // Cleanup
        Object.DestroyImmediate(building);
    }

    [Test]
    public void BuildingGrid_CanFreeCells()
    {
        // Arrange
        Vector2Int gridPos = new Vector2Int(0, 0);
        Vector2Int size = new Vector2Int(2, 2);
        var building = new GameObject("TestBuilding");
        _grid.OccupyCells(gridPos, size, building);

        // Act
        _grid.FreeCells(gridPos, size);
        bool isOccupied = _grid.IsCellOccupied(gridPos);

        // Assert
        Assert.IsFalse(isOccupied);

        // Cleanup
        Object.DestroyImmediate(building);
    }

    #endregion

    #region BuildingPlacer Tests

    private GameObject _placerObject;
    private BuildingPlacer _placer;

    [Test]
    public void BuildingPlacer_CanBeCreated()
    {
        // Arrange
        _placerObject = new GameObject("BuildingPlacer");
        _placer = _placerObject.AddComponent<BuildingPlacer>();

        // Assert
        Assert.IsNotNull(_placer);

        // Cleanup
        Object.DestroyImmediate(_placerObject);
    }

    [Test]
    public void BuildingPlacer_CanSelectBuilding()
    {
        // Arrange
        _placerObject = new GameObject("BuildingPlacer");
        _placer = _placerObject.AddComponent<BuildingPlacer>();
        var data = ScriptableObject.CreateInstance<BuildingData>();

        // Act
        _placer.SelectBuilding(data);

        // Assert
        Assert.AreEqual(data, _placer.SelectedBuilding);

        // Cleanup
        Object.DestroyImmediate(data);
        Object.DestroyImmediate(_placerObject);
    }

    [Test]
    public void BuildingPlacer_CanRotate()
    {
        // Arrange
        _placerObject = new GameObject("BuildingPlacer");
        _placer = _placerObject.AddComponent<BuildingPlacer>();

        // Appeler Awake
        var awakeMethod = typeof(BuildingPlacer).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(_placer, null);

        // Creer un BuildingData avec rotation activee
        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.canRotate = true;
        data.rotationAngles = new int[] { 0, 90, 180, 270 };

        // Selectionner le batiment pour activer la rotation
        _placer.SelectBuilding(data);
        int initialRotation = _placer.CurrentRotation;

        // Act
        _placer.RotateBuilding();
        int newRotation = _placer.CurrentRotation;

        // Assert
        Assert.AreNotEqual(initialRotation, newRotation);

        // Cleanup
        _placer.CancelPlacement();
        Object.DestroyImmediate(data);
        Object.DestroyImmediate(_placerObject);
    }

    #endregion

    #region PlacementValidator Tests

    [Test]
    public void PlacementValidator_CanValidate()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.gridSize = new Vector2Int(1, 1);
        data.requiresFoundation = false;

        var gridObject = new GameObject("Grid");
        var grid = gridObject.AddComponent<BuildingGrid>();

        var awakeMethod = typeof(BuildingGrid).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(grid, null);

        // Act
        bool isValid = PlacementValidator.CanPlace(data, Vector2Int.zero, 0, grid);

        // Assert
        Assert.IsTrue(isValid);

        // Cleanup
        Object.DestroyImmediate(data);
        Object.DestroyImmediate(gridObject);
    }

    [Test]
    public void PlacementValidator_DetectsOccupiedCells()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.gridSize = new Vector2Int(1, 1);

        var gridObject = new GameObject("Grid");
        var grid = gridObject.AddComponent<BuildingGrid>();

        var awakeMethod = typeof(BuildingGrid).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(grid, null);

        // Occuper la cellule
        var blocking = new GameObject("Blocker");
        grid.OccupyCells(Vector2Int.zero, Vector2Int.one, blocking);

        // Act
        bool isValid = PlacementValidator.CanPlace(data, Vector2Int.zero, 0, grid);

        // Assert
        Assert.IsFalse(isValid);

        // Cleanup
        Object.DestroyImmediate(data);
        Object.DestroyImmediate(blocking);
        Object.DestroyImmediate(gridObject);
    }

    #endregion

    #region Building Tests

    [Test]
    public void Building_CanBeCreated()
    {
        // Arrange
        var go = new GameObject("Building");
        var building = go.AddComponent<Building>();

        // Assert
        Assert.IsNotNull(building);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Building_CanBeInitialized()
    {
        // Arrange
        var go = new GameObject("Building");
        var building = go.AddComponent<Building>();
        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.buildingName = "Test";

        // Act
        building.Initialize(data, Vector2Int.zero, 0);

        // Assert
        Assert.AreEqual(data, building.Data);
        Assert.AreEqual(Vector2Int.zero, building.GridPosition);

        // Cleanup
        Object.DestroyImmediate(data);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Building_TracksHealthAndTier()
    {
        // Arrange
        var go = new GameObject("Building");
        var building = go.AddComponent<Building>();
        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.maxHealth = 100f;
        data.baseTier = BuildingTier.Wood;

        building.Initialize(data, Vector2Int.zero, 0);

        // Assert
        Assert.AreEqual(100f, building.CurrentHealth);
        Assert.AreEqual(BuildingTier.Wood, building.CurrentTier);

        // Cleanup
        Object.DestroyImmediate(data);
        Object.DestroyImmediate(go);
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

    #region StorageBuilding Tests

    [Test]
    public void StorageBuilding_CanBeCreated()
    {
        // Arrange
        var go = new GameObject("Storage");
        var storage = go.AddComponent<StorageBuilding>();

        // Assert
        Assert.IsNotNull(storage);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void StorageBuilding_CanAddResource()
    {
        // Arrange
        var go = new GameObject("Storage");
        var storage = go.AddComponent<StorageBuilding>();
        storage.Configure(10, 100);

        // Appeler Awake
        var awakeMethod = typeof(StorageBuilding).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(storage, null);

        // Act
        bool added = storage.AddResource(ResourceType.Wood, 50);

        // Assert
        Assert.IsTrue(added);
        Assert.AreEqual(50, storage.GetResourceCount(ResourceType.Wood));

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void StorageBuilding_CanRemoveResource()
    {
        // Arrange
        var go = new GameObject("Storage");
        var storage = go.AddComponent<StorageBuilding>();
        storage.Configure(10, 100);

        var awakeMethod = typeof(StorageBuilding).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(storage, null);

        storage.AddResource(ResourceType.Stone, 30);

        // Act
        bool removed = storage.RemoveResource(ResourceType.Stone, 10);

        // Assert
        Assert.IsTrue(removed);
        Assert.AreEqual(20, storage.GetResourceCount(ResourceType.Stone));

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void StorageBuilding_RejectsOverCapacity()
    {
        // Arrange
        var go = new GameObject("Storage");
        var storage = go.AddComponent<StorageBuilding>();
        storage.Configure(1, 50);

        var awakeMethod = typeof(StorageBuilding).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(storage, null);

        // Act
        bool added = storage.AddResource(ResourceType.Wood, 100);

        // Assert
        Assert.IsFalse(added);
        Assert.AreEqual(0, storage.GetResourceCount(ResourceType.Wood));

        // Cleanup
        Object.DestroyImmediate(go);
    }

    #endregion

    #region ProductionBuilding Tests

    [Test]
    public void ProductionBuilding_CanBeCreated()
    {
        // Arrange
        var go = new GameObject("Production");
        var production = go.AddComponent<ProductionBuilding>();

        // Assert
        Assert.IsNotNull(production);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void ProductionBuilding_CanConfigure()
    {
        // Arrange
        var go = new GameObject("Production");
        var production = go.AddComponent<ProductionBuilding>();

        // Act
        production.Configure(BuildingSubCategory.Furnace, 2, 10);

        // Assert
        Assert.AreEqual(BuildingSubCategory.Furnace, production.StationType);
        Assert.AreEqual(2, production.StationLevel);
        Assert.AreEqual(10, production.QueueSize);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void ProductionBuilding_CanQueueRecipe()
    {
        // Arrange
        var go = new GameObject("Production");
        var production = go.AddComponent<ProductionBuilding>();
        production.Configure(BuildingSubCategory.Workbench, 1, 5);

        var awakeMethod = typeof(ProductionBuilding).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(production, null);

        var recipe = ScriptableObject.CreateInstance<CraftingRecipeData>();
        recipe.requiredStation = BuildingSubCategory.Workbench;
        recipe.requiredStationLevel = 1;

        // Act
        bool queued = production.QueueRecipe(recipe);

        // Assert
        Assert.IsTrue(queued);
        Assert.AreEqual(1, production.QueueCount);

        // Cleanup
        Object.DestroyImmediate(recipe);
        Object.DestroyImmediate(go);
    }

    #endregion

    #region DefenseTower Tests

    [Test]
    public void DefenseTower_CanBeCreated()
    {
        // Arrange
        var go = new GameObject("Tower");
        var tower = go.AddComponent<DefenseTower>();

        // Assert
        Assert.IsNotNull(tower);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void DefenseTower_CanConfigure()
    {
        // Arrange
        var go = new GameObject("Tower");
        var tower = go.AddComponent<DefenseTower>();

        // Act
        tower.Configure(TowerType.Cannon, 15f, 50f, 0.5f);

        // Assert
        Assert.AreEqual(TowerType.Cannon, tower.TowerType);
        Assert.AreEqual(15f, tower.Range);
        Assert.AreEqual(50f, tower.Damage);
        Assert.AreEqual(0.5f, tower.FireRate);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void DefenseTower_CanSetTargetingMode()
    {
        // Arrange
        var go = new GameObject("Tower");
        var tower = go.AddComponent<DefenseTower>();

        // Act
        tower.SetTargetingMode(TargetingMode.LowestHealth);

        // Assert - pas d'exception = succes
        Assert.IsTrue(true);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void TowerType_HasAllTypes()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(TowerType), "Arrow"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(TowerType), "Cannon"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(TowerType), "Magic"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(TowerType), "Frost"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(TowerType), "Lightning"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(TowerType), "Support"));
    }

    #endregion

    #region PowerGenerator Tests

    [Test]
    public void PowerGenerator_CanBeCreated()
    {
        // Arrange
        var go = new GameObject("Generator");
        var generator = go.AddComponent<PowerGenerator>();

        // Assert
        Assert.IsNotNull(generator);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void PowerGenerator_CanConfigure()
    {
        // Arrange
        var go = new GameObject("Generator");
        var generator = go.AddComponent<PowerGenerator>();

        // Act
        generator.Configure(200f, 20f, GeneratorType.Solar);

        // Assert
        Assert.AreEqual(200f, generator.PowerOutput);
        Assert.AreEqual(20f, generator.Range);
        Assert.AreEqual(GeneratorType.Solar, generator.GeneratorType);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void GeneratorType_HasAllTypes()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(GeneratorType), "Manual"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(GeneratorType), "Fuel"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(GeneratorType), "Solar"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(GeneratorType), "Wind"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(GeneratorType), "Geothermal"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(GeneratorType), "Reactor"));
    }

    #endregion

    #region Projectile Tests

    [Test]
    public void Projectile_CanBeCreated()
    {
        // Arrange
        var go = new GameObject("Projectile");
        var projectile = go.AddComponent<Projectile>();

        // Assert
        Assert.IsNotNull(projectile);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    #endregion

    #region BuildingUpgradeManager Tests

    [Test]
    public void BuildingUpgradeManager_CanBeCreated()
    {
        // Arrange
        var go = new GameObject("UpgradeManager");
        var manager = go.AddComponent<BuildingUpgradeManager>();

        // Assert
        Assert.IsNotNull(manager);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void BuildingUpgradeManager_CanCheckUpgradeability()
    {
        // Arrange
        var managerGO = new GameObject("UpgradeManager");
        var manager = managerGO.AddComponent<BuildingUpgradeManager>();

        var buildingGO = new GameObject("Building");
        var building = buildingGO.AddComponent<Building>();
        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.canUpgrade = true;
        data.baseTier = BuildingTier.Wood;
        data.maxTier = BuildingTier.Tech;
        data.buildTime = 0f;
        building.Initialize(data, Vector2Int.zero, 0);

        // Act
        bool canUpgrade = manager.CanUpgrade(building, BuildingTier.Stone);

        // Assert
        Assert.IsTrue(canUpgrade);

        // Cleanup
        Object.DestroyImmediate(data);
        Object.DestroyImmediate(buildingGO);
        Object.DestroyImmediate(managerGO);
    }

    [Test]
    public void BuildingUpgradeManager_CalculatesUpgradeCost()
    {
        // Arrange
        var managerGO = new GameObject("UpgradeManager");
        var manager = managerGO.AddComponent<BuildingUpgradeManager>();

        var buildingGO = new GameObject("Building");
        var building = buildingGO.AddComponent<Building>();
        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.canUpgrade = true;
        data.baseTier = BuildingTier.Wood;
        data.maxTier = BuildingTier.Tech;
        data.buildTime = 0f;
        data.buildCosts = new ResourceCost[]
        {
            new ResourceCost { resourceType = ResourceType.Wood, amount = 10 }
        };
        building.Initialize(data, Vector2Int.zero, 0);

        // Act
        var costs = manager.GetUpgradeCost(building, BuildingTier.Stone);

        // Assert
        Assert.IsNotNull(costs);
        Assert.Greater(costs.Length, 0);

        // Cleanup
        Object.DestroyImmediate(data);
        Object.DestroyImmediate(buildingGO);
        Object.DestroyImmediate(managerGO);
    }

    [Test]
    public void BuildingUpgradeManager_CalculatesUpgradeTime()
    {
        // Arrange
        var managerGO = new GameObject("UpgradeManager");
        var manager = managerGO.AddComponent<BuildingUpgradeManager>();

        var buildingGO = new GameObject("Building");
        var building = buildingGO.AddComponent<Building>();
        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.canUpgrade = true;
        data.baseTier = BuildingTier.Wood;
        data.maxTier = BuildingTier.Tech;
        data.buildTime = 5f;
        building.InstantBuild(); // Forcer la fin de la construction
        SetField(building, "_data", data);
        SetField(building, "_currentTier", BuildingTier.Wood);

        // Act
        float time = manager.GetUpgradeTime(building, BuildingTier.Stone);

        // Assert
        Assert.Greater(time, 0f);

        // Cleanup
        Object.DestroyImmediate(data);
        Object.DestroyImmediate(buildingGO);
        Object.DestroyImmediate(managerGO);
    }

    #endregion

    #region TierVisualConfig Tests

    [Test]
    public void TierVisualConfig_CanBeCreated()
    {
        // Arrange
        var config = ScriptableObject.CreateInstance<TierVisualConfig>();

        // Assert
        Assert.IsNotNull(config);

        // Cleanup
        Object.DestroyImmediate(config);
    }

    [Test]
    public void TierVisualConfig_HasColorsForAllTiers()
    {
        // Arrange
        var config = ScriptableObject.CreateInstance<TierVisualConfig>();

        // Act & Assert
        Assert.AreNotEqual(Color.clear, config.GetColor(BuildingTier.Wood));
        Assert.AreNotEqual(Color.clear, config.GetColor(BuildingTier.Stone));
        Assert.AreNotEqual(Color.clear, config.GetColor(BuildingTier.Metal));
        Assert.AreNotEqual(Color.clear, config.GetColor(BuildingTier.Tech));

        // Cleanup
        Object.DestroyImmediate(config);
    }

    [Test]
    public void TierVisualConfig_HasStatsForAllTiers()
    {
        // Arrange
        var config = ScriptableObject.CreateInstance<TierVisualConfig>();

        // Act & Assert
        var woodStats = config.GetStats(BuildingTier.Wood);
        var techStats = config.GetStats(BuildingTier.Tech);

        Assert.Greater(woodStats.healthMultiplier, 0f);
        Assert.Greater(techStats.healthMultiplier, woodStats.healthMultiplier);

        // Cleanup
        Object.DestroyImmediate(config);
    }

    #endregion

    #region Building Upgrade Tests

    [Test]
    public void Building_CanBeUpgraded()
    {
        // Arrange
        var go = new GameObject("Building");
        var building = go.AddComponent<Building>();
        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.canUpgrade = true;
        data.baseTier = BuildingTier.Wood;
        data.maxTier = BuildingTier.Tech;
        data.maxHealth = 100f;
        data.buildTime = 0f; // Pas de temps de construction
        building.Initialize(data, Vector2Int.zero, 0);

        // Act
        bool upgraded = building.Upgrade(BuildingTier.Stone);

        // Assert
        Assert.IsTrue(upgraded);
        Assert.AreEqual(BuildingTier.Stone, building.CurrentTier);

        // Cleanup
        Object.DestroyImmediate(data);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Building_RejectsDowngrade()
    {
        // Arrange
        var go = new GameObject("Building");
        var building = go.AddComponent<Building>();
        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.canUpgrade = true;
        data.baseTier = BuildingTier.Stone;
        data.maxTier = BuildingTier.Tech;
        data.buildTime = 0f;
        building.Initialize(data, Vector2Int.zero, 0);

        // Act
        bool downgraded = building.Upgrade(BuildingTier.Wood);

        // Assert
        Assert.IsFalse(downgraded);
        Assert.AreEqual(BuildingTier.Stone, building.CurrentTier);

        // Cleanup
        Object.DestroyImmediate(data);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Building_RejectsUpgradeBeyondMax()
    {
        // Arrange
        var go = new GameObject("Building");
        var building = go.AddComponent<Building>();
        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.canUpgrade = true;
        data.baseTier = BuildingTier.Wood;
        data.maxTier = BuildingTier.Stone;
        data.buildTime = 0f;
        building.Initialize(data, Vector2Int.zero, 0);

        // Act - essayer d'upgrader au-dela du max
        bool upgraded = building.Upgrade(BuildingTier.Metal);

        // Assert
        Assert.IsFalse(upgraded);

        // Cleanup
        Object.DestroyImmediate(data);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Building_HealthIncreasesOnUpgrade()
    {
        // Arrange
        var go = new GameObject("Building");
        var building = go.AddComponent<Building>();
        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.canUpgrade = true;
        data.baseTier = BuildingTier.Wood;
        data.maxTier = BuildingTier.Tech;
        data.maxHealth = 100f;
        data.buildTime = 0f;
        building.Initialize(data, Vector2Int.zero, 0);

        float initialHealth = building.MaxHealth;

        // Act
        building.Upgrade(BuildingTier.Stone);

        // Assert
        Assert.Greater(building.MaxHealth, initialHealth);

        // Cleanup
        Object.DestroyImmediate(data);
        Object.DestroyImmediate(go);
    }

    #endregion
}
