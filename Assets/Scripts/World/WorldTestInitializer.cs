using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Initialise et teste la generation de monde procedural.
/// Affiche une UI de progression pendant la generation.
/// </summary>
public class WorldTestInitializer : MonoBehaviour
{
    [Header("World Settings")]
    [SerializeField] private int _worldSeed = 12345;
    [SerializeField] private bool _generateOnStart = true;

    [Header("References")]
    [SerializeField] private Terrain _terrain;
    [SerializeField] private Transform _player;

    [Header("UI")]
    [SerializeField] private Canvas _loadingCanvas;
    [SerializeField] private Slider _progressBar;
    [SerializeField] private Text _statusText;

    [Header("Debug")]
    [SerializeField] private bool _showDebugInfo = true;

    private float _generationProgress;
    private string _currentStatus = "Initializing...";
    private bool _isGenerating;
    private float _generationStartTime;

    private void Start()
    {
        if (_generateOnStart)
        {
            StartCoroutine(InitializeWorld());
        }
    }

    private IEnumerator InitializeWorld()
    {
        _isGenerating = true;
        _generationStartTime = Time.realtimeSinceStartup;

        // Find or create terrain if not assigned
        if (_terrain == null)
        {
            _terrain = FindFirstObjectByType<Terrain>();

            if (_terrain == null)
            {
                // Create terrain data
                TerrainData terrainData = new TerrainData();
                terrainData.heightmapResolution = 513;
                terrainData.size = new Vector3(512, 100, 512);

                // Create terrain GameObject
                GameObject terrainObj = Terrain.CreateTerrainGameObject(terrainData);
                terrainObj.name = "ProceduralTerrain";
                _terrain = terrainObj.GetComponent<Terrain>();

                Debug.Log("[WorldTestInitializer] Created new terrain");
            }
        }

        // Find player if not assigned
        if (_player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                _player = playerObj.transform;
            }
        }

        _currentStatus = "Finding world generator...";
        yield return null;

        // Get or add ProceduralWorldGenerator
        var generator = FindFirstObjectByType<ProceduralWorldGenerator>();
        if (generator == null)
        {
            generator = gameObject.AddComponent<ProceduralWorldGenerator>();
            Debug.Log("[WorldTestInitializer] Added ProceduralWorldGenerator component");
        }

        // Subscribe to progress events
        generator.OnGenerationProgress += OnProgress;
        generator.OnGenerationComplete += OnComplete;

        _currentStatus = "Starting world generation...";
        yield return null;

        // Start generation
        generator.GenerateWorld(_worldSeed);

        // Wait for completion
        while (_isGenerating)
        {
            yield return null;
        }

        // Unsubscribe
        generator.OnGenerationProgress -= OnProgress;
        generator.OnGenerationComplete -= OnComplete;

        // Position player on terrain
        PositionPlayerOnTerrain();

        // Hide loading UI
        if (_loadingCanvas != null)
        {
            _loadingCanvas.gameObject.SetActive(false);
        }

        float elapsed = Time.realtimeSinceStartup - _generationStartTime;
        Debug.Log($"[WorldTestInitializer] World generation completed in {elapsed:F2} seconds");
    }

    private void OnProgress(float progress)
    {
        _generationProgress = progress;

        if (progress < 0.2f)
            _currentStatus = "Generating heightmap...";
        else if (progress < 0.3f)
            _currentStatus = "Creating climate maps...";
        else if (progress < 0.4f)
            _currentStatus = "Identifying features...";
        else if (progress < 0.55f)
            _currentStatus = "Applying terrain...";
        else if (progress < 0.65f)
            _currentStatus = "Generating water...";
        else if (progress < 0.7f)
            _currentStatus = "Creating caves...";
        else if (progress < 0.8f)
            _currentStatus = "Building villages...";
        else if (progress < 0.95f)
            _currentStatus = "Spawning vegetation...";
        else
            _currentStatus = "Adding clouds...";

        // Update UI
        if (_progressBar != null)
        {
            _progressBar.value = progress;
        }
        if (_statusText != null)
        {
            _statusText.text = $"{_currentStatus} ({Mathf.RoundToInt(progress * 100)}%)";
        }
    }

    private void OnComplete()
    {
        _isGenerating = false;
        _currentStatus = "Complete!";
        _generationProgress = 1f;

        Debug.Log("[WorldTestInitializer] World generation complete!");
    }

    private void PositionPlayerOnTerrain()
    {
        if (_player == null || _terrain == null) return;

        // Position at center of terrain
        Vector3 terrainCenter = _terrain.transform.position +
            new Vector3(_terrain.terrainData.size.x / 2f, 0, _terrain.terrainData.size.z / 2f);

        float height = _terrain.SampleHeight(terrainCenter) + 2f;
        _player.position = new Vector3(terrainCenter.x, height, terrainCenter.z);

        Debug.Log($"[WorldTestInitializer] Player positioned at {_player.position}");
    }

    private void OnGUI()
    {
        if (!_showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.BeginVertical("box");

        if (_isGenerating)
        {
            GUILayout.Label($"Status: {_currentStatus}");
            GUILayout.Label($"Progress: {Mathf.RoundToInt(_generationProgress * 100)}%");

            // Draw progress bar
            GUILayout.BeginHorizontal();
            GUILayout.Box("", GUILayout.Width(_generationProgress * 280), GUILayout.Height(20));
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label("World Generation Complete!");

            var generator = FindFirstObjectByType<ProceduralWorldGenerator>();
            if (generator != null)
            {
                GUILayout.Label($"Seed: {generator.WorldSeed}");
                GUILayout.Label($"World Size: {generator.WorldSize}");

                var lakes = generator.GetLakePositions();
                var villages = generator.GetVillagePositions();
                var caves = generator.GetCaveEntrances();

                GUILayout.Label($"Lakes: {lakes.Count}");
                GUILayout.Label($"Villages: {villages.Count}");
                GUILayout.Label($"Caves: {caves.Count}");
            }

            GUILayout.Space(10);
            GUILayout.Label("Controls:");
            GUILayout.Label("WASD - Move");
            GUILayout.Label("Shift - Sprint");
            GUILayout.Label("Space - Jump");
            GUILayout.Label("Escape - Toggle cursor");
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    /// <summary>
    /// Regenere le monde avec un nouveau seed.
    /// </summary>
    [ContextMenu("Regenerate World")]
    public void RegenerateWorld()
    {
        _worldSeed = Random.Range(0, 999999);
        StartCoroutine(InitializeWorld());
    }

    /// <summary>
    /// Regenere avec le meme seed.
    /// </summary>
    [ContextMenu("Regenerate Same Seed")]
    public void RegenerateSameSeed()
    {
        StartCoroutine(InitializeWorld());
    }
}
