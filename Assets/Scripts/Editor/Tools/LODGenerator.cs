using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Generateur automatique de LOD (Level of Detail) pour les meshes.
/// Menu: EpicLegends > Tools > LOD Generator
/// </summary>
public class LODGenerator : EditorWindow
{
    #region Settings

    [System.Serializable]
    public class LODSettings
    {
        public float screenRelativeHeight = 0.5f;
        public float qualityPercentage = 50f;
        public bool generateCollider = false;
    }

    private List<LODSettings> _lodLevels = new List<LODSettings>
    {
        new LODSettings { screenRelativeHeight = 0.6f, qualityPercentage = 100f },
        new LODSettings { screenRelativeHeight = 0.3f, qualityPercentage = 50f },
        new LODSettings { screenRelativeHeight = 0.1f, qualityPercentage = 25f },
        new LODSettings { screenRelativeHeight = 0.01f, qualityPercentage = 10f }
    };

    private float _cullRatio = 0.01f;
    private bool _preserveBorders = true;
    private bool _preserveUVSeams = true;
    private bool _recalculateNormals = true;
    private float _normalAngle = 60f;

    #endregion

    #region State

    private List<GameObject> _selectedObjects = new List<GameObject>();
    private Vector2 _scrollPos;
    private bool _showAdvanced = false;
    private bool _previewMode = false;
    private int _previewLOD = 0;

    // Statistics
    private int _totalOriginalTris = 0;
    private int _totalReducedTris = 0;

    #endregion

    [MenuItem("EpicLegends/Tools/LOD Generator")]
    public static void ShowWindow()
    {
        var window = GetWindow<LODGenerator>("LOD Generator");
        window.minSize = new Vector2(400, 500);
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChanged;
        OnSelectionChanged();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
    }

    private void OnSelectionChanged()
    {
        _selectedObjects.Clear();
        _totalOriginalTris = 0;

        foreach (var obj in Selection.gameObjects)
        {
            var meshFilter = obj.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                _selectedObjects.Add(obj);
                _totalOriginalTris += meshFilter.sharedMesh.triangles.Length / 3;
            }

            // Also check children
            foreach (var childMf in obj.GetComponentsInChildren<MeshFilter>())
            {
                if (childMf.sharedMesh != null && !_selectedObjects.Contains(childMf.gameObject))
                {
                    _selectedObjects.Add(childMf.gameObject);
                    _totalOriginalTris += childMf.sharedMesh.triangles.Length / 3;
                }
            }
        }

        Repaint();
    }

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        DrawHeader();
        DrawSelection();
        DrawLODLevels();
        DrawAdvancedSettings();
        DrawPreview();
        DrawActions();
        DrawStatistics();

        EditorGUILayout.EndScrollView();
    }

    #region GUI Sections

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("LOD Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Generate Level of Detail meshes for selected objects. " +
            "LOD reduces polygon count based on distance to camera, improving performance.",
            MessageType.Info
        );
        EditorGUILayout.Space(10);
    }

    private void DrawSelection()
    {
        EditorGUILayout.LabelField("Selected Objects", EditorStyles.boldLabel);

        if (_selectedObjects.Count == 0)
        {
            EditorGUILayout.HelpBox("Select GameObjects with MeshFilter components in the Hierarchy.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            foreach (var obj in _selectedObjects.Take(10))
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.ObjectField(obj, typeof(GameObject), true);

                var mf = obj.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    int tris = mf.sharedMesh.triangles.Length / 3;
                    EditorGUILayout.LabelField($"{tris:N0} tris", GUILayout.Width(80));
                }

                EditorGUILayout.EndHorizontal();
            }

            if (_selectedObjects.Count > 10)
            {
                EditorGUILayout.LabelField($"... and {_selectedObjects.Count - 10} more", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.LabelField($"Total: {_totalOriginalTris:N0} triangles", EditorStyles.boldLabel);

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(10);
    }

    private void DrawLODLevels()
    {
        EditorGUILayout.LabelField("LOD Levels", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Visual LOD bar
        Rect barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(30));
        barRect.x += 5;
        barRect.width -= 10;

        float totalWidth = barRect.width;
        float xPos = barRect.x;

        for (int i = 0; i < _lodLevels.Count; i++)
        {
            float nextThreshold = i < _lodLevels.Count - 1 ? _lodLevels[i + 1].screenRelativeHeight : _cullRatio;
            float segmentWidth = (_lodLevels[i].screenRelativeHeight - nextThreshold) * totalWidth;

            Color color = Color.Lerp(Color.green, Color.red, (float)i / _lodLevels.Count);
            EditorGUI.DrawRect(new Rect(xPos, barRect.y, segmentWidth, barRect.height), color);

            // Label
            GUI.Label(new Rect(xPos + 2, barRect.y + 5, segmentWidth, 20),
                $"LOD{i} ({_lodLevels[i].qualityPercentage:F0}%)",
                EditorStyles.miniLabel);

            xPos += segmentWidth;
        }

        // Cull zone
        float cullWidth = _cullRatio * totalWidth;
        EditorGUI.DrawRect(new Rect(xPos, barRect.y, cullWidth, barRect.height), Color.gray);
        GUI.Label(new Rect(xPos + 2, barRect.y + 5, cullWidth, 20), "Cull", EditorStyles.miniLabel);

        EditorGUILayout.Space(5);

        // LOD level settings
        for (int i = 0; i < _lodLevels.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField($"LOD {i}", GUILayout.Width(50));

            EditorGUILayout.LabelField("Screen %:", GUILayout.Width(60));
            _lodLevels[i].screenRelativeHeight = EditorGUILayout.Slider(
                _lodLevels[i].screenRelativeHeight, 0f, 1f, GUILayout.Width(100));

            EditorGUILayout.LabelField("Quality:", GUILayout.Width(50));
            _lodLevels[i].qualityPercentage = EditorGUILayout.Slider(
                _lodLevels[i].qualityPercentage, 1f, 100f, GUILayout.Width(100));

            if (i > 0 && GUILayout.Button("X", GUILayout.Width(20)))
            {
                _lodLevels.RemoveAt(i);
                break;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();

        if (_lodLevels.Count < 6 && GUILayout.Button("+ Add LOD Level"))
        {
            float lastThreshold = _lodLevels.Count > 0 ? _lodLevels[_lodLevels.Count - 1].screenRelativeHeight : 1f;
            float lastQuality = _lodLevels.Count > 0 ? _lodLevels[_lodLevels.Count - 1].qualityPercentage : 100f;

            _lodLevels.Add(new LODSettings
            {
                screenRelativeHeight = lastThreshold * 0.5f,
                qualityPercentage = lastQuality * 0.5f
            });
        }

        if (GUILayout.Button("Reset to Default"))
        {
            _lodLevels = new List<LODSettings>
            {
                new LODSettings { screenRelativeHeight = 0.6f, qualityPercentage = 100f },
                new LODSettings { screenRelativeHeight = 0.3f, qualityPercentage = 50f },
                new LODSettings { screenRelativeHeight = 0.1f, qualityPercentage = 25f },
                new LODSettings { screenRelativeHeight = 0.01f, qualityPercentage = 10f }
            };
        }

        EditorGUILayout.EndHorizontal();

        // Cull ratio
        EditorGUILayout.Space(5);
        _cullRatio = EditorGUILayout.Slider("Cull Below", _cullRatio, 0f, 0.1f);

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);
    }

    private void DrawAdvancedSettings()
    {
        _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced Settings", true);

        if (_showAdvanced)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _preserveBorders = EditorGUILayout.Toggle("Preserve Borders", _preserveBorders);
            _preserveUVSeams = EditorGUILayout.Toggle("Preserve UV Seams", _preserveUVSeams);
            _recalculateNormals = EditorGUILayout.Toggle("Recalculate Normals", _recalculateNormals);

            if (_recalculateNormals)
            {
                _normalAngle = EditorGUILayout.Slider("Normal Angle", _normalAngle, 0f, 180f);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(10);
    }

    private void DrawPreview()
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();
        _previewMode = GUILayout.Toggle(_previewMode, "Preview Mode", "Button");
        if (EditorGUI.EndChangeCheck())
        {
            ApplyPreview();
        }

        if (_previewMode)
        {
            EditorGUILayout.LabelField("LOD Level:", GUILayout.Width(70));
            EditorGUI.BeginChangeCheck();
            _previewLOD = EditorGUILayout.IntSlider(_previewLOD, 0, _lodLevels.Count - 1);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyPreview();
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
    }

    private void DrawActions()
    {
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(_selectedObjects.Count == 0);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Generate LOD Group", GUILayout.Height(30)))
        {
            GenerateLODs();
        }

        if (GUILayout.Button("Generate & Save Meshes", GUILayout.Height(30)))
        {
            GenerateAndSaveLODs();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Analyze Selection"))
        {
            AnalyzeSelection();
        }

        if (GUILayout.Button("Remove LOD Groups"))
        {
            RemoveLODGroups();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(10);
    }

    private void DrawStatistics()
    {
        if (_totalReducedTris > 0)
        {
            EditorGUILayout.LabelField("Last Generation Statistics", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"Original triangles: {_totalOriginalTris:N0}");
            EditorGUILayout.LabelField($"Total LOD triangles: {_totalReducedTris:N0}");

            float reduction = 100f * (1f - (float)_totalReducedTris / (_totalOriginalTris * _lodLevels.Count));
            EditorGUILayout.LabelField($"Average reduction: {reduction:F1}%");

            // Per-LOD breakdown
            EditorGUILayout.Space(5);
            for (int i = 0; i < _lodLevels.Count; i++)
            {
                int estimatedTris = Mathf.RoundToInt(_totalOriginalTris * _lodLevels[i].qualityPercentage / 100f);
                EditorGUILayout.LabelField($"  LOD{i}: ~{estimatedTris:N0} tris ({_lodLevels[i].qualityPercentage:F0}%)");
            }

            EditorGUILayout.EndVertical();
        }
    }

    #endregion

    #region Logic

    private void GenerateLODs()
    {
        if (_selectedObjects.Count == 0) return;

        _totalReducedTris = 0;
        int processedCount = 0;

        foreach (var obj in _selectedObjects)
        {
            try
            {
                GenerateLODForObject(obj, false);
                processedCount++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LODGenerator] Failed to process {obj.name}: {e.Message}");
            }
        }

        Debug.Log($"[LODGenerator] Generated LOD groups for {processedCount} objects");
    }

    private void GenerateAndSaveLODs()
    {
        string folder = EditorUtility.SaveFolderPanel("Save LOD Meshes", "Assets", "LOD_Meshes");
        if (string.IsNullOrEmpty(folder)) return;

        // Convert to relative path
        if (folder.StartsWith(Application.dataPath))
        {
            folder = "Assets" + folder.Substring(Application.dataPath.Length);
        }

        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogError("[LODGenerator] Invalid folder selected");
            return;
        }

        _totalReducedTris = 0;
        int processedCount = 0;

        foreach (var obj in _selectedObjects)
        {
            try
            {
                GenerateLODForObject(obj, true, folder);
                processedCount++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LODGenerator] Failed to process {obj.name}: {e.Message}");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[LODGenerator] Generated and saved LOD meshes for {processedCount} objects");
    }

    private void GenerateLODForObject(GameObject obj, bool saveMeshes, string saveFolder = "")
    {
        var meshFilter = obj.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null) return;

        Mesh originalMesh = meshFilter.sharedMesh;
        var renderer = obj.GetComponent<MeshRenderer>();
        if (renderer == null) return;

        // Create LOD group
        LODGroup lodGroup = obj.GetComponent<LODGroup>();
        if (lodGroup == null)
        {
            lodGroup = Undo.AddComponent<LODGroup>(obj);
        }

        LOD[] lods = new LOD[_lodLevels.Count + 1]; // +1 for cull

        for (int i = 0; i < _lodLevels.Count; i++)
        {
            Mesh lodMesh;

            if (i == 0)
            {
                // LOD0 uses original mesh
                lodMesh = originalMesh;
            }
            else
            {
                // Generate simplified mesh
                lodMesh = SimplifyMesh(originalMesh, _lodLevels[i].qualityPercentage / 100f);

                if (saveMeshes && !string.IsNullOrEmpty(saveFolder))
                {
                    string meshPath = $"{saveFolder}/{obj.name}_LOD{i}.asset";
                    AssetDatabase.CreateAsset(lodMesh, meshPath);
                }

                _totalReducedTris += lodMesh.triangles.Length / 3;
            }

            // Create LOD renderer (for LOD0, use existing; for others, create child)
            Renderer lodRenderer;

            if (i == 0)
            {
                lodRenderer = renderer;
            }
            else
            {
                // Create child object for this LOD
                GameObject lodChild = new GameObject($"LOD{i}");
                lodChild.transform.SetParent(obj.transform);
                lodChild.transform.localPosition = Vector3.zero;
                lodChild.transform.localRotation = Quaternion.identity;
                lodChild.transform.localScale = Vector3.one;

                var childMf = lodChild.AddComponent<MeshFilter>();
                childMf.sharedMesh = lodMesh;

                lodRenderer = lodChild.AddComponent<MeshRenderer>();
                lodRenderer.sharedMaterials = renderer.sharedMaterials;

                Undo.RegisterCreatedObjectUndo(lodChild, "Create LOD");
            }

            lods[i] = new LOD(_lodLevels[i].screenRelativeHeight, new Renderer[] { lodRenderer });
        }

        // Cull LOD (empty renderers)
        lods[_lodLevels.Count] = new LOD(_cullRatio, new Renderer[0]);

        lodGroup.SetLODs(lods);
        lodGroup.RecalculateBounds();

        EditorUtility.SetDirty(obj);
    }

    private Mesh SimplifyMesh(Mesh original, float quality)
    {
        // Clone the mesh
        Mesh simplified = new Mesh();
        simplified.name = original.name + "_simplified";

        // Get original data
        Vector3[] vertices = original.vertices;
        int[] triangles = original.triangles;
        Vector3[] normals = original.normals;
        Vector2[] uvs = original.uv;
        Color[] colors = original.colors;

        int targetTriCount = Mathf.Max(4, Mathf.RoundToInt(triangles.Length / 3 * quality));

        // Simple decimation using vertex clustering
        // (In production, you'd use a proper mesh simplification library like Simplygon or MeshDecimator)
        simplified = SimplifyByVertexClustering(original, quality);

        if (_recalculateNormals)
        {
            simplified.RecalculateNormals();
        }

        simplified.RecalculateBounds();
        simplified.RecalculateTangents();

        return simplified;
    }

    private Mesh SimplifyByVertexClustering(Mesh original, float quality)
    {
        // Simple vertex clustering algorithm
        Vector3[] vertices = original.vertices;
        int[] triangles = original.triangles;
        Vector2[] uvs = original.uv;
        Vector3[] normals = original.normals;

        // Calculate grid cell size based on mesh bounds and quality
        Bounds bounds = original.bounds;
        float maxDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));

        // More cells = higher quality
        int gridResolution = Mathf.Max(4, Mathf.RoundToInt(20 * quality));
        float cellSize = maxDimension / gridResolution;

        // Map vertices to grid cells
        Dictionary<Vector3Int, List<int>> cells = new Dictionary<Vector3Int, List<int>>();
        Dictionary<Vector3Int, int> cellToNewVertex = new Dictionary<Vector3Int, int>();

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3Int cell = new Vector3Int(
                Mathf.FloorToInt((vertices[i].x - bounds.min.x) / cellSize),
                Mathf.FloorToInt((vertices[i].y - bounds.min.y) / cellSize),
                Mathf.FloorToInt((vertices[i].z - bounds.min.z) / cellSize)
            );

            if (!cells.ContainsKey(cell))
            {
                cells[cell] = new List<int>();
            }
            cells[cell].Add(i);
        }

        // Create new vertices (average of vertices in each cell)
        List<Vector3> newVertices = new List<Vector3>();
        List<Vector2> newUVs = new List<Vector2>();
        List<Vector3> newNormals = new List<Vector3>();
        int[] vertexMap = new int[vertices.Length];

        foreach (var kvp in cells)
        {
            Vector3 avgPos = Vector3.zero;
            Vector2 avgUV = Vector2.zero;
            Vector3 avgNormal = Vector3.zero;

            foreach (int idx in kvp.Value)
            {
                avgPos += vertices[idx];
                if (uvs != null && uvs.Length > idx) avgUV += uvs[idx];
                if (normals != null && normals.Length > idx) avgNormal += normals[idx];
            }

            avgPos /= kvp.Value.Count;
            avgUV /= kvp.Value.Count;
            avgNormal = avgNormal.normalized;

            int newIdx = newVertices.Count;
            cellToNewVertex[kvp.Key] = newIdx;

            foreach (int idx in kvp.Value)
            {
                vertexMap[idx] = newIdx;
            }

            newVertices.Add(avgPos);
            newUVs.Add(avgUV);
            newNormals.Add(avgNormal);
        }

        // Remap triangles and remove degenerate ones
        List<int> newTriangles = new List<int>();

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v0 = vertexMap[triangles[i]];
            int v1 = vertexMap[triangles[i + 1]];
            int v2 = vertexMap[triangles[i + 2]];

            // Skip degenerate triangles
            if (v0 != v1 && v1 != v2 && v2 != v0)
            {
                newTriangles.Add(v0);
                newTriangles.Add(v1);
                newTriangles.Add(v2);
            }
        }

        // Create simplified mesh
        Mesh simplified = new Mesh();
        simplified.name = original.name + "_LOD";
        simplified.vertices = newVertices.ToArray();
        simplified.triangles = newTriangles.ToArray();

        if (newUVs.Count > 0) simplified.uv = newUVs.ToArray();
        if (newNormals.Count > 0) simplified.normals = newNormals.ToArray();

        return simplified;
    }

    private void ApplyPreview()
    {
        if (!_previewMode)
        {
            // Restore original meshes
            foreach (var obj in _selectedObjects)
            {
                var mf = obj.GetComponent<MeshFilter>();
                if (mf != null)
                {
                    // Check if we stored original
                    string key = $"LODGenerator_Original_{obj.GetInstanceID()}";
                    if (EditorPrefs.HasKey(key))
                    {
                        string path = EditorPrefs.GetString(key);
                        Mesh original = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                        if (original != null)
                        {
                            mf.sharedMesh = original;
                        }
                    }
                }
            }
            return;
        }

        // Apply preview LOD
        float quality = _lodLevels[_previewLOD].qualityPercentage / 100f;

        foreach (var obj in _selectedObjects)
        {
            var mf = obj.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                // Store original
                string assetPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    EditorPrefs.SetString($"LODGenerator_Original_{obj.GetInstanceID()}", assetPath);
                }

                if (_previewLOD == 0)
                {
                    // LOD0 = original
                    continue;
                }

                // Generate preview mesh
                Mesh preview = SimplifyMesh(mf.sharedMesh, quality);
                mf.sharedMesh = preview;
            }
        }

        SceneView.RepaintAll();
    }

    private void AnalyzeSelection()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== LOD Analysis ===\n");

        int totalVerts = 0;
        int totalTris = 0;

        foreach (var obj in _selectedObjects)
        {
            var mf = obj.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Mesh mesh = mf.sharedMesh;
                int verts = mesh.vertexCount;
                int tris = mesh.triangles.Length / 3;

                totalVerts += verts;
                totalTris += tris;

                sb.AppendLine($"{obj.name}:");
                sb.AppendLine($"  Vertices: {verts:N0}");
                sb.AppendLine($"  Triangles: {tris:N0}");
                sb.AppendLine($"  Submeshes: {mesh.subMeshCount}");
                sb.AppendLine($"  Has UVs: {mesh.uv.Length > 0}");
                sb.AppendLine($"  Has Normals: {mesh.normals.Length > 0}");
                sb.AppendLine($"  Bounds: {mesh.bounds.size}");
                sb.AppendLine();
            }
        }

        sb.AppendLine($"TOTAL: {totalVerts:N0} vertices, {totalTris:N0} triangles");
        sb.AppendLine();
        sb.AppendLine("Estimated LOD triangle counts:");

        foreach (var lod in _lodLevels)
        {
            int estimatedTris = Mathf.RoundToInt(totalTris * lod.qualityPercentage / 100f);
            sb.AppendLine($"  {lod.qualityPercentage:F0}%: ~{estimatedTris:N0} tris");
        }

        Debug.Log(sb.ToString());
    }

    private void RemoveLODGroups()
    {
        int removed = 0;

        foreach (var obj in _selectedObjects)
        {
            var lodGroup = obj.GetComponent<LODGroup>();
            if (lodGroup != null)
            {
                // Remove LOD child objects
                for (int i = obj.transform.childCount - 1; i >= 0; i--)
                {
                    var child = obj.transform.GetChild(i);
                    if (child.name.StartsWith("LOD"))
                    {
                        Undo.DestroyObjectImmediate(child.gameObject);
                    }
                }

                Undo.DestroyObjectImmediate(lodGroup);
                removed++;
            }
        }

        Debug.Log($"[LODGenerator] Removed {removed} LOD groups");
    }

    #endregion

    // StringBuilder helper
    private class StringBuilder
    {
        private System.Text.StringBuilder _sb = new System.Text.StringBuilder();
        public void AppendLine(string line = "") => _sb.AppendLine(line);
        public override string ToString() => _sb.ToString();
    }
}
