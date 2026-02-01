using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Point of Interest and Quest Marker Placement Tool.
/// Visual editor for placing and managing POIs, quest markers, and waypoints.
/// </summary>
public class POIQuestMarkerPlacer : EditorWindow
{
    [MenuItem("EpicLegends/Tools/POI & Quest Marker Placer")]
    public static void ShowWindow()
    {
        var window = GetWindow<POIQuestMarkerPlacer>("POI & Quest Markers");
        window.minSize = new Vector2(400, 600);
    }

    // Data structures
    [System.Serializable]
    public class POIData
    {
        public string id = "";
        public string displayName = "New POI";
        public POIType type = POIType.Generic;
        public Vector3 position;
        public float radius = 5f;
        public string description = "";
        public string iconPath = "";
        public bool showOnMap = true;
        public bool showOnCompass = true;
        public float discoverRange = 20f;
        public bool isDiscovered;
        public string[] tags = new string[0];
        public Dictionary<string, string> metadata = new Dictionary<string, string>();
    }

    [System.Serializable]
    public class QuestMarker
    {
        public string id = "";
        public string questId = "";
        public string objectiveId = "";
        public string displayName = "Quest Marker";
        public QuestMarkerType type = QuestMarkerType.Objective;
        public Vector3 position;
        public float interactionRadius = 2f;
        public string interactionPrompt = "Interact";
        public bool isActive = true;
        public int priority = 0;
        public string[] prerequisiteMarkers = new string[0];
    }

    [System.Serializable]
    public class Waypoint
    {
        public string id = "";
        public string pathId = "";
        public Vector3 position;
        public int order;
        public float waitTime;
        public WaypointAction action = WaypointAction.None;
        public string actionData = "";
    }

    [System.Serializable]
    public class WaypointPath
    {
        public string id = "";
        public string name = "Path";
        public List<Waypoint> waypoints = new List<Waypoint>();
        public bool isLoop;
        public Color pathColor = Color.yellow;
    }

    public enum POIType
    {
        Generic, Town, Village, Dungeon, Cave, Shrine, Statue, Waypoint, Teleporter,
        Shop, Blacksmith, Inn, QuestGiver, Boss, Treasure, Collectible, ViewPoint, Camp
    }

    public enum QuestMarkerType
    {
        QuestGiver, Objective, TurnIn, Waypoint, Area, Enemy, Item, NPC
    }

    public enum WaypointAction
    {
        None, Wait, PlayAnimation, Interact, Dialogue, Combat
    }

    // State
    private List<POIData> _pois = new List<POIData>();
    private List<QuestMarker> _questMarkers = new List<QuestMarker>();
    private List<WaypointPath> _paths = new List<WaypointPath>();

    private int _selectedPOIIndex = -1;
    private int _selectedMarkerIndex = -1;
    private int _selectedPathIndex = -1;
    private int _selectedWaypointIndex = -1;

    private Vector2 _scrollPos;
    private int _currentTab;
    private readonly string[] _tabNames = { "POIs", "Quest Markers", "Waypoints", "Settings" };

    private bool _isPlacingPOI;
    private bool _isPlacingMarker;
    private bool _isPlacingWaypoint;

    // Settings
    private bool _showPOIGizmos = true;
    private bool _showMarkerGizmos = true;
    private bool _showPathGizmos = true;
    private float _gizmoScale = 1f;
    private string _outputPath = "Assets/ScriptableObjects/World";

    // Type colors
    private readonly Dictionary<POIType, Color> _poiColors = new Dictionary<POIType, Color>
    {
        { POIType.Generic, Color.white },
        { POIType.Town, new Color(0.8f, 0.6f, 0.2f) },
        { POIType.Village, new Color(0.6f, 0.5f, 0.3f) },
        { POIType.Dungeon, new Color(0.5f, 0.2f, 0.2f) },
        { POIType.Cave, new Color(0.4f, 0.4f, 0.4f) },
        { POIType.Shrine, new Color(0.6f, 0.4f, 0.8f) },
        { POIType.Statue, new Color(0.7f, 0.7f, 0.9f) },
        { POIType.Waypoint, new Color(0.2f, 0.6f, 0.9f) },
        { POIType.Teleporter, new Color(0.3f, 0.9f, 0.9f) },
        { POIType.Shop, new Color(0.9f, 0.8f, 0.2f) },
        { POIType.Blacksmith, new Color(0.6f, 0.3f, 0.1f) },
        { POIType.Inn, new Color(0.8f, 0.5f, 0.3f) },
        { POIType.QuestGiver, new Color(1f, 0.9f, 0.2f) },
        { POIType.Boss, new Color(0.9f, 0.1f, 0.1f) },
        { POIType.Treasure, new Color(1f, 0.8f, 0f) },
        { POIType.Collectible, new Color(0.4f, 0.9f, 0.4f) },
        { POIType.ViewPoint, new Color(0.5f, 0.8f, 1f) },
        { POIType.Camp, new Color(0.5f, 0.4f, 0.2f) }
    };

    private readonly Dictionary<QuestMarkerType, Color> _markerColors = new Dictionary<QuestMarkerType, Color>
    {
        { QuestMarkerType.QuestGiver, new Color(1f, 0.9f, 0.2f) },
        { QuestMarkerType.Objective, new Color(0.2f, 0.7f, 1f) },
        { QuestMarkerType.TurnIn, new Color(0.2f, 0.9f, 0.2f) },
        { QuestMarkerType.Waypoint, new Color(0.7f, 0.7f, 0.7f) },
        { QuestMarkerType.Area, new Color(0.5f, 0.5f, 0.9f) },
        { QuestMarkerType.Enemy, new Color(0.9f, 0.3f, 0.3f) },
        { QuestMarkerType.Item, new Color(0.9f, 0.6f, 0.2f) },
        { QuestMarkerType.NPC, new Color(0.6f, 0.9f, 0.6f) }
    };

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        DrawToolbar();

        _currentTab = GUILayout.Toolbar(_currentTab, _tabNames, GUILayout.Height(30));

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        switch (_currentTab)
        {
            case 0: DrawPOIsTab(); break;
            case 1: DrawQuestMarkersTab(); break;
            case 2: DrawWaypointsTab(); break;
            case 3: DrawSettingsTab(); break;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Save All", EditorStyles.toolbarButton, GUILayout.Width(70)))
            SaveAll();
        if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(50)))
            LoadFromJson();

        GUILayout.Space(10);

        _showPOIGizmos = GUILayout.Toggle(_showPOIGizmos, "POIs", EditorStyles.toolbarButton, GUILayout.Width(50));
        _showMarkerGizmos = GUILayout.Toggle(_showMarkerGizmos, "Markers", EditorStyles.toolbarButton, GUILayout.Width(60));
        _showPathGizmos = GUILayout.Toggle(_showPathGizmos, "Paths", EditorStyles.toolbarButton, GUILayout.Width(50));

        GUILayout.FlexibleSpace();

        EditorGUILayout.LabelField($"POIs: {_pois.Count} | Markers: {_questMarkers.Count} | Paths: {_paths.Count}", GUILayout.Width(250));

        EditorGUILayout.EndHorizontal();
    }

    #region POIs Tab

    private void DrawPOIsTab()
    {
        EditorGUILayout.LabelField("Points of Interest", EditorStyles.boldLabel);

        // Placement mode
        EditorGUILayout.BeginHorizontal();
        GUI.color = _isPlacingPOI ? Color.green : Color.white;
        if (GUILayout.Button(_isPlacingPOI ? "Click in Scene to Place" : "Start Placing POIs", GUILayout.Height(30)))
        {
            _isPlacingPOI = !_isPlacingPOI;
            _isPlacingMarker = false;
            _isPlacingWaypoint = false;
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Filter by type
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Filter:", GUILayout.Width(40));
        // Could add filtering here
        EditorGUILayout.EndHorizontal();

        // POI List
        for (int i = 0; i < _pois.Count; i++)
        {
            DrawPOIItem(i);
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("+ Add POI at Origin", GUILayout.Height(25)))
        {
            _pois.Add(new POIData
            {
                id = System.Guid.NewGuid().ToString().Substring(0, 8),
                displayName = $"POI {_pois.Count + 1}"
            });
            _selectedPOIIndex = _pois.Count - 1;
        }

        // Quick add buttons
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Quick Add:", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Town")) QuickAddPOI(POIType.Town);
        if (GUILayout.Button("Dungeon")) QuickAddPOI(POIType.Dungeon);
        if (GUILayout.Button("Shrine")) QuickAddPOI(POIType.Shrine);
        if (GUILayout.Button("Shop")) QuickAddPOI(POIType.Shop);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Teleporter")) QuickAddPOI(POIType.Teleporter);
        if (GUILayout.Button("Treasure")) QuickAddPOI(POIType.Treasure);
        if (GUILayout.Button("Boss")) QuickAddPOI(POIType.Boss);
        if (GUILayout.Button("ViewPoint")) QuickAddPOI(POIType.ViewPoint);
        EditorGUILayout.EndHorizontal();
    }

    private void QuickAddPOI(POIType type)
    {
        _pois.Add(new POIData
        {
            id = System.Guid.NewGuid().ToString().Substring(0, 8),
            displayName = $"{type} {_pois.Count(p => p.type == type) + 1}",
            type = type
        });
        _selectedPOIIndex = _pois.Count - 1;
    }

    private void DrawPOIItem(int index)
    {
        var poi = _pois[index];
        bool isSelected = index == _selectedPOIIndex;

        Color typeColor = _poiColors.ContainsKey(poi.type) ? _poiColors[poi.type] : Color.white;

        EditorGUILayout.BeginVertical(isSelected ? "SelectionRect" : EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();

        // Color indicator
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(5, 20), typeColor);

        if (GUILayout.Button(isSelected ? "▼" : "►", GUILayout.Width(25)))
        {
            _selectedPOIIndex = isSelected ? -1 : index;
        }

        poi.displayName = EditorGUILayout.TextField(poi.displayName);
        poi.type = (POIType)EditorGUILayout.EnumPopup(poi.type, GUILayout.Width(80));

        if (GUILayout.Button("Focus", GUILayout.Width(50)))
        {
            SceneView.lastActiveSceneView.LookAt(poi.position);
        }

        GUI.color = Color.red;
        if (GUILayout.Button("X", GUILayout.Width(20)))
        {
            _pois.RemoveAt(index);
            if (_selectedPOIIndex >= _pois.Count) _selectedPOIIndex = -1;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();

        if (isSelected)
        {
            DrawPOIDetails(poi);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawPOIDetails(POIData poi)
    {
        EditorGUI.indentLevel++;

        poi.id = EditorGUILayout.TextField("ID", poi.id);
        poi.position = EditorGUILayout.Vector3Field("Position", poi.position);
        poi.radius = EditorGUILayout.FloatField("Radius", poi.radius);
        poi.description = EditorGUILayout.TextField("Description", poi.description);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Map Settings", EditorStyles.miniBoldLabel);
        poi.showOnMap = EditorGUILayout.Toggle("Show on Map", poi.showOnMap);
        poi.showOnCompass = EditorGUILayout.Toggle("Show on Compass", poi.showOnCompass);
        poi.discoverRange = EditorGUILayout.FloatField("Discover Range", poi.discoverRange);
        poi.isDiscovered = EditorGUILayout.Toggle("Is Discovered", poi.isDiscovered);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Icon", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        poi.iconPath = EditorGUILayout.TextField(poi.iconPath);
        if (GUILayout.Button("...", GUILayout.Width(25)))
        {
            string path = EditorUtility.OpenFilePanel("Select Icon", "Assets", "png");
            if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
            {
                poi.iconPath = "Assets" + path.Substring(Application.dataPath.Length);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel--;
    }

    #endregion

    #region Quest Markers Tab

    private void DrawQuestMarkersTab()
    {
        EditorGUILayout.LabelField("Quest Markers", EditorStyles.boldLabel);

        // Placement mode
        EditorGUILayout.BeginHorizontal();
        GUI.color = _isPlacingMarker ? Color.green : Color.white;
        if (GUILayout.Button(_isPlacingMarker ? "Click in Scene to Place" : "Start Placing Markers", GUILayout.Height(30)))
        {
            _isPlacingMarker = !_isPlacingMarker;
            _isPlacingPOI = false;
            _isPlacingWaypoint = false;
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Marker List
        for (int i = 0; i < _questMarkers.Count; i++)
        {
            DrawMarkerItem(i);
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("+ Add Quest Marker", GUILayout.Height(25)))
        {
            _questMarkers.Add(new QuestMarker
            {
                id = System.Guid.NewGuid().ToString().Substring(0, 8),
                displayName = $"Marker {_questMarkers.Count + 1}"
            });
            _selectedMarkerIndex = _questMarkers.Count - 1;
        }

        // Quick add by type
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Quick Add:", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Quest Giver")) QuickAddMarker(QuestMarkerType.QuestGiver);
        if (GUILayout.Button("Objective")) QuickAddMarker(QuestMarkerType.Objective);
        if (GUILayout.Button("Turn In")) QuickAddMarker(QuestMarkerType.TurnIn);
        if (GUILayout.Button("Enemy")) QuickAddMarker(QuestMarkerType.Enemy);
        EditorGUILayout.EndHorizontal();
    }

    private void QuickAddMarker(QuestMarkerType type)
    {
        _questMarkers.Add(new QuestMarker
        {
            id = System.Guid.NewGuid().ToString().Substring(0, 8),
            displayName = $"{type} Marker",
            type = type
        });
        _selectedMarkerIndex = _questMarkers.Count - 1;
    }

    private void DrawMarkerItem(int index)
    {
        var marker = _questMarkers[index];
        bool isSelected = index == _selectedMarkerIndex;

        Color typeColor = _markerColors.ContainsKey(marker.type) ? _markerColors[marker.type] : Color.white;

        EditorGUILayout.BeginVertical(isSelected ? "SelectionRect" : EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();

        EditorGUI.DrawRect(GUILayoutUtility.GetRect(5, 20), typeColor);

        marker.isActive = EditorGUILayout.Toggle(marker.isActive, GUILayout.Width(20));

        if (GUILayout.Button(isSelected ? "▼" : "►", GUILayout.Width(25)))
        {
            _selectedMarkerIndex = isSelected ? -1 : index;
        }

        marker.displayName = EditorGUILayout.TextField(marker.displayName);
        marker.type = (QuestMarkerType)EditorGUILayout.EnumPopup(marker.type, GUILayout.Width(80));

        if (GUILayout.Button("Focus", GUILayout.Width(50)))
        {
            SceneView.lastActiveSceneView.LookAt(marker.position);
        }

        GUI.color = Color.red;
        if (GUILayout.Button("X", GUILayout.Width(20)))
        {
            _questMarkers.RemoveAt(index);
            if (_selectedMarkerIndex >= _questMarkers.Count) _selectedMarkerIndex = -1;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();

        if (isSelected)
        {
            DrawMarkerDetails(marker);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawMarkerDetails(QuestMarker marker)
    {
        EditorGUI.indentLevel++;

        marker.id = EditorGUILayout.TextField("ID", marker.id);
        marker.questId = EditorGUILayout.TextField("Quest ID", marker.questId);
        marker.objectiveId = EditorGUILayout.TextField("Objective ID", marker.objectiveId);
        marker.position = EditorGUILayout.Vector3Field("Position", marker.position);
        marker.interactionRadius = EditorGUILayout.FloatField("Interaction Radius", marker.interactionRadius);
        marker.interactionPrompt = EditorGUILayout.TextField("Interaction Prompt", marker.interactionPrompt);
        marker.priority = EditorGUILayout.IntField("Priority", marker.priority);

        EditorGUI.indentLevel--;
    }

    #endregion

    #region Waypoints Tab

    private void DrawWaypointsTab()
    {
        EditorGUILayout.LabelField("Waypoint Paths", EditorStyles.boldLabel);

        // Placement mode
        EditorGUILayout.BeginHorizontal();
        GUI.color = _isPlacingWaypoint ? Color.green : Color.white;
        if (GUILayout.Button(_isPlacingWaypoint ? "Click in Scene to Add Waypoint" : "Start Placing Waypoints", GUILayout.Height(30)))
        {
            _isPlacingWaypoint = !_isPlacingWaypoint;
            _isPlacingPOI = false;
            _isPlacingMarker = false;
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Path List
        for (int i = 0; i < _paths.Count; i++)
        {
            DrawPathItem(i);
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("+ Add New Path", GUILayout.Height(25)))
        {
            _paths.Add(new WaypointPath
            {
                id = System.Guid.NewGuid().ToString().Substring(0, 8),
                name = $"Path {_paths.Count + 1}",
                pathColor = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.8f, 1f)
            });
            _selectedPathIndex = _paths.Count - 1;
        }
    }

    private void DrawPathItem(int index)
    {
        var path = _paths[index];
        bool isSelected = index == _selectedPathIndex;

        EditorGUILayout.BeginVertical(isSelected ? "SelectionRect" : EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();

        path.pathColor = EditorGUILayout.ColorField(GUIContent.none, path.pathColor, false, false, false, GUILayout.Width(20));

        if (GUILayout.Button(isSelected ? "▼" : "►", GUILayout.Width(25)))
        {
            _selectedPathIndex = isSelected ? -1 : index;
            _selectedWaypointIndex = -1;
        }

        path.name = EditorGUILayout.TextField(path.name);
        path.isLoop = EditorGUILayout.Toggle("Loop", path.isLoop, GUILayout.Width(60));

        EditorGUILayout.LabelField($"({path.waypoints.Count} waypoints)", GUILayout.Width(100));

        GUI.color = Color.red;
        if (GUILayout.Button("X", GUILayout.Width(20)))
        {
            _paths.RemoveAt(index);
            if (_selectedPathIndex >= _paths.Count) _selectedPathIndex = -1;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();

        if (isSelected)
        {
            DrawPathDetails(path);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawPathDetails(WaypointPath path)
    {
        EditorGUI.indentLevel++;

        // Waypoints list
        EditorGUILayout.LabelField("Waypoints", EditorStyles.miniBoldLabel);

        for (int i = 0; i < path.waypoints.Count; i++)
        {
            var wp = path.waypoints[i];
            bool wpSelected = i == _selectedWaypointIndex;

            EditorGUILayout.BeginHorizontal(wpSelected ? "SelectionRect" : EditorStyles.helpBox);

            EditorGUILayout.LabelField($"#{i}", GUILayout.Width(25));

            wp.position = EditorGUILayout.Vector3Field("", wp.position, GUILayout.MinWidth(150));

            wp.action = (WaypointAction)EditorGUILayout.EnumPopup(wp.action, GUILayout.Width(70));

            if (wp.action == WaypointAction.Wait)
            {
                wp.waitTime = EditorGUILayout.FloatField(wp.waitTime, GUILayout.Width(40));
            }

            if (GUILayout.Button("▲", GUILayout.Width(20)) && i > 0)
            {
                var temp = path.waypoints[i - 1];
                path.waypoints[i - 1] = wp;
                path.waypoints[i] = temp;
            }
            if (GUILayout.Button("▼", GUILayout.Width(20)) && i < path.waypoints.Count - 1)
            {
                var temp = path.waypoints[i + 1];
                path.waypoints[i + 1] = wp;
                path.waypoints[i] = temp;
            }

            if (GUILayout.Button("Focus", GUILayout.Width(45)))
            {
                SceneView.lastActiveSceneView.LookAt(wp.position);
            }

            GUI.color = Color.red;
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                path.waypoints.RemoveAt(i);
                break;
            }
            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Add Waypoint"))
        {
            Vector3 lastPos = path.waypoints.Count > 0 ? path.waypoints.Last().position : Vector3.zero;
            path.waypoints.Add(new Waypoint
            {
                id = System.Guid.NewGuid().ToString().Substring(0, 8),
                pathId = path.id,
                position = lastPos + Vector3.forward * 5f,
                order = path.waypoints.Count
            });
        }

        // Calculate path length
        float totalLength = 0f;
        for (int i = 1; i < path.waypoints.Count; i++)
        {
            totalLength += Vector3.Distance(path.waypoints[i - 1].position, path.waypoints[i].position);
        }
        if (path.isLoop && path.waypoints.Count > 1)
        {
            totalLength += Vector3.Distance(path.waypoints.Last().position, path.waypoints.First().position);
        }
        EditorGUILayout.LabelField($"Total Path Length: {totalLength:F1}m");

        EditorGUI.indentLevel--;
    }

    #endregion

    #region Settings Tab

    private void DrawSettingsTab()
    {
        EditorGUILayout.LabelField("Display Settings", EditorStyles.boldLabel);

        _gizmoScale = EditorGUILayout.Slider("Gizmo Scale", _gizmoScale, 0.5f, 3f);
        _showPOIGizmos = EditorGUILayout.Toggle("Show POI Gizmos", _showPOIGizmos);
        _showMarkerGizmos = EditorGUILayout.Toggle("Show Marker Gizmos", _showMarkerGizmos);
        _showPathGizmos = EditorGUILayout.Toggle("Show Path Gizmos", _showPathGizmos);

        EditorGUILayout.Space(20);

        EditorGUILayout.LabelField("Export Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _outputPath = EditorGUILayout.TextField("Output Path", _outputPath);
        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            string path = EditorUtility.SaveFolderPanel("Select Output Folder", "Assets", "");
            if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
            {
                _outputPath = "Assets" + path.Substring(Application.dataPath.Length);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(20);

        EditorGUILayout.LabelField("Bulk Operations", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear All POIs"))
        {
            if (EditorUtility.DisplayDialog("Clear POIs", "Remove all POIs?", "Yes", "No"))
            {
                _pois.Clear();
                _selectedPOIIndex = -1;
            }
        }
        if (GUILayout.Button("Clear All Markers"))
        {
            if (EditorUtility.DisplayDialog("Clear Markers", "Remove all markers?", "Yes", "No"))
            {
                _questMarkers.Clear();
                _selectedMarkerIndex = -1;
            }
        }
        if (GUILayout.Button("Clear All Paths"))
        {
            if (EditorUtility.DisplayDialog("Clear Paths", "Remove all paths?", "Yes", "No"))
            {
                _paths.Clear();
                _selectedPathIndex = -1;
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(20);

        EditorGUILayout.LabelField("Import from Scene", EditorStyles.boldLabel);

        if (GUILayout.Button("Import POIs from Tagged Objects", GUILayout.Height(25)))
        {
            ImportFromTaggedObjects();
        }

        if (GUILayout.Button("Import Markers from Quest Components", GUILayout.Height(25)))
        {
            ImportFromQuestComponents();
        }
    }

    private void ImportFromTaggedObjects()
    {
        // Find objects with "POI" tag or POIMarker component
        GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag("POI");
        int imported = 0;

        foreach (var obj in taggedObjects)
        {
            if (!_pois.Any(p => Vector3.Distance(p.position, obj.transform.position) < 0.1f))
            {
                _pois.Add(new POIData
                {
                    id = System.Guid.NewGuid().ToString().Substring(0, 8),
                    displayName = obj.name,
                    position = obj.transform.position
                });
                imported++;
            }
        }

        Debug.Log($"Imported {imported} POIs from scene");
    }

    private void ImportFromQuestComponents()
    {
        // This would find quest-related components in scene
        Debug.Log("Import from quest components - implement based on your quest system");
    }

    #endregion

    #region Scene GUI

    private void OnSceneGUI(SceneView sceneView)
    {
        // Handle placement
        HandlePlacement(sceneView);

        // Draw gizmos
        if (_showPOIGizmos) DrawPOIGizmos();
        if (_showMarkerGizmos) DrawMarkerGizmos();
        if (_showPathGizmos) DrawPathGizmos();

        // Handle selection in scene
        HandleSceneSelection();
    }

    private void HandlePlacement(SceneView sceneView)
    {
        Event e = Event.current;

        if (e.type == UnityEngine.EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (_isPlacingPOI)
                {
                    _pois.Add(new POIData
                    {
                        id = System.Guid.NewGuid().ToString().Substring(0, 8),
                        displayName = $"POI {_pois.Count + 1}",
                        position = hit.point
                    });
                    _selectedPOIIndex = _pois.Count - 1;
                    e.Use();
                    Repaint();
                }
                else if (_isPlacingMarker)
                {
                    _questMarkers.Add(new QuestMarker
                    {
                        id = System.Guid.NewGuid().ToString().Substring(0, 8),
                        displayName = $"Marker {_questMarkers.Count + 1}",
                        position = hit.point
                    });
                    _selectedMarkerIndex = _questMarkers.Count - 1;
                    e.Use();
                    Repaint();
                }
                else if (_isPlacingWaypoint && _selectedPathIndex >= 0)
                {
                    var path = _paths[_selectedPathIndex];
                    path.waypoints.Add(new Waypoint
                    {
                        id = System.Guid.NewGuid().ToString().Substring(0, 8),
                        pathId = path.id,
                        position = hit.point,
                        order = path.waypoints.Count
                    });
                    e.Use();
                    Repaint();
                }
            }
        }

        // Cancel with Escape
        if (e.type == UnityEngine.EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            _isPlacingPOI = false;
            _isPlacingMarker = false;
            _isPlacingWaypoint = false;
            e.Use();
            Repaint();
        }
    }

    private void DrawPOIGizmos()
    {
        for (int i = 0; i < _pois.Count; i++)
        {
            var poi = _pois[i];
            Color color = _poiColors.ContainsKey(poi.type) ? _poiColors[poi.type] : Color.white;

            bool isSelected = i == _selectedPOIIndex;
            Handles.color = isSelected ? Color.yellow : color;

            // Draw sphere
            Handles.DrawWireDisc(poi.position, Vector3.up, poi.radius * _gizmoScale);

            // Draw vertical line
            Handles.DrawLine(poi.position, poi.position + Vector3.up * 3f * _gizmoScale);

            // Draw icon representation
            float size = 0.5f * _gizmoScale;
            Handles.DrawSolidDisc(poi.position + Vector3.up * 3f * _gizmoScale, SceneView.lastActiveSceneView.camera.transform.forward, size);

            // Label
            Handles.Label(poi.position + Vector3.up * 3.5f * _gizmoScale, poi.displayName,
                new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = color } });

            // Discover range
            if (poi.discoverRange > 0)
            {
                Handles.color = new Color(color.r, color.g, color.b, 0.2f);
                Handles.DrawWireDisc(poi.position, Vector3.up, poi.discoverRange);
            }

            // Handle position editing
            if (isSelected)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(poi.position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Move POI");
                    poi.position = newPos;
                }
            }
        }
    }

    private void DrawMarkerGizmos()
    {
        for (int i = 0; i < _questMarkers.Count; i++)
        {
            var marker = _questMarkers[i];
            if (!marker.isActive) continue;

            Color color = _markerColors.ContainsKey(marker.type) ? _markerColors[marker.type] : Color.white;
            bool isSelected = i == _selectedMarkerIndex;

            Handles.color = isSelected ? Color.yellow : color;

            // Draw interaction radius
            Handles.DrawWireDisc(marker.position, Vector3.up, marker.interactionRadius * _gizmoScale);

            // Draw marker
            Vector3 top = marker.position + Vector3.up * 2f * _gizmoScale;
            Handles.DrawLine(marker.position, top);

            // Draw arrow pointing down
            Handles.DrawSolidDisc(top, SceneView.lastActiveSceneView.camera.transform.forward, 0.3f * _gizmoScale);

            // Label
            Handles.Label(top + Vector3.up * 0.5f, marker.displayName);

            // Handle position editing
            if (isSelected)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(marker.position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(this, "Move Marker");
                    marker.position = newPos;
                }
            }
        }
    }

    private void DrawPathGizmos()
    {
        for (int p = 0; p < _paths.Count; p++)
        {
            var path = _paths[p];
            bool isSelected = p == _selectedPathIndex;

            Handles.color = isSelected ? Color.yellow : path.pathColor;

            // Draw path lines
            for (int i = 1; i < path.waypoints.Count; i++)
            {
                Handles.DrawLine(path.waypoints[i - 1].position, path.waypoints[i].position);
            }

            // Draw loop connection
            if (path.isLoop && path.waypoints.Count > 1)
            {
                Handles.DrawDottedLine(path.waypoints.Last().position, path.waypoints.First().position, 4f);
            }

            // Draw waypoint spheres
            for (int i = 0; i < path.waypoints.Count; i++)
            {
                var wp = path.waypoints[i];
                bool wpSelected = isSelected && i == _selectedWaypointIndex;

                float size = (wpSelected ? 0.4f : 0.25f) * _gizmoScale;
                Handles.color = wpSelected ? Color.yellow : path.pathColor;
                Handles.SphereHandleCap(0, wp.position, Quaternion.identity, size, UnityEngine.EventType.Repaint);

                // Draw order number
                Handles.Label(wp.position + Vector3.up * 0.5f, i.ToString());

                // Handle position editing for selected waypoint
                if (wpSelected)
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 newPos = Handles.PositionHandle(wp.position, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(this, "Move Waypoint");
                        wp.position = newPos;
                    }
                }
            }
        }
    }

    private void HandleSceneSelection()
    {
        Event e = Event.current;

        if (e.type == UnityEngine.EventType.MouseDown && e.button == 0 && !e.alt && !_isPlacingPOI && !_isPlacingMarker && !_isPlacingWaypoint)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            float closestDist = float.MaxValue;
            int closestPOI = -1;
            int closestMarker = -1;

            // Check POIs
            for (int i = 0; i < _pois.Count; i++)
            {
                float dist = Vector3.Cross(ray.direction, _pois[i].position - ray.origin).magnitude;
                if (dist < 1f * _gizmoScale && dist < closestDist)
                {
                    closestDist = dist;
                    closestPOI = i;
                }
            }

            // Check Markers
            for (int i = 0; i < _questMarkers.Count; i++)
            {
                float dist = Vector3.Cross(ray.direction, _questMarkers[i].position - ray.origin).magnitude;
                if (dist < 1f * _gizmoScale && dist < closestDist)
                {
                    closestDist = dist;
                    closestMarker = i;
                    closestPOI = -1;
                }
            }

            if (closestPOI >= 0)
            {
                _selectedPOIIndex = closestPOI;
                _selectedMarkerIndex = -1;
                _currentTab = 0;
                e.Use();
                Repaint();
            }
            else if (closestMarker >= 0)
            {
                _selectedMarkerIndex = closestMarker;
                _selectedPOIIndex = -1;
                _currentTab = 1;
                e.Use();
                Repaint();
            }
        }
    }

    #endregion

    #region Save/Load

    private void SaveAll()
    {
        // Ensure output folder exists
        if (!AssetDatabase.IsValidFolder(_outputPath))
        {
            string[] parts = _outputPath.Split('/');
            string currentPath = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string nextPath = currentPath + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                    AssetDatabase.CreateFolder(currentPath, parts[i]);
                currentPath = nextPath;
            }
        }

        var saveData = new WorldMarkersSaveData
        {
            pois = _pois,
            questMarkers = _questMarkers,
            paths = _paths
        };

        string json = JsonUtility.ToJson(saveData, true);
        string path = $"{_outputPath}/WorldMarkers.json";
        System.IO.File.WriteAllText(path, json);

        AssetDatabase.Refresh();
        Debug.Log($"World markers saved to: {path}");
    }

    private void LoadFromJson()
    {
        string path = EditorUtility.OpenFilePanel("Load World Markers", "Assets", "json");
        if (string.IsNullOrEmpty(path)) return;

        string json = System.IO.File.ReadAllText(path);
        var saveData = JsonUtility.FromJson<WorldMarkersSaveData>(json);

        _pois = saveData.pois ?? new List<POIData>();
        _questMarkers = saveData.questMarkers ?? new List<QuestMarker>();
        _paths = saveData.paths ?? new List<WaypointPath>();

        _selectedPOIIndex = -1;
        _selectedMarkerIndex = -1;
        _selectedPathIndex = -1;

        Debug.Log($"Loaded {_pois.Count} POIs, {_questMarkers.Count} markers, {_paths.Count} paths");
    }

    [System.Serializable]
    private class WorldMarkersSaveData
    {
        public List<POIData> pois;
        public List<QuestMarker> questMarkers;
        public List<WaypointPath> paths;
    }

    #endregion
}
