using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Outil de placement d'objets avec brush dans la Scene View.
/// Menu: EpicLegends > Tools > Object Placer
/// </summary>
public class ObjectPlacer : EditorWindow
{
    #region Settings

    [Header("Brush")]
    private float _brushRadius = 5f;
    private float _brushDensity = 0.5f;
    private LayerMask _placementLayer = ~0;

    [Header("Placement")]
    private GameObject _prefabToPlace;
    private List<GameObject> _prefabPalette = new List<GameObject>();
    private int _selectedPaletteIndex = -1;

    [Header("Randomization")]
    private float _minScale = 0.8f;
    private float _maxScale = 1.2f;
    private bool _randomYRotation = true;
    private bool _alignToSurface = false;
    private float _yOffset = 0f;

    [Header("Tool State")]
    private bool _isPlacing = false;
    private bool _isErasing = false;

    #endregion

    #region State

    private Vector2 _scrollPosition;
    private List<GameObject> _placedObjects = new List<GameObject>();
    private string _undoGroupName = "Object Placement";

    #endregion

    [MenuItem("EpicLegends/Tools/Object Placer")]
    public static void ShowWindow()
    {
        var window = GetWindow<ObjectPlacer>("Object Placer");
        window.minSize = new Vector2(350, 500);
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        _isPlacing = false;
        _isErasing = false;
    }

    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        DrawHeader();
        EditorGUILayout.Space(10);

        DrawToolButtons();
        EditorGUILayout.Space(10);

        DrawBrushSettings();
        EditorGUILayout.Space(10);

        DrawPrefabPalette();
        EditorGUILayout.Space(10);

        DrawRandomizationSettings();
        EditorGUILayout.Space(10);

        DrawPlacementInfo();

        EditorGUILayout.EndScrollView();

        // Force scene view repaint when tool is active
        if (_isPlacing || _isErasing)
        {
            SceneView.RepaintAll();
        }
    }

    #region GUI Sections

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Object Placer", EditorStyles.boldLabel);

        string status = _isPlacing ? "PLACING (Hold LMB)" :
                        _isErasing ? "ERASING (Hold LMB)" :
                        "Inactive";

        Color statusColor = _isPlacing ? Color.green :
                           _isErasing ? Color.red :
                           Color.gray;

        GUIStyle statusStyle = new GUIStyle(EditorStyles.boldLabel);
        statusStyle.normal.textColor = statusColor;

        EditorGUILayout.LabelField($"Status: {status}", statusStyle);
    }

    private void DrawToolButtons()
    {
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        // Place button
        GUI.backgroundColor = _isPlacing ? Color.green : Color.white;
        if (GUILayout.Button(_isPlacing ? "Stop Placing" : "Start Placing", GUILayout.Height(30)))
        {
            _isPlacing = !_isPlacing;
            _isErasing = false;
        }

        // Erase button
        GUI.backgroundColor = _isErasing ? Color.red : Color.white;
        if (GUILayout.Button(_isErasing ? "Stop Erasing" : "Start Erasing", GUILayout.Height(30)))
        {
            _isErasing = !_isErasing;
            _isPlacing = false;
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "PLACE: Click to place objects in brush area\n" +
            "ERASE: Click to remove placed objects in brush area\n" +
            "Hold SHIFT to temporarily switch modes",
            MessageType.Info);
    }

    private void DrawBrushSettings()
    {
        EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);

        _brushRadius = EditorGUILayout.Slider("Radius", _brushRadius, 1f, 50f);
        _brushDensity = EditorGUILayout.Slider("Density", _brushDensity, 0.1f, 5f);
        _placementLayer = EditorGUILayout.MaskField("Placement Layer", _placementLayer, UnityEditorInternal.InternalEditorUtility.layers);
    }

    private void DrawPrefabPalette()
    {
        EditorGUILayout.LabelField("Prefab Palette", EditorStyles.boldLabel);

        // Add prefab field
        EditorGUILayout.BeginHorizontal();
        _prefabToPlace = (GameObject)EditorGUILayout.ObjectField("Add Prefab", _prefabToPlace, typeof(GameObject), false);
        if (GUILayout.Button("+", GUILayout.Width(25)) && _prefabToPlace != null)
        {
            if (!_prefabPalette.Contains(_prefabToPlace))
            {
                _prefabPalette.Add(_prefabToPlace);
            }
            _prefabToPlace = null;
        }
        EditorGUILayout.EndHorizontal();

        // Palette grid
        if (_prefabPalette.Count > 0)
        {
            EditorGUILayout.Space(5);

            int buttonsPerRow = 4;
            int row = 0;

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < _prefabPalette.Count; i++)
            {
                if (_prefabPalette[i] == null)
                {
                    _prefabPalette.RemoveAt(i);
                    i--;
                    continue;
                }

                bool isSelected = i == _selectedPaletteIndex;
                GUI.backgroundColor = isSelected ? Color.cyan : Color.white;

                GUIContent content = new GUIContent(
                    AssetPreview.GetAssetPreview(_prefabPalette[i]) ?? Texture2D.grayTexture,
                    _prefabPalette[i].name
                );

                if (GUILayout.Button(content, GUILayout.Width(60), GUILayout.Height(60)))
                {
                    _selectedPaletteIndex = isSelected ? -1 : i;
                }

                row++;
                if (row >= buttonsPerRow)
                {
                    row = 0;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }
            EditorGUILayout.EndHorizontal();

            GUI.backgroundColor = Color.white;

            // Remove selected button
            if (_selectedPaletteIndex >= 0 && _selectedPaletteIndex < _prefabPalette.Count)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField($"Selected: {_prefabPalette[_selectedPaletteIndex].name}");

                if (GUILayout.Button("Remove from Palette"))
                {
                    _prefabPalette.RemoveAt(_selectedPaletteIndex);
                    _selectedPaletteIndex = -1;
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Add prefabs to the palette to start placing objects.", MessageType.Info);
        }
    }

    private void DrawRandomizationSettings()
    {
        EditorGUILayout.LabelField("Randomization", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Scale Range");
        _minScale = EditorGUILayout.FloatField(_minScale, GUILayout.Width(50));
        EditorGUILayout.LabelField("-", GUILayout.Width(10));
        _maxScale = EditorGUILayout.FloatField(_maxScale, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();

        _randomYRotation = EditorGUILayout.Toggle("Random Y Rotation", _randomYRotation);
        _alignToSurface = EditorGUILayout.Toggle("Align to Surface", _alignToSurface);
        _yOffset = EditorGUILayout.FloatField("Y Offset", _yOffset);
    }

    private void DrawPlacementInfo()
    {
        EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Objects placed this session: {_placedObjects.Count}");

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Clear Placed Objects"))
        {
            ClearPlacedObjects();
        }

        if (GUILayout.Button("Select All Placed"))
        {
            Selection.objects = _placedObjects.ToArray();
        }
    }

    #endregion

    #region Scene GUI

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!_isPlacing && !_isErasing)
            return;

        Event e = Event.current;

        // Get mouse position on terrain
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, _placementLayer))
            return;

        Vector3 brushCenter = hit.point;

        // Draw brush preview
        DrawBrushPreview(brushCenter);

        // Handle input
        HandleInput(e, brushCenter, hit.normal);

        // Consume events
        if (e.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }
    }

    private void DrawBrushPreview(Vector3 center)
    {
        // Brush color based on mode
        Color brushColor = _isPlacing ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f);

        // Draw circle
        Handles.color = brushColor;
        Handles.DrawWireDisc(center, Vector3.up, _brushRadius);

        // Draw filled disc
        Handles.color = new Color(brushColor.r, brushColor.g, brushColor.b, 0.1f);
        Handles.DrawSolidDisc(center, Vector3.up, _brushRadius);

        // Draw center point
        Handles.color = Color.yellow;
        Handles.DrawWireCube(center, Vector3.one * 0.2f);
    }

    private void HandleInput(Event e, Vector3 brushCenter, Vector3 surfaceNormal)
    {
        // Shift temporarily inverts mode
        bool actualPlacing = _isPlacing;
        bool actualErasing = _isErasing;

        if (e.shift)
        {
            actualPlacing = _isErasing;
            actualErasing = _isPlacing;
        }

        // Left mouse button
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (actualPlacing)
            {
                PlaceObjects(brushCenter, surfaceNormal);
            }
            else if (actualErasing)
            {
                EraseObjects(brushCenter);
            }
            e.Use();
        }

        // Drag for continuous placement
        if (e.type == EventType.MouseDrag && e.button == 0)
        {
            if (actualPlacing)
            {
                PlaceObjects(brushCenter, surfaceNormal);
            }
            else if (actualErasing)
            {
                EraseObjects(brushCenter);
            }
            e.Use();
        }

        // Scroll to change brush size
        if (e.type == EventType.ScrollWheel)
        {
            _brushRadius = Mathf.Clamp(_brushRadius - e.delta.y * 0.5f, 1f, 50f);
            e.Use();
            Repaint();
        }
    }

    #endregion

    #region Placement Logic

    private void PlaceObjects(Vector3 center, Vector3 normal)
    {
        if (_prefabPalette.Count == 0 || _selectedPaletteIndex < 0)
            return;

        GameObject prefab = _prefabPalette[_selectedPaletteIndex];
        if (prefab == null)
            return;

        Undo.SetCurrentGroupName(_undoGroupName);
        int undoGroup = Undo.GetCurrentGroup();

        // Calculate number of objects to place based on density
        int objectCount = Mathf.CeilToInt(_brushDensity * _brushRadius);

        for (int i = 0; i < objectCount; i++)
        {
            // Random position in brush
            Vector2 randomOffset = Random.insideUnitCircle * _brushRadius;
            Vector3 spawnPos = center + new Vector3(randomOffset.x, 100f, randomOffset.y);

            // Raycast down to find surface
            if (!Physics.Raycast(spawnPos, Vector3.down, out RaycastHit hit, 200f, _placementLayer))
                continue;

            // Check if too close to existing object
            if (IsTooCloseToExisting(hit.point, 0.5f))
                continue;

            // Create instance
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Place Object");

            // Position
            instance.transform.position = hit.point + Vector3.up * _yOffset;

            // Rotation
            if (_alignToSurface)
            {
                instance.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            }

            if (_randomYRotation)
            {
                instance.transform.Rotate(Vector3.up, Random.Range(0f, 360f), Space.Self);
            }

            // Scale
            float scale = Random.Range(_minScale, _maxScale);
            instance.transform.localScale = Vector3.one * scale;

            _placedObjects.Add(instance);
        }

        Undo.CollapseUndoOperations(undoGroup);
    }

    private void EraseObjects(Vector3 center)
    {
        Undo.SetCurrentGroupName("Erase Objects");
        int undoGroup = Undo.GetCurrentGroup();

        // Find all objects in brush radius
        List<GameObject> toRemove = new List<GameObject>();

        foreach (var obj in _placedObjects)
        {
            if (obj == null) continue;

            float distance = Vector3.Distance(obj.transform.position, center);
            if (distance <= _brushRadius)
            {
                toRemove.Add(obj);
            }
        }

        // Also check scene for objects with same prefab
        Collider[] colliders = Physics.OverlapSphere(center, _brushRadius);
        foreach (var col in colliders)
        {
            if (_prefabPalette.Count > 0)
            {
                foreach (var prefab in _prefabPalette)
                {
                    if (prefab != null && PrefabUtility.GetCorrespondingObjectFromSource(col.gameObject) == prefab)
                    {
                        if (!toRemove.Contains(col.gameObject))
                        {
                            toRemove.Add(col.gameObject);
                        }
                    }
                }
            }
        }

        // Remove objects
        foreach (var obj in toRemove)
        {
            _placedObjects.Remove(obj);
            Undo.DestroyObjectImmediate(obj);
        }

        Undo.CollapseUndoOperations(undoGroup);
    }

    private bool IsTooCloseToExisting(Vector3 position, float minDistance)
    {
        foreach (var obj in _placedObjects)
        {
            if (obj == null) continue;

            if (Vector3.Distance(obj.transform.position, position) < minDistance)
                return true;
        }
        return false;
    }

    private void ClearPlacedObjects()
    {
        if (EditorUtility.DisplayDialog("Clear Objects",
            $"Are you sure you want to delete all {_placedObjects.Count} placed objects?",
            "Yes", "Cancel"))
        {
            Undo.SetCurrentGroupName("Clear Placed Objects");

            foreach (var obj in _placedObjects)
            {
                if (obj != null)
                {
                    Undo.DestroyObjectImmediate(obj);
                }
            }

            _placedObjects.Clear();
        }
    }

    #endregion
}
