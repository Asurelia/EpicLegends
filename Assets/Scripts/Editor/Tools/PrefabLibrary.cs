using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// <summary>
/// Bibliotheque organisee de prefabs avec recherche et preview.
/// Menu: EpicLegends > Tools > Prefab Library
/// </summary>
public class PrefabLibrary : EditorWindow
{
    #region Categories

    private static readonly string[] CATEGORIES = new string[]
    {
        "All",
        "Environment",
        "Buildings",
        "Characters",
        "Enemies",
        "Items",
        "VFX",
        "Props",
        "UI",
        "Favorites"
    };

    #endregion

    #region State

    private string _searchQuery = "";
    private int _selectedCategory = 0;
    private Vector2 _scrollPosition;
    private List<PrefabEntry> _allPrefabs = new List<PrefabEntry>();
    private List<PrefabEntry> _filteredPrefabs = new List<PrefabEntry>();
    private List<string> _favorites = new List<string>();
    private PrefabEntry _selectedPrefab;
    private Editor _previewEditor;
    private float _previewSize = 80f;

    #endregion

    [System.Serializable]
    private class PrefabEntry
    {
        public string name;
        public string path;
        public string category;
        public GameObject prefab;
        public Texture2D preview;
        public bool isFavorite;
    }

    [MenuItem("EpicLegends/Tools/Prefab Library")]
    public static void ShowWindow()
    {
        var window = GetWindow<PrefabLibrary>("Prefab Library");
        window.minSize = new Vector2(500, 400);
    }

    private void OnEnable()
    {
        LoadFavorites();
        RefreshPrefabList();
    }

    private void OnDisable()
    {
        SaveFavorites();
        if (_previewEditor != null)
        {
            DestroyImmediate(_previewEditor);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();

        // Left panel: List
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.6f));
        DrawSearchBar();
        DrawCategoryTabs();
        DrawPrefabGrid();
        EditorGUILayout.EndVertical();

        // Right panel: Preview
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.4f - 10));
        DrawPreviewPanel();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    #region GUI Sections

    private void DrawSearchBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Search field
        EditorGUI.BeginChangeCheck();
        _searchQuery = EditorGUILayout.TextField(_searchQuery, EditorStyles.toolbarSearchField, GUILayout.Width(200));
        if (EditorGUI.EndChangeCheck())
        {
            FilterPrefabs();
        }

        if (GUILayout.Button("", GUI.skin.FindStyle("SearchCancelButton")))
        {
            _searchQuery = "";
            FilterPrefabs();
            GUI.FocusControl(null);
        }

        GUILayout.FlexibleSpace();

        // Preview size slider
        EditorGUILayout.LabelField("Size:", GUILayout.Width(35));
        _previewSize = EditorGUILayout.Slider(_previewSize, 40f, 120f, GUILayout.Width(100));

        // Refresh button
        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            RefreshPrefabList();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawCategoryTabs()
    {
        EditorGUILayout.BeginHorizontal();

        for (int i = 0; i < CATEGORIES.Length; i++)
        {
            GUIStyle style = i == _selectedCategory ? EditorStyles.toolbarButton : EditorStyles.miniButton;

            if (i == _selectedCategory)
            {
                GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
            }

            if (GUILayout.Button(CATEGORIES[i], style, GUILayout.Height(22)))
            {
                _selectedCategory = i;
                FilterPrefabs();
            }

            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawPrefabGrid()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        if (_filteredPrefabs.Count == 0)
        {
            EditorGUILayout.HelpBox("No prefabs found. Try refreshing or changing the category.", MessageType.Info);
        }
        else
        {
            int columns = Mathf.Max(1, Mathf.FloorToInt((position.width * 0.6f - 20) / (_previewSize + 10)));
            int row = 0;

            EditorGUILayout.BeginHorizontal();

            foreach (var entry in _filteredPrefabs)
            {
                DrawPrefabButton(entry);

                row++;
                if (row >= columns)
                {
                    row = 0;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawPrefabButton(PrefabEntry entry)
    {
        bool isSelected = _selectedPrefab == entry;

        // Button style
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.padding = new RectOffset(2, 2, 2, 2);
        buttonStyle.margin = new RectOffset(2, 2, 2, 2);

        if (isSelected)
        {
            GUI.backgroundColor = new Color(0.5f, 0.8f, 1f);
        }

        EditorGUILayout.BeginVertical(buttonStyle, GUILayout.Width(_previewSize), GUILayout.Height(_previewSize + 20));

        // Preview image
        Rect previewRect = GUILayoutUtility.GetRect(_previewSize - 4, _previewSize - 4);

        if (entry.preview != null)
        {
            GUI.DrawTexture(previewRect, entry.preview, ScaleMode.ScaleToFit);
        }
        else
        {
            EditorGUI.DrawRect(previewRect, new Color(0.3f, 0.3f, 0.3f));
        }

        // Favorite star
        if (entry.isFavorite)
        {
            Rect starRect = new Rect(previewRect.xMax - 16, previewRect.y, 16, 16);
            GUI.Label(starRect, "★", new GUIStyle { fontSize = 14, normal = { textColor = Color.yellow } });
        }

        // Name label
        GUIStyle labelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
        labelStyle.wordWrap = true;
        labelStyle.fontSize = 9;
        EditorGUILayout.LabelField(entry.name, labelStyle, GUILayout.Height(18));

        EditorGUILayout.EndVertical();

        GUI.backgroundColor = Color.white;

        // Handle click
        Rect buttonRect = GUILayoutUtility.GetLastRect();
        Event e = Event.current;

        if (e.type == EventType.MouseDown && buttonRect.Contains(e.mousePosition))
        {
            if (e.button == 0)
            {
                // Left click: Select
                _selectedPrefab = entry;

                if (e.clickCount == 2)
                {
                    // Double click: Add to scene
                    AddPrefabToScene(entry);
                }

                e.Use();
                Repaint();
            }
            else if (e.button == 1)
            {
                // Right click: Context menu
                ShowContextMenu(entry);
                e.Use();
            }
        }

        // Drag support
        if (e.type == EventType.MouseDrag && buttonRect.Contains(e.mousePosition) && entry.prefab != null)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new Object[] { entry.prefab };
            DragAndDrop.StartDrag(entry.name);
            e.Use();
        }
    }

    private void DrawPreviewPanel()
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        if (_selectedPrefab == null || _selectedPrefab.prefab == null)
        {
            EditorGUILayout.HelpBox("Select a prefab to see preview", MessageType.Info);
            return;
        }

        // 3D Preview
        if (_previewEditor == null || _previewEditor.target != _selectedPrefab.prefab)
        {
            if (_previewEditor != null)
            {
                DestroyImmediate(_previewEditor);
            }
            _previewEditor = Editor.CreateEditor(_selectedPrefab.prefab);
        }

        if (_previewEditor != null)
        {
            _previewEditor.OnInteractivePreviewGUI(
                GUILayoutUtility.GetRect(200, 200),
                EditorStyles.helpBox
            );
        }

        EditorGUILayout.Space(10);

        // Info
        EditorGUILayout.LabelField("Name:", _selectedPrefab.name);
        EditorGUILayout.LabelField("Category:", _selectedPrefab.category);
        EditorGUILayout.LabelField("Path:", _selectedPrefab.path, EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space(10);

        // Actions
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add to Scene", GUILayout.Height(25)))
        {
            AddPrefabToScene(_selectedPrefab);
        }

        string favText = _selectedPrefab.isFavorite ? "★ Unfavorite" : "☆ Favorite";
        if (GUILayout.Button(favText, GUILayout.Height(25)))
        {
            ToggleFavorite(_selectedPrefab);
        }

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Select in Project"))
        {
            Selection.activeObject = _selectedPrefab.prefab;
            EditorGUIUtility.PingObject(_selectedPrefab.prefab);
        }

        if (GUILayout.Button("Open Prefab"))
        {
            AssetDatabase.OpenAsset(_selectedPrefab.prefab);
        }
    }

    #endregion

    #region Logic

    private void RefreshPrefabList()
    {
        _allPrefabs.Clear();

        // Find all prefabs in Assets/Prefabs
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null)
            {
                var entry = new PrefabEntry
                {
                    name = prefab.name,
                    path = path,
                    category = DetermineCategory(path),
                    prefab = prefab,
                    preview = AssetPreview.GetAssetPreview(prefab),
                    isFavorite = _favorites.Contains(path)
                };

                _allPrefabs.Add(entry);
            }
        }

        // Also search in other common locations
        string[] additionalPaths = new[] { "Assets/Resources", "Assets/Art", "Assets/Models" };
        foreach (string searchPath in additionalPaths)
        {
            if (Directory.Exists(searchPath))
            {
                guids = AssetDatabase.FindAssets("t:Prefab", new[] { searchPath });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);

                    // Skip if already added
                    if (_allPrefabs.Any(p => p.path == path))
                        continue;

                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        var entry = new PrefabEntry
                        {
                            name = prefab.name,
                            path = path,
                            category = DetermineCategory(path),
                            prefab = prefab,
                            preview = AssetPreview.GetAssetPreview(prefab),
                            isFavorite = _favorites.Contains(path)
                        };

                        _allPrefabs.Add(entry);
                    }
                }
            }
        }

        FilterPrefabs();

        Debug.Log($"[PrefabLibrary] Found {_allPrefabs.Count} prefabs");
    }

    private string DetermineCategory(string path)
    {
        path = path.ToLower();

        if (path.Contains("environment") || path.Contains("terrain") || path.Contains("nature"))
            return "Environment";
        if (path.Contains("building") || path.Contains("structure") || path.Contains("house"))
            return "Buildings";
        if (path.Contains("character") || path.Contains("player") || path.Contains("npc"))
            return "Characters";
        if (path.Contains("enemy") || path.Contains("monster") || path.Contains("creature"))
            return "Enemies";
        if (path.Contains("item") || path.Contains("weapon") || path.Contains("armor") || path.Contains("pickup"))
            return "Items";
        if (path.Contains("vfx") || path.Contains("effect") || path.Contains("particle"))
            return "VFX";
        if (path.Contains("ui") || path.Contains("hud") || path.Contains("menu"))
            return "UI";

        return "Props";
    }

    private void FilterPrefabs()
    {
        _filteredPrefabs.Clear();

        string category = CATEGORIES[_selectedCategory];
        string searchLower = _searchQuery.ToLower();

        foreach (var entry in _allPrefabs)
        {
            // Category filter
            if (category == "Favorites")
            {
                if (!entry.isFavorite) continue;
            }
            else if (category != "All" && entry.category != category)
            {
                continue;
            }

            // Search filter
            if (!string.IsNullOrEmpty(searchLower))
            {
                if (!entry.name.ToLower().Contains(searchLower) &&
                    !entry.path.ToLower().Contains(searchLower))
                {
                    continue;
                }
            }

            _filteredPrefabs.Add(entry);
        }

        // Sort: favorites first, then alphabetically
        _filteredPrefabs = _filteredPrefabs
            .OrderByDescending(p => p.isFavorite)
            .ThenBy(p => p.name)
            .ToList();
    }

    private void AddPrefabToScene(PrefabEntry entry)
    {
        if (entry.prefab == null) return;

        // Find placement position
        Vector3 spawnPos = Vector3.zero;

        // Try to place in front of scene camera
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            Camera sceneCam = sceneView.camera;
            Ray ray = new Ray(sceneCam.transform.position, sceneCam.transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                spawnPos = hit.point;
            }
            else
            {
                spawnPos = sceneCam.transform.position + sceneCam.transform.forward * 10f;
            }
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab);
        instance.transform.position = spawnPos;

        Undo.RegisterCreatedObjectUndo(instance, "Add Prefab");
        Selection.activeGameObject = instance;

        Debug.Log($"[PrefabLibrary] Added {entry.name} to scene at {spawnPos}");
    }

    private void ToggleFavorite(PrefabEntry entry)
    {
        entry.isFavorite = !entry.isFavorite;

        if (entry.isFavorite)
        {
            if (!_favorites.Contains(entry.path))
                _favorites.Add(entry.path);
        }
        else
        {
            _favorites.Remove(entry.path);
        }

        SaveFavorites();
        FilterPrefabs();
    }

    private void ShowContextMenu(PrefabEntry entry)
    {
        GenericMenu menu = new GenericMenu();

        menu.AddItem(new GUIContent("Add to Scene"), false, () => AddPrefabToScene(entry));
        menu.AddItem(new GUIContent(entry.isFavorite ? "Remove from Favorites" : "Add to Favorites"),
            false, () => ToggleFavorite(entry));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Select in Project"), false, () =>
        {
            Selection.activeObject = entry.prefab;
            EditorGUIUtility.PingObject(entry.prefab);
        });
        menu.AddItem(new GUIContent("Open Prefab"), false, () =>
        {
            AssetDatabase.OpenAsset(entry.prefab);
        });
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Copy Path"), false, () =>
        {
            EditorGUIUtility.systemCopyBuffer = entry.path;
        });

        menu.ShowAsContext();
    }

    private void LoadFavorites()
    {
        string key = "PrefabLibrary_Favorites";
        string data = EditorPrefs.GetString(key, "");

        _favorites.Clear();
        if (!string.IsNullOrEmpty(data))
        {
            _favorites.AddRange(data.Split('|').Where(s => !string.IsNullOrEmpty(s)));
        }
    }

    private void SaveFavorites()
    {
        string key = "PrefabLibrary_Favorites";
        string data = string.Join("|", _favorites);
        EditorPrefs.SetString(key, data);
    }

    #endregion
}
