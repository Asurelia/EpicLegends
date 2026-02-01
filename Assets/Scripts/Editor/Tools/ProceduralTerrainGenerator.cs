using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Generateur de terrain procedural avec preview en temps reel.
/// Menu: EpicLegends > Tools > Terrain Generator
/// </summary>
public class ProceduralTerrainGenerator : EditorWindow
{
    #region Settings

    [Header("Terrain Size")]
    private int _terrainWidth = 256;
    private int _terrainLength = 256;
    private float _terrainHeight = 50f;

    [Header("Noise Settings")]
    private int _seed = 42;
    private float _noiseScale = 50f;
    private int _octaves = 4;
    private float _persistence = 0.5f;
    private float _lacunarity = 2f;

    [Header("Height Curve")]
    private AnimationCurve _heightCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Falloff")]
    private bool _useFalloff = true;
    private float _falloffStrength = 3f;
    private float _falloffStart = 0.7f;

    [Header("Output")]
    private string _terrainName = "GeneratedTerrain";

    #endregion

    #region Preview

    private Texture2D _previewTexture;
    private float[,] _heightmap;
    private bool _previewDirty = true;
    private Vector2 _scrollPosition;

    #endregion

    [MenuItem("EpicLegends/Tools/Terrain Generator")]
    public static void ShowWindow()
    {
        var window = GetWindow<ProceduralTerrainGenerator>("Terrain Generator");
        window.minSize = new Vector2(450, 600);
    }

    private void OnEnable()
    {
        _previewDirty = true;
        if (_heightCurve == null || _heightCurve.keys.Length == 0)
        {
            _heightCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        }
    }

    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        DrawHeader();
        EditorGUILayout.Space(10);

        DrawTerrainSettings();
        EditorGUILayout.Space(10);

        DrawNoiseSettings();
        EditorGUILayout.Space(10);

        DrawFalloffSettings();
        EditorGUILayout.Space(10);

        DrawHeightCurve();
        EditorGUILayout.Space(10);

        DrawPreview();
        EditorGUILayout.Space(10);

        DrawOutputSettings();
        EditorGUILayout.Space(10);

        DrawButtons();

        EditorGUILayout.EndScrollView();
    }

    #region GUI Sections

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Procedural Terrain Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Generate procedural terrain using Perlin noise with multiple octaves (fBm).\n" +
            "Adjust settings and click 'Generate Preview' to see results.",
            MessageType.Info);
    }

    private void DrawTerrainSettings()
    {
        EditorGUILayout.LabelField("Terrain Size", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        _terrainWidth = EditorGUILayout.IntSlider("Width", _terrainWidth, 64, 1024);
        _terrainLength = EditorGUILayout.IntSlider("Length", _terrainLength, 64, 1024);
        _terrainHeight = EditorGUILayout.Slider("Max Height", _terrainHeight, 10f, 200f);

        if (EditorGUI.EndChangeCheck())
            _previewDirty = true;
    }

    private void DrawNoiseSettings()
    {
        EditorGUILayout.LabelField("Noise Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.BeginHorizontal();
        _seed = EditorGUILayout.IntField("Seed", _seed);
        if (GUILayout.Button("Random", GUILayout.Width(60)))
        {
            _seed = Random.Range(0, 999999);
            _previewDirty = true;
        }
        EditorGUILayout.EndHorizontal();

        _noiseScale = EditorGUILayout.Slider("Scale", _noiseScale, 10f, 200f);
        _octaves = EditorGUILayout.IntSlider("Octaves", _octaves, 1, 8);
        _persistence = EditorGUILayout.Slider("Persistence", _persistence, 0.1f, 1f);
        _lacunarity = EditorGUILayout.Slider("Lacunarity", _lacunarity, 1f, 4f);

        if (EditorGUI.EndChangeCheck())
            _previewDirty = true;

        EditorGUILayout.HelpBox(
            "Octaves: Detail layers\n" +
            "Persistence: How much each octave contributes\n" +
            "Lacunarity: Frequency multiplier per octave",
            MessageType.None);
    }

    private void DrawFalloffSettings()
    {
        EditorGUILayout.LabelField("Falloff (Island Effect)", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        _useFalloff = EditorGUILayout.Toggle("Use Falloff", _useFalloff);

        if (_useFalloff)
        {
            _falloffStrength = EditorGUILayout.Slider("Strength", _falloffStrength, 1f, 10f);
            _falloffStart = EditorGUILayout.Slider("Start Distance", _falloffStart, 0.1f, 0.9f);
        }

        if (EditorGUI.EndChangeCheck())
            _previewDirty = true;
    }

    private void DrawHeightCurve()
    {
        EditorGUILayout.LabelField("Height Curve", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        _heightCurve = EditorGUILayout.CurveField("Curve", _heightCurve, GUILayout.Height(50));
        if (EditorGUI.EndChangeCheck())
            _previewDirty = true;

        EditorGUILayout.HelpBox("Remap noise values. Use this to create plateaus, cliffs, etc.", MessageType.None);
    }

    private void DrawPreview()
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        if (_previewDirty || _previewTexture == null)
        {
            if (GUILayout.Button("Generate Preview", GUILayout.Height(25)))
            {
                GeneratePreview();
            }
        }

        if (_previewTexture != null)
        {
            float previewSize = Mathf.Min(position.width - 40, 256);
            Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize);
            EditorGUI.DrawPreviewTexture(previewRect, _previewTexture, null, ScaleMode.ScaleToFit);

            EditorGUILayout.LabelField($"Preview: {_terrainWidth}x{_terrainLength}", EditorStyles.miniLabel);
        }
    }

    private void DrawOutputSettings()
    {
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        _terrainName = EditorGUILayout.TextField("Terrain Name", _terrainName);
    }

    private void DrawButtons()
    {
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Generate Terrain", GUILayout.Height(35)))
        {
            GenerateTerrain();
        }
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("Generate Mesh", GUILayout.Height(35)))
        {
            GenerateMesh();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Reset Settings"))
        {
            ResetSettings();
        }
    }

    #endregion

    #region Generation

    private void GeneratePreview()
    {
        _heightmap = GenerateHeightmap(_terrainWidth, _terrainLength);
        _previewTexture = HeightmapToTexture(_heightmap);
        _previewDirty = false;
    }

    private float[,] GenerateHeightmap(int width, int height)
    {
        float[,] heightmap = new float[width, height];
        float[,] falloffMap = _useFalloff ? GenerateFalloffMap(width, height) : null;

        System.Random prng = new System.Random(_seed);
        Vector2[] octaveOffsets = new Vector2[_octaves];
        for (int i = 0; i < _octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000);
            float offsetY = prng.Next(-100000, 100000);
            octaveOffsets[i] = new Vector2(offsetX, offsetY);
        }

        float maxNoiseHeight = float.MinValue;
        float minNoiseHeight = float.MaxValue;

        float halfWidth = width / 2f;
        float halfHeight = height / 2f;

        // Generate raw noise
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float amplitude = 1f;
                float frequency = 1f;
                float noiseHeight = 0f;

                for (int i = 0; i < _octaves; i++)
                {
                    float sampleX = (x - halfWidth + octaveOffsets[i].x) / _noiseScale * frequency;
                    float sampleY = (y - halfHeight + octaveOffsets[i].y) / _noiseScale * frequency;

                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= _persistence;
                    frequency *= _lacunarity;
                }

                if (noiseHeight > maxNoiseHeight)
                    maxNoiseHeight = noiseHeight;
                if (noiseHeight < minNoiseHeight)
                    minNoiseHeight = noiseHeight;

                heightmap[x, y] = noiseHeight;
            }
        }

        // Normalize and apply curve + falloff
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float normalized = Mathf.InverseLerp(minNoiseHeight, maxNoiseHeight, heightmap[x, y]);

                // Apply height curve
                normalized = _heightCurve.Evaluate(normalized);

                // Apply falloff
                if (_useFalloff && falloffMap != null)
                {
                    normalized = Mathf.Clamp01(normalized - falloffMap[x, y]);
                }

                heightmap[x, y] = normalized;
            }
        }

        return heightmap;
    }

    private float[,] GenerateFalloffMap(int width, int height)
    {
        float[,] map = new float[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float xValue = x / (float)width * 2 - 1;
                float yValue = y / (float)height * 2 - 1;

                float distance = Mathf.Max(Mathf.Abs(xValue), Mathf.Abs(yValue));

                if (distance < _falloffStart)
                {
                    map[x, y] = 0;
                }
                else
                {
                    float t = (distance - _falloffStart) / (1 - _falloffStart);
                    map[x, y] = Mathf.Pow(t, _falloffStrength);
                }
            }
        }

        return map;
    }

    private Texture2D HeightmapToTexture(float[,] heightmap)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);

        Texture2D texture = new Texture2D(width, height);
        Color[] colors = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float h = heightmap[x, y];

                // Color gradient based on height
                Color color;
                if (h < 0.2f)
                    color = new Color(0.2f, 0.4f, 0.8f); // Water
                else if (h < 0.3f)
                    color = new Color(0.8f, 0.7f, 0.5f); // Sand
                else if (h < 0.6f)
                    color = new Color(0.3f, 0.6f, 0.2f); // Grass
                else if (h < 0.8f)
                    color = new Color(0.5f, 0.5f, 0.5f); // Rock
                else
                    color = Color.white; // Snow

                colors[y * width + x] = color;
            }
        }

        texture.SetPixels(colors);
        texture.Apply();
        return texture;
    }

    private void GenerateTerrain()
    {
        if (_heightmap == null)
        {
            GeneratePreview();
        }

        // Create terrain data
        TerrainData terrainData = new TerrainData();
        terrainData.heightmapResolution = Mathf.Max(_terrainWidth, _terrainLength) + 1;
        terrainData.size = new Vector3(_terrainWidth, _terrainHeight, _terrainLength);

        // Convert heightmap to terrain format
        int resolution = terrainData.heightmapResolution;
        float[,] heights = new float[resolution, resolution];

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int sampleX = Mathf.FloorToInt((float)x / resolution * _terrainWidth);
                int sampleY = Mathf.FloorToInt((float)y / resolution * _terrainLength);

                sampleX = Mathf.Clamp(sampleX, 0, _terrainWidth - 1);
                sampleY = Mathf.Clamp(sampleY, 0, _terrainLength - 1);

                heights[y, x] = _heightmap[sampleX, sampleY];
            }
        }

        terrainData.SetHeights(0, 0, heights);

        // Create terrain GameObject
        GameObject terrainGO = Terrain.CreateTerrainGameObject(terrainData);
        terrainGO.name = _terrainName;
        terrainGO.transform.position = new Vector3(-_terrainWidth / 2f, 0, -_terrainLength / 2f);

        // Select the new terrain
        Selection.activeGameObject = terrainGO;

        // Save terrain data as asset
        string path = $"Assets/Terrain/{_terrainName}_Data.asset";
        EnsureDirectoryExists(path);
        AssetDatabase.CreateAsset(terrainData, path);
        AssetDatabase.SaveAssets();

        Debug.Log($"[TerrainGenerator] Terrain '{_terrainName}' cree avec succes!");
    }

    private void GenerateMesh()
    {
        if (_heightmap == null)
        {
            GeneratePreview();
        }

        // Create mesh
        Mesh mesh = new Mesh();
        mesh.name = $"{_terrainName}_Mesh";

        int resolution = 64; // Lower resolution for mesh
        Vector3[] vertices = new Vector3[resolution * resolution];
        Vector2[] uvs = new Vector2[resolution * resolution];
        int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];

        float stepX = (float)_terrainWidth / resolution;
        float stepZ = (float)_terrainLength / resolution;

        // Generate vertices
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int sampleX = Mathf.FloorToInt((float)x / resolution * _terrainWidth);
                int sampleZ = Mathf.FloorToInt((float)z / resolution * _terrainLength);

                sampleX = Mathf.Clamp(sampleX, 0, _terrainWidth - 1);
                sampleZ = Mathf.Clamp(sampleZ, 0, _terrainLength - 1);

                float height = _heightmap[sampleX, sampleZ] * _terrainHeight;

                int index = z * resolution + x;
                vertices[index] = new Vector3(x * stepX - _terrainWidth / 2f, height, z * stepZ - _terrainLength / 2f);
                uvs[index] = new Vector2((float)x / resolution, (float)z / resolution);
            }
        }

        // Generate triangles
        int triIndex = 0;
        for (int z = 0; z < resolution - 1; z++)
        {
            for (int x = 0; x < resolution - 1; x++)
            {
                int topLeft = z * resolution + x;
                int topRight = topLeft + 1;
                int bottomLeft = (z + 1) * resolution + x;
                int bottomRight = bottomLeft + 1;

                triangles[triIndex++] = topLeft;
                triangles[triIndex++] = bottomLeft;
                triangles[triIndex++] = topRight;

                triangles[triIndex++] = topRight;
                triangles[triIndex++] = bottomLeft;
                triangles[triIndex++] = bottomRight;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Create GameObject
        GameObject meshGO = new GameObject(_terrainName);
        MeshFilter filter = meshGO.AddComponent<MeshFilter>();
        MeshRenderer renderer = meshGO.AddComponent<MeshRenderer>();
        MeshCollider collider = meshGO.AddComponent<MeshCollider>();

        filter.mesh = mesh;
        collider.sharedMesh = mesh;

        // Create material
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.4f, 0.6f, 0.3f);
        renderer.material = mat;

        // Save mesh
        string meshPath = $"Assets/Meshes/{_terrainName}_Mesh.asset";
        EnsureDirectoryExists(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = meshGO;

        Debug.Log($"[TerrainGenerator] Mesh '{_terrainName}' cree avec succes!");
    }

    private void ResetSettings()
    {
        _terrainWidth = 256;
        _terrainLength = 256;
        _terrainHeight = 50f;
        _seed = 42;
        _noiseScale = 50f;
        _octaves = 4;
        _persistence = 0.5f;
        _lacunarity = 2f;
        _useFalloff = true;
        _falloffStrength = 3f;
        _falloffStart = 0.7f;
        _heightCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        _terrainName = "GeneratedTerrain";
        _previewDirty = true;
        _previewTexture = null;
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
