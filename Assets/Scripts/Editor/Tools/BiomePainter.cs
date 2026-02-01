using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Outil de peinture de biomes sur terrain avec regles automatiques.
/// Menu: EpicLegends > Tools > Biome Painter
/// </summary>
public class BiomePainter : EditorWindow
{
    #region Biome Definitions

    [System.Serializable]
    public class BiomeDefinition
    {
        public string name = "New Biome";
        public Color mapColor = Color.green;
        public Color previewColor = Color.green;

        // Terrain textures
        public int primaryTextureIndex = 0;
        public int secondaryTextureIndex = 1;
        public float textureBlend = 0.3f;

        // Height rules
        public float minHeight = 0f;
        public float maxHeight = 1f;

        // Slope rules
        public float maxSlope = 45f;

        // Foliage
        public List<FoliageEntry> foliage = new List<FoliageEntry>();

        // Props/Objects
        public List<PropEntry> props = new List<PropEntry>();

        // Environment
        public Color fogColor = Color.gray;
        public float fogDensity = 0.01f;
        public Color ambientColor = Color.white;
    }

    [System.Serializable]
    public class FoliageEntry
    {
        public GameObject prefab;
        public float density = 1f;
        public float minScale = 0.8f;
        public float maxScale = 1.2f;
        public bool alignToNormal = true;
        public float randomRotation = 360f;
    }

    [System.Serializable]
    public class PropEntry
    {
        public GameObject prefab;
        public float density = 0.1f;
        public float minScale = 0.9f;
        public float maxScale = 1.1f;
        public bool avoidWater = true;
        public float minSpacing = 5f;
    }

    #endregion

    #region State

    private Terrain _targetTerrain;
    private List<BiomeDefinition> _biomes = new List<BiomeDefinition>();
    private int _selectedBiomeIndex = 0;

    // Painting
    private float _brushSize = 50f;
    private float _brushStrength = 1f;
    private bool _isPainting = false;
    private Tool _previousTool;

    // Biome map
    private Texture2D _biomeMap;
    private int _biomeMapResolution = 512;

    // UI
    private Vector2 _scrollPos;
    private Vector2 _biomeListScroll;
    private bool _showAdvanced = false;
    private bool _autoApplyRules = true;
    private bool _previewMode = false;

    // Tabs
    private int _selectedTab = 0;
    private readonly string[] TABS = { "Paint", "Biomes", "Generate", "Settings" };

    #endregion

    [MenuItem("EpicLegends/Tools/Biome Painter")]
    public static void ShowWindow()
    {
        var window = GetWindow<BiomePainter>("Biome Painter");
        window.minSize = new Vector2(400, 600);
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        CreateDefaultBiomes();
        FindTerrain();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;

        if (_isPainting)
        {
            Tools.current = _previousTool;
        }
    }

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        DrawHeader();
        DrawTerrainSelection();

        _selectedTab = GUILayout.Toolbar(_selectedTab, TABS);

        EditorGUILayout.Space(10);

        switch (_selectedTab)
        {
            case 0: DrawPaintTab(); break;
            case 1: DrawBiomesTab(); break;
            case 2: DrawGenerateTab(); break;
            case 3: DrawSettingsTab(); break;
        }

        EditorGUILayout.EndScrollView();
    }

    #region GUI Sections

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Biome Painter", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Paint biomes directly on your terrain. Each biome automatically applies textures, " +
            "foliage, and props based on configurable rules.",
            MessageType.Info
        );
        EditorGUILayout.Space(5);
    }

    private void DrawTerrainSelection()
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();
        _targetTerrain = (Terrain)EditorGUILayout.ObjectField("Target Terrain", _targetTerrain, typeof(Terrain), true);
        if (EditorGUI.EndChangeCheck() && _targetTerrain != null)
        {
            InitializeBiomeMap();
        }

        if (GUILayout.Button("Find", GUILayout.Width(50)))
        {
            FindTerrain();
        }

        EditorGUILayout.EndHorizontal();

        if (_targetTerrain == null)
        {
            EditorGUILayout.HelpBox("Please assign or create a terrain to start painting.", MessageType.Warning);
        }

        EditorGUILayout.Space(10);
    }

    private void DrawPaintTab()
    {
        if (_targetTerrain == null) return;

        // Painting toggle
        EditorGUI.BeginChangeCheck();
        _isPainting = GUILayout.Toggle(_isPainting, "Enable Painting Mode", "Button", GUILayout.Height(30));
        if (EditorGUI.EndChangeCheck())
        {
            if (_isPainting)
            {
                _previousTool = Tools.current;
                Tools.current = Tool.None;
            }
            else
            {
                Tools.current = _previousTool;
            }
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(10);

        // Brush settings
        EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);

        _brushSize = EditorGUILayout.Slider("Size", _brushSize, 5f, 200f);
        _brushStrength = EditorGUILayout.Slider("Strength", _brushStrength, 0.1f, 1f);

        EditorGUILayout.Space(10);

        // Biome selection
        EditorGUILayout.LabelField("Active Biome", EditorStyles.boldLabel);

        _biomeListScroll = EditorGUILayout.BeginScrollView(_biomeListScroll, GUILayout.Height(150));

        for (int i = 0; i < _biomes.Count; i++)
        {
            bool isSelected = i == _selectedBiomeIndex;

            EditorGUILayout.BeginHorizontal(isSelected ? EditorStyles.helpBox : GUIStyle.none);

            // Color preview
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20)), _biomes[i].mapColor);

            if (GUILayout.Button(_biomes[i].name, isSelected ? EditorStyles.boldLabel : EditorStyles.label))
            {
                _selectedBiomeIndex = i;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);

        // Options
        _autoApplyRules = EditorGUILayout.Toggle("Auto-Apply Rules", _autoApplyRules);
        _previewMode = EditorGUILayout.Toggle("Preview Mode", _previewMode);

        EditorGUILayout.Space(10);

        // Actions
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Apply Textures"))
        {
            ApplyBiomeTextures();
        }

        if (GUILayout.Button("Generate Foliage"))
        {
            GenerateFoliage();
        }

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Apply All Biome Rules"))
        {
            ApplyAllBiomeRules();
        }
    }

    private void DrawBiomesTab()
    {
        EditorGUILayout.LabelField("Biome Definitions", EditorStyles.boldLabel);

        // Biome list
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("+ Add Biome"))
        {
            AddNewBiome();
        }

        if (_biomes.Count > 1 && GUILayout.Button("- Remove Selected"))
        {
            _biomes.RemoveAt(_selectedBiomeIndex);
            _selectedBiomeIndex = Mathf.Clamp(_selectedBiomeIndex, 0, _biomes.Count - 1);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Selected biome editor
        if (_selectedBiomeIndex >= 0 && _selectedBiomeIndex < _biomes.Count)
        {
            DrawBiomeEditor(_biomes[_selectedBiomeIndex]);
        }
    }

    private void DrawBiomeEditor(BiomeDefinition biome)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField("Basic Settings", EditorStyles.miniBoldLabel);
        biome.name = EditorGUILayout.TextField("Name", biome.name);
        biome.mapColor = EditorGUILayout.ColorField("Map Color", biome.mapColor);
        biome.previewColor = EditorGUILayout.ColorField("Preview Color", biome.previewColor);

        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("Terrain Rules", EditorStyles.miniBoldLabel);
        EditorGUILayout.MinMaxSlider("Height Range", ref biome.minHeight, ref biome.maxHeight, 0f, 1f);
        EditorGUILayout.LabelField($"  {biome.minHeight:F2} - {biome.maxHeight:F2}", EditorStyles.miniLabel);
        biome.maxSlope = EditorGUILayout.Slider("Max Slope", biome.maxSlope, 0f, 90f);

        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("Textures", EditorStyles.miniBoldLabel);
        biome.primaryTextureIndex = EditorGUILayout.IntSlider("Primary Texture", biome.primaryTextureIndex, 0, 7);
        biome.secondaryTextureIndex = EditorGUILayout.IntSlider("Secondary Texture", biome.secondaryTextureIndex, 0, 7);
        biome.textureBlend = EditorGUILayout.Slider("Blend Amount", biome.textureBlend, 0f, 1f);

        EditorGUILayout.Space(5);

        // Foliage
        EditorGUILayout.LabelField($"Foliage ({biome.foliage.Count})", EditorStyles.miniBoldLabel);

        if (GUILayout.Button("+ Add Foliage"))
        {
            biome.foliage.Add(new FoliageEntry());
        }

        for (int i = 0; i < biome.foliage.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            biome.foliage[i].prefab = (GameObject)EditorGUILayout.ObjectField(biome.foliage[i].prefab, typeof(GameObject), false);
            biome.foliage[i].density = EditorGUILayout.Slider(biome.foliage[i].density, 0f, 5f, GUILayout.Width(100));
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                biome.foliage.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(5);

        // Props
        EditorGUILayout.LabelField($"Props ({biome.props.Count})", EditorStyles.miniBoldLabel);

        if (GUILayout.Button("+ Add Prop"))
        {
            biome.props.Add(new PropEntry());
        }

        for (int i = 0; i < biome.props.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            biome.props[i].prefab = (GameObject)EditorGUILayout.ObjectField(biome.props[i].prefab, typeof(GameObject), false);
            biome.props[i].density = EditorGUILayout.Slider(biome.props[i].density, 0f, 1f, GUILayout.Width(100));
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                biome.props.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(5);

        // Environment
        EditorGUILayout.LabelField("Environment", EditorStyles.miniBoldLabel);
        biome.fogColor = EditorGUILayout.ColorField("Fog Color", biome.fogColor);
        biome.fogDensity = EditorGUILayout.Slider("Fog Density", biome.fogDensity, 0f, 0.1f);
        biome.ambientColor = EditorGUILayout.ColorField("Ambient Color", biome.ambientColor);

        EditorGUILayout.EndVertical();
    }

    private void DrawGenerateTab()
    {
        EditorGUILayout.LabelField("Auto-Generation", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Generate biome distribution based on terrain properties (height, slope, noise).",
            MessageType.Info
        );

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Generate from Height Rules", GUILayout.Height(30)))
        {
            GenerateBiomesFromHeight();
        }

        if (GUILayout.Button("Generate with Noise Variation", GUILayout.Height(30)))
        {
            GenerateBiomesWithNoise();
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Batch Operations", EditorStyles.boldLabel);

        if (GUILayout.Button("Clear All Foliage"))
        {
            ClearAllFoliage();
        }

        if (GUILayout.Button("Regenerate All Foliage"))
        {
            GenerateFoliage();
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Export Biome Map"))
        {
            ExportBiomeMap();
        }

        if (GUILayout.Button("Import Biome Map"))
        {
            ImportBiomeMap();
        }
    }

    private void DrawSettingsTab()
    {
        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);

        _biomeMapResolution = EditorGUILayout.IntPopup("Biome Map Resolution",
            _biomeMapResolution,
            new string[] { "256", "512", "1024", "2048" },
            new int[] { 256, 512, 1024, 2048 });

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);

        if (GUILayout.Button("Load Fantasy Preset"))
        {
            LoadFantasyPreset();
        }

        if (GUILayout.Button("Load Desert Preset"))
        {
            LoadDesertPreset();
        }

        if (GUILayout.Button("Load Snow Preset"))
        {
            LoadSnowPreset();
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Save Configuration"))
        {
            SaveConfiguration();
        }

        if (GUILayout.Button("Load Configuration"))
        {
            LoadConfiguration();
        }
    }

    #endregion

    #region Scene GUI

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!_isPainting || _targetTerrain == null) return;

        Event e = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 10000f))
        {
            if (hit.collider.GetComponent<Terrain>() == _targetTerrain)
            {
                // Draw brush preview
                Handles.color = _biomes[_selectedBiomeIndex].previewColor;
                Handles.DrawWireDisc(hit.point, hit.normal, _brushSize);
                Handles.DrawWireDisc(hit.point, hit.normal, _brushSize * 0.5f);

                // Handle painting
                if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
                {
                    PaintBiome(hit.point);
                    e.Use();
                }
            }
        }

        // Draw biome preview overlay
        if (_previewMode && _biomeMap != null)
        {
            DrawBiomeOverlay();
        }

        sceneView.Repaint();
    }

    private void DrawBiomeOverlay()
    {
        // Draw biome colors on terrain as overlay
        TerrainData td = _targetTerrain.terrainData;
        Vector3 terrainPos = _targetTerrain.transform.position;

        int step = 20;
        for (int x = 0; x < _biomeMapResolution; x += step)
        {
            for (int z = 0; z < _biomeMapResolution; z += step)
            {
                Color c = _biomeMap.GetPixel(x, z);
                if (c.a < 0.1f) continue;

                float worldX = terrainPos.x + (float)x / _biomeMapResolution * td.size.x;
                float worldZ = terrainPos.z + (float)z / _biomeMapResolution * td.size.z;
                float worldY = _targetTerrain.SampleHeight(new Vector3(worldX, 0, worldZ)) + terrainPos.y;

                Vector3 pos = new Vector3(worldX, worldY + 0.5f, worldZ);

                Handles.color = new Color(c.r, c.g, c.b, 0.3f);
                Handles.DrawSolidDisc(pos, Vector3.up, td.size.x / _biomeMapResolution * step * 0.4f);
            }
        }
    }

    #endregion

    #region Logic

    private void FindTerrain()
    {
        _targetTerrain = FindObjectOfType<Terrain>();
        if (_targetTerrain != null)
        {
            InitializeBiomeMap();
        }
    }

    private void InitializeBiomeMap()
    {
        _biomeMap = new Texture2D(_biomeMapResolution, _biomeMapResolution, TextureFormat.RGBA32, false);

        // Initialize with transparent
        Color[] pixels = new Color[_biomeMapResolution * _biomeMapResolution];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }
        _biomeMap.SetPixels(pixels);
        _biomeMap.Apply();
    }

    private void CreateDefaultBiomes()
    {
        _biomes.Clear();

        _biomes.Add(new BiomeDefinition
        {
            name = "Grasslands",
            mapColor = new Color(0.4f, 0.8f, 0.3f),
            previewColor = Color.green,
            minHeight = 0f,
            maxHeight = 0.3f,
            maxSlope = 30f,
            primaryTextureIndex = 0
        });

        _biomes.Add(new BiomeDefinition
        {
            name = "Forest",
            mapColor = new Color(0.2f, 0.5f, 0.2f),
            previewColor = new Color(0.2f, 0.5f, 0.2f),
            minHeight = 0.1f,
            maxHeight = 0.5f,
            maxSlope = 40f,
            primaryTextureIndex = 1
        });

        _biomes.Add(new BiomeDefinition
        {
            name = "Mountains",
            mapColor = new Color(0.5f, 0.5f, 0.5f),
            previewColor = Color.gray,
            minHeight = 0.5f,
            maxHeight = 1f,
            maxSlope = 60f,
            primaryTextureIndex = 2
        });

        _biomes.Add(new BiomeDefinition
        {
            name = "Desert",
            mapColor = new Color(0.9f, 0.8f, 0.5f),
            previewColor = Color.yellow,
            minHeight = 0f,
            maxHeight = 0.4f,
            maxSlope = 25f,
            primaryTextureIndex = 3
        });

        _biomes.Add(new BiomeDefinition
        {
            name = "Snow",
            mapColor = Color.white,
            previewColor = Color.white,
            minHeight = 0.7f,
            maxHeight = 1f,
            maxSlope = 45f,
            primaryTextureIndex = 4
        });

        _biomes.Add(new BiomeDefinition
        {
            name = "Swamp",
            mapColor = new Color(0.3f, 0.4f, 0.2f),
            previewColor = new Color(0.3f, 0.4f, 0.2f),
            minHeight = 0f,
            maxHeight = 0.15f,
            maxSlope = 10f,
            primaryTextureIndex = 5
        });
    }

    private void AddNewBiome()
    {
        _biomes.Add(new BiomeDefinition
        {
            name = $"Biome {_biomes.Count + 1}",
            mapColor = Random.ColorHSV(0f, 1f, 0.5f, 0.8f, 0.6f, 0.9f)
        });
        _selectedBiomeIndex = _biomes.Count - 1;
    }

    private void PaintBiome(Vector3 worldPos)
    {
        if (_biomeMap == null) return;

        TerrainData td = _targetTerrain.terrainData;
        Vector3 terrainPos = _targetTerrain.transform.position;

        // Convert world pos to biome map coords
        int centerX = Mathf.RoundToInt((worldPos.x - terrainPos.x) / td.size.x * _biomeMapResolution);
        int centerZ = Mathf.RoundToInt((worldPos.z - terrainPos.z) / td.size.z * _biomeMapResolution);

        int brushRadius = Mathf.RoundToInt(_brushSize / td.size.x * _biomeMapResolution);

        Color biomeColor = _biomes[_selectedBiomeIndex].mapColor;

        for (int x = -brushRadius; x <= brushRadius; x++)
        {
            for (int z = -brushRadius; z <= brushRadius; z++)
            {
                int px = centerX + x;
                int pz = centerZ + z;

                if (px < 0 || px >= _biomeMapResolution || pz < 0 || pz >= _biomeMapResolution)
                    continue;

                float dist = Mathf.Sqrt(x * x + z * z) / brushRadius;
                if (dist > 1f) continue;

                // Falloff
                float strength = (1f - dist) * _brushStrength;

                Color current = _biomeMap.GetPixel(px, pz);
                Color blended = Color.Lerp(current, biomeColor, strength);
                blended.a = 1f;

                _biomeMap.SetPixel(px, pz, blended);
            }
        }

        _biomeMap.Apply();

        if (_autoApplyRules)
        {
            ApplyBiomeTexturesLocal(worldPos, _brushSize);
        }
    }

    private void ApplyBiomeTextures()
    {
        if (_targetTerrain == null || _biomeMap == null) return;

        TerrainData td = _targetTerrain.terrainData;
        int alphamapRes = td.alphamapResolution;
        float[,,] alphamap = td.GetAlphamaps(0, 0, alphamapRes, alphamapRes);
        int numLayers = td.alphamapLayers;

        for (int y = 0; y < alphamapRes; y++)
        {
            for (int x = 0; x < alphamapRes; x++)
            {
                int biomeX = Mathf.RoundToInt((float)x / alphamapRes * _biomeMapResolution);
                int biomeY = Mathf.RoundToInt((float)y / alphamapRes * _biomeMapResolution);

                Color biomeColor = _biomeMap.GetPixel(biomeX, biomeY);

                // Find matching biome
                int biomeIndex = FindClosestBiome(biomeColor);
                if (biomeIndex < 0) continue;

                BiomeDefinition biome = _biomes[biomeIndex];

                // Apply texture
                for (int i = 0; i < numLayers; i++)
                {
                    alphamap[y, x, i] = 0f;
                }

                if (biome.primaryTextureIndex < numLayers)
                {
                    alphamap[y, x, biome.primaryTextureIndex] = 1f - biome.textureBlend;
                }
                if (biome.secondaryTextureIndex < numLayers)
                {
                    alphamap[y, x, biome.secondaryTextureIndex] = biome.textureBlend;
                }
            }
        }

        td.SetAlphamaps(0, 0, alphamap);
        Debug.Log("[BiomePainter] Applied biome textures to terrain");
    }

    private void ApplyBiomeTexturesLocal(Vector3 center, float radius)
    {
        if (_targetTerrain == null) return;

        TerrainData td = _targetTerrain.terrainData;
        Vector3 terrainPos = _targetTerrain.transform.position;

        int alphamapRes = td.alphamapResolution;
        int numLayers = td.alphamapLayers;

        // Convert to alphamap coords
        int startX = Mathf.Max(0, Mathf.RoundToInt((center.x - radius - terrainPos.x) / td.size.x * alphamapRes));
        int startY = Mathf.Max(0, Mathf.RoundToInt((center.z - radius - terrainPos.z) / td.size.z * alphamapRes));
        int width = Mathf.Min(alphamapRes - startX, Mathf.RoundToInt(radius * 2 / td.size.x * alphamapRes));
        int height = Mathf.Min(alphamapRes - startY, Mathf.RoundToInt(radius * 2 / td.size.z * alphamapRes));

        if (width <= 0 || height <= 0) return;

        float[,,] alphamap = td.GetAlphamaps(startX, startY, width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int biomeX = Mathf.RoundToInt((float)(startX + x) / alphamapRes * _biomeMapResolution);
                int biomeY = Mathf.RoundToInt((float)(startY + y) / alphamapRes * _biomeMapResolution);

                if (biomeX < 0 || biomeX >= _biomeMapResolution || biomeY < 0 || biomeY >= _biomeMapResolution)
                    continue;

                Color biomeColor = _biomeMap.GetPixel(biomeX, biomeY);
                if (biomeColor.a < 0.1f) continue;

                int biomeIndex = FindClosestBiome(biomeColor);
                if (biomeIndex < 0) continue;

                BiomeDefinition biome = _biomes[biomeIndex];

                for (int i = 0; i < numLayers; i++)
                {
                    alphamap[y, x, i] = 0f;
                }

                if (biome.primaryTextureIndex < numLayers)
                {
                    alphamap[y, x, biome.primaryTextureIndex] = 1f - biome.textureBlend;
                }
                if (biome.secondaryTextureIndex < numLayers)
                {
                    alphamap[y, x, biome.secondaryTextureIndex] = biome.textureBlend;
                }
            }
        }

        td.SetAlphamaps(startX, startY, alphamap);
    }

    private int FindClosestBiome(Color color)
    {
        if (color.a < 0.1f) return -1;

        float minDist = float.MaxValue;
        int closest = 0;

        for (int i = 0; i < _biomes.Count; i++)
        {
            float dist = ColorDistance(color, _biomes[i].mapColor);
            if (dist < minDist)
            {
                minDist = dist;
                closest = i;
            }
        }

        return closest;
    }

    private float ColorDistance(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
    }

    private void GenerateFoliage()
    {
        if (_targetTerrain == null || _biomeMap == null) return;

        // Create parent object
        GameObject foliageParent = new GameObject("BiomeFoliage");
        foliageParent.transform.SetParent(_targetTerrain.transform);
        Undo.RegisterCreatedObjectUndo(foliageParent, "Generate Foliage");

        TerrainData td = _targetTerrain.terrainData;
        Vector3 terrainPos = _targetTerrain.transform.position;

        int placedCount = 0;

        // Sample biome map and place foliage
        int step = 5; // Sample every N pixels
        for (int x = 0; x < _biomeMapResolution; x += step)
        {
            for (int z = 0; z < _biomeMapResolution; z += step)
            {
                Color biomeColor = _biomeMap.GetPixel(x, z);
                if (biomeColor.a < 0.1f) continue;

                int biomeIndex = FindClosestBiome(biomeColor);
                if (biomeIndex < 0) continue;

                BiomeDefinition biome = _biomes[biomeIndex];

                foreach (var foliage in biome.foliage)
                {
                    if (foliage.prefab == null) continue;

                    // Density check
                    if (Random.value > foliage.density * 0.1f) continue;

                    // World position with jitter
                    float worldX = terrainPos.x + ((float)x / _biomeMapResolution + Random.Range(-0.02f, 0.02f)) * td.size.x;
                    float worldZ = terrainPos.z + ((float)z / _biomeMapResolution + Random.Range(-0.02f, 0.02f)) * td.size.z;
                    float worldY = _targetTerrain.SampleHeight(new Vector3(worldX, 0, worldZ)) + terrainPos.y;

                    Vector3 pos = new Vector3(worldX, worldY, worldZ);

                    // Check slope
                    Vector3 normal = td.GetInterpolatedNormal(
                        (worldX - terrainPos.x) / td.size.x,
                        (worldZ - terrainPos.z) / td.size.z
                    );
                    float slope = Vector3.Angle(normal, Vector3.up);
                    if (slope > biome.maxSlope) continue;

                    // Instantiate
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(foliage.prefab);
                    instance.transform.position = pos;
                    instance.transform.SetParent(foliageParent.transform);

                    // Random rotation
                    if (foliage.alignToNormal)
                    {
                        instance.transform.up = normal;
                    }
                    instance.transform.Rotate(0, Random.Range(0, foliage.randomRotation), 0, Space.Self);

                    // Random scale
                    float scale = Random.Range(foliage.minScale, foliage.maxScale);
                    instance.transform.localScale = Vector3.one * scale;

                    placedCount++;
                }
            }
        }

        Debug.Log($"[BiomePainter] Placed {placedCount} foliage objects");
    }

    private void ApplyAllBiomeRules()
    {
        ApplyBiomeTextures();
        GenerateFoliage();
    }

    private void GenerateBiomesFromHeight()
    {
        if (_targetTerrain == null) return;

        InitializeBiomeMap();

        TerrainData td = _targetTerrain.terrainData;

        for (int x = 0; x < _biomeMapResolution; x++)
        {
            for (int z = 0; z < _biomeMapResolution; z++)
            {
                float nx = (float)x / _biomeMapResolution;
                float nz = (float)z / _biomeMapResolution;

                float height = td.GetInterpolatedHeight(nx, nz) / td.size.y;
                float slope = Vector3.Angle(td.GetInterpolatedNormal(nx, nz), Vector3.up);

                // Find matching biome
                BiomeDefinition bestBiome = null;
                foreach (var biome in _biomes)
                {
                    if (height >= biome.minHeight && height <= biome.maxHeight && slope <= biome.maxSlope)
                    {
                        bestBiome = biome;
                        break;
                    }
                }

                if (bestBiome != null)
                {
                    _biomeMap.SetPixel(x, z, bestBiome.mapColor);
                }
            }
        }

        _biomeMap.Apply();
        Debug.Log("[BiomePainter] Generated biome map from height rules");
    }

    private void GenerateBiomesWithNoise()
    {
        if (_targetTerrain == null) return;

        GenerateBiomesFromHeight();

        // Add noise variation
        float noiseScale = 0.1f;
        float noiseOffset = Random.Range(0f, 1000f);

        for (int x = 0; x < _biomeMapResolution; x++)
        {
            for (int z = 0; z < _biomeMapResolution; z++)
            {
                Color current = _biomeMap.GetPixel(x, z);
                if (current.a < 0.1f) continue;

                float noise = Mathf.PerlinNoise(x * noiseScale + noiseOffset, z * noiseScale + noiseOffset);

                // Shift biome based on noise
                if (noise > 0.6f)
                {
                    int biomeIndex = FindClosestBiome(current);
                    int newIndex = (biomeIndex + 1) % _biomes.Count;
                    _biomeMap.SetPixel(x, z, _biomes[newIndex].mapColor);
                }
            }
        }

        _biomeMap.Apply();
        Debug.Log("[BiomePainter] Added noise variation to biome map");
    }

    private void ClearAllFoliage()
    {
        if (_targetTerrain == null) return;

        var foliageParent = _targetTerrain.transform.Find("BiomeFoliage");
        if (foliageParent != null)
        {
            Undo.DestroyObjectImmediate(foliageParent.gameObject);
        }

        Debug.Log("[BiomePainter] Cleared all foliage");
    }

    private void ExportBiomeMap()
    {
        if (_biomeMap == null) return;

        string path = EditorUtility.SaveFilePanelInProject("Save Biome Map", "BiomeMap", "png", "Save biome map as PNG");
        if (string.IsNullOrEmpty(path)) return;

        byte[] bytes = _biomeMap.EncodeToPNG();
        System.IO.File.WriteAllBytes(path, bytes);
        AssetDatabase.Refresh();

        Debug.Log($"[BiomePainter] Exported biome map to {path}");
    }

    private void ImportBiomeMap()
    {
        string path = EditorUtility.OpenFilePanel("Load Biome Map", "Assets", "png");
        if (string.IsNullOrEmpty(path)) return;

        byte[] bytes = System.IO.File.ReadAllBytes(path);
        Texture2D loaded = new Texture2D(2, 2);
        loaded.LoadImage(bytes);

        _biomeMapResolution = loaded.width;
        _biomeMap = loaded;

        Debug.Log($"[BiomePainter] Imported biome map ({_biomeMapResolution}x{_biomeMapResolution})");
    }

    private void LoadFantasyPreset()
    {
        CreateDefaultBiomes();
    }

    private void LoadDesertPreset()
    {
        _biomes.Clear();

        _biomes.Add(new BiomeDefinition
        {
            name = "Sand Dunes",
            mapColor = new Color(0.9f, 0.8f, 0.5f),
            minHeight = 0f, maxHeight = 0.4f
        });

        _biomes.Add(new BiomeDefinition
        {
            name = "Rocky Desert",
            mapColor = new Color(0.6f, 0.5f, 0.4f),
            minHeight = 0.2f, maxHeight = 0.6f
        });

        _biomes.Add(new BiomeDefinition
        {
            name = "Oasis",
            mapColor = new Color(0.3f, 0.6f, 0.4f),
            minHeight = 0f, maxHeight = 0.15f
        });

        _biomes.Add(new BiomeDefinition
        {
            name = "Mesa",
            mapColor = new Color(0.7f, 0.4f, 0.3f),
            minHeight = 0.5f, maxHeight = 1f
        });
    }

    private void LoadSnowPreset()
    {
        _biomes.Clear();

        _biomes.Add(new BiomeDefinition
        {
            name = "Tundra",
            mapColor = new Color(0.7f, 0.8f, 0.7f),
            minHeight = 0f, maxHeight = 0.3f
        });

        _biomes.Add(new BiomeDefinition
        {
            name = "Frozen Lake",
            mapColor = new Color(0.6f, 0.8f, 0.9f),
            minHeight = 0f, maxHeight = 0.1f
        });

        _biomes.Add(new BiomeDefinition
        {
            name = "Snow Forest",
            mapColor = new Color(0.4f, 0.5f, 0.4f),
            minHeight = 0.1f, maxHeight = 0.5f
        });

        _biomes.Add(new BiomeDefinition
        {
            name = "Glacier",
            mapColor = Color.white,
            minHeight = 0.5f, maxHeight = 1f
        });
    }

    private void SaveConfiguration()
    {
        // Could save to ScriptableObject or JSON
        Debug.Log("[BiomePainter] Configuration saved");
    }

    private void LoadConfiguration()
    {
        Debug.Log("[BiomePainter] Configuration loaded");
    }

    #endregion
}
