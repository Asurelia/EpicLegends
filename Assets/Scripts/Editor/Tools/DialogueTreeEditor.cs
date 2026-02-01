using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Editeur visuel d'arbres de dialogue avec nodes et conditions.
/// Menu: EpicLegends > Tools > Dialogue Tree Editor
/// </summary>
public class DialogueTreeEditor : EditorWindow
{
    #region Types

    public enum NodeType
    {
        Start,
        Dialogue,
        Choice,
        Condition,
        Action,
        End
    }

    [System.Serializable]
    public class DialogueNode
    {
        public int id;
        public NodeType type;
        public Rect rect;
        public string title = "";

        // Dialogue content
        public string speakerName = "";
        public string dialogueText = "";
        public Sprite speakerPortrait;
        public AudioClip voiceLine;

        // Choices (for Choice nodes)
        public List<string> choices = new List<string>();
        public List<int> choiceTargets = new List<int>();

        // Conditions
        public string conditionVariable = "";
        public ConditionOperator conditionOperator = ConditionOperator.Equals;
        public string conditionValue = "";
        public int trueTargetId = -1;
        public int falseTargetId = -1;

        // Actions
        public ActionType actionType = ActionType.SetVariable;
        public string actionVariable = "";
        public string actionValue = "";
        public int nextNodeId = -1;

        // Visual
        public bool isSelected = false;
    }

    public enum ConditionOperator
    {
        Equals,
        NotEquals,
        GreaterThan,
        LessThan,
        Contains
    }

    public enum ActionType
    {
        SetVariable,
        AddItem,
        RemoveItem,
        StartQuest,
        CompleteQuest,
        AddExperience,
        PlayAnimation,
        PlaySound
    }

    #endregion

    #region State

    private List<DialogueNode> _nodes = new List<DialogueNode>();
    private int _nextNodeId = 0;
    private DialogueNode _selectedNode;
    private Vector2 _scrollOffset = Vector2.zero;
    private Vector2 _inspectorScroll = Vector2.zero;
    private bool _isConnecting = false;
    private DialogueNode _connectFrom;
    private int _connectChoiceIndex = -1;
    private bool _connectIsTrue = true;

    private float _zoom = 1f;
    private Vector2 _lastMousePos;
    private bool _isDragging = false;

    private string _dialogueName = "New Dialogue";
    private string _dialogueDescription = "";

    // Node dimensions
    private const float NODE_WIDTH = 180f;
    private const float NODE_HEIGHT = 80f;

    #endregion

    [MenuItem("EpicLegends/Tools/Dialogue Tree Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<DialogueTreeEditor>("Dialogue Editor");
        window.minSize = new Vector2(800, 600);
    }

    private void OnEnable()
    {
        if (_nodes.Count == 0)
        {
            CreateDefaultTree();
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();

        // Left panel: Node canvas
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.7f));
        DrawToolbar();
        DrawCanvas();
        EditorGUILayout.EndVertical();

        // Right panel: Inspector
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.3f - 5));
        DrawInspector();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        HandleEvents();
    }

    #region GUI Sections

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Dialogue info
        EditorGUILayout.LabelField("Dialogue:", GUILayout.Width(60));
        _dialogueName = EditorGUILayout.TextField(_dialogueName, GUILayout.Width(150));

        GUILayout.Space(20);

        // Add node buttons
        if (GUILayout.Button("+ Dialogue", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            CreateNode(NodeType.Dialogue, GetCanvasCenter());
        }
        if (GUILayout.Button("+ Choice", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            CreateNode(NodeType.Choice, GetCanvasCenter());
        }
        if (GUILayout.Button("+ Condition", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            CreateNode(NodeType.Condition, GetCanvasCenter());
        }
        if (GUILayout.Button("+ Action", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            CreateNode(NodeType.Action, GetCanvasCenter());
        }
        if (GUILayout.Button("+ End", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            CreateNode(NodeType.End, GetCanvasCenter());
        }

        GUILayout.FlexibleSpace();

        // Zoom
        EditorGUILayout.LabelField("Zoom:", GUILayout.Width(40));
        _zoom = EditorGUILayout.Slider(_zoom, 0.5f, 1.5f, GUILayout.Width(100));

        // Actions
        if (GUILayout.Button("Export", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            ExportDialogue();
        }
        if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            if (EditorUtility.DisplayDialog("Clear Dialogue", "Are you sure you want to clear all nodes?", "Yes", "No"))
            {
                _nodes.Clear();
                CreateDefaultTree();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawCanvas()
    {
        Rect canvasRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        // Draw background
        GUI.Box(canvasRect, "", EditorStyles.helpBox);
        DrawGrid(canvasRect, 20f, new Color(0.3f, 0.3f, 0.3f, 0.3f));
        DrawGrid(canvasRect, 100f, new Color(0.3f, 0.3f, 0.3f, 0.5f));

        // Begin scroll/zoom group
        GUI.BeginGroup(canvasRect);

        Matrix4x4 oldMatrix = GUI.matrix;
        Vector2 canvasCenter = new Vector2(canvasRect.width / 2, canvasRect.height / 2);
        GUIUtility.ScaleAroundPivot(Vector2.one * _zoom, canvasCenter - _scrollOffset);

        // Draw connections first
        DrawConnections();

        // Draw connection preview
        if (_isConnecting && _connectFrom != null)
        {
            Vector2 startPos = GetConnectionStartPoint(_connectFrom, _connectChoiceIndex);
            Vector2 endPos = Event.current.mousePosition;
            DrawBezierConnection(startPos, endPos, Color.yellow);
            Repaint();
        }

        // Draw nodes
        BeginWindows();
        for (int i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            node.rect = GUI.Window(node.id, node.rect, DrawNodeWindow, "", GetNodeStyle(node));
        }
        EndWindows();

        GUI.matrix = oldMatrix;
        GUI.EndGroup();
    }

    private void DrawGrid(Rect rect, float spacing, Color color)
    {
        int widthDivs = Mathf.CeilToInt(rect.width / spacing);
        int heightDivs = Mathf.CeilToInt(rect.height / spacing);

        Handles.BeginGUI();
        Handles.color = color;

        Vector3 offset = new Vector3(_scrollOffset.x % spacing, _scrollOffset.y % spacing, 0);

        for (int i = 0; i <= widthDivs; i++)
        {
            Handles.DrawLine(
                new Vector3(rect.x + spacing * i + offset.x, rect.y, 0),
                new Vector3(rect.x + spacing * i + offset.x, rect.y + rect.height, 0)
            );
        }

        for (int i = 0; i <= heightDivs; i++)
        {
            Handles.DrawLine(
                new Vector3(rect.x, rect.y + spacing * i + offset.y, 0),
                new Vector3(rect.x + rect.width, rect.y + spacing * i + offset.y, 0)
            );
        }

        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private void DrawNodeWindow(int id)
    {
        var node = _nodes.FirstOrDefault(n => n.id == id);
        if (node == null) return;

        // Header with type icon
        GUILayout.BeginHorizontal();

        string icon = GetNodeIcon(node.type);
        GUILayout.Label(icon, new GUIStyle { fontSize = 16, alignment = TextAnchor.MiddleCenter }, GUILayout.Width(25));

        string headerText = node.type == NodeType.Dialogue ? node.speakerName : node.type.ToString();
        if (string.IsNullOrEmpty(headerText)) headerText = node.type.ToString();

        GUILayout.Label(headerText, EditorStyles.boldLabel);

        GUILayout.EndHorizontal();

        // Content preview
        switch (node.type)
        {
            case NodeType.Dialogue:
                string preview = node.dialogueText;
                if (preview.Length > 30) preview = preview.Substring(0, 30) + "...";
                GUILayout.Label(preview, EditorStyles.wordWrappedMiniLabel);
                break;

            case NodeType.Choice:
                for (int i = 0; i < Mathf.Min(node.choices.Count, 3); i++)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"• {node.choices[i]}", EditorStyles.miniLabel);

                    // Connection button
                    if (GUILayout.Button("→", GUILayout.Width(20)))
                    {
                        StartConnection(node, i);
                    }
                    GUILayout.EndHorizontal();
                }
                if (node.choices.Count > 3)
                {
                    GUILayout.Label($"... +{node.choices.Count - 3} more", EditorStyles.centeredGreyMiniLabel);
                }
                break;

            case NodeType.Condition:
                GUILayout.Label($"if {node.conditionVariable}", EditorStyles.miniLabel);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("✓", GUILayout.Width(30)))
                {
                    StartConditionConnection(node, true);
                }
                if (GUILayout.Button("✗", GUILayout.Width(30)))
                {
                    StartConditionConnection(node, false);
                }
                GUILayout.EndHorizontal();
                break;

            case NodeType.Action:
                GUILayout.Label($"{node.actionType}", EditorStyles.miniLabel);
                break;
        }

        // Next button for linear nodes
        if (node.type == NodeType.Dialogue || node.type == NodeType.Action)
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("→ Next", GUILayout.Width(60)))
            {
                StartConnection(node, -1);
            }
            GUILayout.EndHorizontal();
        }

        // Make draggable
        GUI.DragWindow(new Rect(0, 0, NODE_WIDTH, 20));

        // Handle selection
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            SelectNode(node);
            Event.current.Use();
        }
    }

    private void DrawConnections()
    {
        foreach (var node in _nodes)
        {
            switch (node.type)
            {
                case NodeType.Dialogue:
                case NodeType.Action:
                    if (node.nextNodeId >= 0)
                    {
                        var target = _nodes.FirstOrDefault(n => n.id == node.nextNodeId);
                        if (target != null)
                        {
                            DrawConnection(node, target, Color.white);
                        }
                    }
                    break;

                case NodeType.Choice:
                    for (int i = 0; i < node.choiceTargets.Count; i++)
                    {
                        if (node.choiceTargets[i] >= 0)
                        {
                            var target = _nodes.FirstOrDefault(n => n.id == node.choiceTargets[i]);
                            if (target != null)
                            {
                                Vector2 startPos = GetConnectionStartPoint(node, i);
                                Vector2 endPos = new Vector2(target.rect.x, target.rect.center.y);
                                DrawBezierConnection(startPos, endPos, new Color(0.5f, 0.8f, 1f));
                            }
                        }
                    }
                    break;

                case NodeType.Condition:
                    if (node.trueTargetId >= 0)
                    {
                        var trueTarget = _nodes.FirstOrDefault(n => n.id == node.trueTargetId);
                        if (trueTarget != null)
                        {
                            DrawConnection(node, trueTarget, Color.green);
                        }
                    }
                    if (node.falseTargetId >= 0)
                    {
                        var falseTarget = _nodes.FirstOrDefault(n => n.id == node.falseTargetId);
                        if (falseTarget != null)
                        {
                            DrawConnection(node, falseTarget, Color.red);
                        }
                    }
                    break;
            }
        }
    }

    private void DrawConnection(DialogueNode from, DialogueNode to, Color color)
    {
        Vector2 startPos = new Vector2(from.rect.xMax, from.rect.center.y);
        Vector2 endPos = new Vector2(to.rect.x, to.rect.center.y);
        DrawBezierConnection(startPos, endPos, color);
    }

    private void DrawBezierConnection(Vector2 start, Vector2 end, Color color)
    {
        Handles.BeginGUI();

        float tangentStrength = Mathf.Min(50f, Mathf.Abs(end.x - start.x) * 0.5f);
        Vector2 startTangent = start + Vector2.right * tangentStrength;
        Vector2 endTangent = end - Vector2.right * tangentStrength;

        Handles.DrawBezier(start, end, startTangent, endTangent, color, null, 3f);

        // Arrow
        Vector2 arrowDir = (end - endTangent).normalized;
        Vector2 arrowLeft = Quaternion.Euler(0, 0, 30) * -arrowDir * 8;
        Vector2 arrowRight = Quaternion.Euler(0, 0, -30) * -arrowDir * 8;

        Handles.color = color;
        Handles.DrawLine(end, end + arrowLeft);
        Handles.DrawLine(end, end + arrowRight);

        Handles.EndGUI();
    }

    private void DrawInspector()
    {
        EditorGUILayout.LabelField("Properties", EditorStyles.boldLabel);

        if (_selectedNode == null)
        {
            EditorGUILayout.HelpBox("Select a node to edit its properties", MessageType.Info);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Dialogue Info", EditorStyles.boldLabel);
            _dialogueName = EditorGUILayout.TextField("Name", _dialogueName);
            _dialogueDescription = EditorGUILayout.TextArea(_dialogueDescription, GUILayout.Height(60));

            return;
        }

        _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Node type (read-only)
        EditorGUILayout.LabelField("Type", _selectedNode.type.ToString());
        EditorGUILayout.LabelField("ID", _selectedNode.id.ToString());

        EditorGUILayout.Space(10);

        // Type-specific properties
        switch (_selectedNode.type)
        {
            case NodeType.Start:
                EditorGUILayout.HelpBox("This is the entry point of the dialogue.", MessageType.Info);
                break;

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

            case NodeType.End:
                EditorGUILayout.HelpBox("This node ends the dialogue.", MessageType.Info);
                break;
        }

        EditorGUILayout.Space(10);

        // Delete button
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        if (_selectedNode.type != NodeType.Start)
        {
            if (GUILayout.Button("Delete Node"))
            {
                DeleteNode(_selectedNode);
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    private void DrawDialogueNodeInspector()
    {
        EditorGUILayout.LabelField("Speaker", EditorStyles.boldLabel);
        _selectedNode.speakerName = EditorGUILayout.TextField("Name", _selectedNode.speakerName);
        _selectedNode.speakerPortrait = (Sprite)EditorGUILayout.ObjectField("Portrait", _selectedNode.speakerPortrait, typeof(Sprite), false);

        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("Dialogue Text", EditorStyles.boldLabel);
        _selectedNode.dialogueText = EditorGUILayout.TextArea(_selectedNode.dialogueText, GUILayout.Height(80));

        EditorGUILayout.Space(5);

        _selectedNode.voiceLine = (AudioClip)EditorGUILayout.ObjectField("Voice Line", _selectedNode.voiceLine, typeof(AudioClip), false);

        EditorGUILayout.Space(5);

        // Connection
        EditorGUILayout.LabelField("Next Node", EditorStyles.boldLabel);
        var nextNode = _nodes.FirstOrDefault(n => n.id == _selectedNode.nextNodeId);
        string nextName = nextNode != null ? $"Node {nextNode.id}: {nextNode.type}" : "None";
        EditorGUILayout.LabelField("Connected to:", nextName);

        if (GUILayout.Button("Clear Connection"))
        {
            _selectedNode.nextNodeId = -1;
        }
    }

    private void DrawChoiceNodeInspector()
    {
        EditorGUILayout.LabelField("Choices", EditorStyles.boldLabel);

        // Ensure lists are synchronized
        while (_selectedNode.choiceTargets.Count < _selectedNode.choices.Count)
        {
            _selectedNode.choiceTargets.Add(-1);
        }

        for (int i = 0; i < _selectedNode.choices.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField($"{i + 1}.", GUILayout.Width(20));
            _selectedNode.choices[i] = EditorGUILayout.TextField(_selectedNode.choices[i]);

            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                _selectedNode.choices.RemoveAt(i);
                _selectedNode.choiceTargets.RemoveAt(i);
                break;
            }

            EditorGUILayout.EndHorizontal();

            // Show connection
            var target = _nodes.FirstOrDefault(n => n.id == _selectedNode.choiceTargets[i]);
            string targetName = target != null ? $"→ Node {target.id}" : "→ Not connected";
            EditorGUILayout.LabelField("   " + targetName, EditorStyles.miniLabel);
        }

        if (GUILayout.Button("+ Add Choice"))
        {
            _selectedNode.choices.Add("New choice");
            _selectedNode.choiceTargets.Add(-1);
        }
    }

    private void DrawConditionNodeInspector()
    {
        EditorGUILayout.LabelField("Condition", EditorStyles.boldLabel);

        _selectedNode.conditionVariable = EditorGUILayout.TextField("Variable", _selectedNode.conditionVariable);
        _selectedNode.conditionOperator = (ConditionOperator)EditorGUILayout.EnumPopup("Operator", _selectedNode.conditionOperator);
        _selectedNode.conditionValue = EditorGUILayout.TextField("Value", _selectedNode.conditionValue);

        EditorGUILayout.Space(5);

        // True/False connections
        var trueNode = _nodes.FirstOrDefault(n => n.id == _selectedNode.trueTargetId);
        var falseNode = _nodes.FirstOrDefault(n => n.id == _selectedNode.falseTargetId);

        EditorGUILayout.LabelField("If TRUE →", trueNode != null ? $"Node {trueNode.id}" : "Not connected");
        EditorGUILayout.LabelField("If FALSE →", falseNode != null ? $"Node {falseNode.id}" : "Not connected");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear True"))
        {
            _selectedNode.trueTargetId = -1;
        }
        if (GUILayout.Button("Clear False"))
        {
            _selectedNode.falseTargetId = -1;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawActionNodeInspector()
    {
        EditorGUILayout.LabelField("Action", EditorStyles.boldLabel);

        _selectedNode.actionType = (ActionType)EditorGUILayout.EnumPopup("Type", _selectedNode.actionType);

        switch (_selectedNode.actionType)
        {
            case ActionType.SetVariable:
                _selectedNode.actionVariable = EditorGUILayout.TextField("Variable", _selectedNode.actionVariable);
                _selectedNode.actionValue = EditorGUILayout.TextField("Value", _selectedNode.actionValue);
                break;

            case ActionType.AddItem:
            case ActionType.RemoveItem:
                _selectedNode.actionValue = EditorGUILayout.TextField("Item ID", _selectedNode.actionValue);
                break;

            case ActionType.StartQuest:
            case ActionType.CompleteQuest:
                _selectedNode.actionValue = EditorGUILayout.TextField("Quest ID", _selectedNode.actionValue);
                break;

            case ActionType.AddExperience:
                _selectedNode.actionValue = EditorGUILayout.TextField("Amount", _selectedNode.actionValue);
                break;

            case ActionType.PlayAnimation:
            case ActionType.PlaySound:
                _selectedNode.actionValue = EditorGUILayout.TextField("Asset Name", _selectedNode.actionValue);
                break;
        }

        EditorGUILayout.Space(5);

        // Next connection
        var nextNode = _nodes.FirstOrDefault(n => n.id == _selectedNode.nextNodeId);
        EditorGUILayout.LabelField("Next →", nextNode != null ? $"Node {nextNode.id}" : "Not connected");

        if (GUILayout.Button("Clear Connection"))
        {
            _selectedNode.nextNodeId = -1;
        }
    }

    #endregion

    #region Logic

    private void CreateDefaultTree()
    {
        _nodes.Clear();
        _nextNodeId = 0;

        // Create start node
        CreateNode(NodeType.Start, new Vector2(100, 200));
    }

    private DialogueNode CreateNode(NodeType type, Vector2 position)
    {
        var node = new DialogueNode
        {
            id = _nextNodeId++,
            type = type,
            rect = new Rect(position.x, position.y, NODE_WIDTH, NODE_HEIGHT),
            title = type.ToString()
        };

        if (type == NodeType.Choice)
        {
            node.choices.Add("Choice 1");
            node.choices.Add("Choice 2");
            node.choiceTargets.Add(-1);
            node.choiceTargets.Add(-1);
            node.rect.height = 120;
        }

        _nodes.Add(node);
        SelectNode(node);

        return node;
    }

    private void DeleteNode(DialogueNode node)
    {
        // Clear any connections TO this node
        foreach (var other in _nodes)
        {
            if (other.nextNodeId == node.id)
                other.nextNodeId = -1;
            if (other.trueTargetId == node.id)
                other.trueTargetId = -1;
            if (other.falseTargetId == node.id)
                other.falseTargetId = -1;

            for (int i = 0; i < other.choiceTargets.Count; i++)
            {
                if (other.choiceTargets[i] == node.id)
                    other.choiceTargets[i] = -1;
            }
        }

        _nodes.Remove(node);

        if (_selectedNode == node)
            _selectedNode = null;
    }

    private void SelectNode(DialogueNode node)
    {
        foreach (var n in _nodes)
        {
            n.isSelected = false;
        }

        _selectedNode = node;
        if (node != null)
        {
            node.isSelected = true;
        }

        Repaint();
    }

    private void StartConnection(DialogueNode from, int choiceIndex)
    {
        _isConnecting = true;
        _connectFrom = from;
        _connectChoiceIndex = choiceIndex;
    }

    private void StartConditionConnection(DialogueNode from, bool isTrue)
    {
        _isConnecting = true;
        _connectFrom = from;
        _connectIsTrue = isTrue;
        _connectChoiceIndex = -2; // Special marker for condition
    }

    private void CompleteConnection(DialogueNode to)
    {
        if (_connectFrom == null || to == null || _connectFrom == to)
        {
            CancelConnection();
            return;
        }

        if (_connectFrom.type == NodeType.Choice && _connectChoiceIndex >= 0)
        {
            // Choice connection
            if (_connectChoiceIndex < _connectFrom.choiceTargets.Count)
            {
                _connectFrom.choiceTargets[_connectChoiceIndex] = to.id;
            }
        }
        else if (_connectFrom.type == NodeType.Condition && _connectChoiceIndex == -2)
        {
            // Condition connection
            if (_connectIsTrue)
            {
                _connectFrom.trueTargetId = to.id;
            }
            else
            {
                _connectFrom.falseTargetId = to.id;
            }
        }
        else
        {
            // Regular next connection
            _connectFrom.nextNodeId = to.id;
        }

        CancelConnection();
    }

    private void CancelConnection()
    {
        _isConnecting = false;
        _connectFrom = null;
        _connectChoiceIndex = -1;
    }

    private void ExportDialogue()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Dialogue",
            _dialogueName,
            "asset",
            "Save dialogue tree"
        );

        if (string.IsNullOrEmpty(path)) return;

        var data = ScriptableObject.CreateInstance<DialogueTreeData>();
        data.dialogueName = _dialogueName;
        data.description = _dialogueDescription;
        data.nodes = _nodes.Select(n => new DialogueTreeData.NodeData
        {
            id = n.id,
            type = n.type.ToString(),
            positionX = n.rect.x,
            positionY = n.rect.y,
            speakerName = n.speakerName,
            dialogueText = n.dialogueText,
            speakerPortrait = n.speakerPortrait,
            voiceLine = n.voiceLine,
            choices = new List<string>(n.choices),
            choiceTargets = new List<int>(n.choiceTargets),
            conditionVariable = n.conditionVariable,
            conditionOperator = n.conditionOperator.ToString(),
            conditionValue = n.conditionValue,
            trueTargetId = n.trueTargetId,
            falseTargetId = n.falseTargetId,
            actionType = n.actionType.ToString(),
            actionVariable = n.actionVariable,
            actionValue = n.actionValue,
            nextNodeId = n.nextNodeId
        }).ToList();

        AssetDatabase.CreateAsset(data, path);
        AssetDatabase.SaveAssets();

        Selection.activeObject = data;
        Debug.Log($"[DialogueTreeEditor] Exported dialogue to {path}");
    }

    #endregion

    #region Event Handling

    private void HandleEvents()
    {
        Event e = Event.current;

        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 0)
                {
                    if (_isConnecting)
                    {
                        // Try to complete connection
                        var targetNode = GetNodeAtPosition(e.mousePosition);
                        if (targetNode != null)
                        {
                            CompleteConnection(targetNode);
                        }
                        else
                        {
                            CancelConnection();
                        }
                        e.Use();
                    }
                }
                else if (e.button == 1)
                {
                    // Right click - cancel connection or show context menu
                    if (_isConnecting)
                    {
                        CancelConnection();
                        e.Use();
                    }
                }
                else if (e.button == 2)
                {
                    // Middle click - start panning
                    _isDragging = true;
                    _lastMousePos = e.mousePosition;
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (e.button == 2)
                {
                    _isDragging = false;
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (_isDragging)
                {
                    Vector2 delta = e.mousePosition - _lastMousePos;
                    _scrollOffset += delta;
                    _lastMousePos = e.mousePosition;
                    e.Use();
                    Repaint();
                }
                break;

            case EventType.ScrollWheel:
                float oldZoom = _zoom;
                _zoom -= e.delta.y * 0.05f;
                _zoom = Mathf.Clamp(_zoom, 0.5f, 1.5f);

                // Adjust scroll offset to zoom towards mouse
                if (_zoom != oldZoom)
                {
                    // Vector2 mousePos = e.mousePosition;
                    // _scrollOffset = mousePos - (mousePos - _scrollOffset) * (_zoom / oldZoom);
                }

                e.Use();
                Repaint();
                break;

            case EventType.KeyDown:
                if (e.keyCode == KeyCode.Delete && _selectedNode != null && _selectedNode.type != NodeType.Start)
                {
                    DeleteNode(_selectedNode);
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Escape && _isConnecting)
                {
                    CancelConnection();
                    e.Use();
                }
                break;
        }
    }

    private DialogueNode GetNodeAtPosition(Vector2 position)
    {
        foreach (var node in _nodes)
        {
            if (node.rect.Contains(position))
            {
                return node;
            }
        }
        return null;
    }

    #endregion

    #region Helpers

    private GUIStyle GetNodeStyle(DialogueNode node)
    {
        Color color = GetNodeColor(node.type);
        if (node.isSelected)
        {
            color = Color.Lerp(color, Color.white, 0.3f);
        }

        GUIStyle style = new GUIStyle("window");
        style.normal.background = MakeTexture(1, 1, color);
        style.padding = new RectOffset(8, 8, 8, 8);

        return style;
    }

    private Color GetNodeColor(NodeType type)
    {
        switch (type)
        {
            case NodeType.Start: return new Color(0.3f, 0.7f, 0.3f);
            case NodeType.Dialogue: return new Color(0.4f, 0.5f, 0.7f);
            case NodeType.Choice: return new Color(0.7f, 0.5f, 0.3f);
            case NodeType.Condition: return new Color(0.6f, 0.3f, 0.6f);
            case NodeType.Action: return new Color(0.3f, 0.6f, 0.6f);
            case NodeType.End: return new Color(0.7f, 0.3f, 0.3f);
            default: return Color.gray;
        }
    }

    private string GetNodeIcon(NodeType type)
    {
        switch (type)
        {
            case NodeType.Start: return "▶";
            case NodeType.Dialogue: return "💬";
            case NodeType.Choice: return "?";
            case NodeType.Condition: return "⚙";
            case NodeType.Action: return "⚡";
            case NodeType.End: return "■";
            default: return "•";
        }
    }

    private Vector2 GetConnectionStartPoint(DialogueNode node, int choiceIndex)
    {
        if (choiceIndex >= 0 && node.type == NodeType.Choice)
        {
            float yOffset = 40 + choiceIndex * 20;
            return new Vector2(node.rect.xMax, node.rect.y + yOffset);
        }
        return new Vector2(node.rect.xMax, node.rect.center.y);
    }

    private Vector2 GetCanvasCenter()
    {
        return new Vector2(position.width * 0.35f, position.height * 0.5f) - _scrollOffset;
    }

    private Texture2D MakeTexture(int width, int height, Color color)
    {
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
        Texture2D texture = new Texture2D(width, height);
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    #endregion
}

/// <summary>
/// ScriptableObject pour stocker les arbres de dialogue.
/// </summary>
public class DialogueTreeData : ScriptableObject
{
    public string dialogueName;
    public string description;

    [System.Serializable]
    public class NodeData
    {
        public int id;
        public string type;
        public float positionX;
        public float positionY;

        // Dialogue
        public string speakerName;
        public string dialogueText;
        public Sprite speakerPortrait;
        public AudioClip voiceLine;

        // Choices
        public List<string> choices = new List<string>();
        public List<int> choiceTargets = new List<int>();

        // Conditions
        public string conditionVariable;
        public string conditionOperator;
        public string conditionValue;
        public int trueTargetId;
        public int falseTargetId;

        // Actions
        public string actionType;
        public string actionVariable;
        public string actionValue;

        // Next
        public int nextNodeId;
    }

    public List<NodeData> nodes = new List<NodeData>();
}
