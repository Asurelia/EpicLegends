using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Generateur de donjons proceduraux avec BSP et Cellular Automata.
/// Menu: EpicLegends > Tools > Dungeon Generator
/// </summary>
public class DungeonGenerator : EditorWindow
{
    #region Settings

    [Header("Dungeon Size")]
    private int _width = 50;
    private int _height = 50;
    private float _cellSize = 2f;
    private float _wallHeight = 4f;

    [Header("Room Settings")]
    private int _minRoomSize = 5;
    private int _maxRoomSize = 15;
    private int _splitIterations = 4;

    [Header("Generation Type")]
    private DungeonType _dungeonType = DungeonType.BSP;

    [Header("Cave Settings")]
    private float _fillProbability = 0.45f;
    private int _smoothIterations = 5;

    [Header("Decoration")]
    private bool _placeDoors = true;
    private bool _placeChests = true;
    private bool _placeTorches = true;
    private bool _placeEnemySpawns = true;

    [Header("Seed")]
    private int _seed = 42;
    private bool _useRandomSeed = true;

    [Header("Output")]
    private string _dungeonName = "GeneratedDungeon";

    [Header("Prefabs")]
    private GameObject _floorPrefab;
    private GameObject _wallPrefab;
    private GameObject _doorPrefab;
    private GameObject _chestPrefab;
    private GameObject _torchPrefab;
    private GameObject _enemySpawnPrefab;

    #endregion

    #region State

    private int[,] _map;
    private List<RectInt> _rooms = new List<RectInt>();
    private List<Vector2Int> _corridors = new List<Vector2Int>();
    private Texture2D _previewTexture;
    private Vector2 _scrollPosition;

    private const int EMPTY = 0;
    private const int WALL = 1;
    private const int DOOR = 2;
    private const int CHEST = 3;
    private const int TORCH = 4;
    private const int SPAWN = 5;

    #endregion

    public enum DungeonType
    {
        BSP,
        CellularAutomata,
        Hybrid
    }

    [MenuItem("EpicLegends/Tools/Dungeon Generator")]
    public static void ShowWindow()
    {
        var window = GetWindow<DungeonGenerator>("Dungeon Generator");
        window.minSize = new Vector2(400, 700);
    }

    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        DrawHeader();
        EditorGUILayout.Space(10);

        DrawSizeSettings();
        EditorGUILayout.Space(10);

        DrawGenerationSettings();
        EditorGUILayout.Space(10);

        DrawDecorationSettings();
        EditorGUILayout.Space(10);

        DrawPrefabSettings();
        EditorGUILayout.Space(10);

        DrawPreview();
        EditorGUILayout.Space(10);

        DrawButtons();

        EditorGUILayout.EndScrollView();
    }

    #region GUI Sections

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Dungeon Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Generate procedural dungeons using BSP or Cellular Automata algorithms.\n" +
            "BSP: Room-based dungeons with corridors\n" +
            "Cellular Automata: Organic cave systems",
            MessageType.Info);
    }

    private void DrawSizeSettings()
    {
        EditorGUILayout.LabelField("Dungeon Size", EditorStyles.boldLabel);

        _width = EditorGUILayout.IntSlider("Width", _width, 20, 100);
        _height = EditorGUILayout.IntSlider("Height", _height, 20, 100);
        _cellSize = EditorGUILayout.Slider("Cell Size", _cellSize, 1f, 5f);
        _wallHeight = EditorGUILayout.Slider("Wall Height", _wallHeight, 2f, 8f);
    }

    private void DrawGenerationSettings()
    {
        EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);

        _dungeonType = (DungeonType)EditorGUILayout.EnumPopup("Dungeon Type", _dungeonType);

        EditorGUILayout.Space(5);

        // Seed
        EditorGUILayout.BeginHorizontal();
        _useRandomSeed = EditorGUILayout.Toggle("Random Seed", _useRandomSeed);
        if (!_useRandomSeed)
        {
            _seed = EditorGUILayout.IntField(_seed);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        switch (_dungeonType)
        {
            case DungeonType.BSP:
                DrawBSPSettings();
                break;
            case DungeonType.CellularAutomata:
                DrawCaveSettings();
                break;
            case DungeonType.Hybrid:
                DrawBSPSettings();
                DrawCaveSettings();
                break;
        }
    }

    private void DrawBSPSettings()
    {
        EditorGUILayout.LabelField("BSP Settings", EditorStyles.miniBoldLabel);
        _minRoomSize = EditorGUILayout.IntSlider("Min Room Size", _minRoomSize, 3, 10);
        _maxRoomSize = EditorGUILayout.IntSlider("Max Room Size", _maxRoomSize, 8, 20);
        _splitIterations = EditorGUILayout.IntSlider("Split Iterations", _splitIterations, 2, 6);
    }

    private void DrawCaveSettings()
    {
        EditorGUILayout.LabelField("Cave Settings", EditorStyles.miniBoldLabel);
        _fillProbability = EditorGUILayout.Slider("Fill Probability", _fillProbability, 0.3f, 0.6f);
        _smoothIterations = EditorGUILayout.IntSlider("Smooth Iterations", _smoothIterations, 1, 10);
    }

    private void DrawDecorationSettings()
    {
        EditorGUILayout.LabelField("Decoration", EditorStyles.boldLabel);

        _placeDoors = EditorGUILayout.Toggle("Place Doors", _placeDoors);
        _placeChests = EditorGUILayout.Toggle("Place Chests", _placeChests);
        _placeTorches = EditorGUILayout.Toggle("Place Torches", _placeTorches);
        _placeEnemySpawns = EditorGUILayout.Toggle("Place Enemy Spawns", _placeEnemySpawns);
    }

    private void DrawPrefabSettings()
    {
        EditorGUILayout.LabelField("Prefabs (Optional)", EditorStyles.boldLabel);

        _floorPrefab = (GameObject)EditorGUILayout.ObjectField("Floor", _floorPrefab, typeof(GameObject), false);
        _wallPrefab = (GameObject)EditorGUILayout.ObjectField("Wall", _wallPrefab, typeof(GameObject), false);
        _doorPrefab = (GameObject)EditorGUILayout.ObjectField("Door", _doorPrefab, typeof(GameObject), false);
        _chestPrefab = (GameObject)EditorGUILayout.ObjectField("Chest", _chestPrefab, typeof(GameObject), false);
        _torchPrefab = (GameObject)EditorGUILayout.ObjectField("Torch", _torchPrefab, typeof(GameObject), false);
        _enemySpawnPrefab = (GameObject)EditorGUILayout.ObjectField("Enemy Spawn", _enemySpawnPrefab, typeof(GameObject), false);

        EditorGUILayout.HelpBox("If prefabs are not set, primitive shapes will be used.", MessageType.Info);
    }

    private void DrawPreview()
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        if (GUILayout.Button("Generate Preview", GUILayout.Height(25)))
        {
            GeneratePreview();
        }

        if (_previewTexture != null)
        {
            float previewSize = Mathf.Min(position.width - 40, 300);
            Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize);
            EditorGUI.DrawPreviewTexture(previewRect, _previewTexture, null, ScaleMode.ScaleToFit);

            EditorGUILayout.LabelField($"Rooms: {_rooms.Count}", EditorStyles.miniLabel);
        }
    }

    private void DrawButtons()
    {
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        _dungeonName = EditorGUILayout.TextField("Dungeon Name", _dungeonName);

        EditorGUILayout.Space(5);

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Generate Dungeon", GUILayout.Height(35)))
        {
            GenerateDungeon();
        }
        GUI.backgroundColor = Color.white;
    }

    #endregion

    #region Generation

    private void GeneratePreview()
    {
        if (_useRandomSeed)
            _seed = Random.Range(0, 999999);

        Random.InitState(_seed);

        // Initialize map
        _map = new int[_width, _height];
        _rooms.Clear();
        _corridors.Clear();

        // Generate based on type
        switch (_dungeonType)
        {
            case DungeonType.BSP:
                GenerateBSP();
                break;
            case DungeonType.CellularAutomata:
                GenerateCaves();
                break;
            case DungeonType.Hybrid:
                GenerateBSP();
                ApplyCaveSmoothing();
                break;
        }

        // Add decorations
        PlaceDecorations();

        // Create preview texture
        _previewTexture = MapToTexture();
    }

    private void GenerateBSP()
    {
        // Fill with walls
        for (int x = 0; x < _width; x++)
            for (int y = 0; y < _height; y++)
                _map[x, y] = WALL;

        // Create BSP tree
        BSPNode root = new BSPNode(new RectInt(1, 1, _width - 2, _height - 2));
        SplitBSP(root, _splitIterations);

        // Collect leaf rooms
        List<BSPNode> leaves = new List<BSPNode>();
        CollectLeaves(root, leaves);

        // Create rooms in leaves
        foreach (var leaf in leaves)
        {
            RectInt room = CreateRoom(leaf.Bounds);
            _rooms.Add(room);
            CarveRoom(room);
        }

        // Connect rooms with corridors
        ConnectRooms(root);
    }

    private void SplitBSP(BSPNode node, int iterations)
    {
        if (iterations <= 0) return;

        if (node.Bounds.width < _minRoomSize * 2 && node.Bounds.height < _minRoomSize * 2)
            return;

        // Decide split direction
        bool splitHorizontal = Random.value > 0.5f;
        if (node.Bounds.width > node.Bounds.height * 1.25f)
            splitHorizontal = false;
        else if (node.Bounds.height > node.Bounds.width * 1.25f)
            splitHorizontal = true;

        // Split
        int splitPos;
        if (splitHorizontal)
        {
            if (node.Bounds.height < _minRoomSize * 2) return;

            splitPos = Random.Range(_minRoomSize, node.Bounds.height - _minRoomSize);
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

            splitPos = Random.Range(_minRoomSize, node.Bounds.width - _minRoomSize);
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

    private RectInt CreateRoom(RectInt bounds)
    {
        int width = Random.Range(_minRoomSize, Mathf.Min(_maxRoomSize, bounds.width - 1));
        int height = Random.Range(_minRoomSize, Mathf.Min(_maxRoomSize, bounds.height - 1));

        int x = bounds.x + Random.Range(1, bounds.width - width);
        int y = bounds.y + Random.Range(1, bounds.height - height);

        return new RectInt(x, y, width, height);
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

    private void CollectLeaves(BSPNode node, List<BSPNode> leaves)
    {
        if (node == null) return;

        if (node.IsLeaf)
        {
            leaves.Add(node);
        }
        else
        {
            CollectLeaves(node.Left, leaves);
            CollectLeaves(node.Right, leaves);
        }
    }

    private void ConnectRooms(BSPNode node)
    {
        if (node == null || node.IsLeaf) return;

        // Get room from each child
        RectInt leftRoom = GetRoomInNode(node.Left);
        RectInt rightRoom = GetRoomInNode(node.Right);

        // Create corridor between them
        CreateCorridor(leftRoom, rightRoom);

        // Recurse
        ConnectRooms(node.Left);
        ConnectRooms(node.Right);
    }

    private RectInt GetRoomInNode(BSPNode node)
    {
        if (node == null) return new RectInt();

        if (node.IsLeaf)
        {
            // Find the room in this leaf
            foreach (var room in _rooms)
            {
                if (node.Bounds.Contains(new Vector2Int(room.x, room.y)))
                    return room;
            }
        }
        else
        {
            // Pick one side randomly
            return Random.value > 0.5f ? GetRoomInNode(node.Left) : GetRoomInNode(node.Right);
        }

        return new RectInt();
    }

    private void CreateCorridor(RectInt roomA, RectInt roomB)
    {
        Vector2Int centerA = new Vector2Int(roomA.x + roomA.width / 2, roomA.y + roomA.height / 2);
        Vector2Int centerB = new Vector2Int(roomB.x + roomB.width / 2, roomB.y + roomB.height / 2);

        // L-shaped corridor
        if (Random.value > 0.5f)
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

    private void GenerateCaves()
    {
        // Random fill
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                if (x == 0 || x == _width - 1 || y == 0 || y == _height - 1)
                    _map[x, y] = WALL;
                else
                    _map[x, y] = Random.value < _fillProbability ? WALL : EMPTY;
            }
        }

        // Smooth
        for (int i = 0; i < _smoothIterations; i++)
            SmoothCaves();
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

    private void PlaceDecorations()
    {
        foreach (var room in _rooms)
        {
            // Place chest in some rooms
            if (_placeChests && Random.value < 0.4f)
            {
                int cx = room.x + room.width / 2;
                int cy = room.y + room.height / 2;
                if (cx >= 0 && cx < _width && cy >= 0 && cy < _height && _map[cx, cy] == EMPTY)
                    _map[cx, cy] = CHEST;
            }

            // Place torches
            if (_placeTorches)
            {
                PlaceTorchesInRoom(room);
            }

            // Place enemy spawn
            if (_placeEnemySpawns && Random.value < 0.6f)
            {
                int sx = room.x + Random.Range(1, room.width - 1);
                int sy = room.y + Random.Range(1, room.height - 1);
                if (sx >= 0 && sx < _width && sy >= 0 && sy < _height && _map[sx, sy] == EMPTY)
                    _map[sx, sy] = SPAWN;
            }
        }

        // Place doors at corridor-room intersections
        if (_placeDoors)
        {
            PlaceDoors();
        }
    }

    private void PlaceTorchesInRoom(RectInt room)
    {
        // Place torches in corners
        Vector2Int[] corners = new Vector2Int[]
        {
            new Vector2Int(room.x + 1, room.y + 1),
            new Vector2Int(room.x + room.width - 2, room.y + 1),
            new Vector2Int(room.x + 1, room.y + room.height - 2),
            new Vector2Int(room.x + room.width - 2, room.y + room.height - 2)
        };

        foreach (var corner in corners)
        {
            if (corner.x >= 0 && corner.x < _width && corner.y >= 0 && corner.y < _height)
            {
                if (_map[corner.x, corner.y] == EMPTY)
                    _map[corner.x, corner.y] = TORCH;
            }
        }
    }

    private void PlaceDoors()
    {
        // Find corridor cells adjacent to rooms
        foreach (var corridor in _corridors)
        {
            bool adjacentToRoom = false;
            bool adjacentToWall = false;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = corridor.x + dx;
                    int ny = corridor.y + dy;

                    if (nx >= 0 && nx < _width && ny >= 0 && ny < _height)
                    {
                        if (_map[nx, ny] == WALL) adjacentToWall = true;
                        // Check if in a room
                        foreach (var room in _rooms)
                        {
                            if (room.Contains(new Vector2Int(nx, ny)))
                            {
                                adjacentToRoom = true;
                                break;
                            }
                        }
                    }
                }
            }

            if (adjacentToRoom && adjacentToWall && Random.value < 0.3f)
            {
                _map[corridor.x, corridor.y] = DOOR;
            }
        }
    }

    private Texture2D MapToTexture()
    {
        Texture2D texture = new Texture2D(_width, _height);
        Color[] colors = new Color[_width * _height];

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                Color color = _map[x, y] switch
                {
                    WALL => new Color(0.3f, 0.3f, 0.3f),
                    EMPTY => new Color(0.6f, 0.5f, 0.4f),
                    DOOR => new Color(0.6f, 0.3f, 0.1f),
                    CHEST => Color.yellow,
                    TORCH => new Color(1f, 0.6f, 0.2f),
                    SPAWN => Color.red,
                    _ => Color.black
                };
                colors[y * _width + x] = color;
            }
        }

        texture.SetPixels(colors);
        texture.filterMode = FilterMode.Point;
        texture.Apply();
        return texture;
    }

    private void GenerateDungeon()
    {
        if (_map == null)
            GeneratePreview();

        // Create parent object
        GameObject dungeonGO = new GameObject(_dungeonName);
        Undo.RegisterCreatedObjectUndo(dungeonGO, "Generate Dungeon");

        GameObject floorsParent = new GameObject("Floors");
        floorsParent.transform.SetParent(dungeonGO.transform);

        GameObject wallsParent = new GameObject("Walls");
        wallsParent.transform.SetParent(dungeonGO.transform);

        GameObject propsParent = new GameObject("Props");
        propsParent.transform.SetParent(dungeonGO.transform);

        Material floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        floorMat.color = new Color(0.4f, 0.35f, 0.3f);

        Material wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        wallMat.color = new Color(0.3f, 0.25f, 0.2f);

        // Generate geometry
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
                    case TORCH:
                    case SPAWN:
                        CreateFloor(floorsParent.transform, worldPos, floorMat);
                        break;

                    case WALL:
                        CreateWall(wallsParent.transform, worldPos, wallMat);
                        break;
                }

                // Decorations
                switch (_map[x, y])
                {
                    case DOOR:
                        CreateDoor(propsParent.transform, worldPos);
                        break;
                    case CHEST:
                        CreateChest(propsParent.transform, worldPos);
                        break;
                    case TORCH:
                        CreateTorch(propsParent.transform, worldPos);
                        break;
                    case SPAWN:
                        CreateSpawnPoint(propsParent.transform, worldPos);
                        break;
                }
            }
        }

        Selection.activeGameObject = dungeonGO;
        Debug.Log($"[DungeonGenerator] Dungeon '{_dungeonName}' generated with {_rooms.Count} rooms!");
    }

    private void CreateFloor(Transform parent, Vector3 pos, Material mat)
    {
        GameObject floor;
        if (_floorPrefab != null)
        {
            floor = (GameObject)PrefabUtility.InstantiatePrefab(_floorPrefab, parent);
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
            wall = (GameObject)PrefabUtility.InstantiatePrefab(_wallPrefab, parent);
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

    private void CreateDoor(Transform parent, Vector3 pos)
    {
        GameObject door;
        if (_doorPrefab != null)
        {
            door = (GameObject)PrefabUtility.InstantiatePrefab(_doorPrefab, parent);
        }
        else
        {
            door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.transform.SetParent(parent);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.5f, 0.3f, 0.1f);
            door.GetComponent<MeshRenderer>().material = mat;
        }

        door.name = "Door";
        door.transform.position = pos + new Vector3(0, 1.5f, 0);
        door.transform.localScale = new Vector3(_cellSize * 0.9f, 3f, 0.2f);
        door.tag = "Interactable";
    }

    private void CreateChest(Transform parent, Vector3 pos)
    {
        GameObject chest;
        if (_chestPrefab != null)
        {
            chest = (GameObject)PrefabUtility.InstantiatePrefab(_chestPrefab, parent);
        }
        else
        {
            chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chest.transform.SetParent(parent);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.5f, 0.4f, 0.1f);
            chest.GetComponent<MeshRenderer>().material = mat;
        }

        chest.name = "Chest";
        chest.transform.position = pos + new Vector3(0, 0.4f, 0);
        chest.transform.localScale = new Vector3(0.8f, 0.6f, 0.5f);
        chest.tag = "Interactable";
    }

    private void CreateTorch(Transform parent, Vector3 pos)
    {
        GameObject torch = new GameObject("Torch");
        torch.transform.SetParent(parent);
        torch.transform.position = pos + new Vector3(0, 2f, 0);

        // Add point light
        Light light = torch.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.6f, 0.2f);
        light.intensity = 3f;
        light.range = 8f;

        if (_torchPrefab != null)
        {
            GameObject torchModel = (GameObject)PrefabUtility.InstantiatePrefab(_torchPrefab, torch.transform);
            torchModel.transform.localPosition = Vector3.zero;
        }
    }

    private void CreateSpawnPoint(Transform parent, Vector3 pos)
    {
        GameObject spawn;
        if (_enemySpawnPrefab != null)
        {
            spawn = (GameObject)PrefabUtility.InstantiatePrefab(_enemySpawnPrefab, parent);
        }
        else
        {
            spawn = new GameObject("EnemySpawn");
            spawn.transform.SetParent(parent);
        }

        spawn.name = "EnemySpawn";
        spawn.transform.position = pos + new Vector3(0, 0.1f, 0);
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
