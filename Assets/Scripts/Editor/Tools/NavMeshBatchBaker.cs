using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// <summary>
/// NavMesh Batch Baker for baking navigation meshes across multiple scenes.
/// Supports area configuration, agent types, and batch processing.
/// </summary>
public class NavMeshBatchBaker : EditorWindow
{
    [MenuItem("EpicLegends/Tools/NavMesh Batch Baker")]
    public static void ShowWindow()
    {
        var window = GetWindow<NavMeshBatchBaker>("NavMesh Batch Baker");
        window.minSize = new Vector2(450, 550);
    }

    [System.Serializable]
    public class SceneBakeInfo
    {
        public string scenePath;
        public string sceneName;
        public bool selected = true;
        public BakeStatus status = BakeStatus.Pending;
        public float bakeTime;
        public int triangleCount;
        public string errorMessage;
    }

    public enum BakeStatus { Pending, Baking, Success, Failed, Skipped }

    // State
    private List<SceneBakeInfo> _scenes = new List<SceneBakeInfo>();
    private string _sceneFolderPath = "Assets/Scenes";
    private bool _includeSubfolders = true;

    // Bake settings
    private float _agentRadius = 0.5f;
    private float _agentHeight = 2f;
    private float _maxSlope = 45f;
    private float _stepHeight = 0.4f;
    private float _dropHeight = 2f;
    private float _jumpDistance = 0f;
    private float _voxelSize = 0.1f;
    private int _minRegionArea = 2;
    private bool _overrideVoxelSize;
    private bool _overrideTileSize;
    private int _tileSize = 256;

    // Area costs
    private Dictionary<string, float> _areaCosts = new Dictionary<string, float>
    {
        { "Walkable", 1f },
        { "Not Walkable", -1f },
        { "Jump", 2f },
        { "Water", 3f },
        { "Difficult", 5f }
    };

    // UI
    private Vector2 _scrollPos;
    private int _currentTab;
    private readonly string[] _tabNames = { "Scenes", "Settings", "Areas", "Batch", "Tools" };

    // Baking state
    private bool _isBaking;
    private int _currentBakeIndex;
    private float _bakeProgress;
    private string _bakeStatus = "";

    private void OnEnable()
    {
        ScanForScenes();
    }

    private void OnGUI()
    {
        DrawToolbar();

        _currentTab = GUILayout.Toolbar(_currentTab, _tabNames, GUILayout.Height(30));

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        switch (_currentTab)
        {
            case 0: DrawScenesTab(); break;
            case 1: DrawSettingsTab(); break;
            case 2: DrawAreasTab(); break;
            case 3: DrawBatchTab(); break;
            case 4: DrawToolsTab(); break;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Scan", EditorStyles.toolbarButton, GUILayout.Width(50)))
            ScanForScenes();

        GUILayout.Space(10);

        EditorGUILayout.LabelField("Folder:", GUILayout.Width(45));
        _sceneFolderPath = EditorGUILayout.TextField(_sceneFolderPath, GUILayout.Width(180));

        if (GUILayout.Button("...", EditorStyles.toolbarButton, GUILayout.Width(25)))
        {
            string path = EditorUtility.OpenFolderPanel("Select Scenes Folder", "Assets", "");
            if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
            {
                _sceneFolderPath = "Assets" + path.Substring(Application.dataPath.Length);
                ScanForScenes();
            }
        }

        _includeSubfolders = GUILayout.Toggle(_includeSubfolders, "Subfolders", EditorStyles.toolbarButton, GUILayout.Width(70));

        GUILayout.FlexibleSpace();

        int selected = _scenes.Count(s => s.selected);
        EditorGUILayout.LabelField($"Selected: {selected}/{_scenes.Count}", GUILayout.Width(100));

        EditorGUILayout.EndHorizontal();

        if (_isBaking)
        {
            Rect progressRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(20));
            EditorGUI.ProgressBar(progressRect, _bakeProgress, _bakeStatus);
        }
    }

    #region Scenes Tab

    private void DrawScenesTab()
    {
        EditorGUILayout.LabelField("Scenes to Bake", EditorStyles.boldLabel);

        // Select all/none
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Select All", GUILayout.Width(80)))
            _scenes.ForEach(s => s.selected = true);
        if (GUILayout.Button("Select None", GUILayout.Width(80)))
            _scenes.ForEach(s => s.selected = false);
        if (GUILayout.Button("Invert", GUILayout.Width(60)))
            _scenes.ForEach(s => s.selected = !s.selected);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Scene list
        foreach (var scene in _scenes)
        {
            DrawSceneItem(scene);
        }

        if (_scenes.Count == 0)
        {
            EditorGUILayout.HelpBox("No scenes found. Click Scan or select a different folder.", MessageType.Info);
        }
    }

    private void DrawSceneItem(SceneBakeInfo scene)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        scene.selected = EditorGUILayout.Toggle(scene.selected, GUILayout.Width(20));

        // Status indicator
        Color statusColor = Color.gray;
        switch (scene.status)
        {
            case BakeStatus.Success: statusColor = Color.green; break;
            case BakeStatus.Failed: statusColor = Color.red; break;
            case BakeStatus.Baking: statusColor = Color.yellow; break;
            case BakeStatus.Skipped: statusColor = Color.cyan; break;
        }
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(10, 18), statusColor);

        EditorGUILayout.LabelField(scene.sceneName, GUILayout.MinWidth(150));

        if (scene.status == BakeStatus.Success)
        {
            EditorGUILayout.LabelField($"{scene.triangleCount} tris", GUILayout.Width(70));
            EditorGUILayout.LabelField($"{scene.bakeTime:F1}s", GUILayout.Width(50));
        }
        else if (scene.status == BakeStatus.Failed)
        {
            GUI.color = Color.red;
            EditorGUILayout.LabelField(scene.errorMessage, GUILayout.MinWidth(100));
            GUI.color = Color.white;
        }

        if (GUILayout.Button("Open", GUILayout.Width(45)))
        {
            EditorSceneManager.OpenScene(scene.scenePath, OpenSceneMode.Single);
        }

        if (GUILayout.Button("Bake", GUILayout.Width(45)))
        {
            BakeSingleScene(scene);
        }

        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region Settings Tab

    private void DrawSettingsTab()
    {
        EditorGUILayout.LabelField("Agent Settings", EditorStyles.boldLabel);

        _agentRadius = EditorGUILayout.Slider("Agent Radius", _agentRadius, 0.1f, 2f);
        _agentHeight = EditorGUILayout.Slider("Agent Height", _agentHeight, 0.5f, 5f);
        _maxSlope = EditorGUILayout.Slider("Max Slope", _maxSlope, 0f, 60f);
        _stepHeight = EditorGUILayout.Slider("Step Height", _stepHeight, 0f, 1f);

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Off-Mesh Links", EditorStyles.boldLabel);
        _dropHeight = EditorGUILayout.Slider("Drop Height", _dropHeight, 0f, 10f);
        _jumpDistance = EditorGUILayout.Slider("Jump Distance", _jumpDistance, 0f, 10f);

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Advanced", EditorStyles.boldLabel);

        _overrideVoxelSize = EditorGUILayout.Toggle("Override Voxel Size", _overrideVoxelSize);
        if (_overrideVoxelSize)
        {
            _voxelSize = EditorGUILayout.Slider("Voxel Size", _voxelSize, 0.01f, 1f);
        }

        _overrideTileSize = EditorGUILayout.Toggle("Override Tile Size", _overrideTileSize);
        if (_overrideTileSize)
        {
            _tileSize = EditorGUILayout.IntSlider("Tile Size", _tileSize, 16, 1024);
        }

        _minRegionArea = EditorGUILayout.IntSlider("Min Region Area", _minRegionArea, 0, 100);

        EditorGUILayout.Space(20);

        // Presets
        EditorGUILayout.LabelField("Agent Presets", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Humanoid"))
        {
            _agentRadius = 0.5f;
            _agentHeight = 2f;
            _maxSlope = 45f;
            _stepHeight = 0.4f;
        }
        if (GUILayout.Button("Large"))
        {
            _agentRadius = 1f;
            _agentHeight = 3f;
            _maxSlope = 30f;
            _stepHeight = 0.6f;
        }
        if (GUILayout.Button("Small"))
        {
            _agentRadius = 0.25f;
            _agentHeight = 1f;
            _maxSlope = 60f;
            _stepHeight = 0.2f;
        }
        if (GUILayout.Button("Flying"))
        {
            _agentRadius = 0.5f;
            _agentHeight = 1f;
            _maxSlope = 90f;
            _stepHeight = 10f;
        }
        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region Areas Tab

    private void DrawAreasTab()
    {
        EditorGUILayout.LabelField("NavMesh Areas", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Configure area costs for pathfinding. Higher cost = less preferred path.", MessageType.Info);

        EditorGUILayout.Space(10);

        // Built-in areas
        string[] areaNames = NavMesh.GetAreaNames();

        for (int i = 0; i < areaNames.Length; i++)
        {
            string areaName = areaNames[i];
            int areaMask = 1 << NavMesh.GetAreaFromName(areaName);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            EditorGUILayout.LabelField(areaName, GUILayout.Width(120));

            if (!_areaCosts.ContainsKey(areaName))
                _areaCosts[areaName] = 1f;

            float cost = _areaCosts[areaName];
            if (cost < 0)
            {
                EditorGUILayout.LabelField("Not Walkable", GUILayout.Width(100));
            }
            else
            {
                _areaCosts[areaName] = EditorGUILayout.Slider(cost, 0f, 10f);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(20);

        // Common presets
        EditorGUILayout.LabelField("Cost Presets", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Default"))
        {
            foreach (var key in _areaCosts.Keys.ToList())
                _areaCosts[key] = 1f;
            _areaCosts["Not Walkable"] = -1f;
        }
        if (GUILayout.Button("Stealth (Avoid Open)"))
        {
            _areaCosts["Walkable"] = 2f;
            if (_areaCosts.ContainsKey("Cover")) _areaCosts["Cover"] = 0.5f;
        }
        if (GUILayout.Button("Speed (Roads Preferred)"))
        {
            _areaCosts["Walkable"] = 2f;
            if (_areaCosts.ContainsKey("Road")) _areaCosts["Road"] = 0.5f;
        }
        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region Batch Tab

    private void DrawBatchTab()
    {
        EditorGUILayout.LabelField("Batch Baking", EditorStyles.boldLabel);

        int selectedCount = _scenes.Count(s => s.selected);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Scenes selected: {selectedCount}");
        EditorGUILayout.LabelField($"Agent: R={_agentRadius} H={_agentHeight}");
        EditorGUILayout.LabelField($"Slope: {_maxSlope}° Step: {_stepHeight}");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        GUI.enabled = selectedCount > 0 && !_isBaking;
        if (GUILayout.Button("Bake Selected Scenes", GUILayout.Height(40)))
        {
            BakeSelectedScenes();
        }
        GUI.enabled = true;

        if (_isBaking)
        {
            if (GUILayout.Button("Cancel", GUILayout.Height(30)))
            {
                _isBaking = false;
            }
        }

        EditorGUILayout.Space(20);

        // Results summary
        EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);

        int success = _scenes.Count(s => s.status == BakeStatus.Success);
        int failed = _scenes.Count(s => s.status == BakeStatus.Failed);
        int pending = _scenes.Count(s => s.status == BakeStatus.Pending);

        EditorGUILayout.BeginHorizontal();
        GUI.color = Color.green;
        EditorGUILayout.LabelField($"Success: {success}", GUILayout.Width(80));
        GUI.color = Color.red;
        EditorGUILayout.LabelField($"Failed: {failed}", GUILayout.Width(80));
        GUI.color = Color.gray;
        EditorGUILayout.LabelField($"Pending: {pending}", GUILayout.Width(80));
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        if (success > 0)
        {
            float totalTime = _scenes.Where(s => s.status == BakeStatus.Success).Sum(s => s.bakeTime);
            int totalTris = _scenes.Where(s => s.status == BakeStatus.Success).Sum(s => s.triangleCount);
            EditorGUILayout.LabelField($"Total bake time: {totalTime:F1}s");
            EditorGUILayout.LabelField($"Total triangles: {totalTris:N0}");
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Clear Results"))
        {
            foreach (var scene in _scenes)
            {
                scene.status = BakeStatus.Pending;
                scene.bakeTime = 0;
                scene.triangleCount = 0;
                scene.errorMessage = "";
            }
        }
    }

    #endregion

    #region Tools Tab

    private void DrawToolsTab()
    {
        EditorGUILayout.LabelField("NavMesh Tools", EditorStyles.boldLabel);

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Open Navigation Window", GUILayout.Height(30)))
        {
            EditorApplication.ExecuteMenuItem("Window/AI/Navigation");
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Current Scene", EditorStyles.boldLabel);

        if (GUILayout.Button("Bake Current Scene", GUILayout.Height(30)))
        {
            BakeCurrentScene();
        }

        if (GUILayout.Button("Clear NavMesh Data", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("Clear NavMesh", "Clear NavMesh data from current scene?", "Yes", "No"))
            {
                UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
            }
        }

        EditorGUILayout.Space(20);

        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);

        if (GUILayout.Button("Show NavMesh Bounds", GUILayout.Height(25)))
        {
            ShowNavMeshBounds();
        }

        if (GUILayout.Button("Validate NavMesh Agents", GUILayout.Height(25)))
        {
            ValidateNavMeshAgents();
        }

        if (GUILayout.Button("Find Unreachable Areas", GUILayout.Height(25)))
        {
            FindUnreachableAreas();
        }

        EditorGUILayout.Space(20);

        EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);

        if (GUILayout.Button("Export NavMesh as OBJ", GUILayout.Height(25)))
        {
            ExportNavMeshAsOBJ();
        }

        if (GUILayout.Button("Generate NavMesh Report", GUILayout.Height(25)))
        {
            GenerateNavMeshReport();
        }
    }

    #endregion

    #region Baking

    private void ScanForScenes()
    {
        _scenes.Clear();

        if (!Directory.Exists(_sceneFolderPath))
        {
            Debug.LogWarning($"Folder not found: {_sceneFolderPath}");
            return;
        }

        SearchOption searchOption = _includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        string[] sceneFiles = Directory.GetFiles(_sceneFolderPath, "*.unity", searchOption);

        foreach (string scenePath in sceneFiles)
        {
            string relativePath = scenePath.Replace("\\", "/");
            if (!relativePath.StartsWith("Assets/"))
            {
                relativePath = "Assets" + relativePath.Substring(Application.dataPath.Length);
            }

            _scenes.Add(new SceneBakeInfo
            {
                scenePath = relativePath,
                sceneName = Path.GetFileNameWithoutExtension(scenePath)
            });
        }

        Debug.Log($"Found {_scenes.Count} scenes");
    }

    private void BakeSelectedScenes()
    {
        var selectedScenes = _scenes.Where(s => s.selected).ToList();
        if (selectedScenes.Count == 0) return;

        _isBaking = true;
        _currentBakeIndex = 0;

        EditorApplication.update += BakeNextScene;
    }

    private void BakeNextScene()
    {
        var selectedScenes = _scenes.Where(s => s.selected).ToList();

        if (!_isBaking || _currentBakeIndex >= selectedScenes.Count)
        {
            _isBaking = false;
            EditorApplication.update -= BakeNextScene;
            _bakeStatus = "Baking complete";
            return;
        }

        var scene = selectedScenes[_currentBakeIndex];
        _bakeProgress = (float)_currentBakeIndex / selectedScenes.Count;
        _bakeStatus = $"Baking {scene.sceneName} ({_currentBakeIndex + 1}/{selectedScenes.Count})";

        BakeSingleScene(scene);

        _currentBakeIndex++;
        Repaint();
    }

    private void BakeSingleScene(SceneBakeInfo sceneInfo)
    {
        sceneInfo.status = BakeStatus.Baking;

        try
        {
            // Save current scene
            string currentScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;

            // Open target scene
            var scene = EditorSceneManager.OpenScene(sceneInfo.scenePath, OpenSceneMode.Single);

            float startTime = Time.realtimeSinceStartup;

            // Configure and bake
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = _agentRadius;
            settings.agentHeight = _agentHeight;
            settings.agentSlope = _maxSlope;
            settings.agentClimb = _stepHeight;

            if (_overrideVoxelSize)
                settings.voxelSize = _voxelSize;

            if (_overrideTileSize)
                settings.tileSize = _tileSize;

            settings.minRegionArea = _minRegionArea;

            // Bake
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

            sceneInfo.bakeTime = Time.realtimeSinceStartup - startTime;

            // Get triangle count
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            sceneInfo.triangleCount = triangulation.indices.Length / 3;

            // Save scene
            EditorSceneManager.SaveScene(scene);

            sceneInfo.status = BakeStatus.Success;

            // Restore previous scene if needed
            if (!string.IsNullOrEmpty(currentScenePath) && currentScenePath != sceneInfo.scenePath)
            {
                EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
            }
        }
        catch (System.Exception e)
        {
            sceneInfo.status = BakeStatus.Failed;
            sceneInfo.errorMessage = e.Message;
            Debug.LogError($"Failed to bake {sceneInfo.sceneName}: {e.Message}");
        }
    }

    private void BakeCurrentScene()
    {
        float startTime = Time.realtimeSinceStartup;

        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        settings.agentRadius = _agentRadius;
        settings.agentHeight = _agentHeight;
        settings.agentSlope = _maxSlope;
        settings.agentClimb = _stepHeight;

        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

        float bakeTime = Time.realtimeSinceStartup - startTime;
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

        Debug.Log($"NavMesh baked: {triangulation.indices.Length / 3} triangles in {bakeTime:F2}s");
    }

    #endregion

    #region Tools

    private void ShowNavMeshBounds()
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

        if (triangulation.vertices.Length == 0)
        {
            Debug.LogWarning("No NavMesh data found");
            return;
        }

        Bounds bounds = new Bounds(triangulation.vertices[0], Vector3.zero);
        foreach (var vertex in triangulation.vertices)
        {
            bounds.Encapsulate(vertex);
        }

        Debug.Log($"NavMesh Bounds: Center={bounds.center} Size={bounds.size}");
    }

    private void ValidateNavMeshAgents()
    {
        NavMeshAgent[] agents = Object.FindObjectsOfType<NavMeshAgent>();
        int validCount = 0;
        int invalidCount = 0;

        foreach (var agent in agents)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(agent.transform.position, out hit, 2f, NavMesh.AllAreas))
            {
                validCount++;
            }
            else
            {
                invalidCount++;
                Debug.LogWarning($"Agent '{agent.name}' is not on NavMesh", agent);
            }
        }

        Debug.Log($"NavMesh Agent Validation: {validCount} valid, {invalidCount} invalid");
    }

    private void FindUnreachableAreas()
    {
        Debug.Log("Finding unreachable areas - implement flood fill algorithm from spawn points");
    }

    private void ExportNavMeshAsOBJ()
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

        if (triangulation.vertices.Length == 0)
        {
            Debug.LogWarning("No NavMesh to export");
            return;
        }

        string path = EditorUtility.SaveFilePanel("Export NavMesh", "", "NavMesh", "obj");
        if (string.IsNullOrEmpty(path)) return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("# NavMesh Export");

        foreach (var v in triangulation.vertices)
        {
            sb.AppendLine($"v {v.x} {v.y} {v.z}");
        }

        for (int i = 0; i < triangulation.indices.Length; i += 3)
        {
            sb.AppendLine($"f {triangulation.indices[i] + 1} {triangulation.indices[i + 1] + 1} {triangulation.indices[i + 2] + 1}");
        }

        File.WriteAllText(path, sb.ToString());
        Debug.Log($"NavMesh exported to: {path}");
    }

    private void GenerateNavMeshReport()
    {
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

        System.Text.StringBuilder report = new System.Text.StringBuilder();
        report.AppendLine("=== NavMesh Report ===");
        report.AppendLine($"Triangles: {triangulation.indices.Length / 3}");
        report.AppendLine($"Vertices: {triangulation.vertices.Length}");

        // Area breakdown
        Dictionary<int, int> areaTriangles = new Dictionary<int, int>();
        for (int i = 0; i < triangulation.areas.Length; i++)
        {
            int area = triangulation.areas[i];
            if (!areaTriangles.ContainsKey(area))
                areaTriangles[area] = 0;
            areaTriangles[area]++;
        }

        report.AppendLine("\nTriangles by Area:");
        foreach (var kvp in areaTriangles)
        {
            string areaName = NavMesh.GetAreaNames().ElementAtOrDefault(kvp.Key) ?? $"Area {kvp.Key}";
            report.AppendLine($"  {areaName}: {kvp.Value}");
        }

        Debug.Log(report.ToString());
    }

    #endregion
}
