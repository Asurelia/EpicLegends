using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Editeur visuel de zones audio pour definir musique et ambiance par region.
/// Menu: EpicLegends > Tools > Audio Zone Manager
/// </summary>
public class AudioZoneManager : EditorWindow
{
    #region Types

    [System.Serializable]
    public class AudioZone
    {
        public string name = "New Zone";
        public Color color = Color.cyan;
        public Vector3 center = Vector3.zero;
        public Vector3 size = new Vector3(20, 10, 20);
        public ZoneShape shape = ZoneShape.Box;
        public float radius = 10f; // For sphere shape

        // Audio settings
        public AudioClip musicTrack;
        public AudioClip ambianceTrack;
        public float musicVolume = 1f;
        public float ambianceVolume = 0.7f;
        public float fadeTime = 2f;

        // Environment effects
        public ReverbPreset reverbPreset = ReverbPreset.Generic;
        public float reverbMix = 0.5f;

        // Priority (higher = override lower)
        public int priority = 0;

        // Runtime
        public bool isExpanded = true;
        public bool isSelected = false;
    }

    public enum ZoneShape { Box, Sphere, Cylinder }

    public enum ReverbPreset
    {
        Off,
        Generic,
        Room,
        Cave,
        Forest,
        Hall,
        Mountains,
        Underwater,
        Arena
    }

    #endregion

    #region State

    private List<AudioZone> _zones = new List<AudioZone>();
    private AudioZone _selectedZone;
    private Vector2 _listScrollPos;
    private Vector2 _inspectorScrollPos;
    private bool _showGizmos = true;
    private bool _editMode = false;
    private Tool _previousTool;

    // Preview
    private AudioSource _previewSource;
    private bool _isPreviewingMusic = false;
    private bool _isPreviewingAmbiance = false;

    #endregion

    [MenuItem("EpicLegends/Tools/Audio Zone Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<AudioZoneManager>("Audio Zones");
        window.minSize = new Vector2(600, 400);
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        LoadZones();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        StopAllPreviews();

        if (_editMode)
        {
            Tools.current = _previousTool;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();

        // Left panel: Zone list
        EditorGUILayout.BeginVertical(GUILayout.Width(200));
        DrawZoneList();
        EditorGUILayout.EndVertical();

        // Separator
        EditorGUILayout.BeginVertical(GUILayout.Width(2));
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(2, position.height), new Color(0.2f, 0.2f, 0.2f));
        EditorGUILayout.EndVertical();

        // Right panel: Inspector
        EditorGUILayout.BeginVertical();
        DrawInspector();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    #region GUI Sections

    private void DrawZoneList()
    {
        EditorGUILayout.LabelField("Audio Zones", EditorStyles.boldLabel);

        // Toolbar
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(25)))
        {
            CreateNewZone();
        }

        if (GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(25)))
        {
            if (_selectedZone != null)
            {
                _zones.Remove(_selectedZone);
                _selectedZone = _zones.Count > 0 ? _zones[0] : null;
            }
        }

        GUILayout.FlexibleSpace();

        _showGizmos = GUILayout.Toggle(_showGizmos, "Gizmos", EditorStyles.toolbarButton);

        EditorGUILayout.EndHorizontal();

        // Edit mode toggle
        EditorGUI.BeginChangeCheck();
        _editMode = GUILayout.Toggle(_editMode, "Edit Mode (Move Zones)", "Button");
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
            }
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(5);

        // Zone list
        _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos);

        foreach (var zone in _zones)
        {
            DrawZoneListItem(zone);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(5);

        // Actions
        if (GUILayout.Button("Generate Zone Objects"))
        {
            GenerateZoneObjects();
        }

        if (GUILayout.Button("Export Configuration"))
        {
            ExportConfiguration();
        }
    }

    private void DrawZoneListItem(AudioZone zone)
    {
        bool isSelected = zone == _selectedZone;

        GUIStyle style = new GUIStyle(EditorStyles.helpBox);
        if (isSelected)
        {
            style.normal.background = MakeTex(1, 1, new Color(0.3f, 0.5f, 0.8f, 0.5f));
        }

        EditorGUILayout.BeginHorizontal(style);

        // Color indicator
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(4, 20, GUILayout.Width(4)), zone.color);

        // Name
        if (GUILayout.Button(zone.name, EditorStyles.label))
        {
            _selectedZone = zone;

            // Focus in scene view
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.LookAt(zone.center, SceneView.lastActiveSceneView.rotation, zone.radius * 2);
            }
        }

        // Priority badge
        GUILayout.Label($"P{zone.priority}", EditorStyles.miniLabel, GUILayout.Width(25));

        EditorGUILayout.EndHorizontal();
    }

    private void DrawInspector()
    {
        if (_selectedZone == null)
        {
            EditorGUILayout.HelpBox("Select a zone to edit its properties", MessageType.Info);
            return;
        }

        _inspectorScrollPos = EditorGUILayout.BeginScrollView(_inspectorScrollPos);

        EditorGUILayout.LabelField("Zone Properties", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // Basic info
        _selectedZone.name = EditorGUILayout.TextField("Name", _selectedZone.name);
        _selectedZone.color = EditorGUILayout.ColorField("Color", _selectedZone.color);
        _selectedZone.priority = EditorGUILayout.IntSlider("Priority", _selectedZone.priority, 0, 10);

        EditorGUILayout.Space(10);

        // Shape
        EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);
        _selectedZone.shape = (ZoneShape)EditorGUILayout.EnumPopup("Type", _selectedZone.shape);
        _selectedZone.center = EditorGUILayout.Vector3Field("Center", _selectedZone.center);

        switch (_selectedZone.shape)
        {
            case ZoneShape.Box:
                _selectedZone.size = EditorGUILayout.Vector3Field("Size", _selectedZone.size);
                break;
            case ZoneShape.Sphere:
                _selectedZone.radius = EditorGUILayout.FloatField("Radius", _selectedZone.radius);
                break;
            case ZoneShape.Cylinder:
                _selectedZone.radius = EditorGUILayout.FloatField("Radius", _selectedZone.radius);
                _selectedZone.size.y = EditorGUILayout.FloatField("Height", _selectedZone.size.y);
                break;
        }

        EditorGUILayout.Space(10);

        // Audio
        EditorGUILayout.LabelField("Audio", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        _selectedZone.musicTrack = (AudioClip)EditorGUILayout.ObjectField("Music", _selectedZone.musicTrack, typeof(AudioClip), false);
        if (_selectedZone.musicTrack != null)
        {
            if (GUILayout.Button(_isPreviewingMusic ? "■" : "▶", GUILayout.Width(25)))
            {
                ToggleMusicPreview();
            }
        }
        EditorGUILayout.EndHorizontal();
        _selectedZone.musicVolume = EditorGUILayout.Slider("Music Volume", _selectedZone.musicVolume, 0f, 1f);

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        _selectedZone.ambianceTrack = (AudioClip)EditorGUILayout.ObjectField("Ambiance", _selectedZone.ambianceTrack, typeof(AudioClip), false);
        if (_selectedZone.ambianceTrack != null)
        {
            if (GUILayout.Button(_isPreviewingAmbiance ? "■" : "▶", GUILayout.Width(25)))
            {
                ToggleAmbiancePreview();
            }
        }
        EditorGUILayout.EndHorizontal();
        _selectedZone.ambianceVolume = EditorGUILayout.Slider("Ambiance Volume", _selectedZone.ambianceVolume, 0f, 1f);

        _selectedZone.fadeTime = EditorGUILayout.Slider("Fade Time", _selectedZone.fadeTime, 0f, 5f);

        EditorGUILayout.Space(10);

        // Reverb
        EditorGUILayout.LabelField("Reverb", EditorStyles.boldLabel);
        _selectedZone.reverbPreset = (ReverbPreset)EditorGUILayout.EnumPopup("Preset", _selectedZone.reverbPreset);
        if (_selectedZone.reverbPreset != ReverbPreset.Off)
        {
            _selectedZone.reverbMix = EditorGUILayout.Slider("Mix", _selectedZone.reverbMix, 0f, 1f);
        }

        EditorGUILayout.Space(10);

        // Quick actions
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Duplicate"))
        {
            DuplicateZone(_selectedZone);
        }
        if (GUILayout.Button("Focus"))
        {
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.LookAt(_selectedZone.center, SceneView.lastActiveSceneView.rotation, _selectedZone.radius * 2);
            }
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Center on Selection"))
        {
            if (Selection.activeTransform != null)
            {
                _selectedZone.center = Selection.activeTransform.position;
            }
        }

        EditorGUILayout.EndScrollView();
    }

    #endregion

    #region Scene GUI

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!_showGizmos) return;

        foreach (var zone in _zones)
        {
            DrawZoneGizmo(zone);
        }

        // Handle zone manipulation in edit mode
        if (_editMode && _selectedZone != null)
        {
            EditorGUI.BeginChangeCheck();

            // Position handle
            Vector3 newCenter = Handles.PositionHandle(_selectedZone.center, Quaternion.identity);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Move Audio Zone");
                _selectedZone.center = newCenter;
                Repaint();
            }

            // Size handles based on shape
            DrawSizeHandles(_selectedZone);
        }

        // Click to select zone
        HandleZoneSelection();
    }

    private void DrawZoneGizmo(AudioZone zone)
    {
        Color color = zone.color;
        color.a = zone == _selectedZone ? 0.3f : 0.15f;

        Handles.color = zone.color;

        switch (zone.shape)
        {
            case ZoneShape.Box:
                // Draw filled box
                Handles.DrawWireCube(zone.center, zone.size);

                // Draw semi-transparent faces
                Handles.color = color;
                DrawBoxFaces(zone.center, zone.size);
                break;

            case ZoneShape.Sphere:
                Handles.DrawWireDisc(zone.center, Vector3.up, zone.radius);
                Handles.DrawWireDisc(zone.center, Vector3.forward, zone.radius);
                Handles.DrawWireDisc(zone.center, Vector3.right, zone.radius);
                break;

            case ZoneShape.Cylinder:
                // Top and bottom circles
                Handles.DrawWireDisc(zone.center + Vector3.up * zone.size.y / 2, Vector3.up, zone.radius);
                Handles.DrawWireDisc(zone.center - Vector3.up * zone.size.y / 2, Vector3.up, zone.radius);

                // Vertical lines
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * Mathf.PI / 2;
                    Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * zone.radius;
                    Handles.DrawLine(
                        zone.center + offset + Vector3.up * zone.size.y / 2,
                        zone.center + offset - Vector3.up * zone.size.y / 2
                    );
                }
                break;
        }

        // Label
        Handles.Label(zone.center + Vector3.up * (zone.size.y / 2 + 1), zone.name, EditorStyles.whiteBoldLabel);

        // Audio icons
        if (zone.musicTrack != null)
        {
            Handles.Label(zone.center + Vector3.up * (zone.size.y / 2 + 0.5f), "♪",
                new GUIStyle { fontSize = 20, normal = { textColor = Color.yellow } });
        }
    }

    private void DrawBoxFaces(Vector3 center, Vector3 size)
    {
        Vector3 half = size / 2;

        // Define 6 faces
        Vector3[] corners = new Vector3[8];
        corners[0] = center + new Vector3(-half.x, -half.y, -half.z);
        corners[1] = center + new Vector3(half.x, -half.y, -half.z);
        corners[2] = center + new Vector3(half.x, -half.y, half.z);
        corners[3] = center + new Vector3(-half.x, -half.y, half.z);
        corners[4] = center + new Vector3(-half.x, half.y, -half.z);
        corners[5] = center + new Vector3(half.x, half.y, -half.z);
        corners[6] = center + new Vector3(half.x, half.y, half.z);
        corners[7] = center + new Vector3(-half.x, half.y, half.z);

        // Draw faces
        Handles.DrawAAConvexPolygon(corners[0], corners[1], corners[2], corners[3]); // Bottom
        Handles.DrawAAConvexPolygon(corners[4], corners[5], corners[6], corners[7]); // Top
    }

    private void DrawSizeHandles(AudioZone zone)
    {
        Handles.color = zone.color;

        switch (zone.shape)
        {
            case ZoneShape.Box:
                // X axis
                EditorGUI.BeginChangeCheck();
                float xSize = Handles.ScaleSlider(zone.size.x, zone.center + Vector3.right * zone.size.x / 2, Vector3.right, Quaternion.identity, HandleUtility.GetHandleSize(zone.center) * 0.5f, 0.1f);
                if (EditorGUI.EndChangeCheck())
                {
                    zone.size.x = Mathf.Max(1, xSize);
                }

                // Y axis
                EditorGUI.BeginChangeCheck();
                float ySize = Handles.ScaleSlider(zone.size.y, zone.center + Vector3.up * zone.size.y / 2, Vector3.up, Quaternion.identity, HandleUtility.GetHandleSize(zone.center) * 0.5f, 0.1f);
                if (EditorGUI.EndChangeCheck())
                {
                    zone.size.y = Mathf.Max(1, ySize);
                }

                // Z axis
                EditorGUI.BeginChangeCheck();
                float zSize = Handles.ScaleSlider(zone.size.z, zone.center + Vector3.forward * zone.size.z / 2, Vector3.forward, Quaternion.identity, HandleUtility.GetHandleSize(zone.center) * 0.5f, 0.1f);
                if (EditorGUI.EndChangeCheck())
                {
                    zone.size.z = Mathf.Max(1, zSize);
                }
                break;

            case ZoneShape.Sphere:
            case ZoneShape.Cylinder:
                EditorGUI.BeginChangeCheck();
                float newRadius = Handles.RadiusHandle(Quaternion.identity, zone.center, zone.radius);
                if (EditorGUI.EndChangeCheck())
                {
                    zone.radius = Mathf.Max(1, newRadius);
                }
                break;
        }
    }

    private void HandleZoneSelection()
    {
        if (!_editMode) return;

        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            foreach (var zone in _zones)
            {
                if (IsPointInZone(ray.origin + ray.direction * 10, zone) ||
                    IsRayIntersectingZone(ray, zone))
                {
                    _selectedZone = zone;
                    Repaint();
                    e.Use();
                    break;
                }
            }
        }
    }

    private bool IsPointInZone(Vector3 point, AudioZone zone)
    {
        switch (zone.shape)
        {
            case ZoneShape.Box:
                Vector3 local = point - zone.center;
                return Mathf.Abs(local.x) <= zone.size.x / 2 &&
                       Mathf.Abs(local.y) <= zone.size.y / 2 &&
                       Mathf.Abs(local.z) <= zone.size.z / 2;

            case ZoneShape.Sphere:
                return Vector3.Distance(point, zone.center) <= zone.radius;

            case ZoneShape.Cylinder:
                Vector3 flatDist = point - zone.center;
                flatDist.y = 0;
                return flatDist.magnitude <= zone.radius &&
                       Mathf.Abs(point.y - zone.center.y) <= zone.size.y / 2;
        }
        return false;
    }

    private bool IsRayIntersectingZone(Ray ray, AudioZone zone)
    {
        switch (zone.shape)
        {
            case ZoneShape.Box:
                Bounds bounds = new Bounds(zone.center, zone.size);
                return bounds.IntersectRay(ray);

            case ZoneShape.Sphere:
                Vector3 oc = ray.origin - zone.center;
                float a = Vector3.Dot(ray.direction, ray.direction);
                float b = 2f * Vector3.Dot(oc, ray.direction);
                float c = Vector3.Dot(oc, oc) - zone.radius * zone.radius;
                float discriminant = b * b - 4 * a * c;
                return discriminant > 0;

            default:
                return false;
        }
    }

    #endregion

    #region Logic

    private void CreateNewZone()
    {
        Vector3 spawnPos = Vector3.zero;

        if (SceneView.lastActiveSceneView != null)
        {
            spawnPos = SceneView.lastActiveSceneView.camera.transform.position +
                       SceneView.lastActiveSceneView.camera.transform.forward * 20f;
        }

        var zone = new AudioZone
        {
            name = $"Zone_{_zones.Count + 1}",
            center = spawnPos,
            color = Random.ColorHSV(0f, 1f, 0.5f, 0.8f, 0.7f, 1f)
        };

        _zones.Add(zone);
        _selectedZone = zone;

        SceneView.RepaintAll();
    }

    private void DuplicateZone(AudioZone source)
    {
        var newZone = new AudioZone
        {
            name = source.name + "_Copy",
            color = source.color,
            center = source.center + Vector3.right * 5,
            size = source.size,
            shape = source.shape,
            radius = source.radius,
            musicTrack = source.musicTrack,
            ambianceTrack = source.ambianceTrack,
            musicVolume = source.musicVolume,
            ambianceVolume = source.ambianceVolume,
            fadeTime = source.fadeTime,
            reverbPreset = source.reverbPreset,
            reverbMix = source.reverbMix,
            priority = source.priority
        };

        _zones.Add(newZone);
        _selectedZone = newZone;
        SceneView.RepaintAll();
    }

    private void GenerateZoneObjects()
    {
        GameObject parent = new GameObject("AudioZones");
        Undo.RegisterCreatedObjectUndo(parent, "Generate Audio Zones");

        foreach (var zone in _zones)
        {
            GameObject zoneObj = new GameObject(zone.name);
            zoneObj.transform.parent = parent.transform;
            zoneObj.transform.position = zone.center;

            // Add collider as trigger
            switch (zone.shape)
            {
                case ZoneShape.Box:
                    var boxCollider = zoneObj.AddComponent<BoxCollider>();
                    boxCollider.size = zone.size;
                    boxCollider.isTrigger = true;
                    break;

                case ZoneShape.Sphere:
                    var sphereCollider = zoneObj.AddComponent<SphereCollider>();
                    sphereCollider.radius = zone.radius;
                    sphereCollider.isTrigger = true;
                    break;

                case ZoneShape.Cylinder:
                    // Use capsule as approximation
                    var capsuleCollider = zoneObj.AddComponent<CapsuleCollider>();
                    capsuleCollider.radius = zone.radius;
                    capsuleCollider.height = zone.size.y;
                    capsuleCollider.isTrigger = true;
                    break;
            }

            // Add AudioSource for music
            if (zone.musicTrack != null)
            {
                var musicSource = zoneObj.AddComponent<AudioSource>();
                musicSource.clip = zone.musicTrack;
                musicSource.volume = zone.musicVolume;
                musicSource.loop = true;
                musicSource.playOnAwake = false;
                musicSource.spatialBlend = 0f; // 2D
            }

            // Add child for ambiance
            if (zone.ambianceTrack != null)
            {
                GameObject ambianceObj = new GameObject("Ambiance");
                ambianceObj.transform.parent = zoneObj.transform;
                ambianceObj.transform.localPosition = Vector3.zero;

                var ambianceSource = ambianceObj.AddComponent<AudioSource>();
                ambianceSource.clip = zone.ambianceTrack;
                ambianceSource.volume = zone.ambianceVolume;
                ambianceSource.loop = true;
                ambianceSource.playOnAwake = false;
                ambianceSource.spatialBlend = 0f;
            }

            // Add reverb zone if needed
            if (zone.reverbPreset != ReverbPreset.Off)
            {
                var reverb = zoneObj.AddComponent<AudioReverbZone>();
                ApplyReverbPreset(reverb, zone.reverbPreset);
                reverb.minDistance = zone.radius * 0.5f;
                reverb.maxDistance = zone.radius;
            }
        }

        Selection.activeGameObject = parent;
        Debug.Log($"[AudioZoneManager] Generated {_zones.Count} audio zone objects");
    }

    private void ApplyReverbPreset(AudioReverbZone reverb, ReverbPreset preset)
    {
        switch (preset)
        {
            case ReverbPreset.Generic:
                reverb.reverbPreset = AudioReverbPreset.Generic;
                break;
            case ReverbPreset.Room:
                reverb.reverbPreset = AudioReverbPreset.Room;
                break;
            case ReverbPreset.Cave:
                reverb.reverbPreset = AudioReverbPreset.Cave;
                break;
            case ReverbPreset.Forest:
                reverb.reverbPreset = AudioReverbPreset.Forest;
                break;
            case ReverbPreset.Hall:
                reverb.reverbPreset = AudioReverbPreset.Hallway;
                break;
            case ReverbPreset.Mountains:
                reverb.reverbPreset = AudioReverbPreset.Mountains;
                break;
            case ReverbPreset.Underwater:
                reverb.reverbPreset = AudioReverbPreset.Underwater;
                break;
            case ReverbPreset.Arena:
                reverb.reverbPreset = AudioReverbPreset.Arena;
                break;
        }
    }

    private void ExportConfiguration()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Audio Zone Config",
            "AudioZoneConfig",
            "asset",
            "Save audio zone configuration"
        );

        if (string.IsNullOrEmpty(path)) return;

        var config = ScriptableObject.CreateInstance<AudioZoneConfigData>();
        config.zones = _zones.Select(z => new AudioZoneConfigData.ZoneData
        {
            name = z.name,
            center = z.center,
            size = z.size,
            shape = z.shape.ToString(),
            radius = z.radius,
            musicTrack = z.musicTrack,
            ambianceTrack = z.ambianceTrack,
            musicVolume = z.musicVolume,
            ambianceVolume = z.ambianceVolume,
            fadeTime = z.fadeTime,
            reverbPreset = z.reverbPreset.ToString(),
            reverbMix = z.reverbMix,
            priority = z.priority
        }).ToList();

        AssetDatabase.CreateAsset(config, path);
        AssetDatabase.SaveAssets();

        Selection.activeObject = config;
        Debug.Log($"[AudioZoneManager] Exported configuration to {path}");
    }

    #endregion

    #region Audio Preview

    private void ToggleMusicPreview()
    {
        if (_isPreviewingMusic)
        {
            StopAllPreviews();
        }
        else
        {
            StopAllPreviews();
            PlayPreview(_selectedZone.musicTrack, _selectedZone.musicVolume);
            _isPreviewingMusic = true;
        }
    }

    private void ToggleAmbiancePreview()
    {
        if (_isPreviewingAmbiance)
        {
            StopAllPreviews();
        }
        else
        {
            StopAllPreviews();
            PlayPreview(_selectedZone.ambianceTrack, _selectedZone.ambianceVolume);
            _isPreviewingAmbiance = true;
        }
    }

    private void PlayPreview(AudioClip clip, float volume)
    {
        if (clip == null) return;

        // Use Unity's audio preview system
        var unityEditorAssembly = typeof(AudioImporter).Assembly;
        var audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");

        if (audioUtilClass != null)
        {
            var playClipMethod = audioUtilClass.GetMethod(
                "PlayPreviewClip",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                null,
                new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool) },
                null
            );

            if (playClipMethod != null)
            {
                playClipMethod.Invoke(null, new object[] { clip, 0, false });
            }
        }
    }

    private void StopAllPreviews()
    {
        _isPreviewingMusic = false;
        _isPreviewingAmbiance = false;

        var unityEditorAssembly = typeof(AudioImporter).Assembly;
        var audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");

        if (audioUtilClass != null)
        {
            var stopMethod = audioUtilClass.GetMethod(
                "StopAllPreviewClips",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public
            );

            if (stopMethod != null)
            {
                stopMethod.Invoke(null, null);
            }
        }
    }

    #endregion

    #region Persistence

    private void LoadZones()
    {
        string data = EditorPrefs.GetString("AudioZoneManager_Zones", "");
        if (!string.IsNullOrEmpty(data))
        {
            try
            {
                var wrapper = JsonUtility.FromJson<ZoneListWrapper>(data);
                if (wrapper != null && wrapper.zones != null)
                {
                    _zones = wrapper.zones;
                }
            }
            catch
            {
                _zones = new List<AudioZone>();
            }
        }
    }

    private void SaveZones()
    {
        var wrapper = new ZoneListWrapper { zones = _zones };
        string data = JsonUtility.ToJson(wrapper);
        EditorPrefs.SetString("AudioZoneManager_Zones", data);
    }

    private void OnDestroy()
    {
        SaveZones();
    }

    [System.Serializable]
    private class ZoneListWrapper
    {
        public List<AudioZone> zones;
    }

    #endregion

    #region Helpers

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    #endregion
}

/// <summary>
/// ScriptableObject pour stocker la configuration des zones audio.
/// </summary>
public class AudioZoneConfigData : ScriptableObject
{
    [System.Serializable]
    public class ZoneData
    {
        public string name;
        public Vector3 center;
        public Vector3 size;
        public string shape;
        public float radius;
        public AudioClip musicTrack;
        public AudioClip ambianceTrack;
        public float musicVolume;
        public float ambianceVolume;
        public float fadeTime;
        public string reverbPreset;
        public float reverbMix;
        public int priority;
    }

    public List<ZoneData> zones = new List<ZoneData>();
}
