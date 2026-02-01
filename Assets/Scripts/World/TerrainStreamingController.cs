using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controleur de streaming de terrain.
/// Charge/decharge les chunks de terrain autour du joueur pour optimiser les performances.
/// </summary>
public class TerrainStreamingController : MonoBehaviour
{
    #region Singleton

    public static TerrainStreamingController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    #endregion

    #region Serialized Fields

    [Header("Streaming Settings")]
    [SerializeField] private int _chunkSize = 128;
    [SerializeField] private int _viewDistance = 3;
    [SerializeField] private float _updateInterval = 0.5f;
    [SerializeField] private int _chunksPerFrame = 2;

    [Header("LOD Settings")]
    [SerializeField] private float _detailDistance = 100f;
    [SerializeField] private float _treeDistance = 200f;
    [SerializeField] private float _billboardStart = 150f;

    [Header("Performance")]
    [SerializeField] private bool _asyncLoading = true;
    [SerializeField] private bool _unloadDistantChunks = true;
    [SerializeField] private int _maxLoadedChunks = 25;

    [Header("Debug")]
    [SerializeField] private bool _showChunkBorders = false;
    [SerializeField] private bool _logChunkLoading = false;

    #endregion

    #region Private Fields

    private Transform _playerTransform;
    private Vector2Int _currentPlayerChunk;
    private Dictionary<Vector2Int, TerrainChunk> _loadedChunks;
    private Queue<Vector2Int> _loadQueue;
    private Queue<Vector2Int> _unloadQueue;
    private float _updateTimer;
    private bool _isLoading;

    #endregion

    #region Properties

    public int LoadedChunkCount => _loadedChunks?.Count ?? 0;
    public Vector2Int CurrentPlayerChunk => _currentPlayerChunk;
    public int ChunkSize => _chunkSize;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        _loadedChunks = new Dictionary<Vector2Int, TerrainChunk>();
        _loadQueue = new Queue<Vector2Int>();
        _unloadQueue = new Queue<Vector2Int>();

        // Find player
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
        }

        // Apply terrain LOD settings
        ApplyTerrainSettings();
    }

    private void Update()
    {
        if (_playerTransform == null) return;

        _updateTimer += Time.deltaTime;
        if (_updateTimer >= _updateInterval)
        {
            _updateTimer = 0f;
            UpdateStreaming();
        }

        // Process loading queue
        if (!_isLoading && _loadQueue.Count > 0)
        {
            StartCoroutine(ProcessLoadQueue());
        }

        // Process unload queue
        while (_unloadQueue.Count > 0)
        {
            UnloadChunk(_unloadQueue.Dequeue());
        }
    }

    private void OnDestroy()
    {
        // Cleanup all chunks
        if (_loadedChunks != null)
        {
            foreach (var chunk in _loadedChunks.Values)
            {
                chunk.Unload();
            }
            _loadedChunks.Clear();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Definit le joueur a suivre.
    /// </summary>
    public void SetPlayer(Transform player)
    {
        _playerTransform = player;
    }

    /// <summary>
    /// Force le rechargement de tous les chunks.
    /// </summary>
    public void ReloadAllChunks()
    {
        foreach (var coord in new List<Vector2Int>(_loadedChunks.Keys))
        {
            _unloadQueue.Enqueue(coord);
        }

        _loadQueue.Clear();
        UpdateChunksToLoad();
    }

    /// <summary>
    /// Obtient le chunk a une position.
    /// </summary>
    public TerrainChunk GetChunkAt(Vector3 worldPosition)
    {
        Vector2Int coord = WorldToChunkCoord(worldPosition);
        _loadedChunks.TryGetValue(coord, out TerrainChunk chunk);
        return chunk;
    }

    /// <summary>
    /// Verifie si une position est dans un chunk charge.
    /// </summary>
    public bool IsPositionLoaded(Vector3 worldPosition)
    {
        Vector2Int coord = WorldToChunkCoord(worldPosition);
        return _loadedChunks.ContainsKey(coord);
    }

    /// <summary>
    /// Definit la distance de vue.
    /// </summary>
    public void SetViewDistance(int distance)
    {
        _viewDistance = Mathf.Clamp(distance, 1, 5);
        ReloadAllChunks();
    }

    #endregion

    #region Private Methods

    private void ApplyTerrainSettings()
    {
        // Apply global terrain settings
        Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);

        foreach (var terrain in terrains)
        {
            terrain.detailObjectDistance = _detailDistance;
            terrain.treeDistance = _treeDistance;
            terrain.treeBillboardDistance = _billboardStart;
            terrain.heightmapPixelError = QualitySettings.GetQualityLevel() < 2 ? 10 : 5;
        }
    }

    private void UpdateStreaming()
    {
        Vector2Int newChunk = WorldToChunkCoord(_playerTransform.position);

        if (newChunk != _currentPlayerChunk)
        {
            _currentPlayerChunk = newChunk;
            UpdateChunksToLoad();
            UpdateChunksToUnload();

            if (_logChunkLoading)
            {
                Debug.Log($"[TerrainStreaming] Player moved to chunk {newChunk}");
            }
        }
    }

    private void UpdateChunksToLoad()
    {
        // Determine which chunks should be loaded
        HashSet<Vector2Int> shouldBeLoaded = new HashSet<Vector2Int>();

        for (int z = -_viewDistance; z <= _viewDistance; z++)
        {
            for (int x = -_viewDistance; x <= _viewDistance; x++)
            {
                Vector2Int coord = _currentPlayerChunk + new Vector2Int(x, z);

                // Check distance (circular view)
                float dist = Mathf.Sqrt(x * x + z * z);
                if (dist <= _viewDistance)
                {
                    shouldBeLoaded.Add(coord);
                }
            }
        }

        // Queue chunks that need to be loaded
        foreach (var coord in shouldBeLoaded)
        {
            if (!_loadedChunks.ContainsKey(coord) && !_loadQueue.Contains(coord))
            {
                _loadQueue.Enqueue(coord);
            }
        }
    }

    private void UpdateChunksToUnload()
    {
        if (!_unloadDistantChunks) return;

        List<Vector2Int> toUnload = new List<Vector2Int>();

        foreach (var kvp in _loadedChunks)
        {
            Vector2Int coord = kvp.Key;
            float dist = Vector2Int.Distance(coord, _currentPlayerChunk);

            if (dist > _viewDistance + 1)
            {
                toUnload.Add(coord);
            }
        }

        // Prioritize unloading furthest chunks
        toUnload.Sort((a, b) =>
        {
            float distA = Vector2Int.Distance(a, _currentPlayerChunk);
            float distB = Vector2Int.Distance(b, _currentPlayerChunk);
            return distB.CompareTo(distA);
        });

        foreach (var coord in toUnload)
        {
            if (!_unloadQueue.Contains(coord))
            {
                _unloadQueue.Enqueue(coord);
            }
        }

        // Also unload if over max chunks
        while (_loadedChunks.Count > _maxLoadedChunks && toUnload.Count > 0)
        {
            var coord = toUnload[0];
            toUnload.RemoveAt(0);
            _unloadQueue.Enqueue(coord);
        }
    }

    private IEnumerator ProcessLoadQueue()
    {
        _isLoading = true;
        int processed = 0;

        while (_loadQueue.Count > 0 && processed < _chunksPerFrame)
        {
            Vector2Int coord = _loadQueue.Dequeue();

            if (!_loadedChunks.ContainsKey(coord))
            {
                LoadChunk(coord);
                processed++;

                if (_asyncLoading)
                {
                    yield return null;
                }
            }
        }

        _isLoading = false;
    }

    private void LoadChunk(Vector2Int coord)
    {
        if (_loadedChunks.ContainsKey(coord)) return;

        TerrainChunk chunk = new TerrainChunk(coord, _chunkSize, transform);
        chunk.Load();

        _loadedChunks[coord] = chunk;

        if (_logChunkLoading)
        {
            Debug.Log($"[TerrainStreaming] Loaded chunk {coord}");
        }
    }

    private void UnloadChunk(Vector2Int coord)
    {
        if (!_loadedChunks.TryGetValue(coord, out TerrainChunk chunk))
            return;

        chunk.Unload();
        _loadedChunks.Remove(coord);

        if (_logChunkLoading)
        {
            Debug.Log($"[TerrainStreaming] Unloaded chunk {coord}");
        }
    }

    private Vector2Int WorldToChunkCoord(Vector3 worldPosition)
    {
        int x = Mathf.FloorToInt(worldPosition.x / _chunkSize);
        int z = Mathf.FloorToInt(worldPosition.z / _chunkSize);
        return new Vector2Int(x, z);
    }

    #endregion

    #region Debug

    private void OnDrawGizmos()
    {
        if (!_showChunkBorders || _loadedChunks == null) return;

        foreach (var kvp in _loadedChunks)
        {
            Vector2Int coord = kvp.Key;
            Vector3 center = new Vector3(
                (coord.x + 0.5f) * _chunkSize,
                0,
                (coord.y + 0.5f) * _chunkSize
            );

            // Color based on distance from player chunk
            float dist = Vector2Int.Distance(coord, _currentPlayerChunk);
            Gizmos.color = Color.Lerp(Color.green, Color.red, dist / _viewDistance);

            Gizmos.DrawWireCube(center, new Vector3(_chunkSize, 10f, _chunkSize));
        }
    }

    #endregion
}

/// <summary>
/// Represente un chunk de terrain.
/// </summary>
public class TerrainChunk
{
    public Vector2Int Coordinate { get; private set; }
    public int Size { get; private set; }
    public bool IsLoaded { get; private set; }
    public Bounds WorldBounds { get; private set; }

    private Transform _parent;
    private List<GameObject> _objects;

    public TerrainChunk(Vector2Int coord, int size, Transform parent)
    {
        Coordinate = coord;
        Size = size;
        _parent = parent;
        _objects = new List<GameObject>();

        Vector3 min = new Vector3(coord.x * size, 0, coord.y * size);
        Vector3 max = new Vector3((coord.x + 1) * size, 100, (coord.y + 1) * size);
        WorldBounds = new Bounds();
        WorldBounds.SetMinMax(min, max);
    }

    public void Load()
    {
        if (IsLoaded) return;

        // Request vegetation for this chunk from BiomeManager
        if (BiomeManager.Instance != null && ProceduralWorldGenerator.Instance != null)
        {
            var heightmap = ProceduralWorldGenerator.Instance.Heightmap;
            if (heightmap != null)
            {
                // Get chunk heightmap portion
                int worldSize = ProceduralWorldGenerator.Instance.WorldSize;
                int startX = Coordinate.x * Size;
                int startZ = Coordinate.y * Size;

                if (startX >= 0 && startX < worldSize && startZ >= 0 && startZ < worldSize)
                {
                    // Create chunk heightmap
                    int chunkSizeClamped = Mathf.Min(Size, worldSize - startX, worldSize - startZ);
                    float[,] chunkHeightmap = new float[chunkSizeClamped, chunkSizeClamped];

                    for (int z = 0; z < chunkSizeClamped; z++)
                    {
                        for (int x = 0; x < chunkSizeClamped; x++)
                        {
                            int wx = Mathf.Clamp(startX + x, 0, worldSize - 1);
                            int wz = Mathf.Clamp(startZ + z, 0, worldSize - 1);
                            chunkHeightmap[x, z] = heightmap[wx, wz];
                        }
                    }

                    // Generate objects for this chunk
                    var spawns = BiomeManager.Instance.GenerateChunkObjects(Coordinate, chunkSizeClamped, chunkHeightmap);

                    foreach (var spawn in spawns)
                    {
                        if (spawn.prefab != null)
                        {
                            GameObject obj = Object.Instantiate(spawn.prefab, spawn.position, spawn.rotation);
                            obj.transform.localScale = spawn.scale;
                            _objects.Add(obj);
                        }
                    }
                }
            }
        }

        IsLoaded = true;
    }

    public void Unload()
    {
        foreach (var obj in _objects)
        {
            if (obj != null)
            {
                // Unregister from LOD if needed
                if (LODController.Instance != null)
                {
                    LODController.Instance.UnregisterObject(obj);
                }
                Object.Destroy(obj);
            }
        }
        _objects.Clear();

        IsLoaded = false;
    }

    public bool ContainsPosition(Vector3 worldPosition)
    {
        return WorldBounds.Contains(worldPosition);
    }
}
