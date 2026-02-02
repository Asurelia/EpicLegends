using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawner de vegetation avec support GPU Instancing.
/// Place la vegetation en fonction des biomes et du terrain.
/// </summary>
public class VegetationSpawner : MonoBehaviour
{
    #region Serialized Fields

    [Header("Spawn Settings")]
    [SerializeField] private float _spawnDensity = 0.5f;
    [SerializeField] private float _minSpacing = 2f;
    [SerializeField] private LayerMask _groundLayer;

    [Header("Performance")]
    [SerializeField] private bool _useGPUInstancing = true;
    [SerializeField] private int _maxInstancesPerBatch = 1023;
    [SerializeField] private float _lodDistance = 50f;
    [SerializeField] private float _cullDistance = 100f;

    [Header("Variation")]
    [SerializeField] private float _scaleVariation = 0.2f;
    [SerializeField] private float _rotationVariation = 360f;
    [SerializeField] private float _colorVariation = 0.1f;

    #endregion

    #region Private Fields

    private int _worldSize;
    private float _worldScale;
    private float[,] _heightmap;
    private float _terrainHeight;
    private float _waterLevel;
    private System.Random _rng;

    private Dictionary<GameObject, List<Matrix4x4>> _instancedObjects;
    private Dictionary<GameObject, MaterialPropertyBlock> _propertyBlocks;
    private List<GameObject> _spawnedObjects;

    private int _totalSpawned;
    private int _totalBatches;

    #endregion

    #region Properties

    public int TotalSpawned => _totalSpawned;
    public int TotalBatches => _totalBatches;

    #endregion

    #region Initialization

    /// <summary>
    /// Initialise le spawner avec les donnees du monde.
    /// </summary>
    public void Initialize(int worldSize, float worldScale, float[,] heightmap, float terrainHeight, float waterLevel)
    {
        _worldSize = worldSize;
        _worldScale = worldScale;
        _heightmap = heightmap;
        _terrainHeight = terrainHeight;
        _waterLevel = waterLevel;
        _rng = new System.Random(worldSize + (int)(worldScale * 100));

        _instancedObjects = new Dictionary<GameObject, List<Matrix4x4>>();
        _propertyBlocks = new Dictionary<GameObject, MaterialPropertyBlock>();
        _spawnedObjects = new List<GameObject>();

        Debug.Log($"[VegetationSpawner] Initialized for world size {worldSize}");
    }

    #endregion

    #region Spawning Coroutine

    /// <summary>
    /// Spawn la vegetation en coroutine pour eviter les freezes.
    /// </summary>
    public IEnumerator SpawnVegetationCoroutine(int objectsPerFrame)
    {
        if (BiomeManager.Instance == null)
        {
            Debug.LogWarning("[VegetationSpawner] BiomeManager not available!");
            yield break;
        }

        _totalSpawned = 0;
        _totalBatches = 0;

        int step = Mathf.Max(1, Mathf.FloorToInt(1f / _spawnDensity));
        int processed = 0;

        for (int z = 0; z < _worldSize; z += step)
        {
            for (int x = 0; x < _worldSize; x += step)
            {
                TrySpawnVegetationAt(x, z);
                processed++;

                if (processed >= objectsPerFrame)
                {
                    processed = 0;
                    yield return null;
                }
            }
        }

        // Finalize instanced rendering
        if (_useGPUInstancing)
        {
            FinalizeInstancing();
        }

        Debug.Log($"[VegetationSpawner] Spawned {_totalSpawned} vegetation objects in {_totalBatches} batches");
    }

    #endregion

    #region Spawning Logic

    private void TrySpawnVegetationAt(int gridX, int gridZ)
    {
        float height = _heightmap[gridX, gridZ];

        // Skip underwater
        if (height < _waterLevel + 0.02f) return;

        // Skip very steep slopes
        float slope = CalculateSlope(gridX, gridZ);
        if (slope > 0.5f) return;

        // Get world position
        float worldX = gridX * _worldScale;
        float worldZ = gridZ * _worldScale;
        float worldY = height * _terrainHeight;

        Vector3 position = new Vector3(worldX, worldY, worldZ);

        // Get biome at position - with null check on singleton
        if (BiomeManager.Instance == null)
        {
            Debug.LogWarning("[VegetationSpawner] BiomeManager.Instance is null - skipping vegetation spawn");
            return;
        }

        BiomeData biome = BiomeManager.Instance.GetBiomeAt(position);
        if (biome == null) return;

        // Random chance based on biome density
        float spawnChance = (biome.vegetationDensity / 50f) * _spawnDensity;
        if ((float)_rng.NextDouble() > spawnChance) return;

        // Get random vegetation prefab
        GameObject prefab = biome.GetRandomVegetation(_rng);
        if (prefab == null) return;

        // Find BiomeObject for this prefab
        BiomeObject settings = FindBiomeObjectSettings(biome.vegetation, prefab);

        // Calculate transform
        float scale = settings != null
            ? Mathf.Lerp(settings.minScale, settings.maxScale, (float)_rng.NextDouble())
            : 1f + ((float)_rng.NextDouble() - 0.5f) * _scaleVariation;

        float rotation = settings != null && settings.randomYRotation
            ? (float)_rng.NextDouble() * _rotationVariation
            : 0f;

        // Raycast to find exact ground position
        Vector3 spawnPos = RaycastToGround(position + Vector3.up * 10f);
        if (spawnPos == Vector3.zero) return;

        // Apply offset
        if (settings != null)
        {
            spawnPos.y += settings.yOffset;
        }

        // Calculate rotation
        Quaternion rot = Quaternion.Euler(0, rotation, 0);

        if (settings != null && settings.alignToSurface)
        {
            rot = GetSurfaceAlignment(spawnPos) * rot;
        }

        // Spawn or add to instancing
        if (_useGPUInstancing && CanBeInstanced(prefab))
        {
            AddToInstancing(prefab, spawnPos, rot, Vector3.one * scale);
        }
        else
        {
            SpawnObject(prefab, spawnPos, rot, Vector3.one * scale);
        }

        _totalSpawned++;
    }

    private BiomeObject FindBiomeObjectSettings(BiomeObject[] objects, GameObject prefab)
    {
        if (objects == null) return null;

        foreach (var obj in objects)
        {
            if (obj.prefab == prefab) return obj;
        }
        return null;
    }

    private float CalculateSlope(int x, int z)
    {
        if (x <= 0 || x >= _worldSize - 1 || z <= 0 || z >= _worldSize - 1)
            return 0f;

        float dx = _heightmap[x + 1, z] - _heightmap[x - 1, z];
        float dz = _heightmap[x, z + 1] - _heightmap[x, z - 1];

        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private Vector3 RaycastToGround(Vector3 start)
    {
        if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, 100f, _groundLayer))
        {
            return hit.point;
        }

        // Fallback: use heightmap
        int x = Mathf.Clamp(Mathf.FloorToInt(start.x / _worldScale), 0, _worldSize - 1);
        int z = Mathf.Clamp(Mathf.FloorToInt(start.z / _worldScale), 0, _worldSize - 1);

        return new Vector3(start.x, _heightmap[x, z] * _terrainHeight, start.z);
    }

    private Quaternion GetSurfaceAlignment(Vector3 position)
    {
        // Calculate surface normal from heightmap
        int x = Mathf.Clamp(Mathf.FloorToInt(position.x / _worldScale), 1, _worldSize - 2);
        int z = Mathf.Clamp(Mathf.FloorToInt(position.z / _worldScale), 1, _worldSize - 2);

        float hL = _heightmap[x - 1, z] * _terrainHeight;
        float hR = _heightmap[x + 1, z] * _terrainHeight;
        float hD = _heightmap[x, z - 1] * _terrainHeight;
        float hU = _heightmap[x, z + 1] * _terrainHeight;

        Vector3 normal = new Vector3(hL - hR, 2f * _worldScale, hD - hU).normalized;

        return Quaternion.FromToRotation(Vector3.up, normal);
    }

    #endregion

    #region GPU Instancing

    private bool CanBeInstanced(GameObject prefab)
    {
        var renderer = prefab.GetComponentInChildren<MeshRenderer>();
        if (renderer == null) return false;

        var material = renderer.sharedMaterial;
        return material != null && material.enableInstancing;
    }

    private void AddToInstancing(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (!_instancedObjects.ContainsKey(prefab))
        {
            _instancedObjects[prefab] = new List<Matrix4x4>();
        }

        Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scale);
        _instancedObjects[prefab].Add(matrix);
    }

    private void FinalizeInstancing()
    {
        foreach (var kvp in _instancedObjects)
        {
            GameObject prefab = kvp.Key;
            List<Matrix4x4> matrices = kvp.Value;

            if (matrices.Count == 0) continue;

            var meshFilter = prefab.GetComponentInChildren<MeshFilter>();
            var meshRenderer = prefab.GetComponentInChildren<MeshRenderer>();

            if (meshFilter == null || meshRenderer == null) continue;

            Mesh mesh = meshFilter.sharedMesh;
            Material material = meshRenderer.sharedMaterial;

            // Create instance drawer
            GameObject drawer = new GameObject($"InstancedVegetation_{prefab.name}");
            drawer.transform.SetParent(transform);

            var instanceDrawer = drawer.AddComponent<GPUInstanceDrawer>();
            instanceDrawer.Initialize(mesh, material, matrices.ToArray());

            _totalBatches++;
        }
    }

    private void SpawnObject(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        GameObject obj = Instantiate(prefab, position, rotation, transform);
        obj.transform.localScale = scale;

        // Add to LOD system if available
        if (LODController.Instance != null)
        {
            LODController.Instance.RegisterWithAutoDetect(obj);
        }

        _spawnedObjects.Add(obj);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Spawn de la vegetation dans une zone specifique.
    /// </summary>
    public void SpawnInArea(Bounds area, float density)
    {
        if (BiomeManager.Instance == null) return;

        int count = Mathf.FloorToInt(area.size.x * area.size.z * density);

        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(area.min.x, area.max.x);
            float z = Random.Range(area.min.z, area.max.z);

            int gridX = Mathf.Clamp(Mathf.FloorToInt(x / _worldScale), 0, _worldSize - 1);
            int gridZ = Mathf.Clamp(Mathf.FloorToInt(z / _worldScale), 0, _worldSize - 1);

            TrySpawnVegetationAt(gridX, gridZ);
        }
    }

    /// <summary>
    /// Supprime toute la vegetation.
    /// </summary>
    public void ClearAll()
    {
        foreach (var obj in _spawnedObjects)
        {
            if (obj != null)
            {
                if (LODController.Instance != null)
                {
                    LODController.Instance.UnregisterObject(obj);
                }
                Destroy(obj);
            }
        }

        _spawnedObjects.Clear();
        _instancedObjects.Clear();
        _totalSpawned = 0;
        _totalBatches = 0;

        // Destroy instance drawers
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }

    #endregion
}

/// <summary>
/// Composant pour dessiner des objets avec GPU Instancing.
/// </summary>
public class GPUInstanceDrawer : MonoBehaviour
{
    private Mesh _mesh;
    private Material _material;
    private Matrix4x4[][] _batches;
    private MaterialPropertyBlock _propertyBlock;
    private Bounds _bounds;

    /// <summary>
    /// Initialise le drawer avec les instances.
    /// </summary>
    public void Initialize(Mesh mesh, Material material, Matrix4x4[] matrices)
    {
        _mesh = mesh;
        _material = material;
        _propertyBlock = new MaterialPropertyBlock();

        // Split into batches of 1023 (Unity limit)
        int batchCount = Mathf.CeilToInt(matrices.Length / 1023f);
        _batches = new Matrix4x4[batchCount][];

        for (int b = 0; b < batchCount; b++)
        {
            int start = b * 1023;
            int count = Mathf.Min(1023, matrices.Length - start);
            _batches[b] = new Matrix4x4[count];
            System.Array.Copy(matrices, start, _batches[b], 0, count);
        }

        // Calculate bounds for all instances
        CalculateBounds(matrices);
    }

    private void CalculateBounds(Matrix4x4[] matrices)
    {
        if (matrices.Length == 0 || _mesh == null)
        {
            _bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
            return;
        }

        Vector3 min = matrices[0].GetColumn(3);
        Vector3 max = min;

        foreach (var matrix in matrices)
        {
            Vector3 pos = matrix.GetColumn(3);
            min = Vector3.Min(min, pos);
            max = Vector3.Max(max, pos);
        }

        // Expand by mesh bounds
        min -= _mesh.bounds.extents;
        max += _mesh.bounds.extents;

        _bounds = new Bounds();
        _bounds.SetMinMax(min, max);
    }

    private void Update()
    {
        if (_mesh == null || _material == null || _batches == null) return;

        foreach (var batch in _batches)
        {
            Graphics.DrawMeshInstanced(
                _mesh,
                0,
                _material,
                batch,
                batch.Length,
                _propertyBlock,
                UnityEngine.Rendering.ShadowCastingMode.On,
                true,
                0,
                null,
                UnityEngine.Rendering.LightProbeUsage.BlendProbes
            );
        }
    }
}
