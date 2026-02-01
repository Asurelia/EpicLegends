using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EpicLegends.Editor.Tools
{
    /// <summary>
    /// Séquenceur de cinématiques
    /// Création de cutscenes avec caméra, dialogue et animations
    /// </summary>
    public class CutsceneSequencer : EditorWindow
    {
        [MenuItem("EpicLegends/Tools/Cutscene Sequencer")]
        public static void ShowWindow()
        {
            var window = GetWindow<CutsceneSequencer>("Cutscene Sequencer");
            window.minSize = new Vector2(1000, 700);
        }

        // Enums
        private enum TrackType
        {
            Camera, Character, Dialogue, Audio, Animation,
            VFX, Lighting, Event, Fade, Letterbox
        }

        private enum CameraMove { Cut, Dolly, Pan, Orbit, Crane, Handheld }
        private enum DialoguePosition { Bottom, Top, Left, Right, Center }
        private enum FadeType { FadeIn, FadeOut, CrossFade }

        // State
        private Vector2 _scrollPos;
        private CutsceneData _cutscene;
        private int _selectedTrackIdx = -1;
        private int _selectedClipIdx = -1;

        // Timeline
        private float _timelineZoom = 30f;
        private float _timelineOffset = 0f;
        private Rect _timelineRect;
        private float _playheadTime = 0f;
        private bool _isPlaying;
        private double _lastUpdateTime;

        // Track heights
        private const float TRACK_HEIGHT = 35f;
        private const float HEADER_WIDTH = 150f;

        // Track colors
        private readonly Dictionary<TrackType, Color> _trackColors = new Dictionary<TrackType, Color>
        {
            { TrackType.Camera, new Color(0.8f, 0.4f, 0.4f) },
            { TrackType.Character, new Color(0.4f, 0.7f, 0.9f) },
            { TrackType.Dialogue, new Color(0.9f, 0.8f, 0.3f) },
            { TrackType.Audio, new Color(0.4f, 0.9f, 0.4f) },
            { TrackType.Animation, new Color(0.9f, 0.5f, 0.9f) },
            { TrackType.VFX, new Color(1f, 0.6f, 0.3f) },
            { TrackType.Lighting, new Color(1f, 1f, 0.5f) },
            { TrackType.Event, new Color(0.5f, 0.5f, 0.9f) },
            { TrackType.Fade, new Color(0.3f, 0.3f, 0.3f) },
            { TrackType.Letterbox, new Color(0.2f, 0.2f, 0.2f) }
        };

        private void OnEnable()
        {
            if (_cutscene == null)
                CreateNewCutscene();

            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (_isPlaying && _cutscene != null)
            {
                double now = EditorApplication.timeSinceStartup;
                float delta = (float)(now - _lastUpdateTime);
                _lastUpdateTime = now;

                _playheadTime += delta;

                if (_playheadTime >= _cutscene.Duration)
                {
                    _playheadTime = 0;
                    _isPlaying = false;
                }

                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🎬 Cutscene Sequencer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Create cinematic sequences with camera, dialogue, and effects", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // Toolbar
            DrawToolbar();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            // Left - Track list
            EditorGUILayout.BeginVertical(GUILayout.Width(HEADER_WIDTH));
            DrawTrackHeaders();
            EditorGUILayout.EndVertical();

            // Center - Timeline
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawTimeline();
            EditorGUILayout.EndVertical();

            // Right - Properties
            EditorGUILayout.BeginVertical(GUILayout.Width(280));
            DrawProperties();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // File operations
            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(40)))
                CreateNewCutscene();

            if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(40)))
                LoadCutscene();

            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(40)))
                SaveCutscene();

            GUILayout.Space(20);

            // Playback controls
            if (GUILayout.Button("⏮", EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                _playheadTime = 0;
                _isPlaying = false;
            }

            if (GUILayout.Button(_isPlaying ? "⏸" : "▶", EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                _isPlaying = !_isPlaying;
                _lastUpdateTime = EditorApplication.timeSinceStartup;
            }

            if (GUILayout.Button("⏭", EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                _playheadTime = _cutscene.Duration;
                _isPlaying = false;
            }

            EditorGUILayout.LabelField($"Time: {_playheadTime:F2}s / {_cutscene.Duration:F2}s", GUILayout.Width(150));

            GUILayout.FlexibleSpace();

            // Duration
            EditorGUILayout.LabelField("Duration:", GUILayout.Width(55));
            _cutscene.Duration = EditorGUILayout.FloatField(_cutscene.Duration, GUILayout.Width(50));

            GUILayout.Space(10);

            // Zoom
            EditorGUILayout.LabelField("Zoom:", GUILayout.Width(40));
            _timelineZoom = EditorGUILayout.Slider(_timelineZoom, 10f, 100f, GUILayout.Width(100));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTrackHeaders()
        {
            EditorGUILayout.LabelField("Tracks", EditorStyles.boldLabel);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            for (int i = 0; i < _cutscene.Tracks.Count; i++)
            {
                var track = _cutscene.Tracks[i];
                bool isSelected = i == _selectedTrackIdx;

                Color trackColor = _trackColors.GetValueOrDefault(track.Type, Color.gray);
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = isSelected ? trackColor : Color.Lerp(trackColor, Color.gray, 0.5f);

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(TRACK_HEIGHT - 5));
                GUI.backgroundColor = prevBg;

                // Track visibility
                track.IsVisible = EditorGUILayout.Toggle(track.IsVisible, GUILayout.Width(15));

                // Track name
                if (GUILayout.Button(track.Name, EditorStyles.label, GUILayout.ExpandWidth(true)))
                {
                    _selectedTrackIdx = i;
                    _selectedClipIdx = -1;
                }

                // Lock toggle
                track.IsLocked = GUILayout.Toggle(track.IsLocked, "🔒", "Button", GUILayout.Width(25));

                // Delete
                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _cutscene.Tracks.RemoveAt(i);
                    if (_selectedTrackIdx >= _cutscene.Tracks.Count)
                        _selectedTrackIdx = -1;
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            // Add track dropdown
            if (GUILayout.Button("+ Add Track"))
            {
                GenericMenu menu = new GenericMenu();
                foreach (TrackType type in Enum.GetValues(typeof(TrackType)))
                {
                    TrackType t = type;
                    menu.AddItem(new GUIContent(type.ToString()), false, () => AddTrack(t));
                }
                menu.ShowAsContext();
            }
        }

        private void DrawTimeline()
        {
            // Time ruler
            Rect rulerRect = GUILayoutUtility.GetRect(100, 25, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rulerRect, new Color(0.25f, 0.25f, 0.25f));
            DrawTimeRuler(rulerRect);

            // Tracks area
            _timelineRect = GUILayoutUtility.GetRect(100, _cutscene.Tracks.Count * TRACK_HEIGHT + 50, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(_timelineRect, new Color(0.15f, 0.15f, 0.15f));

            float trackY = _timelineRect.y;

            for (int trackIdx = 0; trackIdx < _cutscene.Tracks.Count; trackIdx++)
            {
                var track = _cutscene.Tracks[trackIdx];

                // Track background
                Rect trackRect = new Rect(_timelineRect.x, trackY, _timelineRect.width, TRACK_HEIGHT - 2);
                Color trackBg = _trackColors.GetValueOrDefault(track.Type, Color.gray);
                trackBg.a = 0.2f;
                EditorGUI.DrawRect(trackRect, trackBg);

                // Draw clips
                for (int clipIdx = 0; clipIdx < track.Clips.Count; clipIdx++)
                {
                    var clip = track.Clips[clipIdx];
                    DrawClip(trackRect, trackIdx, clipIdx, clip, track.Type);
                }

                // Track separator
                EditorGUI.DrawRect(new Rect(_timelineRect.x, trackY + TRACK_HEIGHT - 2, _timelineRect.width, 1), new Color(0.3f, 0.3f, 0.3f));

                trackY += TRACK_HEIGHT;
            }

            // Playhead
            float playheadX = _timelineRect.x + (_playheadTime - _timelineOffset) * _timelineZoom;
            if (playheadX >= _timelineRect.x && playheadX <= _timelineRect.xMax)
            {
                EditorGUI.DrawRect(new Rect(playheadX - 1, rulerRect.y, 2, _timelineRect.yMax - rulerRect.y), Color.red);

                // Playhead handle
                Handles.color = Color.red;
                Vector3[] triangle = new Vector3[]
                {
                    new Vector3(playheadX - 6, rulerRect.y, 0),
                    new Vector3(playheadX + 6, rulerRect.y, 0),
                    new Vector3(playheadX, rulerRect.y + 10, 0)
                };
                Handles.DrawAAConvexPolygon(triangle);
            }

            // Handle timeline click for playhead
            if (Event.current.type == UnityEngine.EventType.MouseDown && rulerRect.Contains(Event.current.mousePosition))
            {
                _playheadTime = (Event.current.mousePosition.x - _timelineRect.x) / _timelineZoom + _timelineOffset;
                _playheadTime = Mathf.Clamp(_playheadTime, 0, _cutscene.Duration);
                Event.current.Use();
                Repaint();
            }

            // Context menu for adding clips
            if (Event.current.type == UnityEngine.EventType.ContextClick && _timelineRect.Contains(Event.current.mousePosition))
            {
                float clickTime = (Event.current.mousePosition.x - _timelineRect.x) / _timelineZoom + _timelineOffset;
                int clickTrack = Mathf.FloorToInt((Event.current.mousePosition.y - _timelineRect.y) / TRACK_HEIGHT);

                if (clickTrack >= 0 && clickTrack < _cutscene.Tracks.Count)
                {
                    ShowAddClipMenu(clickTrack, clickTime);
                    Event.current.Use();
                }
            }
        }

        private void DrawTimeRuler(Rect rect)
        {
            float step = 1f;
            if (_timelineZoom < 20) step = 5f;
            if (_timelineZoom < 10) step = 10f;

            for (float t = 0; t <= _cutscene.Duration + step; t += step)
            {
                float x = rect.x + (t - _timelineOffset) * _timelineZoom;
                if (x < rect.x || x > rect.xMax) continue;

                bool isMajor = Mathf.Abs(t % 5) < 0.001f;

                EditorGUI.DrawRect(new Rect(x, rect.yMax - (isMajor ? 10 : 5), 1, isMajor ? 10 : 5), Color.gray);

                if (isMajor || _timelineZoom > 30)
                {
                    GUI.Label(new Rect(x - 15, rect.y + 2, 30, 15), $"{t:F0}s", EditorStyles.centeredGreyMiniLabel);
                }
            }
        }

        private void DrawClip(Rect trackRect, int trackIdx, int clipIdx, CutsceneClip clip, TrackType type)
        {
            float x = trackRect.x + (clip.StartTime - _timelineOffset) * _timelineZoom;
            float width = Mathf.Max(clip.Duration * _timelineZoom, 20);

            if (x + width < trackRect.x || x > trackRect.xMax)
                return;

            Rect clipRect = new Rect(x, trackRect.y + 2, width, trackRect.height - 4);

            bool isSelected = trackIdx == _selectedTrackIdx && clipIdx == _selectedClipIdx;
            Color color = _trackColors.GetValueOrDefault(type, Color.gray);
            if (isSelected) color = Color.Lerp(color, Color.white, 0.3f);

            EditorGUI.DrawRect(clipRect, color);

            // Clip border
            Handles.color = isSelected ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            Handles.DrawWireCube(clipRect.center, new Vector3(clipRect.width, clipRect.height, 0));

            // Clip label
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                clipping = TextClipping.Clip
            };
            GUI.Label(clipRect, clip.Name, style);

            // Selection handling
            if (Event.current.type == UnityEngine.EventType.MouseDown && clipRect.Contains(Event.current.mousePosition))
            {
                _selectedTrackIdx = trackIdx;
                _selectedClipIdx = clipIdx;
                Event.current.Use();
                Repaint();
            }
        }

        private void DrawProperties()
        {
            EditorGUILayout.LabelField("Properties", EditorStyles.boldLabel);

            if (_cutscene == null)
            {
                EditorGUILayout.HelpBox("No cutscene loaded", MessageType.Info);
                return;
            }

            // Cutscene properties
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Cutscene", EditorStyles.miniBoldLabel);
            _cutscene.Name = EditorGUILayout.TextField("Name", _cutscene.Name);
            _cutscene.Duration = EditorGUILayout.FloatField("Duration", _cutscene.Duration);
            _cutscene.IsSkippable = EditorGUILayout.Toggle("Skippable", _cutscene.IsSkippable);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Selected clip properties
            if (_selectedTrackIdx >= 0 && _selectedTrackIdx < _cutscene.Tracks.Count)
            {
                var track = _cutscene.Tracks[_selectedTrackIdx];

                if (_selectedClipIdx >= 0 && _selectedClipIdx < track.Clips.Count)
                {
                    var clip = track.Clips[_selectedClipIdx];

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"Clip: {clip.Name}", EditorStyles.boldLabel);

                    clip.Name = EditorGUILayout.TextField("Name", clip.Name);
                    clip.StartTime = EditorGUILayout.FloatField("Start Time", clip.StartTime);
                    clip.Duration = EditorGUILayout.FloatField("Duration", clip.Duration);

                    EditorGUILayout.Space(5);

                    // Type-specific properties
                    DrawClipProperties(clip, track.Type);

                    EditorGUILayout.Space(5);

                    // Clip actions
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Duplicate"))
                    {
                        var dup = clip.Clone();
                        dup.StartTime += clip.Duration;
                        track.Clips.Add(dup);
                        _selectedClipIdx = track.Clips.Count - 1;
                    }
                    if (GUILayout.Button("Delete"))
                    {
                        track.Clips.RemoveAt(_selectedClipIdx);
                        _selectedClipIdx = -1;
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.EndVertical();
                }
                else
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"Track: {track.Name}", EditorStyles.boldLabel);
                    track.Name = EditorGUILayout.TextField("Name", track.Name);

                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Right-click timeline to add clips", EditorStyles.miniLabel);
                    EditorGUILayout.EndVertical();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Select a track or clip to edit", MessageType.Info);
            }

            EditorGUILayout.Space(10);

            // Quick presets
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);

            if (GUILayout.Button("Story Intro"))
                LoadPreset_StoryIntro();
            if (GUILayout.Button("Boss Entrance"))
                LoadPreset_BossEntrance();
            if (GUILayout.Button("Victory Scene"))
                LoadPreset_Victory();
        }

        private void DrawClipProperties(CutsceneClip clip, TrackType type)
        {
            switch (type)
            {
                case TrackType.Camera:
                    DrawCameraProperties(clip);
                    break;
                case TrackType.Dialogue:
                    DrawDialogueProperties(clip);
                    break;
                case TrackType.Character:
                    DrawCharacterProperties(clip);
                    break;
                case TrackType.Audio:
                    DrawAudioProperties(clip);
                    break;
                case TrackType.Animation:
                    DrawAnimationProperties(clip);
                    break;
                case TrackType.VFX:
                    DrawVFXProperties(clip);
                    break;
                case TrackType.Fade:
                    DrawFadeProperties(clip);
                    break;
                case TrackType.Event:
                    DrawEventProperties(clip);
                    break;
            }
        }

        private void DrawCameraProperties(CutsceneClip clip)
        {
            EditorGUILayout.LabelField("Camera", EditorStyles.miniBoldLabel);
            clip.CameraMove = (CameraMove)EditorGUILayout.EnumPopup("Movement", clip.CameraMove);
            clip.CameraTarget = (Transform)EditorGUILayout.ObjectField("Target", clip.CameraTarget, typeof(Transform), true);
            clip.CameraOffset = EditorGUILayout.Vector3Field("Offset", clip.CameraOffset);
            clip.FieldOfView = EditorGUILayout.Slider("FOV", clip.FieldOfView, 20f, 120f);
            clip.EaseIn = EditorGUILayout.Toggle("Ease In", clip.EaseIn);
            clip.EaseOut = EditorGUILayout.Toggle("Ease Out", clip.EaseOut);
        }

        private void DrawDialogueProperties(CutsceneClip clip)
        {
            EditorGUILayout.LabelField("Dialogue", EditorStyles.miniBoldLabel);
            clip.SpeakerName = EditorGUILayout.TextField("Speaker", clip.SpeakerName);
            EditorGUILayout.LabelField("Text:");
            clip.DialogueText = EditorGUILayout.TextArea(clip.DialogueText, GUILayout.Height(60));
            clip.DialoguePosition = (DialoguePosition)EditorGUILayout.EnumPopup("Position", clip.DialoguePosition);
            clip.VoiceClip = (AudioClip)EditorGUILayout.ObjectField("Voice", clip.VoiceClip, typeof(AudioClip), false);
            clip.TypewriterSpeed = EditorGUILayout.FloatField("Typewriter Speed", clip.TypewriterSpeed);
        }

        private void DrawCharacterProperties(CutsceneClip clip)
        {
            EditorGUILayout.LabelField("Character", EditorStyles.miniBoldLabel);
            clip.CharacterObject = (GameObject)EditorGUILayout.ObjectField("Character", clip.CharacterObject, typeof(GameObject), true);
            clip.TargetPosition = EditorGUILayout.Vector3Field("Move To", clip.TargetPosition);
            clip.TargetRotation = EditorGUILayout.Vector3Field("Rotate To", clip.TargetRotation);
            clip.UsePathfinding = EditorGUILayout.Toggle("Use Pathfinding", clip.UsePathfinding);
        }

        private void DrawAudioProperties(CutsceneClip clip)
        {
            EditorGUILayout.LabelField("Audio", EditorStyles.miniBoldLabel);
            clip.AudioClip = (AudioClip)EditorGUILayout.ObjectField("Clip", clip.AudioClip, typeof(AudioClip), false);
            clip.Volume = EditorGUILayout.Slider("Volume", clip.Volume, 0f, 1f);
            clip.FadeInDuration = EditorGUILayout.FloatField("Fade In", clip.FadeInDuration);
            clip.FadeOutDuration = EditorGUILayout.FloatField("Fade Out", clip.FadeOutDuration);
            clip.IsMusic = EditorGUILayout.Toggle("Is Music", clip.IsMusic);
        }

        private void DrawAnimationProperties(CutsceneClip clip)
        {
            EditorGUILayout.LabelField("Animation", EditorStyles.miniBoldLabel);
            clip.AnimationClip = (AnimationClip)EditorGUILayout.ObjectField("Clip", clip.AnimationClip, typeof(AnimationClip), false);
            clip.AnimationTarget = (Animator)EditorGUILayout.ObjectField("Animator", clip.AnimationTarget, typeof(Animator), true);
            clip.AnimationSpeed = EditorGUILayout.FloatField("Speed", clip.AnimationSpeed);
            clip.CrossFadeDuration = EditorGUILayout.FloatField("CrossFade", clip.CrossFadeDuration);
        }

        private void DrawVFXProperties(CutsceneClip clip)
        {
            EditorGUILayout.LabelField("VFX", EditorStyles.miniBoldLabel);
            clip.VFXPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", clip.VFXPrefab, typeof(GameObject), false);
            clip.VFXPosition = EditorGUILayout.Vector3Field("Position", clip.VFXPosition);
            clip.VFXScale = EditorGUILayout.FloatField("Scale", clip.VFXScale);
            clip.AttachToTarget = EditorGUILayout.Toggle("Attach to Target", clip.AttachToTarget);
        }

        private void DrawFadeProperties(CutsceneClip clip)
        {
            EditorGUILayout.LabelField("Fade", EditorStyles.miniBoldLabel);
            clip.FadeType = (FadeType)EditorGUILayout.EnumPopup("Type", clip.FadeType);
            clip.FadeColor = EditorGUILayout.ColorField("Color", clip.FadeColor);
        }

        private void DrawEventProperties(CutsceneClip clip)
        {
            EditorGUILayout.LabelField("Event", EditorStyles.miniBoldLabel);
            clip.EventName = EditorGUILayout.TextField("Event Name", clip.EventName);
            clip.EventParameter = EditorGUILayout.TextField("Parameter", clip.EventParameter);
        }

        private void AddTrack(TrackType type)
        {
            _cutscene.Tracks.Add(new CutsceneTrack
            {
                Name = type.ToString(),
                Type = type,
                IsVisible = true,
                IsLocked = false
            });
        }

        private void ShowAddClipMenu(int trackIdx, float time)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Add Clip"), false, () => AddClip(trackIdx, time));
            menu.ShowAsContext();
        }

        private void AddClip(int trackIdx, float time)
        {
            var track = _cutscene.Tracks[trackIdx];
            var clip = new CutsceneClip
            {
                Name = $"New {track.Type}",
                StartTime = time,
                Duration = 2f,
                FieldOfView = 60f,
                Volume = 1f,
                AnimationSpeed = 1f,
                VFXScale = 1f,
                TypewriterSpeed = 30f,
                FadeColor = Color.black
            };

            track.Clips.Add(clip);
            _selectedClipIdx = track.Clips.Count - 1;
        }

        private void CreateNewCutscene()
        {
            _cutscene = new CutsceneData
            {
                Name = "New Cutscene",
                Duration = 10f,
                IsSkippable = true,
                Tracks = new List<CutsceneTrack>()
            };

            // Add default tracks
            AddTrack(TrackType.Camera);
            AddTrack(TrackType.Dialogue);
            AddTrack(TrackType.Audio);

            _selectedTrackIdx = -1;
            _selectedClipIdx = -1;
            _playheadTime = 0;
        }

        private void LoadCutscene()
        {
            string path = EditorUtility.OpenFilePanel("Load Cutscene", "Assets", "json");
            if (string.IsNullOrEmpty(path)) return;

            string json = System.IO.File.ReadAllText(path);
            _cutscene = JsonUtility.FromJson<CutsceneData>(json);
            Debug.Log($"Loaded cutscene: {_cutscene.Name}");
        }

        private void SaveCutscene()
        {
            string path = EditorUtility.SaveFilePanel("Save Cutscene", "Assets", _cutscene.Name, "json");
            if (string.IsNullOrEmpty(path)) return;

            string json = JsonUtility.ToJson(_cutscene, true);
            System.IO.File.WriteAllText(path, json);
            Debug.Log($"Saved cutscene to {path}");
        }

        private void LoadPreset_StoryIntro()
        {
            CreateNewCutscene();
            _cutscene.Name = "Story Intro";
            _cutscene.Duration = 15f;

            // Add fade in
            AddTrack(TrackType.Fade);
            var fadeTrack = _cutscene.Tracks.Last();
            fadeTrack.Clips.Add(new CutsceneClip
            {
                Name = "Fade In",
                StartTime = 0,
                Duration = 2f,
                FadeType = FadeType.FadeIn,
                FadeColor = Color.black
            });

            // Camera establishing shot
            var camTrack = _cutscene.Tracks[0];
            camTrack.Clips.Add(new CutsceneClip
            {
                Name = "Wide Shot",
                StartTime = 0,
                Duration = 5f,
                CameraMove = CameraMove.Dolly,
                FieldOfView = 70f
            });

            camTrack.Clips.Add(new CutsceneClip
            {
                Name = "Character Close-up",
                StartTime = 5f,
                Duration = 5f,
                CameraMove = CameraMove.Cut,
                FieldOfView = 40f
            });

            // Dialogue
            var dialogTrack = _cutscene.Tracks[1];
            dialogTrack.Clips.Add(new CutsceneClip
            {
                Name = "Narrator",
                StartTime = 2f,
                Duration = 4f,
                SpeakerName = "Narrator",
                DialogueText = "In a world where legends are born...",
                DialoguePosition = DialoguePosition.Bottom
            });

            dialogTrack.Clips.Add(new CutsceneClip
            {
                Name = "Hero",
                StartTime = 7f,
                Duration = 3f,
                SpeakerName = "Hero",
                DialogueText = "My journey begins here.",
                DialoguePosition = DialoguePosition.Bottom
            });

            // Music
            var audioTrack = _cutscene.Tracks[2];
            audioTrack.Clips.Add(new CutsceneClip
            {
                Name = "Epic Theme",
                StartTime = 0,
                Duration = 15f,
                Volume = 0.7f,
                FadeInDuration = 2f,
                IsMusic = true
            });
        }

        private void LoadPreset_BossEntrance()
        {
            CreateNewCutscene();
            _cutscene.Name = "Boss Entrance";
            _cutscene.Duration = 8f;

            // Camera
            var camTrack = _cutscene.Tracks[0];
            camTrack.Clips.Add(new CutsceneClip
            {
                Name = "Boss Reveal",
                StartTime = 0,
                Duration = 3f,
                CameraMove = CameraMove.Crane,
                FieldOfView = 50f
            });

            camTrack.Clips.Add(new CutsceneClip
            {
                Name = "Close-up",
                StartTime = 3f,
                Duration = 2f,
                CameraMove = CameraMove.Cut,
                FieldOfView = 30f
            });

            // VFX
            AddTrack(TrackType.VFX);
            var vfxTrack = _cutscene.Tracks.Last();
            vfxTrack.Clips.Add(new CutsceneClip
            {
                Name = "Dark Aura",
                StartTime = 1f,
                Duration = 5f,
                VFXScale = 2f
            });

            // Dialogue
            var dialogTrack = _cutscene.Tracks[1];
            dialogTrack.Clips.Add(new CutsceneClip
            {
                Name = "Boss Taunt",
                StartTime = 3f,
                Duration = 3f,
                SpeakerName = "???",
                DialogueText = "You dare challenge ME?",
                DialoguePosition = DialoguePosition.Top
            });

            // Letterbox
            AddTrack(TrackType.Letterbox);
            var letterboxTrack = _cutscene.Tracks.Last();
            letterboxTrack.Clips.Add(new CutsceneClip
            {
                Name = "Cinematic Bars",
                StartTime = 0,
                Duration = 8f
            });
        }

        private void LoadPreset_Victory()
        {
            CreateNewCutscene();
            _cutscene.Name = "Victory Scene";
            _cutscene.Duration = 10f;

            // Slow-mo effect via camera
            var camTrack = _cutscene.Tracks[0];
            camTrack.Clips.Add(new CutsceneClip
            {
                Name = "Victory Pose",
                StartTime = 0,
                Duration = 4f,
                CameraMove = CameraMove.Orbit,
                FieldOfView = 45f
            });

            // Dialogue
            var dialogTrack = _cutscene.Tracks[1];
            dialogTrack.Clips.Add(new CutsceneClip
            {
                Name = "Victory Line",
                StartTime = 2f,
                Duration = 3f,
                SpeakerName = "Hero",
                DialogueText = "Justice prevails!",
                DialoguePosition = DialoguePosition.Bottom
            });

            // Victory music
            var audioTrack = _cutscene.Tracks[2];
            audioTrack.Clips.Add(new CutsceneClip
            {
                Name = "Victory Fanfare",
                StartTime = 0,
                Duration = 8f,
                Volume = 1f,
                IsMusic = true
            });

            // Fade out
            AddTrack(TrackType.Fade);
            var fadeTrack = _cutscene.Tracks.Last();
            fadeTrack.Clips.Add(new CutsceneClip
            {
                Name = "Fade Out",
                StartTime = 7f,
                Duration = 3f,
                FadeType = FadeType.FadeOut,
                FadeColor = Color.white
            });
        }

        // Data classes
        [Serializable]
        private class CutsceneData
        {
            public string Name;
            public float Duration;
            public bool IsSkippable;
            public List<CutsceneTrack> Tracks = new List<CutsceneTrack>();
        }

        [Serializable]
        private class CutsceneTrack
        {
            public string Name;
            public TrackType Type;
            public bool IsVisible = true;
            public bool IsLocked;
            public List<CutsceneClip> Clips = new List<CutsceneClip>();
        }

        [Serializable]
        private class CutsceneClip
        {
            public string Name;
            public float StartTime;
            public float Duration;

            // Camera
            public CameraMove CameraMove;
            public Transform CameraTarget;
            public Vector3 CameraOffset;
            public float FieldOfView = 60f;
            public bool EaseIn;
            public bool EaseOut;

            // Dialogue
            public string SpeakerName;
            public string DialogueText;
            public DialoguePosition DialoguePosition;
            public AudioClip VoiceClip;
            public float TypewriterSpeed = 30f;

            // Character
            public GameObject CharacterObject;
            public Vector3 TargetPosition;
            public Vector3 TargetRotation;
            public bool UsePathfinding;

            // Audio
            public AudioClip AudioClip;
            public float Volume = 1f;
            public float FadeInDuration;
            public float FadeOutDuration;
            public bool IsMusic;

            // Animation
            public AnimationClip AnimationClip;
            public Animator AnimationTarget;
            public float AnimationSpeed = 1f;
            public float CrossFadeDuration;

            // VFX
            public GameObject VFXPrefab;
            public Vector3 VFXPosition;
            public float VFXScale = 1f;
            public bool AttachToTarget;

            // Fade
            public FadeType FadeType;
            public Color FadeColor = Color.black;

            // Event
            public string EventName;
            public string EventParameter;

            public CutsceneClip Clone()
            {
                return (CutsceneClip)MemberwiseClone();
            }
        }
    }
}
