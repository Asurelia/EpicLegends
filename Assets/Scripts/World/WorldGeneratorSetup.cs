using UnityEngine;

/// <summary>
/// Script utilitaire pour configurer rapidement le monde procedural.
/// Ajoute les composants necessaires et configure le terrain.
/// </summary>
public class WorldGeneratorSetup : MonoBehaviour
{
    #region Serialized Fields

    [Header("World Settings")]
    [SerializeField] private int _worldSeed = 12345;
    [SerializeField] private int _worldSize = 512;
    [SerializeField] private float _terrainHeight = 100f;
    [SerializeField] private float _waterLevel = 0.3f;

    [Header("Terrain Layers")]
    [SerializeField] private TerrainLayer _grassLayer;
    [SerializeField] private TerrainLayer _rockLayer;
    [SerializeField] private TerrainLayer _sandLayer;
    [SerializeField] private TerrainLayer _snowLayer;
    [SerializeField] private TerrainLayer _dirtLayer;

    [Header("Materials")]
    [SerializeField] private Material _waterMaterial;
    [SerializeField] private Material _cloudMaterial;

    [Header("Noise Settings")]
    [SerializeField] private TerrainNoiseSettings _noiseSettings;

    [Header("Auto Generate")]
    [SerializeField] private bool _generateOnStart = true;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (_generateOnStart)
        {
            SetupAndGenerate();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Configure et genere le monde.
    /// </summary>
    [ContextMenu("Setup and Generate World")]
    public void SetupAndGenerate()
    {
        // Ensure we have a terrain
        Terrain terrain = GetComponentInChildren<Terrain>();
        if (terrain == null)
        {
            terrain = FindFirstObjectByType<Terrain>();
        }

        if (terrain == null)
        {
            Debug.LogError("[WorldGeneratorSetup] No terrain found! Please create a terrain first.");
            return;
        }

        // Setup terrain layers
        SetupTerrainLayers(terrain);

        // Get or add ProceduralWorldGenerator
        var generator = GetComponent<ProceduralWorldGenerator>();
        if (generator == null)
        {
            generator = gameObject.AddComponent<ProceduralWorldGenerator>();
        }

        // Configure generator via serialized fields reflection or public methods
        // Since we can't directly set private serialized fields, we'll create the config

        Debug.Log("[WorldGeneratorSetup] Configuration complete. Starting generation...");

        // Generate the world
        generator.GenerateWorld(_worldSeed);
    }

    /// <summary>
    /// Cree les TerrainLayers par defaut si absents.
    /// </summary>
    [ContextMenu("Create Default Terrain Layers")]
    public void CreateDefaultTerrainLayers()
    {
#if UNITY_EDITOR
        string path = "Assets/Materials/Terrain/";

        // Ensure directory exists
        if (!System.IO.Directory.Exists(Application.dataPath + "/Materials/Terrain"))
        {
            System.IO.Directory.CreateDirectory(Application.dataPath + "/Materials/Terrain");
        }

        // Create grass layer
        if (_grassLayer == null)
        {
            _grassLayer = CreateTerrainLayer("GrassLayer", new Color(0.3f, 0.6f, 0.2f), path);
        }

        // Create rock layer
        if (_rockLayer == null)
        {
            _rockLayer = CreateTerrainLayer("RockLayer", new Color(0.5f, 0.5f, 0.5f), path);
        }

        // Create sand layer
        if (_sandLayer == null)
        {
            _sandLayer = CreateTerrainLayer("SandLayer", new Color(0.9f, 0.85f, 0.6f), path);
        }

        // Create snow layer
        if (_snowLayer == null)
        {
            _snowLayer = CreateTerrainLayer("SnowLayer", new Color(0.95f, 0.95f, 0.98f), path);
        }

        // Create dirt layer
        if (_dirtLayer == null)
        {
            _dirtLayer = CreateTerrainLayer("DirtLayer", new Color(0.45f, 0.35f, 0.25f), path);
        }

        Debug.Log("[WorldGeneratorSetup] Default terrain layers created.");
        UnityEditor.AssetDatabase.Refresh();
#else
        Debug.LogWarning("[WorldGeneratorSetup] Terrain layer creation only available in Editor.");
#endif
    }

    #endregion

    #region Private Methods

    private void SetupTerrainLayers(Terrain terrain)
    {
        var layers = new System.Collections.Generic.List<TerrainLayer>();

        if (_grassLayer != null) layers.Add(_grassLayer);
        if (_rockLayer != null) layers.Add(_rockLayer);
        if (_sandLayer != null) layers.Add(_sandLayer);
        if (_snowLayer != null) layers.Add(_snowLayer);
        if (_dirtLayer != null) layers.Add(_dirtLayer);

        if (layers.Count > 0)
        {
            terrain.terrainData.terrainLayers = layers.ToArray();
            Debug.Log($"[WorldGeneratorSetup] Applied {layers.Count} terrain layers.");
        }
        else
        {
            Debug.LogWarning("[WorldGeneratorSetup] No terrain layers assigned. Terrain will use default texturing.");
        }
    }

#if UNITY_EDITOR
    private TerrainLayer CreateTerrainLayer(string name, Color tint, string path)
    {
        TerrainLayer layer = new TerrainLayer();
        layer.name = name;
        layer.tileSize = new Vector2(10, 10);
        layer.tileOffset = Vector2.zero;

        // Create a simple colored texture
        Texture2D tex = new Texture2D(64, 64);
        Color[] pixels = new Color[64 * 64];
        for (int i = 0; i < pixels.Length; i++)
        {
            // Add slight noise
            float noise = Random.Range(-0.05f, 0.05f);
            pixels[i] = new Color(
                Mathf.Clamp01(tint.r + noise),
                Mathf.Clamp01(tint.g + noise),
                Mathf.Clamp01(tint.b + noise),
                1f
            );
        }
        tex.SetPixels(pixels);
        tex.Apply();

        // Save texture
        string texPath = path + name + "_Diffuse.png";
        System.IO.File.WriteAllBytes(
            Application.dataPath + texPath.Substring(6), // Remove "Assets"
            tex.EncodeToPNG()
        );

        UnityEditor.AssetDatabase.Refresh();

        // Load and assign texture
        Texture2D loadedTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        layer.diffuseTexture = loadedTex;

        // Save layer
        string layerPath = path + name + ".terrainlayer";
        UnityEditor.AssetDatabase.CreateAsset(layer, layerPath);

        return UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
    }
#endif

    #endregion

    #region Validation

    private void OnValidate()
    {
        // Initialize noise settings if null
        if (_noiseSettings == null)
        {
            _noiseSettings = new TerrainNoiseSettings
            {
                scale = 50f,
                octaves = 6,
                persistence = 0.5f,
                lacunarity = 2f,
                redistribution = 1.2f,
                addRidges = true,
                ridgeWeight = 0.3f,
                ridgeScale = 3f,
                erosionIterations = 3
            };
        }

        _worldSize = Mathf.Clamp(_worldSize, 64, 2048);
        _terrainHeight = Mathf.Clamp(_terrainHeight, 10f, 500f);
        _waterLevel = Mathf.Clamp01(_waterLevel);
    }

    #endregion
}
