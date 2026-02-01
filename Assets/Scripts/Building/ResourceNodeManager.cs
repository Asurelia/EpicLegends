using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire centralisé des nodes de ressources.
/// Gère l'enregistrement, le respawn global et la sauvegarde.
/// </summary>
public class ResourceNodeManager : MonoBehaviour
{
    #region Singleton

    private static ResourceNodeManager _instance;
    public static ResourceNodeManager Instance
    {
        get => _instance;
        private set => _instance = value;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _registeredNodes = new Dictionary<string, ResourceSource>();
        _nodeStates = new Dictionary<string, ResourceNodeState>();
    }

    #endregion

    #region Events

    /// <summary>Déclenché quand une node est collectée.</summary>
    public event Action<string, ResourceType, int> OnNodeGathered;

    /// <summary>Déclenché quand une node est épuisée.</summary>
    public event Action<string> OnNodeDepleted;

    /// <summary>Déclenché quand une node respawn.</summary>
    public event Action<string> OnNodeRespawned;

    #endregion

    #region Fields

    [Header("Configuration")]
    [SerializeField] private bool _trackNodeStates = true;
    [SerializeField] private float _globalRespawnMultiplier = 1f;

    private Dictionary<string, ResourceSource> _registeredNodes;
    private Dictionary<string, ResourceNodeState> _nodeStates;

    #endregion

    #region Properties

    /// <summary>Nombre de nodes enregistrées.</summary>
    public int RegisteredNodeCount => _registeredNodes?.Count ?? 0;

    /// <summary>Multiplicateur global de respawn.</summary>
    public float GlobalRespawnMultiplier
    {
        get => _globalRespawnMultiplier;
        set => _globalRespawnMultiplier = Mathf.Max(0.1f, value);
    }

    #endregion

    #region Public Methods - Registration

    /// <summary>
    /// Enregistre une node de ressource.
    /// </summary>
    public void RegisterNode(ResourceSource node)
    {
        if (node == null) return;

        string nodeId = GetNodeId(node);
        if (_registeredNodes.ContainsKey(nodeId)) return;

        _registeredNodes[nodeId] = node;

        // Abonnement aux événements
        node.OnResourceGathered += (type, amount) => HandleNodeGathered(nodeId, type, amount);
        node.OnDepleted += () => HandleNodeDepleted(nodeId);
        node.OnRespawned += () => HandleNodeRespawned(nodeId);

        // Restaurer l'état si sauvegardé
        if (_nodeStates.TryGetValue(nodeId, out var state))
        {
            RestoreNodeState(node, state);
        }
    }

    /// <summary>
    /// Retire une node du registre.
    /// </summary>
    public void UnregisterNode(ResourceSource node)
    {
        if (node == null) return;

        string nodeId = GetNodeId(node);
        if (_registeredNodes.ContainsKey(nodeId))
        {
            // Sauvegarder l'état avant de retirer
            if (_trackNodeStates)
            {
                _nodeStates[nodeId] = CaptureNodeState(node);
            }
            _registeredNodes.Remove(nodeId);
        }
    }

    /// <summary>
    /// Génère un ID unique pour une node basé sur sa position.
    /// </summary>
    public string GetNodeId(ResourceSource node)
    {
        if (node == null) return string.Empty;

        // Utiliser le nom + position pour un ID unique
        var pos = node.transform.position;
        return $"{node.name}_{pos.x:F1}_{pos.y:F1}_{pos.z:F1}";
    }

    #endregion

    #region Public Methods - Query

    /// <summary>
    /// Obtient une node par son ID.
    /// </summary>
    public ResourceSource GetNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return null;
        _registeredNodes.TryGetValue(nodeId, out var node);
        return node;
    }

    /// <summary>
    /// Obtient toutes les nodes d'un type donné.
    /// </summary>
    public List<ResourceSource> GetNodesByType(ResourceType type)
    {
        var result = new List<ResourceSource>();
        foreach (var node in _registeredNodes.Values)
        {
            if (node != null && node.ResourceType == type)
            {
                result.Add(node);
            }
        }
        return result;
    }

    /// <summary>
    /// Obtient toutes les nodes épuisées.
    /// </summary>
    public List<ResourceSource> GetDepletedNodes()
    {
        var result = new List<ResourceSource>();
        foreach (var node in _registeredNodes.Values)
        {
            if (node != null && node.IsDepleted)
            {
                result.Add(node);
            }
        }
        return result;
    }

    /// <summary>
    /// Obtient toutes les nodes disponibles (non épuisées).
    /// </summary>
    public List<ResourceSource> GetAvailableNodes()
    {
        var result = new List<ResourceSource>();
        foreach (var node in _registeredNodes.Values)
        {
            if (node != null && !node.IsDepleted)
            {
                result.Add(node);
            }
        }
        return result;
    }

    /// <summary>
    /// Trouve la node la plus proche d'une position.
    /// </summary>
    public ResourceSource GetNearestNode(Vector3 position, ResourceType? typeFilter = null, bool availableOnly = true)
    {
        ResourceSource nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var node in _registeredNodes.Values)
        {
            if (node == null) continue;
            if (availableOnly && node.IsDepleted) continue;
            if (typeFilter.HasValue && node.ResourceType != typeFilter.Value) continue;

            float dist = Vector3.Distance(position, node.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = node;
            }
        }
        return nearest;
    }

    #endregion

    #region Public Methods - Global Actions

    /// <summary>
    /// Force le respawn de toutes les nodes épuisées.
    /// </summary>
    public void RespawnAllNodes()
    {
        foreach (var node in _registeredNodes.Values)
        {
            if (node != null && node.IsDepleted && node.CanRespawn)
            {
                node.ForceRespawn();
            }
        }
    }

    /// <summary>
    /// Épuise toutes les nodes (pour tests/debug).
    /// </summary>
    public void DepleteAllNodes()
    {
        foreach (var node in _registeredNodes.Values)
        {
            if (node != null && !node.IsDepleted)
            {
                node.Deplete();
            }
        }
    }

    /// <summary>
    /// Remet toutes les nodes à leur quantité maximale.
    /// </summary>
    public void RefillAllNodes()
    {
        foreach (var node in _registeredNodes.Values)
        {
            if (node != null)
            {
                node.SetResourceAmount(node.MaxResources);
            }
        }
    }

    #endregion

    #region Public Methods - Save/Load

    /// <summary>
    /// Obtient les données de sauvegarde.
    /// </summary>
    public ResourceNodeSaveData GetSaveData()
    {
        var data = new ResourceNodeSaveData();

        foreach (var kvp in _registeredNodes)
        {
            if (kvp.Value != null)
            {
                data.nodeStates.Add(CaptureNodeState(kvp.Value, kvp.Key));
            }
        }

        // Inclure aussi les états des nodes non-présentes (pour persistence entre scènes)
        foreach (var kvp in _nodeStates)
        {
            if (!_registeredNodes.ContainsKey(kvp.Key))
            {
                data.nodeStates.Add(kvp.Value);
            }
        }

        return data;
    }

    /// <summary>
    /// Charge les données de sauvegarde.
    /// </summary>
    public void LoadSaveData(ResourceNodeSaveData data)
    {
        if (data == null) return;

        _nodeStates.Clear();

        foreach (var state in data.nodeStates)
        {
            _nodeStates[state.nodeId] = state;

            // Appliquer aux nodes déjà enregistrées
            if (_registeredNodes.TryGetValue(state.nodeId, out var node))
            {
                RestoreNodeState(node, state);
            }
        }
    }

    #endregion

    #region Private Methods

    private void HandleNodeGathered(string nodeId, ResourceType type, int amount)
    {
        OnNodeGathered?.Invoke(nodeId, type, amount);

        // Notifier l'AchievementManager
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnResourceGathered(type, amount);
        }
    }

    private void HandleNodeDepleted(string nodeId)
    {
        if (_trackNodeStates && _registeredNodes.TryGetValue(nodeId, out var node))
        {
            _nodeStates[nodeId] = CaptureNodeState(node, nodeId);
        }

        OnNodeDepleted?.Invoke(nodeId);
    }

    private void HandleNodeRespawned(string nodeId)
    {
        // Retirer l'état épuisé
        if (_nodeStates.ContainsKey(nodeId))
        {
            var state = _nodeStates[nodeId];
            state.isDepleted = false;
            state.currentResources = state.maxResources;
        }

        OnNodeRespawned?.Invoke(nodeId);
    }

    private ResourceNodeState CaptureNodeState(ResourceSource node, string nodeId = null)
    {
        return new ResourceNodeState
        {
            nodeId = nodeId ?? GetNodeId(node),
            currentResources = node.CurrentResources,
            maxResources = node.MaxResources,
            isDepleted = node.IsDepleted,
            respawnTimeRemaining = node.RespawnTimeRemaining
        };
    }

    private void RestoreNodeState(ResourceSource node, ResourceNodeState state)
    {
        if (state.isDepleted)
        {
            node.Deplete();
            // Note: Le timer de respawn reprendra depuis le début
            // Pour une persistence exacte, il faudrait exposer un SetRespawnTimer
        }
        else
        {
            node.SetResourceAmount(state.currentResources);
        }
    }

    #endregion
}

/// <summary>
/// État d'une node de ressource pour la sauvegarde.
/// </summary>
[Serializable]
public class ResourceNodeState
{
    public string nodeId;
    public int currentResources;
    public int maxResources;
    public bool isDepleted;
    public float respawnTimeRemaining;
}

/// <summary>
/// Données de sauvegarde des nodes de ressources.
/// </summary>
[Serializable]
public class ResourceNodeSaveData
{
    public List<ResourceNodeState> nodeStates = new List<ResourceNodeState>();
}
