using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generateur de monde procedural complet.
/// Gere la generation du terrain, des biomes, de l'eau, des villages et des grottes.
/// </summary>
public class ProceduralWorldGenerator : MonoBehaviour
{
    #region Singleton

    public static ProceduralWorldGenerator Instance { get; private set; }

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

    #region Events

    public event Action<float> OnGenerationProgress;
    public event Action OnGenerationComplete;
    public event Action<WorldChunk> OnChunkGenerated;

    #endregion

    #region Serialized Fields - World Settings

    [Header("World Settings")]
    [SerializeField] private int _worldSeed = 42;
    [SerializeField] private int _worldSize = 512;
    [SerializeField] private float _worldScale = 1f;
    [SerializeField] private int _chunkSize = 64;

    [Header("Terrain Settings")]
    [SerializeField] private float _terrainHeight = 100f;
    [SerializeField] private float _waterLevel = 0.3f;
    [SerializeField] private TerrainNoiseSettings _terrainNoise;

    [Header("Feature Settings")]
    [SerializeField] private bool _generateWater = true;
    [SerializeField] private bool _generateVillages = true;
    [SerializeField] private bool _generateCaves = true;
    [SerializeField] private bool _generateVegetation = true;
    [SerializeField] private bool _generateClouds = true;

    [Header("References")]
    [SerializeField] private Terrain _terrain;
    [SerializeField] private Material _waterMaterial;
    [SerializeField] private Material _cloudMaterial;
    [SerializeField] private TerrainLayer[] _terrainLayers;

    [Header("Performance")]
    [SerializeField] private int _vegetationBatchSize = 100;
    [SerializeField] private float _generateDelay = 0.01f;

    #endregion

    #region Private Fields

    private float[,] _heightmap;
    private float[,] _moistureMap;
    private float[,] _temperatureMap;
    private Dictionary<Vector2Int, WorldChunk> _chunks;
    private System.Random _rng;
    private bool _isGenerating;

    // Feature positions
    private List<Vector3> _lakePositions;
    private List<Vector3> _villagePositions;
    private List<Vector3> _caveEntrances;
    private List<Vector3> _mountainPeaks;

    #endregion

    #region Properties

    public int WorldSeed => _worldSeed;
    public int WorldSize => _worldSize;
    public float TerrainHeight => _terrainHeight;
    public float WaterLevel => _waterLevel;
    public bool IsGenerating => _isGenerating;
    public float[,] Heightmap => _heightmap;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        _chunks = new Dictionary<Vector2Int, WorldChunk>();
        _lakePositions = new List<Vector3>();
        _villagePositions = new List<Vector3>();
        _caveEntrances = new List<Vector3>();
        _mountainPeaks = new List<Vector3>();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Genere un nouveau monde avec le seed specifie.
    /// </summary>
    public void GenerateWorld(int seed)
    {
        _worldSeed = seed;
        StartCoroutine(GenerateWorldCoroutine());
    }

    /// <summary>
    /// Genere un nouveau monde avec le seed actuel.
    /// </summary>
    public void GenerateWorld()
    {
        StartCoroutine(GenerateWorldCoroutine());
    }

    /// <summary>
    /// Obtient la hauteur du terrain a une position monde.
    /// </summary>
    public float GetTerrainHeight(float worldX, float worldZ)
    {
        if (_heightmap == null) return 0f;

        int x = Mathf.Clamp(Mathf.FloorToInt(worldX / _worldScale), 0, _worldSize - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(worldZ / _worldScale), 0, _worldSize - 1);

        return _heightmap[x, z] * _terrainHeight;
    }

    /// <summary>
    /// Verifie si une position est sous l'eau.
    /// </summary>
    public bool IsUnderwater(Vector3 worldPosition)
    {
        float terrainHeight = GetTerrainHeight(worldPosition.x, worldPosition.z);
        return terrainHeight < _waterLevel * _terrainHeight;
    }

    /// <summary>
    /// Obtient le chunk a une position.
    /// </summary>
    public WorldChunk GetChunkAt(Vector3 worldPosition)
    {
        Vector2Int coord = WorldToChunkCoord(worldPosition);
        _chunks.TryGetValue(coord, out WorldChunk chunk);
        return chunk;
    }

    /// <summary>
    /// Obtient les positions des lacs.
    /// </summary>
    public List<Vector3> GetLakePositions() => new List<Vector3>(_lakePositions);

    /// <summary>
    /// Obtient les positions des villages.
    /// </summary>
    public List<Vector3> GetVillagePositions() => new List<Vector3>(_villagePositions);

    /// <summary>
    /// Obtient les entrees de grottes.
    /// </summary>
    public List<Vector3> GetCaveEntrances() => new List<Vector3>(_caveEntrances);

    #endregion

    #region Generation Coroutine

    private IEnumerator GenerateWorldCoroutine()
    {
        if (_isGenerating) yield break;

        _isGenerating = true;
        _rng = new System.Random(_worldSeed);

        Debug.Log($"[ProceduralWorldGenerator] Starting world generation with seed {_worldSeed}");

        // Step 1: Generate heightmap (20%)
        OnGenerationProgress?.Invoke(0.05f);
        yield return StartCoroutine(GenerateHeightmapCoroutine());
        OnGenerationProgress?.Invoke(0.2f);

        // Step 2: Generate climate maps (10%)
        GenerateClimateMaps();
        OnGenerationProgress?.Invoke(0.3f);
        yield return new WaitForSeconds(_generateDelay);

        // Step 3: Identify features (10%)
        IdentifyWorldFeatures();
        OnGenerationProgress?.Invoke(0.4f);
        yield return new WaitForSeconds(_generateDelay);

        // Step 4: Apply terrain (15%)
        yield return StartCoroutine(ApplyTerrainCoroutine());
        OnGenerationProgress?.Invoke(0.55f);

        // Step 5: Generate water (10%)
        if (_generateWater)
        {
            yield return StartCoroutine(GenerateWaterCoroutine());
        }
        OnGenerationProgress?.Invoke(0.65f);

        // Step 6: Generate caves (5%)
        if (_generateCaves)
        {
            yield return StartCoroutine(GenerateCavesCoroutine());
        }
        OnGenerationProgress?.Invoke(0.7f);

        // Step 7: Generate villages (10%)
        if (_generateVillages)
        {
            yield return StartCoroutine(GenerateVillagesCoroutine());
        }
        OnGenerationProgress?.Invoke(0.8f);

        // Step 8: Generate vegetation (15%)
        if (_generateVegetation)
        {
            yield return StartCoroutine(GenerateVegetationCoroutine());
        }
        OnGenerationProgress?.Invoke(0.95f);

        // Step 9: Generate clouds (5%)
        if (_generateClouds)
        {
            GenerateClouds();
        }
        OnGenerationProgress?.Invoke(1f);

        _isGenerating = false;
        OnGenerationComplete?.Invoke();
        Debug.Log("[ProceduralWorldGenerator] World generation complete!");
    }

    #endregion

    #region Heightmap Generation

    private IEnumerator GenerateHeightmapCoroutine()
    {
        _heightmap = new float[_worldSize, _worldSize];

        int processed = 0;
        int total = _worldSize * _worldSize;

        for (int z = 0; z < _worldSize; z++)
        {
            for (int x = 0; x < _worldSize; x++)
            {
                _heightmap[x, z] = GenerateHeightValue(x, z);
                processed++;
            }

            // Yield every row for smooth generation
            if (z % 16 == 0)
            {
                yield return null;
            }
        }

        // Normalize heightmap
        NormalizeHeightmap();

        // Apply erosion simulation
        ApplyErosion();

        Debug.Log("[ProceduralWorldGenerator] Heightmap generated");
    }

    private float GenerateHeightValue(int x, int z)
    {
        float height = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxValue = 0f;

        float nx = (float)x / _worldSize;
        float nz = (float)z / _worldSize;

        // Multi-octave noise
        for (int i = 0; i < _terrainNoise.octaves; i++)
        {
            float sampleX = (nx * _terrainNoise.scale * frequency) + _rng.Next(-10000, 10000);
            float sampleZ = (nz * _terrainNoise.scale * frequency) + _rng.Next(-10000, 10000);

            float perlinValue = Mathf.PerlinNoise(sampleX, sampleZ);
            height += perlinValue * amplitude;

            maxValue += amplitude;
            amplitude *= _terrainNoise.persistence;
            frequency *= _terrainNoise.lacunarity;
        }

        height /= maxValue;

        // Apply redistribution for more interesting terrain
        height = Mathf.Pow(height, _terrainNoise.redistribution);

        // Add mountain ridges
        if (_terrainNoise.addRidges)
        {
            float ridgeNoise = GenerateRidgeNoise(nx, nz);
            height = Mathf.Lerp(height, ridgeNoise, _terrainNoise.ridgeWeight);
        }

        return height;
    }

    private float GenerateRidgeNoise(float nx, float nz)
    {
        float ridgeValue = 0f;
        float amplitude = 0.5f;
        float frequency = _terrainNoise.ridgeScale;

        for (int i = 0; i < 4; i++)
        {
            float sampleX = nx * frequency + _worldSeed;
            float sampleZ = nz * frequency + _worldSeed * 2;

            float noise = Mathf.PerlinNoise(sampleX, sampleZ);
            noise = Mathf.Abs(noise * 2f - 1f); // Create ridges
            noise = 1f - noise; // Invert

            ridgeValue += noise * amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return Mathf.Pow(ridgeValue, 2f);
    }

    private void NormalizeHeightmap()
    {
        float min = float.MaxValue;
        float max = float.MinValue;

        for (int z = 0; z < _worldSize; z++)
        {
            for (int x = 0; x < _worldSize; x++)
            {
                if (_heightmap[x, z] < min) min = _heightmap[x, z];
                if (_heightmap[x, z] > max) max = _heightmap[x, z];
            }
        }

        float range = max - min;
        if (range > 0)
        {
            for (int z = 0; z < _worldSize; z++)
            {
                for (int x = 0; x < _worldSize; x++)
                {
                    _heightmap[x, z] = (_heightmap[x, z] - min) / range;
                }
            }
        }
    }

    private void ApplyErosion()
    {
        // Simple thermal erosion simulation
        float talusAngle = 0.05f;
        int iterations = _terrainNoise.erosionIterations;

        for (int iter = 0; iter < iterations; iter++)
        {
            for (int z = 1; z < _worldSize - 1; z++)
            {
                for (int x = 1; x < _worldSize - 1; x++)
                {
                    float h = _heightmap[x, z];

                    // Check neighbors
                    float[] neighbors = new float[]
                    {
                        _heightmap[x - 1, z],
                        _heightmap[x + 1, z],
                        _heightmap[x, z - 1],
                        _heightmap[x, z + 1]
                    };

                    float minNeighbor = Mathf.Min(neighbors);
                    float diff = h - minNeighbor;

                    if (diff > talusAngle)
                    {
                        float transfer = diff * 0.5f;
                        _heightmap[x, z] -= transfer * 0.5f;

                        // Find which neighbor to transfer to
                        for (int n = 0; n < 4; n++)
                        {
                            if (neighbors[n] == minNeighbor)
                            {
                                int nx = x + (n == 0 ? -1 : n == 1 ? 1 : 0);
                                int nz = z + (n == 2 ? -1 : n == 3 ? 1 : 0);
                                _heightmap[nx, nz] += transfer * 0.5f;
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    #endregion

    #region Climate Maps

    private void GenerateClimateMaps()
    {
        _moistureMap = new float[_worldSize, _worldSize];
        _temperatureMap = new float[_worldSize, _worldSize];

        float moistureOffsetX = _rng.Next(-10000, 10000);
        float moistureOffsetZ = _rng.Next(-10000, 10000);
        float tempOffsetX = _rng.Next(-10000, 10000);
        float tempOffsetZ = _rng.Next(-10000, 10000);

        for (int z = 0; z < _worldSize; z++)
        {
            for (int x = 0; x < _worldSize; x++)
            {
                float nx = (float)x / _worldSize;
                float nz = (float)z / _worldSize;

                // Moisture - influenced by distance to water
                _moistureMap[x, z] = Mathf.PerlinNoise(
                    nx * 4f + moistureOffsetX,
                    nz * 4f + moistureOffsetZ
                );

                // Temperature - influenced by height and latitude
                float baseTemp = 1f - Mathf.Abs(nz - 0.5f) * 2f; // Cooler at poles
                float heightPenalty = _heightmap[x, z] * 0.5f; // Cooler at altitude
                float noiseTemp = Mathf.PerlinNoise(
                    nx * 3f + tempOffsetX,
                    nz * 3f + tempOffsetZ
                ) * 0.3f;

                _temperatureMap[x, z] = Mathf.Clamp01(baseTemp - heightPenalty + noiseTemp);
            }
        }

        // Initialize biome manager with maps
        if (BiomeManager.Instance != null)
        {
            BiomeManager.Instance.InitializeBiomeMap(_worldSize, _worldSize, _heightmap);
        }

        Debug.Log("[ProceduralWorldGenerator] Climate maps generated");
    }

    #endregion

    #region Feature Identification

    private void IdentifyWorldFeatures()
    {
        _lakePositions.Clear();
        _villagePositions.Clear();
        _caveEntrances.Clear();
        _mountainPeaks.Clear();

        // Find lakes (low areas below water level)
        FindLakes();

        // Find mountain peaks (local maxima)
        FindMountainPeaks();

        // Find suitable village locations
        FindVillageLocations();

        // Find cave entrance locations (mountainsides)
        FindCaveLocations();

        Debug.Log($"[ProceduralWorldGenerator] Features found: {_lakePositions.Count} lakes, " +
                  $"{_mountainPeaks.Count} peaks, {_villagePositions.Count} village sites, " +
                  $"{_caveEntrances.Count} cave sites");
    }

    private void FindLakes()
    {
        int sampleStep = 16;
        float minLakeSize = 0.1f;

        for (int z = sampleStep; z < _worldSize - sampleStep; z += sampleStep)
        {
            for (int x = sampleStep; x < _worldSize - sampleStep; x += sampleStep)
            {
                if (_heightmap[x, z] < _waterLevel)
                {
                    // Check if this is a lake center (local minimum)
                    bool isLakeCenter = true;
                    float totalLakeArea = 0;

                    for (int dz = -sampleStep; dz <= sampleStep; dz += sampleStep / 2)
                    {
                        for (int dx = -sampleStep; dx <= sampleStep; dx += sampleStep / 2)
                        {
                            int nx = x + dx;
                            int nz = z + dz;

                            if (nx >= 0 && nx < _worldSize && nz >= 0 && nz < _worldSize)
                            {
                                if (_heightmap[nx, nz] < _waterLevel)
                                {
                                    totalLakeArea++;
                                }
                            }
                        }
                    }

                    if (totalLakeArea > 5 && !IsNearExistingFeature(_lakePositions, x, z, sampleStep * 2))
                    {
                        Vector3 lakePos = new Vector3(
                            x * _worldScale,
                            _waterLevel * _terrainHeight,
                            z * _worldScale
                        );
                        _lakePositions.Add(lakePos);
                    }
                }
            }
        }
    }

    private void FindMountainPeaks()
    {
        int sampleStep = 32;
        float minPeakHeight = 0.7f;

        for (int z = sampleStep; z < _worldSize - sampleStep; z += sampleStep)
        {
            for (int x = sampleStep; x < _worldSize - sampleStep; x += sampleStep)
            {
                if (_heightmap[x, z] > minPeakHeight)
                {
                    bool isPeak = true;

                    // Check if higher than all neighbors
                    for (int dz = -sampleStep; dz <= sampleStep; dz += sampleStep)
                    {
                        for (int dx = -sampleStep; dx <= sampleStep; dx += sampleStep)
                        {
                            if (dx == 0 && dz == 0) continue;

                            int nx = x + dx;
                            int nz = z + dz;

                            if (nx >= 0 && nx < _worldSize && nz >= 0 && nz < _worldSize)
                            {
                                if (_heightmap[nx, nz] >= _heightmap[x, z])
                                {
                                    isPeak = false;
                                    break;
                                }
                            }
                        }
                        if (!isPeak) break;
                    }

                    if (isPeak && !IsNearExistingFeature(_mountainPeaks, x, z, sampleStep * 3))
                    {
                        Vector3 peakPos = new Vector3(
                            x * _worldScale,
                            _heightmap[x, z] * _terrainHeight,
                            z * _worldScale
                        );
                        _mountainPeaks.Add(peakPos);
                    }
                }
            }
        }
    }

    private void FindVillageLocations()
    {
        int sampleStep = 64;
        float minVillageHeight = _waterLevel + 0.05f;
        float maxVillageHeight = 0.5f;
        int maxVillages = 5;

        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int z = sampleStep; z < _worldSize - sampleStep; z += sampleStep)
        {
            for (int x = sampleStep; x < _worldSize - sampleStep; x += sampleStep)
            {
                float height = _heightmap[x, z];

                if (height > minVillageHeight && height < maxVillageHeight)
                {
                    // Check flatness
                    float variance = CalculateHeightVariance(x, z, 16);

                    if (variance < 0.02f)
                    {
                        // Check proximity to water (villages like to be near water)
                        bool nearWater = IsNearWater(x, z, 32);

                        if (nearWater)
                        {
                            candidates.Add(new Vector2Int(x, z));
                        }
                    }
                }
            }
        }

        // Select best candidates
        candidates.Sort((a, b) =>
        {
            float scoreA = CalculateVillageScore(a.x, a.y);
            float scoreB = CalculateVillageScore(b.x, b.y);
            return scoreB.CompareTo(scoreA);
        });

        for (int i = 0; i < Mathf.Min(maxVillages, candidates.Count); i++)
        {
            var pos = candidates[i];
            if (!IsNearExistingFeature(_villagePositions, pos.x, pos.y, 100))
            {
                Vector3 villagePos = new Vector3(
                    pos.x * _worldScale,
                    _heightmap[pos.x, pos.y] * _terrainHeight,
                    pos.y * _worldScale
                );
                _villagePositions.Add(villagePos);
            }
        }
    }

    private void FindCaveLocations()
    {
        int sampleStep = 48;
        float minCaveHeight = 0.4f;
        float maxCaveHeight = 0.75f;
        int maxCaves = 8;

        for (int z = sampleStep; z < _worldSize - sampleStep; z += sampleStep)
        {
            for (int x = sampleStep; x < _worldSize - sampleStep; x += sampleStep)
            {
                float height = _heightmap[x, z];

                if (height > minCaveHeight && height < maxCaveHeight)
                {
                    // Check for steep slope (mountainside)
                    float slope = CalculateSlope(x, z);

                    if (slope > 0.3f && slope < 0.7f)
                    {
                        if (!IsNearExistingFeature(_caveEntrances, x, z, 64))
                        {
                            Vector3 cavePos = new Vector3(
                                x * _worldScale,
                                height * _terrainHeight,
                                z * _worldScale
                            );
                            _caveEntrances.Add(cavePos);

                            if (_caveEntrances.Count >= maxCaves) return;
                        }
                    }
                }
            }
        }
    }

    private bool IsNearExistingFeature(List<Vector3> features, int x, int z, float minDistance)
    {
        Vector3 testPos = new Vector3(x * _worldScale, 0, z * _worldScale);

        foreach (var feature in features)
        {
            Vector3 featureFlat = new Vector3(feature.x, 0, feature.z);
            if (Vector3.Distance(testPos, featureFlat) < minDistance * _worldScale)
            {
                return true;
            }
        }
        return false;
    }

    private float CalculateHeightVariance(int centerX, int centerZ, int radius)
    {
        float sum = 0f;
        float sumSq = 0f;
        int count = 0;

        for (int z = -radius; z <= radius; z++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                int nx = centerX + x;
                int nz = centerZ + z;

                if (nx >= 0 && nx < _worldSize && nz >= 0 && nz < _worldSize)
                {
                    float h = _heightmap[nx, nz];
                    sum += h;
                    sumSq += h * h;
                    count++;
                }
            }
        }

        float mean = sum / count;
        float variance = (sumSq / count) - (mean * mean);
        return variance;
    }

    private bool IsNearWater(int x, int z, int searchRadius)
    {
        for (int dz = -searchRadius; dz <= searchRadius; dz += 4)
        {
            for (int dx = -searchRadius; dx <= searchRadius; dx += 4)
            {
                int nx = x + dx;
                int nz = z + dz;

                if (nx >= 0 && nx < _worldSize && nz >= 0 && nz < _worldSize)
                {
                    if (_heightmap[nx, nz] < _waterLevel)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private float CalculateSlope(int x, int z)
    {
        if (x <= 0 || x >= _worldSize - 1 || z <= 0 || z >= _worldSize - 1)
            return 0f;

        float dx = _heightmap[x + 1, z] - _heightmap[x - 1, z];
        float dz = _heightmap[x, z + 1] - _heightmap[x, z - 1];

        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private float CalculateVillageScore(int x, int z)
    {
        float flatness = 1f - CalculateHeightVariance(x, z, 24) * 10f;
        float waterProximity = IsNearWater(x, z, 48) ? 1f : 0f;
        float heightScore = 1f - Mathf.Abs(_heightmap[x, z] - 0.35f) * 2f;

        return flatness * 0.4f + waterProximity * 0.3f + heightScore * 0.3f;
    }

    #endregion

    #region Terrain Application

    private IEnumerator ApplyTerrainCoroutine()
    {
        if (_terrain == null)
        {
            Debug.LogWarning("[ProceduralWorldGenerator] No terrain assigned!");
            yield break;
        }

        TerrainData terrainData = _terrain.terrainData;

        // Set terrain size
        terrainData.heightmapResolution = _worldSize + 1;
        terrainData.size = new Vector3(_worldSize * _worldScale, _terrainHeight, _worldSize * _worldScale);

        // Apply heightmap
        float[,] unityHeightmap = new float[_worldSize + 1, _worldSize + 1];

        for (int z = 0; z <= _worldSize; z++)
        {
            for (int x = 0; x <= _worldSize; x++)
            {
                int sx = Mathf.Min(x, _worldSize - 1);
                int sz = Mathf.Min(z, _worldSize - 1);
                unityHeightmap[z, x] = _heightmap[sx, sz];
            }
        }

        terrainData.SetHeights(0, 0, unityHeightmap);

        yield return null;

        // Apply terrain layers
        if (_terrainLayers != null && _terrainLayers.Length > 0)
        {
            terrainData.terrainLayers = _terrainLayers;
            yield return StartCoroutine(PaintTerrainCoroutine(terrainData));
        }

        Debug.Log("[ProceduralWorldGenerator] Terrain applied");
    }

    private IEnumerator PaintTerrainCoroutine(TerrainData terrainData)
    {
        int alphamapRes = terrainData.alphamapResolution;
        float[,,] splatmapData = new float[alphamapRes, alphamapRes, _terrainLayers.Length];

        for (int z = 0; z < alphamapRes; z++)
        {
            for (int x = 0; x < alphamapRes; x++)
            {
                // Map alphamap to heightmap coords
                int hx = Mathf.FloorToInt((float)x / alphamapRes * _worldSize);
                int hz = Mathf.FloorToInt((float)z / alphamapRes * _worldSize);

                hx = Mathf.Clamp(hx, 0, _worldSize - 1);
                hz = Mathf.Clamp(hz, 0, _worldSize - 1);

                float height = _heightmap[hx, hz];
                float slope = CalculateSlope(hx, hz);
                float moisture = _moistureMap != null ? _moistureMap[hx, hz] : 0.5f;

                // Determine texture weights based on terrain features
                float[] weights = CalculateTextureWeights(height, slope, moisture);

                for (int i = 0; i < _terrainLayers.Length; i++)
                {
                    splatmapData[z, x, i] = weights[i];
                }
            }

            if (z % 32 == 0)
            {
                yield return null;
            }
        }

        terrainData.SetAlphamaps(0, 0, splatmapData);
    }

    private float[] CalculateTextureWeights(float height, float slope, float moisture)
    {
        float[] weights = new float[_terrainLayers.Length];

        if (_terrainLayers.Length == 0) return weights;

        // Default weight distribution based on terrain analysis
        // Assuming order: Grass, Rock, Sand, Snow, Dirt
        float grassWeight = 0f;
        float rockWeight = 0f;
        float sandWeight = 0f;
        float snowWeight = 0f;
        float dirtWeight = 0f;

        // Under water = sand
        if (height < _waterLevel)
        {
            sandWeight = 1f;
        }
        // Beach zone
        else if (height < _waterLevel + 0.05f)
        {
            sandWeight = 1f - ((height - _waterLevel) / 0.05f);
            grassWeight = 1f - sandWeight;
        }
        // Low altitude with low slope = grass
        else if (height < 0.5f && slope < 0.3f)
        {
            grassWeight = 1f - slope * 2f;
            dirtWeight = slope * 2f;
        }
        // Medium altitude or steep = rock/dirt
        else if (height < 0.75f)
        {
            float slopeFactor = Mathf.Clamp01(slope * 2f);
            rockWeight = slopeFactor;
            dirtWeight = (1f - slopeFactor) * (1f - moisture);
            grassWeight = (1f - slopeFactor) * moisture;
        }
        // High altitude = snow/rock
        else
        {
            float snowLine = (height - 0.75f) / 0.25f;
            snowWeight = snowLine;
            rockWeight = 1f - snowLine;
        }

        // Assign to array based on available layers
        if (_terrainLayers.Length >= 1) weights[0] = grassWeight;
        if (_terrainLayers.Length >= 2) weights[1] = rockWeight;
        if (_terrainLayers.Length >= 3) weights[2] = sandWeight;
        if (_terrainLayers.Length >= 4) weights[3] = snowWeight;
        if (_terrainLayers.Length >= 5) weights[4] = dirtWeight;

        // Normalize weights
        float total = 0f;
        for (int i = 0; i < weights.Length; i++) total += weights[i];

        if (total > 0)
        {
            for (int i = 0; i < weights.Length; i++) weights[i] /= total;
        }
        else if (weights.Length > 0)
        {
            weights[0] = 1f;
        }

        return weights;
    }

    #endregion

    #region Water Generation

    private IEnumerator GenerateWaterCoroutine()
    {
        if (_waterMaterial == null)
        {
            Debug.LogWarning("[ProceduralWorldGenerator] No water material assigned!");
            yield break;
        }

        // Create water plane at water level
        GameObject waterObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
        waterObject.name = "Water";
        waterObject.transform.SetParent(transform);

        float waterHeight = _waterLevel * _terrainHeight;
        float waterSize = _worldSize * _worldScale;

        waterObject.transform.position = new Vector3(waterSize / 2f, waterHeight, waterSize / 2f);
        waterObject.transform.localScale = new Vector3(waterSize / 10f, 1f, waterSize / 10f);

        // Apply water material
        var renderer = waterObject.GetComponent<Renderer>();
        renderer.material = _waterMaterial;

        // Remove collider (we'll use custom water detection)
        var collider = waterObject.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        // Add water component for interactions
        var waterSystem = waterObject.AddComponent<WaterSystem>();
        waterSystem.Initialize(_waterLevel * _terrainHeight, waterSize);

        Debug.Log("[ProceduralWorldGenerator] Water generated");
        yield return null;
    }

    #endregion

    #region Cave Generation

    private IEnumerator GenerateCavesCoroutine()
    {
        foreach (var cavePos in _caveEntrances)
        {
            GenerateCaveAtPosition(cavePos);
            yield return new WaitForSeconds(_generateDelay);
        }

        Debug.Log($"[ProceduralWorldGenerator] {_caveEntrances.Count} caves generated");
    }

    private void GenerateCaveAtPosition(Vector3 position)
    {
        // Create cave entrance marker
        GameObject caveEntrance = new GameObject($"CaveEntrance_{_caveEntrances.IndexOf(position)}");
        caveEntrance.transform.SetParent(transform);
        caveEntrance.transform.position = position;

        // Add cave marker component
        var marker = caveEntrance.AddComponent<CaveEntranceMarker>();
        marker.Initialize(position, _rng.Next());

        // TODO: When we have cave prefabs, instantiate them here
    }

    #endregion

    #region Village Generation

    private IEnumerator GenerateVillagesCoroutine()
    {
        int villageIndex = 0;

        foreach (var villagePos in _villagePositions)
        {
            yield return StartCoroutine(GenerateVillageAtPosition(villagePos, villageIndex));
            villageIndex++;
        }

        Debug.Log($"[ProceduralWorldGenerator] {_villagePositions.Count} villages generated");
    }

    private IEnumerator GenerateVillageAtPosition(Vector3 centerPosition, int villageIndex)
    {
        GameObject villageRoot = new GameObject($"Village_{villageIndex}");
        villageRoot.transform.SetParent(transform);
        villageRoot.transform.position = centerPosition;

        // Add village manager component
        var villageManager = villageRoot.AddComponent<VillageGenerator>();
        villageManager.Initialize(centerPosition, _rng.Next(), _terrain);

        yield return null;
    }

    #endregion

    #region Vegetation Generation

    private IEnumerator GenerateVegetationCoroutine()
    {
        if (BiomeManager.Instance == null)
        {
            Debug.LogWarning("[ProceduralWorldGenerator] BiomeManager not found!");
            yield break;
        }

        // Create vegetation parent
        GameObject vegetationRoot = new GameObject("Vegetation");
        vegetationRoot.transform.SetParent(transform);

        // Add vegetation spawner
        var spawner = vegetationRoot.AddComponent<VegetationSpawner>();
        spawner.Initialize(_worldSize, _worldScale, _heightmap, _terrainHeight, _waterLevel);

        yield return StartCoroutine(spawner.SpawnVegetationCoroutine(_vegetationBatchSize));

        Debug.Log("[ProceduralWorldGenerator] Vegetation generated");
    }

    #endregion

    #region Cloud Generation

    private void GenerateClouds()
    {
        if (_cloudMaterial == null)
        {
            Debug.LogWarning("[ProceduralWorldGenerator] No cloud material assigned!");
            return;
        }

        GameObject cloudRoot = new GameObject("Clouds");
        cloudRoot.transform.SetParent(transform);

        var cloudSystem = cloudRoot.AddComponent<CloudSystem>();
        cloudSystem.Initialize(
            _worldSize * _worldScale,
            _terrainHeight * 1.5f,
            _cloudMaterial
        );

        Debug.Log("[ProceduralWorldGenerator] Clouds generated");
    }

    #endregion

    #region Helper Methods

    private Vector2Int WorldToChunkCoord(Vector3 worldPosition)
    {
        int chunkX = Mathf.FloorToInt(worldPosition.x / (_chunkSize * _worldScale));
        int chunkZ = Mathf.FloorToInt(worldPosition.z / (_chunkSize * _worldScale));
        return new Vector2Int(chunkX, chunkZ);
    }

    #endregion
}

#region Supporting Classes

/// <summary>
/// Parametres de bruit pour le terrain.
/// </summary>
[Serializable]
public class TerrainNoiseSettings
{
    [Tooltip("Echelle du bruit")]
    public float scale = 50f;

    [Tooltip("Nombre d'octaves de bruit")]
    [Range(1, 8)]
    public int octaves = 6;

    [Tooltip("Persistance (amplitude par octave)")]
    [Range(0f, 1f)]
    public float persistence = 0.5f;

    [Tooltip("Lacunarite (frequence par octave)")]
    [Range(1f, 4f)]
    public float lacunarity = 2f;

    [Tooltip("Redistribution de hauteur")]
    [Range(0.5f, 3f)]
    public float redistribution = 1.2f;

    [Tooltip("Ajouter des cretes de montagne")]
    public bool addRidges = true;

    [Tooltip("Poids des cretes")]
    [Range(0f, 1f)]
    public float ridgeWeight = 0.3f;

    [Tooltip("Echelle des cretes")]
    public float ridgeScale = 3f;

    [Tooltip("Iterations d'erosion")]
    [Range(0, 10)]
    public int erosionIterations = 3;
}

/// <summary>
/// Chunk du monde genere.
/// </summary>
public class WorldChunk
{
    public Vector2Int Coordinate { get; set; }
    public Bounds Bounds { get; set; }
    public bool IsLoaded { get; set; }
    public List<GameObject> SpawnedObjects { get; set; }

    public WorldChunk()
    {
        SpawnedObjects = new List<GameObject>();
    }
}

#endregion
