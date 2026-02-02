using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Generateur de monde procedural complet.
/// Algorithmes professionnels inspires de Sebastian Lague, Nick McDonald et des studios AAA.
/// Inclut: fBM multi-couches, erosion hydraulique/thermique, rivieres procedurales,
/// domain warping, biomes Whittaker, et bruit de Voronoi.
/// </summary>
public class ProceduralWorldGenerator : MonoBehaviour
{
    #region Singleton

    public static ProceduralWorldGenerator Instance { get; private set; }

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // CRITICAL: Initialize collections in Awake, NOT Start
        // This ensures they're ready before any external code calls GenerateWorld()
        _chunks = new Dictionary<Vector2Int, WorldChunk>();
        _lakePositions = new List<Vector3>();
        _villagePositions = new List<Vector3>();
        _caveEntrances = new List<Vector3>();
        _mountainPeaks = new List<Vector3>();
        _riverPaths = new List<List<Vector2Int>>();
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
    [SerializeField] private TerrainNoiseSettings _terrainNoise = new TerrainNoiseSettings();

    [Header("Feature Settings")]
    [SerializeField] private bool _generateWater = true;
    [SerializeField] private bool _generateRivers = true;
    [SerializeField] private bool _generateVillages = true;
    [SerializeField] private bool _generateCaves = true;
    [SerializeField] private bool _generateVegetation = true;
    [SerializeField] private bool _generateClouds = true;

    [Header("Advanced Terrain")]
    [SerializeField] private bool _useDomainWarping = true;
    [SerializeField] private float _domainWarpStrength = 0.15f;
    [SerializeField] private bool _useVoronoiNoise = true;
    [SerializeField] private float _voronoiWeight = 0.1f;
    [SerializeField] private int _voronoiCellCount = 64;

    [Header("River Settings")]
    [SerializeField] private int _riverParticleCount = 5000;
    [SerializeField] private float _riverDepth = 0.02f;
    [SerializeField] private float _riverWidth = 3f;

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

    // River system
    private float[,] _riverMap;
    private float[,] _streamMap;
    private List<List<Vector2Int>> _riverPaths;

    // Voronoi cells for terrain features
    private Vector2[] _voronoiPoints;

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

    private void OnDestroy()
    {
        // Cleanup singleton reference
        if (Instance == this)
        {
            Instance = null;
        }
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
        Debug.Log($"[ProceduralWorldGenerator] Features: DomainWarp={_useDomainWarping}, Voronoi={_useVoronoiNoise}, Rivers={_generateRivers}");

        // Initialize Voronoi points for terrain features
        if (_useVoronoiNoise)
        {
            InitializeVoronoiPoints();
        }

        // Auto-find terrain if not assigned
        if (_terrain == null)
        {
            _terrain = FindFirstObjectByType<Terrain>();
            if (_terrain == null)
            {
                // Create terrain dynamically
                TerrainData terrainData = new TerrainData();
                terrainData.heightmapResolution = _worldSize + 1;
                terrainData.size = new Vector3(_worldSize * _worldScale, _terrainHeight, _worldSize * _worldScale);

                GameObject terrainObj = Terrain.CreateTerrainGameObject(terrainData);
                terrainObj.name = "ProceduralTerrain";
                terrainObj.transform.SetParent(transform.parent);
                _terrain = terrainObj.GetComponent<Terrain>();

                Debug.Log("[ProceduralWorldGenerator] Created terrain dynamically");
            }
            else
            {
                Debug.Log("[ProceduralWorldGenerator] Found existing terrain");
            }
        }

        // Create default water material if not assigned
        if (_waterMaterial == null)
        {
            _waterMaterial = CreateDefaultWaterMaterial();
            Debug.Log("[ProceduralWorldGenerator] Created default water material");
        }

        // Create default cloud material if not assigned
        if (_cloudMaterial == null)
        {
            _cloudMaterial = CreateDefaultCloudMaterial();
            Debug.Log("[ProceduralWorldGenerator] Created default cloud material");
        }

        // Step 1: Generate heightmap (20%)
        OnGenerationProgress?.Invoke(0.05f);
        yield return StartCoroutine(GenerateHeightmapCoroutine());
        OnGenerationProgress?.Invoke(0.2f);

        // Step 2: Generate climate maps (10%)
        GenerateClimateMaps();
        OnGenerationProgress?.Invoke(0.3f);
        yield return new WaitForSeconds(_generateDelay);

        // Step 3: Generate river system (10%)
        if (_generateRivers)
        {
            GenerateRiverSystem();
        }
        OnGenerationProgress?.Invoke(0.4f);
        yield return new WaitForSeconds(_generateDelay);

        // Step 4: Identify features (5%)
        IdentifyWorldFeatures();
        OnGenerationProgress?.Invoke(0.45f);
        yield return new WaitForSeconds(_generateDelay);

        // Step 5: Apply terrain (15%)
        yield return StartCoroutine(ApplyTerrainCoroutine());
        OnGenerationProgress?.Invoke(0.6f);

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
        // Coordonnées normalisées [0, 1]
        float nx = (float)x / _worldSize;
        float nz = (float)z / _worldSize;

        // Offset basé sur le seed (constant pour tout le terrain)
        float seedOffsetX = (_worldSeed % 10000) * 0.01f;
        float seedOffsetZ = ((_worldSeed * 31) % 10000) * 0.01f;

        // === DOMAIN WARPING (technique AAA pour formes organiques) ===
        // Déforme les coordonnées avec du bruit pour des formes plus naturelles
        float warpedNx = nx;
        float warpedNz = nz;

        if (_useDomainWarping)
        {
            // Premier niveau de déformation
            float warpX1 = GenerateFBM(nx + seedOffsetX, nz + seedOffsetZ, 4, 0.5f, 2f, _terrainNoise.scale * 0.5f);
            float warpZ1 = GenerateFBM(nx + seedOffsetX + 5.2f, nz + seedOffsetZ + 1.3f, 4, 0.5f, 2f, _terrainNoise.scale * 0.5f);

            // Deuxième niveau de déformation (domain warping imbriqué)
            float warpX2 = GenerateFBM(nx + warpX1 * _domainWarpStrength + seedOffsetX,
                                       nz + warpZ1 * _domainWarpStrength + seedOffsetZ,
                                       3, 0.5f, 2f, _terrainNoise.scale * 0.3f);
            float warpZ2 = GenerateFBM(nx + warpX1 * _domainWarpStrength + seedOffsetX + 3.7f,
                                       nz + warpZ1 * _domainWarpStrength + seedOffsetZ + 8.3f,
                                       3, 0.5f, 2f, _terrainNoise.scale * 0.3f);

            warpedNx = nx + (warpX1 + warpX2 * 0.5f) * _domainWarpStrength;
            warpedNz = nz + (warpZ1 + warpZ2 * 0.5f) * _domainWarpStrength;
        }

        // === COUCHE 1: Terrain continental (très basse fréquence) ===
        // Définit les grandes masses terrestres vs océan
        float continentNoise = GenerateFBM(warpedNx * 0.3f + seedOffsetX, warpedNz * 0.3f + seedOffsetZ,
                                           3, 0.6f, 2f, _terrainNoise.scale * 0.2f);
        continentNoise = Mathf.Pow(continentNoise, 1.5f); // Accentuer les continents

        // === COUCHE 2: Terrain de base avec fBM ===
        float baseHeight = GenerateFBM(warpedNx + seedOffsetX, warpedNz + seedOffsetZ,
                                        _terrainNoise.octaves,
                                        _terrainNoise.persistence,
                                        _terrainNoise.lacunarity,
                                        _terrainNoise.scale);

        // === COUCHE 3: Formations de montagnes (fréquence moyenne) ===
        float mountainNoise = GenerateFBM(warpedNx * 1.5f + seedOffsetX + 100f,
                                          warpedNz * 1.5f + seedOffsetZ + 100f,
                                          5, 0.55f, 2.2f, _terrainNoise.scale * 0.8f);

        // === COUCHE 4: Détails locaux (haute fréquence) ===
        float detailNoise = GenerateFBM(warpedNx * 3f + seedOffsetX, warpedNz * 3f + seedOffsetZ,
                                        4, 0.4f, 2.5f, _terrainNoise.scale * 2.5f);

        // === COUCHE 5: Micro-détails (très haute fréquence) ===
        float microDetail = GenerateFBM(warpedNx * 8f + seedOffsetX, warpedNz * 8f + seedOffsetZ,
                                        3, 0.3f, 2f, _terrainNoise.scale * 5f);

        // === VORONOI NOISE pour les plateaux et cratères ===
        float voronoiValue = 0f;
        if (_useVoronoiNoise && _voronoiPoints != null && _voronoiPoints.Length > 0)
        {
            voronoiValue = GenerateVoronoiNoise(warpedNx, warpedNz);
        }

        // === COMBINAISON DES COUCHES ===
        // Pondération intelligente basée sur l'altitude
        float height = continentNoise * 0.25f + baseHeight * 0.35f + mountainNoise * 0.25f +
                       detailNoise * 0.1f + microDetail * 0.05f;

        // Ajouter Voronoi pour des plateaux rocheux
        if (_useVoronoiNoise)
        {
            float voronoiMask = Mathf.Clamp01((height - 0.4f) * 3f); // Seulement sur terrain élevé
            height = Mathf.Lerp(height, height + voronoiValue * 0.15f, voronoiMask * _voronoiWeight);
        }

        // === CRÊTES DE MONTAGNES (Ridge Noise) ===
        if (_terrainNoise.addRidges)
        {
            float ridgeNoise = GenerateRidgeNoise(warpedNx + seedOffsetX, warpedNz + seedOffsetZ);
            // Les crêtes n'apparaissent que sur les zones hautes (montagnes)
            float ridgeMask = Mathf.Clamp01((height - 0.55f) * 2.5f);
            height = Mathf.Lerp(height, height + ridgeNoise * 0.25f, ridgeMask * _terrainNoise.ridgeWeight);
        }

        // === TERRASSES NATURELLES (effet escalier subtil) ===
        float terraceStrength = 0.03f;
        float terraceCount = 12f;
        float terraced = Mathf.Round(height * terraceCount) / terraceCount;
        height = Mathf.Lerp(height, terraced, terraceStrength * Mathf.Clamp01((height - 0.5f) * 2f));

        // === REDISTRIBUTION pour contraste vallées/pics ===
        height = Mathf.Pow(Mathf.Clamp01(height), _terrainNoise.redistribution);

        // === ZONE DE SPAWN SÉCURISÉE (centre du monde) ===
        float distFromCenter = Vector2.Distance(new Vector2(nx, nz), new Vector2(0.5f, 0.5f));
        float safeZoneRadius = 0.15f; // 15% du monde
        float transitionZone = 0.1f;

        if (distFromCenter < safeZoneRadius)
        {
            // Zone parfaitement plate et sûre
            height = Mathf.Lerp(0.38f, height, distFromCenter / safeZoneRadius * 0.3f);
        }
        else if (distFromCenter < safeZoneRadius + transitionZone)
        {
            // Transition douce vers le terrain normal
            float t = (distFromCenter - safeZoneRadius) / transitionZone;
            t = t * t * (3f - 2f * t); // Smoothstep
            height = Mathf.Lerp(0.38f, height, t);
        }

        // === FALLOFF aux bords (éviter les murs verticaux) ===
        float edgeFalloff = CalculateEdgeFalloff(nx, nz, 0.1f);
        height *= edgeFalloff;

        return Mathf.Clamp01(height);
    }

    /// <summary>
    /// Calcule un falloff doux aux bords de la carte pour éviter les coupures brutales
    /// </summary>
    private float CalculateEdgeFalloff(float nx, float nz, float falloffWidth)
    {
        float distToEdgeX = Mathf.Min(nx, 1f - nx);
        float distToEdgeZ = Mathf.Min(nz, 1f - nz);
        float distToEdge = Mathf.Min(distToEdgeX, distToEdgeZ);

        if (distToEdge > falloffWidth) return 1f;

        float t = distToEdge / falloffWidth;
        return t * t * (3f - 2f * t); // Smoothstep
    }

    /// <summary>
    /// Génère du bruit Voronoi pour créer des plateaux, cellules naturelles
    /// </summary>
    private float GenerateVoronoiNoise(float nx, float nz)
    {
        float minDist = float.MaxValue;
        float secondMinDist = float.MaxValue;

        foreach (var point in _voronoiPoints)
        {
            float dist = Vector2.Distance(new Vector2(nx, nz), point);

            if (dist < minDist)
            {
                secondMinDist = minDist;
                minDist = dist;
            }
            else if (dist < secondMinDist)
            {
                secondMinDist = dist;
            }
        }

        // F2 - F1 crée des crêtes entre les cellules (plateaux)
        float voronoi = secondMinDist - minDist;
        return Mathf.Clamp01(voronoi * 5f); // Normaliser
    }

    /// <summary>
    /// Initialise les points Voronoi pour le terrain
    /// </summary>
    private void InitializeVoronoiPoints()
    {
        _voronoiPoints = new Vector2[_voronoiCellCount];

        for (int i = 0; i < _voronoiCellCount; i++)
        {
            _voronoiPoints[i] = new Vector2(
                (float)_rng.NextDouble(),
                (float)_rng.NextDouble()
            );
        }

        Debug.Log($"[ProceduralWorldGenerator] Initialized {_voronoiCellCount} Voronoi cells");
    }

    /// <summary>
    /// Fractal Brownian Motion - génère du bruit cohérent multi-octaves
    /// </summary>
    private float GenerateFBM(float x, float z, int octaves, float persistence, float lacunarity, float scale)
    {
        float total = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxValue = 0f;

        for (int i = 0; i < octaves; i++)
        {
            float sampleX = x * scale * frequency;
            float sampleZ = z * scale * frequency;

            // Perlin Noise retourne [0, 1], on le centre sur [-0.5, 0.5] puis [0, 1]
            float perlinValue = Mathf.PerlinNoise(sampleX, sampleZ);
            total += perlinValue * amplitude;

            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return total / maxValue;
    }

    private float GenerateRidgeNoise(float nx, float nz)
    {
        float ridgeValue = 0f;
        float amplitude = 1f;
        float frequency = _terrainNoise.ridgeScale;
        float maxValue = 0f;

        for (int i = 0; i < 4; i++)
        {
            float sampleX = nx * frequency * _terrainNoise.scale;
            float sampleZ = nz * frequency * _terrainNoise.scale;

            // Ridge noise: 1 - |noise * 2 - 1| crée des crêtes
            float noise = Mathf.PerlinNoise(sampleX, sampleZ);
            noise = 1f - Mathf.Abs(noise * 2f - 1f);
            noise = noise * noise; // Accentuer les pics

            ridgeValue += noise * amplitude;
            maxValue += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return ridgeValue / maxValue;
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
        // Érosion hydraulique réaliste (basée sur l'algorithme de Sebastian Lague)
        // Simule des gouttes d'eau qui creusent le terrain

        int dropletCount = _terrainNoise.erosionIterations * 1000; // Nombre de gouttes

        // Paramètres d'érosion
        float inertia = 0.05f;           // Résistance au changement de direction
        float sedimentCapacityFactor = 4f; // Capacité de transport de sédiments
        float minSedimentCapacity = 0.01f;
        float erodeSpeed = 0.3f;         // Vitesse d'érosion
        float depositSpeed = 0.3f;       // Vitesse de dépôt
        float evaporateSpeed = 0.01f;    // Évaporation
        float gravity = 4f;
        int maxDropletLifetime = 30;     // Durée de vie max d'une goutte
        int erosionRadius = 3;           // Rayon d'érosion

        // Pré-calculer les poids d'érosion pour le rayon
        float[,] erosionWeights = new float[erosionRadius * 2 + 1, erosionRadius * 2 + 1];
        float weightSum = 0f;
        for (int y = -erosionRadius; y <= erosionRadius; y++)
        {
            for (int x = -erosionRadius; x <= erosionRadius; x++)
            {
                float dist = Mathf.Sqrt(x * x + y * y);
                if (dist <= erosionRadius)
                {
                    float weight = Mathf.Max(0, erosionRadius - dist);
                    erosionWeights[x + erosionRadius, y + erosionRadius] = weight;
                    weightSum += weight;
                }
            }
        }
        // Normaliser les poids
        for (int y = 0; y < erosionRadius * 2 + 1; y++)
        {
            for (int x = 0; x < erosionRadius * 2 + 1; x++)
            {
                erosionWeights[x, y] /= weightSum;
            }
        }

        // Simuler chaque goutte d'eau
        for (int i = 0; i < dropletCount; i++)
        {
            // Position initiale aléatoire
            float posX = _rng.Next(erosionRadius, _worldSize - erosionRadius);
            float posZ = _rng.Next(erosionRadius, _worldSize - erosionRadius);
            float dirX = 0f;
            float dirZ = 0f;
            float speed = 1f;
            float water = 1f;
            float sediment = 0f;

            for (int lifetime = 0; lifetime < maxDropletLifetime; lifetime++)
            {
                int nodeX = Mathf.FloorToInt(posX);
                int nodeZ = Mathf.FloorToInt(posZ);

                // Vérifier les limites
                if (nodeX < erosionRadius || nodeX >= _worldSize - erosionRadius ||
                    nodeZ < erosionRadius || nodeZ >= _worldSize - erosionRadius)
                    break;

                // Calculer le gradient (direction de la pente)
                float heightNW = _heightmap[nodeX, nodeZ];
                float heightNE = _heightmap[Mathf.Min(nodeX + 1, _worldSize - 1), nodeZ];
                float heightSW = _heightmap[nodeX, Mathf.Min(nodeZ + 1, _worldSize - 1)];
                float heightSE = _heightmap[Mathf.Min(nodeX + 1, _worldSize - 1), Mathf.Min(nodeZ + 1, _worldSize - 1)];

                // Interpolation bilinéaire pour la hauteur actuelle
                float u = posX - nodeX;
                float v = posZ - nodeZ;
                float currentHeight = heightNW * (1 - u) * (1 - v) + heightNE * u * (1 - v) +
                                     heightSW * (1 - u) * v + heightSE * u * v;

                // Calculer le gradient
                float gradientX = (heightNE - heightNW) * (1 - v) + (heightSE - heightSW) * v;
                float gradientZ = (heightSW - heightNW) * (1 - u) + (heightSE - heightNE) * u;

                // Mettre à jour la direction avec inertie
                dirX = dirX * inertia - gradientX * (1 - inertia);
                dirZ = dirZ * inertia - gradientZ * (1 - inertia);

                // Normaliser la direction
                float dirLength = Mathf.Sqrt(dirX * dirX + dirZ * dirZ);
                if (dirLength < 0.0001f)
                {
                    // Direction aléatoire si sur terrain plat
                    float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
                    dirX = Mathf.Cos(angle);
                    dirZ = Mathf.Sin(angle);
                }
                else
                {
                    dirX /= dirLength;
                    dirZ /= dirLength;
                }

                // Nouvelle position
                float newPosX = posX + dirX;
                float newPosZ = posZ + dirZ;

                // Vérifier les limites
                if (newPosX < erosionRadius || newPosX >= _worldSize - erosionRadius ||
                    newPosZ < erosionRadius || newPosZ >= _worldSize - erosionRadius)
                    break;

                // Hauteur à la nouvelle position
                int newNodeX = Mathf.FloorToInt(newPosX);
                int newNodeZ = Mathf.FloorToInt(newPosZ);
                float newU = newPosX - newNodeX;
                float newV = newPosZ - newNodeZ;

                float newHeightNW = _heightmap[newNodeX, newNodeZ];
                float newHeightNE = _heightmap[Mathf.Min(newNodeX + 1, _worldSize - 1), newNodeZ];
                float newHeightSW = _heightmap[newNodeX, Mathf.Min(newNodeZ + 1, _worldSize - 1)];
                float newHeightSE = _heightmap[Mathf.Min(newNodeX + 1, _worldSize - 1), Mathf.Min(newNodeZ + 1, _worldSize - 1)];

                float newHeight = newHeightNW * (1 - newU) * (1 - newV) + newHeightNE * newU * (1 - newV) +
                                 newHeightSW * (1 - newU) * newV + newHeightSE * newU * newV;

                float deltaHeight = newHeight - currentHeight;

                // Capacité de sédiment basée sur la vitesse et la pente
                float sedimentCapacity = Mathf.Max(-deltaHeight * speed * water * sedimentCapacityFactor, minSedimentCapacity);

                // Déposer ou éroder
                if (sediment > sedimentCapacity || deltaHeight > 0)
                {
                    // Déposer des sédiments
                    float amountToDeposit = (deltaHeight > 0) ?
                        Mathf.Min(deltaHeight, sediment) :
                        (sediment - sedimentCapacity) * depositSpeed;

                    sediment -= amountToDeposit;

                    // Déposer sur les 4 coins du carré actuel
                    _heightmap[nodeX, nodeZ] += amountToDeposit * (1 - u) * (1 - v);
                    _heightmap[Mathf.Min(nodeX + 1, _worldSize - 1), nodeZ] += amountToDeposit * u * (1 - v);
                    _heightmap[nodeX, Mathf.Min(nodeZ + 1, _worldSize - 1)] += amountToDeposit * (1 - u) * v;
                    _heightmap[Mathf.Min(nodeX + 1, _worldSize - 1), Mathf.Min(nodeZ + 1, _worldSize - 1)] += amountToDeposit * u * v;
                }
                else
                {
                    // Éroder le terrain
                    float amountToErode = Mathf.Min((sedimentCapacity - sediment) * erodeSpeed, -deltaHeight);

                    // Éroder dans un rayon
                    for (int ez = -erosionRadius; ez <= erosionRadius; ez++)
                    {
                        for (int ex = -erosionRadius; ex <= erosionRadius; ex++)
                        {
                            int erodeX = nodeX + ex;
                            int erodeZ = nodeZ + ez;

                            if (erodeX >= 0 && erodeX < _worldSize && erodeZ >= 0 && erodeZ < _worldSize)
                            {
                                float weight = erosionWeights[ex + erosionRadius, ez + erosionRadius];
                                float erodeAmount = amountToErode * weight;
                                _heightmap[erodeX, erodeZ] = Mathf.Max(0, _heightmap[erodeX, erodeZ] - erodeAmount);
                            }
                        }
                    }

                    sediment += amountToErode;
                }

                // Mettre à jour vitesse et eau
                speed = Mathf.Sqrt(Mathf.Max(0, speed * speed + deltaHeight * gravity));
                water *= (1 - evaporateSpeed);

                // Mettre à jour la position
                posX = newPosX;
                posZ = newPosZ;

                // Arrêter si plus d'eau
                if (water < 0.01f) break;
            }
        }

        // Appliquer aussi une légère érosion thermique pour lisser
        ApplyThermalErosion(3);

        Debug.Log($"[ProceduralWorldGenerator] Hydraulic erosion applied with {dropletCount} droplets");
    }

    /// <summary>
    /// Érosion thermique améliorée avec angle de talus variable selon le matériau
    /// </summary>
    private void ApplyThermalErosion(int iterations)
    {
        // Angles de talus variables selon l'altitude (roche vs terre)
        float baseTalusAngle = 0.03f;
        float rockTalusAngle = 0.06f; // La roche supporte des pentes plus raides

        for (int iter = 0; iter < iterations; iter++)
        {
            for (int z = 1; z < _worldSize - 1; z++)
            {
                for (int x = 1; x < _worldSize - 1; x++)
                {
                    float h = _heightmap[x, z];

                    // Angle de talus variable selon altitude
                    float talusAngle = Mathf.Lerp(baseTalusAngle, rockTalusAngle, Mathf.Clamp01((h - 0.5f) * 2f));

                    // Trouver le voisin le plus bas avec sa direction
                    float minNeighbor = h;
                    int minDx = 0, minDz = 0;

                    int[,] neighbors = { { -1, 0 }, { 1, 0 }, { 0, -1 }, { 0, 1 }, { -1, -1 }, { 1, -1 }, { -1, 1 }, { 1, 1 } };

                    for (int i = 0; i < 8; i++)
                    {
                        int nx = x + neighbors[i, 0];
                        int nz = z + neighbors[i, 1];

                        if (nx >= 0 && nx < _worldSize && nz >= 0 && nz < _worldSize)
                        {
                            float nh = _heightmap[nx, nz];
                            // Diagonales comptent moins (distance sqrt(2))
                            float distance = (Mathf.Abs(neighbors[i, 0]) + Mathf.Abs(neighbors[i, 1])) > 1 ? 1.414f : 1f;

                            if (nh < minNeighbor)
                            {
                                minNeighbor = nh;
                                minDx = neighbors[i, 0];
                                minDz = neighbors[i, 1];
                            }
                        }
                    }

                    float diff = h - minNeighbor;
                    if (diff > talusAngle && (minDx != 0 || minDz != 0))
                    {
                        float transfer = diff * 0.3f;
                        _heightmap[x, z] -= transfer;
                        _heightmap[x + minDx, z + minDz] += transfer * 0.8f; // Perte de matériau
                    }
                }
            }
        }
    }

    #endregion

    #region River Generation

    /// <summary>
    /// Génère des rivières procédurales en utilisant l'algorithme de descente de particules
    /// Inspiré de Nick McDonald's SimpleHydrology
    /// </summary>
    private void GenerateRiverSystem()
    {
        if (!_generateRivers) return;

        _riverMap = new float[_worldSize, _worldSize];
        _streamMap = new float[_worldSize, _worldSize];
        _riverPaths.Clear();

        // Trouver les points de départ des rivières (sommets de montagnes)
        List<Vector2Int> riverSources = FindRiverSources();

        Debug.Log($"[ProceduralWorldGenerator] Found {riverSources.Count} potential river sources");

        // Simuler les particules d'eau pour tracer les rivières
        foreach (var source in riverSources)
        {
            SimulateRiverParticle(source.x, source.y);
        }

        // Simuler des particules aléatoires pour densifier le réseau
        for (int i = 0; i < _riverParticleCount; i++)
        {
            int startX = _rng.Next(10, _worldSize - 10);
            int startZ = _rng.Next(10, _worldSize - 10);

            // Commencer seulement sur terrain élevé
            if (_heightmap[startX, startZ] > 0.5f)
            {
                SimulateRiverParticle(startX, startZ);
            }
        }

        // Appliquer les rivières au heightmap
        ApplyRiversToTerrain();

        Debug.Log($"[ProceduralWorldGenerator] Generated {_riverPaths.Count} river paths");
    }

    /// <summary>
    /// Trouve les sources potentielles de rivières (pics de montagnes)
    /// </summary>
    private List<Vector2Int> FindRiverSources()
    {
        List<Vector2Int> sources = new List<Vector2Int>();
        int sampleStep = 32;

        for (int z = sampleStep; z < _worldSize - sampleStep; z += sampleStep)
        {
            for (int x = sampleStep; x < _worldSize - sampleStep; x += sampleStep)
            {
                if (_heightmap[x, z] > 0.65f) // Zones élevées
                {
                    // Vérifier si c'est un maximum local
                    bool isLocalMax = true;
                    for (int dz = -sampleStep / 2; dz <= sampleStep / 2 && isLocalMax; dz += 4)
                    {
                        for (int dx = -sampleStep / 2; dx <= sampleStep / 2 && isLocalMax; dx += 4)
                        {
                            if (dx == 0 && dz == 0) continue;
                            int nx = x + dx;
                            int nz = z + dz;
                            if (nx >= 0 && nx < _worldSize && nz >= 0 && nz < _worldSize)
                            {
                                if (_heightmap[nx, nz] > _heightmap[x, z])
                                {
                                    isLocalMax = false;
                                }
                            }
                        }
                    }

                    if (isLocalMax)
                    {
                        sources.Add(new Vector2Int(x, z));
                    }
                }
            }
        }

        return sources;
    }

    /// <summary>
    /// Simule une particule d'eau descendant le terrain pour tracer une rivière
    /// </summary>
    private void SimulateRiverParticle(int startX, int startZ)
    {
        List<Vector2Int> path = new List<Vector2Int>();

        float posX = startX;
        float posZ = startZ;
        float velX = 0f;
        float velZ = 0f;
        float volume = 1f;

        float friction = 0.1f;
        float evaporationRate = 0.002f;
        float inertia = 0.3f;
        int maxIterations = 500;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            int nodeX = Mathf.Clamp(Mathf.FloorToInt(posX), 1, _worldSize - 2);
            int nodeZ = Mathf.Clamp(Mathf.FloorToInt(posZ), 1, _worldSize - 2);

            // Ajouter au chemin
            path.Add(new Vector2Int(nodeX, nodeZ));

            // Marquer sur la stream map
            _streamMap[nodeX, nodeZ] += volume;

            // Calculer le gradient
            Vector2 gradient = CalculateTerrainGradient(nodeX, nodeZ);

            // Mise à jour de la vélocité avec inertie
            velX = velX * inertia - gradient.x * (1f - inertia);
            velZ = velZ * inertia - gradient.y * (1f - inertia);

            // Normaliser
            float velMag = Mathf.Sqrt(velX * velX + velZ * velZ);
            if (velMag > 0.001f)
            {
                velX /= velMag;
                velZ /= velMag;
            }
            else
            {
                // Direction aléatoire si terrain plat
                float angle = (float)_rng.NextDouble() * Mathf.PI * 2f;
                velX = Mathf.Cos(angle);
                velZ = Mathf.Sin(angle);
            }

            // Appliquer friction
            velX *= (1f - friction);
            velZ *= (1f - friction);

            // Nouvelle position
            posX += velX;
            posZ += velZ;

            // Évaporation
            volume *= (1f - evaporationRate);

            // Vérifier les conditions d'arrêt
            if (posX < 2 || posX >= _worldSize - 2 || posZ < 2 || posZ >= _worldSize - 2)
                break;

            if (volume < 0.01f)
                break;

            // Arrêter si on atteint l'eau
            if (_heightmap[nodeX, nodeZ] < _waterLevel + 0.02f)
                break;

            // Rejoindre une rivière existante (confluence)
            if (_streamMap[nodeX, nodeZ] > 2f && path.Count > 10)
                break;
        }

        if (path.Count > 20) // Rivière suffisamment longue
        {
            _riverPaths.Add(path);
        }
    }

    /// <summary>
    /// Calcule le gradient du terrain (direction de la pente descendante)
    /// </summary>
    private Vector2 CalculateTerrainGradient(int x, int z)
    {
        float left = _heightmap[Mathf.Max(0, x - 1), z];
        float right = _heightmap[Mathf.Min(_worldSize - 1, x + 1), z];
        float down = _heightmap[x, Mathf.Max(0, z - 1)];
        float up = _heightmap[x, Mathf.Min(_worldSize - 1, z + 1)];

        return new Vector2(left - right, down - up);
    }

    /// <summary>
    /// Applique les rivières au terrain en creusant des lits de rivière
    /// </summary>
    private void ApplyRiversToTerrain()
    {
        // Normaliser la stream map
        float maxStream = 0f;
        for (int z = 0; z < _worldSize; z++)
        {
            for (int x = 0; x < _worldSize; x++)
            {
                if (_streamMap[x, z] > maxStream)
                    maxStream = _streamMap[x, z];
            }
        }

        if (maxStream < 0.01f) return;

        // Appliquer l'érosion des rivières
        for (int z = 1; z < _worldSize - 1; z++)
        {
            for (int x = 1; x < _worldSize - 1; x++)
            {
                float streamValue = _streamMap[x, z] / maxStream;

                if (streamValue > 0.1f) // Seuil pour considérer comme rivière
                {
                    // Intensité de l'érosion basée sur le flux
                    float erosionIntensity = Mathf.Pow(streamValue, 0.5f);

                    // Creuser le lit de la rivière
                    float carveDepth = _riverDepth * erosionIntensity;
                    _heightmap[x, z] -= carveDepth;

                    // Élargir les bords de la rivière (berges douces)
                    int radius = Mathf.CeilToInt(_riverWidth * erosionIntensity);
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            if (dx == 0 && dz == 0) continue;

                            int nx = x + dx;
                            int nz = z + dz;

                            if (nx >= 0 && nx < _worldSize && nz >= 0 && nz < _worldSize)
                            {
                                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                                float falloff = 1f - (dist / (radius + 1f));
                                falloff = falloff * falloff; // Falloff quadratique

                                _heightmap[nx, nz] -= carveDepth * falloff * 0.3f;
                            }
                        }
                    }

                    // Marquer sur la river map
                    _riverMap[x, z] = erosionIntensity;
                }
            }
        }

        // Lisser les rivières
        SmoothRiverBeds();
    }

    /// <summary>
    /// Lisse les lits de rivière pour des transitions plus naturelles
    /// </summary>
    private void SmoothRiverBeds()
    {
        float[,] smoothed = new float[_worldSize, _worldSize];
        Array.Copy(_heightmap, smoothed, _heightmap.Length);

        for (int z = 1; z < _worldSize - 1; z++)
        {
            for (int x = 1; x < _worldSize - 1; x++)
            {
                if (_riverMap[x, z] > 0.1f)
                {
                    // Moyenne des voisins pour lisser
                    float sum = 0f;
                    int count = 0;

                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            sum += _heightmap[x + dx, z + dz];
                            count++;
                        }
                    }

                    smoothed[x, z] = Mathf.Lerp(_heightmap[x, z], sum / count, 0.5f);
                }
            }
        }

        Array.Copy(smoothed, _heightmap, _heightmap.Length);
    }

    #endregion

    #region Climate Maps

    private void GenerateClimateMaps()
    {
        _moistureMap = new float[_worldSize, _worldSize];
        _temperatureMap = new float[_worldSize, _worldSize];

        // Offsets déterministes basés sur le seed
        float moistureOffsetX = ((_worldSeed * 17) % 10000) * 0.01f;
        float moistureOffsetZ = ((_worldSeed * 23) % 10000) * 0.01f;
        float tempOffsetX = ((_worldSeed * 29) % 10000) * 0.01f;
        float tempOffsetZ = ((_worldSeed * 37) % 10000) * 0.01f;

        for (int z = 0; z < _worldSize; z++)
        {
            for (int x = 0; x < _worldSize; x++)
            {
                float nx = (float)x / _worldSize;
                float nz = (float)z / _worldSize;

                // === MOISTURE MAP ===
                // Multi-octave pour variation réaliste
                float baseMoisture = GenerateFBM(nx * 3f + moistureOffsetX, nz * 3f + moistureOffsetZ,
                                                  4, 0.5f, 2f, _terrainNoise.scale * 0.5f);

                // Les zones près de l'eau sont plus humides
                float waterProximityBonus = 0f;
                if (_heightmap[x, z] < _waterLevel + 0.1f)
                {
                    waterProximityBonus = 0.3f * (1f - (_heightmap[x, z] - _waterLevel) / 0.1f);
                }

                // Les rivières augmentent l'humidité
                float riverBonus = 0f;
                if (_riverMap != null && _riverMap[x, z] > 0)
                {
                    riverBonus = _riverMap[x, z] * 0.4f;
                }

                // Les zones d'ombre des montagnes (côté est) sont plus sèches
                float rainShadow = 0f;
                if (x > 10)
                {
                    float westHeight = _heightmap[x - 10, z];
                    if (westHeight > _heightmap[x, z] + 0.15f)
                    {
                        rainShadow = -0.2f; // Zone d'ombre pluviométrique
                    }
                }

                _moistureMap[x, z] = Mathf.Clamp01(baseMoisture + waterProximityBonus + riverBonus + rainShadow);

                // === TEMPERATURE MAP ===
                // Base: gradient latitude (équateur chaud, pôles froids)
                float latitude = Mathf.Abs(nz - 0.5f) * 2f; // 0 à l'équateur, 1 aux pôles
                float baseTemp = 1f - latitude * 0.7f;

                // Altitude: -6°C par 1000m (lapse rate)
                // Avec terrainHeight = 100, on simule des montagnes jusqu'à ~3000m
                float altitudeEffect = _heightmap[x, z] * 0.4f;

                // Variation locale avec bruit
                float noiseTemp = GenerateFBM(nx * 2f + tempOffsetX, nz * 2f + tempOffsetZ,
                                              3, 0.4f, 2f, _terrainNoise.scale * 0.3f) * 0.2f - 0.1f;

                // Océan modère les températures (effet maritime)
                float maritimeEffect = 0f;
                if (_heightmap[x, z] < _waterLevel + 0.05f)
                {
                    maritimeEffect = 0.1f; // Plus tempéré près de l'eau
                }

                _temperatureMap[x, z] = Mathf.Clamp01(baseTemp - altitudeEffect + noiseTemp + maritimeEffect);
            }
        }

        // Initialize biome manager with maps
        if (BiomeManager.Instance != null)
        {
            BiomeManager.Instance.InitializeBiomeMap(_worldSize, _worldSize, _heightmap);
        }

        Debug.Log("[ProceduralWorldGenerator] Climate maps generated with Whittaker-style biome logic");
    }

    /// <summary>
    /// Détermine le biome selon le diagramme de Whittaker (température/précipitations)
    /// Utilisé pour la texture et la végétation
    /// </summary>
    public BiomeType GetBiomeAt(int x, int z)
    {
        if (x < 0 || x >= _worldSize || z < 0 || z >= _worldSize)
            return BiomeType.Plains;

        float height = _heightmap[x, z];
        float moisture = _moistureMap != null ? _moistureMap[x, z] : 0.5f;
        float temperature = _temperatureMap != null ? _temperatureMap[x, z] : 0.5f;

        // Eau
        if (height < _waterLevel)
            return BiomeType.Ocean;

        // Plage
        if (height < _waterLevel + 0.03f)
            return BiomeType.Beach;

        // Haute altitude = montagnes
        if (height > 0.75f)
            return BiomeType.Mountains;

        // Altitude moyenne-haute
        if (height > 0.6f)
        {
            if (temperature < 0.25f)
                return BiomeType.Tundra;
            if (moisture < 0.3f)
                return BiomeType.Mountains;
            return BiomeType.Forest;
        }

        // === DIAGRAMME DE WHITTAKER pour basses altitudes ===
        // Froid
        if (temperature < 0.2f)
        {
            if (moisture < 0.3f)
                return BiomeType.Tundra;
            return BiomeType.Forest; // Forêt boréale
        }

        // Tempéré
        if (temperature < 0.5f)
        {
            if (moisture < 0.2f)
                return BiomeType.Desert;
            if (moisture < 0.5f)
                return BiomeType.Plains;
            return BiomeType.Forest;
        }

        // Chaud
        if (temperature < 0.75f)
        {
            if (moisture < 0.2f)
                return BiomeType.Desert;
            if (moisture < 0.4f)
                return BiomeType.Plains; // Savane
            if (moisture < 0.7f)
                return BiomeType.Forest;
            return BiomeType.Swamp; // Marécage tropical
        }

        // Très chaud (tropical)
        if (moisture < 0.25f)
            return BiomeType.Desert;
        if (moisture < 0.5f)
            return BiomeType.Plains;
        if (moisture < 0.75f)
            return BiomeType.Forest;
        return BiomeType.Swamp; // Forêt tropicale humide
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
        bool createdNewTerrainData = false;

        // Create terrain data if it doesn't exist
        if (terrainData == null)
        {
            terrainData = new TerrainData();
            terrainData.name = "ProceduralTerrainData_" + _worldSeed;
            createdNewTerrainData = true;
            Debug.Log("[ProceduralWorldGenerator] Creating new TerrainData...");
        }

        // CRITICAL: Set heightmapResolution BEFORE setting size (Unity requirement)
        // Resolution must be power of 2 + 1 (e.g., 513, 1025, 2049)
        int resolution = Mathf.ClosestPowerOfTwo(_worldSize) + 1;
        resolution = Mathf.Clamp(resolution, 33, 4097); // Unity limits
        terrainData.heightmapResolution = resolution;

        // Set terrain size - MUST be done after heightmapResolution
        float terrainWidth = _worldSize * _worldScale;
        float terrainLength = _worldSize * _worldScale;
        terrainData.size = new Vector3(terrainWidth, _terrainHeight, terrainLength);

        Debug.Log($"[ProceduralWorldGenerator] TerrainData configured: resolution={resolution}, size={terrainData.size}");

        // CRITICAL: Save the TerrainData as an asset in Editor mode
#if UNITY_EDITOR
        if (createdNewTerrainData && !Application.isPlaying)
        {
            // Ensure directory exists
            string folderPath = "Assets/GeneratedTerrain";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets", "GeneratedTerrain");
            }

            string assetPath = $"{folderPath}/TerrainData_{_worldSeed}.asset";

            // Delete existing asset if present
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            AssetDatabase.CreateAsset(terrainData, assetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ProceduralWorldGenerator] TerrainData saved to: {assetPath}");
        }
#endif

        // CRITICAL: Assign to terrain component AFTER saving
        _terrain.terrainData = terrainData;

        // Also update TerrainCollider
        var collider = _terrain.GetComponent<TerrainCollider>();
        if (collider != null)
        {
            collider.terrainData = terrainData;
        }

        // Assign URP Terrain material
        AssignURPTerrainMaterial();

        yield return null;

        // Prepare heightmap data for Unity (Unity uses [y,x] indexing!)
        int unityResolution = terrainData.heightmapResolution;
        float[,] unityHeightmap = new float[unityResolution, unityResolution];

        float scaleX = (float)(_worldSize - 1) / (unityResolution - 1);
        float scaleZ = (float)(_worldSize - 1) / (unityResolution - 1);

        for (int z = 0; z < unityResolution; z++)
        {
            for (int x = 0; x < unityResolution; x++)
            {
                // Sample from our heightmap with interpolation
                float srcX = x * scaleX;
                float srcZ = z * scaleZ;

                int x0 = Mathf.FloorToInt(srcX);
                int z0 = Mathf.FloorToInt(srcZ);
                int x1 = Mathf.Min(x0 + 1, _worldSize - 1);
                int z1 = Mathf.Min(z0 + 1, _worldSize - 1);
                x0 = Mathf.Clamp(x0, 0, _worldSize - 1);
                z0 = Mathf.Clamp(z0, 0, _worldSize - 1);

                float tx = srcX - x0;
                float tz = srcZ - z0;

                // Bilinear interpolation
                float h00 = _heightmap[x0, z0];
                float h10 = _heightmap[x1, z0];
                float h01 = _heightmap[x0, z1];
                float h11 = _heightmap[x1, z1];

                float h = Mathf.Lerp(
                    Mathf.Lerp(h00, h10, tx),
                    Mathf.Lerp(h01, h11, tx),
                    tz
                );

                // Unity heightmap uses [z, x] indexing!
                unityHeightmap[z, x] = Mathf.Clamp01(h);
            }

            // Yield every 64 rows for responsiveness
            if (z % 64 == 0)
            {
                yield return null;
            }
        }

        // Apply heightmap to terrain
        terrainData.SetHeights(0, 0, unityHeightmap);

        // CRITICAL: Flush terrain changes
        _terrain.Flush();

        yield return null;

        // Apply terrain layers - try to load from Resources if not assigned
        if (_terrainLayers == null || _terrainLayers.Length == 0)
        {
            _terrainLayers = LoadDefaultTerrainLayers();
        }

        if (_terrainLayers != null && _terrainLayers.Length > 0)
        {
            terrainData.terrainLayers = _terrainLayers;
            yield return StartCoroutine(PaintTerrainCoroutine(terrainData));
            Debug.Log($"[ProceduralWorldGenerator] Applied {_terrainLayers.Length} terrain layers");
        }
        else
        {
            // Create a single default terrain layer if none found
            TerrainLayer defaultLayer = CreateDefaultTerrainLayer();
            terrainData.terrainLayers = new TerrainLayer[] { defaultLayer };
            Debug.LogWarning("[ProceduralWorldGenerator] Using default terrain layer - no terrain layers found");
        }

        // Final flush to ensure all changes are applied
        _terrain.Flush();

#if UNITY_EDITOR
        // Mark terrain data dirty for saving
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(terrainData);
            EditorUtility.SetDirty(_terrain);
            AssetDatabase.SaveAssets();
        }
#endif

        Debug.Log("[ProceduralWorldGenerator] Terrain applied successfully!");
    }

    private TerrainLayer[] LoadDefaultTerrainLayers()
    {
        // Try to load terrain layers from Resources
        TerrainLayer[] layers = Resources.LoadAll<TerrainLayer>("TerrainLayers");
        if (layers != null && layers.Length > 0)
        {
            Debug.Log($"[ProceduralWorldGenerator] Loaded {layers.Length} terrain layers from Resources");
            return layers;
        }

        return null;
    }

    private TerrainLayer CreateDefaultTerrainLayer()
    {
        TerrainLayer layer = new TerrainLayer();
        layer.diffuseTexture = CreateDefaultTerrainTexture();
        layer.tileSize = new Vector2(10, 10);
        return layer;
    }

    private Texture2D CreateDefaultTerrainTexture()
    {
        // Create a simple grass-like texture
        Texture2D tex = new Texture2D(64, 64);
        Color grassColor = new Color(0.3f, 0.5f, 0.2f);

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float noise = UnityEngine.Random.Range(-0.1f, 0.1f);
                Color pixelColor = new Color(
                    Mathf.Clamp01(grassColor.r + noise),
                    Mathf.Clamp01(grassColor.g + noise),
                    Mathf.Clamp01(grassColor.b + noise),
                    1f
                );
                tex.SetPixel(x, y, pixelColor);
            }
        }
        tex.Apply();
        return tex;
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

        // CRITICAL: Flush terrain after splatmap changes
        if (_terrain != null)
        {
            _terrain.Flush();
        }

        Debug.Log("[ProceduralWorldGenerator] Splatmaps applied and flushed");
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

    #region Material Creation

    /// <summary>
    /// Assigns URP Terrain Lit material to the terrain.
    /// This is CRITICAL - without this, terrain shows magenta/pink.
    /// </summary>
    private void AssignURPTerrainMaterial()
    {
        if (_terrain == null) return;

        // Try to find URP Terrain Lit shader
        Shader urpTerrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");

        // Fallback shader names
        if (urpTerrainShader == null)
            urpTerrainShader = Shader.Find("Universal Render Pipeline/TerrainLit");
        if (urpTerrainShader == null)
            urpTerrainShader = Shader.Find("Terrain/Lit");

        if (urpTerrainShader == null)
        {
            Debug.LogError("[ProceduralWorldGenerator] URP Terrain Lit shader not found! Terrain will appear magenta.");
            return;
        }

        // Create material with URP terrain shader
        Material urpTerrainMaterial = new Material(urpTerrainShader);
        urpTerrainMaterial.name = "URP_Terrain_Material";

        // Enable instancing for better performance
        urpTerrainMaterial.enableInstancing = true;

        // Assign to terrain
        _terrain.materialTemplate = urpTerrainMaterial;

        // Enable draw instanced on terrain for per-pixel normals
        _terrain.drawInstanced = true;

        Debug.Log($"[ProceduralWorldGenerator] Assigned URP Terrain material with shader: {urpTerrainShader.name}");
    }

    private Material CreateDefaultWaterMaterial()
    {
        // Try multiple URP shader paths
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Shader Graphs/URP Lit");
        if (shader == null) shader = Shader.Find("Lit");
        if (shader == null) shader = Shader.Find("Standard");

        if (shader == null)
        {
            Debug.LogError("[ProceduralWorldGenerator] Could not find any valid shader for water!");
            // Return a simple unlit as last resort
            shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
        }

        Material mat = new Material(shader);
        mat.name = "DefaultWater";

        // Set water-like properties
        mat.SetColor("_BaseColor", new Color(0.2f, 0.5f, 0.8f, 0.7f));
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", new Color(0.2f, 0.5f, 0.8f, 0.7f));

        // URP transparency settings
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0); // Alpha
        }

        if (mat.HasProperty("_AlphaClip"))
            mat.SetFloat("_AlphaClip", 0);

        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;

        // Metallic/smoothness for water-like look
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0.1f);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.9f);

        Debug.Log($"[ProceduralWorldGenerator] Created water material with shader: {shader.name}");
        return mat;
    }

    private Material CreateDefaultCloudMaterial()
    {
        // Use URP 2D/Sprite shader for billboard clouds, fallback to Unlit or legacy Sprites/Default
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            Debug.LogWarning("[ProceduralWorldGenerator] No sprite shader found for clouds, using Unlit");
            shader = Shader.Find("Unlit/Color");
        }

        Material mat = new Material(shader);
        mat.name = "DefaultCloud";

        // White semi-transparent - use URP property names with fallback
        Color cloudColor = new Color(1f, 1f, 1f, 0.6f);
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", cloudColor);
            // Enable transparency for URP
            if (mat.HasProperty("_Surface"))
                mat.SetFloat("_Surface", 1); // Transparent
            if (mat.HasProperty("_Blend"))
                mat.SetFloat("_Blend", 0); // Alpha blend
        }
        else if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", cloudColor);
        }
        else
        {
            mat.color = cloudColor;
        }

        return mat;
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
