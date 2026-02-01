using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Editeur visuel de quetes avec nodes et connexions.
/// Menu: EpicLegends > Tools > Quest Graph Editor
/// </summary>
public class QuestGraphEditor : EditorWindow
{
    #region State

    private List<QuestNode> _nodes = new List<QuestNode>();
    private List<QuestConnection> _connections = new List<QuestConnection>();
    private QuestNode _selectedNode;
    private QuestNode _connectingFrom;
    private Vector2 _scrollOffset;
    private Vector2 _drag;
    private bool _isDraggingNode;
    private bool _isDraggingCanvas;
    private Rect _canvasRect;

    private string _questName = "New Quest";
    private string _questDescription = "";

    #endregion

    #region Constants

    private const float NODE_WIDTH = 180f;
    private const float NODE_HEIGHT = 80f;
    private const float GRID_SIZE = 20f;

    #endregion

    [MenuItem("EpicLegends/Tools/Quest Graph Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<QuestGraphEditor>("Quest Graph Editor");
        window.minSize = new Vector2(800, 600);
    }

    private void OnEnable()
    {
        // Create default start node if empty
        if (_nodes.Count == 0)
        {
            CreateDefaultNodes();
        }
    }

    private void OnGUI()
    {
        // Toolbar
        DrawToolbar();

        // Main area
        EditorGUILayout.BeginHorizontal();

        // Canvas
        _canvasRect = new Rect(0, 20, position.width - 250, position.height - 20);
        DrawCanvas();

        // Properties panel
        GUILayout.BeginArea(new Rect(position.width - 250, 20, 250, position.height - 20));
        DrawPropertiesPanel();
        GUILayout.EndArea();

        EditorGUILayout.EndHorizontal();

        // Handle events
        ProcessEvents(Event.current);

        if (GUI.changed)
            Repaint();
    }

    #region Drawing

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            NewQuest();
        }

        if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            LoadQuest();
        }

        if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            SaveQuest();
        }

        GUILayout.Space(20);

        EditorGUILayout.LabelField("Quest:", GUILayout.Width(45));
        _questName = EditorGUILayout.TextField(_questName, GUILayout.Width(150));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("+ Start", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            AddNode(QuestNodeType.Start);
        }
        if (GUILayout.Button("+ Objective", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            AddNode(QuestNodeType.Objective);
        }
        if (GUILayout.Button("+ Branch", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            AddNode(QuestNodeType.Branch);
        }
        if (GUILayout.Button("+ Reward", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            AddNode(QuestNodeType.Reward);
        }
        if (GUILayout.Button("+ End", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            AddNode(QuestNodeType.End);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawCanvas()
    {
        // Background
        GUI.Box(_canvasRect, "", EditorStyles.helpBox);

        // Draw grid
        DrawGrid(GRID_SIZE, 0.2f, Color.gray);
        DrawGrid(GRID_SIZE * 5, 0.4f, Color.gray);

        // Begin scroll view
        GUI.BeginGroup(_canvasRect);

        // Draw connections
        foreach (var connection in _connections)
        {
            DrawConnection(connection);
        }

        // Draw connecting line
        if (_connectingFrom != null)
        {
            Vector2 start = _connectingFrom.rect.center + _scrollOffset;
            Vector2 end = Event.current.mousePosition;
            DrawBezier(start, end, Color.yellow);
            Repaint();
        }

        // Draw nodes
        BeginWindows();
        for (int i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            Rect nodeRect = new Rect(
                node.rect.x + _scrollOffset.x,
                node.rect.y + _scrollOffset.y,
                node.rect.width,
                node.rect.height
            );

            // Node window
            Color nodeColor = GetNodeColor(node.type);
            GUI.backgroundColor = nodeColor;

            nodeRect = GUI.Window(i, nodeRect, DrawNodeWindow, node.title);

            // Update position (accounting for scroll offset)
            node.rect.x = nodeRect.x - _scrollOffset.x;
            node.rect.y = nodeRect.y - _scrollOffset.y;

            GUI.backgroundColor = Color.white;
        }
        EndWindows();

        GUI.EndGroup();
    }

    private void DrawGrid(float gridSpacing, float gridOpacity, Color gridColor)
    {
        int widthDivs = Mathf.CeilToInt(_canvasRect.width / gridSpacing);
        int heightDivs = Mathf.CeilToInt(_canvasRect.height / gridSpacing);

        Handles.BeginGUI();
        Handles.color = new Color(gridColor.r, gridColor.g, gridColor.b, gridOpacity);

        Vector3 offset = new Vector3(_scrollOffset.x % gridSpacing, _scrollOffset.y % gridSpacing, 0);

        for (int i = 0; i <= widthDivs; i++)
        {
            Vector3 start = new Vector3(gridSpacing * i + _canvasRect.x, _canvasRect.y, 0) + offset;
            Vector3 end = new Vector3(gridSpacing * i + _canvasRect.x, _canvasRect.yMax, 0) + offset;
            Handles.DrawLine(start, end);
        }

        for (int j = 0; j <= heightDivs; j++)
        {
            Vector3 start = new Vector3(_canvasRect.x, gridSpacing * j + _canvasRect.y, 0) + offset;
            Vector3 end = new Vector3(_canvasRect.xMax, gridSpacing * j + _canvasRect.y, 0) + offset;
            Handles.DrawLine(start, end);
        }

        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private void DrawNodeWindow(int id)
    {
        var node = _nodes[id];

        // Connection buttons
        Rect inRect = new Rect(0, 30, 20, 20);
        Rect outRect = new Rect(node.rect.width - 20, 30, 20, 20);

        if (node.type != QuestNodeType.Start)
        {
            GUI.backgroundColor = Color.green;
            if (GUI.Button(inRect, "◀"))
            {
                // Handle incoming connection
            }
        }

        if (node.type != QuestNodeType.End)
        {
            GUI.backgroundColor = Color.red;
            if (GUI.Button(outRect, "▶"))
            {
                if (_connectingFrom == null)
                {
                    _connectingFrom = node;
                }
                else if (_connectingFrom != node)
                {
                    CreateConnection(_connectingFrom, node);
                    _connectingFrom = null;
                }
            }
        }

        GUI.backgroundColor = Color.white;

        // Node content
        GUILayout.BeginArea(new Rect(25, 25, node.rect.width - 50, node.rect.height - 30));

        switch (node.type)
        {
            case QuestNodeType.Objective:
                EditorGUILayout.LabelField(node.objectiveType.ToString(), EditorStyles.miniLabel);
                break;
            case QuestNodeType.Branch:
                EditorGUILayout.LabelField("Condition", EditorStyles.miniLabel);
                break;
            case QuestNodeType.Reward:
                EditorGUILayout.LabelField($"XP: {node.xpReward}", EditorStyles.miniLabel);
                break;
        }

        GUILayout.EndArea();

        // Selection
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            _selectedNode = node;
            GUI.changed = true;
        }

        // Right click menu
        if (Event.current.type == EventType.MouseDown && Event.current.button == 1)
        {
            ShowNodeContextMenu(node);
            Event.current.Use();
        }

        GUI.DragWindow();
    }

    private void DrawConnection(QuestConnection connection)
    {
        if (connection.from == null || connection.to == null)
            return;

        Vector2 start = connection.from.rect.center + _scrollOffset;
        start.x += connection.from.rect.width / 2;

        Vector2 end = connection.to.rect.center + _scrollOffset;
        end.x -= connection.to.rect.width / 2;

        DrawBezier(start, end, Color.white);

        // Delete button at midpoint
        Vector2 midPoint = (start + end) / 2;
        if (GUI.Button(new Rect(midPoint.x - 8, midPoint.y - 8, 16, 16), "×"))
        {
            _connections.Remove(connection);
        }
    }

    private void DrawBezier(Vector2 start, Vector2 end, Color color)
    {
        Vector2 startTangent = start + Vector2.right * 50;
        Vector2 endTangent = end + Vector2.left * 50;

        Handles.DrawBezier(start, end, startTangent, endTangent, color, null, 3f);
    }

    private void DrawPropertiesPanel()
    {
        EditorGUILayout.LabelField("Properties", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // Quest properties
        EditorGUILayout.LabelField("Quest Info", EditorStyles.miniBoldLabel);
        _questName = EditorGUILayout.TextField("Name", _questName);
        _questDescription = EditorGUILayout.TextArea(_questDescription, GUILayout.Height(60));

        EditorGUILayout.Space(10);

        // Selected node properties
        if (_selectedNode != null)
        {
            EditorGUILayout.LabelField("Selected Node", EditorStyles.boldLabel);

            GUI.backgroundColor = GetNodeColor(_selectedNode.type);
            EditorGUILayout.LabelField($"Type: {_selectedNode.type}");
            GUI.backgroundColor = Color.white;

            _selectedNode.title = EditorGUILayout.TextField("Title", _selectedNode.title);

            EditorGUILayout.Space(5);

            switch (_selectedNode.type)
            {
                case QuestNodeType.Objective:
                    DrawObjectiveProperties();
                    break;
                case QuestNodeType.Branch:
                    DrawBranchProperties();
                    break;
                case QuestNodeType.Reward:
                    DrawRewardProperties();
                    break;
            }

            EditorGUILayout.Space(10);

            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("Delete Node"))
            {
                DeleteNode(_selectedNode);
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            EditorGUILayout.HelpBox("Select a node to edit its properties", MessageType.Info);
        }

        GUILayout.FlexibleSpace();

        // Export button
        EditorGUILayout.Space(10);
        GUI.backgroundColor = new Color(0.5f, 0.8f, 0.5f);
        if (GUILayout.Button("Export to QuestData", GUILayout.Height(30)))
        {
            ExportToQuestData();
        }
        GUI.backgroundColor = Color.white;
    }

    private void DrawObjectiveProperties()
    {
        _selectedNode.objectiveType = (ObjectiveType)EditorGUILayout.EnumPopup("Objective Type", _selectedNode.objectiveType);
        _selectedNode.description = EditorGUILayout.TextField("Description", _selectedNode.description);
        _selectedNode.targetCount = EditorGUILayout.IntField("Target Count", _selectedNode.targetCount);
        _selectedNode.targetId = EditorGUILayout.TextField("Target ID", _selectedNode.targetId);
    }

    private void DrawBranchProperties()
    {
        _selectedNode.conditionType = (ConditionType)EditorGUILayout.EnumPopup("Condition", _selectedNode.conditionType);
        _selectedNode.conditionValue = EditorGUILayout.TextField("Value", _selectedNode.conditionValue);
    }

    private void DrawRewardProperties()
    {
        _selectedNode.xpReward = EditorGUILayout.IntField("XP Reward", _selectedNode.xpReward);
        _selectedNode.goldReward = EditorGUILayout.IntField("Gold Reward", _selectedNode.goldReward);
        _selectedNode.itemRewards = EditorGUILayout.TextField("Item IDs (comma)", _selectedNode.itemRewards);
    }

    #endregion

    #region Events

    private void ProcessEvents(Event e)
    {
        _drag = Vector2.zero;

        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 0)
                {
                    // Cancel connection on left click on canvas
                    if (_connectingFrom != null && !IsMouseOverNode(e.mousePosition))
                    {
                        _connectingFrom = null;
                    }
                }
                else if (e.button == 1)
                {
                    // Right click on canvas
                    if (!IsMouseOverNode(e.mousePosition))
                    {
                        ShowCanvasContextMenu(e.mousePosition);
                    }
                }
                else if (e.button == 2)
                {
                    _isDraggingCanvas = true;
                }
                break;

            case EventType.MouseUp:
                _isDraggingCanvas = false;
                break;

            case EventType.MouseDrag:
                if (e.button == 2 || (e.button == 0 && e.alt))
                {
                    _drag = e.delta;
                    _scrollOffset += _drag;
                    e.Use();
                }
                break;

            case EventType.KeyDown:
                if (e.keyCode == KeyCode.Delete && _selectedNode != null)
                {
                    DeleteNode(_selectedNode);
                    e.Use();
                }
                if (e.keyCode == KeyCode.Escape)
                {
                    _connectingFrom = null;
                    _selectedNode = null;
                }
                break;
        }
    }

    private bool IsMouseOverNode(Vector2 mousePos)
    {
        foreach (var node in _nodes)
        {
            Rect nodeRect = new Rect(
                node.rect.x + _scrollOffset.x,
                node.rect.y + _scrollOffset.y + 20,
                node.rect.width,
                node.rect.height
            );
            if (nodeRect.Contains(mousePos))
                return true;
        }
        return false;
    }

    private void ShowNodeContextMenu(QuestNode node)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Connect From"), false, () => _connectingFrom = node);
        menu.AddItem(new GUIContent("Duplicate"), false, () => DuplicateNode(node));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Delete"), false, () => DeleteNode(node));
        menu.ShowAsContext();
    }

    private void ShowCanvasContextMenu(Vector2 mousePos)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Add Start Node"), false, () => AddNodeAtPosition(QuestNodeType.Start, mousePos));
        menu.AddItem(new GUIContent("Add Objective Node"), false, () => AddNodeAtPosition(QuestNodeType.Objective, mousePos));
        menu.AddItem(new GUIContent("Add Branch Node"), false, () => AddNodeAtPosition(QuestNodeType.Branch, mousePos));
        menu.AddItem(new GUIContent("Add Reward Node"), false, () => AddNodeAtPosition(QuestNodeType.Reward, mousePos));
        menu.AddItem(new GUIContent("Add End Node"), false, () => AddNodeAtPosition(QuestNodeType.End, mousePos));
        menu.ShowAsContext();
    }

    #endregion

    #region Node Management

    private void CreateDefaultNodes()
    {
        var startNode = new QuestNode
        {
            type = QuestNodeType.Start,
            title = "Quest Start",
            rect = new Rect(50, 200, NODE_WIDTH, NODE_HEIGHT)
        };

        var objectiveNode = new QuestNode
        {
            type = QuestNodeType.Objective,
            title = "Kill Enemies",
            objectiveType = ObjectiveType.Kill,
            targetCount = 5,
            rect = new Rect(300, 200, NODE_WIDTH, NODE_HEIGHT)
        };

        var endNode = new QuestNode
        {
            type = QuestNodeType.End,
            title = "Quest Complete",
            rect = new Rect(550, 200, NODE_WIDTH, NODE_HEIGHT)
        };

        _nodes.Add(startNode);
        _nodes.Add(objectiveNode);
        _nodes.Add(endNode);

        _connections.Add(new QuestConnection { from = startNode, to = objectiveNode });
        _connections.Add(new QuestConnection { from = objectiveNode, to = endNode });
    }

    private void AddNode(QuestNodeType type)
    {
        Vector2 pos = new Vector2(200, 200) - _scrollOffset;
        AddNodeAtPosition(type, pos);
    }

    private void AddNodeAtPosition(QuestNodeType type, Vector2 position)
    {
        var node = new QuestNode
        {
            type = type,
            title = GetDefaultTitle(type),
            rect = new Rect(position.x - _scrollOffset.x - NODE_WIDTH / 2,
                           position.y - _scrollOffset.y - NODE_HEIGHT / 2 - 20,
                           NODE_WIDTH, NODE_HEIGHT)
        };

        _nodes.Add(node);
        _selectedNode = node;
    }

    private void DeleteNode(QuestNode node)
    {
        // Remove connections
        _connections.RemoveAll(c => c.from == node || c.to == node);

        // Remove node
        _nodes.Remove(node);

        if (_selectedNode == node)
            _selectedNode = null;
    }

    private void DuplicateNode(QuestNode node)
    {
        var newNode = new QuestNode
        {
            type = node.type,
            title = node.title + " (Copy)",
            description = node.description,
            objectiveType = node.objectiveType,
            targetCount = node.targetCount,
            targetId = node.targetId,
            xpReward = node.xpReward,
            goldReward = node.goldReward,
            rect = new Rect(node.rect.x + 30, node.rect.y + 30, node.rect.width, node.rect.height)
        };

        _nodes.Add(newNode);
        _selectedNode = newNode;
    }

    private void CreateConnection(QuestNode from, QuestNode to)
    {
        // Don't connect to self
        if (from == to) return;

        // Check if connection already exists
        if (_connections.Any(c => c.from == from && c.to == to))
            return;

        _connections.Add(new QuestConnection { from = from, to = to });
    }

    private Color GetNodeColor(QuestNodeType type)
    {
        return type switch
        {
            QuestNodeType.Start => new Color(0.4f, 0.8f, 0.4f),
            QuestNodeType.Objective => new Color(0.4f, 0.6f, 0.9f),
            QuestNodeType.Branch => new Color(0.9f, 0.7f, 0.3f),
            QuestNodeType.Reward => new Color(0.9f, 0.9f, 0.3f),
            QuestNodeType.End => new Color(0.8f, 0.4f, 0.4f),
            _ => Color.gray
        };
    }

    private string GetDefaultTitle(QuestNodeType type)
    {
        return type switch
        {
            QuestNodeType.Start => "Quest Start",
            QuestNodeType.Objective => "New Objective",
            QuestNodeType.Branch => "Condition",
            QuestNodeType.Reward => "Reward",
            QuestNodeType.End => "Quest End",
            _ => "Node"
        };
    }

    #endregion

    #region Save/Load

    private void NewQuest()
    {
        if (EditorUtility.DisplayDialog("New Quest", "Clear current quest and start new?", "Yes", "Cancel"))
        {
            _nodes.Clear();
            _connections.Clear();
            _selectedNode = null;
            _questName = "New Quest";
            _questDescription = "";
            CreateDefaultNodes();
        }
    }

    private void SaveQuest()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Quest Graph",
            _questName + "_Graph",
            "asset",
            "Save quest graph data"
        );

        if (!string.IsNullOrEmpty(path))
        {
            // Create save data
            var saveData = ScriptableObject.CreateInstance<QuestGraphData>();
            saveData.questName = _questName;
            saveData.questDescription = _questDescription;
            saveData.nodes = _nodes.ToArray();
            saveData.connections = _connections.Select(c => new QuestConnectionData
            {
                fromIndex = _nodes.IndexOf(c.from),
                toIndex = _nodes.IndexOf(c.to)
            }).ToArray();

            AssetDatabase.CreateAsset(saveData, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[QuestGraphEditor] Saved quest graph: {path}");
        }
    }

    private void LoadQuest()
    {
        string path = EditorUtility.OpenFilePanel("Load Quest Graph", "Assets", "asset");

        if (!string.IsNullOrEmpty(path))
        {
            // Convert to relative path
            path = "Assets" + path.Substring(Application.dataPath.Length);

            var saveData = AssetDatabase.LoadAssetAtPath<QuestGraphData>(path);
            if (saveData != null)
            {
                _questName = saveData.questName;
                _questDescription = saveData.questDescription;
                _nodes = saveData.nodes.ToList();
                _connections.Clear();

                foreach (var connData in saveData.connections)
                {
                    if (connData.fromIndex >= 0 && connData.fromIndex < _nodes.Count &&
                        connData.toIndex >= 0 && connData.toIndex < _nodes.Count)
                    {
                        _connections.Add(new QuestConnection
                        {
                            from = _nodes[connData.fromIndex],
                            to = _nodes[connData.toIndex]
                        });
                    }
                }

                Debug.Log($"[QuestGraphEditor] Loaded quest graph: {path}");
            }
        }
    }

    private void ExportToQuestData()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Export Quest Data",
            _questName,
            "asset",
            "Export to QuestData ScriptableObject"
        );

        if (string.IsNullOrEmpty(path)) return;

        var questData = ScriptableObject.CreateInstance<QuestData>();
        questData.questName = _questName;
        questData.shortDescription = _questDescription;
        questData.fullDescription = _questDescription;

        // Convert nodes to objectives
        var objectives = new List<QuestObjective>();
        foreach (var node in _nodes.Where(n => n.type == QuestNodeType.Objective))
        {
            objectives.Add(new QuestObjective
            {
                description = node.description,
                type = ConvertObjectiveType(node.objectiveType),
                targetId = node.targetId,
                requiredAmount = node.targetCount
            });
        }
        questData.objectives = objectives.ToArray();

        // Get rewards
        var rewardNode = _nodes.FirstOrDefault(n => n.type == QuestNodeType.Reward);
        if (rewardNode != null)
        {
            questData.goldReward = rewardNode.goldReward;
            questData.xpReward = rewardNode.xpReward;
        }

        AssetDatabase.CreateAsset(questData, path);
        AssetDatabase.SaveAssets();

        Debug.Log($"[QuestGraphEditor] Exported QuestData: {path}");
        EditorGUIUtility.PingObject(questData);
    }

    #endregion

    #region Data Classes

    public enum QuestNodeType
    {
        Start,
        Objective,
        Branch,
        Reward,
        End
    }

    public enum ObjectiveType
    {
        Kill,
        Collect,
        Talk,
        Reach,
        Escort,
        Defend
    }

    public enum ConditionType
    {
        HasItem,
        QuestComplete,
        LevelReached,
        FlagSet
    }

    [System.Serializable]
    public class QuestNode
    {
        public QuestNodeType type;
        public string title = "Node";
        public string description = "";
        public Rect rect;

        // Objective
        public ObjectiveType objectiveType;
        public string targetId = "";
        public int targetCount = 1;

        // Branch
        public ConditionType conditionType;
        public string conditionValue = "";

        // Reward
        public int xpReward;
        public int goldReward;
        public string itemRewards = "";
    }

    public class QuestConnection
    {
        public QuestNode from;
        public QuestNode to;
    }

    [System.Serializable]
    public class QuestConnectionData
    {
        public int fromIndex;
        public int toIndex;
    }

    #endregion

    #region Helpers

    private QuestObjectiveType ConvertObjectiveType(ObjectiveType type)
    {
        switch (type)
        {
            case ObjectiveType.Kill: return QuestObjectiveType.Kill;
            case ObjectiveType.Collect: return QuestObjectiveType.Collect;
            case ObjectiveType.Talk: return QuestObjectiveType.Talk;
            case ObjectiveType.Reach: return QuestObjectiveType.Reach;
            case ObjectiveType.Escort: return QuestObjectiveType.Escort;
            case ObjectiveType.Defend: return QuestObjectiveType.Defend;
            default: return QuestObjectiveType.Kill;
        }
    }

    #endregion
}

/// <summary>
/// ScriptableObject pour sauvegarder les graphs de quetes.
/// </summary>
public class QuestGraphData : ScriptableObject
{
    public string questName;
    public string questDescription;
    public QuestGraphEditor.QuestNode[] nodes;
    public QuestGraphEditor.QuestConnectionData[] connections;
}
