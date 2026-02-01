using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EpicLegends.Editor.Tools
{
    /// <summary>
    /// Chorégraphe de combat de boss
    /// Création visuelle de patterns d'attaque et phases de boss
    /// </summary>
    public class BossFightChoreographer : EditorWindow
    {
        [MenuItem("EpicLegends/Tools/Boss Fight Choreographer")]
        public static void ShowWindow()
        {
            var window = GetWindow<BossFightChoreographer>("Boss Choreographer");
            window.minSize = new Vector2(1000, 700);
        }

        // Enums
        private enum AttackType
        {
            Melee, Ranged, AoE, Charge, Sweep, Slam, Projectile, Beam, Summon,
            Teleport, Shield, Heal, Buff, Debuff, Special
        }

        private enum TelegraphType { None, Visual, Audio, Both }
        private enum DodgeDirection { Any, Left, Right, Back, Jump, Iframe }

        // State
        private Vector2 _scrollPos;
        private BossFightData _currentBoss;
        private int _selectedPhaseIdx = 0;
        private int _selectedPatternIdx = -1;
        private bool _isPreviewMode;
        private float _previewTime;

        // Timeline
        private float _timelineZoom = 50f;
        private float _timelineOffset = 0f;
        private Rect _timelineRect;

        // Preview
        private Vector2 _arenaCenter = Vector2.zero;
        private float _arenaSize = 10f;
        private List<PreviewMarker> _previewMarkers = new List<PreviewMarker>();

        // Colors
        private readonly Dictionary<AttackType, Color> _attackColors = new Dictionary<AttackType, Color>
        {
            { AttackType.Melee, new Color(1f, 0.4f, 0.4f) },
            { AttackType.Ranged, new Color(0.4f, 0.7f, 1f) },
            { AttackType.AoE, new Color(1f, 0.6f, 0.2f) },
            { AttackType.Charge, new Color(0.9f, 0.3f, 0.5f) },
            { AttackType.Sweep, new Color(0.8f, 0.4f, 0.8f) },
            { AttackType.Slam, new Color(0.6f, 0.3f, 0.1f) },
            { AttackType.Projectile, new Color(0.3f, 0.8f, 0.3f) },
            { AttackType.Beam, new Color(1f, 1f, 0.3f) },
            { AttackType.Summon, new Color(0.5f, 0.2f, 0.7f) },
            { AttackType.Teleport, new Color(0.3f, 0.3f, 0.8f) },
            { AttackType.Shield, new Color(0.4f, 0.8f, 0.8f) },
            { AttackType.Heal, new Color(0.3f, 1f, 0.5f) },
            { AttackType.Buff, new Color(0.8f, 0.8f, 0.3f) },
            { AttackType.Debuff, new Color(0.6f, 0.2f, 0.6f) },
            { AttackType.Special, new Color(1f, 0.8f, 0.9f) }
        };

        private void OnEnable()
        {
            if (_currentBoss == null)
                CreateDefaultBoss();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🐉 Boss Fight Choreographer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Design boss attack patterns and phase transitions", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            // Left panel - Boss & Phase info
            EditorGUILayout.BeginVertical(GUILayout.Width(250));
            DrawBossPanel();
            EditorGUILayout.EndVertical();

            // Center - Timeline & Arena Preview
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawMainArea();
            EditorGUILayout.EndVertical();

            // Right panel - Pattern editor
            EditorGUILayout.BeginVertical(GUILayout.Width(280));
            DrawPatternEditor();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawBossPanel()
        {
            EditorGUILayout.LabelField("Boss Configuration", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_currentBoss == null)
            {
                if (GUILayout.Button("Create New Boss"))
                    CreateDefaultBoss();
                EditorGUILayout.EndVertical();
                return;
            }

            _currentBoss.Name = EditorGUILayout.TextField("Name", _currentBoss.Name);
            _currentBoss.MaxHealth = EditorGUILayout.FloatField("Max Health", _currentBoss.MaxHealth);
            _currentBoss.Level = EditorGUILayout.IntField("Level", _currentBoss.Level);

            EditorGUILayout.Space(5);

            // Phase list
            EditorGUILayout.LabelField("Phases", EditorStyles.miniBoldLabel);

            for (int i = 0; i < _currentBoss.Phases.Count; i++)
            {
                var phase = _currentBoss.Phases[i];
                bool isSelected = i == _selectedPhaseIdx;

                Color phaseColor = GetPhaseColor(i);
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = isSelected ? phaseColor : Color.Lerp(phaseColor, Color.gray, 0.5f);

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUI.backgroundColor = prevBg;

                if (GUILayout.Button($"Phase {i + 1}", EditorStyles.label))
                    _selectedPhaseIdx = i;

                EditorGUILayout.LabelField($"<{phase.HealthThreshold}%", GUILayout.Width(50));

                if (GUILayout.Button("×", GUILayout.Width(20)))
                {
                    _currentBoss.Phases.RemoveAt(i);
                    if (_selectedPhaseIdx >= _currentBoss.Phases.Count)
                        _selectedPhaseIdx = Mathf.Max(0, _currentBoss.Phases.Count - 1);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Phase"))
            {
                int threshold = _currentBoss.Phases.Count > 0 ?
                    _currentBoss.Phases.Last().HealthThreshold - 30 : 70;

                _currentBoss.Phases.Add(new BossPhase
                {
                    Name = $"Phase {_currentBoss.Phases.Count + 1}",
                    HealthThreshold = Mathf.Max(0, threshold)
                });
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Current phase settings
            if (_selectedPhaseIdx >= 0 && _selectedPhaseIdx < _currentBoss.Phases.Count)
            {
                var phase = _currentBoss.Phases[_selectedPhaseIdx];

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Phase {_selectedPhaseIdx + 1} Settings", EditorStyles.boldLabel);

                phase.Name = EditorGUILayout.TextField("Name", phase.Name);
                phase.HealthThreshold = EditorGUILayout.IntSlider("HP Threshold %", phase.HealthThreshold, 0, 100);
                phase.SpeedMultiplier = EditorGUILayout.Slider("Speed Mult", phase.SpeedMultiplier, 0.5f, 2f);
                phase.DamageMultiplier = EditorGUILayout.Slider("Damage Mult", phase.DamageMultiplier, 0.5f, 3f);

                EditorGUILayout.Space(5);

                phase.TransitionType = (PhaseTransitionType)EditorGUILayout.EnumPopup("Transition", phase.TransitionType);
                phase.TransitionDuration = EditorGUILayout.FloatField("Transition Time", phase.TransitionDuration);

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(10);

            // Presets
            EditorGUILayout.LabelField("Boss Presets", EditorStyles.boldLabel);

            if (GUILayout.Button("Dragon Boss"))
                LoadPreset_Dragon();
            if (GUILayout.Button("Humanoid Boss"))
                LoadPreset_Humanoid();
            if (GUILayout.Button("Swarm Boss"))
                LoadPreset_Swarm();
        }

        private void DrawMainArea()
        {
            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            _isPreviewMode = GUILayout.Toggle(_isPreviewMode, _isPreviewMode ? "⏸ Stop Preview" : "▶ Preview", EditorStyles.toolbarButton, GUILayout.Width(100));

            if (_isPreviewMode)
            {
                _previewTime = EditorGUILayout.Slider(_previewTime, 0, GetPhaseDuration(), GUILayout.Width(200));
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField("Zoom:", GUILayout.Width(40));
            _timelineZoom = EditorGUILayout.Slider(_timelineZoom, 20f, 100f, GUILayout.Width(100));

            EditorGUILayout.EndHorizontal();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Arena preview
            DrawArenaPreview();

            EditorGUILayout.Space(10);

            // Timeline
            DrawTimeline();

            EditorGUILayout.EndScrollView();
        }

        private void DrawArenaPreview()
        {
            EditorGUILayout.LabelField("Arena Preview", EditorStyles.boldLabel);

            Rect arenaRect = GUILayoutUtility.GetRect(100, 250, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(arenaRect, new Color(0.1f, 0.1f, 0.15f));

            // Draw arena circle
            Vector2 center = arenaRect.center;
            float radius = Mathf.Min(arenaRect.width, arenaRect.height) * 0.4f;

            // Draw arena boundary
            Handles.color = new Color(0.4f, 0.4f, 0.4f);
            Handles.DrawWireDisc(new Vector3(center.x, center.y, 0), Vector3.forward, radius);

            // Draw grid
            Handles.color = new Color(0.2f, 0.2f, 0.2f);
            for (int i = -4; i <= 4; i++)
            {
                float x = center.x + (i / 4f) * radius;
                Handles.DrawLine(new Vector3(x, center.y - radius, 0), new Vector3(x, center.y + radius, 0));
                float y = center.y + (i / 4f) * radius;
                Handles.DrawLine(new Vector3(center.x - radius, y, 0), new Vector3(center.x + radius, y, 0));
            }

            // Draw boss position
            Handles.color = Color.red;
            Handles.DrawSolidDisc(new Vector3(center.x, center.y - radius * 0.3f, 0), Vector3.forward, 15);
            GUI.Label(new Rect(center.x - 20, center.y - radius * 0.3f - 25, 40, 20), "BOSS", EditorStyles.centeredGreyMiniLabel);

            // Draw player position
            Handles.color = Color.green;
            Handles.DrawSolidDisc(new Vector3(center.x, center.y + radius * 0.5f, 0), Vector3.forward, 10);
            GUI.Label(new Rect(center.x - 20, center.y + radius * 0.5f + 10, 40, 20), "Player", EditorStyles.centeredGreyMiniLabel);

            // Draw attack previews based on current time
            if (_isPreviewMode && _selectedPhaseIdx >= 0 && _selectedPhaseIdx < _currentBoss.Phases.Count)
            {
                var phase = _currentBoss.Phases[_selectedPhaseIdx];
                foreach (var pattern in phase.Patterns)
                {
                    if (_previewTime >= pattern.StartTime && _previewTime <= pattern.StartTime + pattern.Duration)
                    {
                        DrawAttackPreview(center, radius, pattern, _previewTime - pattern.StartTime);
                    }
                }
            }

            // Draw selected pattern preview
            if (!_isPreviewMode && _selectedPatternIdx >= 0)
            {
                var phase = _currentBoss.Phases[_selectedPhaseIdx];
                if (_selectedPatternIdx < phase.Patterns.Count)
                {
                    var pattern = phase.Patterns[_selectedPatternIdx];
                    DrawAttackPreview(center, radius, pattern, 0);
                }
            }
        }

        private void DrawAttackPreview(Vector2 center, float radius, AttackPattern pattern, float localTime)
        {
            Color attackColor = _attackColors.GetValueOrDefault(pattern.Type, Color.red);
            attackColor.a = 0.5f;
            Handles.color = attackColor;

            float progress = Mathf.Clamp01(localTime / pattern.Duration);
            Vector2 bossPos = new Vector2(center.x, center.y - radius * 0.3f);

            switch (pattern.Type)
            {
                case AttackType.AoE:
                    float aoeRadius = pattern.Range * (radius / _arenaSize) * progress;
                    Handles.DrawSolidDisc(new Vector3(bossPos.x, bossPos.y, 0), Vector3.forward, aoeRadius);
                    break;

                case AttackType.Sweep:
                    float angle = pattern.Angle * Mathf.Deg2Rad;
                    float sweepRadius = pattern.Range * (radius / _arenaSize);
                    Handles.DrawSolidArc(new Vector3(bossPos.x, bossPos.y, 0), Vector3.forward,
                        Quaternion.Euler(0, 0, -pattern.Angle / 2) * Vector3.down,
                        pattern.Angle, sweepRadius);
                    break;

                case AttackType.Beam:
                    float beamWidth = 20f;
                    float beamLength = pattern.Range * (radius / _arenaSize);
                    Vector2 beamEnd = bossPos + Vector2.down * beamLength;
                    EditorGUI.DrawRect(new Rect(bossPos.x - beamWidth / 2, bossPos.y, beamWidth, beamLength), attackColor);
                    break;

                case AttackType.Projectile:
                    float projDist = pattern.Range * (radius / _arenaSize) * progress;
                    Vector2 projPos = bossPos + Vector2.down * projDist;
                    Handles.DrawSolidDisc(new Vector3(projPos.x, projPos.y, 0), Vector3.forward, 8);
                    break;

                case AttackType.Charge:
                    float chargeProgress = Mathf.Lerp(0, pattern.Range * (radius / _arenaSize), progress);
                    Vector2 chargePos = bossPos + Vector2.down * chargeProgress;
                    Handles.DrawSolidDisc(new Vector3(chargePos.x, chargePos.y, 0), Vector3.forward, 20);
                    break;

                case AttackType.Slam:
                    if (progress > 0.8f)
                    {
                        float slamRadius = pattern.Range * (radius / _arenaSize);
                        Handles.DrawSolidDisc(new Vector3(bossPos.x, bossPos.y, 0), Vector3.forward, slamRadius);
                    }
                    break;
            }
        }

        private void DrawTimeline()
        {
            EditorGUILayout.LabelField("Attack Timeline", EditorStyles.boldLabel);

            if (_selectedPhaseIdx < 0 || _selectedPhaseIdx >= _currentBoss.Phases.Count)
                return;

            var phase = _currentBoss.Phases[_selectedPhaseIdx];

            _timelineRect = GUILayoutUtility.GetRect(100, 150, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(_timelineRect, new Color(0.15f, 0.15f, 0.15f));

            // Draw time ruler
            DrawTimeRuler();

            // Draw patterns as blocks
            float trackY = _timelineRect.y + 25;

            for (int i = 0; i < phase.Patterns.Count; i++)
            {
                var pattern = phase.Patterns[i];
                bool isSelected = i == _selectedPatternIdx;

                float x = _timelineRect.x + 5 + (pattern.StartTime - _timelineOffset) * _timelineZoom;
                float width = Mathf.Max(pattern.Duration * _timelineZoom, 20);

                if (x + width < _timelineRect.x || x > _timelineRect.xMax)
                    continue;

                Rect patternRect = new Rect(x, trackY, width, 30);

                Color color = _attackColors.GetValueOrDefault(pattern.Type, Color.gray);
                if (isSelected) color = Color.Lerp(color, Color.white, 0.3f);

                EditorGUI.DrawRect(patternRect, color);

                // Pattern label
                var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
                GUI.Label(patternRect, pattern.Name, style);

                // Telegraph indicator
                if (pattern.TelegraphTime > 0)
                {
                    float telegraphX = x - pattern.TelegraphTime * _timelineZoom;
                    Rect telegraphRect = new Rect(telegraphX, trackY + 25, pattern.TelegraphTime * _timelineZoom, 5);
                    EditorGUI.DrawRect(telegraphRect, new Color(1f, 1f, 0f, 0.5f));
                }

                // Selection handling
                if (Event.current.type == UnityEngine.EventType.MouseDown && patternRect.Contains(Event.current.mousePosition))
                {
                    _selectedPatternIdx = i;
                    Event.current.Use();
                    Repaint();
                }

                trackY += 35;
            }

            // Playhead
            if (_isPreviewMode)
            {
                float playheadX = _timelineRect.x + 5 + (_previewTime - _timelineOffset) * _timelineZoom;
                EditorGUI.DrawRect(new Rect(playheadX - 1, _timelineRect.y, 2, _timelineRect.height), Color.red);
            }

            // Context menu for adding patterns
            if (Event.current.type == UnityEngine.EventType.ContextClick && _timelineRect.Contains(Event.current.mousePosition))
            {
                float clickTime = (Event.current.mousePosition.x - _timelineRect.x - 5) / _timelineZoom + _timelineOffset;
                ShowAddPatternMenu(clickTime);
                Event.current.Use();
            }
        }

        private void DrawTimeRuler()
        {
            Rect rulerRect = new Rect(_timelineRect.x, _timelineRect.y, _timelineRect.width, 20);
            EditorGUI.DrawRect(rulerRect, new Color(0.25f, 0.25f, 0.25f));

            float duration = GetPhaseDuration();
            float step = 1f;
            if (_timelineZoom < 30) step = 2f;

            for (float t = 0; t <= duration + step; t += step)
            {
                float x = _timelineRect.x + 5 + (t - _timelineOffset) * _timelineZoom;
                if (x < _timelineRect.x || x > _timelineRect.xMax) continue;

                EditorGUI.DrawRect(new Rect(x, rulerRect.y + 15, 1, 5), Color.gray);
                GUI.Label(new Rect(x - 15, rulerRect.y, 30, 15), $"{t:F0}s", EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawPatternEditor()
        {
            EditorGUILayout.LabelField("Pattern Editor", EditorStyles.boldLabel);

            if (_selectedPhaseIdx < 0 || _selectedPhaseIdx >= _currentBoss.Phases.Count)
            {
                EditorGUILayout.HelpBox("Select a phase first", MessageType.Info);
                return;
            }

            var phase = _currentBoss.Phases[_selectedPhaseIdx];

            if (_selectedPatternIdx < 0 || _selectedPatternIdx >= phase.Patterns.Count)
            {
                EditorGUILayout.HelpBox("Right-click timeline to add patterns", MessageType.Info);

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Quick Add", EditorStyles.boldLabel);

                foreach (AttackType type in Enum.GetValues(typeof(AttackType)))
                {
                    if (GUILayout.Button($"+ {type}"))
                    {
                        AddPattern(type, GetPhaseDuration());
                    }
                }

                return;
            }

            var pattern = phase.Patterns[_selectedPatternIdx];

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            pattern.Name = EditorGUILayout.TextField("Name", pattern.Name);
            pattern.Type = (AttackType)EditorGUILayout.EnumPopup("Type", pattern.Type);

            EditorGUILayout.Space(5);

            // Timing
            EditorGUILayout.LabelField("Timing", EditorStyles.miniBoldLabel);
            pattern.StartTime = EditorGUILayout.FloatField("Start Time", pattern.StartTime);
            pattern.Duration = EditorGUILayout.FloatField("Duration", pattern.Duration);
            pattern.TelegraphTime = EditorGUILayout.FloatField("Telegraph Time", pattern.TelegraphTime);
            pattern.Cooldown = EditorGUILayout.FloatField("Cooldown", pattern.Cooldown);

            EditorGUILayout.Space(5);

            // Combat stats
            EditorGUILayout.LabelField("Combat", EditorStyles.miniBoldLabel);
            pattern.Damage = EditorGUILayout.FloatField("Damage", pattern.Damage);
            pattern.Range = EditorGUILayout.FloatField("Range", pattern.Range);
            pattern.Angle = EditorGUILayout.FloatField("Angle", pattern.Angle);
            pattern.Knockback = EditorGUILayout.FloatField("Knockback", pattern.Knockback);

            EditorGUILayout.Space(5);

            // Dodge info
            EditorGUILayout.LabelField("Dodge Info", EditorStyles.miniBoldLabel);
            pattern.TelegraphType = (TelegraphType)EditorGUILayout.EnumPopup("Telegraph", pattern.TelegraphType);
            pattern.DodgeDirection = (DodgeDirection)EditorGUILayout.EnumPopup("Dodge Direction", pattern.DodgeDirection);
            pattern.DodgeWindow = EditorGUILayout.FloatField("Dodge Window", pattern.DodgeWindow);

            EditorGUILayout.Space(5);

            // Visual/Audio
            EditorGUILayout.LabelField("Effects", EditorStyles.miniBoldLabel);
            pattern.VFXPrefab = (GameObject)EditorGUILayout.ObjectField("VFX", pattern.VFXPrefab, typeof(GameObject), false);
            pattern.SFXName = EditorGUILayout.TextField("SFX", pattern.SFXName);
            pattern.CameraShake = EditorGUILayout.FloatField("Camera Shake", pattern.CameraShake);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Actions
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Duplicate"))
            {
                var dup = pattern.Clone();
                dup.Name += " (Copy)";
                dup.StartTime += pattern.Duration + 0.5f;
                phase.Patterns.Add(dup);
                _selectedPatternIdx = phase.Patterns.Count - 1;
            }
            if (GUILayout.Button("Delete"))
            {
                phase.Patterns.RemoveAt(_selectedPatternIdx);
                _selectedPatternIdx = -1;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Export
            if (GUILayout.Button("📋 Export Boss Data"))
                ExportBossData();
        }

        private void ShowAddPatternMenu(float time)
        {
            GenericMenu menu = new GenericMenu();

            foreach (AttackType type in Enum.GetValues(typeof(AttackType)))
            {
                AttackType capturedType = type;
                menu.AddItem(new GUIContent($"Add {type}"), false, () => AddPattern(capturedType, time));
            }

            menu.ShowAsContext();
        }

        private void AddPattern(AttackType type, float startTime)
        {
            if (_selectedPhaseIdx < 0 || _selectedPhaseIdx >= _currentBoss.Phases.Count)
                return;

            var pattern = new AttackPattern
            {
                Name = type.ToString(),
                Type = type,
                StartTime = startTime,
                Duration = GetDefaultDuration(type),
                TelegraphTime = GetDefaultTelegraph(type),
                Damage = 100,
                Range = 5f,
                Angle = 90f,
                DodgeWindow = 0.5f,
                DodgeDirection = GetDefaultDodge(type)
            };

            _currentBoss.Phases[_selectedPhaseIdx].Patterns.Add(pattern);
            _selectedPatternIdx = _currentBoss.Phases[_selectedPhaseIdx].Patterns.Count - 1;
        }

        private float GetDefaultDuration(AttackType type)
        {
            return type switch
            {
                AttackType.Melee => 0.8f,
                AttackType.AoE => 1.5f,
                AttackType.Charge => 2f,
                AttackType.Beam => 3f,
                AttackType.Sweep => 1.2f,
                AttackType.Slam => 1f,
                AttackType.Projectile => 0.5f,
                AttackType.Summon => 2f,
                _ => 1f
            };
        }

        private float GetDefaultTelegraph(AttackType type)
        {
            return type switch
            {
                AttackType.AoE => 1.5f,
                AttackType.Charge => 1f,
                AttackType.Beam => 2f,
                AttackType.Slam => 0.8f,
                _ => 0.5f
            };
        }

        private DodgeDirection GetDefaultDodge(AttackType type)
        {
            return type switch
            {
                AttackType.Sweep => DodgeDirection.Back,
                AttackType.Charge => DodgeDirection.Left,
                AttackType.Beam => DodgeDirection.Left,
                AttackType.Slam => DodgeDirection.Back,
                AttackType.AoE => DodgeDirection.Back,
                _ => DodgeDirection.Any
            };
        }

        private float GetPhaseDuration()
        {
            if (_selectedPhaseIdx < 0 || _selectedPhaseIdx >= _currentBoss.Phases.Count)
                return 10f;

            var phase = _currentBoss.Phases[_selectedPhaseIdx];
            if (phase.Patterns.Count == 0) return 10f;

            return phase.Patterns.Max(p => p.StartTime + p.Duration) + 2f;
        }

        private Color GetPhaseColor(int index)
        {
            Color[] colors = { Color.green, Color.yellow, Color.red, new Color(0.8f, 0.2f, 0.8f) };
            return colors[index % colors.Length];
        }

        private void CreateDefaultBoss()
        {
            _currentBoss = new BossFightData
            {
                Name = "New Boss",
                MaxHealth = 100000,
                Level = 90,
                Phases = new List<BossPhase>
                {
                    new BossPhase
                    {
                        Name = "Phase 1",
                        HealthThreshold = 100,
                        SpeedMultiplier = 1f,
                        DamageMultiplier = 1f
                    }
                }
            };
        }

        private void LoadPreset_Dragon()
        {
            _currentBoss = new BossFightData
            {
                Name = "Ancient Dragon",
                MaxHealth = 500000,
                Level = 90,
                Phases = new List<BossPhase>
                {
                    new BossPhase
                    {
                        Name = "Grounded",
                        HealthThreshold = 100,
                        SpeedMultiplier = 1f,
                        DamageMultiplier = 1f,
                        Patterns = new List<AttackPattern>
                        {
                            new AttackPattern { Name = "Tail Sweep", Type = AttackType.Sweep, StartTime = 0, Duration = 1.2f, Angle = 180, Range = 8, Damage = 150, TelegraphTime = 0.8f },
                            new AttackPattern { Name = "Fire Breath", Type = AttackType.Beam, StartTime = 2, Duration = 3f, Angle = 60, Range = 15, Damage = 200, TelegraphTime = 1.5f },
                            new AttackPattern { Name = "Stomp", Type = AttackType.AoE, StartTime = 6, Duration = 1f, Range = 6, Damage = 180, TelegraphTime = 1f }
                        }
                    },
                    new BossPhase
                    {
                        Name = "Airborne",
                        HealthThreshold = 50,
                        SpeedMultiplier = 1.2f,
                        DamageMultiplier = 1.3f,
                        TransitionType = PhaseTransitionType.Invulnerable,
                        TransitionDuration = 3f,
                        Patterns = new List<AttackPattern>
                        {
                            new AttackPattern { Name = "Dive Bomb", Type = AttackType.Charge, StartTime = 0, Duration = 2f, Range = 20, Damage = 300, TelegraphTime = 1.5f },
                            new AttackPattern { Name = "Rain of Fire", Type = AttackType.Projectile, StartTime = 3, Duration = 4f, Range = 25, Damage = 100, TelegraphTime = 0.5f }
                        }
                    }
                }
            };
        }

        private void LoadPreset_Humanoid()
        {
            _currentBoss = new BossFightData
            {
                Name = "Dark Knight",
                MaxHealth = 200000,
                Level = 80,
                Phases = new List<BossPhase>
                {
                    new BossPhase
                    {
                        Name = "Sword Phase",
                        HealthThreshold = 100,
                        Patterns = new List<AttackPattern>
                        {
                            new AttackPattern { Name = "Slash Combo", Type = AttackType.Melee, StartTime = 0, Duration = 1.5f, Range = 3, Damage = 100, TelegraphTime = 0.3f },
                            new AttackPattern { Name = "Thrust", Type = AttackType.Charge, StartTime = 2, Duration = 1f, Range = 8, Damage = 150, TelegraphTime = 0.5f },
                            new AttackPattern { Name = "Sword Wave", Type = AttackType.Projectile, StartTime = 4, Duration = 0.8f, Range = 15, Damage = 120, TelegraphTime = 0.4f }
                        }
                    },
                    new BossPhase
                    {
                        Name = "Rage Mode",
                        HealthThreshold = 30,
                        SpeedMultiplier = 1.5f,
                        DamageMultiplier = 1.5f,
                        TransitionType = PhaseTransitionType.Cutscene,
                        TransitionDuration = 2f
                    }
                }
            };
        }

        private void LoadPreset_Swarm()
        {
            _currentBoss = new BossFightData
            {
                Name = "Hivemind",
                MaxHealth = 300000,
                Level = 85,
                Phases = new List<BossPhase>
                {
                    new BossPhase
                    {
                        Name = "Swarm Control",
                        HealthThreshold = 100,
                        Patterns = new List<AttackPattern>
                        {
                            new AttackPattern { Name = "Summon Adds", Type = AttackType.Summon, StartTime = 0, Duration = 2f, TelegraphTime = 1f },
                            new AttackPattern { Name = "Poison Cloud", Type = AttackType.AoE, StartTime = 3, Duration = 5f, Range = 10, Damage = 50, TelegraphTime = 1.5f },
                            new AttackPattern { Name = "Swarm Attack", Type = AttackType.Ranged, StartTime = 8, Duration = 3f, Range = 20, Damage = 80 }
                        }
                    }
                }
            };
        }

        private void ExportBossData()
        {
            string path = EditorUtility.SaveFilePanel("Export Boss", "", _currentBoss.Name, "json");
            if (string.IsNullOrEmpty(path)) return;

            string json = JsonUtility.ToJson(_currentBoss, true);
            System.IO.File.WriteAllText(path, json);
            Debug.Log($"Exported boss to {path}");
        }

        // Data classes
        [Serializable]
        private class BossFightData
        {
            public string Name;
            public float MaxHealth;
            public int Level;
            public List<BossPhase> Phases = new List<BossPhase>();
        }

        private enum PhaseTransitionType { Instant, Invulnerable, Cutscene, Stagger }

        [Serializable]
        private class BossPhase
        {
            public string Name;
            public int HealthThreshold;
            public float SpeedMultiplier = 1f;
            public float DamageMultiplier = 1f;
            public PhaseTransitionType TransitionType;
            public float TransitionDuration;
            public List<AttackPattern> Patterns = new List<AttackPattern>();
        }

        [Serializable]
        private class AttackPattern
        {
            public string Name;
            public AttackType Type;
            public float StartTime;
            public float Duration;
            public float TelegraphTime;
            public float Cooldown;

            public float Damage;
            public float Range;
            public float Angle;
            public float Knockback;

            public TelegraphType TelegraphType;
            public DodgeDirection DodgeDirection;
            public float DodgeWindow;

            public GameObject VFXPrefab;
            public string SFXName;
            public float CameraShake;

            public AttackPattern Clone()
            {
                return (AttackPattern)MemberwiseClone();
            }
        }

        private class PreviewMarker
        {
            public Vector2 Position;
            public float Radius;
            public Color Color;
            public float Lifetime;
        }
    }
}
