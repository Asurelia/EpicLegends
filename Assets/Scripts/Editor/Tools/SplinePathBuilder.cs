using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Constructeur de routes/chemins avec splines, banking auto et navmesh carve.
/// Menu: EpicLegends > Tools > Spline Path Builder
/// </summary>
public class SplinePathBuilder : EditorWindow
{
    #region Types

    [System.Serializable]
    public class SplinePath
    {
        public string name = "New Path";
        public PathType type = PathType.Road;
        public List<SplinePoint> points = new List<SplinePoint>();
        public bool isClosed = false;

        // Appearance
        public float width = 4f;
        public Material material;
        public Color previewColor = Color.white;

        // Mesh settings
        public int segmentsPerCurve = 10;
        public float uvTiling = 1f;
        public bool generateCollider = true;
        public bool carveNavmesh = true;

        // Banking
        public bool autoBanking = true;
        public float maxBankAngle = 15f;
        public float bankingSmoothing = 0.5f;

        // Terrain
        public bool conformToTerrain = true;
        public float terrainOffset = 0.05f;
        public bool flattenTerrain = false;
        public float flattenWidth = 1.2f;

        // Decorations
        public bool addBorders = false;
        public float borderHeight = 0.2f;
        public float borderWidth = 0.3f;

        // Runtime
        public GameObject generatedMesh;
        public bool isExpanded = true;
    }

    [System.Serializable]
    public class SplinePoint
    {
        public Vector3 position;
        public Vector3 tangentIn;
        public Vector3 tangentOut;
        public float width = 1f; // Width multiplier
        public float banking = 0f; // Manual banking override
        public bool autoTangents = true;
    }

    public enum PathType
    {
        Road,
        Trail,
        River,
        Bridge,
        Railway,
        Fence,
        Wall
    }

    #endregion

    #region State

    private List<SplinePath> _paths = new List<SplinePath>();
    private int _selectedPathIndex = -1;
    private int _selectedPointIndex = -1;

    private bool _editMode = false;
    private bool _addPointMode = false;
    private Tool _previousTool;

    private Vector2 _scrollPos;
    private Vector2 _pathListScroll;

    // Preview
    private bool _showPreview = true;
    private int _previewResolution = 50;

    // Terrain reference
    private Terrain _terrain;

    #endregion

    [MenuItem("EpicLegends/Tools/Spline Path Builder")]
    public static void ShowWindow()
    {
        var window = GetWindow<SplinePathBuilder>("Path Builder");
        window.minSize = new Vector2(400, 600);
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        _terrain = FindObjectOfType<Terrain>();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;

        if (_editMode)
        {
            Tools.current = _previousTool;
        }
    }

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        DrawHeader();
        DrawPathList();

        if (_selectedPathIndex >= 0 && _selectedPathIndex < _paths.Count)
        {
            DrawPathEditor(_paths[_selectedPathIndex]);
        }

        DrawActions();

        EditorGUILayout.EndScrollView();
    }

    #region GUI Sections

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Spline Path Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Create roads, trails, rivers, and other path-based geometry using splines. " +
            "Supports auto-banking, terrain conforming, and navmesh carving.",
            MessageType.Info
        );

        // Edit mode toggle
        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();
        _editMode = GUILayout.Toggle(_editMode, "Edit Mode", "Button", GUILayout.Height(25));
        if (EditorGUI.EndChangeCheck())
        {
            if (_editMode)
            {
                _previousTool = Tools.current;
                Tools.current = Tool.None;
            }
            else
            {
                Tools.current = _previousTool;
                _addPointMode = false;
            }
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = _addPointMode ? Color.green : Color.white;
        if (GUILayout.Toggle(_addPointMode, "+ Add Points", "Button", GUILayout.Height(25)))
        {
            _addPointMode = true;
            _editMode = true;
        }
        else if (_addPointMode)
        {
            _addPointMode = false;
        }
        GUI.backgroundColor = Color.white;

        _showPreview = GUILayout.Toggle(_showPreview, "Preview", "Button", GUILayout.Height(25));

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
    }

    private void DrawPathList()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Paths", EditorStyles.boldLabel);

        if (GUILayout.Button("+ New Path", GUILayout.Width(80)))
        {
            CreateNewPath();
        }

        EditorGUILayout.EndHorizontal();

        _pathListScroll = EditorGUILayout.BeginScrollView(_pathListScroll, GUILayout.Height(120));

        for (int i = 0; i < _paths.Count; i++)
        {
            DrawPathListItem(i);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);
    }

    private void DrawPathListItem(int index)
    {
        var path = _paths[index];
        bool isSelected = index == _selectedPathIndex;

        EditorGUILayout.BeginHorizontal(isSelected ? EditorStyles.helpBox : GUIStyle.none);

        // Color indicator
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(8, 20, GUILayout.Width(8)), path.previewColor);

        // Type icon
        string icon = GetPathIcon(path.type);
        GUILayout.Label(icon, GUILayout.Width(20));

        // Name
        if (GUILayout.Button($"{path.name} ({path.points.Count} pts)", EditorStyles.label))
        {
            _selectedPathIndex = index;
            _selectedPointIndex = -1;
        }

        // Visibility toggle
        bool hasGenerated = path.generatedMesh != null;
        GUILayout.Label(hasGenerated ? "✓" : "○", GUILayout.Width(20));

        // Delete
        if (GUILayout.Button("X", GUILayout.Width(20)))
        {
            if (path.generatedMesh != null)
            {
                DestroyImmediate(path.generatedMesh);
            }
            _paths.RemoveAt(index);
            if (_selectedPathIndex >= _paths.Count)
            {
                _selectedPathIndex = _paths.Count - 1;
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawPathEditor(SplinePath path)
    {
        EditorGUILayout.LabelField("Path Settings", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Basic
        path.name = EditorGUILayout.TextField("Name", path.name);
        path.type = (PathType)EditorGUILayout.EnumPopup("Type", path.type);
        path.previewColor = EditorGUILayout.ColorField("Preview Color", path.previewColor);
        path.isClosed = EditorGUILayout.Toggle("Closed Loop", path.isClosed);

        EditorGUILayout.Space(5);

        // Dimensions
        EditorGUILayout.LabelField("Dimensions", EditorStyles.miniBoldLabel);
        path.width = EditorGUILayout.Slider("Width", path.width, 0.5f, 20f);
        path.segmentsPerCurve = EditorGUILayout.IntSlider("Segments/Curve", path.segmentsPerCurve, 2, 30);

        EditorGUILayout.Space(5);

        // Material
        EditorGUILayout.LabelField("Appearance", EditorStyles.miniBoldLabel);
        path.material = (Material)EditorGUILayout.ObjectField("Material", path.material, typeof(Material), false);
        path.uvTiling = EditorGUILayout.Slider("UV Tiling", path.uvTiling, 0.1f, 10f);

        EditorGUILayout.Space(5);

        // Banking
        EditorGUILayout.LabelField("Banking", EditorStyles.miniBoldLabel);
        path.autoBanking = EditorGUILayout.Toggle("Auto Banking", path.autoBanking);
        if (path.autoBanking)
        {
            path.maxBankAngle = EditorGUILayout.Slider("Max Bank Angle", path.maxBankAngle, 0f, 45f);
            path.bankingSmoothing = EditorGUILayout.Slider("Smoothing", path.bankingSmoothing, 0f, 1f);
        }

        EditorGUILayout.Space(5);

        // Terrain
        EditorGUILayout.LabelField("Terrain", EditorStyles.miniBoldLabel);
        path.conformToTerrain = EditorGUILayout.Toggle("Conform to Terrain", path.conformToTerrain);
        if (path.conformToTerrain)
        {
            path.terrainOffset = EditorGUILayout.Slider("Height Offset", path.terrainOffset, 0f, 1f);
        }
        path.flattenTerrain = EditorGUILayout.Toggle("Flatten Terrain", path.flattenTerrain);
        if (path.flattenTerrain)
        {
            path.flattenWidth = EditorGUILayout.Slider("Flatten Width", path.flattenWidth, 1f, 3f);
        }

        EditorGUILayout.Space(5);

        // Collider & Navigation
        EditorGUILayout.LabelField("Physics & Navigation", EditorStyles.miniBoldLabel);
        path.generateCollider = EditorGUILayout.Toggle("Generate Collider", path.generateCollider);
        path.carveNavmesh = EditorGUILayout.Toggle("Carve NavMesh", path.carveNavmesh);

        EditorGUILayout.Space(5);

        // Borders
        EditorGUILayout.LabelField("Decorations", EditorStyles.miniBoldLabel);
        path.addBorders = EditorGUILayout.Toggle("Add Borders", path.addBorders);
        if (path.addBorders)
        {
            path.borderHeight = EditorGUILayout.Slider("Border Height", path.borderHeight, 0.1f, 1f);
            path.borderWidth = EditorGUILayout.Slider("Border Width", path.borderWidth, 0.1f, 1f);
        }

        EditorGUILayout.Space(5);

        // Point list
        DrawPointList(path);

        EditorGUILayout.EndVertical();
    }

    private void DrawPointList(SplinePath path)
    {
        EditorGUILayout.LabelField($"Control Points ({path.points.Count})", EditorStyles.miniBoldLabel);

        if (path.points.Count == 0)
        {
            EditorGUILayout.HelpBox("No points. Enable 'Add Points' mode and click in Scene View.", MessageType.Info);
        }

        for (int i = 0; i < Mathf.Min(path.points.Count, 8); i++)
        {
            EditorGUILayout.BeginHorizontal();

            bool isSelected = i == _selectedPointIndex;
            GUI.backgroundColor = isSelected ? Color.cyan : Color.white;

            if (GUILayout.Button($"Point {i}", GUILayout.Width(60)))
            {
                _selectedPointIndex = i;
                SceneView.lastActiveSceneView?.LookAt(path.points[i].position);
            }

            GUI.backgroundColor = Color.white;

            path.points[i].width = EditorGUILayout.Slider(path.points[i].width, 0.5f, 2f, GUILayout.Width(80));

            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                path.points.RemoveAt(i);
                if (_selectedPointIndex >= path.points.Count)
                {
                    _selectedPointIndex = path.points.Count - 1;
                }
                break;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (path.points.Count > 8)
        {
            EditorGUILayout.LabelField($"... and {path.points.Count - 8} more points", EditorStyles.centeredGreyMiniLabel);
        }
    }

    private void DrawActions()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.5f, 0.8f, 0.5f);
        if (GUILayout.Button("Generate Mesh", GUILayout.Height(30)))
        {
            GenerateSelectedPath();
        }
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("Generate All", GUILayout.Height(30)))
        {
            GenerateAllPaths();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Smooth Path"))
        {
            SmoothSelectedPath();
        }

        if (GUILayout.Button("Reverse Direction"))
        {
            ReverseSelectedPath();
        }

        if (GUILayout.Button("Clear Mesh"))
        {
            ClearSelectedPathMesh();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Export as Prefab"))
        {
            ExportAsPrefab();
        }
    }

    #endregion

    #region Scene GUI

    private void OnSceneGUI(SceneView sceneView)
    {
        if (_paths.Count == 0) return;

        // Draw all paths
        foreach (var path in _paths)
        {
            if (_showPreview)
            {
                DrawPathPreview(path);
            }
        }

        // Edit selected path
        if (_editMode && _selectedPathIndex >= 0 && _selectedPathIndex < _paths.Count)
        {
            var path = _paths[_selectedPathIndex];

            // Draw control points
            DrawControlPoints(path);

            // Handle adding new points
            if (_addPointMode)
            {
                HandleAddPoint(path);
            }

            // Handle point manipulation
            HandlePointManipulation(path);
        }
    }

    private void DrawPathPreview(SplinePath path)
    {
        if (path.points.Count < 2) return;

        List<Vector3> curvePoints = GetCurvePoints(path, _previewResolution);

        // Draw path line
        Handles.color = path.previewColor;

        for (int i = 0; i < curvePoints.Count - 1; i++)
        {
            Handles.DrawLine(curvePoints[i], curvePoints[i + 1]);
        }

        if (path.isClosed && curvePoints.Count > 0)
        {
            Handles.DrawLine(curvePoints[curvePoints.Count - 1], curvePoints[0]);
        }

        // Draw width preview
        Handles.color = new Color(path.previewColor.r, path.previewColor.g, path.previewColor.b, 0.3f);

        for (int i = 0; i < curvePoints.Count - 1; i++)
        {
            Vector3 forward = (curvePoints[i + 1] - curvePoints[i]).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            float t = (float)i / curvePoints.Count;
            float width = GetWidthAtT(path, t) * path.width * 0.5f;

            Vector3 left1 = curvePoints[i] - right * width;
            Vector3 right1 = curvePoints[i] + right * width;
            Vector3 left2 = curvePoints[i + 1] - right * width;
            Vector3 right2 = curvePoints[i + 1] + right * width;

            Handles.DrawAAConvexPolygon(left1, right1, right2, left2);
        }
    }

    private void DrawControlPoints(SplinePath path)
    {
        for (int i = 0; i < path.points.Count; i++)
        {
            var point = path.points[i];
            bool isSelected = i == _selectedPointIndex;

            // Main point
            Handles.color = isSelected ? Color.yellow : path.previewColor;
            float handleSize = HandleUtility.GetHandleSize(point.position) * 0.1f;

            if (Handles.Button(point.position, Quaternion.identity, handleSize, handleSize * 1.2f, Handles.SphereHandleCap))
            {
                _selectedPointIndex = i;
                Repaint();
            }

            // Point label
            Handles.Label(point.position + Vector3.up * 0.5f, $"{i}", EditorStyles.boldLabel);

            // Tangent handles for selected point
            if (isSelected && !point.autoTangents)
            {
                Handles.color = Color.blue;

                // Tangent in
                Vector3 tangentInWorld = point.position + point.tangentIn;
                Handles.DrawLine(point.position, tangentInWorld);
                EditorGUI.BeginChangeCheck();
                Vector3 newTangentIn = Handles.FreeMoveHandle(tangentInWorld, handleSize * 0.7f, Vector3.zero, Handles.CubeHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Move Tangent");
                    point.tangentIn = newTangentIn - point.position;
                }

                // Tangent out
                Handles.color = Color.red;
                Vector3 tangentOutWorld = point.position + point.tangentOut;
                Handles.DrawLine(point.position, tangentOutWorld);
                EditorGUI.BeginChangeCheck();
                Vector3 newTangentOut = Handles.FreeMoveHandle(tangentOutWorld, handleSize * 0.7f, Vector3.zero, Handles.CubeHandleCap);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Move Tangent");
                    point.tangentOut = newTangentOut - point.position;
                }
            }
        }
    }

    private void HandleAddPoint(SplinePath path)
    {
        Event e = Event.current;

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            Vector3 hitPoint;
            if (_terrain != null && Physics.Raycast(ray, out RaycastHit hit, 10000f))
            {
                hitPoint = hit.point;
            }
            else
            {
                // Place on a plane at y=0 or last point height
                float planeY = path.points.Count > 0 ? path.points[path.points.Count - 1].position.y : 0f;
                Plane plane = new Plane(Vector3.up, new Vector3(0, planeY, 0));
                if (plane.Raycast(ray, out float distance))
                {
                    hitPoint = ray.GetPoint(distance);
                }
                else
                {
                    return;
                }
            }

            // Add point
            var newPoint = new SplinePoint
            {
                position = hitPoint,
                autoTangents = true
            };

            path.points.Add(newPoint);
            _selectedPointIndex = path.points.Count - 1;

            // Auto-calculate tangents
            RecalculateAutoTangents(path);

            e.Use();
            Repaint();
        }

        // Show cursor hint
        Handles.BeginGUI();
        Vector2 mousePos = Event.current.mousePosition;
        GUI.Label(new Rect(mousePos.x + 20, mousePos.y, 200, 20), "Click to add point", EditorStyles.helpBox);
        Handles.EndGUI();
    }

    private void HandlePointManipulation(SplinePath path)
    {
        if (_selectedPointIndex < 0 || _selectedPointIndex >= path.points.Count) return;

        var point = path.points[_selectedPointIndex];

        EditorGUI.BeginChangeCheck();
        Vector3 newPos = Handles.PositionHandle(point.position, Quaternion.identity);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(this, "Move Path Point");
            point.position = newPos;

            // Conform to terrain if enabled
            var selectedPath = _paths[_selectedPathIndex];
            if (selectedPath.conformToTerrain && _terrain != null)
            {
                point.position.y = _terrain.SampleHeight(point.position) +
                    _terrain.transform.position.y + selectedPath.terrainOffset;
            }

            RecalculateAutoTangents(path);
        }
    }

    #endregion

    #region Spline Math

    private List<Vector3> GetCurvePoints(SplinePath path, int resolution)
    {
        var points = new List<Vector3>();

        if (path.points.Count < 2) return points;

        int segmentCount = path.isClosed ? path.points.Count : path.points.Count - 1;

        for (int seg = 0; seg < segmentCount; seg++)
        {
            int i0 = seg;
            int i1 = (seg + 1) % path.points.Count;

            var p0 = path.points[i0];
            var p1 = path.points[i1];

            for (int i = 0; i < resolution; i++)
            {
                float t = (float)i / resolution;
                Vector3 point = CubicBezier(
                    p0.position,
                    p0.position + p0.tangentOut,
                    p1.position + p1.tangentIn,
                    p1.position,
                    t
                );

                // Conform to terrain
                if (path.conformToTerrain && _terrain != null)
                {
                    point.y = _terrain.SampleHeight(point) + _terrain.transform.position.y + path.terrainOffset;
                }

                points.Add(point);
            }
        }

        // Add last point if not closed
        if (!path.isClosed && path.points.Count > 0)
        {
            points.Add(path.points[path.points.Count - 1].position);
        }

        return points;
    }

    private Vector3 CubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        return uuu * p0 + 3 * uu * t * p1 + 3 * u * tt * p2 + ttt * p3;
    }

    private Vector3 CubicBezierDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1 - t;
        return 3 * u * u * (p1 - p0) + 6 * u * t * (p2 - p1) + 3 * t * t * (p3 - p2);
    }

    private void RecalculateAutoTangents(SplinePath path)
    {
        for (int i = 0; i < path.points.Count; i++)
        {
            if (!path.points[i].autoTangents) continue;

            Vector3 prev = i > 0 ? path.points[i - 1].position :
                (path.isClosed ? path.points[path.points.Count - 1].position : path.points[i].position);

            Vector3 next = i < path.points.Count - 1 ? path.points[i + 1].position :
                (path.isClosed ? path.points[0].position : path.points[i].position);

            Vector3 direction = (next - prev).normalized;
            float distance = Vector3.Distance(prev, next) * 0.25f;

            path.points[i].tangentIn = -direction * distance;
            path.points[i].tangentOut = direction * distance;
        }
    }

    private float GetWidthAtT(SplinePath path, float t)
    {
        if (path.points.Count == 0) return 1f;

        float pointT = t * (path.points.Count - 1);
        int index = Mathf.FloorToInt(pointT);
        float frac = pointT - index;

        index = Mathf.Clamp(index, 0, path.points.Count - 1);
        int nextIndex = Mathf.Min(index + 1, path.points.Count - 1);

        return Mathf.Lerp(path.points[index].width, path.points[nextIndex].width, frac);
    }

    #endregion

    #region Mesh Generation

    private void GenerateSelectedPath()
    {
        if (_selectedPathIndex < 0 || _selectedPathIndex >= _paths.Count) return;

        GeneratePathMesh(_paths[_selectedPathIndex]);
    }

    private void GenerateAllPaths()
    {
        foreach (var path in _paths)
        {
            GeneratePathMesh(path);
        }
    }

    private void GeneratePathMesh(SplinePath path)
    {
        if (path.points.Count < 2)
        {
            Debug.LogWarning("[SplinePathBuilder] Need at least 2 points to generate mesh");
            return;
        }

        // Clean up existing
        if (path.generatedMesh != null)
        {
            DestroyImmediate(path.generatedMesh);
        }

        // Get curve points
        int totalSegments = path.segmentsPerCurve * (path.isClosed ? path.points.Count : path.points.Count - 1);
        List<Vector3> curvePoints = GetCurvePoints(path, path.segmentsPerCurve);

        if (curvePoints.Count < 2) return;

        // Generate mesh
        Mesh mesh = new Mesh();
        mesh.name = $"{path.name}_Mesh";

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        float totalLength = 0f;
        for (int i = 0; i < curvePoints.Count - 1; i++)
        {
            totalLength += Vector3.Distance(curvePoints[i], curvePoints[i + 1]);
        }

        float currentLength = 0f;

        for (int i = 0; i < curvePoints.Count; i++)
        {
            Vector3 pos = curvePoints[i];
            Vector3 forward;

            if (i < curvePoints.Count - 1)
            {
                forward = (curvePoints[i + 1] - pos).normalized;
            }
            else if (path.isClosed)
            {
                forward = (curvePoints[0] - pos).normalized;
            }
            else
            {
                forward = (pos - curvePoints[i - 1]).normalized;
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 up = Vector3.Cross(forward, right);

            // Calculate banking
            float banking = 0f;
            if (path.autoBanking && i > 0 && i < curvePoints.Count - 1)
            {
                Vector3 prevDir = (pos - curvePoints[i - 1]).normalized;
                Vector3 nextDir = (curvePoints[i + 1] - pos).normalized;
                float turnAngle = Vector3.SignedAngle(prevDir, nextDir, Vector3.up);
                banking = Mathf.Clamp(turnAngle * 0.5f, -path.maxBankAngle, path.maxBankAngle);
            }

            // Apply banking rotation
            Quaternion bankRotation = Quaternion.AngleAxis(banking, forward);
            right = bankRotation * right;

            float t = (float)i / curvePoints.Count;
            float width = GetWidthAtT(path, t) * path.width * 0.5f;

            // Add vertices
            vertices.Add(pos - right * width);
            vertices.Add(pos + right * width);

            // UVs
            float v = currentLength / totalLength * path.uvTiling;
            uvs.Add(new Vector2(0, v));
            uvs.Add(new Vector2(1, v));

            // Triangles
            if (i < curvePoints.Count - 1)
            {
                int baseIndex = i * 2;
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 1);

                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 3);
            }

            if (i < curvePoints.Count - 1)
            {
                currentLength += Vector3.Distance(pos, curvePoints[i + 1]);
            }
        }

        // Close the loop
        if (path.isClosed && curvePoints.Count > 2)
        {
            int lastIndex = (curvePoints.Count - 1) * 2;
            triangles.Add(lastIndex);
            triangles.Add(0);
            triangles.Add(lastIndex + 1);

            triangles.Add(lastIndex + 1);
            triangles.Add(0);
            triangles.Add(1);
        }

        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Create GameObject
        GameObject pathObj = new GameObject(path.name);
        pathObj.transform.position = Vector3.zero;

        MeshFilter mf = pathObj.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = pathObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = path.material != null ? path.material : GetDefaultMaterial(path.type);

        // Add collider
        if (path.generateCollider)
        {
            MeshCollider mc = pathObj.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
        }

        // NavMesh carving
        if (path.carveNavmesh)
        {
            var obstacle = pathObj.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
        }

        path.generatedMesh = pathObj;

        Undo.RegisterCreatedObjectUndo(pathObj, "Generate Path");

        Debug.Log($"[SplinePathBuilder] Generated mesh for '{path.name}' with {vertices.Count} vertices");
    }

    private Material GetDefaultMaterial(PathType type)
    {
        // Return a default material based on type
        switch (type)
        {
            case PathType.Road:
                return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
            case PathType.River:
                return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
            default:
                return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
        }
    }

    #endregion

    #region Actions

    private void CreateNewPath()
    {
        var path = new SplinePath
        {
            name = $"Path_{_paths.Count + 1}",
            previewColor = Random.ColorHSV(0f, 1f, 0.6f, 0.8f, 0.7f, 0.9f)
        };

        _paths.Add(path);
        _selectedPathIndex = _paths.Count - 1;
        _selectedPointIndex = -1;
    }

    private void SmoothSelectedPath()
    {
        if (_selectedPathIndex < 0 || _selectedPathIndex >= _paths.Count) return;

        var path = _paths[_selectedPathIndex];

        // Insert midpoints
        var newPoints = new List<SplinePoint>();

        for (int i = 0; i < path.points.Count; i++)
        {
            newPoints.Add(path.points[i]);

            int nextIndex = (i + 1) % path.points.Count;
            if (i < path.points.Count - 1 || path.isClosed)
            {
                Vector3 midPos = (path.points[i].position + path.points[nextIndex].position) * 0.5f;
                newPoints.Add(new SplinePoint
                {
                    position = midPos,
                    width = (path.points[i].width + path.points[nextIndex].width) * 0.5f,
                    autoTangents = true
                });
            }
        }

        path.points = newPoints;
        RecalculateAutoTangents(path);

        Debug.Log("[SplinePathBuilder] Path smoothed");
    }

    private void ReverseSelectedPath()
    {
        if (_selectedPathIndex < 0 || _selectedPathIndex >= _paths.Count) return;

        var path = _paths[_selectedPathIndex];
        path.points.Reverse();

        // Swap tangents
        foreach (var point in path.points)
        {
            var temp = point.tangentIn;
            point.tangentIn = -point.tangentOut;
            point.tangentOut = -temp;
        }

        Debug.Log("[SplinePathBuilder] Path direction reversed");
    }

    private void ClearSelectedPathMesh()
    {
        if (_selectedPathIndex < 0 || _selectedPathIndex >= _paths.Count) return;

        var path = _paths[_selectedPathIndex];
        if (path.generatedMesh != null)
        {
            Undo.DestroyObjectImmediate(path.generatedMesh);
            path.generatedMesh = null;
        }
    }

    private void ExportAsPrefab()
    {
        if (_selectedPathIndex < 0 || _selectedPathIndex >= _paths.Count) return;

        var path = _paths[_selectedPathIndex];
        if (path.generatedMesh == null)
        {
            EditorUtility.DisplayDialog("No Mesh", "Generate the mesh first before exporting.", "OK");
            return;
        }

        string savePath = EditorUtility.SaveFilePanelInProject(
            "Save Path Prefab",
            path.name,
            "prefab",
            "Save path as prefab"
        );

        if (string.IsNullOrEmpty(savePath)) return;

        // Save mesh asset
        string meshPath = savePath.Replace(".prefab", "_Mesh.asset");
        var mf = path.generatedMesh.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            AssetDatabase.CreateAsset(Instantiate(mf.sharedMesh), meshPath);
        }

        // Create prefab
        PrefabUtility.SaveAsPrefabAsset(path.generatedMesh, savePath);

        Debug.Log($"[SplinePathBuilder] Exported prefab to {savePath}");
    }

    #endregion

    #region Helpers

    private string GetPathIcon(PathType type)
    {
        switch (type)
        {
            case PathType.Road: return "🛣";
            case PathType.Trail: return "🥾";
            case PathType.River: return "🌊";
            case PathType.Bridge: return "🌉";
            case PathType.Railway: return "🚂";
            case PathType.Fence: return "🚧";
            case PathType.Wall: return "🧱";
            default: return "—";
        }
    }

    #endregion
}
