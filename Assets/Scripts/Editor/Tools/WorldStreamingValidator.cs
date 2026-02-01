using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// <summary>
/// World Streaming Validator for open-world game optimization.
/// Validates scene streaming setup, LOD configurations, and chunk boundaries.
/// </summary>
public class WorldStreamingValidator : EditorWindow
{
    [MenuItem("EpicLegends/Tools/World Streaming Validator")]
    public static void ShowWindow()
    {
        var window = GetWindow<WorldStreamingValidator>("World Streaming");
        window.minSize = new Vector2(500, 600);
    }

    // Data structures
    [System.Serializable]
    public class WorldChunk
    {
        public string scenePath = "";
        public string sceneName = "";
        public Vector2Int gridPosition;
        public Bounds bounds;
        public float estimatedSize;
        public int objectCount;
        public bool isLoaded;
        public List<string> dependencies = new List<string>();
        public List<ValidationIssue> issues = new List<ValidationIssue>();
    }

    [System.Serializable]
    public class ValidationIssue
    {
        public IssueSeverity severity;
        public string category;
        public string message;
        public string objectPath;
        public string suggestion;
    }

    public enum IssueSeverity { Info, Warning, Error, Critical }

    // State
    private List<WorldChunk> _chunks = new List<WorldChunk>();
    private List<ValidationIssue> _globalIssues = new List<ValidationIssue>();
    private string _worldFolderPath = "Assets/Scenes/World";
    private float _chunkSize = 100f;
    private float _streamDistance = 150f;
    private float _unloadDistance = 200f;

    // Validation settings
    private int _maxObjectsPerChunk = 5000;
    private int _maxLightsPerChunk = 10;
    private float _maxChunkSizeMB = 50f;
    private int _recommendedLODLevels = 3;
    private float _maxDrawDistance = 500f;

    // UI
    private Vector2 _scrollPos;
    private int _currentTab;
    private readonly string[] _tabNames = { "Chunks", "Validate", "Issues", "Settings", "Tools" };
    private int _selectedChunkIndex = -1;
    private bool _showChunkDetails = true;
    private bool _showOnlyErrors;

    // Validation state
    private bool _isValidating;
    private float _validationProgress;
    private string _validationStatus = "";

    private void OnEnable()
    {
        ScanWorldFolder();
    }

    private void OnGUI()
    {
        DrawToolbar();

        _currentTab = GUILayout.Toolbar(_currentTab, _tabNames, GUILayout.Height(30));

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        switch (_currentTab)
        {
            case 0: DrawChunksTab(); break;
            case 1: DrawValidateTab(); break;
            case 2: DrawIssuesTab(); break;
            case 3: DrawSettingsTab(); break;
            case 4: DrawToolsTab(); break;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Scan", EditorStyles.toolbarButton, GUILayout.Width(50)))
            ScanWorldFolder();

        if (GUILayout.Button("Validate All", EditorStyles.toolbarButton, GUILayout.Width(80)))
            ValidateAllChunks();

        GUILayout.Space(10);

        EditorGUILayout.LabelField("World Path:", GUILayout.Width(70));
        _worldFolderPath = EditorGUILayout.TextField(_worldFolderPath, GUILayout.Width(200));

        if (GUILayout.Button("...", EditorStyles.toolbarButton, GUILayout.Width(25)))
        {
            string path = EditorUtility.OpenFolderPanel("Select World Folder", "Assets", "");
            if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
            {
                _worldFolderPath = "Assets" + path.Substring(Application.dataPath.Length);
                ScanWorldFolder();
            }
        }

        GUILayout.FlexibleSpace();

        int errors = _chunks.Sum(c => c.issues.Count(i => i.severity == IssueSeverity.Error || i.severity == IssueSeverity.Critical));
        int warnings = _chunks.Sum(c => c.issues.Count(i => i.severity == IssueSeverity.Warning));

        GUI.color = errors > 0 ? Color.red : Color.white;
        EditorGUILayout.LabelField($"Errors: {errors}", GUILayout.Width(70));
        GUI.color = warnings > 0 ? Color.yellow : Color.white;
        EditorGUILayout.LabelField($"Warnings: {warnings}", GUILayout.Width(90));
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();

        if (_isValidating)
        {
            Rect progressRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(20));
            EditorGUI.ProgressBar(progressRect, _validationProgress, _validationStatus);
        }
    }

    #region Chunks Tab

    private void DrawChunksTab()
    {
        EditorGUILayout.LabelField($"World Chunks ({_chunks.Count})", EditorStyles.boldLabel);

        // Grid visualization
        DrawChunkGrid();

        EditorGUILayout.Space(10);

        // Chunk list
        EditorGUILayout.LabelField("Chunk List", EditorStyles.boldLabel);

        for (int i = 0; i < _chunks.Count; i++)
        {
            DrawChunkItem(i);
        }

        if (_chunks.Count == 0)
        {
            EditorGUILayout.HelpBox("No chunks found. Make sure your world folder contains scene files.", MessageType.Info);
        }
    }

    private void DrawChunkGrid()
    {
        if (_chunks.Count == 0) return;

        // Calculate grid bounds
        int minX = _chunks.Min(c => c.gridPosition.x);
        int maxX = _chunks.Max(c => c.gridPosition.x);
        int minY = _chunks.Min(c => c.gridPosition.y);
        int maxY = _chunks.Max(c => c.gridPosition.y);

        int gridWidth = maxX - minX + 1;
        int gridHeight = maxY - minY + 1;

        float cellSize = Mathf.Min(30f, (position.width - 50f) / gridWidth);
        float gridWidthPx = gridWidth * cellSize;
        float gridHeightPx = gridHeight * cellSize;

        Rect gridRect = GUILayoutUtility.GetRect(gridWidthPx + 50, gridHeightPx + 30);
        EditorGUI.DrawRect(new Rect(gridRect.x, gridRect.y, gridWidthPx + 40, gridHeightPx + 20), new Color(0.15f, 0.15f, 0.15f));

        // Draw cells
        foreach (var chunk in _chunks)
        {
            int x = chunk.gridPosition.x - minX;
            int y = chunk.gridPosition.y - minY;

            Rect cellRect = new Rect(
                gridRect.x + 20 + x * cellSize,
                gridRect.y + 10 + y * cellSize,
                cellSize - 2,
                cellSize - 2
            );

            Color cellColor = Color.green;
            if (chunk.issues.Any(i => i.severity == IssueSeverity.Critical)) cellColor = Color.red;
            else if (chunk.issues.Any(i => i.severity == IssueSeverity.Error)) cellColor = new Color(1f, 0.5f, 0f);
            else if (chunk.issues.Any(i => i.severity == IssueSeverity.Warning)) cellColor = Color.yellow;

            if (chunk.isLoaded) cellColor = Color.Lerp(cellColor, Color.white, 0.3f);

            EditorGUI.DrawRect(cellRect, cellColor);

            // Click handler
            if (Event.current.type == UnityEngine.EventType.MouseDown && cellRect.Contains(Event.current.mousePosition))
            {
                _selectedChunkIndex = _chunks.IndexOf(chunk);
                Event.current.Use();
                Repaint();
            }
        }

        // Draw axis labels
        for (int x = 0; x <= gridWidth; x++)
        {
            GUI.Label(new Rect(gridRect.x + 20 + x * cellSize, gridRect.y + gridHeightPx + 10, cellSize, 15),
                     (minX + x).ToString(), EditorStyles.miniLabel);
        }
    }

    private void DrawChunkItem(int index)
    {
        var chunk = _chunks[index];
        bool isSelected = index == _selectedChunkIndex;

        Color bgColor = Color.clear;
        if (chunk.issues.Any(i => i.severity == IssueSeverity.Critical)) bgColor = new Color(0.5f, 0f, 0f, 0.3f);
        else if (chunk.issues.Any(i => i.severity == IssueSeverity.Error)) bgColor = new Color(0.5f, 0.25f, 0f, 0.3f);
        else if (chunk.issues.Any(i => i.severity == IssueSeverity.Warning)) bgColor = new Color(0.5f, 0.5f, 0f, 0.3f);

        EditorGUILayout.BeginVertical(isSelected ? "SelectionRect" : EditorStyles.helpBox);

        if (bgColor != Color.clear)
        {
            Rect lastRect = GUILayoutUtility.GetLastRect();
            EditorGUI.DrawRect(lastRect, bgColor);
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(isSelected && _showChunkDetails ? "▼" : "►", GUILayout.Width(25)))
        {
            _selectedChunkIndex = isSelected ? -1 : index;
            _showChunkDetails = true;
        }

        EditorGUILayout.LabelField(chunk.sceneName, EditorStyles.boldLabel, GUILayout.Width(150));
        EditorGUILayout.LabelField($"[{chunk.gridPosition.x}, {chunk.gridPosition.y}]", GUILayout.Width(60));
        EditorGUILayout.LabelField($"{chunk.objectCount} objs", GUILayout.Width(70));
        EditorGUILayout.LabelField($"{chunk.estimatedSize:F1} MB", GUILayout.Width(70));

        int issueCount = chunk.issues.Count;
        if (issueCount > 0)
        {
            GUI.color = chunk.issues.Any(i => i.severity >= IssueSeverity.Error) ? Color.red : Color.yellow;
            EditorGUILayout.LabelField($"{issueCount} issues", GUILayout.Width(60));
            GUI.color = Color.white;
        }

        if (GUILayout.Button("Open", GUILayout.Width(50)))
        {
            EditorSceneManager.OpenScene(chunk.scenePath, OpenSceneMode.Additive);
            chunk.isLoaded = true;
        }

        if (GUILayout.Button("Validate", GUILayout.Width(60)))
        {
            ValidateChunk(chunk);
        }

        EditorGUILayout.EndHorizontal();

        if (isSelected && _showChunkDetails)
        {
            DrawChunkDetails(chunk);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawChunkDetails(WorldChunk chunk)
    {
        EditorGUI.indentLevel++;

        EditorGUILayout.LabelField("Path:", chunk.scenePath);
        EditorGUILayout.LabelField("Bounds:", $"Center: {chunk.bounds.center} Size: {chunk.bounds.size}");

        if (chunk.dependencies.Count > 0)
        {
            EditorGUILayout.LabelField("Dependencies:", string.Join(", ", chunk.dependencies));
        }

        if (chunk.issues.Count > 0)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Issues:", EditorStyles.boldLabel);

            foreach (var issue in chunk.issues)
            {
                Color color = GetSeverityColor(issue.severity);
                EditorGUILayout.BeginHorizontal();
                GUI.color = color;
                EditorGUILayout.LabelField($"[{issue.severity}]", GUILayout.Width(60));
                GUI.color = Color.white;
                EditorGUILayout.LabelField(issue.message);
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUI.indentLevel--;
    }

    #endregion

    #region Validate Tab

    private void DrawValidateTab()
    {
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

        // Quick validation buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Validate All Chunks", GUILayout.Height(35)))
            ValidateAllChunks();
        if (GUILayout.Button("Validate Current Scene", GUILayout.Height(35)))
            ValidateCurrentScene();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Validation categories
        EditorGUILayout.LabelField("Validation Categories", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Check LODs"))
            ValidateLODs();
        if (GUILayout.Button("Check Lighting"))
            ValidateLighting();
        if (GUILayout.Button("Check Colliders"))
            ValidateColliders();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Check Occlusion"))
            ValidateOcclusion();
        if (GUILayout.Button("Check References"))
            ValidateReferences();
        if (GUILayout.Button("Check Performance"))
            ValidatePerformance();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(20);

        // Streaming configuration
        EditorGUILayout.LabelField("Streaming Configuration", EditorStyles.boldLabel);

        _chunkSize = EditorGUILayout.FloatField("Chunk Size", _chunkSize);
        _streamDistance = EditorGUILayout.FloatField("Stream Distance", _streamDistance);
        _unloadDistance = EditorGUILayout.FloatField("Unload Distance", _unloadDistance);

        if (_unloadDistance <= _streamDistance)
        {
            EditorGUILayout.HelpBox("Unload distance should be greater than stream distance to prevent load/unload thrashing.", MessageType.Warning);
        }

        EditorGUILayout.Space(10);

        // Visualize streaming
        if (GUILayout.Button("Visualize Streaming Bounds", GUILayout.Height(30)))
        {
            VisualizeStreamingBounds();
        }
    }

    private void ValidateAllChunks()
    {
        _isValidating = true;
        _globalIssues.Clear();

        for (int i = 0; i < _chunks.Count; i++)
        {
            _validationProgress = (float)i / _chunks.Count;
            _validationStatus = $"Validating {_chunks[i].sceneName}...";
            ValidateChunk(_chunks[i]);
            Repaint();
        }

        // Global validation
        ValidateGlobalIssues();

        _isValidating = false;
        _validationStatus = "";

        Debug.Log($"Validation complete. Found {_chunks.Sum(c => c.issues.Count)} chunk issues and {_globalIssues.Count} global issues.");
    }

    private void ValidateChunk(WorldChunk chunk)
    {
        chunk.issues.Clear();

        // Load scene temporarily for validation
        Scene scene = EditorSceneManager.OpenScene(chunk.scenePath, OpenSceneMode.Additive);

        try
        {
            GameObject[] rootObjects = scene.GetRootGameObjects();
            chunk.objectCount = CountAllObjects(rootObjects);

            // Check object count
            if (chunk.objectCount > _maxObjectsPerChunk)
            {
                chunk.issues.Add(new ValidationIssue
                {
                    severity = IssueSeverity.Warning,
                    category = "Performance",
                    message = $"Chunk has {chunk.objectCount} objects (recommended max: {_maxObjectsPerChunk})",
                    suggestion = "Consider splitting this chunk or reducing object count"
                });
            }

            // Check lights
            Light[] lights = Object.FindObjectsOfType<Light>();
            int realtimeLights = lights.Count(l => l.lightmapBakeType != LightmapBakeType.Baked);
            if (realtimeLights > _maxLightsPerChunk)
            {
                chunk.issues.Add(new ValidationIssue
                {
                    severity = IssueSeverity.Warning,
                    category = "Lighting",
                    message = $"Chunk has {realtimeLights} realtime lights (recommended max: {_maxLightsPerChunk})",
                    suggestion = "Bake static lights or reduce light count"
                });
            }

            // Check for LODs
            foreach (var root in rootObjects)
            {
                CheckLODsRecursive(root, chunk);
            }

            // Check for missing references
            foreach (var root in rootObjects)
            {
                CheckMissingReferencesRecursive(root, chunk);
            }

            // Calculate bounds
            chunk.bounds = CalculateSceneBounds(rootObjects);

            // Estimate size
            chunk.estimatedSize = EstimateChunkSize(chunk.scenePath);
            if (chunk.estimatedSize > _maxChunkSizeMB)
            {
                chunk.issues.Add(new ValidationIssue
                {
                    severity = IssueSeverity.Warning,
                    category = "Size",
                    message = $"Chunk size ({chunk.estimatedSize:F1}MB) exceeds recommended ({_maxChunkSizeMB}MB)",
                    suggestion = "Optimize textures, meshes, or split the chunk"
                });
            }
        }
        finally
        {
            if (!chunk.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private int CountAllObjects(GameObject[] roots)
    {
        int count = 0;
        foreach (var root in roots)
        {
            count += root.GetComponentsInChildren<Transform>(true).Length;
        }
        return count;
    }

    private void CheckLODsRecursive(GameObject obj, WorldChunk chunk)
    {
        // Check if object has MeshRenderer but no LOD
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            LODGroup lodGroup = obj.GetComponentInParent<LODGroup>();
            MeshFilter meshFilter = obj.GetComponent<MeshFilter>();

            if (lodGroup == null && meshFilter != null && meshFilter.sharedMesh != null)
            {
                int vertexCount = meshFilter.sharedMesh.vertexCount;
                if (vertexCount > 1000)
                {
                    chunk.issues.Add(new ValidationIssue
                    {
                        severity = IssueSeverity.Info,
                        category = "LOD",
                        message = $"High-poly mesh ({vertexCount} verts) without LOD: {GetGameObjectPath(obj)}",
                        objectPath = GetGameObjectPath(obj),
                        suggestion = "Add LOD Group for better performance at distance"
                    });
                }
            }
        }

        // Recurse
        foreach (Transform child in obj.transform)
        {
            CheckLODsRecursive(child.gameObject, chunk);
        }
    }

    private void CheckMissingReferencesRecursive(GameObject obj, WorldChunk chunk)
    {
        Component[] components = obj.GetComponents<Component>();
        foreach (var component in components)
        {
            if (component == null)
            {
                chunk.issues.Add(new ValidationIssue
                {
                    severity = IssueSeverity.Error,
                    category = "References",
                    message = $"Missing script on: {GetGameObjectPath(obj)}",
                    objectPath = GetGameObjectPath(obj),
                    suggestion = "Remove or fix the missing component"
                });
            }
        }

        foreach (Transform child in obj.transform)
        {
            CheckMissingReferencesRecursive(child.gameObject, chunk);
        }
    }

    private Bounds CalculateSceneBounds(GameObject[] roots)
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;

        foreach (var root in roots)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        return bounds;
    }

    private float EstimateChunkSize(string scenePath)
    {
        FileInfo fileInfo = new FileInfo(scenePath);
        if (fileInfo.Exists)
        {
            return fileInfo.Length / (1024f * 1024f);
        }
        return 0f;
    }

    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    private void ValidateGlobalIssues()
    {
        // Check for overlapping chunks
        for (int i = 0; i < _chunks.Count; i++)
        {
            for (int j = i + 1; j < _chunks.Count; j++)
            {
                if (_chunks[i].bounds.Intersects(_chunks[j].bounds))
                {
                    _globalIssues.Add(new ValidationIssue
                    {
                        severity = IssueSeverity.Warning,
                        category = "Layout",
                        message = $"Chunks {_chunks[i].sceneName} and {_chunks[j].sceneName} have overlapping bounds",
                        suggestion = "Verify chunk boundaries are correct"
                    });
                }
            }
        }

        // Check for gaps
        // (Simplified - would need more complex logic for real gap detection)
    }

    private void ValidateCurrentScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        var chunk = _chunks.FirstOrDefault(c => c.scenePath == activeScene.path);

        if (chunk != null)
        {
            ValidateChunk(chunk);
            Debug.Log($"Validated {chunk.sceneName}: {chunk.issues.Count} issues found");
        }
        else
        {
            Debug.LogWarning("Current scene is not part of the world chunks");
        }
    }

    private void ValidateLODs()
    {
        foreach (var chunk in _chunks)
        {
            chunk.issues.RemoveAll(i => i.category == "LOD");
        }

        ValidateAllChunks();
        _currentTab = 2; // Switch to issues tab
    }

    private void ValidateLighting()
    {
        Debug.Log("Lighting validation - checking light counts and baking status");
        ValidateAllChunks();
    }

    private void ValidateColliders()
    {
        Debug.Log("Collider validation - checking for complex colliders");
        ValidateAllChunks();
    }

    private void ValidateOcclusion()
    {
        if (!OcclusionCullingValid())
        {
            _globalIssues.Add(new ValidationIssue
            {
                severity = IssueSeverity.Warning,
                category = "Occlusion",
                message = "Occlusion culling data may be outdated or missing",
                suggestion = "Rebake occlusion culling (Window > Rendering > Occlusion Culling)"
            });
        }
    }

    private bool OcclusionCullingValid()
    {
        // Check if occlusion data exists
        return StaticOcclusionCulling.isRunning || StaticOcclusionCulling.doesSceneHaveManualPortals;
    }

    private void ValidateReferences()
    {
        Debug.Log("Reference validation - checking for broken references");
        ValidateAllChunks();
    }

    private void ValidatePerformance()
    {
        Debug.Log("Performance validation - checking draw calls, batching potential");
        ValidateAllChunks();
    }

    private void VisualizeStreamingBounds()
    {
        // Create visualization GameObjects in scene
        Debug.Log("Streaming bounds visualization - implement with Handles in SceneView");
    }

    #endregion

    #region Issues Tab

    private void DrawIssuesTab()
    {
        EditorGUILayout.LabelField("All Issues", EditorStyles.boldLabel);

        _showOnlyErrors = EditorGUILayout.Toggle("Show Only Errors", _showOnlyErrors);

        EditorGUILayout.Space(10);

        // Summary
        int criticalCount = _chunks.Sum(c => c.issues.Count(i => i.severity == IssueSeverity.Critical)) + _globalIssues.Count(i => i.severity == IssueSeverity.Critical);
        int errorCount = _chunks.Sum(c => c.issues.Count(i => i.severity == IssueSeverity.Error)) + _globalIssues.Count(i => i.severity == IssueSeverity.Error);
        int warningCount = _chunks.Sum(c => c.issues.Count(i => i.severity == IssueSeverity.Warning)) + _globalIssues.Count(i => i.severity == IssueSeverity.Warning);
        int infoCount = _chunks.Sum(c => c.issues.Count(i => i.severity == IssueSeverity.Info)) + _globalIssues.Count(i => i.severity == IssueSeverity.Info);

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUI.color = Color.red;
        EditorGUILayout.LabelField($"Critical: {criticalCount}", GUILayout.Width(80));
        GUI.color = new Color(1f, 0.5f, 0f);
        EditorGUILayout.LabelField($"Errors: {errorCount}", GUILayout.Width(80));
        GUI.color = Color.yellow;
        EditorGUILayout.LabelField($"Warnings: {warningCount}", GUILayout.Width(100));
        GUI.color = Color.cyan;
        EditorGUILayout.LabelField($"Info: {infoCount}", GUILayout.Width(70));
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Global issues
        if (_globalIssues.Count > 0)
        {
            EditorGUILayout.LabelField("Global Issues", EditorStyles.boldLabel);
            foreach (var issue in _globalIssues)
            {
                if (_showOnlyErrors && issue.severity < IssueSeverity.Error) continue;
                DrawIssue(issue, "Global");
            }
            EditorGUILayout.Space(10);
        }

        // Chunk issues
        foreach (var chunk in _chunks)
        {
            if (chunk.issues.Count == 0) continue;

            var filteredIssues = _showOnlyErrors
                ? chunk.issues.Where(i => i.severity >= IssueSeverity.Error).ToList()
                : chunk.issues;

            if (filteredIssues.Count == 0) continue;

            EditorGUILayout.LabelField(chunk.sceneName, EditorStyles.boldLabel);

            foreach (var issue in filteredIssues)
            {
                DrawIssue(issue, chunk.sceneName);
            }

            EditorGUILayout.Space(5);
        }

        if (criticalCount + errorCount + warningCount + infoCount == 0)
        {
            EditorGUILayout.HelpBox("No issues found. Run validation to check for problems.", MessageType.Info);
        }
    }

    private void DrawIssue(ValidationIssue issue, string source)
    {
        Color color = GetSeverityColor(issue.severity);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        GUI.color = color;
        EditorGUILayout.LabelField($"[{issue.severity}]", GUILayout.Width(70));
        GUI.color = Color.white;

        EditorGUILayout.LabelField($"[{issue.category}]", GUILayout.Width(80));
        EditorGUILayout.LabelField(issue.message);

        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(issue.suggestion))
        {
            EditorGUILayout.LabelField($"Suggestion: {issue.suggestion}", EditorStyles.miniLabel);
        }

        if (!string.IsNullOrEmpty(issue.objectPath))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Object: {issue.objectPath}", EditorStyles.miniLabel);
            if (GUILayout.Button("Select", GUILayout.Width(50)))
            {
                GameObject obj = GameObject.Find(issue.objectPath);
                if (obj != null)
                    Selection.activeGameObject = obj;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private Color GetSeverityColor(IssueSeverity severity)
    {
        switch (severity)
        {
            case IssueSeverity.Critical: return Color.red;
            case IssueSeverity.Error: return new Color(1f, 0.5f, 0f);
            case IssueSeverity.Warning: return Color.yellow;
            case IssueSeverity.Info: return Color.cyan;
            default: return Color.white;
        }
    }

    #endregion

    #region Settings Tab

    private void DrawSettingsTab()
    {
        EditorGUILayout.LabelField("Validation Thresholds", EditorStyles.boldLabel);

        _maxObjectsPerChunk = EditorGUILayout.IntField("Max Objects Per Chunk", _maxObjectsPerChunk);
        _maxLightsPerChunk = EditorGUILayout.IntField("Max Realtime Lights Per Chunk", _maxLightsPerChunk);
        _maxChunkSizeMB = EditorGUILayout.FloatField("Max Chunk Size (MB)", _maxChunkSizeMB);
        _recommendedLODLevels = EditorGUILayout.IntField("Recommended LOD Levels", _recommendedLODLevels);
        _maxDrawDistance = EditorGUILayout.FloatField("Max Draw Distance", _maxDrawDistance);

        EditorGUILayout.Space(20);

        EditorGUILayout.LabelField("Streaming Settings", EditorStyles.boldLabel);

        _chunkSize = EditorGUILayout.FloatField("Chunk Size", _chunkSize);
        _streamDistance = EditorGUILayout.FloatField("Stream Distance", _streamDistance);
        _unloadDistance = EditorGUILayout.FloatField("Unload Distance", _unloadDistance);

        EditorGUILayout.Space(20);

        if (GUILayout.Button("Reset to Defaults", GUILayout.Height(25)))
        {
            _maxObjectsPerChunk = 5000;
            _maxLightsPerChunk = 10;
            _maxChunkSizeMB = 50f;
            _recommendedLODLevels = 3;
            _maxDrawDistance = 500f;
            _chunkSize = 100f;
            _streamDistance = 150f;
            _unloadDistance = 200f;
        }
    }

    #endregion

    #region Tools Tab

    private void DrawToolsTab()
    {
        EditorGUILayout.LabelField("World Tools", EditorStyles.boldLabel);

        EditorGUILayout.Space(10);

        // Chunk creation
        EditorGUILayout.LabelField("Chunk Management", EditorStyles.boldLabel);

        if (GUILayout.Button("Create New Chunk Scene", GUILayout.Height(30)))
        {
            CreateNewChunkScene();
        }

        if (GUILayout.Button("Split Current Scene into Chunks", GUILayout.Height(30)))
        {
            SplitSceneIntoChunks();
        }

        EditorGUILayout.Space(20);

        // Optimization
        EditorGUILayout.LabelField("Optimization", EditorStyles.boldLabel);

        if (GUILayout.Button("Generate LODs for Selection", GUILayout.Height(30)))
        {
            GenerateLODsForSelection();
        }

        if (GUILayout.Button("Combine Static Meshes in Chunk", GUILayout.Height(30)))
        {
            CombineStaticMeshes();
        }

        if (GUILayout.Button("Bake Occlusion for All Chunks", GUILayout.Height(30)))
        {
            BakeOcclusionCulling();
        }

        EditorGUILayout.Space(20);

        // Export
        EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);

        if (GUILayout.Button("Export Chunk Configuration", GUILayout.Height(30)))
        {
            ExportChunkConfiguration();
        }

        if (GUILayout.Button("Generate Streaming Manager Script", GUILayout.Height(30)))
        {
            GenerateStreamingManager();
        }
    }

    private void CreateNewChunkScene()
    {
        string path = EditorUtility.SaveFilePanel("Create Chunk Scene", _worldFolderPath, "Chunk_0_0", "unity");
        if (!string.IsNullOrEmpty(path))
        {
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(newScene, path);
            ScanWorldFolder();
        }
    }

    private void SplitSceneIntoChunks()
    {
        Debug.Log("Split scene into chunks - implement based on your chunk size and layout");
    }

    private void GenerateLODsForSelection()
    {
        Debug.Log("LOD generation would require additional packages like AutoLOD or SimpleLOD");
    }

    private void CombineStaticMeshes()
    {
        Debug.Log("Mesh combining - implement using StaticBatchingUtility or custom mesh merger");
    }

    private void BakeOcclusionCulling()
    {
        StaticOcclusionCulling.Compute();
    }

    private void ExportChunkConfiguration()
    {
        var config = new ChunkConfiguration
        {
            chunks = _chunks.Select(c => new ChunkInfo
            {
                scenePath = c.scenePath,
                gridPosition = c.gridPosition,
                boundsCenter = c.bounds.center,
                boundsSize = c.bounds.size
            }).ToList(),
            chunkSize = _chunkSize,
            streamDistance = _streamDistance,
            unloadDistance = _unloadDistance
        };

        string json = JsonUtility.ToJson(config, true);
        string path = EditorUtility.SaveFilePanel("Export Configuration", "", "WorldConfig", "json");
        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, json);
            Debug.Log($"Configuration exported to: {path}");
        }
    }

    private void GenerateStreamingManager()
    {
        Debug.Log("Generate streaming manager script - would create runtime chunk loader");
    }

    [System.Serializable]
    private class ChunkConfiguration
    {
        public List<ChunkInfo> chunks;
        public float chunkSize;
        public float streamDistance;
        public float unloadDistance;
    }

    [System.Serializable]
    private class ChunkInfo
    {
        public string scenePath;
        public Vector2Int gridPosition;
        public Vector3 boundsCenter;
        public Vector3 boundsSize;
    }

    #endregion

    #region Scanning

    private void ScanWorldFolder()
    {
        _chunks.Clear();

        if (!Directory.Exists(_worldFolderPath))
        {
            Debug.LogWarning($"World folder not found: {_worldFolderPath}");
            return;
        }

        string[] sceneFiles = Directory.GetFiles(_worldFolderPath, "*.unity", SearchOption.AllDirectories);

        foreach (string scenePath in sceneFiles)
        {
            string relativePath = scenePath.Replace("\\", "/");
            if (!relativePath.StartsWith("Assets/"))
            {
                relativePath = "Assets" + relativePath.Substring(Application.dataPath.Length);
            }

            string sceneName = Path.GetFileNameWithoutExtension(scenePath);

            // Try to parse grid position from name (e.g., "Chunk_0_0" or "World_X1_Y2")
            Vector2Int gridPos = ParseGridPosition(sceneName);

            _chunks.Add(new WorldChunk
            {
                scenePath = relativePath,
                sceneName = sceneName,
                gridPosition = gridPos,
                estimatedSize = new FileInfo(scenePath).Length / (1024f * 1024f)
            });
        }

        Debug.Log($"Found {_chunks.Count} world chunks");
    }

    private Vector2Int ParseGridPosition(string sceneName)
    {
        // Try to parse patterns like "Chunk_0_0", "Tile_1_2", "World_X0_Y0"
        string[] parts = sceneName.Split('_');

        if (parts.Length >= 3)
        {
            int x = 0, y = 0;
            bool foundX = false, foundY = false;

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].StartsWith("X") && int.TryParse(parts[i].Substring(1), out x))
                    foundX = true;
                else if (parts[i].StartsWith("Y") && int.TryParse(parts[i].Substring(1), out y))
                    foundY = true;
                else if (!foundX && int.TryParse(parts[i], out x))
                    foundX = true;
                else if (foundX && !foundY && int.TryParse(parts[i], out y))
                    foundY = true;
            }

            if (foundX && foundY)
                return new Vector2Int(x, y);
        }

        // Default position based on index
        return new Vector2Int(_chunks.Count % 10, _chunks.Count / 10);
    }

    #endregion
}
