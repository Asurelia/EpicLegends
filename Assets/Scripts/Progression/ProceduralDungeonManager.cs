using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manager pour la generation de donjons proceduraux en runtime.
/// Utilise BSP et Cellular Automata pour creer des layouts dynamiques.
/// </summary>
public class ProceduralDungeonManager : MonoBehaviour
{
    #region Singleton

    private static ProceduralDungeonManager _instance;
    public static ProceduralDungeonManager Instance
    {
        get => _instance;
        private set => _instance = value;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _rooms = new List<DungeonRoom>();
        _corridors = new List<Vector2Int>();
    }

    private void OnDestroy()
    {
        // MAJOR FIX: Stop all coroutines to prevent memory leaks
        StopAllCoroutines();
    }

    #endregion

    #region Events

    public event Action OnGenerationStarted;
    public event Action<DungeonGenerationResult> OnGenerationCompleted;
    public event Action<float> OnGenerationProgress;
    public event Action<DungeonRoom> OnRoomGenerated;
    public event Action OnDungeonCleared;

    #endregion

    #region Serialized Fields

    [Header("Generation Settings")]
    [SerializeField] private int _width = 50;
    [SerializeField] private int _height = 50;
    [SerializeField] private float _cellSize = 2f;
    [SerializeField] private float _wallHeight = 4f;

    [Header("Room Settings")]
    [SerializeField] private int _minRoomSize = 5;
    [SerializeField] private int _maxRoomSize = 15;
    [SerializeField] private int _splitIterations = 4;

    [Header("Generation Type")]
    [SerializeField] private DungeonGenerationType _generationType = DungeonGenerationType.BSP;

    [Header("Cave Settings")]
    [SerializeField] [Range(0.3f, 0.6f)] private float _fillProbability = 0.45f;
    [SerializeField] [Range(1, 10)] private int _smoothIterations = 5;

    [Header("Content")]
    [SerializeField] private bool _generateEnemies = true;
    [SerializeField] private bool _generateTreasures = true;
    [SerializeField] private bool _generateTraps = false;
    [SerializeField] [Range(0f, 1f)] private float _enemyDensity = 0.3f;
    [SerializeField] [Range(0f, 1f)] private float _treasureDensity = 0.15f;

    [Header("Prefabs")]
    [SerializeField] private GameObject _floorPrefab;
    [SerializeField] private GameObject _wallPrefab;
    [SerializeField] private GameObject _doorPrefab;
    [SerializeField] private GameObject _chestPrefab;
    [SerializeField] private GameObject _enemySpawnPrefab;
    [SerializeField] private GameObject _playerSpawnPrefab;
    [SerializeField] private GameObject _exitPrefab;

    [Header("Materials")]
    [SerializeField] private Material _floorMaterial;
    [SerializeField] private Material _wallMaterial;

    [Header("Seed")]
    [SerializeField] private bool _useRandomSeed = true;
    [SerializeField] private int _seed = 42;

    #endregion

    #region Private Fields

    private int[,] _map;
    private List<DungeonRoom> _rooms;
    private List<Vector2Int> _corridors;
    private GameObject _dungeonRoot;
    private bool _isGenerating;
    private DungeonData _currentDungeonData;

    private const int EMPTY = 0;
    private const int WALL = 1;
    private const int DOOR = 2;
    private const int CHEST = 3;
    private const int ENEMY_SPAWN = 4;
    private const int PLAYER_SPAWN = 5;
    private const int EXIT = 6;

    #endregion

    #region Properties

    public bool IsGenerating => _isGenerating;
    public int RoomCount => _rooms?.Count ?? 0;
    public List<DungeonRoom> Rooms => _rooms;
    public DungeonData CurrentDungeonData => _currentDungeonData;
    public int CurrentSeed => _seed;

    #endregion

    #region Public Methods

    /// <summary>
    /// Genere un donjon procedural.
    /// </summary>
    public void GenerateDungeon(DungeonData dungeonData = null, int? customSeed = null)
    {
        if (_isGenerating) return;

        _currentDungeonData = dungeonData;

        // Setup seed
        if (customSeed.HasValue)
        {
            _seed = customSeed.Value;
        }
        else if (_useRandomSeed)
        {
            _seed = UnityEngine.Random.Range(0, 999999);
        }

        StartCoroutine(GenerateDungeonCoroutine());
    }

    /// <summary>
    /// Efface le donjon actuel.
    /// </summary>
    public void ClearDungeon()
    {
        if (_dungeonRoot != null)
        {
            Destroy(_dungeonRoot);
            _dungeonRoot = null;
        }

        _rooms?.Clear();
        _corridors?.Clear();
        _map = null;

        OnDungeonCleared?.Invoke();
    }

    /// <summary>
    /// Obtient la piece la plus proche d'une position.
    /// </summary>
    public DungeonRoom GetNearestRoom(Vector3 worldPosition)
    {
        if (_rooms == null || _rooms.Count == 0) return null;

        DungeonRoom nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var room in _rooms)
        {
            float dist = Vector3.Distance(worldPosition, room.WorldCenter);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = room;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Obtient une piece par son type.
    /// </summary>
    public DungeonRoom GetRoomByType(DungeonRoomType type)
    {
        if (_rooms == null) return null;

        foreach (var room in _rooms)
        {
            if (room.RoomType == type) return room;
        }

        return null;
    }

    /// <summary>
    /// Verifie si une position est dans une piece.
    /// </summary>
    public bool IsPositionInRoom(Vector3 worldPosition, out DungeonRoom room)
    {
        room = null;
        if (_rooms == null) return false;

        foreach (var r in _rooms)
        {
            if (r.ContainsWorldPosition(worldPosition))
            {
                room = r;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Convertit une position monde en coordonnees de grille.
    /// </summary>
    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt(worldPosition.x / _cellSize);
        int y = Mathf.FloorToInt(worldPosition.z / _cellSize);
        return new Vector2Int(x, y);
    }

    /// <summary>
    /// Convertit des coordonnees de grille en position monde.
    /// </summary>
    public Vector3 GridToWorld(Vector2Int gridPosition)
    {
        return new Vector3(gridPosition.x * _cellSize, 0, gridPosition.y * _cellSize);
    }

    /// <summary>
    /// Configure les parametres de generation.
    /// </summary>
    public void SetGenerationSettings(int width, int height, DungeonGenerationType type)
    {
        _width = Mathf.Clamp(width, 20, 200);
        _height = Mathf.Clamp(height, 20, 200);
        _generationType = type;
    }

    #endregion

    #region Private Methods - Generation

    private IEnumerator GenerateDungeonCoroutine()
    {
        _isGenerating = true;
        OnGenerationStarted?.Invoke();

        // Clear previous
        ClearDungeon();

        // Init random
        UnityEngine.Random.InitState(_seed);

        // Initialize map
        _map = new int[_width, _height];
        _rooms = new List<DungeonRoom>();
        _corridors = new List<Vector2Int>();

        OnGenerationProgress?.Invoke(0.1f);
        yield return null;

        // Generate layout based on type
        switch (_generationType)
        {
            case DungeonGenerationType.BSP:
                yield return StartCoroutine(GenerateBSP());
                break;
            case DungeonGenerationType.CellularAutomata:
                yield return StartCoroutine(GenerateCaves());
                break;
            case DungeonGenerationType.Hybrid:
                yield return StartCoroutine(GenerateBSP());
                ApplyCaveSmoothing();
                break;
        }

        OnGenerationProgress?.Invoke(0.5f);
        yield return null;

        // Place content
        PlaceDecorations();

        OnGenerationProgress?.Invoke(0.6f);
        yield return null;

        // Build geometry
        yield return StartCoroutine(BuildGeometry());

        OnGenerationProgress?.Invoke(1f);

        // Result
        var result = new DungeonGenerationResult
        {
            success = true,
            seed = _seed,
            roomCount = _rooms.Count,
            width = _width,
            height = _height,
            spawnRoom = GetRoomByType(DungeonRoomType.Entrance),
            exitRoom = GetRoomByType(DungeonRoomType.Exit),
            bossRoom = GetRoomByType(DungeonRoomType.Boss)
        };

        _isGenerating = false;
        OnGenerationCompleted?.Invoke(result);

        Debug.Log($"[ProceduralDungeonManager] Dungeon generated! Seed: {_seed}, Rooms: {_rooms.Count}");
    }

    private IEnumerator GenerateBSP()
    {
        // Fill with walls
        for (int x = 0; x < _width; x++)
            for (int y = 0; y < _height; y++)
                _map[x, y] = WALL;

        // Create BSP tree
        BSPNode root = new BSPNode(new RectInt(1, 1, _width - 2, _height - 2));
        SplitBSP(root, _splitIterations);

        // Collect leaf nodes
        List<BSPNode> leaves = new List<BSPNode>();
        CollectLeaves(root, leaves);

        yield return null;

        // Create rooms in leaves
        int roomIndex = 0;
        foreach (var leaf in leaves)
        {
            RectInt roomRect = CreateRoomRect(leaf.Bounds);
            var room = CreateRoom(roomRect, roomIndex);
            _rooms.Add(room);
            CarveRoom(roomRect);
            OnRoomGenerated?.Invoke(room);
            roomIndex++;

            if (roomIndex % 5 == 0) yield return null;
        }

        // Connect rooms
        ConnectRooms(root);

        // Assign room types
        AssignRoomTypes();
    }

    private IEnumerator GenerateCaves()
    {
        // Random fill
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                if (x == 0 || x == _width - 1 || y == 0 || y == _height - 1)
                    _map[x, y] = WALL;
                else
                    _map[x, y] = UnityEngine.Random.value < _fillProbability ? WALL : EMPTY;
            }
        }

        yield return null;

        // Smooth
        for (int i = 0; i < _smoothIterations; i++)
        {
            SmoothCaves();
            if (i % 2 == 0) yield return null;
        }

        // Find connected areas as "rooms"
        FindCaveRooms();
    }

    private void SplitBSP(BSPNode node, int iterations)
    {
        if (iterations <= 0) return;

        if (node.Bounds.width < _minRoomSize * 2 && node.Bounds.height < _minRoomSize * 2)
            return;

        bool splitHorizontal = UnityEngine.Random.value > 0.5f;
        if (node.Bounds.width > node.Bounds.height * 1.25f)
            splitHorizontal = false;
        else if (node.Bounds.height > node.Bounds.width * 1.25f)
            splitHorizontal = true;

        int splitPos;
        if (splitHorizontal)
        {
            if (node.Bounds.height < _minRoomSize * 2) return;

            splitPos = UnityEngine.Random.Range(_minRoomSize, node.Bounds.height - _minRoomSize);
            node.Left = new BSPNode(new RectInt(
                node.Bounds.x, node.Bounds.y,
                node.Bounds.width, splitPos));
            node.Right = new BSPNode(new RectInt(
                node.Bounds.x, node.Bounds.y + splitPos,
                node.Bounds.width, node.Bounds.height - splitPos));
        }
        else
        {
            if (node.Bounds.width < _minRoomSize * 2) return;

            splitPos = UnityEngine.Random.Range(_minRoomSize, node.Bounds.width - _minRoomSize);
            node.Left = new BSPNode(new RectInt(
                node.Bounds.x, node.Bounds.y,
                splitPos, node.Bounds.height));
            node.Right = new BSPNode(new RectInt(
                node.Bounds.x + splitPos, node.Bounds.y,
                node.Bounds.width - splitPos, node.Bounds.height));
        }

        SplitBSP(node.Left, iterations - 1);
        SplitBSP(node.Right, iterations - 1);
    }

    private void CollectLeaves(BSPNode node, List<BSPNode> leaves)
    {
        if (node == null) return;

        if (node.IsLeaf)
            leaves.Add(node);
        else
        {
            CollectLeaves(node.Left, leaves);
            CollectLeaves(node.Right, leaves);
        }
    }

    private RectInt CreateRoomRect(RectInt bounds)
    {
        int width = UnityEngine.Random.Range(_minRoomSize, Mathf.Min(_maxRoomSize, bounds.width - 1));
        int height = UnityEngine.Random.Range(_minRoomSize, Mathf.Min(_maxRoomSize, bounds.height - 1));

        int x = bounds.x + UnityEngine.Random.Range(1, Mathf.Max(1, bounds.width - width));
        int y = bounds.y + UnityEngine.Random.Range(1, Mathf.Max(1, bounds.height - height));

        return new RectInt(x, y, width, height);
    }

    private DungeonRoom CreateRoom(RectInt rect, int index)
    {
        var room = new DungeonRoom
        {
            RoomId = $"room_{index}",
            GridBounds = rect,
            RoomType = DungeonRoomType.Normal,
            CellSize = _cellSize,
            EnemySpawnPoints = new List<Vector3>(),
            TreasurePositions = new List<Vector3>()
        };

        return room;
    }

    private void CarveRoom(RectInt room)
    {
        for (int x = room.x; x < room.x + room.width; x++)
        {
            for (int y = room.y; y < room.y + room.height; y++)
            {
                if (x >= 0 && x < _width && y >= 0 && y < _height)
                {
                    _map[x, y] = EMPTY;
                }
            }
        }
    }

    private void ConnectRooms(BSPNode node)
    {
        if (node == null || node.IsLeaf) return;

        RectInt leftRoom = GetRoomInNode(node.Left);
        RectInt rightRoom = GetRoomInNode(node.Right);

        CreateCorridor(leftRoom, rightRoom);

        ConnectRooms(node.Left);
        ConnectRooms(node.Right);
    }

    private RectInt GetRoomInNode(BSPNode node)
    {
        if (node == null) return new RectInt();

        if (node.IsLeaf)
        {
            foreach (var room in _rooms)
            {
                if (node.Bounds.Contains(new Vector2Int(room.GridBounds.x, room.GridBounds.y)))
                    return room.GridBounds;
            }
        }
        else
        {
            return UnityEngine.Random.value > 0.5f
                ? GetRoomInNode(node.Left)
                : GetRoomInNode(node.Right);
        }

        return new RectInt();
    }

    private void CreateCorridor(RectInt roomA, RectInt roomB)
    {
        Vector2Int centerA = new Vector2Int(roomA.x + roomA.width / 2, roomA.y + roomA.height / 2);
        Vector2Int centerB = new Vector2Int(roomB.x + roomB.width / 2, roomB.y + roomB.height / 2);

        if (UnityEngine.Random.value > 0.5f)
        {
            CarveCorridorH(centerA.x, centerB.x, centerA.y);
            CarveCorridorV(centerA.y, centerB.y, centerB.x);
        }
        else
        {
            CarveCorridorV(centerA.y, centerB.y, centerA.x);
            CarveCorridorH(centerA.x, centerB.x, centerB.y);
        }
    }

    private void CarveCorridorH(int x1, int x2, int y)
    {
        int minX = Mathf.Min(x1, x2);
        int maxX = Mathf.Max(x1, x2);

        for (int x = minX; x <= maxX; x++)
        {
            if (x >= 0 && x < _width && y >= 0 && y < _height)
            {
                _map[x, y] = EMPTY;
                _corridors.Add(new Vector2Int(x, y));
            }
        }
    }

    private void CarveCorridorV(int y1, int y2, int x)
    {
        int minY = Mathf.Min(y1, y2);
        int maxY = Mathf.Max(y1, y2);

        for (int y = minY; y <= maxY; y++)
        {
            if (x >= 0 && x < _width && y >= 0 && y < _height)
            {
                _map[x, y] = EMPTY;
                _corridors.Add(new Vector2Int(x, y));
            }
        }
    }

    private void SmoothCaves()
    {
        int[,] newMap = new int[_width, _height];

        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                int neighbors = CountWallNeighbors(x, y);

                if (neighbors >= 5)
                    newMap[x, y] = WALL;
                else if (neighbors <= 3)
                    newMap[x, y] = EMPTY;
                else
                    newMap[x, y] = _map[x, y];
            }
        }

        _map = newMap;
    }

    private void ApplyCaveSmoothing()
    {
        for (int i = 0; i < 2; i++)
            SmoothCaves();
    }

    private int CountWallNeighbors(int cx, int cy)
    {
        int count = 0;
        for (int x = cx - 1; x <= cx + 1; x++)
        {
            for (int y = cy - 1; y <= cy + 1; y++)
            {
                if (x == cx && y == cy) continue;
                if (x < 0 || x >= _width || y < 0 || y >= _height)
                    count++;
                else if (_map[x, y] == WALL)
                    count++;
            }
        }
        return count;
    }

    private void FindCaveRooms()
    {
        // Simple flood fill to find connected regions
        bool[,] visited = new bool[_width, _height];
        int roomIndex = 0;

        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                if (_map[x, y] == EMPTY && !visited[x, y])
                {
                    var cells = FloodFill(x, y, visited);
                    if (cells.Count > 20) // Minimum size
                    {
                        var bounds = GetBoundsOfCells(cells);
                        var room = CreateRoom(bounds, roomIndex);
                        _rooms.Add(room);
                        roomIndex++;
                    }
                }
            }
        }

        AssignRoomTypes();
    }

    private List<Vector2Int> FloodFill(int startX, int startY, bool[,] visited)
    {
        var cells = new List<Vector2Int>();
        var stack = new Stack<Vector2Int>();
        stack.Push(new Vector2Int(startX, startY));

        while (stack.Count > 0)
        {
            var cell = stack.Pop();
            if (cell.x < 0 || cell.x >= _width || cell.y < 0 || cell.y >= _height)
                continue;
            if (visited[cell.x, cell.y] || _map[cell.x, cell.y] != EMPTY)
                continue;

            visited[cell.x, cell.y] = true;
            cells.Add(cell);

            stack.Push(new Vector2Int(cell.x + 1, cell.y));
            stack.Push(new Vector2Int(cell.x - 1, cell.y));
            stack.Push(new Vector2Int(cell.x, cell.y + 1));
            stack.Push(new Vector2Int(cell.x, cell.y - 1));
        }

        return cells;
    }

    private RectInt GetBoundsOfCells(List<Vector2Int> cells)
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var cell in cells)
        {
            minX = Mathf.Min(minX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxX = Mathf.Max(maxX, cell.x);
            maxY = Mathf.Max(maxY, cell.y);
        }

        return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private void AssignRoomTypes()
    {
        if (_rooms.Count == 0) return;

        // First room is entrance
        _rooms[0].RoomType = DungeonRoomType.Entrance;

        // Last room is exit (or boss)
        if (_rooms.Count > 1)
        {
            var lastRoom = _rooms[_rooms.Count - 1];
            lastRoom.RoomType = _currentDungeonData?.finalBoss != null
                ? DungeonRoomType.Boss
                : DungeonRoomType.Exit;
        }

        // Random treasure room
        if (_rooms.Count > 3)
        {
            int treasureIndex = UnityEngine.Random.Range(1, _rooms.Count - 1);
            _rooms[treasureIndex].RoomType = DungeonRoomType.Treasure;
        }
    }

    private void PlaceDecorations()
    {
        // Place player spawn in entrance
        var entrance = GetRoomByType(DungeonRoomType.Entrance);
        if (entrance != null)
        {
            Vector2Int center = entrance.GridCenter;
            if (IsValidCell(center.x, center.y))
                _map[center.x, center.y] = PLAYER_SPAWN;
        }

        // Place exit in exit/boss room
        var exitRoom = GetRoomByType(DungeonRoomType.Exit) ?? GetRoomByType(DungeonRoomType.Boss);
        if (exitRoom != null)
        {
            Vector2Int center = exitRoom.GridCenter;
            if (IsValidCell(center.x, center.y))
                _map[center.x, center.y] = EXIT;
        }

        // Place content in normal rooms
        foreach (var room in _rooms)
        {
            if (room.RoomType == DungeonRoomType.Entrance) continue;

            // Enemies
            if (_generateEnemies && room.RoomType != DungeonRoomType.Treasure)
            {
                int enemyCount = Mathf.RoundToInt(room.GridBounds.width * room.GridBounds.height * _enemyDensity * 0.1f);
                PlaceEnemiesInRoom(room, enemyCount);
            }

            // Treasures
            if (_generateTreasures && (room.RoomType == DungeonRoomType.Treasure || UnityEngine.Random.value < _treasureDensity))
            {
                PlaceTreasureInRoom(room);
            }
        }
    }

    private void PlaceEnemiesInRoom(DungeonRoom room, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int x = room.GridBounds.x + UnityEngine.Random.Range(1, room.GridBounds.width - 1);
            int y = room.GridBounds.y + UnityEngine.Random.Range(1, room.GridBounds.height - 1);

            if (IsValidCell(x, y) && _map[x, y] == EMPTY)
            {
                _map[x, y] = ENEMY_SPAWN;
                room.EnemySpawnPoints.Add(GridToWorld(new Vector2Int(x, y)));
            }
        }
    }

    private void PlaceTreasureInRoom(DungeonRoom room)
    {
        Vector2Int center = room.GridCenter;
        if (IsValidCell(center.x, center.y) && _map[center.x, center.y] == EMPTY)
        {
            _map[center.x, center.y] = CHEST;
            room.TreasurePositions.Add(GridToWorld(center));
        }
    }

    private bool IsValidCell(int x, int y)
    {
        return x >= 0 && x < _width && y >= 0 && y < _height;
    }

    #endregion

    #region Private Methods - Geometry

    private IEnumerator BuildGeometry()
    {
        _dungeonRoot = new GameObject("ProceduralDungeon");
        _dungeonRoot.transform.position = Vector3.zero;

        var floorsParent = new GameObject("Floors");
        floorsParent.transform.SetParent(_dungeonRoot.transform);

        var wallsParent = new GameObject("Walls");
        wallsParent.transform.SetParent(_dungeonRoot.transform);

        var propsParent = new GameObject("Props");
        propsParent.transform.SetParent(_dungeonRoot.transform);

        // Create materials if not assigned
        Material floorMat = _floorMaterial;
        Material wallMat = _wallMaterial;

        if (floorMat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");

            if (shader != null)
            {
                floorMat = new Material(shader);
                Color floorColor = new Color(0.4f, 0.35f, 0.3f);

                // Set color using URP property names with fallback
                if (floorMat.HasProperty("_BaseColor"))
                    floorMat.SetColor("_BaseColor", floorColor);
                else if (floorMat.HasProperty("_Color"))
                    floorMat.SetColor("_Color", floorColor);
                else
                    floorMat.color = floorColor;
            }
        }

        if (wallMat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");

            if (shader != null)
            {
                wallMat = new Material(shader);
                Color wallColor = new Color(0.3f, 0.25f, 0.2f);

                // Set color using URP property names with fallback
                if (wallMat.HasProperty("_BaseColor"))
                    wallMat.SetColor("_BaseColor", wallColor);
                else if (wallMat.HasProperty("_Color"))
                    wallMat.SetColor("_Color", wallColor);
                else
                    wallMat.color = wallColor;
            }
        }

        int cellsProcessed = 0;
        int totalCells = _width * _height;

        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                Vector3 worldPos = new Vector3(x * _cellSize, 0, y * _cellSize);

                switch (_map[x, y])
                {
                    case EMPTY:
                    case DOOR:
                    case CHEST:
                    case ENEMY_SPAWN:
                    case PLAYER_SPAWN:
                    case EXIT:
                        CreateFloor(floorsParent.transform, worldPos, floorMat);
                        break;
                    case WALL:
                        CreateWall(wallsParent.transform, worldPos, wallMat);
                        break;
                }

                // Props
                switch (_map[x, y])
                {
                    case CHEST:
                        CreateProp(propsParent.transform, worldPos, _chestPrefab, "Chest");
                        break;
                    case ENEMY_SPAWN:
                        CreateProp(propsParent.transform, worldPos, _enemySpawnPrefab, "EnemySpawn");
                        break;
                    case PLAYER_SPAWN:
                        CreateProp(propsParent.transform, worldPos, _playerSpawnPrefab, "PlayerSpawn");
                        break;
                    case EXIT:
                        CreateProp(propsParent.transform, worldPos, _exitPrefab, "Exit");
                        break;
                }

                cellsProcessed++;
                if (cellsProcessed % 500 == 0)
                {
                    float progress = 0.6f + (0.4f * cellsProcessed / totalCells);
                    OnGenerationProgress?.Invoke(progress);
                    yield return null;
                }
            }
        }
    }

    private void CreateFloor(Transform parent, Vector3 pos, Material mat)
    {
        GameObject floor;
        if (_floorPrefab != null)
        {
            floor = Instantiate(_floorPrefab, parent);
        }
        else
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(parent);
            floor.GetComponent<MeshRenderer>().material = mat;
        }

        floor.name = "Floor";
        floor.transform.position = pos + new Vector3(0, -0.25f, 0);
        floor.transform.localScale = new Vector3(_cellSize, 0.5f, _cellSize);
        floor.isStatic = true;
    }

    private void CreateWall(Transform parent, Vector3 pos, Material mat)
    {
        GameObject wall;
        if (_wallPrefab != null)
        {
            wall = Instantiate(_wallPrefab, parent);
        }
        else
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(parent);
            wall.GetComponent<MeshRenderer>().material = mat;
        }

        wall.name = "Wall";
        wall.transform.position = pos + new Vector3(0, _wallHeight / 2f, 0);
        wall.transform.localScale = new Vector3(_cellSize, _wallHeight, _cellSize);
        wall.isStatic = true;
    }

    private void CreateProp(Transform parent, Vector3 pos, GameObject prefab, string defaultName)
    {
        GameObject prop;
        if (prefab != null)
        {
            prop = Instantiate(prefab, parent);
        }
        else
        {
            prop = new GameObject(defaultName);
            prop.transform.SetParent(parent);
        }

        prop.name = defaultName;
        prop.transform.position = pos + new Vector3(0, 0.1f, 0);
    }

    #endregion

    #region BSP Node

    private class BSPNode
    {
        public RectInt Bounds;
        public BSPNode Left;
        public BSPNode Right;

        public bool IsLeaf => Left == null && Right == null;

        public BSPNode(RectInt bounds)
        {
            Bounds = bounds;
        }
    }

    #endregion
}

/// <summary>
/// Types de generation de donjon.
/// </summary>
public enum DungeonGenerationType
{
    BSP,
    CellularAutomata,
    Hybrid
}

/// <summary>
/// Resultat de la generation de donjon.
/// </summary>
public class DungeonGenerationResult
{
    public bool success;
    public int seed;
    public int roomCount;
    public int width;
    public int height;
    public DungeonRoom spawnRoom;
    public DungeonRoom exitRoom;
    public DungeonRoom bossRoom;
}
