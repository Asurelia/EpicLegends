using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Designer de vagues d'ennemis avec timeline visuelle.
/// Menu: EpicLegends > Tools > Enemy Wave Designer
/// </summary>
public class EnemyWaveDesigner : EditorWindow
{
    #region State

    private List<WaveData> _waves = new List<WaveData>();
    private int _selectedWaveIndex = -1;
    private int _selectedSpawnIndex = -1;
    private Vector2 _scrollPosition;
    private Vector2 _timelineScroll;

    private string _configName = "WaveConfig";
    private float _timeScale = 50f; // pixels per second
    private float _totalDuration = 120f; // 2 minutes

    #endregion

    #region Enemy Types

    private static readonly string[] ENEMY_TYPES = new string[]
    {
        "Goblin",
        "Skeleton",
        "Orc",
        "Wolf",
        "Spider",
        "Troll",
        "Mage",
        "Archer",
        "Knight",
        "Boss"
    };

    private static readonly Color[] ENEMY_COLORS = new Color[]
    {
        new Color(0.4f, 0.8f, 0.3f),  // Goblin - green
        new Color(0.9f, 0.9f, 0.9f),  // Skeleton - white
        new Color(0.5f, 0.7f, 0.3f),  // Orc - olive
        new Color(0.6f, 0.5f, 0.4f),  // Wolf - brown
        new Color(0.3f, 0.3f, 0.3f),  // Spider - dark gray
        new Color(0.4f, 0.6f, 0.4f),  // Troll - dark green
        new Color(0.5f, 0.3f, 0.7f),  // Mage - purple
        new Color(0.7f, 0.5f, 0.3f),  // Archer - tan
        new Color(0.5f, 0.5f, 0.6f),  // Knight - steel
        new Color(0.8f, 0.2f, 0.2f),  // Boss - red
    };

    #endregion

    [MenuItem("EpicLegends/Tools/Enemy Wave Designer")]
    public static void ShowWindow()
    {
        var window = GetWindow<EnemyWaveDesigner>("Wave Designer");
        window.minSize = new Vector2(900, 500);
    }

    private void OnEnable()
    {
        if (_waves.Count == 0)
        {
            CreateDefaultWaves();
        }
    }

    private void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.BeginHorizontal();

        // Left panel: Wave list
        EditorGUILayout.BeginVertical(GUILayout.Width(200));
        DrawWaveList();
        EditorGUILayout.EndVertical();

        // Center: Timeline
        EditorGUILayout.BeginVertical();
        DrawTimeline();
        EditorGUILayout.EndVertical();

        // Right panel: Properties
        EditorGUILayout.BeginVertical(GUILayout.Width(250));
        DrawPropertiesPanel();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    #region Drawing

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            NewConfig();
        }
        if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            LoadConfig();
        }
        if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            SaveConfig();
        }

        GUILayout.Space(20);

        EditorGUILayout.LabelField("Config:", GUILayout.Width(45));
        _configName = EditorGUILayout.TextField(_configName, GUILayout.Width(150));

        GUILayout.FlexibleSpace();

        EditorGUILayout.LabelField("Duration:", GUILayout.Width(55));
        _totalDuration = EditorGUILayout.FloatField(_totalDuration, GUILayout.Width(50));

        EditorGUILayout.LabelField("Zoom:", GUILayout.Width(40));
        _timeScale = EditorGUILayout.Slider(_timeScale, 20f, 100f, GUILayout.Width(100));

        EditorGUILayout.EndHorizontal();
    }

    private void DrawWaveList()
    {
        EditorGUILayout.LabelField("Waves", EditorStyles.boldLabel);

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        for (int i = 0; i < _waves.Count; i++)
        {
            var wave = _waves[i];
            bool isSelected = i == _selectedWaveIndex;

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = isSelected ? new Color(0.5f, 0.7f, 1f) : Color.white;

            if (GUILayout.Button($"Wave {i + 1}: {wave.name}", isSelected ? EditorStyles.boldLabel : EditorStyles.label))
            {
                _selectedWaveIndex = i;
                _selectedSpawnIndex = -1;
            }

            GUI.backgroundColor = Color.white;

            // Enable/disable toggle
            wave.enabled = EditorGUILayout.Toggle(wave.enabled, GUILayout.Width(20));

            EditorGUILayout.EndHorizontal();

            // Show spawn count
            EditorGUILayout.LabelField($"  Spawns: {wave.spawns.Count}", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Add Wave"))
        {
            AddWave();
        }
        if (_selectedWaveIndex >= 0 && GUILayout.Button("- Remove"))
        {
            RemoveWave(_selectedWaveIndex);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawTimeline()
    {
        EditorGUILayout.LabelField("Timeline", EditorStyles.boldLabel);

        Rect timelineRect = GUILayoutUtility.GetRect(position.width - 470, 300);
        GUI.Box(timelineRect, "", EditorStyles.helpBox);

        // Draw time markers
        DrawTimeMarkers(timelineRect);

        // Draw waves
        float waveHeight = 40f;
        float yOffset = 30f;

        for (int i = 0; i < _waves.Count; i++)
        {
            var wave = _waves[i];
            if (!wave.enabled) continue;

            Rect waveRect = new Rect(
                timelineRect.x + 10,
                timelineRect.y + yOffset + i * (waveHeight + 5),
                timelineRect.width - 20,
                waveHeight
            );

            // Wave background
            GUI.backgroundColor = i == _selectedWaveIndex ? new Color(0.6f, 0.8f, 1f, 0.3f) : new Color(0.5f, 0.5f, 0.5f, 0.2f);
            GUI.Box(waveRect, "", EditorStyles.helpBox);
            GUI.backgroundColor = Color.white;

            // Wave label
            GUI.Label(new Rect(waveRect.x + 2, waveRect.y, 60, 15), wave.name, EditorStyles.miniLabel);

            // Draw spawns
            foreach (var spawn in wave.spawns)
            {
                float spawnX = waveRect.x + (spawn.spawnTime / _totalDuration) * waveRect.width;
                float spawnWidth = (spawn.duration / _totalDuration) * waveRect.width;
                spawnWidth = Mathf.Max(spawnWidth, 20f);

                Rect spawnRect = new Rect(spawnX, waveRect.y + 15, spawnWidth, waveHeight - 20);

                // Spawn block
                Color enemyColor = ENEMY_COLORS[spawn.enemyTypeIndex % ENEMY_COLORS.Length];
                GUI.backgroundColor = enemyColor;

                bool isSpawnSelected = _selectedWaveIndex == i &&
                                       _selectedSpawnIndex == wave.spawns.IndexOf(spawn);

                GUIStyle style = new GUIStyle(GUI.skin.box);
                if (isSpawnSelected)
                {
                    style.normal.background = EditorGUIUtility.whiteTexture;
                }

                if (GUI.Button(spawnRect, "", style))
                {
                    _selectedWaveIndex = i;
                    _selectedSpawnIndex = wave.spawns.IndexOf(spawn);
                }

                // Spawn info
                string info = $"{ENEMY_TYPES[spawn.enemyTypeIndex]} x{spawn.count}";
                GUI.Label(spawnRect, info, EditorStyles.centeredGreyMiniLabel);

                GUI.backgroundColor = Color.white;
            }
        }

        // Handle timeline click
        HandleTimelineClick(timelineRect, waveHeight, yOffset);
    }

    private void DrawTimeMarkers(Rect rect)
    {
        float step = 10f; // seconds
        int markerCount = Mathf.CeilToInt(_totalDuration / step);

        for (int i = 0; i <= markerCount; i++)
        {
            float time = i * step;
            float x = rect.x + 10 + (time / _totalDuration) * (rect.width - 20);

            // Line
            Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            Handles.DrawLine(
                new Vector3(x, rect.y + 15, 0),
                new Vector3(x, rect.yMax - 5, 0)
            );

            // Label
            GUI.Label(new Rect(x - 15, rect.y, 30, 15), FormatTime(time), EditorStyles.centeredGreyMiniLabel);
        }
    }

    private void HandleTimelineClick(Rect timelineRect, float waveHeight, float yOffset)
    {
        Event e = Event.current;

        if (e.type == EventType.MouseDown && e.button == 1 && timelineRect.Contains(e.mousePosition))
        {
            // Calculate which wave was clicked
            int waveIndex = Mathf.FloorToInt((e.mousePosition.y - timelineRect.y - yOffset) / (waveHeight + 5));

            if (waveIndex >= 0 && waveIndex < _waves.Count)
            {
                // Calculate time at click position
                float time = ((e.mousePosition.x - timelineRect.x - 10) / (timelineRect.width - 20)) * _totalDuration;
                time = Mathf.Max(0, time);

                ShowTimelineContextMenu(waveIndex, time);
                e.Use();
            }
        }
    }

    private void DrawPropertiesPanel()
    {
        EditorGUILayout.LabelField("Properties", EditorStyles.boldLabel);

        if (_selectedWaveIndex < 0 || _selectedWaveIndex >= _waves.Count)
        {
            EditorGUILayout.HelpBox("Select a wave to edit properties", MessageType.Info);
            return;
        }

        var wave = _waves[_selectedWaveIndex];

        // Wave properties
        EditorGUILayout.LabelField("Wave Settings", EditorStyles.miniBoldLabel);
        wave.name = EditorGUILayout.TextField("Name", wave.name);
        wave.enabled = EditorGUILayout.Toggle("Enabled", wave.enabled);
        wave.difficulty = EditorGUILayout.IntSlider("Difficulty", wave.difficulty, 1, 10);

        EditorGUILayout.Space(10);

        // Selected spawn properties
        if (_selectedSpawnIndex >= 0 && _selectedSpawnIndex < wave.spawns.Count)
        {
            var spawn = wave.spawns[_selectedSpawnIndex];

            EditorGUILayout.LabelField("Spawn Settings", EditorStyles.miniBoldLabel);

            spawn.enemyTypeIndex = EditorGUILayout.Popup("Enemy Type", spawn.enemyTypeIndex, ENEMY_TYPES);
            spawn.count = EditorGUILayout.IntSlider("Count", spawn.count, 1, 50);
            spawn.spawnTime = EditorGUILayout.Slider("Spawn Time", spawn.spawnTime, 0, _totalDuration);
            spawn.duration = EditorGUILayout.Slider("Duration", spawn.duration, 1f, 30f);
            spawn.spawnInterval = EditorGUILayout.Slider("Interval", spawn.spawnInterval, 0.1f, 5f);

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Spawn Point", EditorStyles.miniBoldLabel);
            spawn.spawnPointName = EditorGUILayout.TextField("Point Name", spawn.spawnPointName);
            spawn.randomizePosition = EditorGUILayout.Toggle("Randomize Position", spawn.randomizePosition);
            if (spawn.randomizePosition)
            {
                spawn.randomRadius = EditorGUILayout.Slider("Random Radius", spawn.randomRadius, 1f, 20f);
            }

            EditorGUILayout.Space(10);

            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("Delete Spawn"))
            {
                wave.spawns.RemoveAt(_selectedSpawnIndex);
                _selectedSpawnIndex = -1;
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            EditorGUILayout.HelpBox("Right-click on timeline to add spawns", MessageType.Info);
        }

        GUILayout.FlexibleSpace();

        // Stats
        EditorGUILayout.LabelField("Wave Statistics", EditorStyles.miniBoldLabel);
        int totalEnemies = wave.spawns.Sum(s => s.count);
        EditorGUILayout.LabelField($"Total Enemies: {totalEnemies}");
        EditorGUILayout.LabelField($"Spawn Events: {wave.spawns.Count}");

        if (wave.spawns.Count > 0)
        {
            float lastSpawn = wave.spawns.Max(s => s.spawnTime + s.duration);
            EditorGUILayout.LabelField($"Wave Duration: {FormatTime(lastSpawn)}");
        }

        EditorGUILayout.Space(10);

        // Add spawn button
        if (GUILayout.Button("+ Add Spawn", GUILayout.Height(25)))
        {
            AddSpawn(_selectedWaveIndex, 0f);
        }
    }

    #endregion

    #region Logic

    private void CreateDefaultWaves()
    {
        _waves.Add(new WaveData
        {
            name = "Easy",
            difficulty = 1,
            spawns = new List<SpawnData>
            {
                new SpawnData { enemyTypeIndex = 0, count = 3, spawnTime = 0, duration = 5 },
                new SpawnData { enemyTypeIndex = 0, count = 5, spawnTime = 10, duration = 8 }
            }
        });

        _waves.Add(new WaveData
        {
            name = "Medium",
            difficulty = 3,
            spawns = new List<SpawnData>
            {
                new SpawnData { enemyTypeIndex = 0, count = 5, spawnTime = 0, duration = 5 },
                new SpawnData { enemyTypeIndex = 1, count = 3, spawnTime = 5, duration = 5 },
                new SpawnData { enemyTypeIndex = 2, count = 2, spawnTime = 15, duration = 5 }
            }
        });

        _waves.Add(new WaveData
        {
            name = "Hard",
            difficulty = 5,
            spawns = new List<SpawnData>
            {
                new SpawnData { enemyTypeIndex = 2, count = 5, spawnTime = 0, duration = 5 },
                new SpawnData { enemyTypeIndex = 3, count = 4, spawnTime = 8, duration = 5 },
                new SpawnData { enemyTypeIndex = 9, count = 1, spawnTime = 20, duration = 1 } // Boss
            }
        });
    }

    private void AddWave()
    {
        _waves.Add(new WaveData
        {
            name = $"Wave {_waves.Count + 1}",
            difficulty = 1,
            spawns = new List<SpawnData>()
        });
        _selectedWaveIndex = _waves.Count - 1;
    }

    private void RemoveWave(int index)
    {
        if (index >= 0 && index < _waves.Count)
        {
            _waves.RemoveAt(index);
            _selectedWaveIndex = Mathf.Clamp(_selectedWaveIndex, -1, _waves.Count - 1);
        }
    }

    private void AddSpawn(int waveIndex, float time)
    {
        if (waveIndex >= 0 && waveIndex < _waves.Count)
        {
            var spawn = new SpawnData
            {
                enemyTypeIndex = 0,
                count = 5,
                spawnTime = time,
                duration = 5f,
                spawnInterval = 0.5f
            };
            _waves[waveIndex].spawns.Add(spawn);
            _selectedSpawnIndex = _waves[waveIndex].spawns.Count - 1;
        }
    }

    private void ShowTimelineContextMenu(int waveIndex, float time)
    {
        GenericMenu menu = new GenericMenu();

        for (int i = 0; i < ENEMY_TYPES.Length; i++)
        {
            int typeIndex = i;
            menu.AddItem(new GUIContent($"Add {ENEMY_TYPES[i]} Spawn"), false, () =>
            {
                _selectedWaveIndex = waveIndex;
                var spawn = new SpawnData
                {
                    enemyTypeIndex = typeIndex,
                    count = typeIndex == 9 ? 1 : 5, // Boss = 1, others = 5
                    spawnTime = time,
                    duration = 5f,
                    spawnInterval = 0.5f
                };
                _waves[waveIndex].spawns.Add(spawn);
                _selectedSpawnIndex = _waves[waveIndex].spawns.Count - 1;
            });
        }

        menu.ShowAsContext();
    }

    private string FormatTime(float seconds)
    {
        int mins = Mathf.FloorToInt(seconds / 60);
        int secs = Mathf.FloorToInt(seconds % 60);
        return $"{mins}:{secs:D2}";
    }

    #endregion

    #region Save/Load

    private void NewConfig()
    {
        if (EditorUtility.DisplayDialog("New Config", "Clear current config?", "Yes", "Cancel"))
        {
            _waves.Clear();
            _selectedWaveIndex = -1;
            _selectedSpawnIndex = -1;
            _configName = "WaveConfig";
            CreateDefaultWaves();
        }
    }

    private void SaveConfig()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Wave Config",
            _configName,
            "asset",
            "Save wave configuration"
        );

        if (!string.IsNullOrEmpty(path))
        {
            var config = ScriptableObject.CreateInstance<WaveConfigData>();
            config.configName = _configName;
            config.totalDuration = _totalDuration;
            config.waves = _waves.ToArray();

            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[WaveDesigner] Saved config: {path}");
        }
    }

    private void LoadConfig()
    {
        string path = EditorUtility.OpenFilePanel("Load Wave Config", "Assets", "asset");

        if (!string.IsNullOrEmpty(path))
        {
            path = "Assets" + path.Substring(Application.dataPath.Length);

            var config = AssetDatabase.LoadAssetAtPath<WaveConfigData>(path);
            if (config != null)
            {
                _configName = config.configName;
                _totalDuration = config.totalDuration;
                _waves = config.waves.ToList();
                _selectedWaveIndex = -1;
                _selectedSpawnIndex = -1;

                Debug.Log($"[WaveDesigner] Loaded config: {path}");
            }
        }
    }

    #endregion

    #region Data Classes

    [System.Serializable]
    public class WaveData
    {
        public string name = "Wave";
        public bool enabled = true;
        public int difficulty = 1;
        public List<SpawnData> spawns = new List<SpawnData>();
    }

    [System.Serializable]
    public class SpawnData
    {
        public int enemyTypeIndex;
        public int count = 1;
        public float spawnTime;
        public float duration = 5f;
        public float spawnInterval = 0.5f;
        public string spawnPointName = "SpawnPoint";
        public bool randomizePosition = true;
        public float randomRadius = 5f;
    }

    #endregion
}

/// <summary>
/// ScriptableObject pour sauvegarder les configurations de vagues.
/// </summary>
public class WaveConfigData : ScriptableObject
{
    public string configName;
    public float totalDuration;
    public EnemyWaveDesigner.WaveData[] waves;
}
