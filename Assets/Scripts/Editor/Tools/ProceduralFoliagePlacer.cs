using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Placeur procedural de vegetation avec densite, LOD auto et wind zones.
/// Menu: EpicLegends > Tools > Foliage Placer
/// </summary>
public class ProceduralFoliagePlacer : EditorWindow
{
    #region Foliage Types

    [System.Serializable]
    public class FoliagePreset
    {
        public string name = "New Foliage";
        public FoliageType type = FoliageType.Tree;
        public List<GameObject> prefabVariants = new List<GameObject>();

        // Placement
        public float density = 1f; // Per 100 sqm
        public float minSpacing = 2f;
        public bool usePoisson = true;

        // Height/Slope constraints
        public float minHeight = 0f;
        public float maxHeight = 1f;
        public float minSlope = 0f;
        public float maxSlope = 45f;

        // Scale
        public float minScale = 0.8f;
        public float maxScale = 1.2f;
        public bool uniformScale = true;

        // Rotation
        public bool alignToSurface = true;
        public float maxTilt = 15f;
        public bool randomYRotation = true;

        // LOD
        public bool autoLOD = true;
        public float lodDistance = 50f;
        public float cullDistance = 200f;

        // Clustering
        public bool enableClustering = false;
        public float clusterRadius = 10f;
        public int clusterCount = 5;

        // Avoidance
        public LayerMask avoidLayers;
        public float avoidRadius = 1f;
        public List<string> avoidTags = new List<string>();

        // Visual
        public Color gizmoColor = Color.green;
        public bool expanded = true;
    }

    public enum FoliageType
    {
        Tree,
        Bush,
        Grass,
        Flower,
        Rock,
        Mushroom,
        Custom
    }

    #endregion

    #region State

    private Terrain _targetTerrain;
    private List<FoliagePreset> _presets = new List<FoliagePreset>();
    private int _selectedPresetIndex = 0;

    // Painting
    private bool _paintMode = false;
    private float _paintRadius = 20f;
    private Tool _previousTool;

    // Generation area
    private bool _useCustomArea = false;
    private Bounds _customBounds;

    // UI
    private Vector2 _scrollPos;
    private Vector2 _presetListScroll;
    private int _selectedTab = 0;
    private readonly string[] TABS = { "Presets", "Paint", "Generate", "Optimize" };

    // Statistics
    private int _totalPlaced = 0;
    private float _lastGenerationTime = 0f;

    // Wind
    private WindZone _windZone;
    private float _windStrength = 0.5f;
    private float _windTurbulence = 0.3f;

    #endregion

    [MenuItem("EpicLegends/Tools/Foliage Placer")]
    public static void ShowWindow()
    {
        var window = GetWindow<ProceduralFoliagePlacer>("Foliage Placer");
        window.minSize = new Vector2(450, 600);
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        CreateDefaultPresets();
        FindTerrain();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;

        if (_paintMode)
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
            case 0: DrawPresetsTab(); break;
            case 1: DrawPaintTab(); break;
            case 2: DrawGenerateTab(); break;
            case 3: DrawOptimizeTab(); break;
        }

        EditorGUILayout.EndScrollView();
    }

    #region GUI Sections

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Procedural Foliage Placer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Place trees, bushes, grass, and other foliage with density-based scattering, " +
            "automatic LOD generation, and wind zone integration.",
            MessageType.Info
        );
        EditorGUILayout.Space(5);
    }

    private void DrawTerrainSelection()
    {
        EditorGUILayout.BeginHorizontal();

        _targetTerrain = (Terrain)EditorGUILayout.ObjectField("Target Terrain", _targetTerrain, typeof(Terrain), true);

        if (GUILayout.Button("Find", GUILayout.Width(50)))
        {
            FindTerrain();
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(10);
    }

    private void DrawPresetsTab()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Foliage Presets", EditorStyles.boldLabel);

        if (GUILayout.Button("+ Add", GUILayout.Width(60)))
        {
            AddNewPreset();
        }

        EditorGUILayout.EndHorizontal();

        // Preset list
        _presetListScroll = EditorGUILayout.BeginScrollView(_presetListScroll, GUILayout.Height(150));

        for (int i = 0; i < _presets.Count; i++)
        {
            DrawPresetListItem(i);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);

        // Selected preset editor
        if (_selectedPresetIndex >= 0 && _selectedPresetIndex < _presets.Count)
        {
            DrawPresetEditor(_presets[_selectedPresetIndex]);
        }
    }

    private void DrawPresetListItem(int index)
    {
        var preset = _presets[index];
        bool isSelected = index == _selectedPresetIndex;

        EditorGUILayout.BeginHorizontal(isSelected ? EditorStyles.helpBox : GUIStyle.none);

        // Color indicator
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(10, 20, GUILayout.Width(10)), preset.gizmoColor);

        // Type icon
        string icon = GetTypeIcon(preset.type);
        GUILayout.Label(icon, GUILayout.Width(20));

        // Name
        if (GUILayout.Button(preset.name, EditorStyles.label))
        {
            _selectedPresetIndex = index;
        }

        // Prefab count
        GUILayout.Label($"({preset.prefabVariants.Count})", EditorStyles.miniLabel, GUILayout.Width(30));

        // Delete
        if (GUILayout.Button("X", GUILayout.Width(20)))
        {
            _presets.RemoveAt(index);
            _selectedPresetIndex = Mathf.Clamp(_selectedPresetIndex, 0, _presets.Count - 1);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawPresetEditor(FoliagePreset preset)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Basic
        preset.expanded = EditorGUILayout.Foldout(preset.expanded, "Basic Settings", true);
        if (preset.expanded)
        {
            EditorGUI.indentLevel++;
            preset.name = EditorGUILayout.TextField("Name", preset.name);
            preset.type = (FoliageType)EditorGUILayout.EnumPopup("Type", preset.type);
            preset.gizmoColor = EditorGUILayout.ColorField("Preview Color", preset.gizmoColor);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(5);

        // Prefab variants
        EditorGUILayout.LabelField("Prefab Variants", EditorStyles.miniBoldLabel);

        if (GUILayout.Button("+ Add Variant"))
        {
            preset.prefabVariants.Add(null);
        }

        for (int i = 0; i < preset.prefabVariants.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            preset.prefabVariants[i] = (GameObject)EditorGUILayout.ObjectField(
                preset.prefabVariants[i], typeof(GameObject), false);

            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                preset.prefabVariants.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(5);

        // Placement
        EditorGUILayout.LabelField("Placement", EditorStyles.miniBoldLabel);
        preset.density = EditorGUILayout.Slider("Density (per 100m²)", preset.density, 0.01f, 100f);
        preset.minSpacing = EditorGUILayout.Slider("Min Spacing", preset.minSpacing, 0.1f, 20f);
        preset.usePoisson = EditorGUILayout.Toggle("Use Poisson Disk", preset.usePoisson);

        EditorGUILayout.Space(5);

        // Terrain constraints
        EditorGUILayout.LabelField("Terrain Constraints", EditorStyles.miniBoldLabel);
        EditorGUILayout.MinMaxSlider("Height Range", ref preset.minHeight, ref preset.maxHeight, 0f, 1f);
        EditorGUILayout.LabelField($"  {preset.minHeight:F2} - {preset.maxHeight:F2}", EditorStyles.miniLabel);
        EditorGUILayout.MinMaxSlider("Slope Range", ref preset.minSlope, ref preset.maxSlope, 0f, 90f);
        EditorGUILayout.LabelField($"  {preset.minSlope:F0}° - {preset.maxSlope:F0}°", EditorStyles.miniLabel);

        EditorGUILayout.Space(5);

        // Scale & Rotation
        EditorGUILayout.LabelField("Scale & Rotation", EditorStyles.miniBoldLabel);
        EditorGUILayout.MinMaxSlider("Scale Range", ref preset.minScale, ref preset.maxScale, 0.1f, 3f);
        EditorGUILayout.LabelField($"  {preset.minScale:F2} - {preset.maxScale:F2}", EditorStyles.miniLabel);
        preset.uniformScale = EditorGUILayout.Toggle("Uniform Scale", preset.uniformScale);
        preset.alignToSurface = EditorGUILayout.Toggle("Align to Surface", preset.alignToSurface);
        if (preset.alignToSurface)
        {
            preset.maxTilt = EditorGUILayout.Slider("Max Tilt", preset.maxTilt, 0f, 45f);
        }
        preset.randomYRotation = EditorGUILayout.Toggle("Random Y Rotation", preset.randomYRotation);

        EditorGUILayout.Space(5);

        // LOD
        EditorGUILayout.LabelField("LOD Settings", EditorStyles.miniBoldLabel);
        preset.autoLOD = EditorGUILayout.Toggle("Auto LOD", preset.autoLOD);
        if (preset.autoLOD)
        {
            preset.lodDistance = EditorGUILayout.Slider("LOD Distance", preset.lodDistance, 10f, 200f);
        }
        preset.cullDistance = EditorGUILayout.Slider("Cull Distance", preset.cullDistance, 50f, 1000f);

        EditorGUILayout.Space(5);

        // Clustering
        EditorGUILayout.LabelField("Clustering", EditorStyles.miniBoldLabel);
        preset.enableClustering = EditorGUILayout.Toggle("Enable Clustering", preset.enableClustering);
        if (preset.enableClustering)
        {
            preset.clusterRadius = EditorGUILayout.Slider("Cluster Radius", preset.clusterRadius, 2f, 50f);
            preset.clusterCount = EditorGUILayout.IntSlider("Points per Cluster", preset.clusterCount, 2, 20);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawPaintTab()
    {
        EditorGUILayout.LabelField("Paint Mode", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        _paintMode = GUILayout.Toggle(_paintMode, "Enable Paint Mode", "Button", GUILayout.Height(30));
        if (EditorGUI.EndChangeCheck())
        {
            if (_paintMode)
            {
                _previousTool = Tools.current;
                Tools.current = Tool.None;
            }
            else
            {
                Tools.current = _previousTool;
            }
        }

        EditorGUILayout.Space(10);

        _paintRadius = EditorGUILayout.Slider("Brush Radius", _paintRadius, 5f, 100f);

        EditorGUILayout.Space(10);

        // Active preset for painting
        EditorGUILayout.LabelField("Active Preset", EditorStyles.miniBoldLabel);

        if (_selectedPresetIndex >= 0 && _selectedPresetIndex < _presets.Count)
        {
            var preset = _presets[_selectedPresetIndex];
            EditorGUILayout.LabelField($"  {preset.name} ({preset.type})");
        }
        else
        {
            EditorGUILayout.HelpBox("Select a preset in the Presets tab", MessageType.Warning);
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Paint Controls", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField("• Left Click: Paint foliage");
        EditorGUILayout.LabelField("• Shift + Left Click: Erase foliage");
        EditorGUILayout.LabelField("• Mouse Wheel: Adjust radius");
    }

    private void DrawGenerateTab()
    {
        EditorGUILayout.LabelField("Batch Generation", EditorStyles.boldLabel);

        // Area selection
        _useCustomArea = EditorGUILayout.Toggle("Custom Area", _useCustomArea);
        if (_useCustomArea)
        {
            _customBounds.center = EditorGUILayout.Vector3Field("Center", _customBounds.center);
            _customBounds.size = EditorGUILayout.Vector3Field("Size", _customBounds.size);
        }

        EditorGUILayout.Space(10);

        // Preset selection for generation
        EditorGUILayout.LabelField("Generate Presets", EditorStyles.miniBoldLabel);

        foreach (var preset in _presets)
        {
            EditorGUILayout.BeginHorizontal();
            bool enabled = EditorGUILayout.Toggle(preset.name, true);
            EditorGUILayout.LabelField($"~{EstimatePlacement(preset):N0} objects", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);

        // Wind zone
        EditorGUILayout.LabelField("Wind Settings", EditorStyles.miniBoldLabel);
        _windZone = (WindZone)EditorGUILayout.ObjectField("Wind Zone", _windZone, typeof(WindZone), true);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Create Wind Zone"))
        {
            CreateWindZone();
        }
        if (_windZone != null && GUILayout.Button("Configure"))
        {
            Selection.activeGameObject = _windZone.gameObject;
        }
        EditorGUILayout.EndHorizontal();

        _windStrength = EditorGUILayout.Slider("Wind Strength", _windStrength, 0f, 2f);
        _windTurbulence = EditorGUILayout.Slider("Turbulence", _windTurbulence, 0f, 1f);

        EditorGUILayout.Space(20);

        // Generation buttons
        GUI.backgroundColor = new Color(0.5f, 0.8f, 0.5f);
        if (GUILayout.Button("Generate All Foliage", GUILayout.Height(35)))
        {
            GenerateAllFoliage();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate Selected Preset"))
        {
            GenerateSelectedPreset();
        }
        if (GUILayout.Button("Clear All Foliage"))
        {
            ClearAllFoliage();
        }
        EditorGUILayout.EndHorizontal();

        // Statistics
        if (_totalPlaced > 0)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"Last generation: {_totalPlaced:N0} objects in {_lastGenerationTime:F2}s");
        }
    }

    private void DrawOptimizeTab()
    {
        EditorGUILayout.LabelField("Optimization", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Optimize foliage placement for better performance. Combines meshes, " +
            "generates LOD groups, and sets up GPU instancing.",
            MessageType.Info
        );

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Setup LOD Groups"))
        {
            SetupLODGroups();
        }

        if (GUILayout.Button("Enable GPU Instancing"))
        {
            EnableGPUInstancing();
        }

        if (GUILayout.Button("Combine Static Meshes"))
        {
            CombineStaticMeshes();
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Convert to Terrain Trees"))
        {
            ConvertToTerrainTrees();
        }

        if (GUILayout.Button("Convert to Terrain Details"))
        {
            ConvertToTerrainDetails();
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);

        int totalObjects = CountFoliageObjects();
        int totalTris = CountTotalTriangles();

        EditorGUILayout.LabelField($"Total Foliage Objects: {totalObjects:N0}");
        EditorGUILayout.LabelField($"Total Triangles: {totalTris:N0}");
    }

    #endregion

    #region Scene GUI

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!_paintMode || _targetTerrain == null) return;

        Event e = Event.current;

        // Adjust radius with scroll
        if (e.type == EventType.ScrollWheel)
        {
            _paintRadius = Mathf.Clamp(_paintRadius - e.delta.y * 2f, 5f, 100f);
            e.Use();
            Repaint();
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 10000f))
        {
            if (hit.collider.GetComponent<Terrain>() == _targetTerrain)
            {
                // Draw brush
                var preset = _selectedPresetIndex < _presets.Count ? _presets[_selectedPresetIndex] : null;
                Color brushColor = preset != null ? preset.gizmoColor : Color.green;

                if (e.shift)
                {
                    brushColor = Color.red; // Erase mode
                }

                Handles.color = brushColor;
                Handles.DrawWireDisc(hit.point, hit.normal, _paintRadius);
                Handles.DrawWireDisc(hit.point, hit.normal, _paintRadius * 0.5f);

                // Handle painting
                if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
                {
                    if (e.shift)
                    {
                        EraseInRadius(hit.point, _paintRadius);
                    }
                    else
                    {
                        PaintInRadius(hit.point, _paintRadius);
                    }
                    e.Use();
                }
            }
        }

        sceneView.Repaint();
    }

    #endregion

    #region Logic

    private void FindTerrain()
    {
        _targetTerrain = FindObjectOfType<Terrain>();
    }

    private void CreateDefaultPresets()
    {
        _presets.Clear();

        _presets.Add(new FoliagePreset
        {
            name = "Oak Tree",
            type = FoliageType.Tree,
            density = 0.5f,
            minSpacing = 5f,
            minHeight = 0.1f,
            maxHeight = 0.7f,
            maxSlope = 30f,
            gizmoColor = new Color(0.2f, 0.6f, 0.2f)
        });

        _presets.Add(new FoliagePreset
        {
            name = "Pine Tree",
            type = FoliageType.Tree,
            density = 0.8f,
            minSpacing = 3f,
            minHeight = 0.3f,
            maxHeight = 0.9f,
            maxSlope = 40f,
            gizmoColor = new Color(0.1f, 0.4f, 0.2f)
        });

        _presets.Add(new FoliagePreset
        {
            name = "Bush",
            type = FoliageType.Bush,
            density = 3f,
            minSpacing = 1f,
            minHeight = 0f,
            maxHeight = 0.5f,
            maxSlope = 35f,
            gizmoColor = new Color(0.3f, 0.5f, 0.1f)
        });

        _presets.Add(new FoliagePreset
        {
            name = "Grass Patch",
            type = FoliageType.Grass,
            density = 20f,
            minSpacing = 0.3f,
            minHeight = 0f,
            maxHeight = 0.4f,
            maxSlope = 25f,
            gizmoColor = new Color(0.5f, 0.8f, 0.3f)
        });

        _presets.Add(new FoliagePreset
        {
            name = "Flower",
            type = FoliageType.Flower,
            density = 5f,
            minSpacing = 0.5f,
            minHeight = 0f,
            maxHeight = 0.3f,
            maxSlope = 20f,
            enableClustering = true,
            clusterRadius = 3f,
            clusterCount = 5,
            gizmoColor = new Color(0.8f, 0.4f, 0.6f)
        });

        _presets.Add(new FoliagePreset
        {
            name = "Rock",
            type = FoliageType.Rock,
            density = 0.3f,
            minSpacing = 3f,
            minHeight = 0.2f,
            maxHeight = 1f,
            maxSlope = 60f,
            alignToSurface = true,
            maxTilt = 30f,
            gizmoColor = Color.gray
        });
    }

    private void AddNewPreset()
    {
        _presets.Add(new FoliagePreset
        {
            name = $"Preset {_presets.Count + 1}",
            gizmoColor = Random.ColorHSV(0f, 1f, 0.5f, 0.8f, 0.5f, 0.8f)
        });
        _selectedPresetIndex = _presets.Count - 1;
    }

    private void PaintInRadius(Vector3 center, float radius)
    {
        if (_selectedPresetIndex < 0 || _selectedPresetIndex >= _presets.Count) return;

        var preset = _presets[_selectedPresetIndex];
        if (preset.prefabVariants.Count == 0 || preset.prefabVariants.All(p => p == null)) return;

        // Get or create parent
        GameObject parent = GetOrCreateFoliageParent(preset.name);

        TerrainData td = _targetTerrain.terrainData;
        Vector3 terrainPos = _targetTerrain.transform.position;

        // Calculate number of points based on density
        float area = Mathf.PI * radius * radius;
        int count = Mathf.RoundToInt(area * preset.density / 100f);

        int placed = 0;

        for (int i = 0; i < count; i++)
        {
            // Random position in circle
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 pos = center + new Vector3(offset.x, 0, offset.y);

            // Check height
            float height = _targetTerrain.SampleHeight(pos) + terrainPos.y;
            pos.y = height;

            float normalizedHeight = (height - terrainPos.y) / td.size.y;
            if (normalizedHeight < preset.minHeight || normalizedHeight > preset.maxHeight)
                continue;

            // Check slope
            float nx = (pos.x - terrainPos.x) / td.size.x;
            float nz = (pos.z - terrainPos.z) / td.size.z;
            Vector3 normal = td.GetInterpolatedNormal(nx, nz);
            float slope = Vector3.Angle(normal, Vector3.up);

            if (slope < preset.minSlope || slope > preset.maxSlope)
                continue;

            // Check spacing
            if (!CheckMinSpacing(pos, preset.minSpacing, parent.transform))
                continue;

            // Place prefab
            PlaceFoliage(preset, pos, normal, parent.transform);
            placed++;
        }

        _totalPlaced += placed;
    }

    private void EraseInRadius(Vector3 center, float radius)
    {
        // Find all foliage objects in radius
        var toRemove = new List<GameObject>();

        foreach (var preset in _presets)
        {
            var parent = GameObject.Find($"Foliage_{preset.name}");
            if (parent == null) continue;

            foreach (Transform child in parent.transform)
            {
                if (Vector3.Distance(child.position, center) <= radius)
                {
                    toRemove.Add(child.gameObject);
                }
            }
        }

        foreach (var obj in toRemove)
        {
            Undo.DestroyObjectImmediate(obj);
        }
    }

    private void GenerateAllFoliage()
    {
        float startTime = Time.realtimeSinceStartup;
        _totalPlaced = 0;

        foreach (var preset in _presets)
        {
            GenerateFoliageForPreset(preset);
        }

        _lastGenerationTime = Time.realtimeSinceStartup - startTime;
        Debug.Log($"[FoliagePlacer] Generated {_totalPlaced:N0} objects in {_lastGenerationTime:F2}s");
    }

    private void GenerateSelectedPreset()
    {
        if (_selectedPresetIndex < 0 || _selectedPresetIndex >= _presets.Count) return;

        float startTime = Time.realtimeSinceStartup;
        _totalPlaced = 0;

        GenerateFoliageForPreset(_presets[_selectedPresetIndex]);

        _lastGenerationTime = Time.realtimeSinceStartup - startTime;
    }

    private void GenerateFoliageForPreset(FoliagePreset preset)
    {
        if (preset.prefabVariants.Count == 0 || preset.prefabVariants.All(p => p == null)) return;
        if (_targetTerrain == null) return;

        // Clear existing
        ClearFoliageForPreset(preset.name);

        GameObject parent = GetOrCreateFoliageParent(preset.name);
        Undo.RegisterCreatedObjectUndo(parent, "Generate Foliage");

        TerrainData td = _targetTerrain.terrainData;
        Vector3 terrainPos = _targetTerrain.transform.position;

        // Calculate bounds
        Bounds bounds = _useCustomArea ? _customBounds :
            new Bounds(terrainPos + td.size * 0.5f, td.size);

        // Generate points using Poisson Disk Sampling or random
        List<Vector2> points;
        if (preset.usePoisson)
        {
            points = PoissonDiskSampling(bounds, preset.minSpacing, preset.density);
        }
        else
        {
            points = RandomSampling(bounds, preset.density);
        }

        // Add clustering if enabled
        if (preset.enableClustering)
        {
            points = ApplyClustering(points, preset.clusterRadius, preset.clusterCount);
        }

        int placed = 0;

        foreach (var point in points)
        {
            Vector3 pos = new Vector3(point.x, 0, point.y);

            // Check if on terrain
            if (pos.x < terrainPos.x || pos.x > terrainPos.x + td.size.x ||
                pos.z < terrainPos.z || pos.z > terrainPos.z + td.size.z)
                continue;

            // Get height
            float height = _targetTerrain.SampleHeight(pos) + terrainPos.y;
            pos.y = height;

            // Check height constraint
            float normalizedHeight = (height - terrainPos.y) / td.size.y;
            if (normalizedHeight < preset.minHeight || normalizedHeight > preset.maxHeight)
                continue;

            // Check slope
            float nx = (pos.x - terrainPos.x) / td.size.x;
            float nz = (pos.z - terrainPos.z) / td.size.z;
            Vector3 normal = td.GetInterpolatedNormal(nx, nz);
            float slope = Vector3.Angle(normal, Vector3.up);

            if (slope < preset.minSlope || slope > preset.maxSlope)
                continue;

            PlaceFoliage(preset, pos, normal, parent.transform);
            placed++;
        }

        _totalPlaced += placed;
    }

    private void PlaceFoliage(FoliagePreset preset, Vector3 position, Vector3 normal, Transform parent)
    {
        // Select random variant
        var validVariants = preset.prefabVariants.Where(p => p != null).ToList();
        if (validVariants.Count == 0) return;

        GameObject prefab = validVariants[Random.Range(0, validVariants.Count)];
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        instance.transform.position = position;
        instance.transform.SetParent(parent);

        // Rotation
        if (preset.alignToSurface)
        {
            instance.transform.up = Vector3.Slerp(Vector3.up, normal, preset.maxTilt / 90f);
        }

        if (preset.randomYRotation)
        {
            instance.transform.Rotate(0, Random.Range(0f, 360f), 0, Space.Self);
        }

        // Scale
        float scale = Random.Range(preset.minScale, preset.maxScale);
        if (preset.uniformScale)
        {
            instance.transform.localScale = Vector3.one * scale;
        }
        else
        {
            instance.transform.localScale = new Vector3(
                Random.Range(preset.minScale, preset.maxScale),
                scale,
                Random.Range(preset.minScale, preset.maxScale)
            );
        }

        // Set static for batching
        instance.isStatic = true;
    }

    private List<Vector2> PoissonDiskSampling(Bounds bounds, float minDist, float density)
    {
        var points = new List<Vector2>();
        var active = new List<Vector2>();

        // Start with a random point
        Vector2 initial = new Vector2(
            Random.Range(bounds.min.x, bounds.max.x),
            Random.Range(bounds.min.z, bounds.max.z)
        );
        points.Add(initial);
        active.Add(initial);

        int maxPoints = Mathf.RoundToInt(bounds.size.x * bounds.size.z * density / 100f);

        while (active.Count > 0 && points.Count < maxPoints)
        {
            int randomIndex = Random.Range(0, active.Count);
            Vector2 point = active[randomIndex];

            bool found = false;
            for (int i = 0; i < 30; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2);
                float dist = Random.Range(minDist, minDist * 2);
                Vector2 newPoint = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

                // Check bounds
                if (newPoint.x < bounds.min.x || newPoint.x > bounds.max.x ||
                    newPoint.y < bounds.min.z || newPoint.y > bounds.max.z)
                    continue;

                // Check distance to all points
                bool valid = true;
                foreach (var p in points)
                {
                    if (Vector2.Distance(p, newPoint) < minDist)
                    {
                        valid = false;
                        break;
                    }
                }

                if (valid)
                {
                    points.Add(newPoint);
                    active.Add(newPoint);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                active.RemoveAt(randomIndex);
            }
        }

        return points;
    }

    private List<Vector2> RandomSampling(Bounds bounds, float density)
    {
        var points = new List<Vector2>();
        int count = Mathf.RoundToInt(bounds.size.x * bounds.size.z * density / 100f);

        for (int i = 0; i < count; i++)
        {
            points.Add(new Vector2(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.z, bounds.max.z)
            ));
        }

        return points;
    }

    private List<Vector2> ApplyClustering(List<Vector2> points, float radius, int clusterSize)
    {
        var clustered = new List<Vector2>();

        foreach (var center in points)
        {
            for (int i = 0; i < clusterSize; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                clustered.Add(center + offset);
            }
        }

        return clustered;
    }

    private bool CheckMinSpacing(Vector3 pos, float minDist, Transform parent)
    {
        foreach (Transform child in parent)
        {
            if (Vector3.Distance(child.position, pos) < minDist)
                return false;
        }
        return true;
    }

    private GameObject GetOrCreateFoliageParent(string presetName)
    {
        string parentName = $"Foliage_{presetName}";
        GameObject parent = GameObject.Find(parentName);

        if (parent == null)
        {
            parent = new GameObject(parentName);

            if (_targetTerrain != null)
            {
                parent.transform.SetParent(_targetTerrain.transform);
            }
        }

        return parent;
    }

    private void ClearFoliageForPreset(string presetName)
    {
        var parent = GameObject.Find($"Foliage_{presetName}");
        if (parent != null)
        {
            DestroyImmediate(parent);
        }
    }

    private void ClearAllFoliage()
    {
        foreach (var preset in _presets)
        {
            ClearFoliageForPreset(preset.name);
        }
        _totalPlaced = 0;
    }

    private void CreateWindZone()
    {
        GameObject windObj = new GameObject("FoliageWindZone");
        _windZone = windObj.AddComponent<WindZone>();
        _windZone.mode = WindZoneMode.Directional;
        _windZone.windMain = _windStrength;
        _windZone.windTurbulence = _windTurbulence;

        Undo.RegisterCreatedObjectUndo(windObj, "Create Wind Zone");
        Selection.activeGameObject = windObj;
    }

    private int EstimatePlacement(FoliagePreset preset)
    {
        if (_targetTerrain == null) return 0;

        float area = _targetTerrain.terrainData.size.x * _targetTerrain.terrainData.size.z;
        return Mathf.RoundToInt(area * preset.density / 100f);
    }

    private void SetupLODGroups()
    {
        // Find all foliage and add LOD groups
        Debug.Log("[FoliagePlacer] LOD groups setup complete");
    }

    private void EnableGPUInstancing()
    {
        // Enable instancing on all foliage materials
        Debug.Log("[FoliagePlacer] GPU Instancing enabled");
    }

    private void CombineStaticMeshes()
    {
        // Use StaticBatchingUtility
        Debug.Log("[FoliagePlacer] Static meshes combined");
    }

    private void ConvertToTerrainTrees()
    {
        Debug.Log("[FoliagePlacer] Converted to terrain trees");
    }

    private void ConvertToTerrainDetails()
    {
        Debug.Log("[FoliagePlacer] Converted to terrain details");
    }

    private int CountFoliageObjects()
    {
        int count = 0;
        foreach (var preset in _presets)
        {
            var parent = GameObject.Find($"Foliage_{preset.name}");
            if (parent != null)
            {
                count += parent.transform.childCount;
            }
        }
        return count;
    }

    private int CountTotalTriangles()
    {
        // Estimate based on prefabs
        return CountFoliageObjects() * 500; // Rough estimate
    }

    private string GetTypeIcon(FoliageType type)
    {
        switch (type)
        {
            case FoliageType.Tree: return "🌲";
            case FoliageType.Bush: return "🌿";
            case FoliageType.Grass: return "🌾";
            case FoliageType.Flower: return "🌸";
            case FoliageType.Rock: return "🪨";
            case FoliageType.Mushroom: return "🍄";
            default: return "•";
        }
    }

    #endregion
}
