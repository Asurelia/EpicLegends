using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Visual Animation State Machine Builder for creating Animator Controllers.
/// Provides node-based editing, transition visualization, and preset templates.
/// </summary>
public class AnimationStateMachineBuilder : EditorWindow
{
    [MenuItem("EpicLegends/Tools/Animation State Machine Builder")]
    public static void ShowWindow()
    {
        var window = GetWindow<AnimationStateMachineBuilder>("Animation State Machine");
        window.minSize = new Vector2(900, 600);
    }

    // Data structures
    [System.Serializable]
    public class StateNode
    {
        public string name = "New State";
        public AnimationClip clip;
        public Vector2 position;
        public float speed = 1f;
        public bool isDefault;
        public bool isLoop = true;
        public string tag = "";
        public List<string> behaviors = new List<string>();
        public Color nodeColor = new Color(0.3f, 0.3f, 0.5f);
    }

    [System.Serializable]
    public class StateTransition
    {
        public int fromIndex;
        public int toIndex;
        public string conditionParam = "";
        public ConditionType conditionType = ConditionType.Trigger;
        public float threshold = 0f;
        public bool hasExitTime = true;
        public float exitTime = 0.9f;
        public float transitionDuration = 0.1f;
        public bool canInterrupt = false;
    }

    public enum ConditionType { Trigger, BoolTrue, BoolFalse, FloatGreater, FloatLess, IntEquals, IntGreater, IntLess }

    [System.Serializable]
    public class AnimatorParameter
    {
        public string name = "NewParam";
        public AnimatorControllerParameterType type = AnimatorControllerParameterType.Trigger;
        public float defaultFloat;
        public int defaultInt;
        public bool defaultBool;
    }

    [System.Serializable]
    public class AnimatorLayer
    {
        public string name = "Base Layer";
        public float weight = 1f;
        public AnimatorLayerBlendingMode blendingMode = AnimatorLayerBlendingMode.Override;
        public List<StateNode> states = new List<StateNode>();
        public List<StateTransition> transitions = new List<StateTransition>();
    }

    // State
    private List<AnimatorLayer> _layers = new List<AnimatorLayer>();
    private List<AnimatorParameter> _parameters = new List<AnimatorParameter>();
    private int _currentLayerIndex;
    private int _selectedStateIndex = -1;
    private int _selectedTransitionIndex = -1;
    private Vector2 _scrollPos;
    private Vector2 _canvasOffset;
    private float _zoom = 1f;
    private bool _isDragging;
    private bool _isPanning;
    private bool _isCreatingTransition;
    private int _transitionFromIndex = -1;
    private Vector2 _lastMousePos;
    private string _controllerName = "NewAnimatorController";
    private string _outputPath = "Assets/Animations/Controllers";

    // UI State
    private int _currentTab;
    private readonly string[] _tabNames = { "Graph", "States", "Parameters", "Presets", "Export" };
    private Vector2 _statesScroll;
    private Vector2 _paramsScroll;
    private Vector2 _presetsScroll;

    // Styling
    private const float NODE_WIDTH = 150f;
    private const float NODE_HEIGHT = 60f;
    private GUIStyle _nodeStyle;
    private GUIStyle _selectedNodeStyle;
    private GUIStyle _defaultNodeStyle;

    private void OnEnable()
    {
        if (_layers.Count == 0)
        {
            _layers.Add(new AnimatorLayer { name = "Base Layer" });
        }
        InitStyles();
    }

    private void InitStyles()
    {
        _nodeStyle = new GUIStyle();
        _nodeStyle.normal.background = MakeColorTexture(new Color(0.25f, 0.25f, 0.35f));
        _nodeStyle.border = new RectOffset(8, 8, 8, 8);
        _nodeStyle.padding = new RectOffset(10, 10, 5, 5);
        _nodeStyle.alignment = TextAnchor.MiddleCenter;
        _nodeStyle.normal.textColor = Color.white;
        _nodeStyle.fontStyle = FontStyle.Bold;

        _selectedNodeStyle = new GUIStyle(_nodeStyle);
        _selectedNodeStyle.normal.background = MakeColorTexture(new Color(0.3f, 0.5f, 0.8f));

        _defaultNodeStyle = new GUIStyle(_nodeStyle);
        _defaultNodeStyle.normal.background = MakeColorTexture(new Color(0.6f, 0.4f, 0.2f));
    }

    private Texture2D MakeColorTexture(Color color)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    private void OnGUI()
    {
        if (_nodeStyle == null) InitStyles();

        DrawToolbar();

        _currentTab = GUILayout.Toolbar(_currentTab, _tabNames, GUILayout.Height(30));

        switch (_currentTab)
        {
            case 0: DrawGraphTab(); break;
            case 1: DrawStatesTab(); break;
            case 2: DrawParametersTab(); break;
            case 3: DrawPresetsTab(); break;
            case 4: DrawExportTab(); break;
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            if (EditorUtility.DisplayDialog("New Controller", "Clear current work?", "Yes", "No"))
            {
                _layers.Clear();
                _layers.Add(new AnimatorLayer { name = "Base Layer" });
                _parameters.Clear();
                _selectedStateIndex = -1;
                _selectedTransitionIndex = -1;
            }
        }

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Layer:", GUILayout.Width(40));
        string[] layerNames = _layers.Select(l => l.name).ToArray();
        _currentLayerIndex = EditorGUILayout.Popup(_currentLayerIndex, layerNames, EditorStyles.toolbarPopup, GUILayout.Width(150));

        if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(25)))
        {
            _layers.Add(new AnimatorLayer { name = $"Layer {_layers.Count}" });
        }

        if (_layers.Count > 1 && GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(25)))
        {
            _layers.RemoveAt(_currentLayerIndex);
            _currentLayerIndex = Mathf.Clamp(_currentLayerIndex, 0, _layers.Count - 1);
        }

        GUILayout.FlexibleSpace();

        EditorGUILayout.LabelField($"States: {CurrentLayer.states.Count} | Transitions: {CurrentLayer.transitions.Count}", GUILayout.Width(200));

        EditorGUILayout.EndHorizontal();
    }

    private AnimatorLayer CurrentLayer => _layers[Mathf.Clamp(_currentLayerIndex, 0, _layers.Count - 1)];

    #region Graph Tab

    private void DrawGraphTab()
    {
        Rect canvasRect = new Rect(0, 80, position.width, position.height - 80);

        // Handle input
        HandleGraphInput(canvasRect);

        // Draw background grid
        DrawGrid(canvasRect, 20f * _zoom, 0.2f, Color.gray);
        DrawGrid(canvasRect, 100f * _zoom, 0.4f, Color.gray);

        // Begin clipped area
        GUI.BeginGroup(canvasRect);

        // Draw transitions
        DrawTransitions();

        // Draw creating transition line
        if (_isCreatingTransition && _transitionFromIndex >= 0 && _transitionFromIndex < CurrentLayer.states.Count)
        {
            Vector2 fromPos = GetNodeCenter(CurrentLayer.states[_transitionFromIndex]);
            Vector2 toPos = Event.current.mousePosition;
            DrawArrow(fromPos, toPos, Color.yellow, 3f);
            Repaint();
        }

        // Draw nodes
        DrawNodes();

        GUI.EndGroup();

        // Draw mini toolbar
        DrawGraphToolbar(canvasRect);

        // Instructions
        Rect instructRect = new Rect(10, canvasRect.y + canvasRect.height - 60, 400, 50);
        EditorGUI.HelpBox(instructRect, "Right-click: Add State | Drag: Move | Ctrl+Click: Create Transition | Del: Delete", MessageType.Info);
    }

    private void DrawGrid(Rect rect, float spacing, float opacity, Color color)
    {
        int widthDivs = Mathf.CeilToInt(rect.width / spacing);
        int heightDivs = Mathf.CeilToInt(rect.height / spacing);

        Handles.BeginGUI();
        Handles.color = new Color(color.r, color.g, color.b, opacity);

        Vector3 offset = new Vector3(_canvasOffset.x % spacing, _canvasOffset.y % spacing, 0);

        for (int i = 0; i <= widthDivs; i++)
        {
            Handles.DrawLine(
                new Vector3(spacing * i + offset.x + rect.x, rect.y, 0),
                new Vector3(spacing * i + offset.x + rect.x, rect.y + rect.height, 0)
            );
        }

        for (int j = 0; j <= heightDivs; j++)
        {
            Handles.DrawLine(
                new Vector3(rect.x, spacing * j + offset.y + rect.y, 0),
                new Vector3(rect.x + rect.width, spacing * j + offset.y + rect.y, 0)
            );
        }

        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private void HandleGraphInput(Rect canvasRect)
    {
        Event e = Event.current;
        Vector2 mousePos = e.mousePosition - new Vector2(0, 80);

        if (!canvasRect.Contains(e.mousePosition)) return;

        switch (e.type)
        {
            case UnityEngine.EventType.MouseDown:
                if (e.button == 0)
                {
                    int clickedNode = GetNodeAtPosition(mousePos);

                    if (e.control && clickedNode >= 0)
                    {
                        // Start creating transition
                        _isCreatingTransition = true;
                        _transitionFromIndex = clickedNode;
                        e.Use();
                    }
                    else if (clickedNode >= 0)
                    {
                        _selectedStateIndex = clickedNode;
                        _selectedTransitionIndex = -1;
                        _isDragging = true;
                        e.Use();
                    }
                    else
                    {
                        _selectedStateIndex = -1;
                        _selectedTransitionIndex = GetTransitionAtPosition(mousePos);
                        _isPanning = true;
                    }
                    _lastMousePos = e.mousePosition;
                }
                else if (e.button == 1)
                {
                    ShowContextMenu(mousePos);
                    e.Use();
                }
                break;

            case UnityEngine.EventType.MouseUp:
                if (_isCreatingTransition && _transitionFromIndex >= 0)
                {
                    int targetNode = GetNodeAtPosition(mousePos);
                    if (targetNode >= 0 && targetNode != _transitionFromIndex)
                    {
                        // Check if transition already exists
                        bool exists = CurrentLayer.transitions.Any(t =>
                            t.fromIndex == _transitionFromIndex && t.toIndex == targetNode);
                        if (!exists)
                        {
                            CurrentLayer.transitions.Add(new StateTransition
                            {
                                fromIndex = _transitionFromIndex,
                                toIndex = targetNode
                            });
                        }
                    }
                    _isCreatingTransition = false;
                    _transitionFromIndex = -1;
                }
                _isDragging = false;
                _isPanning = false;
                break;

            case UnityEngine.EventType.MouseDrag:
                if (_isDragging && _selectedStateIndex >= 0)
                {
                    CurrentLayer.states[_selectedStateIndex].position += e.delta / _zoom;
                    e.Use();
                }
                else if (_isPanning)
                {
                    _canvasOffset += e.delta;
                    e.Use();
                }
                Repaint();
                break;

            case UnityEngine.EventType.ScrollWheel:
                _zoom = Mathf.Clamp(_zoom - e.delta.y * 0.05f, 0.5f, 2f);
                e.Use();
                Repaint();
                break;

            case UnityEngine.EventType.KeyDown:
                if (e.keyCode == KeyCode.Delete)
                {
                    if (_selectedStateIndex >= 0)
                    {
                        DeleteState(_selectedStateIndex);
                        _selectedStateIndex = -1;
                        e.Use();
                    }
                    else if (_selectedTransitionIndex >= 0)
                    {
                        CurrentLayer.transitions.RemoveAt(_selectedTransitionIndex);
                        _selectedTransitionIndex = -1;
                        e.Use();
                    }
                }
                break;
        }
    }

    private void ShowContextMenu(Vector2 mousePos)
    {
        GenericMenu menu = new GenericMenu();

        menu.AddItem(new GUIContent("Add Empty State"), false, () => AddState(mousePos, "Empty State"));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Add Idle State"), false, () => AddState(mousePos, "Idle", true));
        menu.AddItem(new GUIContent("Add Walk State"), false, () => AddState(mousePos, "Walk"));
        menu.AddItem(new GUIContent("Add Run State"), false, () => AddState(mousePos, "Run"));
        menu.AddItem(new GUIContent("Add Jump State"), false, () => AddState(mousePos, "Jump"));
        menu.AddItem(new GUIContent("Add Attack State"), false, () => AddState(mousePos, "Attack"));
        menu.AddItem(new GUIContent("Add Skill State"), false, () => AddState(mousePos, "Skill"));
        menu.AddItem(new GUIContent("Add Hit State"), false, () => AddState(mousePos, "Hit"));
        menu.AddItem(new GUIContent("Add Death State"), false, () => AddState(mousePos, "Death"));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Center View"), false, CenterView);

        menu.ShowAsContext();
    }

    private void AddState(Vector2 position, string name, bool isDefault = false)
    {
        // If this is the first state or marked as default, make it default
        if (isDefault || CurrentLayer.states.Count == 0)
        {
            foreach (var state in CurrentLayer.states)
                state.isDefault = false;
        }

        CurrentLayer.states.Add(new StateNode
        {
            name = name,
            position = (position - _canvasOffset) / _zoom,
            isDefault = isDefault || CurrentLayer.states.Count == 0
        });
    }

    private void DeleteState(int index)
    {
        if (index < 0 || index >= CurrentLayer.states.Count) return;

        // Remove transitions connected to this state
        CurrentLayer.transitions.RemoveAll(t => t.fromIndex == index || t.toIndex == index);

        // Update transition indices
        foreach (var t in CurrentLayer.transitions)
        {
            if (t.fromIndex > index) t.fromIndex--;
            if (t.toIndex > index) t.toIndex--;
        }

        CurrentLayer.states.RemoveAt(index);
    }

    private void CenterView()
    {
        if (CurrentLayer.states.Count == 0)
        {
            _canvasOffset = Vector2.zero;
            return;
        }

        Vector2 center = Vector2.zero;
        foreach (var state in CurrentLayer.states)
            center += state.position;
        center /= CurrentLayer.states.Count;

        _canvasOffset = new Vector2(position.width / 2, position.height / 2) - center * _zoom;
    }

    private int GetNodeAtPosition(Vector2 pos)
    {
        for (int i = CurrentLayer.states.Count - 1; i >= 0; i--)
        {
            Rect nodeRect = GetNodeRect(CurrentLayer.states[i]);
            if (nodeRect.Contains(pos))
                return i;
        }
        return -1;
    }

    private int GetTransitionAtPosition(Vector2 pos)
    {
        for (int i = 0; i < CurrentLayer.transitions.Count; i++)
        {
            var t = CurrentLayer.transitions[i];
            if (t.fromIndex >= CurrentLayer.states.Count || t.toIndex >= CurrentLayer.states.Count)
                continue;

            Vector2 from = GetNodeCenter(CurrentLayer.states[t.fromIndex]);
            Vector2 to = GetNodeCenter(CurrentLayer.states[t.toIndex]);

            float dist = HandleUtility.DistancePointLine(pos, from, to);
            if (dist < 10f)
                return i;
        }
        return -1;
    }

    private Rect GetNodeRect(StateNode node)
    {
        Vector2 pos = node.position * _zoom + _canvasOffset;
        return new Rect(pos.x - NODE_WIDTH / 2 * _zoom, pos.y - NODE_HEIGHT / 2 * _zoom,
                       NODE_WIDTH * _zoom, NODE_HEIGHT * _zoom);
    }

    private Vector2 GetNodeCenter(StateNode node)
    {
        return node.position * _zoom + _canvasOffset;
    }

    private void DrawNodes()
    {
        for (int i = 0; i < CurrentLayer.states.Count; i++)
        {
            var state = CurrentLayer.states[i];
            Rect rect = GetNodeRect(state);

            GUIStyle style = _nodeStyle;
            if (i == _selectedStateIndex)
                style = _selectedNodeStyle;
            else if (state.isDefault)
                style = _defaultNodeStyle;

            // Draw node background
            GUI.Box(rect, "", style);

            // Draw state name
            GUI.Label(new Rect(rect.x, rect.y + 5, rect.width, 20),
                     state.name, new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } });

            // Draw clip indicator
            string clipText = state.clip != null ? state.clip.name : "(No Clip)";
            GUI.Label(new Rect(rect.x, rect.y + 25, rect.width, 20),
                     clipText, new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.gray } });

            // Draw default indicator
            if (state.isDefault)
            {
                GUI.Label(new Rect(rect.x, rect.y + rect.height - 15, rect.width, 15),
                         "DEFAULT", new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.yellow } });
            }
        }
    }

    private void DrawTransitions()
    {
        for (int i = 0; i < CurrentLayer.transitions.Count; i++)
        {
            var t = CurrentLayer.transitions[i];
            if (t.fromIndex >= CurrentLayer.states.Count || t.toIndex >= CurrentLayer.states.Count)
                continue;

            Vector2 from = GetNodeCenter(CurrentLayer.states[t.fromIndex]);
            Vector2 to = GetNodeCenter(CurrentLayer.states[t.toIndex]);

            Color color = i == _selectedTransitionIndex ? Color.yellow : Color.white;
            DrawArrow(from, to, color, i == _selectedTransitionIndex ? 3f : 2f);

            // Draw condition label
            if (!string.IsNullOrEmpty(t.conditionParam))
            {
                Vector2 midPoint = (from + to) / 2;
                GUI.Label(new Rect(midPoint.x - 50, midPoint.y - 10, 100, 20),
                         t.conditionParam, EditorStyles.miniLabel);
            }
        }
    }

    private void DrawArrow(Vector2 from, Vector2 to, Color color, float width)
    {
        Handles.BeginGUI();
        Handles.color = color;

        // Offset to avoid overlapping with node
        Vector2 dir = (to - from).normalized;
        from += dir * NODE_WIDTH / 2 * _zoom;
        to -= dir * NODE_WIDTH / 2 * _zoom;

        Handles.DrawAAPolyLine(width, from, to);

        // Draw arrowhead
        Vector2 arrowDir = (from - to).normalized;
        Vector2 arrowRight = new Vector2(-arrowDir.y, arrowDir.x);
        float arrowSize = 10f * _zoom;

        Vector2 arrowTip = to;
        Vector2 arrowLeft = arrowTip + arrowDir * arrowSize + arrowRight * arrowSize * 0.5f;
        Vector2 arrowRightPt = arrowTip + arrowDir * arrowSize - arrowRight * arrowSize * 0.5f;

        Handles.DrawAAConvexPolygon(arrowTip, arrowLeft, arrowRightPt);

        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private void DrawGraphToolbar(Rect canvasRect)
    {
        Rect toolbarRect = new Rect(canvasRect.x + 10, canvasRect.y + 10, 200, 100);

        GUILayout.BeginArea(toolbarRect);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField("Zoom: " + (_zoom * 100).ToString("F0") + "%");
        _zoom = EditorGUILayout.Slider(_zoom, 0.5f, 2f);

        if (GUILayout.Button("Center View", EditorStyles.miniButton))
            CenterView();

        if (GUILayout.Button("Auto Layout", EditorStyles.miniButton))
            AutoLayoutStates();

        EditorGUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void AutoLayoutStates()
    {
        if (CurrentLayer.states.Count == 0) return;

        // Simple grid layout
        int cols = Mathf.CeilToInt(Mathf.Sqrt(CurrentLayer.states.Count));
        float spacing = 200f;

        for (int i = 0; i < CurrentLayer.states.Count; i++)
        {
            int row = i / cols;
            int col = i % cols;
            CurrentLayer.states[i].position = new Vector2(col * spacing, row * spacing);
        }

        CenterView();
    }

    #endregion

    #region States Tab

    private void DrawStatesTab()
    {
        _statesScroll = EditorGUILayout.BeginScrollView(_statesScroll);

        EditorGUILayout.LabelField("Layer: " + CurrentLayer.name, EditorStyles.boldLabel);
        CurrentLayer.weight = EditorGUILayout.Slider("Weight", CurrentLayer.weight, 0f, 1f);
        CurrentLayer.blendingMode = (AnimatorLayerBlendingMode)EditorGUILayout.EnumPopup("Blending", CurrentLayer.blendingMode);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("States", EditorStyles.boldLabel);

        for (int i = 0; i < CurrentLayer.states.Count; i++)
        {
            DrawStateInspector(i);
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("+ Add State", GUILayout.Height(30)))
        {
            AddState(new Vector2(200, 200), "New State " + CurrentLayer.states.Count);
        }

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("Transitions", EditorStyles.boldLabel);

        for (int i = 0; i < CurrentLayer.transitions.Count; i++)
        {
            DrawTransitionInspector(i);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawStateInspector(int index)
    {
        var state = CurrentLayer.states[index];
        bool isSelected = index == _selectedStateIndex;

        EditorGUILayout.BeginVertical(isSelected ? "SelectionRect" : EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(isSelected ? "▼" : "►", GUILayout.Width(25)))
        {
            _selectedStateIndex = isSelected ? -1 : index;
        }

        state.name = EditorGUILayout.TextField(state.name);

        if (!state.isDefault && GUILayout.Button("Set Default", GUILayout.Width(80)))
        {
            foreach (var s in CurrentLayer.states) s.isDefault = false;
            state.isDefault = true;
        }

        GUI.color = Color.red;
        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            DeleteState(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();

        if (isSelected)
        {
            EditorGUI.indentLevel++;
            state.clip = (AnimationClip)EditorGUILayout.ObjectField("Animation", state.clip, typeof(AnimationClip), false);
            state.speed = EditorGUILayout.FloatField("Speed", state.speed);
            state.isLoop = EditorGUILayout.Toggle("Loop", state.isLoop);
            state.tag = EditorGUILayout.TextField("Tag", state.tag);
            state.nodeColor = EditorGUILayout.ColorField("Node Color", state.nodeColor);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawTransitionInspector(int index)
    {
        var trans = CurrentLayer.transitions[index];
        bool isSelected = index == _selectedTransitionIndex;

        string fromName = trans.fromIndex < CurrentLayer.states.Count ? CurrentLayer.states[trans.fromIndex].name : "?";
        string toName = trans.toIndex < CurrentLayer.states.Count ? CurrentLayer.states[trans.toIndex].name : "?";

        EditorGUILayout.BeginVertical(isSelected ? "SelectionRect" : EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(isSelected ? "▼" : "►", GUILayout.Width(25)))
        {
            _selectedTransitionIndex = isSelected ? -1 : index;
            _selectedStateIndex = -1;
        }

        EditorGUILayout.LabelField($"{fromName} → {toName}");

        GUI.color = Color.red;
        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            CurrentLayer.transitions.RemoveAt(index);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();

        if (isSelected)
        {
            EditorGUI.indentLevel++;

            // Parameter selection
            string[] paramNames = _parameters.Select(p => p.name).Prepend("(None)").ToArray();
            int paramIndex = string.IsNullOrEmpty(trans.conditionParam) ? 0 :
                             System.Array.IndexOf(paramNames, trans.conditionParam);
            paramIndex = EditorGUILayout.Popup("Condition Param", Mathf.Max(0, paramIndex), paramNames);
            trans.conditionParam = paramIndex > 0 ? paramNames[paramIndex] : "";

            trans.conditionType = (ConditionType)EditorGUILayout.EnumPopup("Condition Type", trans.conditionType);

            if (trans.conditionType == ConditionType.FloatGreater || trans.conditionType == ConditionType.FloatLess ||
                trans.conditionType == ConditionType.IntEquals || trans.conditionType == ConditionType.IntGreater ||
                trans.conditionType == ConditionType.IntLess)
            {
                trans.threshold = EditorGUILayout.FloatField("Threshold", trans.threshold);
            }

            trans.hasExitTime = EditorGUILayout.Toggle("Has Exit Time", trans.hasExitTime);
            if (trans.hasExitTime)
            {
                trans.exitTime = EditorGUILayout.Slider("Exit Time", trans.exitTime, 0f, 1f);
            }
            trans.transitionDuration = EditorGUILayout.FloatField("Duration", trans.transitionDuration);
            trans.canInterrupt = EditorGUILayout.Toggle("Can Interrupt", trans.canInterrupt);

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    #endregion

    #region Parameters Tab

    private void DrawParametersTab()
    {
        _paramsScroll = EditorGUILayout.BeginScrollView(_paramsScroll);

        EditorGUILayout.LabelField("Animator Parameters", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("These parameters will be created in the Animator Controller for controlling transitions.", MessageType.Info);

        EditorGUILayout.Space(10);

        for (int i = 0; i < _parameters.Count; i++)
        {
            DrawParameterInspector(i);
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Trigger", GUILayout.Height(30)))
            _parameters.Add(new AnimatorParameter { type = AnimatorControllerParameterType.Trigger, name = "NewTrigger" });
        if (GUILayout.Button("+ Bool", GUILayout.Height(30)))
            _parameters.Add(new AnimatorParameter { type = AnimatorControllerParameterType.Bool, name = "NewBool" });
        if (GUILayout.Button("+ Float", GUILayout.Height(30)))
            _parameters.Add(new AnimatorParameter { type = AnimatorControllerParameterType.Float, name = "NewFloat" });
        if (GUILayout.Button("+ Int", GUILayout.Height(30)))
            _parameters.Add(new AnimatorParameter { type = AnimatorControllerParameterType.Int, name = "NewInt" });
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(20);

        // Common presets
        EditorGUILayout.LabelField("Quick Add Presets", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Movement Params"))
        {
            AddParameterIfNotExists("Speed", AnimatorControllerParameterType.Float);
            AddParameterIfNotExists("IsGrounded", AnimatorControllerParameterType.Bool);
            AddParameterIfNotExists("IsMoving", AnimatorControllerParameterType.Bool);
            AddParameterIfNotExists("Jump", AnimatorControllerParameterType.Trigger);
        }
        if (GUILayout.Button("Combat Params"))
        {
            AddParameterIfNotExists("Attack", AnimatorControllerParameterType.Trigger);
            AddParameterIfNotExists("Skill", AnimatorControllerParameterType.Trigger);
            AddParameterIfNotExists("Burst", AnimatorControllerParameterType.Trigger);
            AddParameterIfNotExists("Hit", AnimatorControllerParameterType.Trigger);
            AddParameterIfNotExists("Die", AnimatorControllerParameterType.Trigger);
            AddParameterIfNotExists("AttackIndex", AnimatorControllerParameterType.Int);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
    }

    private void DrawParameterInspector(int index)
    {
        var param = _parameters[index];

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        param.name = EditorGUILayout.TextField(param.name, GUILayout.Width(150));
        param.type = (AnimatorControllerParameterType)EditorGUILayout.EnumPopup(param.type, GUILayout.Width(80));

        switch (param.type)
        {
            case AnimatorControllerParameterType.Float:
                param.defaultFloat = EditorGUILayout.FloatField("Default:", param.defaultFloat, GUILayout.Width(120));
                break;
            case AnimatorControllerParameterType.Int:
                param.defaultInt = EditorGUILayout.IntField("Default:", param.defaultInt, GUILayout.Width(120));
                break;
            case AnimatorControllerParameterType.Bool:
                param.defaultBool = EditorGUILayout.Toggle("Default:", param.defaultBool, GUILayout.Width(120));
                break;
            default:
                GUILayout.Space(120);
                break;
        }

        GUI.color = Color.red;
        if (GUILayout.Button("X", GUILayout.Width(25)))
        {
            _parameters.RemoveAt(index);
        }
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();
    }

    private void AddParameterIfNotExists(string name, AnimatorControllerParameterType type)
    {
        if (!_parameters.Any(p => p.name == name))
        {
            _parameters.Add(new AnimatorParameter { name = name, type = type });
        }
    }

    #endregion

    #region Presets Tab

    private void DrawPresetsTab()
    {
        _presetsScroll = EditorGUILayout.BeginScrollView(_presetsScroll);

        EditorGUILayout.LabelField("State Machine Presets", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Apply preset templates to quickly set up common state machine patterns.", MessageType.Info);

        EditorGUILayout.Space(10);

        // Character Locomotion
        EditorGUILayout.LabelField("Character Locomotion", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Basic (Idle/Walk/Run)", GUILayout.Height(40)))
            ApplyBasicLocomotionPreset();
        if (GUILayout.Button("Advanced (+ Jump/Fall)", GUILayout.Height(40)))
            ApplyAdvancedLocomotionPreset();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Combat
        EditorGUILayout.LabelField("Combat", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Melee Combat", GUILayout.Height(40)))
            ApplyMeleeCombatPreset();
        if (GUILayout.Button("Ranged Combat", GUILayout.Height(40)))
            ApplyRangedCombatPreset();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // RPG Specific
        EditorGUILayout.LabelField("RPG Character", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Genshin-Style Character", GUILayout.Height(40)))
            ApplyGenshinCharacterPreset();
        if (GUILayout.Button("Turn-Based RPG", GUILayout.Height(40)))
            ApplyTurnBasedPreset();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Enemies
        EditorGUILayout.LabelField("Enemies", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Basic Enemy AI", GUILayout.Height(40)))
            ApplyBasicEnemyPreset();
        if (GUILayout.Button("Boss Enemy", GUILayout.Height(40)))
            ApplyBossEnemyPreset();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
    }

    private void ApplyBasicLocomotionPreset()
    {
        if (!ConfirmPresetApply()) return;

        CurrentLayer.states.Clear();
        CurrentLayer.transitions.Clear();

        AddState(new Vector2(100, 200), "Idle", true);
        AddState(new Vector2(300, 200), "Walk");
        AddState(new Vector2(500, 200), "Run");

        AddParameterIfNotExists("Speed", AnimatorControllerParameterType.Float);

        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 0, toIndex = 1, conditionParam = "Speed", conditionType = ConditionType.FloatGreater, threshold = 0.1f });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 1, toIndex = 0, conditionParam = "Speed", conditionType = ConditionType.FloatLess, threshold = 0.1f });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 1, toIndex = 2, conditionParam = "Speed", conditionType = ConditionType.FloatGreater, threshold = 0.5f });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 2, toIndex = 1, conditionParam = "Speed", conditionType = ConditionType.FloatLess, threshold = 0.5f });
    }

    private void ApplyAdvancedLocomotionPreset()
    {
        ApplyBasicLocomotionPreset();

        AddState(new Vector2(300, 50), "Jump");
        AddState(new Vector2(300, 350), "Fall");
        AddState(new Vector2(500, 350), "Land");

        AddParameterIfNotExists("Jump", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("IsGrounded", AnimatorControllerParameterType.Bool);

        // Jump transitions
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 0, toIndex = 3, conditionParam = "Jump", conditionType = ConditionType.Trigger });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 1, toIndex = 3, conditionParam = "Jump", conditionType = ConditionType.Trigger });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 2, toIndex = 3, conditionParam = "Jump", conditionType = ConditionType.Trigger });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 3, toIndex = 4, hasExitTime = true, exitTime = 0.9f });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 4, toIndex = 5, conditionParam = "IsGrounded", conditionType = ConditionType.BoolTrue });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 5, toIndex = 0, hasExitTime = true, exitTime = 1f });
    }

    private void ApplyMeleeCombatPreset()
    {
        if (!ConfirmPresetApply()) return;

        CurrentLayer.states.Clear();
        CurrentLayer.transitions.Clear();

        AddState(new Vector2(100, 200), "Combat Idle", true);
        AddState(new Vector2(300, 100), "Attack 1");
        AddState(new Vector2(500, 100), "Attack 2");
        AddState(new Vector2(700, 100), "Attack 3");
        AddState(new Vector2(300, 300), "Block");
        AddState(new Vector2(500, 300), "Hit");
        AddState(new Vector2(700, 300), "Skill");

        AddParameterIfNotExists("Attack", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("Block", AnimatorControllerParameterType.Bool);
        AddParameterIfNotExists("Hit", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("Skill", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("AttackIndex", AnimatorControllerParameterType.Int);

        // Attack combo
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 0, toIndex = 1, conditionParam = "Attack", conditionType = ConditionType.Trigger });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 1, toIndex = 2, conditionParam = "Attack", conditionType = ConditionType.Trigger, hasExitTime = true, exitTime = 0.7f });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 2, toIndex = 3, conditionParam = "Attack", conditionType = ConditionType.Trigger, hasExitTime = true, exitTime = 0.7f });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 1, toIndex = 0, hasExitTime = true, exitTime = 1f });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 2, toIndex = 0, hasExitTime = true, exitTime = 1f });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 3, toIndex = 0, hasExitTime = true, exitTime = 1f });

        // Block
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 0, toIndex = 4, conditionParam = "Block", conditionType = ConditionType.BoolTrue });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 4, toIndex = 0, conditionParam = "Block", conditionType = ConditionType.BoolFalse });

        // Hit
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 0, toIndex = 5, conditionParam = "Hit", conditionType = ConditionType.Trigger, canInterrupt = true });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 5, toIndex = 0, hasExitTime = true, exitTime = 1f });

        // Skill
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 0, toIndex = 6, conditionParam = "Skill", conditionType = ConditionType.Trigger });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 6, toIndex = 0, hasExitTime = true, exitTime = 1f });
    }

    private void ApplyRangedCombatPreset()
    {
        if (!ConfirmPresetApply()) return;

        CurrentLayer.states.Clear();
        CurrentLayer.transitions.Clear();

        AddState(new Vector2(100, 200), "Combat Idle", true);
        AddState(new Vector2(300, 100), "Aim");
        AddState(new Vector2(500, 100), "Shoot");
        AddState(new Vector2(300, 300), "Reload");
        AddState(new Vector2(500, 300), "Charged Shot");

        AddParameterIfNotExists("Aim", AnimatorControllerParameterType.Bool);
        AddParameterIfNotExists("Fire", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("Reload", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("ChargedShot", AnimatorControllerParameterType.Trigger);

        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 0, toIndex = 1, conditionParam = "Aim", conditionType = ConditionType.BoolTrue });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 1, toIndex = 0, conditionParam = "Aim", conditionType = ConditionType.BoolFalse });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 1, toIndex = 2, conditionParam = "Fire", conditionType = ConditionType.Trigger });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 2, toIndex = 1, hasExitTime = true, exitTime = 1f });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 0, toIndex = 3, conditionParam = "Reload", conditionType = ConditionType.Trigger });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 3, toIndex = 0, hasExitTime = true, exitTime = 1f });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 1, toIndex = 4, conditionParam = "ChargedShot", conditionType = ConditionType.Trigger });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 4, toIndex = 1, hasExitTime = true, exitTime = 1f });
    }

    private void ApplyGenshinCharacterPreset()
    {
        if (!ConfirmPresetApply()) return;

        // Base Layer - Locomotion
        _layers.Clear();
        _layers.Add(new AnimatorLayer { name = "Base Layer" });
        _currentLayerIndex = 0;

        CurrentLayer.states.Clear();
        AddState(new Vector2(100, 200), "Idle", true);
        AddState(new Vector2(300, 200), "Walk");
        AddState(new Vector2(500, 200), "Run");
        AddState(new Vector2(700, 200), "Sprint");
        AddState(new Vector2(300, 50), "Jump");
        AddState(new Vector2(500, 50), "Glide");
        AddState(new Vector2(700, 50), "Climb");
        AddState(new Vector2(100, 350), "Swim Idle");
        AddState(new Vector2(300, 350), "Swim");

        // Action Layer
        _layers.Add(new AnimatorLayer { name = "Action Layer", weight = 1f, blendingMode = AnimatorLayerBlendingMode.Override });
        _currentLayerIndex = 1;

        CurrentLayer.states.Clear();
        AddState(new Vector2(100, 200), "Empty", true);
        AddState(new Vector2(300, 100), "Normal Attack 1");
        AddState(new Vector2(500, 100), "Normal Attack 2");
        AddState(new Vector2(700, 100), "Normal Attack 3");
        AddState(new Vector2(900, 100), "Normal Attack 4");
        AddState(new Vector2(300, 300), "Charged Attack");
        AddState(new Vector2(500, 300), "Plunge Attack");
        AddState(new Vector2(100, 400), "Elemental Skill");
        AddState(new Vector2(300, 400), "Elemental Burst");
        AddState(new Vector2(500, 400), "Hit");
        AddState(new Vector2(700, 400), "Die");

        // Parameters
        _parameters.Clear();
        AddParameterIfNotExists("Speed", AnimatorControllerParameterType.Float);
        AddParameterIfNotExists("IsSprinting", AnimatorControllerParameterType.Bool);
        AddParameterIfNotExists("IsGrounded", AnimatorControllerParameterType.Bool);
        AddParameterIfNotExists("IsSwimming", AnimatorControllerParameterType.Bool);
        AddParameterIfNotExists("IsGliding", AnimatorControllerParameterType.Bool);
        AddParameterIfNotExists("IsClimbing", AnimatorControllerParameterType.Bool);
        AddParameterIfNotExists("Jump", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("Attack", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("ChargedAttack", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("Skill", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("Burst", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("Hit", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("Die", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("AttackIndex", AnimatorControllerParameterType.Int);

        _currentLayerIndex = 0;
    }

    private void ApplyTurnBasedPreset()
    {
        if (!ConfirmPresetApply()) return;

        CurrentLayer.states.Clear();
        CurrentLayer.transitions.Clear();

        AddState(new Vector2(100, 200), "Idle", true);
        AddState(new Vector2(300, 100), "Ready");
        AddState(new Vector2(500, 100), "Attack");
        AddState(new Vector2(700, 100), "Skill");
        AddState(new Vector2(300, 300), "Defend");
        AddState(new Vector2(500, 300), "Hit");
        AddState(new Vector2(700, 300), "Victory");
        AddState(new Vector2(500, 400), "Defeat");

        AddParameterIfNotExists("Ready", AnimatorControllerParameterType.Bool);
        AddParameterIfNotExists("Attack", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("Skill", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("Defend", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("Hit", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("Victory", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("Defeat", AnimatorControllerParameterType.Trigger);
    }

    private void ApplyBasicEnemyPreset()
    {
        if (!ConfirmPresetApply()) return;

        CurrentLayer.states.Clear();
        CurrentLayer.transitions.Clear();

        AddState(new Vector2(100, 200), "Idle", true);
        AddState(new Vector2(300, 100), "Patrol");
        AddState(new Vector2(500, 100), "Chase");
        AddState(new Vector2(700, 100), "Attack");
        AddState(new Vector2(300, 300), "Hit");
        AddState(new Vector2(500, 300), "Die");

        AddParameterIfNotExists("IsPatrolling", AnimatorControllerParameterType.Bool);
        AddParameterIfNotExists("TargetInRange", AnimatorControllerParameterType.Bool);
        AddParameterIfNotExists("CanAttack", AnimatorControllerParameterType.Bool);
        AddParameterIfNotExists("Hit", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("Die", AnimatorControllerParameterType.Trigger);

        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 0, toIndex = 1, conditionParam = "IsPatrolling", conditionType = ConditionType.BoolTrue });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 1, toIndex = 0, conditionParam = "IsPatrolling", conditionType = ConditionType.BoolFalse });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 0, toIndex = 2, conditionParam = "TargetInRange", conditionType = ConditionType.BoolTrue });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 1, toIndex = 2, conditionParam = "TargetInRange", conditionType = ConditionType.BoolTrue });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 2, toIndex = 0, conditionParam = "TargetInRange", conditionType = ConditionType.BoolFalse });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 2, toIndex = 3, conditionParam = "CanAttack", conditionType = ConditionType.BoolTrue });
        CurrentLayer.transitions.Add(new StateTransition { fromIndex = 3, toIndex = 2, hasExitTime = true, exitTime = 1f });
    }

    private void ApplyBossEnemyPreset()
    {
        if (!ConfirmPresetApply()) return;

        CurrentLayer.states.Clear();
        CurrentLayer.transitions.Clear();

        AddState(new Vector2(100, 200), "Idle", true);
        AddState(new Vector2(300, 50), "Intro");
        AddState(new Vector2(500, 100), "Phase 1 Idle");
        AddState(new Vector2(700, 100), "Melee Attack");
        AddState(new Vector2(900, 100), "Ranged Attack");
        AddState(new Vector2(500, 250), "Phase Transition");
        AddState(new Vector2(500, 350), "Phase 2 Idle");
        AddState(new Vector2(700, 350), "Ultimate");
        AddState(new Vector2(300, 350), "Stagger");
        AddState(new Vector2(100, 350), "Enrage");
        AddState(new Vector2(500, 500), "Die");

        AddParameterIfNotExists("StartFight", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("MeleeAttack", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("RangedAttack", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("PhaseTransition", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("Ultimate", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("Stagger", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("Enrage", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("Die", AnimatorControllerParameterType.Trigger);
        AddParameterIfNotExists("Phase", AnimatorControllerParameterType.Int);
    }

    private bool ConfirmPresetApply()
    {
        if (CurrentLayer.states.Count > 0)
        {
            return EditorUtility.DisplayDialog("Apply Preset",
                "This will replace current layer states. Continue?", "Yes", "No");
        }
        return true;
    }

    #endregion

    #region Export Tab

    private void DrawExportTab()
    {
        EditorGUILayout.LabelField("Export Animator Controller", EditorStyles.boldLabel);

        EditorGUILayout.Space(10);

        _controllerName = EditorGUILayout.TextField("Controller Name", _controllerName);

        EditorGUILayout.BeginHorizontal();
        _outputPath = EditorGUILayout.TextField("Output Path", _outputPath);
        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            string path = EditorUtility.SaveFolderPanel("Select Output Folder", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath))
                    _outputPath = "Assets" + path.Substring(Application.dataPath.Length);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(20);

        // Summary
        EditorGUILayout.LabelField("Export Summary", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"Layers: {_layers.Count}");
        int totalStates = _layers.Sum(l => l.states.Count);
        int totalTransitions = _layers.Sum(l => l.transitions.Count);
        EditorGUILayout.LabelField($"Total States: {totalStates}");
        EditorGUILayout.LabelField($"Total Transitions: {totalTransitions}");
        EditorGUILayout.LabelField($"Parameters: {_parameters.Count}");

        // Warnings
        int statesWithoutClips = _layers.Sum(l => l.states.Count(s => s.clip == null));
        if (statesWithoutClips > 0)
        {
            EditorGUILayout.HelpBox($"{statesWithoutClips} states have no animation clip assigned.", MessageType.Warning);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(20);

        GUI.enabled = totalStates > 0;
        if (GUILayout.Button("Export Animator Controller", GUILayout.Height(40)))
        {
            ExportAnimatorController();
        }
        GUI.enabled = true;

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save as JSON", GUILayout.Height(30)))
            SaveToJson();
        if (GUILayout.Button("Load from JSON", GUILayout.Height(30)))
            LoadFromJson();
        EditorGUILayout.EndHorizontal();
    }

    private void ExportAnimatorController()
    {
        // Create output folder
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

        string fullPath = $"{_outputPath}/{_controllerName}.controller";

        // Create controller
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(fullPath);

        // Add parameters
        foreach (var param in _parameters)
        {
            controller.AddParameter(param.name, param.type);

            // Set defaults
            var controllerParam = controller.parameters.First(p => p.name == param.name);
            switch (param.type)
            {
                case AnimatorControllerParameterType.Float:
                    controllerParam.defaultFloat = param.defaultFloat;
                    break;
                case AnimatorControllerParameterType.Int:
                    controllerParam.defaultInt = param.defaultInt;
                    break;
                case AnimatorControllerParameterType.Bool:
                    controllerParam.defaultBool = param.defaultBool;
                    break;
            }
        }

        // Process layers
        for (int layerIndex = 0; layerIndex < _layers.Count; layerIndex++)
        {
            var layer = _layers[layerIndex];
            AnimatorControllerLayer controllerLayer;

            if (layerIndex == 0)
            {
                controllerLayer = controller.layers[0];
                controllerLayer.name = layer.name;
            }
            else
            {
                controller.AddLayer(layer.name);
                controllerLayer = controller.layers[layerIndex];
            }

            controllerLayer.defaultWeight = layer.weight;
            controllerLayer.blendingMode = layer.blendingMode;

            var stateMachine = controllerLayer.stateMachine;

            // Add states
            Dictionary<int, AnimatorState> stateMap = new Dictionary<int, AnimatorState>();

            for (int i = 0; i < layer.states.Count; i++)
            {
                var stateNode = layer.states[i];
                var state = stateMachine.AddState(stateNode.name, stateNode.position);

                state.motion = stateNode.clip;
                state.speed = stateNode.speed;
                state.tag = stateNode.tag;

                if (stateNode.isDefault)
                    stateMachine.defaultState = state;

                stateMap[i] = state;
            }

            // Add transitions
            foreach (var trans in layer.transitions)
            {
                if (!stateMap.ContainsKey(trans.fromIndex) || !stateMap.ContainsKey(trans.toIndex))
                    continue;

                var fromState = stateMap[trans.fromIndex];
                var toState = stateMap[trans.toIndex];

                var transition = fromState.AddTransition(toState);
                transition.hasExitTime = trans.hasExitTime;
                transition.exitTime = trans.exitTime;
                transition.duration = trans.transitionDuration;
                transition.canTransitionToSelf = false;
                transition.interruptionSource = trans.canInterrupt ?
                    TransitionInterruptionSource.Source : TransitionInterruptionSource.None;

                // Add condition
                if (!string.IsNullOrEmpty(trans.conditionParam))
                {
                    AnimatorConditionMode mode = AnimatorConditionMode.If;
                    float threshold = trans.threshold;

                    switch (trans.conditionType)
                    {
                        case ConditionType.Trigger:
                            mode = AnimatorConditionMode.If;
                            break;
                        case ConditionType.BoolTrue:
                            mode = AnimatorConditionMode.If;
                            break;
                        case ConditionType.BoolFalse:
                            mode = AnimatorConditionMode.IfNot;
                            break;
                        case ConditionType.FloatGreater:
                            mode = AnimatorConditionMode.Greater;
                            break;
                        case ConditionType.FloatLess:
                            mode = AnimatorConditionMode.Less;
                            break;
                        case ConditionType.IntEquals:
                            mode = AnimatorConditionMode.Equals;
                            break;
                        case ConditionType.IntGreater:
                            mode = AnimatorConditionMode.Greater;
                            break;
                        case ConditionType.IntLess:
                            mode = AnimatorConditionMode.Less;
                            break;
                    }

                    transition.AddCondition(mode, threshold, trans.conditionParam);
                }
            }

            // Update layer in controller
            var layers = controller.layers;
            layers[layerIndex] = controllerLayer;
            controller.layers = layers;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Export Complete",
            $"Animator Controller exported to:\n{fullPath}", "OK");

        Selection.activeObject = controller;
        EditorGUIUtility.PingObject(controller);
    }

    private void SaveToJson()
    {
        string path = EditorUtility.SaveFilePanel("Save State Machine", "", _controllerName, "json");
        if (string.IsNullOrEmpty(path)) return;

        var saveData = new StateMachineSaveData
        {
            controllerName = _controllerName,
            layers = _layers,
            parameters = _parameters
        };

        string json = JsonUtility.ToJson(saveData, true);
        System.IO.File.WriteAllText(path, json);

        Debug.Log($"State machine saved to: {path}");
    }

    private void LoadFromJson()
    {
        string path = EditorUtility.OpenFilePanel("Load State Machine", "", "json");
        if (string.IsNullOrEmpty(path)) return;

        string json = System.IO.File.ReadAllText(path);
        var saveData = JsonUtility.FromJson<StateMachineSaveData>(json);

        _controllerName = saveData.controllerName;
        _layers = saveData.layers;
        _parameters = saveData.parameters;
        _currentLayerIndex = 0;
        _selectedStateIndex = -1;
        _selectedTransitionIndex = -1;

        Debug.Log($"State machine loaded from: {path}");
    }

    [System.Serializable]
    private class StateMachineSaveData
    {
        public string controllerName;
        public List<AnimatorLayer> layers;
        public List<AnimatorParameter> parameters;
    }

    #endregion
}
