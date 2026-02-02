using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gestionnaire des biomes - determine le biome a chaque position du monde.
/// </summary>
public class BiomeManager : MonoBehaviour
{
    #region Singleton

    public static BiomeManager Instance { get; private set; }

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

    #region Settings

    [Header("Biomes")]
    [SerializeField] private BiomeData[] _availableBiomes;

    [Header("World Generation")]
    [SerializeField] private int _worldSeed = 42;
    [SerializeField] private float _temperatureScale = 100f;
    [SerializeField] private float _humidityScale = 80f;

    [Header("References")]
    [SerializeField] private Terrain _terrain;

    #endregion

    #region Cached Data

    private float[,] _temperatureMap;
    private float[,] _humidityMap;
    private BiomeData[,] _biomeMap;
    private int _mapWidth;
    private int _mapHeight;

    #endregion

    #region Public API

    /// <summary>
    /// Initialise les maps de biomes pour le monde.
    /// </summary>
    public void InitializeBiomeMap(int width, int height, float[,] heightmap = null)
    {
        _mapWidth = width;
        _mapHeight = height;

        System.Random rng = new System.Random(_worldSeed);

        // Generate temperature and humidity maps
        _temperatureMap = GenerateNoiseMap(width, height, _temperatureScale, rng.Next());
        _humidityMap = GenerateNoiseMap(width, height, _humidityScale, rng.Next());

        // Assign biomes
        // NOTE: Using [x, z] indexing to match Unity terrain conventions
        // 'width' = X dimension, 'height' = Z dimension (terrain depth)
        _biomeMap = new BiomeData[width, height];

        for (int z = 0; z < height; z++)  // z = terrain depth (was misleadingly named 'y')
        {
            for (int x = 0; x < width; x++)
            {
                // IMPORTANT: heightmap uses [x, z] indexing to match terrain
                float heightValue = heightmap != null ? heightmap[x, z] : 0.5f;
                float temperature = _temperatureMap[x, z];
                float humidity = _humidityMap[x, z];

                _biomeMap[x, z] = GetBestBiome(heightValue, temperature, humidity);
            }
        }

        Debug.Log($"[BiomeManager] Biome map initialized: {width}x{height} (X x Z)");
    }

    /// <summary>
    /// Obtient le biome a une position monde.
    /// </summary>
    public BiomeData GetBiomeAt(Vector3 worldPosition)
    {
        // CRITICAL FIX: Check for null before accessing Length
        if (_biomeMap == null)
            return (_availableBiomes != null && _availableBiomes.Length > 0) ? _availableBiomes[0] : null;

        // Convert world position to map coordinates
        // Using [x, z] indexing consistently
        int x = WorldToMapX(worldPosition.x);
        int z = WorldToMapZ(worldPosition.z);  // Renamed from 'y' to 'z' for clarity

        x = Mathf.Clamp(x, 0, _mapWidth - 1);
        z = Mathf.Clamp(z, 0, _mapHeight - 1);

        return _biomeMap[x, z];
    }

    /// <summary>
    /// Obtient le biome par hauteur, temperature et humidite.
    /// </summary>
    public BiomeData GetBiomeForConditions(float height, float temperature, float humidity)
    {
        return GetBestBiome(height, temperature, humidity);
    }

    /// <summary>
    /// Genere des objets pour un chunk du monde.
    /// </summary>
    public List<BiomeSpawnData> GenerateChunkObjects(Vector2Int chunkCoord, int chunkSize, float[,] chunkHeightmap)
    {
        List<BiomeSpawnData> spawns = new List<BiomeSpawnData>();

        System.Random rng = new System.Random(GetChunkSeed(chunkCoord));

        for (int z = 0; z < chunkSize; z++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                int worldX = chunkCoord.x * chunkSize + x;
                int worldZ = chunkCoord.y * chunkSize + z;

                float height = chunkHeightmap[x, z];

                // Get biome at this position
                BiomeData biome = GetBiomeAtMapCoord(worldX, worldZ);
                if (biome == null) continue;

                // Try spawn vegetation
                float vegChance = biome.vegetationDensity / 100f;
                if ((float)rng.NextDouble() < vegChance)
                {
                    GameObject prefab = biome.GetRandomVegetation(rng);
                    if (prefab != null)
                    {
                        spawns.Add(CreateSpawnData(prefab, worldX, worldZ, height, biome.vegetation, rng));
                    }
                }

                // Try spawn props
                float propChance = biome.propsDensity / 100f;
                if ((float)rng.NextDouble() < propChance)
                {
                    GameObject prefab = biome.GetRandomProp(rng);
                    if (prefab != null)
                    {
                        spawns.Add(CreateSpawnData(prefab, worldX, worldZ, height, biome.props, rng));
                    }
                }
            }
        }

        return spawns;
    }

    /// <summary>
    /// Applique les parametres d'ambiance du biome.
    /// </summary>
    public void ApplyBiomeAmbiance(BiomeData biome)
    {
        if (biome == null) return;

        // Fog
        RenderSettings.fogColor = biome.fogColor;
        RenderSettings.fogDensity = biome.fogDensity;

        // Ambient
        RenderSettings.ambientLight = biome.ambientColor;
    }

    #endregion

    #region Private Methods

    private BiomeData GetBestBiome(float height, float temperature, float humidity)
    {
        BiomeData bestBiome = null;
        float bestScore = 0f;

        // CRITICAL FIX: Check for null before iterating
        if (_availableBiomes == null) return null;

        foreach (var biome in _availableBiomes)
        {
            float score = biome.GetMatchScore(height, temperature, humidity);
            if (score > bestScore)
            {
                bestScore = score;
                bestBiome = biome;
            }
        }

        // Fallback to first biome (already checked _availableBiomes != null above)
        if (bestBiome == null && _availableBiomes.Length > 0)
            bestBiome = _availableBiomes[0];

        return bestBiome;
    }

    private BiomeData GetBiomeAtMapCoord(int x, int y)
    {
        // CRITICAL FIX: Check for null before accessing Length
        if (_biomeMap == null)
            return (_availableBiomes != null && _availableBiomes.Length > 0) ? _availableBiomes[0] : null;

        x = Mathf.Clamp(x, 0, _mapWidth - 1);
        y = Mathf.Clamp(y, 0, _mapHeight - 1);

        return _biomeMap[x, y];
    }

    private float[,] GenerateNoiseMap(int width, int height, float scale, int seed)
    {
        float[,] map = new float[width, height];
        System.Random rng = new System.Random(seed);

        float offsetX = rng.Next(-100000, 100000);
        float offsetY = rng.Next(-100000, 100000);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float sampleX = (x + offsetX) / scale;
                float sampleY = (y + offsetY) / scale;

                map[x, y] = Mathf.PerlinNoise(sampleX, sampleY);
            }
        }

        return map;
    }

    private BiomeSpawnData CreateSpawnData(GameObject prefab, int worldX, int worldZ, float height, BiomeObject[] objects, System.Random rng)
    {
        // Find the matching object for scale/rotation settings
        BiomeObject settings = null;
        foreach (var obj in objects)
        {
            if (obj.prefab == prefab)
            {
                settings = obj;
                break;
            }
        }

        float scale = settings != null
            ? Mathf.Lerp(settings.minScale, settings.maxScale, (float)rng.NextDouble())
            : 1f;

        float yRotation = settings != null && settings.randomYRotation
            ? (float)rng.NextDouble() * 360f
            : 0f;

        float yOffset = settings?.yOffset ?? 0f;

        return new BiomeSpawnData
        {
            prefab = prefab,
            position = new Vector3(worldX, height + yOffset, worldZ),
            rotation = Quaternion.Euler(0, yRotation, 0),
            scale = Vector3.one * scale,
            alignToSurface = settings?.alignToSurface ?? false
        };
    }

    private int WorldToMapX(float worldX)
    {
        if (_terrain != null)
        {
            Vector3 terrainPos = _terrain.transform.position;
            Vector3 terrainSize = _terrain.terrainData.size;
            return Mathf.FloorToInt((worldX - terrainPos.x) / terrainSize.x * _mapWidth);
        }
        return Mathf.FloorToInt(worldX);
    }

    private int WorldToMapZ(float worldZ)
    {
        if (_terrain != null)
        {
            Vector3 terrainPos = _terrain.transform.position;
            Vector3 terrainSize = _terrain.terrainData.size;
            return Mathf.FloorToInt((worldZ - terrainPos.z) / terrainSize.z * _mapHeight);
        }
        return Mathf.FloorToInt(worldZ);
    }

    private int GetChunkSeed(Vector2Int coord)
    {
        unchecked
        {
            int hash = _worldSeed;
            hash = hash * 397 ^ coord.x;
            hash = hash * 397 ^ coord.y;
            return hash;
        }
    }

    #endregion
}

/// <summary>
/// Donnees de spawn pour un objet de biome.
/// </summary>
public struct BiomeSpawnData
{
    public GameObject prefab;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
    public bool alignToSurface;
}
