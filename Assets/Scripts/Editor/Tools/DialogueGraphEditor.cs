using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Visual Dialogue Graph Editor for creating branching conversations.
/// Node-based editor with condition support and localization integration.
/// </summary>
public class DialogueGraphEditor : EditorWindow
{
    [MenuItem("EpicLegends/Tools/Dialogue Graph Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<DialogueGraphEditor>("Dialogue Graph");
        window.minSize = new Vector2(900, 600);
    }

    // Data structures
    [System.Serializable]
    public class DialogueNode
    {
        public string id = "";
        public string speakerName = "";
        public string speakerPortrait = "";
        public string dialogueText = "";
        public string localizationKey = "";
        public NodeType type = NodeType.Dialogue;
        public Vector2 position;
        public List<string> outgoingConnections = new List<string>();
        public List<DialogueChoice> choices = new List<DialogueChoice>();
        public List<DialogueCondition> conditions = new List<DialogueCondition>();
        public List<DialogueAction> actions = new List<DialogueAction>();
        public float duration = 0f;
        public string audioClipPath = "";
        public AnimationType animation = AnimationType.None;
    }

    [System.Serializable]
    public class DialogueChoice
    {
        public string text = "Choice";
        public string localizationKey = "";
        public string targetNodeId = "";
        public List<DialogueCondition> conditions = new List<DialogueCondition>();
        public List<DialogueAction> actions = new List<DialogueAction>();
    }

    [System.Serializable]
    public class DialogueCondition
    {
        public ConditionType type = ConditionType.HasItem;
        public string parameter = "";
        public CompareOperator compareOp = CompareOperator.Equals;
        public string value = "";
    }

    [System.Serializable]
    public class DialogueAction
    {
        public ActionType type = ActionType.GiveItem;
        public string parameter = "";
        public string value = "";
    }

    [System.Serializable]
    public class DialogueGraph
    {
        public string graphName = "New Dialogue";
        public string startNodeId = "";
        public List<DialogueNode> nodes = new List<DialogueNode>();
        public List<string> speakers = new List<string>();
        public Dictionary<string, string> variables = new Dictionary<string, string>();
    }

    public enum NodeType { Start, Dialogue, Choice, Condition, Action, Random, End }
    public enum ConditionType { HasItem, HasQuest, QuestComplete, FlagSet, StatCheck, RelationshipLevel }
    public enum CompareOperator { Equals, NotEquals, Greater, Less, GreaterEqual, LessEqual }
    public enum ActionType { GiveItem, RemoveItem, StartQuest, CompleteQuest, SetFlag, AddRelationship, PlaySound, PlayAnimation }
    public enum AnimationType { None, Happy, Sad, Angry, Surprised, Thinking, Nodding }

    // State
    private DialogueGraph _currentGraph = new DialogueGraph();
    private DialogueNode _selectedNode;
    private int _selectedChoiceIndex = -1;
    private bool _isConnecting;
    private string _connectFromNodeId;
    private int _connectFromChoiceIndex = -1;

    // View
    private Vector2 _scrollOffset;
    private float _zoom = 1f;
    private Vector2 _lastMousePos;
    private bool _isPanning;

    // UI
    private Vector2 _inspectorScroll;
    private int _currentTab;
    private readonly string[] _tabNames = { "Graph", "Speakers", "Variables", "Settings" };

    // Node sizes
    private const float NODE_WIDTH = 200f;
    private const float NODE_MIN_HEIGHT = 80f;
    private const float CHOICE_HEIGHT = 25f;

    // Colors
    private readonly Dictionary<NodeType, Color> _nodeColors = new Dictionary<NodeType, Color>
    {
        { NodeType.Start, new Color(0.2f, 0.7f, 0.2f) },
        { NodeType.Dialogue, new Color(0.3f, 0.4f, 0.7f) },
        { NodeType.Choice, new Color(0.7f, 0.5f, 0.2f) },
        { NodeType.Condition, new Color(0.6f, 0.3f, 0.6f) },
        { NodeType.Action, new Color(0.7f, 0.3f, 0.3f) },
        { NodeType.Random, new Color(0.5f, 0.5f, 0.5f) },
        { NodeType.End, new Color(0.5f, 0.2f, 0.2f) }
    };

    private void OnEnable()
    {
        if (_currentGraph.nodes.Count == 0)
        {
            CreateNewGraph();
        }
    }

    private void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.BeginHorizontal();

        // Main graph area
        DrawGraphArea();

        // Inspector panel
        DrawInspector();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(40)))
        {
            if (EditorUtility.DisplayDialog("New Graph", "Create new dialogue graph?", "Yes", "No"))
                CreateNewGraph();
        }

        if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(40)))
            SaveGraph();

        if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(40)))
            LoadGraph();

        GUILayout.Space(10);

        _currentGraph.graphName = EditorGUILayout.TextField(_currentGraph.graphName, GUILayout.Width(200));

        GUILayout.Space(10);

        // Add node buttons
        if (GUILayout.Button("+ Dialogue", EditorStyles.toolbarButton, GUILayout.Width(70)))
            AddNode(NodeType.Dialogue);
        if (GUILayout.Button("+ Choice", EditorStyles.toolbarButton, GUILayout.Width(60)))
            AddNode(NodeType.Choice);
        if (GUILayout.Button("+ Condition", EditorStyles.toolbarButton, GUILayout.Width(75)))
            AddNode(NodeType.Condition);
        if (GUILayout.Button("+ Action", EditorStyles.toolbarButton, GUILayout.Width(60)))
            AddNode(NodeType.Action);
        if (GUILayout.Button("+ End", EditorStyles.toolbarButton, GUILayout.Width(45)))
            AddNode(NodeType.End);

        GUILayout.FlexibleSpace();

        EditorGUILayout.LabelField($"Nodes: {_currentGraph.nodes.Count}", GUILayout.Width(80));
        EditorGUILayout.LabelField($"Zoom: {_zoom:P0}", GUILayout.Width(80));

        EditorGUILayout.EndHorizontal();
    }

    private void DrawGraphArea()
    {
        Rect graphRect = new Rect(0, 40, position.width - 300, position.height - 40);

        // Background
        EditorGUI.DrawRect(graphRect, new Color(0.15f, 0.15f, 0.15f));

        // Handle input
        HandleGraphInput(graphRect);

        // Begin clipping
        GUI.BeginGroup(graphRect);

        // Draw grid
        DrawGrid(graphRect, 20f * _zoom, 0.2f);
        DrawGrid(graphRect, 100f * _zoom, 0.4f);

        // Draw connections
        DrawConnections();

        // Draw nodes
        DrawNodes();

        // Draw connecting line
        if (_isConnecting && !string.IsNullOrEmpty(_connectFromNodeId))
        {
            var fromNode = _currentGraph.nodes.Find(n => n.id == _connectFromNodeId);
            if (fromNode != null)
            {
                Vector2 startPos = GetNodeOutputPosition(fromNode, _connectFromChoiceIndex);
                Vector2 endPos = Event.current.mousePosition;
                DrawBezierConnection(startPos, endPos, Color.yellow);
                Repaint();
            }
        }

        GUI.EndGroup();

        // Instructions
        Rect instructRect = new Rect(10, graphRect.height - 30, 400, 25);
        GUI.Label(instructRect, "Right-click: Context menu | Drag: Move nodes | Middle-drag: Pan | Scroll: Zoom",
                 EditorStyles.miniLabel);
    }

    private void DrawGrid(Rect rect, float spacing, float opacity)
    {
        int widthDivs = Mathf.CeilToInt(rect.width / spacing);
        int heightDivs = Mathf.CeilToInt(rect.height / spacing);

        Handles.BeginGUI();
        Handles.color = new Color(0.5f, 0.5f, 0.5f, opacity);

        Vector3 offset = new Vector3(_scrollOffset.x % spacing, _scrollOffset.y % spacing, 0);

        for (int i = 0; i <= widthDivs; i++)
        {
            Handles.DrawLine(
                new Vector3(spacing * i + offset.x, 0, 0),
                new Vector3(spacing * i + offset.x, rect.height, 0)
            );
        }

        for (int j = 0; j <= heightDivs; j++)
        {
            Handles.DrawLine(
                new Vector3(0, spacing * j + offset.y, 0),
                new Vector3(rect.width, spacing * j + offset.y, 0)
            );
        }

        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private void HandleGraphInput(Rect graphRect)
    {
        Event e = Event.current;
        Vector2 mousePos = e.mousePosition;

        if (!graphRect.Contains(mousePos + new Vector2(0, 40))) return;

        switch (e.type)
        {
            case UnityEngine.EventType.MouseDown:
                if (e.button == 0)
                {
                    // Left click - select node
                    var clickedNode = GetNodeAtPosition(mousePos);
                    if (clickedNode != null)
                    {
                        if (_isConnecting)
                        {
                            // Complete connection
                            CompleteConnection(clickedNode);
                        }
                        else
                        {
                            _selectedNode = clickedNode;
                            _selectedChoiceIndex = -1;
                        }
                        e.Use();
                    }
                    else
                    {
                        _selectedNode = null;
                        _isConnecting = false;
                    }
                    _lastMousePos = mousePos;
                }
                else if (e.button == 1)
                {
                    // Right click - context menu
                    ShowContextMenu(mousePos);
                    e.Use();
                }
                else if (e.button == 2)
                {
                    // Middle click - start panning
                    _isPanning = true;
                    _lastMousePos = mousePos;
                    e.Use();
                }
                break;

            case UnityEngine.EventType.MouseUp:
                _isPanning = false;
                break;

            case UnityEngine.EventType.MouseDrag:
                if (_isPanning)
                {
                    _scrollOffset += e.delta;
                    e.Use();
                    Repaint();
                }
                else if (e.button == 0 && _selectedNode != null && !_isConnecting)
                {
                    _selectedNode.position += e.delta / _zoom;
                    e.Use();
                    Repaint();
                }
                break;

            case UnityEngine.EventType.ScrollWheel:
                _zoom = Mathf.Clamp(_zoom - e.delta.y * 0.05f, 0.5f, 2f);
                e.Use();
                Repaint();
                break;

            case UnityEngine.EventType.KeyDown:
                if (e.keyCode == KeyCode.Delete && _selectedNode != null)
                {
                    DeleteNode(_selectedNode);
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    _isConnecting = false;
                    e.Use();
                }
                break;
        }
    }

    private void ShowContextMenu(Vector2 mousePos)
    {
        GenericMenu menu = new GenericMenu();

        var nodeAtPos = GetNodeAtPosition(mousePos);

        if (nodeAtPos != null)
        {
            menu.AddItem(new GUIContent("Delete Node"), false, () => DeleteNode(nodeAtPos));
            menu.AddItem(new GUIContent("Duplicate Node"), false, () => DuplicateNode(nodeAtPos));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Set as Start"), false, () => SetAsStart(nodeAtPos));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Connect From Here"), false, () => StartConnection(nodeAtPos.id, -1));
        }
        else
        {
            menu.AddItem(new GUIContent("Add Dialogue Node"), false, () => AddNodeAtPosition(NodeType.Dialogue, mousePos));
            menu.AddItem(new GUIContent("Add Choice Node"), false, () => AddNodeAtPosition(NodeType.Choice, mousePos));
            menu.AddItem(new GUIContent("Add Condition Node"), false, () => AddNodeAtPosition(NodeType.Condition, mousePos));
            menu.AddItem(new GUIContent("Add Action Node"), false, () => AddNodeAtPosition(NodeType.Action, mousePos));
            menu.AddItem(new GUIContent("Add End Node"), false, () => AddNodeAtPosition(NodeType.End, mousePos));
        }

        menu.ShowAsContext();
    }

    private void DrawNodes()
    {
        foreach (var node in _currentGraph.nodes)
        {
            DrawNode(node);
        }
    }

    private void DrawNode(DialogueNode node)
    {
        Vector2 pos = node.position * _zoom + _scrollOffset;
        float height = CalculateNodeHeight(node);

        Rect nodeRect = new Rect(pos.x, pos.y, NODE_WIDTH * _zoom, height * _zoom);

        // Node background
        Color bgColor = _nodeColors.ContainsKey(node.type) ? _nodeColors[node.type] : Color.gray;
        if (_selectedNode == node)
            bgColor = Color.Lerp(bgColor, Color.white, 0.3f);

        EditorGUI.DrawRect(nodeRect, bgColor);

        // Border
        Color borderColor = node.id == _currentGraph.startNodeId ? Color.green : Color.black;
        DrawRectBorder(nodeRect, borderColor, 2f);

        // Header
        Rect headerRect = new Rect(nodeRect.x, nodeRect.y, nodeRect.width, 20 * _zoom);
        EditorGUI.DrawRect(headerRect, new Color(0, 0, 0, 0.3f));

        string headerText = node.type.ToString();
        if (!string.IsNullOrEmpty(node.speakerName))
            headerText = node.speakerName;

        GUI.Label(new Rect(headerRect.x + 5, headerRect.y + 2, headerRect.width - 10, 16),
                 headerText, new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white }, fontSize = (int)(11 * _zoom) });

        // Content
        float contentY = nodeRect.y + 22 * _zoom;

        switch (node.type)
        {
            case NodeType.Dialogue:
                string preview = node.dialogueText.Length > 50 ? node.dialogueText.Substring(0, 47) + "..." : node.dialogueText;
                GUI.Label(new Rect(nodeRect.x + 5, contentY, nodeRect.width - 10, 40 * _zoom),
                         preview, new GUIStyle(EditorStyles.wordWrappedLabel) { normal = { textColor = Color.white }, fontSize = (int)(10 * _zoom) });
                break;

            case NodeType.Choice:
                for (int i = 0; i < node.choices.Count; i++)
                {
                    Rect choiceRect = new Rect(nodeRect.x + 5, contentY + i * CHOICE_HEIGHT * _zoom, nodeRect.width - 30, CHOICE_HEIGHT * _zoom - 2);
                    EditorGUI.DrawRect(choiceRect, new Color(0, 0, 0, 0.2f));
                    GUI.Label(choiceRect, node.choices[i].text,
                             new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } });

                    // Connection button
                    Rect connBtn = new Rect(nodeRect.x + nodeRect.width - 20, contentY + i * CHOICE_HEIGHT * _zoom, 15, 15);
                    if (GUI.Button(connBtn, ">"))
                    {
                        StartConnection(node.id, i);
                    }
                }
                break;

            case NodeType.Condition:
                if (node.conditions.Count > 0)
                {
                    var cond = node.conditions[0];
                    GUI.Label(new Rect(nodeRect.x + 5, contentY, nodeRect.width - 10, 20 * _zoom),
                             $"If {cond.type}", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } });
                }
                break;

            case NodeType.Action:
                if (node.actions.Count > 0)
                {
                    var action = node.actions[0];
                    GUI.Label(new Rect(nodeRect.x + 5, contentY, nodeRect.width - 10, 20 * _zoom),
                             $"{action.type}", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } });
                }
                break;
        }

        // Output connector (for non-choice nodes)
        if (node.type != NodeType.Choice && node.type != NodeType.End)
        {
            Rect outRect = new Rect(nodeRect.x + nodeRect.width - 15, nodeRect.y + nodeRect.height / 2 - 7, 15, 15);
            if (GUI.Button(outRect, ">"))
            {
                StartConnection(node.id, -1);
            }
        }

        // Input connector
        Rect inRect = new Rect(nodeRect.x, nodeRect.y + nodeRect.height / 2 - 7, 15, 15);
        EditorGUI.DrawRect(inRect, Color.white);
    }

    private float CalculateNodeHeight(DialogueNode node)
    {
        float height = NODE_MIN_HEIGHT;

        if (node.type == NodeType.Choice)
        {
            height = 25 + node.choices.Count * CHOICE_HEIGHT + 10;
        }

        return Mathf.Max(height, NODE_MIN_HEIGHT);
    }

    private void DrawRectBorder(Rect rect, Color color, float width)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - width, rect.width, width), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.x + rect.width - width, rect.y, width, rect.height), color);
    }

    private void DrawConnections()
    {
        foreach (var node in _currentGraph.nodes)
        {
            if (node.type == NodeType.Choice)
            {
                for (int i = 0; i < node.choices.Count; i++)
                {
                    if (!string.IsNullOrEmpty(node.choices[i].targetNodeId))
                    {
                        var targetNode = _currentGraph.nodes.Find(n => n.id == node.choices[i].targetNodeId);
                        if (targetNode != null)
                        {
                            Vector2 startPos = GetNodeOutputPosition(node, i);
                            Vector2 endPos = GetNodeInputPosition(targetNode);
                            DrawBezierConnection(startPos, endPos, Color.white);
                        }
                    }
                }
            }
            else
            {
                foreach (var targetId in node.outgoingConnections)
                {
                    var targetNode = _currentGraph.nodes.Find(n => n.id == targetId);
                    if (targetNode != null)
                    {
                        Vector2 startPos = GetNodeOutputPosition(node, -1);
                        Vector2 endPos = GetNodeInputPosition(targetNode);
                        DrawBezierConnection(startPos, endPos, Color.white);
                    }
                }
            }
        }
    }

    private Vector2 GetNodeOutputPosition(DialogueNode node, int choiceIndex)
    {
        Vector2 pos = node.position * _zoom + _scrollOffset;
        float height = CalculateNodeHeight(node) * _zoom;

        if (choiceIndex >= 0 && node.type == NodeType.Choice)
        {
            return new Vector2(pos.x + NODE_WIDTH * _zoom, pos.y + 25 * _zoom + choiceIndex * CHOICE_HEIGHT * _zoom + CHOICE_HEIGHT * _zoom / 2);
        }

        return new Vector2(pos.x + NODE_WIDTH * _zoom, pos.y + height / 2);
    }

    private Vector2 GetNodeInputPosition(DialogueNode node)
    {
        Vector2 pos = node.position * _zoom + _scrollOffset;
        float height = CalculateNodeHeight(node) * _zoom;
        return new Vector2(pos.x, pos.y + height / 2);
    }

    private void DrawBezierConnection(Vector2 start, Vector2 end, Color color)
    {
        Handles.BeginGUI();
        Handles.color = color;

        Vector2 startTangent = start + Vector2.right * 50f;
        Vector2 endTangent = end - Vector2.right * 50f;

        Handles.DrawBezier(start, end, startTangent, endTangent, color, null, 2f);

        // Arrow
        Vector2 arrowDir = (endTangent - end).normalized;
        Vector2 arrowRight = new Vector2(-arrowDir.y, arrowDir.x);
        float arrowSize = 8f;

        Vector3[] arrowPoints = new Vector3[3];
        arrowPoints[0] = end;
        arrowPoints[1] = end + (Vector2)(arrowDir + arrowRight * 0.5f) * arrowSize;
        arrowPoints[2] = end + (Vector2)(arrowDir - arrowRight * 0.5f) * arrowSize;

        Handles.DrawAAConvexPolygon(arrowPoints);

        Handles.EndGUI();
    }

    private DialogueNode GetNodeAtPosition(Vector2 mousePos)
    {
        foreach (var node in _currentGraph.nodes)
        {
            Vector2 pos = node.position * _zoom + _scrollOffset;
            float height = CalculateNodeHeight(node) * _zoom;
            Rect nodeRect = new Rect(pos.x, pos.y, NODE_WIDTH * _zoom, height);

            if (nodeRect.Contains(mousePos))
                return node;
        }
        return null;
    }

    private void StartConnection(string nodeId, int choiceIndex)
    {
        _isConnecting = true;
        _connectFromNodeId = nodeId;
        _connectFromChoiceIndex = choiceIndex;
    }

    private void CompleteConnection(DialogueNode targetNode)
    {
        var fromNode = _currentGraph.nodes.Find(n => n.id == _connectFromNodeId);
        if (fromNode == null || targetNode == null || fromNode == targetNode)
        {
            _isConnecting = false;
            return;
        }

        if (_connectFromChoiceIndex >= 0 && fromNode.type == NodeType.Choice)
        {
            if (_connectFromChoiceIndex < fromNode.choices.Count)
            {
                fromNode.choices[_connectFromChoiceIndex].targetNodeId = targetNode.id;
            }
        }
        else
        {
            if (!fromNode.outgoingConnections.Contains(targetNode.id))
            {
                fromNode.outgoingConnections.Add(targetNode.id);
            }
        }

        _isConnecting = false;
        _connectFromNodeId = "";
        _connectFromChoiceIndex = -1;
    }

    #region Inspector

    private void DrawInspector()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(300));

        _currentTab = GUILayout.Toolbar(_currentTab, _tabNames, GUILayout.Height(25));

        _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);

        switch (_currentTab)
        {
            case 0: DrawNodeInspector(); break;
            case 1: DrawSpeakersTab(); break;
            case 2: DrawVariablesTab(); break;
            case 3: DrawSettingsTab(); break;
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawNodeInspector()
    {
        if (_selectedNode == null)
        {
            EditorGUILayout.HelpBox("Select a node to edit its properties.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"{_selectedNode.type} Node", EditorStyles.boldLabel);

        EditorGUILayout.Space(5);

        _selectedNode.id = EditorGUILayout.TextField("ID", _selectedNode.id);

        EditorGUILayout.Space(10);

        switch (_selectedNode.type)
        {
            case NodeType.Dialogue:
                DrawDialogueNodeInspector();
                break;
            case NodeType.Choice:
                DrawChoiceNodeInspector();
                break;
            case NodeType.Condition:
                DrawConditionNodeInspector();
                break;
            case NodeType.Action:
                DrawActionNodeInspector();
                break;
        }

        EditorGUILayout.Space(20);

        // Connections
        EditorGUILayout.LabelField("Outgoing Connections", EditorStyles.boldLabel);
        for (int i = 0; i < _selectedNode.outgoingConnections.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(_selectedNode.outgoingConnections[i]);
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                _selectedNode.outgoingConnections.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawDialogueNodeInspector()
    {
        // Speaker selection
        string[] speakers = _currentGraph.speakers.Prepend("(None)").ToArray();
        int speakerIndex = System.Array.IndexOf(speakers, _selectedNode.speakerName);
        speakerIndex = EditorGUILayout.Popup("Speaker", Mathf.Max(0, speakerIndex), speakers);
        _selectedNode.speakerName = speakerIndex > 0 ? speakers[speakerIndex] : "";

        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("Dialogue Text");
        _selectedNode.dialogueText = EditorGUILayout.TextArea(_selectedNode.dialogueText, GUILayout.Height(80));

        _selectedNode.localizationKey = EditorGUILayout.TextField("Localization Key", _selectedNode.localizationKey);

        EditorGUILayout.Space(10);

        _selectedNode.animation = (AnimationType)EditorGUILayout.EnumPopup("Animation", _selectedNode.animation);
        _selectedNode.duration = EditorGUILayout.FloatField("Duration (0=auto)", _selectedNode.duration);

        EditorGUILayout.BeginHorizontal();
        _selectedNode.audioClipPath = EditorGUILayout.TextField("Voice Clip", _selectedNode.audioClipPath);
        if (GUILayout.Button("...", GUILayout.Width(25)))
        {
            string path = EditorUtility.OpenFilePanel("Select Audio", "Assets", "wav,mp3,ogg");
            if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
            {
                _selectedNode.audioClipPath = "Assets" + path.Substring(Application.dataPath.Length);
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawChoiceNodeInspector()
    {
        EditorGUILayout.LabelField("Choices", EditorStyles.boldLabel);

        for (int i = 0; i < _selectedNode.choices.Count; i++)
        {
            var choice = _selectedNode.choices[i];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Choice {i + 1}", EditorStyles.boldLabel);
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                _selectedNode.choices.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();

            choice.text = EditorGUILayout.TextField("Text", choice.text);
            choice.localizationKey = EditorGUILayout.TextField("Loc Key", choice.localizationKey);

            string targetName = "(Not Connected)";
            if (!string.IsNullOrEmpty(choice.targetNodeId))
            {
                var targetNode = _currentGraph.nodes.Find(n => n.id == choice.targetNodeId);
                targetName = targetNode?.speakerName ?? choice.targetNodeId;
            }
            EditorGUILayout.LabelField("Target", targetName);

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("+ Add Choice"))
        {
            _selectedNode.choices.Add(new DialogueChoice { text = $"Choice {_selectedNode.choices.Count + 1}" });
        }
    }

    private void DrawConditionNodeInspector()
    {
        EditorGUILayout.LabelField("Conditions", EditorStyles.boldLabel);

        for (int i = 0; i < _selectedNode.conditions.Count; i++)
        {
            var cond = _selectedNode.conditions[i];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            cond.type = (ConditionType)EditorGUILayout.EnumPopup(cond.type, GUILayout.Width(120));
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                _selectedNode.conditions.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();

            cond.parameter = EditorGUILayout.TextField("Parameter", cond.parameter);
            cond.compareOp = (CompareOperator)EditorGUILayout.EnumPopup("Operator", cond.compareOp);
            cond.value = EditorGUILayout.TextField("Value", cond.value);

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("+ Add Condition"))
        {
            _selectedNode.conditions.Add(new DialogueCondition());
        }
    }

    private void DrawActionNodeInspector()
    {
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        for (int i = 0; i < _selectedNode.actions.Count; i++)
        {
            var action = _selectedNode.actions[i];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            action.type = (ActionType)EditorGUILayout.EnumPopup(action.type, GUILayout.Width(120));
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                _selectedNode.actions.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();

            action.parameter = EditorGUILayout.TextField("Parameter", action.parameter);
            action.value = EditorGUILayout.TextField("Value", action.value);

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("+ Add Action"))
        {
            _selectedNode.actions.Add(new DialogueAction());
        }
    }

    private void DrawSpeakersTab()
    {
        EditorGUILayout.LabelField("Speakers", EditorStyles.boldLabel);

        for (int i = 0; i < _currentGraph.speakers.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            _currentGraph.speakers[i] = EditorGUILayout.TextField(_currentGraph.speakers[i]);
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                _currentGraph.speakers.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("+ Add Speaker"))
        {
            _currentGraph.speakers.Add($"Speaker {_currentGraph.speakers.Count + 1}");
        }

        EditorGUILayout.Space(20);

        // Presets
        EditorGUILayout.LabelField("Quick Add", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Player"))
            if (!_currentGraph.speakers.Contains("Player")) _currentGraph.speakers.Add("Player");
        if (GUILayout.Button("NPC"))
            if (!_currentGraph.speakers.Contains("NPC")) _currentGraph.speakers.Add("NPC");
        if (GUILayout.Button("Narrator"))
            if (!_currentGraph.speakers.Contains("Narrator")) _currentGraph.speakers.Add("Narrator");
        EditorGUILayout.EndHorizontal();
    }

    private void DrawVariablesTab()
    {
        EditorGUILayout.LabelField("Dialogue Variables", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Variables can be used in dialogue text with {variable_name} syntax.", MessageType.Info);

        var keys = _currentGraph.variables.Keys.ToList();
        for (int i = 0; i < keys.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(keys[i], GUILayout.Width(100));
            _currentGraph.variables[keys[i]] = EditorGUILayout.TextField(_currentGraph.variables[keys[i]]);
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                _currentGraph.variables.Remove(keys[i]);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Add Variable"))
        {
            string newKey = $"var_{_currentGraph.variables.Count}";
            _currentGraph.variables[newKey] = "";
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSettingsTab()
    {
        EditorGUILayout.LabelField("Graph Settings", EditorStyles.boldLabel);

        _currentGraph.graphName = EditorGUILayout.TextField("Graph Name", _currentGraph.graphName);

        EditorGUILayout.Space(10);

        // Start node
        string[] nodeIds = _currentGraph.nodes.Select(n => n.id).Prepend("(None)").ToArray();
        int startIndex = System.Array.IndexOf(nodeIds, _currentGraph.startNodeId);
        startIndex = EditorGUILayout.Popup("Start Node", Mathf.Max(0, startIndex), nodeIds);
        _currentGraph.startNodeId = startIndex > 0 ? nodeIds[startIndex] : "";

        EditorGUILayout.Space(20);

        EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);

        if (GUILayout.Button("Export to JSON", GUILayout.Height(30)))
            ExportToJson();

        if (GUILayout.Button("Generate C# Data Class", GUILayout.Height(30)))
            GenerateDataClass();

        if (GUILayout.Button("Generate Localization Keys", GUILayout.Height(30)))
            GenerateLocalizationKeys();
    }

    #endregion

    #region Node Operations

    private void CreateNewGraph()
    {
        _currentGraph = new DialogueGraph();
        _currentGraph.graphName = "New Dialogue";

        // Add start node
        var startNode = new DialogueNode
        {
            id = System.Guid.NewGuid().ToString().Substring(0, 8),
            type = NodeType.Start,
            position = new Vector2(50, 200)
        };
        _currentGraph.nodes.Add(startNode);
        _currentGraph.startNodeId = startNode.id;

        _selectedNode = null;
        _scrollOffset = Vector2.zero;
    }

    private void AddNode(NodeType type)
    {
        AddNodeAtPosition(type, new Vector2(300, 200) - _scrollOffset);
    }

    private void AddNodeAtPosition(NodeType type, Vector2 position)
    {
        var node = new DialogueNode
        {
            id = System.Guid.NewGuid().ToString().Substring(0, 8),
            type = type,
            position = (position - _scrollOffset) / _zoom
        };

        if (type == NodeType.Choice)
        {
            node.choices.Add(new DialogueChoice { text = "Choice 1" });
            node.choices.Add(new DialogueChoice { text = "Choice 2" });
        }

        _currentGraph.nodes.Add(node);
        _selectedNode = node;
    }

    private void DeleteNode(DialogueNode node)
    {
        // Remove connections to this node
        foreach (var n in _currentGraph.nodes)
        {
            n.outgoingConnections.Remove(node.id);
            foreach (var choice in n.choices)
            {
                if (choice.targetNodeId == node.id)
                    choice.targetNodeId = "";
            }
        }

        _currentGraph.nodes.Remove(node);

        if (_currentGraph.startNodeId == node.id)
            _currentGraph.startNodeId = "";

        if (_selectedNode == node)
            _selectedNode = null;
    }

    private void DuplicateNode(DialogueNode node)
    {
        string json = JsonUtility.ToJson(node);
        var newNode = JsonUtility.FromJson<DialogueNode>(json);
        newNode.id = System.Guid.NewGuid().ToString().Substring(0, 8);
        newNode.position += new Vector2(50, 50);
        newNode.outgoingConnections.Clear();

        _currentGraph.nodes.Add(newNode);
        _selectedNode = newNode;
    }

    private void SetAsStart(DialogueNode node)
    {
        _currentGraph.startNodeId = node.id;
    }

    #endregion

    #region Save/Load

    private void SaveGraph()
    {
        string path = EditorUtility.SaveFilePanel("Save Dialogue", "Assets", _currentGraph.graphName, "json");
        if (string.IsNullOrEmpty(path)) return;

        string json = JsonUtility.ToJson(_currentGraph, true);
        System.IO.File.WriteAllText(path, json);
        Debug.Log($"Dialogue saved to: {path}");
    }

    private void LoadGraph()
    {
        string path = EditorUtility.OpenFilePanel("Load Dialogue", "Assets", "json");
        if (string.IsNullOrEmpty(path)) return;

        string json = System.IO.File.ReadAllText(path);
        _currentGraph = JsonUtility.FromJson<DialogueGraph>(json);
        _selectedNode = null;
        Debug.Log($"Dialogue loaded: {_currentGraph.graphName}");
    }

    private void ExportToJson()
    {
        SaveGraph();
    }

    private void GenerateDataClass()
    {
        Debug.Log("Generate C# data class - would create runtime dialogue data structure");
    }

    private void GenerateLocalizationKeys()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("# Localization Keys for " + _currentGraph.graphName);
        sb.AppendLine();

        foreach (var node in _currentGraph.nodes)
        {
            if (node.type == NodeType.Dialogue && !string.IsNullOrEmpty(node.dialogueText))
            {
                string key = string.IsNullOrEmpty(node.localizationKey) ? $"DLG_{_currentGraph.graphName}_{node.id}" : node.localizationKey;
                sb.AppendLine($"{key}={node.dialogueText}");
            }

            foreach (var choice in node.choices)
            {
                if (!string.IsNullOrEmpty(choice.text))
                {
                    string key = string.IsNullOrEmpty(choice.localizationKey) ? $"CHOICE_{_currentGraph.graphName}_{node.id}_{node.choices.IndexOf(choice)}" : choice.localizationKey;
                    sb.AppendLine($"{key}={choice.text}");
                }
            }
        }

        string path = EditorUtility.SaveFilePanel("Export Localization", "", $"{_currentGraph.graphName}_localization", "txt");
        if (!string.IsNullOrEmpty(path))
        {
            System.IO.File.WriteAllText(path, sb.ToString());
            Debug.Log($"Localization keys exported to: {path}");
        }
    }

    #endregion
}
