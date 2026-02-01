using UnityEngine;
using UnityEditor;
using UnityEngine.Profiling;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// In-Editor Performance Profiler for monitoring and optimizing game performance.
/// Tracks FPS, memory, draw calls, and provides optimization suggestions.
/// </summary>
public class PerformanceProfiler : EditorWindow
{
    [MenuItem("EpicLegends/Tools/Performance Profiler")]
    public static void ShowWindow()
    {
        var window = GetWindow<PerformanceProfiler>("Performance Profiler");
        window.minSize = new Vector2(500, 600);
    }

    // Data structures
    [System.Serializable]
    public class PerformanceSnapshot
    {
        public float timestamp;
        public float fps;
        public float frameTime;
        public long totalMemory;
        public long usedMemory;
        public int drawCalls;
        public int triangles;
        public int vertices;
        public int setPassCalls;
        public int batches;
        public int dynamicBatches;
        public int staticBatches;
        public int instances;
    }

    [System.Serializable]
    public class PerformanceIssue
    {
        public IssueSeverity severity;
        public string category;
        public string message;
        public string suggestion;
        public string objectPath;
    }

    public enum IssueSeverity { Info, Warning, Error, Critical }

    // State
    private List<PerformanceSnapshot> _snapshots = new List<PerformanceSnapshot>();
    private List<PerformanceIssue> _issues = new List<PerformanceIssue>();
    private const int MAX_SNAPSHOTS = 300;
    private bool _isRecording;
    private double _lastUpdateTime;
    private float _updateInterval = 0.1f;

    // Current stats
    private float _currentFPS;
    private float _averageFPS;
    private float _minFPS = float.MaxValue;
    private float _maxFPS;
    private long _peakMemory;

    // Thresholds
    private float _targetFPS = 60f;
    private float _warningFPSThreshold = 45f;
    private float _criticalFPSThreshold = 30f;
    private int _maxDrawCallsWarning = 200;
    private int _maxTrianglesWarning = 500000;
    private long _maxMemoryWarning = 1024 * 1024 * 1024; // 1GB

    // UI
    private Vector2 _scrollPos;
    private int _currentTab;
    private readonly string[] _tabNames = { "Overview", "Timeline", "Analysis", "Issues", "Settings" };
    private bool _showDetails = true;

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (!_isRecording || !Application.isPlaying) return;

        double currentTime = EditorApplication.timeSinceStartup;
        if (currentTime - _lastUpdateTime < _updateInterval) return;

        _lastUpdateTime = currentTime;
        CaptureSnapshot();
        Repaint();
    }

    private void CaptureSnapshot()
    {
        var snapshot = new PerformanceSnapshot
        {
            timestamp = Time.realtimeSinceStartup,
            fps = 1f / Time.unscaledDeltaTime,
            frameTime = Time.unscaledDeltaTime * 1000f,
            totalMemory = Profiler.GetTotalReservedMemoryLong(),
            usedMemory = Profiler.GetTotalAllocatedMemoryLong()
        };

        // Get rendering stats if available
#if UNITY_EDITOR
        snapshot.drawCalls = UnityStats.drawCalls;
        snapshot.triangles = UnityStats.triangles;
        snapshot.vertices = UnityStats.vertices;
        snapshot.setPassCalls = UnityStats.setPassCalls;
        snapshot.batches = UnityStats.batches;
        snapshot.dynamicBatches = UnityStats.dynamicBatches;
        snapshot.staticBatches = UnityStats.staticBatches;
#endif

        _snapshots.Add(snapshot);

        // Keep history limited
        while (_snapshots.Count > MAX_SNAPSHOTS)
            _snapshots.RemoveAt(0);

        // Update stats
        _currentFPS = snapshot.fps;
        _minFPS = Mathf.Min(_minFPS, snapshot.fps);
        _maxFPS = Mathf.Max(_maxFPS, snapshot.fps);
        _averageFPS = _snapshots.Average(s => s.fps);
        _peakMemory = Mathf.Max(_peakMemory, snapshot.usedMemory);
    }

    private void OnGUI()
    {
        DrawToolbar();

        _currentTab = GUILayout.Toolbar(_currentTab, _tabNames, GUILayout.Height(30));

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        switch (_currentTab)
        {
            case 0: DrawOverviewTab(); break;
            case 1: DrawTimelineTab(); break;
            case 2: DrawAnalysisTab(); break;
            case 3: DrawIssuesTab(); break;
            case 4: DrawSettingsTab(); break;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUI.color = _isRecording ? Color.red : Color.white;
        if (GUILayout.Button(_isRecording ? "⏹ Stop" : "⏺ Record", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            _isRecording = !_isRecording;
            if (_isRecording)
            {
                _lastUpdateTime = EditorApplication.timeSinceStartup;
            }
        }
        GUI.color = Color.white;

        if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            ClearData();
        }

        if (GUILayout.Button("Analyze", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            AnalyzeScene();
        }

        GUILayout.Space(10);

        if (!Application.isPlaying)
        {
            GUI.color = Color.yellow;
            EditorGUILayout.LabelField("⚠ Enter Play Mode for live profiling", GUILayout.Width(220));
            GUI.color = Color.white;
        }

        GUILayout.FlexibleSpace();

        EditorGUILayout.LabelField($"Samples: {_snapshots.Count}", GUILayout.Width(100));

        EditorGUILayout.EndHorizontal();
    }

    #region Overview Tab

    private void DrawOverviewTab()
    {
        // Live stats
        EditorGUILayout.LabelField("Live Performance", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        // FPS Box
        DrawStatBox("FPS", _currentFPS.ToString("F1"), GetFPSColor(_currentFPS), 100);
        DrawStatBox("Avg FPS", _averageFPS.ToString("F1"), GetFPSColor(_averageFPS), 100);
        DrawStatBox("Min FPS", _minFPS == float.MaxValue ? "--" : _minFPS.ToString("F1"), GetFPSColor(_minFPS), 100);
        DrawStatBox("Max FPS", _maxFPS.ToString("F1"), GetFPSColor(_maxFPS), 100);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Memory
        EditorGUILayout.LabelField("Memory", EditorStyles.boldLabel);

        var lastSnapshot = _snapshots.LastOrDefault() ?? new PerformanceSnapshot();

        EditorGUILayout.BeginHorizontal();
        DrawStatBox("Used", FormatBytes(lastSnapshot.usedMemory), GetMemoryColor(lastSnapshot.usedMemory), 120);
        DrawStatBox("Reserved", FormatBytes(lastSnapshot.totalMemory), Color.white, 120);
        DrawStatBox("Peak", FormatBytes(_peakMemory), GetMemoryColor(_peakMemory), 120);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Rendering
        EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        DrawStatBox("Draw Calls", lastSnapshot.drawCalls.ToString(), GetDrawCallColor(lastSnapshot.drawCalls), 100);
        DrawStatBox("Batches", lastSnapshot.batches.ToString(), Color.white, 100);
        DrawStatBox("Triangles", FormatNumber(lastSnapshot.triangles), GetTriangleColor(lastSnapshot.triangles), 100);
        DrawStatBox("SetPass", lastSnapshot.setPassCalls.ToString(), Color.white, 100);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        DrawStatBox("Static Batches", lastSnapshot.staticBatches.ToString(), Color.cyan, 120);
        DrawStatBox("Dynamic Batches", lastSnapshot.dynamicBatches.ToString(), Color.yellow, 120);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(20);

        // Quick analysis
        EditorGUILayout.LabelField("Quick Analysis", EditorStyles.boldLabel);

        if (_averageFPS < _criticalFPSThreshold)
        {
            EditorGUILayout.HelpBox($"Critical: Average FPS ({_averageFPS:F1}) is below {_criticalFPSThreshold}. Performance issues detected.", MessageType.Error);
        }
        else if (_averageFPS < _warningFPSThreshold)
        {
            EditorGUILayout.HelpBox($"Warning: Average FPS ({_averageFPS:F1}) is below target ({_targetFPS}). Consider optimization.", MessageType.Warning);
        }
        else if (_averageFPS >= _targetFPS)
        {
            EditorGUILayout.HelpBox($"Good: Average FPS ({_averageFPS:F1}) meets target ({_targetFPS}).", MessageType.Info);
        }

        if (lastSnapshot.drawCalls > _maxDrawCallsWarning)
        {
            EditorGUILayout.HelpBox($"High draw call count ({lastSnapshot.drawCalls}). Consider batching or reducing objects.", MessageType.Warning);
        }

        if (lastSnapshot.triangles > _maxTrianglesWarning)
        {
            EditorGUILayout.HelpBox($"High triangle count ({FormatNumber(lastSnapshot.triangles)}). Consider LODs or mesh optimization.", MessageType.Warning);
        }
    }

    private void DrawStatBox(string label, string value, Color valueColor, float width)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(width));
        EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
        GUI.color = valueColor;
        EditorGUILayout.LabelField(value, new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 });
        GUI.color = Color.white;
        EditorGUILayout.EndVertical();
    }

    private Color GetFPSColor(float fps)
    {
        if (fps >= _targetFPS) return Color.green;
        if (fps >= _warningFPSThreshold) return Color.yellow;
        if (fps >= _criticalFPSThreshold) return new Color(1f, 0.5f, 0f);
        return Color.red;
    }

    private Color GetMemoryColor(long bytes)
    {
        if (bytes > _maxMemoryWarning) return Color.red;
        if (bytes > _maxMemoryWarning * 0.8f) return Color.yellow;
        return Color.white;
    }

    private Color GetDrawCallColor(int drawCalls)
    {
        if (drawCalls > _maxDrawCallsWarning * 1.5f) return Color.red;
        if (drawCalls > _maxDrawCallsWarning) return Color.yellow;
        return Color.green;
    }

    private Color GetTriangleColor(int triangles)
    {
        if (triangles > _maxTrianglesWarning * 1.5f) return Color.red;
        if (triangles > _maxTrianglesWarning) return Color.yellow;
        return Color.green;
    }

    #endregion

    #region Timeline Tab

    private void DrawTimelineTab()
    {
        EditorGUILayout.LabelField("Performance Timeline", EditorStyles.boldLabel);

        if (_snapshots.Count < 2)
        {
            EditorGUILayout.HelpBox("Start recording in Play Mode to see timeline data.", MessageType.Info);
            return;
        }

        // FPS Graph
        DrawGraph("FPS", _snapshots.Select(s => s.fps).ToList(), 0, 120, Color.green, 150);

        EditorGUILayout.Space(10);

        // Frame Time Graph
        DrawGraph("Frame Time (ms)", _snapshots.Select(s => s.frameTime).ToList(), 0, 50, Color.cyan, 100);

        EditorGUILayout.Space(10);

        // Memory Graph
        DrawGraph("Memory (MB)", _snapshots.Select(s => s.usedMemory / (1024f * 1024f)).ToList(), 0, 1024, Color.yellow, 100);

        EditorGUILayout.Space(10);

        // Draw Calls Graph
        DrawGraph("Draw Calls", _snapshots.Select(s => (float)s.drawCalls).ToList(), 0, 500, Color.magenta, 80);
    }

    private void DrawGraph(string label, List<float> values, float min, float max, Color color, float height)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

        Rect graphRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
            GUILayout.ExpandWidth(true), GUILayout.Height(height));

        EditorGUI.DrawRect(graphRect, new Color(0.15f, 0.15f, 0.15f));

        if (values.Count < 2) return;

        // Calculate actual min/max from data
        float dataMin = values.Min();
        float dataMax = values.Max();
        min = Mathf.Min(min, dataMin);
        max = Mathf.Max(max, dataMax * 1.1f);

        // Draw grid lines
        Handles.BeginGUI();
        Handles.color = new Color(0.3f, 0.3f, 0.3f);
        for (int i = 0; i <= 4; i++)
        {
            float y = graphRect.y + graphRect.height * (i / 4f);
            Handles.DrawLine(new Vector3(graphRect.x, y), new Vector3(graphRect.x + graphRect.width, y));

            float value = Mathf.Lerp(max, min, i / 4f);
            GUI.Label(new Rect(graphRect.x + 5, y - 8, 50, 16), value.ToString("F0"), EditorStyles.miniLabel);
        }

        // Draw graph line
        Handles.color = color;
        float stepX = graphRect.width / (values.Count - 1);

        for (int i = 1; i < values.Count; i++)
        {
            float x1 = graphRect.x + (i - 1) * stepX;
            float x2 = graphRect.x + i * stepX;
            float y1 = graphRect.y + graphRect.height * (1 - (values[i - 1] - min) / (max - min));
            float y2 = graphRect.y + graphRect.height * (1 - (values[i] - min) / (max - min));

            y1 = Mathf.Clamp(y1, graphRect.y, graphRect.y + graphRect.height);
            y2 = Mathf.Clamp(y2, graphRect.y, graphRect.y + graphRect.height);

            Handles.DrawLine(new Vector3(x1, y1), new Vector3(x2, y2));
        }

        Handles.EndGUI();

        // Current value
        EditorGUILayout.LabelField($"Current: {values.Last():F2} | Avg: {values.Average():F2} | Min: {dataMin:F2} | Max: {dataMax:F2}");
    }

    #endregion

    #region Analysis Tab

    private void DrawAnalysisTab()
    {
        EditorGUILayout.LabelField("Scene Analysis", EditorStyles.boldLabel);

        if (GUILayout.Button("Analyze Current Scene", GUILayout.Height(30)))
        {
            AnalyzeScene();
        }

        EditorGUILayout.Space(20);

        // Object counts
        EditorGUILayout.LabelField("Object Statistics", EditorStyles.boldLabel);

        int totalObjects = Object.FindObjectsOfType<GameObject>().Length;
        int activeObjects = Object.FindObjectsOfType<GameObject>().Count(g => g.activeInHierarchy);
        int meshRenderers = Object.FindObjectsOfType<MeshRenderer>().Length;
        int skinnedMeshes = Object.FindObjectsOfType<SkinnedMeshRenderer>().Length;
        int lights = Object.FindObjectsOfType<Light>().Length;
        int particleSystems = Object.FindObjectsOfType<ParticleSystem>().Length;
        int audioSources = Object.FindObjectsOfType<AudioSource>().Length;
        int rigidBodies = Object.FindObjectsOfType<Rigidbody>().Length;
        int colliders = Object.FindObjectsOfType<Collider>().Length;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Total GameObjects: {totalObjects} ({activeObjects} active)");
        EditorGUILayout.LabelField($"Mesh Renderers: {meshRenderers}");
        EditorGUILayout.LabelField($"Skinned Meshes: {skinnedMeshes}");
        EditorGUILayout.LabelField($"Lights: {lights}");
        EditorGUILayout.LabelField($"Particle Systems: {particleSystems}");
        EditorGUILayout.LabelField($"Audio Sources: {audioSources}");
        EditorGUILayout.LabelField($"Rigidbodies: {rigidBodies}");
        EditorGUILayout.LabelField($"Colliders: {colliders}");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // Material analysis
        EditorGUILayout.LabelField("Material Analysis", EditorStyles.boldLabel);

        var renderers = Object.FindObjectsOfType<Renderer>();
        var materials = new HashSet<Material>();
        var shaders = new HashSet<Shader>();

        foreach (var r in renderers)
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m != null)
                {
                    materials.Add(m);
                    if (m.shader != null)
                        shaders.Add(m.shader);
                }
            }
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Unique Materials: {materials.Count}");
        EditorGUILayout.LabelField($"Unique Shaders: {shaders.Count}");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // Texture memory estimate
        EditorGUILayout.LabelField("Texture Analysis", EditorStyles.boldLabel);

        var textures = Resources.FindObjectsOfTypeAll<Texture2D>();
        long totalTextureMemory = 0;
        foreach (var tex in textures)
        {
            totalTextureMemory += Profiler.GetRuntimeMemorySizeLong(tex);
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Loaded Textures: {textures.Length}");
        EditorGUILayout.LabelField($"Estimated Texture Memory: {FormatBytes(totalTextureMemory)}");
        EditorGUILayout.EndVertical();
    }

    private void AnalyzeScene()
    {
        _issues.Clear();

        // Check for realtime lights
        Light[] lights = Object.FindObjectsOfType<Light>();
        int realtimeLights = lights.Count(l => l.lightmapBakeType != LightmapBakeType.Baked);
        if (realtimeLights > 4)
        {
            _issues.Add(new PerformanceIssue
            {
                severity = IssueSeverity.Warning,
                category = "Lighting",
                message = $"{realtimeLights} realtime lights in scene",
                suggestion = "Consider baking lights or reducing realtime light count"
            });
        }

        // Check for unoptimized meshes
        MeshFilter[] meshFilters = Object.FindObjectsOfType<MeshFilter>();
        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh != null && mf.sharedMesh.vertexCount > 50000)
            {
                _issues.Add(new PerformanceIssue
                {
                    severity = IssueSeverity.Warning,
                    category = "Mesh",
                    message = $"High-poly mesh: {mf.sharedMesh.name} ({mf.sharedMesh.vertexCount} verts)",
                    objectPath = GetGameObjectPath(mf.gameObject),
                    suggestion = "Consider using LODs or reducing polygon count"
                });
            }
        }

        // Check for missing LODs on large objects
        MeshRenderer[] renderers = Object.FindObjectsOfType<MeshRenderer>();
        foreach (var r in renderers)
        {
            MeshFilter mf = r.GetComponent<MeshFilter>();
            LODGroup lod = r.GetComponentInParent<LODGroup>();

            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.vertexCount > 5000 && lod == null)
            {
                _issues.Add(new PerformanceIssue
                {
                    severity = IssueSeverity.Info,
                    category = "LOD",
                    message = $"No LOD Group on object with {mf.sharedMesh.vertexCount} vertices",
                    objectPath = GetGameObjectPath(r.gameObject),
                    suggestion = "Add LOD Group for better distance optimization"
                });
            }
        }

        // Check for expensive particle systems
        ParticleSystem[] particles = Object.FindObjectsOfType<ParticleSystem>();
        foreach (var ps in particles)
        {
            var main = ps.main;
            if (main.maxParticles > 1000)
            {
                _issues.Add(new PerformanceIssue
                {
                    severity = IssueSeverity.Warning,
                    category = "Particles",
                    message = $"High particle count: {main.maxParticles}",
                    objectPath = GetGameObjectPath(ps.gameObject),
                    suggestion = "Reduce max particles or optimize particle system"
                });
            }
        }

        // Check for complex colliders
        MeshCollider[] meshColliders = Object.FindObjectsOfType<MeshCollider>();
        foreach (var mc in meshColliders)
        {
            if (mc.sharedMesh != null && mc.sharedMesh.vertexCount > 1000 && !mc.convex)
            {
                _issues.Add(new PerformanceIssue
                {
                    severity = IssueSeverity.Warning,
                    category = "Physics",
                    message = $"Complex non-convex mesh collider: {mc.sharedMesh.vertexCount} verts",
                    objectPath = GetGameObjectPath(mc.gameObject),
                    suggestion = "Use convex collider or primitive colliders"
                });
            }
        }

        _currentTab = 3; // Switch to issues tab
        Debug.Log($"Analysis complete: {_issues.Count} issues found");
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

    #endregion

    #region Issues Tab

    private void DrawIssuesTab()
    {
        EditorGUILayout.LabelField("Performance Issues", EditorStyles.boldLabel);

        if (_issues.Count == 0)
        {
            EditorGUILayout.HelpBox("No issues found. Run 'Analyze' to scan the scene.", MessageType.Info);
            return;
        }

        // Summary
        int critical = _issues.Count(i => i.severity == IssueSeverity.Critical);
        int errors = _issues.Count(i => i.severity == IssueSeverity.Error);
        int warnings = _issues.Count(i => i.severity == IssueSeverity.Warning);
        int infos = _issues.Count(i => i.severity == IssueSeverity.Info);

        EditorGUILayout.BeginHorizontal();
        GUI.color = Color.red;
        EditorGUILayout.LabelField($"Critical: {critical}", GUILayout.Width(80));
        GUI.color = new Color(1f, 0.5f, 0f);
        EditorGUILayout.LabelField($"Errors: {errors}", GUILayout.Width(80));
        GUI.color = Color.yellow;
        EditorGUILayout.LabelField($"Warnings: {warnings}", GUILayout.Width(100));
        GUI.color = Color.cyan;
        EditorGUILayout.LabelField($"Info: {infos}", GUILayout.Width(70));
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Issue list
        foreach (var issue in _issues.OrderByDescending(i => i.severity))
        {
            DrawIssueItem(issue);
        }
    }

    private void DrawIssueItem(PerformanceIssue issue)
    {
        Color color = issue.severity switch
        {
            IssueSeverity.Critical => Color.red,
            IssueSeverity.Error => new Color(1f, 0.5f, 0f),
            IssueSeverity.Warning => Color.yellow,
            _ => Color.cyan
        };

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

    #endregion

    #region Settings Tab

    private void DrawSettingsTab()
    {
        EditorGUILayout.LabelField("Profiler Settings", EditorStyles.boldLabel);

        _updateInterval = EditorGUILayout.Slider("Sample Interval (s)", _updateInterval, 0.016f, 1f);

        EditorGUILayout.Space(20);

        EditorGUILayout.LabelField("Thresholds", EditorStyles.boldLabel);

        _targetFPS = EditorGUILayout.FloatField("Target FPS", _targetFPS);
        _warningFPSThreshold = EditorGUILayout.FloatField("Warning FPS", _warningFPSThreshold);
        _criticalFPSThreshold = EditorGUILayout.FloatField("Critical FPS", _criticalFPSThreshold);

        EditorGUILayout.Space(10);

        _maxDrawCallsWarning = EditorGUILayout.IntField("Max Draw Calls Warning", _maxDrawCallsWarning);
        _maxTrianglesWarning = EditorGUILayout.IntField("Max Triangles Warning", _maxTrianglesWarning);
        _maxMemoryWarning = EditorGUILayout.LongField("Max Memory Warning (bytes)", _maxMemoryWarning);

        EditorGUILayout.Space(20);

        EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Mobile"))
        {
            _targetFPS = 30f;
            _warningFPSThreshold = 25f;
            _criticalFPSThreshold = 20f;
            _maxDrawCallsWarning = 100;
            _maxTrianglesWarning = 100000;
            _maxMemoryWarning = 512 * 1024 * 1024;
        }
        if (GUILayout.Button("PC Standard"))
        {
            _targetFPS = 60f;
            _warningFPSThreshold = 45f;
            _criticalFPSThreshold = 30f;
            _maxDrawCallsWarning = 200;
            _maxTrianglesWarning = 500000;
            _maxMemoryWarning = 1024 * 1024 * 1024;
        }
        if (GUILayout.Button("PC High-End"))
        {
            _targetFPS = 144f;
            _warningFPSThreshold = 100f;
            _criticalFPSThreshold = 60f;
            _maxDrawCallsWarning = 500;
            _maxTrianglesWarning = 2000000;
            _maxMemoryWarning = 4L * 1024 * 1024 * 1024;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(20);

        if (GUILayout.Button("Export Report", GUILayout.Height(30)))
        {
            ExportReport();
        }
    }

    #endregion

    #region Utilities

    private void ClearData()
    {
        _snapshots.Clear();
        _issues.Clear();
        _minFPS = float.MaxValue;
        _maxFPS = 0;
        _averageFPS = 0;
        _peakMemory = 0;
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:F2} {sizes[order]}";
    }

    private string FormatNumber(int number)
    {
        if (number >= 1000000)
            return $"{number / 1000000f:F1}M";
        if (number >= 1000)
            return $"{number / 1000f:F1}K";
        return number.ToString();
    }

    private void ExportReport()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine("=== Performance Report ===");
        sb.AppendLine($"Generated: {System.DateTime.Now}");
        sb.AppendLine();

        sb.AppendLine("== Summary ==");
        sb.AppendLine($"Average FPS: {_averageFPS:F1}");
        sb.AppendLine($"Min FPS: {_minFPS:F1}");
        sb.AppendLine($"Max FPS: {_maxFPS:F1}");
        sb.AppendLine($"Peak Memory: {FormatBytes(_peakMemory)}");
        sb.AppendLine();

        sb.AppendLine("== Issues ==");
        foreach (var issue in _issues)
        {
            sb.AppendLine($"[{issue.severity}] [{issue.category}] {issue.message}");
            if (!string.IsNullOrEmpty(issue.suggestion))
                sb.AppendLine($"  Suggestion: {issue.suggestion}");
        }

        string path = EditorUtility.SaveFilePanel("Export Report", "", "PerformanceReport", "txt");
        if (!string.IsNullOrEmpty(path))
        {
            System.IO.File.WriteAllText(path, sb.ToString());
            Debug.Log($"Report exported to: {path}");
        }
    }

    #endregion
}
