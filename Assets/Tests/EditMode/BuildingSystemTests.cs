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
}
