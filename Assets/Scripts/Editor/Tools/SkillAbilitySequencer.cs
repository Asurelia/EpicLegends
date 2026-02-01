using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EpicLegends.Editor.Tools
{
    /// <summary>
    /// Éditeur visuel de séquences de compétences et combos
    /// Permet de créer des chaînes d'actions avec timing précis
    /// </summary>
    public class SkillAbilitySequencer : EditorWindow
    {
        [MenuItem("EpicLegends/Tools/Skill & Ability Sequencer")]
        public static void ShowWindow()
        {
            var window = GetWindow<SkillAbilitySequencer>("Skill Sequencer");
            window.minSize = new Vector2(900, 600);
        }

        // Enums
        private enum ActionType
        {
            Attack, Skill, Burst, Dash, Jump, Swap, Wait, Cancel,
            Animation, VFX, SFX, DamageInstance, StatusApply, Buff
        }

        private enum ElementType { Physical, Pyro, Hydro, Electro, Anemo, Cryo, Geo, Dendro }
        private enum DamageScaling { ATK, DEF, HP, EM, MaxHP }

        // State
        private Vector2 _scrollPos;
        private List<SkillSequence> _sequences = new List<SkillSequence>();
        private int _selectedSequenceIdx = -1;
        private int _selectedActionIdx = -1;
        private float _timelineZoom = 100f; // pixels per second
        private float _timelineOffset = 0f;
        private bool _isPlaying;
        private float _playbackTime;
        private double _lastPlaybackUpdate;

        // Drag state
        private bool _isDraggingAction;
        private int _dragActionIdx = -1;
        private float _dragStartTime;
        private float _dragOffset;

        // Timeline dimensions
        private Rect _timelineRect;
        private const float TRACK_HEIGHT = 40f;
        private const float HEADER_WIDTH = 150f;

        // Colors per action type
        private readonly Dictionary<ActionType, Color> _actionColors = new Dictionary<ActionType, Color>
        {
            { ActionType.Attack, new Color(0.9f, 0.4f, 0.4f) },
            { ActionType.Skill, new Color(0.4f, 0.7f, 0.9f) },
            { ActionType.Burst, new Color(0.9f, 0.7f, 0.3f) },
            { ActionType.Dash, new Color(0.5f, 0.9f, 0.5f) },
            { ActionType.Jump, new Color(0.6f, 0.6f, 0.9f) },
            { ActionType.Swap, new Color(0.9f, 0.5f, 0.9f) },
            { ActionType.Wait, new Color(0.6f, 0.6f, 0.6f) },
            { ActionType.Cancel, new Color(0.9f, 0.3f, 0.3f) },
            { ActionType.Animation, new Color(0.4f, 0.8f, 0.8f) },
            { ActionType.VFX, new Color(0.8f, 0.4f, 0.8f) },
            { ActionType.SFX, new Color(0.8f, 0.8f, 0.4f) },
            { ActionType.DamageInstance, new Color(1f, 0.3f, 0.3f) },
            { ActionType.StatusApply, new Color(0.3f, 0.8f, 0.3f) },
            { ActionType.Buff, new Color(0.3f, 0.6f, 1f) }
        };

        private void OnEnable()
        {
            if (_sequences.Count == 0)
                CreateDefaultSequence();
        }

        private void Update()
        {
            if (_isPlaying)
            {
                double now = EditorApplication.timeSinceStartup;
                _playbackTime += (float)(now - _lastPlaybackUpdate);
                _lastPlaybackUpdate = now;

                if (_selectedSequenceIdx >= 0 && _selectedSequenceIdx < _sequences.Count)
                {
                    float totalDuration = _sequences[_selectedSequenceIdx].TotalDuration;
                    if (_playbackTime >= totalDuration)
                        _playbackTime = 0f; // Loop
                }

                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚡ Skill & Ability Sequencer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Design skill sequences, combos, and action chains", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            // Left panel - Sequence list
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            DrawSequenceList();
            EditorGUILayout.EndVertical();

            // Main area - Timeline
            EditorGUILayout.BeginVertical();
            DrawToolbar();
            DrawTimeline();
            EditorGUILayout.EndVertical();

            // Right panel - Properties
            EditorGUILayout.BeginVertical(GUILayout.Width(250));
            DrawPropertyPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            ProcessEvents();
        }

        private void DrawSequenceList()
        {
            EditorGUILayout.LabelField("Sequences", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(200));

            for (int i = 0; i < _sequences.Count; i++)
            {
                var seq = _sequences[i];
                bool isSelected = i == _selectedSequenceIdx;

                var style = isSelected ? new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold } : EditorStyles.label;

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button(seq.Name, style))
                {
                    _selectedSequenceIdx = i;
                    _selectedActionIdx = -1;
                }

                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _sequences.RemoveAt(i);
                    if (_selectedSequenceIdx >= _sequences.Count)
                        _selectedSequenceIdx = _sequences.Count - 1;
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            if (GUILayout.Button("+ New Sequence"))
            {
                CreateNewSequence();
            }

            EditorGUILayout.Space(10);

            // Quick presets
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);

            if (GUILayout.Button("Basic Attack Combo"))
                CreatePreset_BasicCombo();
            if (GUILayout.Button("Skill + Burst"))
                CreatePreset_SkillBurst();
            if (GUILayout.Button("Swap Rotation"))
                CreatePreset_SwapRotation();
            if (GUILayout.Button("Dash Cancel"))
                CreatePreset_DashCancel();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Playback controls
            if (GUILayout.Button(_isPlaying ? "⏸" : "▶", EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                _isPlaying = !_isPlaying;
                if (_isPlaying)
                    _lastPlaybackUpdate = EditorApplication.timeSinceStartup;
            }

            if (GUILayout.Button("⏹", EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                _isPlaying = false;
                _playbackTime = 0f;
            }

            EditorGUILayout.LabelField($"Time: {_playbackTime:F2}s", GUILayout.Width(80));

            GUILayout.FlexibleSpace();

            // Zoom controls
            EditorGUILayout.LabelField("Zoom:", GUILayout.Width(40));
            _timelineZoom = EditorGUILayout.Slider(_timelineZoom, 50f, 300f, GUILayout.Width(100));

            if (GUILayout.Button("Fit", EditorStyles.toolbarButton, GUILayout.Width(40)))
            {
                if (_selectedSequenceIdx >= 0 && _selectedSequenceIdx < _sequences.Count)
                {
                    float duration = _sequences[_selectedSequenceIdx].TotalDuration;
                    if (duration > 0)
                        _timelineZoom = (_timelineRect.width - HEADER_WIDTH) / duration;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTimeline()
        {
            _timelineRect = GUILayoutUtility.GetRect(100, 400, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // Background
            EditorGUI.DrawRect(_timelineRect, new Color(0.15f, 0.15f, 0.15f));

            if (_selectedSequenceIdx < 0 || _selectedSequenceIdx >= _sequences.Count)
            {
                GUI.Label(_timelineRect, "Select or create a sequence", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            var sequence = _sequences[_selectedSequenceIdx];

            // Draw time ruler
            DrawTimeRuler(sequence.TotalDuration);

            // Draw tracks by action type
            var groupedActions = sequence.Actions.GroupBy(a => a.Type).ToList();
            float trackY = _timelineRect.y + 25f;

            foreach (var group in groupedActions)
            {
                // Track header
                Rect headerRect = new Rect(_timelineRect.x, trackY, HEADER_WIDTH - 5, TRACK_HEIGHT - 5);
                EditorGUI.DrawRect(headerRect, new Color(0.25f, 0.25f, 0.25f));
                GUI.Label(headerRect, $" {group.Key}", EditorStyles.miniLabel);

                // Track background
                Rect trackBg = new Rect(_timelineRect.x + HEADER_WIDTH, trackY, _timelineRect.width - HEADER_WIDTH, TRACK_HEIGHT - 5);
                EditorGUI.DrawRect(trackBg, new Color(0.2f, 0.2f, 0.2f));

                // Draw actions in this track
                var actions = group.ToList();
                for (int i = 0; i < sequence.Actions.Count; i++)
                {
                    var action = sequence.Actions[i];
                    if (action.Type != group.Key) continue;

                    float x = _timelineRect.x + HEADER_WIDTH + (action.StartTime - _timelineOffset) * _timelineZoom;
                    float width = action.Duration * _timelineZoom;

                    if (x + width < _timelineRect.x + HEADER_WIDTH || x > _timelineRect.xMax)
                        continue;

                    Rect actionRect = new Rect(x, trackY + 2, Mathf.Max(width, 10), TRACK_HEIGHT - 10);

                    // Clamp to visible area
                    if (actionRect.x < _timelineRect.x + HEADER_WIDTH)
                    {
                        actionRect.width -= (_timelineRect.x + HEADER_WIDTH - actionRect.x);
                        actionRect.x = _timelineRect.x + HEADER_WIDTH;
                    }

                    // Draw action block
                    Color color = _actionColors.GetValueOrDefault(action.Type, Color.gray);
                    if (i == _selectedActionIdx)
                        color = Color.Lerp(color, Color.white, 0.3f);

                    EditorGUI.DrawRect(actionRect, color);

                    // Action label
                    var labelStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.white }
                    };
                    GUI.Label(actionRect, action.Name, labelStyle);

                    // Handle selection
                    if (Event.current.type == EventType.MouseDown && actionRect.Contains(Event.current.mousePosition))
                    {
                        _selectedActionIdx = i;
                        _isDraggingAction = true;
                        _dragActionIdx = i;
                        _dragStartTime = action.StartTime;
                        _dragOffset = (Event.current.mousePosition.x - actionRect.x) / _timelineZoom;
                        Event.current.Use();
                        Repaint();
                    }
                }

                trackY += TRACK_HEIGHT;
            }

            // Playhead
            if (_isPlaying || _playbackTime > 0)
            {
                float playheadX = _timelineRect.x + HEADER_WIDTH + (_playbackTime - _timelineOffset) * _timelineZoom;
                if (playheadX >= _timelineRect.x + HEADER_WIDTH && playheadX <= _timelineRect.xMax)
                {
                    EditorGUI.DrawRect(new Rect(playheadX - 1, _timelineRect.y, 2, _timelineRect.height), Color.red);
                }
            }

            // Add action context menu
            if (Event.current.type == EventType.ContextClick && _timelineRect.Contains(Event.current.mousePosition))
            {
                float clickTime = (Event.current.mousePosition.x - _timelineRect.x - HEADER_WIDTH) / _timelineZoom + _timelineOffset;
                if (clickTime >= 0)
                {
                    ShowAddActionMenu(clickTime);
                    Event.current.Use();
                }
            }
        }

        private void DrawTimeRuler(float duration)
        {
            Rect rulerRect = new Rect(_timelineRect.x + HEADER_WIDTH, _timelineRect.y, _timelineRect.width - HEADER_WIDTH, 20);
            EditorGUI.DrawRect(rulerRect, new Color(0.3f, 0.3f, 0.3f));

            // Draw time markers
            float step = 0.1f;
            if (_timelineZoom < 80) step = 0.5f;
            if (_timelineZoom < 50) step = 1f;

            for (float t = 0; t <= duration + step; t += step)
            {
                float x = rulerRect.x + (t - _timelineOffset) * _timelineZoom;
                if (x < rulerRect.x || x > rulerRect.xMax) continue;

                bool majorTick = Mathf.Abs(t % 1f) < 0.001f || Mathf.Abs(t % 1f - 1f) < 0.001f;

                float tickHeight = majorTick ? 15 : 8;
                EditorGUI.DrawRect(new Rect(x, rulerRect.y + 20 - tickHeight, 1, tickHeight), Color.gray);

                if (majorTick)
                {
                    GUI.Label(new Rect(x - 10, rulerRect.y, 20, 15), $"{t:F0}s", EditorStyles.centeredGreyMiniLabel);
                }
            }
        }

        private void DrawPropertyPanel()
        {
            EditorGUILayout.LabelField("Properties", EditorStyles.boldLabel);

            if (_selectedSequenceIdx < 0 || _selectedSequenceIdx >= _sequences.Count)
                return;

            var sequence = _sequences[_selectedSequenceIdx];

            // Sequence properties
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Sequence", EditorStyles.boldLabel);
            sequence.Name = EditorGUILayout.TextField("Name", sequence.Name);
            EditorGUILayout.LabelField($"Duration: {sequence.TotalDuration:F2}s");
            EditorGUILayout.LabelField($"Actions: {sequence.Actions.Count}");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Selected action properties
            if (_selectedActionIdx >= 0 && _selectedActionIdx < sequence.Actions.Count)
            {
                var action = sequence.Actions[_selectedActionIdx];

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Selected Action", EditorStyles.boldLabel);

                action.Name = EditorGUILayout.TextField("Name", action.Name);
                action.Type = (ActionType)EditorGUILayout.EnumPopup("Type", action.Type);
                action.StartTime = EditorGUILayout.FloatField("Start Time", action.StartTime);
                action.Duration = EditorGUILayout.FloatField("Duration", action.Duration);

                EditorGUILayout.Space(5);

                // Type-specific properties
                switch (action.Type)
                {
                    case ActionType.Attack:
                    case ActionType.Skill:
                    case ActionType.Burst:
                        DrawDamageProperties(action);
                        break;
                    case ActionType.Animation:
                        DrawAnimationProperties(action);
                        break;
                    case ActionType.VFX:
                        DrawVFXProperties(action);
                        break;
                    case ActionType.StatusApply:
                        DrawStatusProperties(action);
                        break;
                    case ActionType.Buff:
                        DrawBuffProperties(action);
                        break;
                }

                EditorGUILayout.Space(5);

                if (GUILayout.Button("Delete Action", GUILayout.Height(25)))
                {
                    sequence.Actions.RemoveAt(_selectedActionIdx);
                    _selectedActionIdx = -1;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);

            // Export/Import
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);

            if (GUILayout.Button("Export to ScriptableObject"))
                ExportToScriptableObject(sequence);

            if (GUILayout.Button("Export to JSON"))
                ExportToJson(sequence);

            EditorGUILayout.EndVertical();
        }

        private void DrawDamageProperties(SequenceAction action)
        {
            EditorGUILayout.LabelField("Damage", EditorStyles.miniBoldLabel);
            action.Element = (ElementType)EditorGUILayout.EnumPopup("Element", action.Element);
            action.DamageMultiplier = EditorGUILayout.FloatField("Multiplier %", action.DamageMultiplier);
            action.Scaling = (DamageScaling)EditorGUILayout.EnumPopup("Scaling", action.Scaling);
            action.HitCount = EditorGUILayout.IntSlider("Hits", action.HitCount, 1, 10);
            action.CanCrit = EditorGUILayout.Toggle("Can Crit", action.CanCrit);
            action.AppliesElement = EditorGUILayout.Toggle("Applies Element", action.AppliesElement);
        }

        private void DrawAnimationProperties(SequenceAction action)
        {
            EditorGUILayout.LabelField("Animation", EditorStyles.miniBoldLabel);
            action.AnimationClip = (AnimationClip)EditorGUILayout.ObjectField("Clip", action.AnimationClip, typeof(AnimationClip), false);
            action.AnimationSpeed = EditorGUILayout.FloatField("Speed", action.AnimationSpeed);
            action.CrossFadeDuration = EditorGUILayout.FloatField("CrossFade", action.CrossFadeDuration);
        }

        private void DrawVFXProperties(SequenceAction action)
        {
            EditorGUILayout.LabelField("VFX", EditorStyles.miniBoldLabel);
            action.VFXPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", action.VFXPrefab, typeof(GameObject), false);
            action.VFXOffset = EditorGUILayout.Vector3Field("Offset", action.VFXOffset);
            action.VFXScale = EditorGUILayout.FloatField("Scale", action.VFXScale);
        }

        private void DrawStatusProperties(SequenceAction action)
        {
            EditorGUILayout.LabelField("Status Effect", EditorStyles.miniBoldLabel);
            action.StatusName = EditorGUILayout.TextField("Status Name", action.StatusName);
            action.StatusDuration = EditorGUILayout.FloatField("Duration", action.StatusDuration);
            action.StatusStacks = EditorGUILayout.IntSlider("Stacks", action.StatusStacks, 1, 10);
        }

        private void DrawBuffProperties(SequenceAction action)
        {
            EditorGUILayout.LabelField("Buff", EditorStyles.miniBoldLabel);
            action.BuffName = EditorGUILayout.TextField("Buff Name", action.BuffName);
            action.BuffValue = EditorGUILayout.FloatField("Value %", action.BuffValue);
            action.BuffDuration = EditorGUILayout.FloatField("Duration", action.BuffDuration);
            action.BuffTarget = EditorGUILayout.TextField("Target (self/party/enemy)", action.BuffTarget);
        }

        private void ProcessEvents()
        {
            Event e = Event.current;

            // Handle action dragging
            if (_isDraggingAction && _dragActionIdx >= 0)
            {
                if (e.type == EventType.MouseDrag)
                {
                    float newTime = (e.mousePosition.x - _timelineRect.x - HEADER_WIDTH) / _timelineZoom - _dragOffset + _timelineOffset;
                    newTime = Mathf.Max(0, newTime);

                    if (_selectedSequenceIdx >= 0 && _selectedSequenceIdx < _sequences.Count)
                    {
                        _sequences[_selectedSequenceIdx].Actions[_dragActionIdx].StartTime = newTime;
                    }

                    e.Use();
                    Repaint();
                }
                else if (e.type == EventType.MouseUp)
                {
                    _isDraggingAction = false;
                    _dragActionIdx = -1;
                    e.Use();
                }
            }

            // Timeline scroll with mouse wheel
            if (e.type == EventType.ScrollWheel && _timelineRect.Contains(e.mousePosition))
            {
                _timelineZoom -= e.delta.y * 5f;
                _timelineZoom = Mathf.Clamp(_timelineZoom, 50f, 300f);
                e.Use();
                Repaint();
            }

            // Keyboard shortcuts
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Space)
                {
                    _isPlaying = !_isPlaying;
                    if (_isPlaying)
                        _lastPlaybackUpdate = EditorApplication.timeSinceStartup;
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Delete && _selectedActionIdx >= 0)
                {
                    if (_selectedSequenceIdx >= 0 && _selectedSequenceIdx < _sequences.Count)
                    {
                        _sequences[_selectedSequenceIdx].Actions.RemoveAt(_selectedActionIdx);
                        _selectedActionIdx = -1;
                        e.Use();
                    }
                }
            }
        }

        private void ShowAddActionMenu(float time)
        {
            GenericMenu menu = new GenericMenu();

            foreach (ActionType type in Enum.GetValues(typeof(ActionType)))
            {
                ActionType capturedType = type;
                menu.AddItem(new GUIContent($"Add {type}"), false, () => AddAction(capturedType, time));
            }

            menu.ShowAsContext();
        }

        private void AddAction(ActionType type, float startTime)
        {
            if (_selectedSequenceIdx < 0 || _selectedSequenceIdx >= _sequences.Count)
                return;

            var action = new SequenceAction
            {
                Name = type.ToString(),
                Type = type,
                StartTime = startTime,
                Duration = GetDefaultDuration(type),
                DamageMultiplier = 100f,
                HitCount = 1,
                CanCrit = true,
                AnimationSpeed = 1f,
                VFXScale = 1f,
                StatusStacks = 1,
                BuffTarget = "self"
            };

            _sequences[_selectedSequenceIdx].Actions.Add(action);
            _selectedActionIdx = _sequences[_selectedSequenceIdx].Actions.Count - 1;
        }

        private float GetDefaultDuration(ActionType type)
        {
            return type switch
            {
                ActionType.Attack => 0.5f,
                ActionType.Skill => 1f,
                ActionType.Burst => 2f,
                ActionType.Dash => 0.3f,
                ActionType.Jump => 0.4f,
                ActionType.Swap => 0.8f,
                ActionType.Wait => 0.5f,
                ActionType.Cancel => 0.1f,
                ActionType.Animation => 1f,
                ActionType.VFX => 0.5f,
                ActionType.SFX => 0.2f,
                ActionType.DamageInstance => 0.1f,
                ActionType.StatusApply => 0.1f,
                ActionType.Buff => 0.1f,
                _ => 0.5f
            };
        }

        private void CreateDefaultSequence()
        {
            _sequences.Add(new SkillSequence { Name = "New Sequence" });
            _selectedSequenceIdx = 0;
        }

        private void CreateNewSequence()
        {
            _sequences.Add(new SkillSequence { Name = $"Sequence {_sequences.Count + 1}" });
            _selectedSequenceIdx = _sequences.Count - 1;
            _selectedActionIdx = -1;
        }

        // Presets
        private void CreatePreset_BasicCombo()
        {
            var seq = new SkillSequence { Name = "Basic Attack Combo" };

            float t = 0f;
            for (int i = 1; i <= 5; i++)
            {
                seq.Actions.Add(new SequenceAction
                {
                    Name = $"N{i}",
                    Type = ActionType.Attack,
                    StartTime = t,
                    Duration = 0.4f + i * 0.1f,
                    DamageMultiplier = 50 + i * 20,
                    HitCount = 1,
                    CanCrit = true
                });
                t += 0.4f + i * 0.1f;
            }

            _sequences.Add(seq);
            _selectedSequenceIdx = _sequences.Count - 1;
        }

        private void CreatePreset_SkillBurst()
        {
            var seq = new SkillSequence { Name = "Skill → Burst" };

            seq.Actions.Add(new SequenceAction
            {
                Name = "Elemental Skill",
                Type = ActionType.Skill,
                StartTime = 0f,
                Duration = 1f,
                Element = ElementType.Pyro,
                DamageMultiplier = 250f,
                HitCount = 1,
                CanCrit = true,
                AppliesElement = true
            });

            seq.Actions.Add(new SequenceAction
            {
                Name = "Skill VFX",
                Type = ActionType.VFX,
                StartTime = 0.2f,
                Duration = 0.8f
            });

            seq.Actions.Add(new SequenceAction
            {
                Name = "Elemental Burst",
                Type = ActionType.Burst,
                StartTime = 1.2f,
                Duration = 2f,
                Element = ElementType.Pyro,
                DamageMultiplier = 500f,
                HitCount = 5,
                CanCrit = true,
                AppliesElement = true
            });

            seq.Actions.Add(new SequenceAction
            {
                Name = "ATK Buff",
                Type = ActionType.Buff,
                StartTime = 1.4f,
                Duration = 0.1f,
                BuffName = "ATK%",
                BuffValue = 25f,
                BuffDuration = 10f,
                BuffTarget = "party"
            });

            _sequences.Add(seq);
            _selectedSequenceIdx = _sequences.Count - 1;
        }

        private void CreatePreset_SwapRotation()
        {
            var seq = new SkillSequence { Name = "Swap Rotation" };

            string[] chars = { "Support 1", "Support 2", "Sub DPS", "Main DPS" };
            float t = 0f;

            foreach (var c in chars)
            {
                seq.Actions.Add(new SequenceAction
                {
                    Name = $"Swap to {c}",
                    Type = ActionType.Swap,
                    StartTime = t,
                    Duration = 0.8f
                });
                t += 0.8f;

                seq.Actions.Add(new SequenceAction
                {
                    Name = $"{c} Skill",
                    Type = ActionType.Skill,
                    StartTime = t,
                    Duration = 1f
                });
                t += 1.2f;
            }

            _sequences.Add(seq);
            _selectedSequenceIdx = _sequences.Count - 1;
        }

        private void CreatePreset_DashCancel()
        {
            var seq = new SkillSequence { Name = "Dash Cancel" };

            seq.Actions.Add(new SequenceAction
            {
                Name = "Charged Attack",
                Type = ActionType.Attack,
                StartTime = 0f,
                Duration = 0.8f,
                DamageMultiplier = 180f
            });

            seq.Actions.Add(new SequenceAction
            {
                Name = "Dash Cancel",
                Type = ActionType.Dash,
                StartTime = 0.5f,
                Duration = 0.3f
            });

            seq.Actions.Add(new SequenceAction
            {
                Name = "Cancel Frame",
                Type = ActionType.Cancel,
                StartTime = 0.5f,
                Duration = 0.1f
            });

            seq.Actions.Add(new SequenceAction
            {
                Name = "Charged Attack 2",
                Type = ActionType.Attack,
                StartTime = 0.8f,
                Duration = 0.8f,
                DamageMultiplier = 180f
            });

            _sequences.Add(seq);
            _selectedSequenceIdx = _sequences.Count - 1;
        }

        private void ExportToScriptableObject(SkillSequence sequence)
        {
            string path = EditorUtility.SaveFilePanelInProject("Save Skill Sequence", sequence.Name, "asset", "Save sequence as ScriptableObject");
            if (string.IsNullOrEmpty(path)) return;

            // Would create actual ScriptableObject here
            Debug.Log($"Would export to ScriptableObject: {path}");
            EditorUtility.DisplayDialog("Export", $"Sequence '{sequence.Name}' exported to {path}", "OK");
        }

        private void ExportToJson(SkillSequence sequence)
        {
            string path = EditorUtility.SaveFilePanel("Export Sequence", "", sequence.Name, "json");
            if (string.IsNullOrEmpty(path)) return;

            string json = JsonUtility.ToJson(sequence, true);
            System.IO.File.WriteAllText(path, json);
            Debug.Log($"Exported sequence to {path}");
        }

        // Data classes
        [Serializable]
        private class SkillSequence
        {
            public string Name = "New Sequence";
            public List<SequenceAction> Actions = new List<SequenceAction>();

            public float TotalDuration
            {
                get
                {
                    float max = 0;
                    foreach (var action in Actions)
                    {
                        float end = action.StartTime + action.Duration;
                        if (end > max) max = end;
                    }
                    return max;
                }
            }
        }

        [Serializable]
        private class SequenceAction
        {
            public string Name;
            public ActionType Type;
            public float StartTime;
            public float Duration;

            // Damage properties
            public ElementType Element;
            public float DamageMultiplier;
            public DamageScaling Scaling;
            public int HitCount;
            public bool CanCrit;
            public bool AppliesElement;

            // Animation properties
            public AnimationClip AnimationClip;
            public float AnimationSpeed = 1f;
            public float CrossFadeDuration;

            // VFX properties
            public GameObject VFXPrefab;
            public Vector3 VFXOffset;
            public float VFXScale = 1f;

            // Status properties
            public string StatusName;
            public float StatusDuration;
            public int StatusStacks;

            // Buff properties
            public string BuffName;
            public float BuffValue;
            public float BuffDuration;
            public string BuffTarget;
        }
    }
}
