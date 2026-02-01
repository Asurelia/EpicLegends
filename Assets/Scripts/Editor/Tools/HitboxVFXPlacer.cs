using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Visual Hitbox and VFX Placement Tool for attack animations.
/// Allows frame-by-frame placement of hitboxes, VFX spawn points, and timing.
/// </summary>
public class HitboxVFXPlacer : EditorWindow
{
    [MenuItem("EpicLegends/Tools/Hitbox & VFX Placer")]
    public static void ShowWindow()
    {
        var window = GetWindow<HitboxVFXPlacer>("Hitbox & VFX Placer");
        window.minSize = new Vector2(800, 600);
    }

    // Data structures
    [System.Serializable]
    public class HitboxData
    {
        public string name = "Hitbox";
        public HitboxType type = HitboxType.Damage;
        public HitboxShape shape = HitboxShape.Box;
        public Vector3 offset = Vector3.zero;
        public Vector3 size = Vector3.one;
        public float radius = 0.5f;
        public float height = 1f;
        public int startFrame;
        public int endFrame = 5;
        public float damage = 10f;
        public float knockback = 5f;
        public Vector3 knockbackDirection = Vector3.forward;
        public string[] hitTags = { "Enemy" };
        public Color gizmoColor = new Color(1f, 0f, 0f, 0.5f);
        public bool isActive = true;
    }

    [System.Serializable]
    public class VFXSpawnPoint
    {
        public string name = "VFX";
        public string vfxPrefabPath = "";
        public Vector3 offset = Vector3.zero;
        public Vector3 rotation = Vector3.zero;
        public Vector3 scale = Vector3.one;
        public int spawnFrame;
        public float duration = 1f;
        public bool attachToCharacter = true;
        public string attachBone = "";
        public Color previewColor = Color.cyan;
        public bool isActive = true;
    }

    [System.Serializable]
    public class SFXTrigger
    {
        public string name = "SFX";
        public string audioClipPath = "";
        public int triggerFrame;
        public float volume = 1f;
        public float pitchVariation = 0.1f;
        public bool isActive = true;
    }

    [System.Serializable]
    public class AttackSequence
    {
        public string name = "Attack 1";
        public AnimationClip clip;
        public int totalFrames = 30;
        public float frameRate = 30f;
        public List<HitboxData> hitboxes = new List<HitboxData>();
        public List<VFXSpawnPoint> vfxPoints = new List<VFXSpawnPoint>();
        public List<SFXTrigger> sfxTriggers = new List<SFXTrigger>();
        public int startupFrames = 5;
        public int activeFrames = 10;
        public int recoveryFrames = 15;
        public bool canCancel;
        public int cancelWindowStart;
        public int cancelWindowEnd;
    }

    public enum HitboxType { Damage, Guard, Parry, GrabPoint, Projectile, AoE }
    public enum HitboxShape { Box, Sphere, Capsule }

    // State
    private List<AttackSequence> _sequences = new List<AttackSequence>();
    private int _currentSequenceIndex;
    private int _selectedHitboxIndex = -1;
    private int _selectedVFXIndex = -1;
    private int _selectedSFXIndex = -1;
    private int _currentFrame;
    private bool _isPlaying;
    private float _playbackSpeed = 1f;
    private double _lastFrameTime;
    private GameObject _previewObject;
    private Animator _previewAnimator;

    // UI
    private Vector2 _leftPanelScroll;
    private Vector2 _timelineScroll;
    private float _timelineZoom = 5f;
    private int _currentTab;
    private readonly string[] _tabNames = { "Hitboxes", "VFX", "SFX", "Timing", "Export" };

    // Timeline
    private const float TIMELINE_HEIGHT = 150f;
    private const float TRACK_HEIGHT = 25f;
    private const float FRAME_WIDTH_BASE = 10f;

    private void OnEnable()
    {
        if (_sequences.Count == 0)
        {
            _sequences.Add(new AttackSequence());
        }
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        CleanupPreview();
    }

    private void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.BeginHorizontal();

        // Left panel - sequence list and properties
        DrawLeftPanel();

        // Right panel - main editor
        DrawMainPanel();

        EditorGUILayout.EndHorizontal();

        // Timeline at bottom
        DrawTimeline();

        // Handle playback
        if (_isPlaying)
        {
            UpdatePlayback();
            Repaint();
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("New Sequence", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            _sequences.Add(new AttackSequence { name = $"Attack {_sequences.Count + 1}" });
        }

        GUILayout.Space(10);

        // Sequence selection
        string[] seqNames = _sequences.Select(s => s.name).ToArray();
        int newIndex = EditorGUILayout.Popup(_currentSequenceIndex, seqNames, EditorStyles.toolbarPopup, GUILayout.Width(150));
        if (newIndex != _currentSequenceIndex)
        {
            _currentSequenceIndex = newIndex;
            _selectedHitboxIndex = -1;
            _selectedVFXIndex = -1;
            _selectedSFXIndex = -1;
            _currentFrame = 0;
        }

        GUILayout.FlexibleSpace();

        // Preview object
        EditorGUILayout.LabelField("Preview:", GUILayout.Width(50));
        var newPreview = (GameObject)EditorGUILayout.ObjectField(_previewObject, typeof(GameObject), true, GUILayout.Width(150));
        if (newPreview != _previewObject)
        {
            _previewObject = newPreview;
            if (_previewObject != null)
                _previewAnimator = _previewObject.GetComponent<Animator>();
        }

        EditorGUILayout.EndHorizontal();
    }

    private AttackSequence CurrentSequence => _sequences[Mathf.Clamp(_currentSequenceIndex, 0, _sequences.Count - 1)];

    private void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(280));
        _leftPanelScroll = EditorGUILayout.BeginScrollView(_leftPanelScroll);

        // Sequence properties
        EditorGUILayout.LabelField("Sequence Properties", EditorStyles.boldLabel);
        CurrentSequence.name = EditorGUILayout.TextField("Name", CurrentSequence.name);
        CurrentSequence.clip = (AnimationClip)EditorGUILayout.ObjectField("Animation", CurrentSequence.clip, typeof(AnimationClip), false);

        if (CurrentSequence.clip != null)
        {
            CurrentSequence.frameRate = CurrentSequence.clip.frameRate;
            CurrentSequence.totalFrames = Mathf.RoundToInt(CurrentSequence.clip.length * CurrentSequence.frameRate);
            EditorGUILayout.LabelField($"Frames: {CurrentSequence.totalFrames} @ {CurrentSequence.frameRate} FPS");
        }
        else
        {
            CurrentSequence.totalFrames = EditorGUILayout.IntField("Total Frames", CurrentSequence.totalFrames);
            CurrentSequence.frameRate = EditorGUILayout.FloatField("Frame Rate", CurrentSequence.frameRate);
        }

        EditorGUILayout.Space(10);

        // Frame timing
        EditorGUILayout.LabelField("Frame Windows", EditorStyles.boldLabel);
        CurrentSequence.startupFrames = EditorGUILayout.IntSlider("Startup", CurrentSequence.startupFrames, 0, CurrentSequence.totalFrames);
        CurrentSequence.activeFrames = EditorGUILayout.IntSlider("Active", CurrentSequence.activeFrames, 0, CurrentSequence.totalFrames);
        CurrentSequence.recoveryFrames = EditorGUILayout.IntSlider("Recovery", CurrentSequence.recoveryFrames, 0, CurrentSequence.totalFrames);

        int sum = CurrentSequence.startupFrames + CurrentSequence.activeFrames + CurrentSequence.recoveryFrames;
        if (sum != CurrentSequence.totalFrames)
        {
            EditorGUILayout.HelpBox($"Frame sum ({sum}) differs from total ({CurrentSequence.totalFrames})", MessageType.Warning);
        }

        EditorGUILayout.Space(5);
        CurrentSequence.canCancel = EditorGUILayout.Toggle("Can Cancel", CurrentSequence.canCancel);
        if (CurrentSequence.canCancel)
        {
            EditorGUILayout.BeginHorizontal();
            CurrentSequence.cancelWindowStart = EditorGUILayout.IntField("Cancel Start", CurrentSequence.cancelWindowStart);
            CurrentSequence.cancelWindowEnd = EditorGUILayout.IntField("End", CurrentSequence.cancelWindowEnd);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(20);

        // Tab content
        _currentTab = GUILayout.Toolbar(_currentTab, _tabNames.Take(4).ToArray());

        EditorGUILayout.Space(10);

        switch (_currentTab)
        {
            case 0: DrawHitboxList(); break;
            case 1: DrawVFXList(); break;
            case 2: DrawSFXList(); break;
            case 3: DrawTimingDetails(); break;
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawHitboxList()
    {
        EditorGUILayout.LabelField("Hitboxes", EditorStyles.boldLabel);

        for (int i = 0; i < CurrentSequence.hitboxes.Count; i++)
        {
            var hb = CurrentSequence.hitboxes[i];
            bool isSelected = i == _selectedHitboxIndex;

            EditorGUILayout.BeginHorizontal(isSelected ? "SelectionRect" : EditorStyles.helpBox);

            hb.isActive = EditorGUILayout.Toggle(hb.isActive, GUILayout.Width(20));

            if (GUILayout.Button(hb.name, isSelected ? EditorStyles.boldLabel : EditorStyles.label))
            {
                _selectedHitboxIndex = isSelected ? -1 : i;
                _selectedVFXIndex = -1;
                _selectedSFXIndex = -1;
            }

            hb.gizmoColor = EditorGUILayout.ColorField(GUIContent.none, hb.gizmoColor, false, true, false, GUILayout.Width(30));

            GUI.color = Color.red;
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                CurrentSequence.hitboxes.RemoveAt(i);
                if (_selectedHitboxIndex >= CurrentSequence.hitboxes.Count)
                    _selectedHitboxIndex = -1;
                break;
            }
            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();

            if (isSelected)
            {
                DrawHitboxDetails(hb);
            }
        }

        if (GUILayout.Button("+ Add Hitbox"))
        {
            CurrentSequence.hitboxes.Add(new HitboxData
            {
                name = $"Hitbox {CurrentSequence.hitboxes.Count + 1}",
                startFrame = _currentFrame,
                endFrame = Mathf.Min(_currentFrame + 5, CurrentSequence.totalFrames)
            });
            _selectedHitboxIndex = CurrentSequence.hitboxes.Count - 1;
        }
    }

    private void DrawHitboxDetails(HitboxData hb)
    {
        EditorGUI.indentLevel++;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        hb.name = EditorGUILayout.TextField("Name", hb.name);
        hb.type = (HitboxType)EditorGUILayout.EnumPopup("Type", hb.type);
        hb.shape = (HitboxShape)EditorGUILayout.EnumPopup("Shape", hb.shape);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Transform", EditorStyles.miniBoldLabel);
        hb.offset = EditorGUILayout.Vector3Field("Offset", hb.offset);

        switch (hb.shape)
        {
            case HitboxShape.Box:
                hb.size = EditorGUILayout.Vector3Field("Size", hb.size);
                break;
            case HitboxShape.Sphere:
                hb.radius = EditorGUILayout.FloatField("Radius", hb.radius);
                break;
            case HitboxShape.Capsule:
                hb.radius = EditorGUILayout.FloatField("Radius", hb.radius);
                hb.height = EditorGUILayout.FloatField("Height", hb.height);
                break;
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Timing", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        hb.startFrame = EditorGUILayout.IntField("Start", hb.startFrame);
        hb.endFrame = EditorGUILayout.IntField("End", hb.endFrame);
        EditorGUILayout.EndHorizontal();

        if (hb.type == HitboxType.Damage)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Damage", EditorStyles.miniBoldLabel);
            hb.damage = EditorGUILayout.FloatField("Damage", hb.damage);
            hb.knockback = EditorGUILayout.FloatField("Knockback", hb.knockback);
            hb.knockbackDirection = EditorGUILayout.Vector3Field("KB Direction", hb.knockbackDirection);
        }

        EditorGUILayout.EndVertical();
        EditorGUI.indentLevel--;
    }

    private void DrawVFXList()
    {
        EditorGUILayout.LabelField("VFX Spawn Points", EditorStyles.boldLabel);

        for (int i = 0; i < CurrentSequence.vfxPoints.Count; i++)
        {
            var vfx = CurrentSequence.vfxPoints[i];
            bool isSelected = i == _selectedVFXIndex;

            EditorGUILayout.BeginHorizontal(isSelected ? "SelectionRect" : EditorStyles.helpBox);

            vfx.isActive = EditorGUILayout.Toggle(vfx.isActive, GUILayout.Width(20));

            if (GUILayout.Button(vfx.name, isSelected ? EditorStyles.boldLabel : EditorStyles.label))
            {
                _selectedVFXIndex = isSelected ? -1 : i;
                _selectedHitboxIndex = -1;
                _selectedSFXIndex = -1;
            }

            vfx.previewColor = EditorGUILayout.ColorField(GUIContent.none, vfx.previewColor, false, true, false, GUILayout.Width(30));

            GUI.color = Color.red;
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                CurrentSequence.vfxPoints.RemoveAt(i);
                if (_selectedVFXIndex >= CurrentSequence.vfxPoints.Count)
                    _selectedVFXIndex = -1;
                break;
            }
            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();

            if (isSelected)
            {
                DrawVFXDetails(vfx);
            }
        }

        if (GUILayout.Button("+ Add VFX Point"))
        {
            CurrentSequence.vfxPoints.Add(new VFXSpawnPoint
            {
                name = $"VFX {CurrentSequence.vfxPoints.Count + 1}",
                spawnFrame = _currentFrame
            });
            _selectedVFXIndex = CurrentSequence.vfxPoints.Count - 1;
        }
    }

    private void DrawVFXDetails(VFXSpawnPoint vfx)
    {
        EditorGUI.indentLevel++;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        vfx.name = EditorGUILayout.TextField("Name", vfx.name);

        EditorGUILayout.BeginHorizontal();
        vfx.vfxPrefabPath = EditorGUILayout.TextField("Prefab Path", vfx.vfxPrefabPath);
        if (GUILayout.Button("...", GUILayout.Width(25)))
        {
            string path = EditorUtility.OpenFilePanel("Select VFX Prefab", "Assets", "prefab");
            if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
            {
                vfx.vfxPrefabPath = "Assets" + path.Substring(Application.dataPath.Length);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Transform", EditorStyles.miniBoldLabel);
        vfx.offset = EditorGUILayout.Vector3Field("Offset", vfx.offset);
        vfx.rotation = EditorGUILayout.Vector3Field("Rotation", vfx.rotation);
        vfx.scale = EditorGUILayout.Vector3Field("Scale", vfx.scale);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Timing", EditorStyles.miniBoldLabel);
        vfx.spawnFrame = EditorGUILayout.IntField("Spawn Frame", vfx.spawnFrame);
        vfx.duration = EditorGUILayout.FloatField("Duration", vfx.duration);

        EditorGUILayout.Space(5);
        vfx.attachToCharacter = EditorGUILayout.Toggle("Attach to Character", vfx.attachToCharacter);
        if (vfx.attachToCharacter)
        {
            vfx.attachBone = EditorGUILayout.TextField("Attach Bone", vfx.attachBone);
        }

        EditorGUILayout.EndVertical();
        EditorGUI.indentLevel--;
    }

    private void DrawSFXList()
    {
        EditorGUILayout.LabelField("SFX Triggers", EditorStyles.boldLabel);

        for (int i = 0; i < CurrentSequence.sfxTriggers.Count; i++)
        {
            var sfx = CurrentSequence.sfxTriggers[i];
            bool isSelected = i == _selectedSFXIndex;

            EditorGUILayout.BeginHorizontal(isSelected ? "SelectionRect" : EditorStyles.helpBox);

            sfx.isActive = EditorGUILayout.Toggle(sfx.isActive, GUILayout.Width(20));

            if (GUILayout.Button(sfx.name, isSelected ? EditorStyles.boldLabel : EditorStyles.label))
            {
                _selectedSFXIndex = isSelected ? -1 : i;
                _selectedHitboxIndex = -1;
                _selectedVFXIndex = -1;
            }

            GUI.color = Color.red;
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                CurrentSequence.sfxTriggers.RemoveAt(i);
                if (_selectedSFXIndex >= CurrentSequence.sfxTriggers.Count)
                    _selectedSFXIndex = -1;
                break;
            }
            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();

            if (isSelected)
            {
                DrawSFXDetails(sfx);
            }
        }

        if (GUILayout.Button("+ Add SFX Trigger"))
        {
            CurrentSequence.sfxTriggers.Add(new SFXTrigger
            {
                name = $"SFX {CurrentSequence.sfxTriggers.Count + 1}",
                triggerFrame = _currentFrame
            });
            _selectedSFXIndex = CurrentSequence.sfxTriggers.Count - 1;
        }
    }

    private void DrawSFXDetails(SFXTrigger sfx)
    {
        EditorGUI.indentLevel++;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        sfx.name = EditorGUILayout.TextField("Name", sfx.name);

        EditorGUILayout.BeginHorizontal();
        sfx.audioClipPath = EditorGUILayout.TextField("Audio Path", sfx.audioClipPath);
        if (GUILayout.Button("...", GUILayout.Width(25)))
        {
            string path = EditorUtility.OpenFilePanel("Select Audio", "Assets", "wav,mp3,ogg");
            if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
            {
                sfx.audioClipPath = "Assets" + path.Substring(Application.dataPath.Length);
            }
        }
        EditorGUILayout.EndHorizontal();

        sfx.triggerFrame = EditorGUILayout.IntField("Trigger Frame", sfx.triggerFrame);
        sfx.volume = EditorGUILayout.Slider("Volume", sfx.volume, 0f, 1f);
        sfx.pitchVariation = EditorGUILayout.Slider("Pitch Variation", sfx.pitchVariation, 0f, 0.5f);

        EditorGUILayout.EndVertical();
        EditorGUI.indentLevel--;
    }

    private void DrawTimingDetails()
    {
        EditorGUILayout.LabelField("Frame Analysis", EditorStyles.boldLabel);

        float startupTime = CurrentSequence.startupFrames / CurrentSequence.frameRate;
        float activeTime = CurrentSequence.activeFrames / CurrentSequence.frameRate;
        float recoveryTime = CurrentSequence.recoveryFrames / CurrentSequence.frameRate;
        float totalTime = CurrentSequence.totalFrames / CurrentSequence.frameRate;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Startup: {startupTime:F3}s ({CurrentSequence.startupFrames} frames)");
        EditorGUILayout.LabelField($"Active: {activeTime:F3}s ({CurrentSequence.activeFrames} frames)");
        EditorGUILayout.LabelField($"Recovery: {recoveryTime:F3}s ({CurrentSequence.recoveryFrames} frames)");
        EditorGUILayout.LabelField($"Total: {totalTime:F3}s ({CurrentSequence.totalFrames} frames)");
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // Visual timing bar
        Rect barRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(30));
        float startupWidth = barRect.width * ((float)CurrentSequence.startupFrames / CurrentSequence.totalFrames);
        float activeWidth = barRect.width * ((float)CurrentSequence.activeFrames / CurrentSequence.totalFrames);
        float recoveryWidth = barRect.width * ((float)CurrentSequence.recoveryFrames / CurrentSequence.totalFrames);

        EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, startupWidth, barRect.height), new Color(0.5f, 0.5f, 0.8f));
        EditorGUI.DrawRect(new Rect(barRect.x + startupWidth, barRect.y, activeWidth, barRect.height), new Color(0.8f, 0.3f, 0.3f));
        EditorGUI.DrawRect(new Rect(barRect.x + startupWidth + activeWidth, barRect.y, recoveryWidth, barRect.height), new Color(0.3f, 0.8f, 0.3f));

        // Labels
        GUI.Label(new Rect(barRect.x + 5, barRect.y + 5, 100, 20), "Startup", EditorStyles.miniLabel);
        GUI.Label(new Rect(barRect.x + startupWidth + 5, barRect.y + 5, 100, 20), "Active", EditorStyles.miniLabel);
        GUI.Label(new Rect(barRect.x + startupWidth + activeWidth + 5, barRect.y + 5, 100, 20), "Recovery", EditorStyles.miniLabel);

        EditorGUILayout.Space(10);

        // Quick presets
        EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Light Attack"))
        {
            CurrentSequence.startupFrames = 3;
            CurrentSequence.activeFrames = 4;
            CurrentSequence.recoveryFrames = 8;
            CurrentSequence.totalFrames = 15;
        }
        if (GUILayout.Button("Heavy Attack"))
        {
            CurrentSequence.startupFrames = 12;
            CurrentSequence.activeFrames = 6;
            CurrentSequence.recoveryFrames = 18;
            CurrentSequence.totalFrames = 36;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Skill"))
        {
            CurrentSequence.startupFrames = 8;
            CurrentSequence.activeFrames = 15;
            CurrentSequence.recoveryFrames = 12;
            CurrentSequence.totalFrames = 35;
        }
        if (GUILayout.Button("Burst"))
        {
            CurrentSequence.startupFrames = 15;
            CurrentSequence.activeFrames = 45;
            CurrentSequence.recoveryFrames = 20;
            CurrentSequence.totalFrames = 80;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawMainPanel()
    {
        EditorGUILayout.BeginVertical();

        // 3D preview area
        Rect previewRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(300));
        EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f));

        if (_previewObject == null)
        {
            GUI.Label(new Rect(previewRect.center.x - 100, previewRect.center.y - 10, 200, 20),
                     "Assign a Preview Object in toolbar", EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            GUI.Label(new Rect(previewRect.x + 10, previewRect.y + 10, 200, 20),
                     $"Preview: {_previewObject.name}", EditorStyles.boldLabel);
            GUI.Label(new Rect(previewRect.x + 10, previewRect.y + 30, 200, 20),
                     $"Frame: {_currentFrame} / {CurrentSequence.totalFrames}", EditorStyles.label);
        }

        // Playback controls
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("|<", GUILayout.Width(30)))
            _currentFrame = 0;
        if (GUILayout.Button("<", GUILayout.Width(30)))
            _currentFrame = Mathf.Max(0, _currentFrame - 1);
        if (GUILayout.Button(_isPlaying ? "||" : ">", GUILayout.Width(40)))
            _isPlaying = !_isPlaying;
        if (GUILayout.Button(">", GUILayout.Width(30)))
            _currentFrame = Mathf.Min(CurrentSequence.totalFrames - 1, _currentFrame + 1);
        if (GUILayout.Button(">|", GUILayout.Width(30)))
            _currentFrame = CurrentSequence.totalFrames - 1;

        GUILayout.Space(20);
        EditorGUILayout.LabelField("Speed:", GUILayout.Width(45));
        _playbackSpeed = EditorGUILayout.Slider(_playbackSpeed, 0.1f, 2f, GUILayout.Width(100));

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        // Frame scrubber
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Frame:", GUILayout.Width(50));
        _currentFrame = EditorGUILayout.IntSlider(_currentFrame, 0, Mathf.Max(1, CurrentSequence.totalFrames - 1));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Export section
        EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate Script", GUILayout.Height(30)))
            GenerateAttackScript();
        if (GUILayout.Button("Save JSON", GUILayout.Height(30)))
            SaveToJson();
        if (GUILayout.Button("Load JSON", GUILayout.Height(30)))
            LoadFromJson();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawTimeline()
    {
        Rect timelineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
            GUILayout.ExpandWidth(true), GUILayout.Height(TIMELINE_HEIGHT));

        EditorGUI.DrawRect(timelineRect, new Color(0.15f, 0.15f, 0.15f));

        float frameWidth = FRAME_WIDTH_BASE * _timelineZoom;
        float totalWidth = CurrentSequence.totalFrames * frameWidth;

        // Zoom controls
        Rect zoomRect = new Rect(timelineRect.x + 5, timelineRect.y + 5, 100, 20);
        EditorGUI.LabelField(zoomRect, $"Zoom: {_timelineZoom:F1}x");

        Event e = Event.current;
        if (timelineRect.Contains(e.mousePosition) && e.type == UnityEngine.EventType.ScrollWheel)
        {
            _timelineZoom = Mathf.Clamp(_timelineZoom - e.delta.y * 0.1f, 1f, 20f);
            e.Use();
            Repaint();
        }

        // Timeline content area
        Rect contentRect = new Rect(timelineRect.x, timelineRect.y + 25, timelineRect.width, TIMELINE_HEIGHT - 25);
        _timelineScroll = GUI.BeginScrollView(contentRect, _timelineScroll, new Rect(0, 0, totalWidth, TIMELINE_HEIGHT - 35));

        // Frame markers
        for (int i = 0; i <= CurrentSequence.totalFrames; i += 5)
        {
            float x = i * frameWidth;
            GUI.Label(new Rect(x, 0, 30, 15), i.ToString(), EditorStyles.miniLabel);
            EditorGUI.DrawRect(new Rect(x, 15, 1, TIMELINE_HEIGHT - 50), Color.gray * 0.5f);
        }

        float trackY = 20;

        // Timing track
        DrawTimingTrack(trackY, frameWidth);
        trackY += TRACK_HEIGHT;

        // Hitbox tracks
        DrawTrackLabel(trackY, "Hitboxes");
        foreach (var hb in CurrentSequence.hitboxes)
        {
            if (hb.isActive)
            {
                DrawTimelineClip(trackY, hb.startFrame, hb.endFrame, frameWidth, hb.gizmoColor, hb.name);
            }
        }
        trackY += TRACK_HEIGHT;

        // VFX tracks
        DrawTrackLabel(trackY, "VFX");
        foreach (var vfx in CurrentSequence.vfxPoints)
        {
            if (vfx.isActive)
            {
                int endFrame = vfx.spawnFrame + Mathf.RoundToInt(vfx.duration * CurrentSequence.frameRate);
                DrawTimelineClip(trackY, vfx.spawnFrame, endFrame, frameWidth, vfx.previewColor, vfx.name);
            }
        }
        trackY += TRACK_HEIGHT;

        // SFX markers
        DrawTrackLabel(trackY, "SFX");
        foreach (var sfx in CurrentSequence.sfxTriggers)
        {
            if (sfx.isActive)
            {
                float x = sfx.triggerFrame * frameWidth;
                EditorGUI.DrawRect(new Rect(x - 2, trackY, 4, TRACK_HEIGHT - 5), Color.yellow);
                GUI.Label(new Rect(x + 5, trackY, 100, TRACK_HEIGHT), sfx.name, EditorStyles.miniLabel);
            }
        }

        // Playhead
        float playheadX = _currentFrame * frameWidth;
        EditorGUI.DrawRect(new Rect(playheadX, 0, 2, TIMELINE_HEIGHT - 35), Color.red);

        // Handle click to seek
        if (e.type == UnityEngine.EventType.MouseDown && e.button == 0)
        {
            _currentFrame = Mathf.Clamp(Mathf.RoundToInt(e.mousePosition.x / frameWidth), 0, CurrentSequence.totalFrames - 1);
            e.Use();
            Repaint();
        }

        GUI.EndScrollView();
    }

    private void DrawTimingTrack(float y, float frameWidth)
    {
        float startupWidth = CurrentSequence.startupFrames * frameWidth;
        float activeWidth = CurrentSequence.activeFrames * frameWidth;
        float recoveryWidth = CurrentSequence.recoveryFrames * frameWidth;

        EditorGUI.DrawRect(new Rect(0, y, startupWidth, TRACK_HEIGHT - 5), new Color(0.3f, 0.3f, 0.6f, 0.8f));
        EditorGUI.DrawRect(new Rect(startupWidth, y, activeWidth, TRACK_HEIGHT - 5), new Color(0.6f, 0.2f, 0.2f, 0.8f));
        EditorGUI.DrawRect(new Rect(startupWidth + activeWidth, y, recoveryWidth, TRACK_HEIGHT - 5), new Color(0.2f, 0.6f, 0.2f, 0.8f));

        // Cancel window
        if (CurrentSequence.canCancel)
        {
            float cancelStart = CurrentSequence.cancelWindowStart * frameWidth;
            float cancelWidth = (CurrentSequence.cancelWindowEnd - CurrentSequence.cancelWindowStart) * frameWidth;
            EditorGUI.DrawRect(new Rect(cancelStart, y + TRACK_HEIGHT - 8, cancelWidth, 3), Color.yellow);
        }
    }

    private void DrawTrackLabel(float y, string label)
    {
        GUI.Label(new Rect(5, y, 60, TRACK_HEIGHT), label, EditorStyles.miniLabel);
    }

    private void DrawTimelineClip(float y, int startFrame, int endFrame, float frameWidth, Color color, string label)
    {
        float x = startFrame * frameWidth;
        float width = (endFrame - startFrame) * frameWidth;

        EditorGUI.DrawRect(new Rect(x, y, width, TRACK_HEIGHT - 5), color);
        GUI.Label(new Rect(x + 2, y + 2, width - 4, TRACK_HEIGHT - 5), label,
                 new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip });
    }

    private void UpdatePlayback()
    {
        double currentTime = EditorApplication.timeSinceStartup;
        float frameDuration = 1f / (CurrentSequence.frameRate * _playbackSpeed);

        if (currentTime - _lastFrameTime >= frameDuration)
        {
            _currentFrame++;
            if (_currentFrame >= CurrentSequence.totalFrames)
                _currentFrame = 0;
            _lastFrameTime = currentTime;

            UpdatePreviewAnimation();
        }
    }

    private void UpdatePreviewAnimation()
    {
        if (_previewAnimator == null || CurrentSequence.clip == null) return;

        float normalizedTime = (float)_currentFrame / CurrentSequence.totalFrames;
        CurrentSequence.clip.SampleAnimation(_previewObject, normalizedTime * CurrentSequence.clip.length);
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (_previewObject == null) return;

        Handles.color = Color.white;

        // Draw hitboxes
        foreach (var hb in CurrentSequence.hitboxes)
        {
            if (!hb.isActive) continue;
            if (_currentFrame < hb.startFrame || _currentFrame > hb.endFrame) continue;

            Vector3 worldPos = _previewObject.transform.TransformPoint(hb.offset);
            Handles.color = hb.gizmoColor;

            switch (hb.shape)
            {
                case HitboxShape.Box:
                    Matrix4x4 matrix = Matrix4x4.TRS(worldPos, _previewObject.transform.rotation, hb.size);
                    using (new Handles.DrawingScope(matrix))
                    {
                        Handles.DrawWireCube(Vector3.zero, Vector3.one);
                    }
                    break;
                case HitboxShape.Sphere:
                    Handles.DrawWireDisc(worldPos, Vector3.up, hb.radius);
                    Handles.DrawWireDisc(worldPos, Vector3.forward, hb.radius);
                    Handles.DrawWireDisc(worldPos, Vector3.right, hb.radius);
                    break;
                case HitboxShape.Capsule:
                    DrawWireCapsule(worldPos, hb.radius, hb.height);
                    break;
            }

            Handles.Label(worldPos + Vector3.up * 0.5f, hb.name);
        }

        // Draw VFX points
        foreach (var vfx in CurrentSequence.vfxPoints)
        {
            if (!vfx.isActive) continue;
            if (_currentFrame != vfx.spawnFrame) continue;

            Vector3 worldPos = _previewObject.transform.TransformPoint(vfx.offset);
            Handles.color = vfx.previewColor;
            Handles.DrawWireCube(worldPos, Vector3.one * 0.2f);
            Handles.DrawDottedLine(worldPos, worldPos + Quaternion.Euler(vfx.rotation) * Vector3.forward * 0.5f, 2f);
            Handles.Label(worldPos + Vector3.up * 0.3f, vfx.name);
        }

        sceneView.Repaint();
    }

    private void DrawWireCapsule(Vector3 center, float radius, float height)
    {
        float halfHeight = height / 2f - radius;
        Vector3 top = center + Vector3.up * halfHeight;
        Vector3 bottom = center - Vector3.up * halfHeight;

        Handles.DrawWireDisc(top, Vector3.up, radius);
        Handles.DrawWireDisc(bottom, Vector3.up, radius);

        Handles.DrawWireArc(top, Vector3.forward, Vector3.right, 180f, radius);
        Handles.DrawWireArc(top, Vector3.right, -Vector3.forward, 180f, radius);
        Handles.DrawWireArc(bottom, Vector3.forward, Vector3.right, -180f, radius);
        Handles.DrawWireArc(bottom, Vector3.right, -Vector3.forward, -180f, radius);

        Handles.DrawLine(top + Vector3.forward * radius, bottom + Vector3.forward * radius);
        Handles.DrawLine(top - Vector3.forward * radius, bottom - Vector3.forward * radius);
        Handles.DrawLine(top + Vector3.right * radius, bottom + Vector3.right * radius);
        Handles.DrawLine(top - Vector3.right * radius, bottom - Vector3.right * radius);
    }

    private void CleanupPreview()
    {
        // Cleanup if needed
    }

    private void GenerateAttackScript()
    {
        string script = GenerateAttackDataScript();
        string path = EditorUtility.SaveFilePanel("Save Attack Script", "Assets/Scripts", $"{CurrentSequence.name}Data", "cs");

        if (!string.IsNullOrEmpty(path))
        {
            System.IO.File.WriteAllText(path, script);
            AssetDatabase.Refresh();
            Debug.Log($"Attack script saved to: {path}");
        }
    }

    private string GenerateAttackDataScript()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"[CreateAssetMenu(fileName = \"{CurrentSequence.name}\", menuName = \"EpicLegends/Attack Data/{CurrentSequence.name}\")]");
        sb.AppendLine($"public class {CurrentSequence.name.Replace(" ", "")}Data : ScriptableObject");
        sb.AppendLine("{");
        sb.AppendLine($"    public AnimationClip animationClip;");
        sb.AppendLine($"    public int totalFrames = {CurrentSequence.totalFrames};");
        sb.AppendLine($"    public float frameRate = {CurrentSequence.frameRate}f;");
        sb.AppendLine($"    public int startupFrames = {CurrentSequence.startupFrames};");
        sb.AppendLine($"    public int activeFrames = {CurrentSequence.activeFrames};");
        sb.AppendLine($"    public int recoveryFrames = {CurrentSequence.recoveryFrames};");
        sb.AppendLine();

        // Hitboxes
        sb.AppendLine("    [System.Serializable]");
        sb.AppendLine("    public class HitboxInfo");
        sb.AppendLine("    {");
        sb.AppendLine("        public string name;");
        sb.AppendLine("        public Vector3 offset;");
        sb.AppendLine("        public Vector3 size;");
        sb.AppendLine("        public float radius;");
        sb.AppendLine("        public int startFrame;");
        sb.AppendLine("        public int endFrame;");
        sb.AppendLine("        public float damage;");
        sb.AppendLine("        public float knockback;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public List<HitboxInfo> hitboxes = new List<HitboxInfo>()");
        sb.AppendLine("    {");
        foreach (var hb in CurrentSequence.hitboxes)
        {
            sb.AppendLine($"        new HitboxInfo {{ name = \"{hb.name}\", offset = new Vector3({hb.offset.x}f, {hb.offset.y}f, {hb.offset.z}f), " +
                         $"size = new Vector3({hb.size.x}f, {hb.size.y}f, {hb.size.z}f), radius = {hb.radius}f, " +
                         $"startFrame = {hb.startFrame}, endFrame = {hb.endFrame}, damage = {hb.damage}f, knockback = {hb.knockback}f }},");
        }
        sb.AppendLine("    };");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private void SaveToJson()
    {
        string path = EditorUtility.SaveFilePanel("Save Attack Sequences", "", "AttackSequences", "json");
        if (string.IsNullOrEmpty(path)) return;

        var saveData = new AttackSequencesSaveData { sequences = _sequences };
        string json = JsonUtility.ToJson(saveData, true);
        System.IO.File.WriteAllText(path, json);
        Debug.Log($"Saved to: {path}");
    }

    private void LoadFromJson()
    {
        string path = EditorUtility.OpenFilePanel("Load Attack Sequences", "", "json");
        if (string.IsNullOrEmpty(path)) return;

        string json = System.IO.File.ReadAllText(path);
        var saveData = JsonUtility.FromJson<AttackSequencesSaveData>(json);
        _sequences = saveData.sequences;
        _currentSequenceIndex = 0;
        Debug.Log($"Loaded from: {path}");
    }

    [System.Serializable]
    private class AttackSequencesSaveData
    {
        public List<AttackSequence> sequences;
    }
}
