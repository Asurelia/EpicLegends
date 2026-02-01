using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Outil de peinture de textures sur le terrain avec splat maps.
/// Menu: EpicLegends > Tools > Texture Blender
/// </summary>
public class TextureBlender : EditorWindow
{
    #region Settings

    [Header("Brush")]
    private float _brushSize = 10f;
    private float _brushStrength = 0.5f;
    private float _brushFalloff = 0.5f;

    [Header("Target")]
    private Terrain _targetTerrain;

    [Header("Textures")]
    private List<TerrainTextureData> _textures = new List<TerrainTextureData>();
    private int _selectedTextureIndex = 0;

    [Header("Auto Rules")]
    private bool _useAutoRules = false;
    private float _snowHeight = 0.8f;
    private float _rockSlopeThreshold = 45f;
    private float _sandHeight = 0.1f;

    [Header("Tool State")]
    private bool _isPainting = false;

    #endregion

    #region State

    private Vector2 _scrollPosition;
    private TerrainData _terrainData;
    private float[,,] _splatmapData;

    #endregion

    [System.Serializable]
    public class TerrainTextureData
    {
        public string name = "New Texture";
        public Texture2D diffuse;
        public Texture2D normal;
        public Vector2 tileSize = new Vector2(10, 10);
        public Color previewColor = Color.gray;
    }

    [MenuItem("EpicLegends/Tools/Texture Blender")]
    public static void ShowWindow()
    {
        var window = GetWindow<TextureBlender>("Texture Blender");
        window.minSize = new Vector2(400, 600);
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        InitializeDefaultTextures();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        _isPainting = false;
    }

    private void InitializeDefaultTextures()
    {
        if (_textures.Count == 0)
        {
            _textures.Add(new TerrainTextureData { name = "Grass", previewColor = new Color(0.3f, 0.6f, 0.2f) });
            _textures.Add(new TerrainTextureData { name = "Dirt", previewColor = new Color(0.5f, 0.4f, 0.3f) });
            _textures.Add(new TerrainTextureData { name = "Rock", previewColor = new Color(0.5f, 0.5f, 0.5f) });
            _textures.Add(new TerrainTextureData { name = "Sand", previewColor = new Color(0.9f, 0.8f, 0.6f) });
            _textures.Add(new TerrainTextureData { name = "Snow", previewColor = Color.white });
            _textures.Add(new TerrainTextureData { name = "Path", previewColor = new Color(0.6f, 0.5f, 0.4f) });
        }
    }

    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        DrawHeader();
        EditorGUILayout.Space(10);

        DrawTerrainSelection();
        EditorGUILayout.Space(10);

        DrawBrushSettings();
        EditorGUILayout.Space(10);

        DrawTexturePalette();
        EditorGUILayout.Space(10);

        DrawAutoRules();
        EditorGUILayout.Space(10);

        DrawToolButtons();

        EditorGUILayout.EndScrollView();

        if (_isPainting)
            SceneView.RepaintAll();
    }

    #region GUI Sections

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Texture Blender", EditorStyles.boldLabel);

        string status = _isPainting ? "PAINTING (Hold LMB)" : "Inactive";
        Color statusColor = _isPainting ? Color.green : Color.gray;

        GUIStyle statusStyle = new GUIStyle(EditorStyles.boldLabel);
        statusStyle.normal.textColor = statusColor;
        EditorGUILayout.LabelField($"Status: {status}", statusStyle);

        EditorGUILayout.HelpBox(
            "Paint textures directly on terrain using splat maps.\n" +
            "Scroll to change brush size, Shift+Scroll for strength.",
            MessageType.Info);
    }

    private void DrawTerrainSelection()
    {
        EditorGUILayout.LabelField("Target Terrain", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        _targetTerrain = (Terrain)EditorGUILayout.ObjectField("Terrain", _targetTerrain, typeof(Terrain), true);

        if (EditorGUI.EndChangeCheck() && _targetTerrain != null)
        {
            _terrainData = _targetTerrain.terrainData;
            LoadSplatmapData();
        }

        if (_targetTerrain == null)
        {
            if (GUILayout.Button("Find Terrain in Scene"))
            {
                _targetTerrain = FindFirstObjectByType<Terrain>();
                if (_targetTerrain != null)
                {
                    _terrainData = _targetTerrain.terrainData;
                    LoadSplatmapData();
                }
            }
        }
        else
        {
            EditorGUILayout.LabelField($"Terrain Size: {_terrainData.size}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Splat Layers: {_terrainData.alphamapLayers}", EditorStyles.miniLabel);
        }
    }

    private void DrawBrushSettings()
    {
        EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);

        _brushSize = EditorGUILayout.Slider("Size", _brushSize, 1f, 100f);
        _brushStrength = EditorGUILayout.Slider("Strength", _brushStrength, 0.01f, 1f);
        _brushFalloff = EditorGUILayout.Slider("Falloff", _brushFalloff, 0f, 1f);
    }

    private void DrawTexturePalette()
    {
        EditorGUILayout.LabelField("Texture Palette", EditorStyles.boldLabel);

        // Grid of texture buttons
        int buttonsPerRow = 3;
        int row = 0;

        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < _textures.Count; i++)
        {
            bool isSelected = i == _selectedTextureIndex;
            GUI.backgroundColor = isSelected ? Color.cyan : _textures[i].previewColor;

            GUIContent content = new GUIContent(
                _textures[i].diffuse != null ? AssetPreview.GetAssetPreview(_textures[i].diffuse) : null,
                _textures[i].name
            );

            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.fixedHeight = 60;
            style.fixedWidth = 80;

            if (GUILayout.Button(content, style))
            {
                _selectedTextureIndex = i;
            }

            // Label below
            Rect lastRect = GUILayoutUtility.GetLastRect();
            GUI.Label(new Rect(lastRect.x, lastRect.yMax - 15, lastRect.width, 15),
                _textures[i].name, EditorStyles.centeredGreyMiniLabel);

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

        EditorGUILayout.Space(5);

        // Selected texture details
        if (_selectedTextureIndex >= 0 && _selectedTextureIndex < _textures.Count)
        {
            var tex = _textures[_selectedTextureIndex];
            EditorGUILayout.LabelField($"Selected: {tex.name}", EditorStyles.boldLabel);

            tex.name = EditorGUILayout.TextField("Name", tex.name);
            tex.diffuse = (Texture2D)EditorGUILayout.ObjectField("Diffuse", tex.diffuse, typeof(Texture2D), false);
            tex.normal = (Texture2D)EditorGUILayout.ObjectField("Normal", tex.normal, typeof(Texture2D), false);
            tex.tileSize = EditorGUILayout.Vector2Field("Tile Size", tex.tileSize);
            tex.previewColor = EditorGUILayout.ColorField("Preview Color", tex.previewColor);
        }

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Texture"))
        {
            _textures.Add(new TerrainTextureData { name = $"Texture {_textures.Count + 1}" });
        }
        if (_textures.Count > 1 && GUILayout.Button("Remove Selected"))
        {
            _textures.RemoveAt(_selectedTextureIndex);
            _selectedTextureIndex = Mathf.Clamp(_selectedTextureIndex, 0, _textures.Count - 1);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawAutoRules()
    {
        EditorGUILayout.LabelField("Auto Rules", EditorStyles.boldLabel);

        _useAutoRules = EditorGUILayout.Toggle("Enable Auto Rules", _useAutoRules);

        if (_useAutoRules)
        {
            EditorGUILayout.HelpBox(
                "Auto rules paint textures based on terrain height and slope.",
                MessageType.Info);

            _snowHeight = EditorGUILayout.Slider("Snow Height", _snowHeight, 0.5f, 1f);
            _rockSlopeThreshold = EditorGUILayout.Slider("Rock Slope (degrees)", _rockSlopeThreshold, 20f, 60f);
            _sandHeight = EditorGUILayout.Slider("Sand Height", _sandHeight, 0f, 0.3f);

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Apply Auto Rules to Entire Terrain", GUILayout.Height(30)))
            {
                ApplyAutoRules();
            }
        }
    }

    private void DrawToolButtons()
    {
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = _isPainting ? Color.green : Color.white;
        if (GUILayout.Button(_isPainting ? "Stop Painting" : "Start Painting", GUILayout.Height(30)))
        {
            _isPainting = !_isPainting;
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Apply Textures to Terrain"))
        {
            ApplyTexturesToTerrain();
        }

        if (GUILayout.Button("Reset Splatmap"))
        {
            ResetSplatmap();
        }
    }

    #endregion

    #region Scene GUI

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!_isPainting || _targetTerrain == null)
            return;

        Event e = Event.current;

        // Raycast to terrain
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f))
            return;

        if (hit.collider.GetComponent<Terrain>() != _targetTerrain)
            return;

        Vector3 brushCenter = hit.point;

        // Draw brush preview
        DrawBrushPreview(brushCenter);

        // Handle input
        HandlePaintInput(e, brushCenter);

        // Consume events
        if (e.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }
    }

    private void DrawBrushPreview(Vector3 center)
    {
        Color brushColor = _textures[_selectedTextureIndex].previewColor;
        brushColor.a = 0.5f;

        // Draw outer circle
        Handles.color = brushColor;
        Handles.DrawWireDisc(center, Vector3.up, _brushSize);

        // Draw inner falloff circle
        Handles.color = new Color(brushColor.r, brushColor.g, brushColor.b, 0.3f);
        Handles.DrawSolidDisc(center, Vector3.up, _brushSize * (1f - _brushFalloff));

        // Draw filled disc
        Handles.color = new Color(brushColor.r, brushColor.g, brushColor.b, 0.1f);
        Handles.DrawSolidDisc(center, Vector3.up, _brushSize);
    }

    private void HandlePaintInput(Event e, Vector3 brushCenter)
    {
        // Scroll to change brush size
        if (e.type == EventType.ScrollWheel)
        {
            if (e.shift)
            {
                _brushStrength = Mathf.Clamp(_brushStrength - e.delta.y * 0.05f, 0.01f, 1f);
            }
            else
            {
                _brushSize = Mathf.Clamp(_brushSize - e.delta.y * 2f, 1f, 100f);
            }
            e.Use();
            Repaint();
        }

        // Paint on click/drag
        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
        {
            PaintAtPosition(brushCenter);
            e.Use();
        }
    }

    #endregion

    #region Painting Logic

    private void LoadSplatmapData()
    {
        if (_terrainData == null) return;

        int alphamapWidth = _terrainData.alphamapWidth;
        int alphamapHeight = _terrainData.alphamapHeight;
        int alphamapLayers = _terrainData.alphamapLayers;

        _splatmapData = _terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);
    }

    private void PaintAtPosition(Vector3 worldPos)
    {
        if (_terrainData == null || _splatmapData == null) return;

        // Convert world position to terrain-local
        Vector3 terrainPos = _targetTerrain.transform.position;
        Vector3 terrainSize = _terrainData.size;

        float relativeX = (worldPos.x - terrainPos.x) / terrainSize.x;
        float relativeZ = (worldPos.z - terrainPos.z) / terrainSize.z;

        int alphamapWidth = _terrainData.alphamapWidth;
        int alphamapHeight = _terrainData.alphamapHeight;

        int centerX = Mathf.RoundToInt(relativeX * alphamapWidth);
        int centerZ = Mathf.RoundToInt(relativeZ * alphamapHeight);

        // Calculate brush radius in alphamap coordinates
        float brushRadiusX = (_brushSize / terrainSize.x) * alphamapWidth;
        float brushRadiusZ = (_brushSize / terrainSize.z) * alphamapHeight;

        int radiusX = Mathf.CeilToInt(brushRadiusX);
        int radiusZ = Mathf.CeilToInt(brushRadiusZ);

        Undo.RecordObject(_terrainData, "Paint Terrain");

        // Paint in brush area
        for (int z = -radiusZ; z <= radiusZ; z++)
        {
            for (int x = -radiusX; x <= radiusX; x++)
            {
                int mapX = centerX + x;
                int mapZ = centerZ + z;

                if (mapX < 0 || mapX >= alphamapWidth || mapZ < 0 || mapZ >= alphamapHeight)
                    continue;

                // Calculate distance from center
                float distX = x / brushRadiusX;
                float distZ = z / brushRadiusZ;
                float distance = Mathf.Sqrt(distX * distX + distZ * distZ);

                if (distance > 1f) continue;

                // Apply falloff
                float falloff = 1f;
                if (distance > (1f - _brushFalloff))
                {
                    falloff = 1f - ((distance - (1f - _brushFalloff)) / _brushFalloff);
                }

                float strength = _brushStrength * falloff;

                // Blend textures
                BlendTextureAtPoint(mapX, mapZ, _selectedTextureIndex, strength);
            }
        }

        // Apply changes
        _terrainData.SetAlphamaps(0, 0, _splatmapData);
    }

    private void BlendTextureAtPoint(int x, int z, int textureIndex, float strength)
    {
        int layers = _splatmapData.GetLength(2);

        if (textureIndex >= layers) return;

        // Get current values
        float[] weights = new float[layers];
        for (int i = 0; i < layers; i++)
        {
            weights[i] = _splatmapData[z, x, i];
        }

        // Increase selected texture
        weights[textureIndex] = Mathf.Clamp01(weights[textureIndex] + strength);

        // Normalize all weights
        float total = 0f;
        for (int i = 0; i < layers; i++)
            total += weights[i];

        if (total > 0f)
        {
            for (int i = 0; i < layers; i++)
            {
                _splatmapData[z, x, i] = weights[i] / total;
            }
        }
    }

    private void ApplyAutoRules()
    {
        if (_terrainData == null) return;

        Undo.RecordObject(_terrainData, "Apply Auto Rules");

        int alphamapWidth = _terrainData.alphamapWidth;
        int alphamapHeight = _terrainData.alphamapHeight;
        int layers = _terrainData.alphamapLayers;

        float[,,] newSplatmap = new float[alphamapHeight, alphamapWidth, layers];

        for (int z = 0; z < alphamapHeight; z++)
        {
            for (int x = 0; x < alphamapWidth; x++)
            {
                // Get height and slope
                float normalizedX = (float)x / alphamapWidth;
                float normalizedZ = (float)z / alphamapHeight;

                float height = _terrainData.GetInterpolatedHeight(normalizedX, normalizedZ) / _terrainData.size.y;
                float steepness = _terrainData.GetSteepness(normalizedX, normalizedZ);

                // Determine texture based on rules
                int textureIndex = 0; // Default: grass

                if (height > _snowHeight)
                {
                    textureIndex = 4; // Snow
                }
                else if (steepness > _rockSlopeThreshold)
                {
                    textureIndex = 2; // Rock
                }
                else if (height < _sandHeight)
                {
                    textureIndex = 3; // Sand
                }
                else
                {
                    textureIndex = 0; // Grass
                }

                // Set weight
                for (int i = 0; i < layers; i++)
                {
                    newSplatmap[z, x, i] = (i == textureIndex) ? 1f : 0f;
                }
            }
        }

        _terrainData.SetAlphamaps(0, 0, newSplatmap);
        _splatmapData = newSplatmap;

        Debug.Log("[TextureBlender] Auto rules applied!");
    }

    private void ApplyTexturesToTerrain()
    {
        if (_targetTerrain == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a terrain first.", "OK");
            return;
        }

        Undo.RecordObject(_terrainData, "Apply Textures");

        // Create terrain layers from our texture data
        List<TerrainLayer> layers = new List<TerrainLayer>();

        foreach (var tex in _textures)
        {
            TerrainLayer layer = new TerrainLayer();
            layer.diffuseTexture = tex.diffuse;
            layer.normalMapTexture = tex.normal;
            layer.tileSize = tex.tileSize;
            layer.tileOffset = Vector2.zero;

            // Save terrain layer as asset
            string path = $"Assets/TerrainLayers/{tex.name}_Layer.terrainlayer";
            EnsureDirectoryExists(path);

            AssetDatabase.CreateAsset(layer, path);
            layers.Add(layer);
        }

        _terrainData.terrainLayers = layers.ToArray();
        AssetDatabase.SaveAssets();

        // Reload splatmap data
        LoadSplatmapData();

        Debug.Log($"[TextureBlender] Applied {layers.Count} texture layers to terrain!");
    }

    private void ResetSplatmap()
    {
        if (_terrainData == null) return;

        if (!EditorUtility.DisplayDialog("Reset Splatmap",
            "This will reset all texture painting. Are you sure?",
            "Yes", "Cancel"))
            return;

        Undo.RecordObject(_terrainData, "Reset Splatmap");

        int alphamapWidth = _terrainData.alphamapWidth;
        int alphamapHeight = _terrainData.alphamapHeight;
        int layers = _terrainData.alphamapLayers;

        float[,,] newSplatmap = new float[alphamapHeight, alphamapWidth, layers];

        // Set first layer to 1, rest to 0
        for (int z = 0; z < alphamapHeight; z++)
        {
            for (int x = 0; x < alphamapWidth; x++)
            {
                newSplatmap[z, x, 0] = 1f;
                for (int i = 1; i < layers; i++)
                {
                    newSplatmap[z, x, i] = 0f;
                }
            }
        }

        _terrainData.SetAlphamaps(0, 0, newSplatmap);
        _splatmapData = newSplatmap;

        Debug.Log("[TextureBlender] Splatmap reset!");
    }

    private void EnsureDirectoryExists(string filePath)
    {
        string directory = System.IO.Path.GetDirectoryName(filePath);
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }
    }

    #endregion
}
