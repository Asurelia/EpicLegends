using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generateur de village procedural.
/// Place des batiments, routes et decorations de village.
/// </summary>
public class VillageGenerator : MonoBehaviour
{
    #region Serialized Fields

    [Header("Village Settings")]
    [SerializeField] private float _villageRadius = 30f;
    [SerializeField] private int _minBuildings = 5;
    [SerializeField] private int _maxBuildings = 12;
    [SerializeField] private float _buildingSpacing = 8f;

    [Header("Building Prefabs")]
    [SerializeField] private GameObject[] _housePrefabs;
    [SerializeField] private GameObject[] _shopPrefabs;
    [SerializeField] private GameObject _innPrefab;
    [SerializeField] private GameObject _blacksmithPrefab;
    [SerializeField] private GameObject _wellPrefab;
    [SerializeField] private GameObject _marketStallPrefab;

    [Header("Decoration Prefabs")]
    [SerializeField] private GameObject[] _fencePrefabs;
    [SerializeField] private GameObject[] _lampPrefabs;
    [SerializeField] private GameObject[] _benchPrefabs;
    [SerializeField] private GameObject[] _cartPrefabs;

    [Header("NPC Settings")]
    [SerializeField] private GameObject _villagerPrefab;
    [SerializeField] private int _minVillagers = 3;
    [SerializeField] private int _maxVillagers = 8;

    #endregion

    #region Private Fields

    private Vector3 _centerPosition;
    private int _seed;
    private Terrain _terrain;
    private System.Random _rng;

    private List<VillageBuilding> _buildings;
    private List<Vector3> _pathNodes;
    private Bounds _villageBounds;

    #endregion

    #region Properties

    public Vector3 CenterPosition => _centerPosition;
    public float Radius => _villageRadius;
    public List<VillageBuilding> Buildings => _buildings;

    #endregion

    #region Initialization

    /// <summary>
    /// Initialise le generateur de village.
    /// </summary>
    public void Initialize(Vector3 center, int seed, Terrain terrain)
    {
        _centerPosition = center;
        _seed = seed;
        _terrain = terrain;
        _rng = new System.Random(seed);

        _buildings = new List<VillageBuilding>();
        _pathNodes = new List<Vector3>();

        // Calculate village bounds
        _villageBounds = new Bounds(_centerPosition, Vector3.one * _villageRadius * 2f);

        // Generate the village
        GenerateVillage();
    }

    #endregion

    #region Generation

    private void GenerateVillage()
    {
        Debug.Log($"[VillageGenerator] Generating village at {_centerPosition}");

        // Step 1: Generate building positions
        GenerateBuildingPositions();

        // Step 2: Place buildings
        PlaceBuildings();

        // Step 3: Generate paths between buildings
        GeneratePaths();

        // Step 4: Add decorations
        AddDecorations();

        // Step 5: Spawn villagers
        SpawnVillagers();

        Debug.Log($"[VillageGenerator] Village generated with {_buildings.Count} buildings");
    }

    private void GenerateBuildingPositions()
    {
        int targetBuildings = _rng.Next(_minBuildings, _maxBuildings + 1);

        // Place well/center feature first
        Vector3 centerPos = GetTerrainPosition(_centerPosition);
        _pathNodes.Add(centerPos);

        // Use Poisson disk sampling for building placement
        List<Vector2> positions = PoissonDiskSampling(_villageRadius, _buildingSpacing, targetBuildings * 2);

        foreach (var pos2D in positions)
        {
            Vector3 worldPos = _centerPosition + new Vector3(pos2D.x, 0, pos2D.y);
            worldPos = GetTerrainPosition(worldPos);

            // Check if position is valid (not too steep, not underwater)
            if (IsValidBuildingPosition(worldPos))
            {
                _buildings.Add(new VillageBuilding
                {
                    Position = worldPos,
                    Rotation = Quaternion.Euler(0, _rng.Next(0, 4) * 90f, 0),
                    Type = DetermineBuildingType(_buildings.Count)
                });

                _pathNodes.Add(worldPos);

                if (_buildings.Count >= targetBuildings)
                    break;
            }
        }
    }

    private List<Vector2> PoissonDiskSampling(float radius, float minDist, int maxSamples)
    {
        List<Vector2> points = new List<Vector2>();
        List<Vector2> active = new List<Vector2>();

        // Start with center point
        active.Add(Vector2.zero);
        points.Add(Vector2.zero);

        int k = 30; // Attempts per point

        while (active.Count > 0 && points.Count < maxSamples)
        {
            int randIndex = _rng.Next(0, active.Count);
            Vector2 center = active[randIndex];
            bool found = false;

            for (int i = 0; i < k; i++)
            {
                float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
                float dist = minDist + (float)_rng.NextDouble() * minDist;

                Vector2 newPoint = center + new Vector2(
                    Mathf.Cos(angle) * dist,
                    Mathf.Sin(angle) * dist
                );

                // Check if within radius
                if (newPoint.magnitude > radius) continue;

                // Check distance to all existing points
                bool tooClose = false;
                foreach (var p in points)
                {
                    if (Vector2.Distance(newPoint, p) < minDist)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    points.Add(newPoint);
                    active.Add(newPoint);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                active.RemoveAt(randIndex);
            }
        }

        return points;
    }

    private VillageBuildingType DetermineBuildingType(int buildingIndex)
    {
        // First few buildings are essential
        switch (buildingIndex)
        {
            case 0:
                return VillageBuildingType.Inn;
            case 1:
                return VillageBuildingType.Blacksmith;
            case 2:
            case 3:
                return VillageBuildingType.Shop;
            default:
                // Remaining are mostly houses with occasional shops
                return _rng.NextDouble() < 0.2 ? VillageBuildingType.Shop : VillageBuildingType.House;
        }
    }

    private bool IsValidBuildingPosition(Vector3 position)
    {
        // Check height (not underwater)
        if (WaterSystem.Instance != null && WaterSystem.Instance.IsPositionInWater(position))
            return false;

        // Check slope
        float slope = GetTerrainSlope(position);
        if (slope > 0.3f)
            return false;

        // Check distance to center
        float distToCenter = Vector3.Distance(position, _centerPosition);
        if (distToCenter > _villageRadius)
            return false;

        return true;
    }

    private void PlaceBuildings()
    {
        // Place well at center
        if (_wellPrefab != null)
        {
            Vector3 wellPos = GetTerrainPosition(_centerPosition);
            Instantiate(_wellPrefab, wellPos, Quaternion.identity, transform);
        }

        // Place each building
        foreach (var building in _buildings)
        {
            GameObject prefab = GetBuildingPrefab(building.Type);

            if (prefab != null)
            {
                GameObject obj = Instantiate(prefab, building.Position, building.Rotation, transform);
                building.GameObject = obj;

                // Flatten terrain under building
                if (_terrain != null)
                {
                    FlattenTerrainUnderBuilding(building.Position, GetBuildingSize(prefab));
                }
            }
        }
    }

    private GameObject GetBuildingPrefab(VillageBuildingType type)
    {
        switch (type)
        {
            case VillageBuildingType.House:
                return _housePrefabs != null && _housePrefabs.Length > 0
                    ? _housePrefabs[_rng.Next(0, _housePrefabs.Length)]
                    : null;

            case VillageBuildingType.Shop:
                return _shopPrefabs != null && _shopPrefabs.Length > 0
                    ? _shopPrefabs[_rng.Next(0, _shopPrefabs.Length)]
                    : _marketStallPrefab;

            case VillageBuildingType.Inn:
                return _innPrefab;

            case VillageBuildingType.Blacksmith:
                return _blacksmithPrefab;

            default:
                return null;
        }
    }

    private float GetBuildingSize(GameObject prefab)
    {
        var renderer = prefab.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            return Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.z);
        }
        return 5f;
    }

    private void GeneratePaths()
    {
        // Simple path generation - connect all buildings to center
        foreach (var building in _buildings)
        {
            // Path from building to nearest point (center or another building)
            Vector3 nearestNode = _centerPosition;
            float nearestDist = Vector3.Distance(building.Position, _centerPosition);

            foreach (var other in _buildings)
            {
                if (other == building) continue;

                float dist = Vector3.Distance(building.Position, other.Position);
                if (dist < nearestDist && dist < _buildingSpacing * 2f)
                {
                    nearestDist = dist;
                    nearestNode = other.Position;
                }
            }

            // Create path markers (for potential path rendering)
            building.PathTarget = nearestNode;
        }
    }

    private void AddDecorations()
    {
        // Add lamps near buildings
        if (_lampPrefabs != null && _lampPrefabs.Length > 0)
        {
            foreach (var building in _buildings)
            {
                if (_rng.NextDouble() < 0.6) // 60% chance
                {
                    Vector3 lampPos = building.Position + building.Rotation * new Vector3(3f, 0, 0);
                    lampPos = GetTerrainPosition(lampPos);

                    GameObject lamp = _lampPrefabs[_rng.Next(0, _lampPrefabs.Length)];
                    Instantiate(lamp, lampPos, Quaternion.identity, transform);
                }
            }
        }

        // Add benches
        if (_benchPrefabs != null && _benchPrefabs.Length > 0)
        {
            int benchCount = _rng.Next(2, 5);
            for (int i = 0; i < benchCount; i++)
            {
                Vector2 offset = Random.insideUnitCircle * _villageRadius * 0.5f;
                Vector3 pos = _centerPosition + new Vector3(offset.x, 0, offset.y);
                pos = GetTerrainPosition(pos);

                if (IsValidBuildingPosition(pos))
                {
                    GameObject bench = _benchPrefabs[_rng.Next(0, _benchPrefabs.Length)];
                    float rotation = _rng.Next(0, 4) * 90f;
                    Instantiate(bench, pos, Quaternion.Euler(0, rotation, 0), transform);
                }
            }
        }

        // Add carts
        if (_cartPrefabs != null && _cartPrefabs.Length > 0 && _rng.NextDouble() < 0.5)
        {
            Vector2 offset = Random.insideUnitCircle * _villageRadius * 0.3f;
            Vector3 pos = _centerPosition + new Vector3(offset.x, 0, offset.y);
            pos = GetTerrainPosition(pos);

            if (IsValidBuildingPosition(pos))
            {
                GameObject cart = _cartPrefabs[_rng.Next(0, _cartPrefabs.Length)];
                float rotation = (float)_rng.NextDouble() * 360f;
                Instantiate(cart, pos, Quaternion.Euler(0, rotation, 0), transform);
            }
        }
    }

    private void SpawnVillagers()
    {
        if (_villagerPrefab == null) return;

        int villagerCount = _rng.Next(_minVillagers, _maxVillagers + 1);

        for (int i = 0; i < villagerCount; i++)
        {
            // Spawn near a random building
            if (_buildings.Count > 0)
            {
                var building = _buildings[_rng.Next(0, _buildings.Count)];
                Vector2 offset = Random.insideUnitCircle * 5f;
                Vector3 pos = building.Position + new Vector3(offset.x, 0, offset.y);
                pos = GetTerrainPosition(pos);

                GameObject villager = Instantiate(_villagerPrefab, pos, Quaternion.identity, transform);
                villager.name = $"Villager_{i}";

                // Assign home building
                var npc = villager.GetComponent<NPCInteractable>();
                if (npc != null)
                {
                    // Configure NPC for village behavior
                }
            }
        }
    }

    #endregion

    #region Terrain Helpers

    private Vector3 GetTerrainPosition(Vector3 worldPosition)
    {
        if (_terrain != null)
        {
            float y = _terrain.SampleHeight(worldPosition);
            return new Vector3(worldPosition.x, y, worldPosition.z);
        }
        return worldPosition;
    }

    private float GetTerrainSlope(Vector3 position)
    {
        if (_terrain == null) return 0f;

        Vector3 terrainPos = _terrain.transform.position;
        Vector3 terrainSize = _terrain.terrainData.size;

        float normX = (position.x - terrainPos.x) / terrainSize.x;
        float normZ = (position.z - terrainPos.z) / terrainSize.z;

        return _terrain.terrainData.GetSteepness(normX, normZ) / 90f;
    }

    private void FlattenTerrainUnderBuilding(Vector3 position, float size)
    {
        if (_terrain == null) return;

        TerrainData terrainData = _terrain.terrainData;
        Vector3 terrainPos = _terrain.transform.position;

        int heightmapRes = terrainData.heightmapResolution;
        float terrainWidth = terrainData.size.x;
        float terrainHeight = terrainData.size.z;

        // Calculate area to flatten
        float targetHeight = (position.y - terrainPos.y) / terrainData.size.y;
        float halfSize = size * 0.6f;

        int startX = Mathf.FloorToInt((position.x - terrainPos.x - halfSize) / terrainWidth * heightmapRes);
        int startZ = Mathf.FloorToInt((position.z - terrainPos.z - halfSize) / terrainHeight * heightmapRes);
        int endX = Mathf.CeilToInt((position.x - terrainPos.x + halfSize) / terrainWidth * heightmapRes);
        int endZ = Mathf.CeilToInt((position.z - terrainPos.z + halfSize) / terrainHeight * heightmapRes);

        startX = Mathf.Clamp(startX, 0, heightmapRes - 1);
        startZ = Mathf.Clamp(startZ, 0, heightmapRes - 1);
        endX = Mathf.Clamp(endX, 0, heightmapRes - 1);
        endZ = Mathf.Clamp(endZ, 0, heightmapRes - 1);

        int width = endX - startX;
        int height = endZ - startZ;

        if (width <= 0 || height <= 0) return;

        float[,] heights = terrainData.GetHeights(startX, startZ, width, height);

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                heights[z, x] = Mathf.Lerp(heights[z, x], targetHeight, 0.8f);
            }
        }

        terrainData.SetHeights(startX, startZ, heights);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Obtient le batiment le plus proche d'une position.
    /// </summary>
    public VillageBuilding GetNearestBuilding(Vector3 position)
    {
        VillageBuilding nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var building in _buildings)
        {
            float dist = Vector3.Distance(position, building.Position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = building;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Verifie si une position est dans le village.
    /// </summary>
    public bool IsInVillage(Vector3 position)
    {
        return _villageBounds.Contains(position);
    }

    #endregion
}

/// <summary>
/// Types de batiments de village.
/// </summary>
public enum VillageBuildingType
{
    House,
    Shop,
    Inn,
    Blacksmith,
    Well,
    MarketStall
}

/// <summary>
/// Donnees d'un batiment de village.
/// </summary>
public class VillageBuilding
{
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    public VillageBuildingType Type { get; set; }
    public GameObject GameObject { get; set; }
    public Vector3 PathTarget { get; set; }
}
