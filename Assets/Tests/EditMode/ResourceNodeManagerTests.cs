using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests unitaires pour ResourceNodeManager.
/// </summary>
[TestFixture]
public class ResourceNodeManagerTests
{
    private GameObject _managerObj;
    private ResourceNodeManager _manager;
    private List<GameObject> _testObjects;

    [SetUp]
    public void SetUp()
    {
        _testObjects = new List<GameObject>();

        // Créer le manager - appeler Awake manuellement via reflection
        _managerObj = new GameObject("ResourceNodeManager");
        _manager = _managerObj.AddComponent<ResourceNodeManager>();
        _testObjects.Add(_managerObj);

        // Invoke Awake pour initialiser les dictionnaires
        var awakeMethod = typeof(ResourceNodeManager).GetMethod("Awake",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        awakeMethod?.Invoke(_manager, null);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in _testObjects)
        {
            if (obj != null)
            {
                Object.DestroyImmediate(obj);
            }
        }
        _testObjects.Clear();
    }

    private ResourceSource CreateTestNode(string name, Vector3 position)
    {
        var nodeObj = new GameObject(name);
        nodeObj.transform.position = position;
        var node = nodeObj.AddComponent<ResourceSource>();
        _testObjects.Add(nodeObj);
        return node;
    }

    #region Registration Tests

    [Test]
    public void RegisterNode_AddsNodeToRegistry()
    {
        // Arrange
        var node = CreateTestNode("TestTree", Vector3.zero);

        // Act
        _manager.RegisterNode(node);

        // Assert
        Assert.AreEqual(1, _manager.RegisteredNodeCount);
    }

    [Test]
    public void RegisterNode_DoesNotAddDuplicates()
    {
        // Arrange
        var node = CreateTestNode("TestTree", Vector3.zero);

        // Act
        _manager.RegisterNode(node);
        _manager.RegisterNode(node);

        // Assert
        Assert.AreEqual(1, _manager.RegisteredNodeCount);
    }

    [Test]
    public void RegisterNode_NullDoesNotThrow()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => _manager.RegisterNode(null));
        Assert.AreEqual(0, _manager.RegisteredNodeCount);
    }

    [Test]
    public void UnregisterNode_RemovesNode()
    {
        // Arrange
        var node = CreateTestNode("TestTree", Vector3.zero);
        _manager.RegisterNode(node);

        // Act
        _manager.UnregisterNode(node);

        // Assert
        Assert.AreEqual(0, _manager.RegisteredNodeCount);
    }

    #endregion

    #region Node ID Tests

    [Test]
    public void GetNodeId_ReturnsConsistentId()
    {
        // Arrange
        var node = CreateTestNode("TestTree", new Vector3(10f, 0f, 20f));

        // Act
        string id1 = _manager.GetNodeId(node);
        string id2 = _manager.GetNodeId(node);

        // Assert
        Assert.AreEqual(id1, id2, "Node ID should be consistent across calls");
        Assert.IsFalse(string.IsNullOrEmpty(id1), "Node ID should not be null or empty");
    }

    [Test]
    public void GetNodeId_DifferentPositionsGiveDifferentIds()
    {
        // Arrange
        var node1 = CreateTestNode("Tree", new Vector3(10f, 0f, 20f));
        var node2 = CreateTestNode("Tree", new Vector3(30f, 0f, 40f));

        // Act
        string id1 = _manager.GetNodeId(node1);
        string id2 = _manager.GetNodeId(node2);

        // Assert
        Assert.AreNotEqual(id1, id2);
    }

    #endregion

    #region Query Tests

    [Test]
    public void GetNode_ReturnsRegisteredNode()
    {
        // Arrange
        var node = CreateTestNode("TestTree", Vector3.zero);
        _manager.RegisterNode(node);
        string nodeId = _manager.GetNodeId(node);

        // Act
        var result = _manager.GetNode(nodeId);

        // Assert
        Assert.AreEqual(node, result);
    }

    [Test]
    public void GetNode_ReturnsNullForUnknownId()
    {
        // Act
        var result = _manager.GetNode("unknown_id");

        // Assert
        Assert.IsNull(result);
    }

    [Test]
    public void GetAvailableNodes_ReturnsNonDepletedNodes()
    {
        // Arrange
        var node1 = CreateTestNode("Tree1", Vector3.zero);
        var node2 = CreateTestNode("Tree2", new Vector3(10, 0, 0));
        _manager.RegisterNode(node1);
        _manager.RegisterNode(node2);

        // Act
        var available = _manager.GetAvailableNodes();

        // Assert
        Assert.AreEqual(2, available.Count);
    }

    [Test]
    public void GetNearestNode_ReturnsClosestNode()
    {
        // Arrange
        var near = CreateTestNode("Near", new Vector3(5, 0, 0));
        var far = CreateTestNode("Far", new Vector3(100, 0, 0));
        _manager.RegisterNode(near);
        _manager.RegisterNode(far);

        // Act
        var nearest = _manager.GetNearestNode(Vector3.zero);

        // Assert
        Assert.AreEqual(near, nearest);
    }

    #endregion

    #region Save/Load Tests

    [Test]
    public void GetSaveData_ReturnsAllNodeStates()
    {
        // Arrange
        var node1 = CreateTestNode("Tree1", Vector3.zero);
        var node2 = CreateTestNode("Tree2", new Vector3(10, 0, 0));
        _manager.RegisterNode(node1);
        _manager.RegisterNode(node2);

        // Act
        var saveData = _manager.GetSaveData();

        // Assert
        Assert.IsNotNull(saveData);
        Assert.AreEqual(2, saveData.nodeStates.Count);
    }

    [Test]
    public void LoadSaveData_RestoresNodeStates()
    {
        // Arrange
        var node = CreateTestNode("Tree1", Vector3.zero);
        _manager.RegisterNode(node);

        var saveData = new ResourceNodeSaveData();
        saveData.nodeStates.Add(new ResourceNodeState
        {
            nodeId = _manager.GetNodeId(node),
            currentResources = 5,
            maxResources = 10,
            isDepleted = false,
            respawnTimeRemaining = 0f
        });

        // Act
        _manager.LoadSaveData(saveData);

        // Assert
        Assert.AreEqual(5, node.CurrentResources);
    }

    [Test]
    public void LoadSaveData_RestoresDepletedState()
    {
        // Arrange
        var node = CreateTestNode("Tree1", Vector3.zero);
        _manager.RegisterNode(node);

        var saveData = new ResourceNodeSaveData();
        saveData.nodeStates.Add(new ResourceNodeState
        {
            nodeId = _manager.GetNodeId(node),
            currentResources = 0,
            maxResources = 10,
            isDepleted = true,
            respawnTimeRemaining = 30f
        });

        // Act
        _manager.LoadSaveData(saveData);

        // Assert
        Assert.IsTrue(node.IsDepleted);
    }

    #endregion

    #region Global Actions Tests

    [Test]
    public void RefillAllNodes_ResetsAllNodesToMax()
    {
        // Arrange
        var node1 = CreateTestNode("Tree1", Vector3.zero);
        var node2 = CreateTestNode("Tree2", new Vector3(10, 0, 0));
        _manager.RegisterNode(node1);
        _manager.RegisterNode(node2);

        node1.SetResourceAmount(5);
        node2.SetResourceAmount(3);

        // Act
        _manager.RefillAllNodes();

        // Assert
        Assert.AreEqual(node1.MaxResources, node1.CurrentResources);
        Assert.AreEqual(node2.MaxResources, node2.CurrentResources);
    }

    #endregion
}
