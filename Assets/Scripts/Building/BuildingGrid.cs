using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grille de placement pour les batiments.
/// Gere les cellules occupees et les conversions de coordonnees.
/// </summary>
public class BuildingGrid : MonoBehaviour
{
    #region Fields

    [Header("Grid Settings")]
    [SerializeField] private float _cellSize = 1f;
    [SerializeField] private int _gridWidth = 100;
    [SerializeField] private int _gridHeight = 100;
    [SerializeField] private Vector3 _gridOrigin = Vector3.zero;

    [Header("Visualization")]
    [SerializeField] private bool _showGrid = false;
    [SerializeField] private Color _gridColor = new Color(1f, 1f, 1f, 0.3f);
    [SerializeField] private Color _occupiedColor = new Color(1f, 0f, 0f, 0.3f);

    // Donnees d'occupation
    private Dictionary<Vector2Int, GameObject> _occupiedCells = new Dictionary<Vector2Int, GameObject>();

    // Multi-niveau
    private Dictionary<int, Dictionary<Vector2Int, GameObject>> _levelOccupation =
        new Dictionary<int, Dictionary<Vector2Int, GameObject>>();

    #endregion

    #region Properties

    public float CellSize => _cellSize;
    public int GridWidth => _gridWidth;
    public int GridHeight => _gridHeight;
    public Vector3 GridOrigin => _gridOrigin;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _occupiedCells = new Dictionary<Vector2Int, GameObject>();
        _levelOccupation = new Dictionary<int, Dictionary<Vector2Int, GameObject>>();
        _levelOccupation[0] = _occupiedCells;
    }

    #endregion

    #region Public Methods - Coordinate Conversion

    /// <summary>
    /// Convertit une position monde en coordonnees grille.
    /// </summary>
    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        Vector3 localPos = worldPosition - _gridOrigin;
        int x = Mathf.FloorToInt(localPos.x / _cellSize);
        int z = Mathf.FloorToInt(localPos.z / _cellSize);
        return new Vector2Int(x, z);
    }

    /// <summary>
    /// Convertit des coordonnees grille en position monde (centre de la cellule).
    /// </summary>
    public Vector3 GridToWorld(Vector2Int gridPosition)
    {
        float x = gridPosition.x * _cellSize + _cellSize * 0.5f;
        float z = gridPosition.y * _cellSize + _cellSize * 0.5f;
        return _gridOrigin + new Vector3(x, 0f, z);
    }

    /// <summary>
    /// Convertit des coordonnees grille en position monde avec taille.
    /// </summary>
    public Vector3 GridToWorld(Vector2Int gridPosition, Vector2Int size)
    {
        float x = gridPosition.x * _cellSize + size.x * _cellSize * 0.5f;
        float z = gridPosition.y * _cellSize + size.y * _cellSize * 0.5f;
        return _gridOrigin + new Vector3(x, 0f, z);
    }

    /// <summary>
    /// Snape une position monde sur la grille.
    /// </summary>
    public Vector3 SnapToGrid(Vector3 worldPosition)
    {
        Vector2Int gridPos = WorldToGrid(worldPosition);
        return GridToWorld(gridPos);
    }

    /// <summary>
    /// Snape une position monde sur la grille avec taille.
    /// </summary>
    public Vector3 SnapToGrid(Vector3 worldPosition, Vector2Int size)
    {
        Vector2Int gridPos = WorldToGrid(worldPosition);
        return GridToWorld(gridPos, size);
    }

    #endregion

    #region Public Methods - Occupation

    /// <summary>
    /// Verifie si une cellule est occupee.
    /// </summary>
    public bool IsCellOccupied(Vector2Int gridPosition, int level = 0)
    {
        if (!_levelOccupation.TryGetValue(level, out var cells))
            return false;

        return cells.ContainsKey(gridPosition);
    }

    /// <summary>
    /// Verifie si une zone est occupee.
    /// </summary>
    public bool IsAreaOccupied(Vector2Int gridPosition, Vector2Int size, int level = 0)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                Vector2Int checkPos = new Vector2Int(gridPosition.x + x, gridPosition.y + z);
                if (IsCellOccupied(checkPos, level))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Verifie si une zone est dans les limites de la grille.
    /// </summary>
    public bool IsInBounds(Vector2Int gridPosition, Vector2Int size)
    {
        if (gridPosition.x < 0 || gridPosition.y < 0)
            return false;
        if (gridPosition.x + size.x > _gridWidth)
            return false;
        if (gridPosition.y + size.y > _gridHeight)
            return false;
        return true;
    }

    /// <summary>
    /// Occupe les cellules pour un batiment.
    /// </summary>
    public void OccupyCells(Vector2Int gridPosition, Vector2Int size, GameObject building, int level = 0)
    {
        if (!_levelOccupation.ContainsKey(level))
            _levelOccupation[level] = new Dictionary<Vector2Int, GameObject>();

        var cells = _levelOccupation[level];

        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                Vector2Int cellPos = new Vector2Int(gridPosition.x + x, gridPosition.y + z);
                cells[cellPos] = building;
            }
        }
    }

    /// <summary>
    /// Libere les cellules d'un batiment.
    /// </summary>
    public void FreeCells(Vector2Int gridPosition, Vector2Int size, int level = 0)
    {
        if (!_levelOccupation.TryGetValue(level, out var cells))
            return;

        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                Vector2Int cellPos = new Vector2Int(gridPosition.x + x, gridPosition.y + z);
                cells.Remove(cellPos);
            }
        }
    }

    /// <summary>
    /// Obtient le batiment a une position.
    /// </summary>
    public GameObject GetBuildingAt(Vector2Int gridPosition, int level = 0)
    {
        if (!_levelOccupation.TryGetValue(level, out var cells))
            return null;

        cells.TryGetValue(gridPosition, out GameObject building);
        return building;
    }

    /// <summary>
    /// Obtient tous les batiments dans une zone.
    /// </summary>
    public List<GameObject> GetBuildingsInArea(Vector2Int gridPosition, Vector2Int size, int level = 0)
    {
        var buildings = new HashSet<GameObject>();

        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                Vector2Int checkPos = new Vector2Int(gridPosition.x + x, gridPosition.y + z);
                var building = GetBuildingAt(checkPos, level);
                if (building != null)
                    buildings.Add(building);
            }
        }

        return new List<GameObject>(buildings);
    }

    /// <summary>
    /// Efface toute la grille.
    /// </summary>
    public void ClearGrid()
    {
        foreach (var level in _levelOccupation.Values)
        {
            level.Clear();
        }
    }

    #endregion

    #region Public Methods - Terrain

    /// <summary>
    /// Obtient la hauteur du terrain a une position.
    /// </summary>
    public float GetTerrainHeight(Vector3 worldPosition)
    {
        if (Physics.Raycast(worldPosition + Vector3.up * 100f, Vector3.down, out RaycastHit hit, 200f))
        {
            return hit.point.y;
        }
        return 0f;
    }

    /// <summary>
    /// Verifie si le terrain est plat dans une zone.
    /// </summary>
    public bool IsTerrainFlat(Vector2Int gridPosition, Vector2Int size, float tolerance = 0.5f)
    {
        Vector3 centerWorld = GridToWorld(gridPosition, size);
        float centerHeight = GetTerrainHeight(centerWorld);

        for (int x = 0; x <= size.x; x++)
        {
            for (int z = 0; z <= size.y; z++)
            {
                Vector3 checkWorld = GridToWorld(new Vector2Int(gridPosition.x + x, gridPosition.y + z));
                float checkHeight = GetTerrainHeight(checkWorld);

                if (Mathf.Abs(checkHeight - centerHeight) > tolerance)
                    return false;
            }
        }

        return true;
    }

    #endregion

    #region Visualization

    private void OnDrawGizmos()
    {
        if (!_showGrid) return;

        // Dessiner la grille
        Gizmos.color = _gridColor;
        for (int x = 0; x <= _gridWidth; x++)
        {
            Vector3 start = _gridOrigin + new Vector3(x * _cellSize, 0, 0);
            Vector3 end = _gridOrigin + new Vector3(x * _cellSize, 0, _gridHeight * _cellSize);
            Gizmos.DrawLine(start, end);
        }
        for (int z = 0; z <= _gridHeight; z++)
        {
            Vector3 start = _gridOrigin + new Vector3(0, 0, z * _cellSize);
            Vector3 end = _gridOrigin + new Vector3(_gridWidth * _cellSize, 0, z * _cellSize);
            Gizmos.DrawLine(start, end);
        }

        // Dessiner les cellules occupees
        Gizmos.color = _occupiedColor;
        foreach (var kvp in _occupiedCells)
        {
            Vector3 center = GridToWorld(kvp.Key);
            Gizmos.DrawCube(center, new Vector3(_cellSize * 0.9f, 0.1f, _cellSize * 0.9f));
        }
    }

    #endregion
}
