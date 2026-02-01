using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Tutorial Sequence Editor for creating in-game tutorials and onboarding flows.
/// Supports step-by-step guides, tooltips, highlights, and condition-based progression.
/// </summary>
public class TutorialSequenceEditor : EditorWindow
{
    [MenuItem("EpicLegends/Tools/Tutorial Sequence Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<TutorialSequenceEditor>("Tutorial Editor");
        window.minSize = new Vector2(500, 600);
    }

    // Data structures
    [System.Serializable]
    public class TutorialSequence
    {
        public string id = "";
        public string name = "New Tutorial";
        public string description = "";
        public TutorialTrigger trigger = TutorialTrigger.Manual;
        public string triggerCondition = "";
        public bool canSkip = true;
        public bool pauseGame;
        public bool showOnce = true;
        public int priority = 0;
        public List<TutorialStep> steps = new List<TutorialStep>();
    }

    [System.Serializable]
    public class TutorialStep
    {
        public string id = "";
        public string title = "";
        public string message = "";
        public string localizationKey = "";
        public StepType type = StepType.Tooltip;
        public Vector2 position;
        public AnchorPosition anchor = AnchorPosition.Center;
        public string targetElement = "";
        public bool highlightTarget = true;
        public Color highlightColor = new Color(1f, 0.8f, 0f, 0.5f);
        public StepAction requiredAction = StepAction.None;
        public string actionParameter = "";
        public float autoAdvanceDelay = 0f;
        public string voiceoverPath = "";
        public List<StepCondition> conditions = new List<StepCondition>();
        public List<StepReward> rewards = new List<StepReward>();
    }

    [System.Serializable]
    public class StepCondition
    {
        public ConditionType type = ConditionType.None;
        public string parameter = "";
        public string value = "";
    }

    [System.Serializable]
    public class StepReward
    {
        public RewardType type = RewardType.Item;
        public string itemId = "";
        public int amount = 1;
    }

    public enum TutorialTrigger { Manual, OnFirstPlay, OnLevelStart, OnUIOpen, OnItemPickup, OnCombatStart, OnQuestAccept }
    public enum StepType { Tooltip, FullScreen, Highlight, Arrow, Hand, Video, Interactive }
    public enum AnchorPosition { TopLeft, Top, TopRight, Left, Center, Right, BottomLeft, Bottom, BottomRight }
    public enum StepAction { None, Click, Drag, Move, Attack, UseItem, OpenMenu, Talk, Custom }
    public enum ConditionType { None, HasItem, HasQuest, LevelReached, FlagSet, StatCheck }
    public enum RewardType { Item, Currency, Experience, Unlock }

    // State
    private List<TutorialSequence> _sequences = new List<TutorialSequence>();
    private int _selectedSequenceIndex = -1;
    private int _selectedStepIndex = -1;

    // UI
    private Vector2 _leftScroll;
    private Vector2 _rightScroll;
    private int _currentTab;
    private readonly string[] _tabNames = { "Sequences", "Preview", "Settings" };

    // Preview
    private bool _previewMode;
    private int _previewStepIndex;

    private void OnEnable()
    {
        if (_sequences.Count == 0)
        {
            CreateDefaultSequences();
        }
    }

    private void OnGUI()
    {
        DrawToolbar();

        _currentTab = GUILayout.Toolbar(_currentTab, _tabNames, GUILayout.Height(30));

        switch (_currentTab)
        {
            case 0: DrawSequencesTab(); break;
            case 1: DrawPreviewTab(); break;
            case 2: DrawSettingsTab(); break;
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(40)))
        {
            _sequences.Add(new TutorialSequence
            {
                id = System.Guid.NewGuid().ToString().Substring(0, 8),
                name = $"Tutorial {_sequences.Count + 1}"
            });
            _selectedSequenceIndex = _sequences.Count - 1;
        }

        if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(40)))
            SaveSequences();

        if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(40)))
            LoadSequences();

        GUILayout.FlexibleSpace();

        EditorGUILayout.LabelField($"Sequences: {_sequences.Count}", GUILayout.Width(100));

        EditorGUILayout.EndHorizontal();
    }

    #region Sequences Tab

    private void DrawSequencesTab()
    {
        EditorGUILayout.BeginHorizontal();

        // Left panel - sequence list
        DrawSequenceList();

        // Right panel - step editor
        DrawStepEditor();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSequenceList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(250));
        _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

        EditorGUILayout.LabelField("Tutorial Sequences", EditorStyles.boldLabel);

        for (int i = 0; i < _sequences.Count; i++)
        {
            DrawSequenceItem(i);
        }

        EditorGUILayout.Space(10);

        // Quick add presets
        EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Movement"))
            AddPresetSequence("Movement Tutorial", CreateMovementPreset());
        if (GUILayout.Button("Combat"))
            AddPresetSequence("Combat Tutorial", CreateCombatPreset());
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("UI"))
            AddPresetSequence("UI Tutorial", CreateUIPreset());
        if (GUILayout.Button("Gacha"))
            AddPresetSequence("Gacha Tutorial", CreateGachaPreset());
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawSequenceItem(int index)
    {
        var seq = _sequences[index];
        bool isSelected = index == _selectedSequenceIndex;

        EditorGUILayout.BeginVertical(isSelected ? "SelectionRect" : EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(isSelected ? "▼" : "►", GUILayout.Width(25)))
        {
            _selectedSequenceIndex = isSelected ? -1 : index;
            _selectedStepIndex = -1;
        }

        seq.name = EditorGUILayout.TextField(seq.name);

        GUI.color = Color.red;
        if (GUILayout.Button("X", GUILayout.Width(20)))
        {
            _sequences.RemoveAt(index);
            if (_selectedSequenceIndex >= _sequences.Count)
                _selectedSequenceIndex = -1;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();

        if (isSelected)
        {
            DrawSequenceDetails(seq);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSequenceDetails(TutorialSequence seq)
    {
        EditorGUI.indentLevel++;

        seq.id = EditorGUILayout.TextField("ID", seq.id);
        seq.description = EditorGUILayout.TextField("Description", seq.description);
        seq.trigger = (TutorialTrigger)EditorGUILayout.EnumPopup("Trigger", seq.trigger);

        if (seq.trigger != TutorialTrigger.Manual)
        {
            seq.triggerCondition = EditorGUILayout.TextField("Condition", seq.triggerCondition);
        }

        seq.canSkip = EditorGUILayout.Toggle("Can Skip", seq.canSkip);
        seq.pauseGame = EditorGUILayout.Toggle("Pause Game", seq.pauseGame);
        seq.showOnce = EditorGUILayout.Toggle("Show Once", seq.showOnce);
        seq.priority = EditorGUILayout.IntField("Priority", seq.priority);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField($"Steps: {seq.steps.Count}", EditorStyles.miniBoldLabel);

        // Steps mini list
        for (int i = 0; i < seq.steps.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            bool stepSelected = i == _selectedStepIndex;
            GUI.color = stepSelected ? Color.yellow : Color.white;

            if (GUILayout.Button($"{i + 1}. {seq.steps[i].title}", EditorStyles.miniButton))
            {
                _selectedStepIndex = stepSelected ? -1 : i;
            }

            GUI.color = Color.white;

            if (GUILayout.Button("▲", GUILayout.Width(20)) && i > 0)
            {
                var temp = seq.steps[i - 1];
                seq.steps[i - 1] = seq.steps[i];
                seq.steps[i] = temp;
            }
            if (GUILayout.Button("▼", GUILayout.Width(20)) && i < seq.steps.Count - 1)
            {
                var temp = seq.steps[i + 1];
                seq.steps[i + 1] = seq.steps[i];
                seq.steps[i] = temp;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Add Step"))
        {
            seq.steps.Add(new TutorialStep
            {
                id = System.Guid.NewGuid().ToString().Substring(0, 8),
                title = $"Step {seq.steps.Count + 1}"
            });
            _selectedStepIndex = seq.steps.Count - 1;
        }

        EditorGUI.indentLevel--;
    }

    private void DrawStepEditor()
    {
        EditorGUILayout.BeginVertical();
        _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

        if (_selectedSequenceIndex < 0 || _selectedSequenceIndex >= _sequences.Count)
        {
            EditorGUILayout.HelpBox("Select a sequence to edit.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        var seq = _sequences[_selectedSequenceIndex];

        if (_selectedStepIndex < 0 || _selectedStepIndex >= seq.steps.Count)
        {
            EditorGUILayout.HelpBox("Select a step to edit, or add a new step.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        var step = seq.steps[_selectedStepIndex];

        EditorGUILayout.LabelField($"Step {_selectedStepIndex + 1}: {step.title}", EditorStyles.boldLabel);

        EditorGUILayout.Space(10);

        // Basic info
        step.id = EditorGUILayout.TextField("ID", step.id);
        step.title = EditorGUILayout.TextField("Title", step.title);

        EditorGUILayout.LabelField("Message");
        step.message = EditorGUILayout.TextArea(step.message, GUILayout.Height(60));

        step.localizationKey = EditorGUILayout.TextField("Localization Key", step.localizationKey);

        EditorGUILayout.Space(10);

        // Display settings
        EditorGUILayout.LabelField("Display", EditorStyles.boldLabel);
        step.type = (StepType)EditorGUILayout.EnumPopup("Type", step.type);
        step.anchor = (AnchorPosition)EditorGUILayout.EnumPopup("Anchor", step.anchor);
        step.position = EditorGUILayout.Vector2Field("Offset", step.position);

        EditorGUILayout.Space(10);

        // Target highlight
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        step.targetElement = EditorGUILayout.TextField("Target Element", step.targetElement);
        step.highlightTarget = EditorGUILayout.Toggle("Highlight Target", step.highlightTarget);
        if (step.highlightTarget)
        {
            step.highlightColor = EditorGUILayout.ColorField("Highlight Color", step.highlightColor);
        }

        EditorGUILayout.Space(10);

        // Required action
        EditorGUILayout.LabelField("Progression", EditorStyles.boldLabel);
        step.requiredAction = (StepAction)EditorGUILayout.EnumPopup("Required Action", step.requiredAction);
        if (step.requiredAction != StepAction.None)
        {
            step.actionParameter = EditorGUILayout.TextField("Action Parameter", step.actionParameter);
        }
        step.autoAdvanceDelay = EditorGUILayout.FloatField("Auto-Advance Delay (0=none)", step.autoAdvanceDelay);

        EditorGUILayout.Space(10);

        // Audio
        EditorGUILayout.LabelField("Audio", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        step.voiceoverPath = EditorGUILayout.TextField("Voiceover", step.voiceoverPath);
        if (GUILayout.Button("...", GUILayout.Width(25)))
        {
            string path = EditorUtility.OpenFilePanel("Select Audio", "Assets", "wav,mp3,ogg");
            if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
            {
                step.voiceoverPath = "Assets" + path.Substring(Application.dataPath.Length);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Conditions
        DrawStepConditions(step);

        EditorGUILayout.Space(10);

        // Rewards
        DrawStepRewards(step);

        EditorGUILayout.Space(20);

        // Delete button
        GUI.color = Color.red;
        if (GUILayout.Button("Delete Step", GUILayout.Height(25)))
        {
            seq.steps.RemoveAt(_selectedStepIndex);
            _selectedStepIndex = Mathf.Min(_selectedStepIndex, seq.steps.Count - 1);
        }
        GUI.color = Color.white;

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawStepConditions(TutorialStep step)
    {
        EditorGUILayout.LabelField("Conditions", EditorStyles.boldLabel);

        for (int i = 0; i < step.conditions.Count; i++)
        {
            var cond = step.conditions[i];

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            cond.type = (ConditionType)EditorGUILayout.EnumPopup(cond.type, GUILayout.Width(100));
            cond.parameter = EditorGUILayout.TextField(cond.parameter, GUILayout.Width(100));
            cond.value = EditorGUILayout.TextField(cond.value, GUILayout.Width(80));

            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                step.conditions.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Add Condition", EditorStyles.miniButton))
        {
            step.conditions.Add(new StepCondition());
        }
    }

    private void DrawStepRewards(TutorialStep step)
    {
        EditorGUILayout.LabelField("Completion Rewards", EditorStyles.boldLabel);

        for (int i = 0; i < step.rewards.Count; i++)
        {
            var reward = step.rewards[i];

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            reward.type = (RewardType)EditorGUILayout.EnumPopup(reward.type, GUILayout.Width(80));
            reward.itemId = EditorGUILayout.TextField(reward.itemId, GUILayout.Width(100));
            reward.amount = EditorGUILayout.IntField(reward.amount, GUILayout.Width(50));

            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                step.rewards.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Add Reward", EditorStyles.miniButton))
        {
            step.rewards.Add(new StepReward());
        }
    }

    #endregion

    #region Preview Tab

    private void DrawPreviewTab()
    {
        EditorGUILayout.LabelField("Tutorial Preview", EditorStyles.boldLabel);

        if (_selectedSequenceIndex < 0 || _selectedSequenceIndex >= _sequences.Count)
        {
            EditorGUILayout.HelpBox("Select a sequence to preview.", MessageType.Info);
            return;
        }

        var seq = _sequences[_selectedSequenceIndex];

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Previewing: {seq.name}", EditorStyles.boldLabel);

        _previewMode = GUILayout.Toggle(_previewMode, "Preview Mode", "Button", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        if (seq.steps.Count == 0)
        {
            EditorGUILayout.HelpBox("This sequence has no steps.", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(10);

        // Preview controls
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = _previewStepIndex > 0;
        if (GUILayout.Button("◄ Previous"))
            _previewStepIndex--;
        GUI.enabled = true;

        EditorGUILayout.LabelField($"Step {_previewStepIndex + 1} / {seq.steps.Count}", GUILayout.Width(100));

        GUI.enabled = _previewStepIndex < seq.steps.Count - 1;
        if (GUILayout.Button("Next ►"))
            _previewStepIndex++;
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        _previewStepIndex = Mathf.Clamp(_previewStepIndex, 0, seq.steps.Count - 1);

        EditorGUILayout.Space(20);

        // Preview display
        var step = seq.steps[_previewStepIndex];
        DrawStepPreview(step);
    }

    private void DrawStepPreview(TutorialStep step)
    {
        // Simulate tutorial display
        Rect previewRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
            GUILayout.ExpandWidth(true), GUILayout.Height(300));

        EditorGUI.DrawRect(previewRect, new Color(0.1f, 0.1f, 0.1f));

        // Draw based on step type
        Rect tooltipRect = GetAnchoredRect(previewRect, step.anchor, new Vector2(300, 150));
        tooltipRect.x += step.position.x;
        tooltipRect.y += step.position.y;

        // Background
        EditorGUI.DrawRect(tooltipRect, new Color(0.2f, 0.2f, 0.3f, 0.95f));

        // Border
        DrawRectBorder(tooltipRect, new Color(0.4f, 0.6f, 0.9f), 2);

        // Title
        GUI.Label(new Rect(tooltipRect.x + 10, tooltipRect.y + 10, tooltipRect.width - 20, 25),
                 step.title, new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, normal = { textColor = Color.white } });

        // Message
        GUI.Label(new Rect(tooltipRect.x + 10, tooltipRect.y + 40, tooltipRect.width - 20, 80),
                 step.message, new GUIStyle(EditorStyles.wordWrappedLabel) { normal = { textColor = Color.white } });

        // Action hint
        if (step.requiredAction != StepAction.None)
        {
            string actionHint = GetActionHint(step.requiredAction, step.actionParameter);
            GUI.Label(new Rect(tooltipRect.x + 10, tooltipRect.y + tooltipRect.height - 30, tooltipRect.width - 20, 25),
                     actionHint, new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.yellow } });
        }

        // Target indicator
        if (step.highlightTarget && !string.IsNullOrEmpty(step.targetElement))
        {
            Rect targetRect = new Rect(previewRect.x + 50, previewRect.y + 50, 100, 50);
            EditorGUI.DrawRect(targetRect, step.highlightColor);
            GUI.Label(targetRect, step.targetElement, EditorStyles.centeredGreyMiniLabel);
        }

        // Step type indicator
        DrawStepTypeIndicator(step.type, tooltipRect);
    }

    private Rect GetAnchoredRect(Rect container, AnchorPosition anchor, Vector2 size)
    {
        float x = container.x, y = container.y;

        switch (anchor)
        {
            case AnchorPosition.TopLeft:
                x = container.x + 10;
                y = container.y + 10;
                break;
            case AnchorPosition.Top:
                x = container.x + (container.width - size.x) / 2;
                y = container.y + 10;
                break;
            case AnchorPosition.TopRight:
                x = container.x + container.width - size.x - 10;
                y = container.y + 10;
                break;
            case AnchorPosition.Left:
                x = container.x + 10;
                y = container.y + (container.height - size.y) / 2;
                break;
            case AnchorPosition.Center:
                x = container.x + (container.width - size.x) / 2;
                y = container.y + (container.height - size.y) / 2;
                break;
            case AnchorPosition.Right:
                x = container.x + container.width - size.x - 10;
                y = container.y + (container.height - size.y) / 2;
                break;
            case AnchorPosition.BottomLeft:
                x = container.x + 10;
                y = container.y + container.height - size.y - 10;
                break;
            case AnchorPosition.Bottom:
                x = container.x + (container.width - size.x) / 2;
                y = container.y + container.height - size.y - 10;
                break;
            case AnchorPosition.BottomRight:
                x = container.x + container.width - size.x - 10;
                y = container.y + container.height - size.y - 10;
                break;
        }

        return new Rect(x, y, size.x, size.y);
    }

    private void DrawRectBorder(Rect rect, Color color, float width)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - width, rect.width, width), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.x + rect.width - width, rect.y, width, rect.height), color);
    }

    private string GetActionHint(StepAction action, string parameter)
    {
        switch (action)
        {
            case StepAction.Click: return $"Click {parameter}";
            case StepAction.Drag: return $"Drag {parameter}";
            case StepAction.Move: return "Move your character";
            case StepAction.Attack: return "Attack the enemy";
            case StepAction.UseItem: return $"Use {parameter}";
            case StepAction.OpenMenu: return $"Open {parameter} menu";
            case StepAction.Talk: return $"Talk to {parameter}";
            case StepAction.Custom: return parameter;
            default: return "";
        }
    }

    private void DrawStepTypeIndicator(StepType type, Rect tooltipRect)
    {
        string icon = "";
        switch (type)
        {
            case StepType.Tooltip: icon = "i"; break;
            case StepType.FullScreen: icon = "[]"; break;
            case StepType.Highlight: icon = "*"; break;
            case StepType.Arrow: icon = "→"; break;
            case StepType.Hand: icon = "☞"; break;
            case StepType.Video: icon = "▶"; break;
            case StepType.Interactive: icon = "⚡"; break;
        }

        GUI.Label(new Rect(tooltipRect.x + tooltipRect.width - 25, tooltipRect.y + 5, 20, 20),
                 icon, new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, normal = { textColor = Color.cyan } });
    }

    #endregion

    #region Settings Tab

    private void DrawSettingsTab()
    {
        EditorGUILayout.LabelField("Export Settings", EditorStyles.boldLabel);

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Export All to JSON", GUILayout.Height(30)))
        {
            ExportToJson();
        }

        if (GUILayout.Button("Generate ScriptableObjects", GUILayout.Height(30)))
        {
            GenerateScriptableObjects();
        }

        if (GUILayout.Button("Generate Runtime Script", GUILayout.Height(30)))
        {
            GenerateRuntimeScript();
        }

        EditorGUILayout.Space(20);

        EditorGUILayout.LabelField("Localization", EditorStyles.boldLabel);

        if (GUILayout.Button("Export Localization Keys", GUILayout.Height(25)))
        {
            ExportLocalizationKeys();
        }

        EditorGUILayout.Space(20);

        EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);

        int totalSteps = _sequences.Sum(s => s.steps.Count);
        int withVoiceover = _sequences.Sum(s => s.steps.Count(st => !string.IsNullOrEmpty(st.voiceoverPath)));
        int withRewards = _sequences.Sum(s => s.steps.Count(st => st.rewards.Count > 0));

        EditorGUILayout.LabelField($"Total Sequences: {_sequences.Count}");
        EditorGUILayout.LabelField($"Total Steps: {totalSteps}");
        EditorGUILayout.LabelField($"Steps with Voiceover: {withVoiceover}");
        EditorGUILayout.LabelField($"Steps with Rewards: {withRewards}");
    }

    #endregion

    #region Presets

    private void CreateDefaultSequences()
    {
        // Empty by default - user creates their own
    }

    private void AddPresetSequence(string name, List<TutorialStep> steps)
    {
        var seq = new TutorialSequence
        {
            id = System.Guid.NewGuid().ToString().Substring(0, 8),
            name = name,
            steps = steps
        };
        _sequences.Add(seq);
        _selectedSequenceIndex = _sequences.Count - 1;
    }

    private List<TutorialStep> CreateMovementPreset()
    {
        return new List<TutorialStep>
        {
            new TutorialStep
            {
                id = "move1",
                title = "Movement",
                message = "Use WASD or the left joystick to move your character.",
                type = StepType.Tooltip,
                anchor = AnchorPosition.Bottom,
                requiredAction = StepAction.Move
            },
            new TutorialStep
            {
                id = "move2",
                title = "Sprint",
                message = "Hold Shift or press the sprint button to run faster.",
                type = StepType.Tooltip,
                anchor = AnchorPosition.Bottom,
                requiredAction = StepAction.Custom,
                actionParameter = "Sprint"
            },
            new TutorialStep
            {
                id = "move3",
                title = "Jump",
                message = "Press Space or the jump button to jump.",
                type = StepType.Tooltip,
                anchor = AnchorPosition.Bottom,
                requiredAction = StepAction.Custom,
                actionParameter = "Jump"
            }
        };
    }

    private List<TutorialStep> CreateCombatPreset()
    {
        return new List<TutorialStep>
        {
            new TutorialStep
            {
                id = "combat1",
                title = "Basic Attack",
                message = "Click or press the attack button to perform a basic attack.",
                type = StepType.Highlight,
                anchor = AnchorPosition.Center,
                requiredAction = StepAction.Attack,
                targetElement = "Enemy"
            },
            new TutorialStep
            {
                id = "combat2",
                title = "Elemental Skill",
                message = "Press E to use your elemental skill.",
                type = StepType.Tooltip,
                anchor = AnchorPosition.Right,
                requiredAction = StepAction.Custom,
                actionParameter = "UseSkill"
            },
            new TutorialStep
            {
                id = "combat3",
                title = "Elemental Burst",
                message = "Press Q when your energy is full to unleash your elemental burst!",
                type = StepType.FullScreen,
                anchor = AnchorPosition.Center,
                requiredAction = StepAction.Custom,
                actionParameter = "UseBurst"
            }
        };
    }

    private List<TutorialStep> CreateUIPreset()
    {
        return new List<TutorialStep>
        {
            new TutorialStep
            {
                id = "ui1",
                title = "Inventory",
                message = "Press I or tap the bag icon to open your inventory.",
                type = StepType.Arrow,
                anchor = AnchorPosition.Right,
                requiredAction = StepAction.OpenMenu,
                actionParameter = "Inventory",
                targetElement = "InventoryButton"
            },
            new TutorialStep
            {
                id = "ui2",
                title = "Equipment",
                message = "Drag items to the equipment slots to equip them.",
                type = StepType.Hand,
                anchor = AnchorPosition.Center,
                requiredAction = StepAction.Drag,
                targetElement = "EquipmentSlot"
            }
        };
    }

    private List<TutorialStep> CreateGachaPreset()
    {
        return new List<TutorialStep>
        {
            new TutorialStep
            {
                id = "gacha1",
                title = "Wish System",
                message = "Welcome to the Wish system! Here you can obtain new characters and weapons.",
                type = StepType.FullScreen,
                anchor = AnchorPosition.Center,
                autoAdvanceDelay = 3f
            },
            new TutorialStep
            {
                id = "gacha2",
                title = "Wish Currency",
                message = "You need Intertwined Fate or Acquaint Fate to make wishes.",
                type = StepType.Tooltip,
                anchor = AnchorPosition.Top,
                targetElement = "CurrencyDisplay"
            },
            new TutorialStep
            {
                id = "gacha3",
                title = "Make a Wish",
                message = "Tap 'Wish x1' for a single wish, or 'Wish x10' for ten wishes at once!",
                type = StepType.Arrow,
                anchor = AnchorPosition.Bottom,
                requiredAction = StepAction.Click,
                actionParameter = "WishButton",
                targetElement = "WishButton"
            }
        };
    }

    #endregion

    #region Save/Load

    private void SaveSequences()
    {
        var data = new TutorialData { sequences = _sequences };
        string json = JsonUtility.ToJson(data, true);

        string path = EditorUtility.SaveFilePanel("Save Tutorials", "Assets", "Tutorials", "json");
        if (!string.IsNullOrEmpty(path))
        {
            System.IO.File.WriteAllText(path, json);
            Debug.Log($"Tutorials saved to: {path}");
        }
    }

    private void LoadSequences()
    {
        string path = EditorUtility.OpenFilePanel("Load Tutorials", "Assets", "json");
        if (!string.IsNullOrEmpty(path))
        {
            string json = System.IO.File.ReadAllText(path);
            var data = JsonUtility.FromJson<TutorialData>(json);
            _sequences = data.sequences;
            _selectedSequenceIndex = -1;
            _selectedStepIndex = -1;
            Debug.Log($"Loaded {_sequences.Count} tutorials");
        }
    }

    private void ExportToJson()
    {
        SaveSequences();
    }

    private void GenerateScriptableObjects()
    {
        Debug.Log("Generate ScriptableObjects - implement based on your data structure");
    }

    private void GenerateRuntimeScript()
    {
        Debug.Log("Generate runtime tutorial manager script");
    }

    private void ExportLocalizationKeys()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        foreach (var seq in _sequences)
        {
            sb.AppendLine($"# {seq.name}");
            foreach (var step in seq.steps)
            {
                string key = string.IsNullOrEmpty(step.localizationKey) ?
                    $"TUT_{seq.id}_{step.id}" : step.localizationKey;
                sb.AppendLine($"{key}_TITLE={step.title}");
                sb.AppendLine($"{key}_MSG={step.message}");
            }
            sb.AppendLine();
        }

        string path = EditorUtility.SaveFilePanel("Export Localization", "", "tutorial_localization", "txt");
        if (!string.IsNullOrEmpty(path))
        {
            System.IO.File.WriteAllText(path, sb.ToString());
            Debug.Log($"Localization exported to: {path}");
        }
    }

    [System.Serializable]
    private class TutorialData
    {
        public List<TutorialSequence> sequences = new List<TutorialSequence>();
    }

    #endregion
}
