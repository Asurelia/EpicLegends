using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represente une piece dans un donjon procedural.
/// Contient les informations de layout et de contenu.
/// </summary>
[Serializable]
public class DungeonRoom
{
    #region Properties

    /// <summary>ID unique de la piece.</summary>
    public string RoomId { get; set; }

    /// <summary>Type de la piece.</summary>
    public DungeonRoomType RoomType { get; set; }

    /// <summary>Limites en coordonnees de grille.</summary>
    public RectInt GridBounds { get; set; }

    /// <summary>Taille d'une cellule en unites monde.</summary>
    public float CellSize { get; set; } = 2f;

    /// <summary>Points de spawn d'ennemis.</summary>
    public List<Vector3> EnemySpawnPoints { get; set; }

    /// <summary>Positions des tresors.</summary>
    public List<Vector3> TreasurePositions { get; set; }

    /// <summary>Pieces connectees.</summary>
    public List<DungeonRoom> ConnectedRooms { get; set; }

    /// <summary>La piece a ete exploree.</summary>
    public bool IsExplored { get; set; }

    /// <summary>La piece a ete nettoyee (tous les ennemis vaincus).</summary>
    public bool IsCleared { get; set; }

    /// <summary>Les tresors ont ete recuperes.</summary>
    public bool TreasuresLooted { get; set; }

    #endregion

    #region Computed Properties

    /// <summary>Centre en coordonnees de grille.</summary>
    public Vector2Int GridCenter => new Vector2Int(
        GridBounds.x + GridBounds.width / 2,
        GridBounds.y + GridBounds.height / 2
    );

    /// <summary>Centre en coordonnees monde.</summary>
    public Vector3 WorldCenter => new Vector3(
        GridCenter.x * CellSize,
        0,
        GridCenter.y * CellSize
    );

    /// <summary>Limites en coordonnees monde.</summary>
    public Bounds WorldBounds
    {
        get
        {
            Vector3 min = new Vector3(GridBounds.x * CellSize, 0, GridBounds.y * CellSize);
            Vector3 max = new Vector3((GridBounds.x + GridBounds.width) * CellSize, 4f, (GridBounds.y + GridBounds.height) * CellSize);
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }
    }

    /// <summary>Surface en cellules.</summary>
    public int Area => GridBounds.width * GridBounds.height;

    /// <summary>Nombre d'ennemis dans la piece.</summary>
    public int EnemyCount => EnemySpawnPoints?.Count ?? 0;

    /// <summary>Nombre de tresors dans la piece.</summary>
    public int TreasureCount => TreasurePositions?.Count ?? 0;

    #endregion

    #region Constructor

    public DungeonRoom()
    {
        EnemySpawnPoints = new List<Vector3>();
        TreasurePositions = new List<Vector3>();
        ConnectedRooms = new List<DungeonRoom>();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Verifie si une position monde est dans cette piece.
    /// </summary>
    public bool ContainsWorldPosition(Vector3 worldPosition)
    {
        return WorldBounds.Contains(worldPosition);
    }

    /// <summary>
    /// Verifie si une position grille est dans cette piece.
    /// </summary>
    public bool ContainsGridPosition(Vector2Int gridPosition)
    {
        return GridBounds.Contains(gridPosition);
    }

    /// <summary>
    /// Obtient une position aleatoire dans la piece.
    /// </summary>
    public Vector3 GetRandomPosition()
    {
        int x = GridBounds.x + UnityEngine.Random.Range(1, GridBounds.width - 1);
        int y = GridBounds.y + UnityEngine.Random.Range(1, GridBounds.height - 1);
        return new Vector3(x * CellSize, 0, y * CellSize);
    }

    /// <summary>
    /// Obtient une position aleatoire valide pour un spawn.
    /// </summary>
    public Vector3 GetRandomSpawnPosition(float minDistanceFromEdge = 1f)
    {
        float minX = GridBounds.x * CellSize + minDistanceFromEdge;
        float maxX = (GridBounds.x + GridBounds.width) * CellSize - minDistanceFromEdge;
        float minZ = GridBounds.y * CellSize + minDistanceFromEdge;
        float maxZ = (GridBounds.y + GridBounds.height) * CellSize - minDistanceFromEdge;

        return new Vector3(
            UnityEngine.Random.Range(minX, maxX),
            0,
            UnityEngine.Random.Range(minZ, maxZ)
        );
    }

    /// <summary>
    /// Connecte cette piece a une autre.
    /// </summary>
    public void ConnectTo(DungeonRoom other)
    {
        if (other == null || other == this) return;

        if (!ConnectedRooms.Contains(other))
        {
            ConnectedRooms.Add(other);
        }

        if (!other.ConnectedRooms.Contains(this))
        {
            other.ConnectedRooms.Add(this);
        }
    }

    /// <summary>
    /// Verifie si cette piece est connectee a une autre.
    /// </summary>
    public bool IsConnectedTo(DungeonRoom other)
    {
        return ConnectedRooms != null && ConnectedRooms.Contains(other);
    }

    /// <summary>
    /// Marque la piece comme exploree.
    /// </summary>
    public void MarkExplored()
    {
        IsExplored = true;
    }

    /// <summary>
    /// Marque la piece comme nettoyee.
    /// </summary>
    public void MarkCleared()
    {
        IsCleared = true;
    }

    /// <summary>
    /// Marque les tresors comme recuperes.
    /// </summary>
    public void MarkTreasuresLooted()
    {
        TreasuresLooted = true;
    }

    /// <summary>
    /// Calcule la distance vers une autre piece.
    /// </summary>
    public float DistanceTo(DungeonRoom other)
    {
        if (other == null) return float.MaxValue;
        return Vector3.Distance(WorldCenter, other.WorldCenter);
    }

    /// <summary>
    /// Obtient les coins en coordonnees monde.
    /// </summary>
    public Vector3[] GetWorldCorners()
    {
        return new Vector3[]
        {
            new Vector3(GridBounds.x * CellSize, 0, GridBounds.y * CellSize),
            new Vector3((GridBounds.x + GridBounds.width) * CellSize, 0, GridBounds.y * CellSize),
            new Vector3((GridBounds.x + GridBounds.width) * CellSize, 0, (GridBounds.y + GridBounds.height) * CellSize),
            new Vector3(GridBounds.x * CellSize, 0, (GridBounds.y + GridBounds.height) * CellSize)
        };
    }

    #endregion

    #region Save/Load

    /// <summary>
    /// Cree des donnees de sauvegarde.
    /// </summary>
    public DungeonRoomSaveData ToSaveData()
    {
        return new DungeonRoomSaveData
        {
            roomId = RoomId,
            roomType = RoomType,
            isExplored = IsExplored,
            isCleared = IsCleared,
            treasuresLooted = TreasuresLooted
        };
    }

    /// <summary>
    /// Applique des donnees de sauvegarde.
    /// </summary>
    public void LoadFromSaveData(DungeonRoomSaveData data)
    {
        if (data == null) return;

        IsExplored = data.isExplored;
        IsCleared = data.isCleared;
        TreasuresLooted = data.treasuresLooted;
    }

    #endregion
}

/// <summary>
/// Types de pieces de donjon.
/// </summary>
public enum DungeonRoomType
{
    /// <summary>Piece normale.</summary>
    Normal,

    /// <summary>Entree du donjon.</summary>
    Entrance,

    /// <summary>Sortie du donjon.</summary>
    Exit,

    /// <summary>Piece du boss.</summary>
    Boss,

    /// <summary>Piece a tresor.</summary>
    Treasure,

    /// <summary>Piece de puzzle.</summary>
    Puzzle,

    /// <summary>Piece secrete.</summary>
    Secret,

    /// <summary>Couloir.</summary>
    Corridor,

    /// <summary>Point de sauvegarde.</summary>
    Checkpoint
}

/// <summary>
/// Donnees de sauvegarde d'une piece.
/// </summary>
[Serializable]
public class DungeonRoomSaveData
{
    public string roomId;
    public DungeonRoomType roomType;
    public bool isExplored;
    public bool isCleared;
    public bool treasuresLooted;
}

/// <summary>
/// Donnees de sauvegarde d'un donjon procedural.
/// </summary>
[Serializable]
public class ProceduralDungeonSaveData
{
    public string dungeonId;
    public int seed;
    public int currentFloor;
    public List<DungeonRoomSaveData> roomStates = new List<DungeonRoomSaveData>();
    public float completionPercentage;
    public bool bossDefeated;
}
