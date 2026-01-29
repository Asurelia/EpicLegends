using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire du reseau logistique (stockages, convoyeurs, priorites).
/// </summary>
public class LogisticsNetwork : MonoBehaviour
{
    #region Singleton

    public static LogisticsNetwork Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _storages = new List<StorageNode>();
        _requests = new List<ResourceRequest>();
    }

    #endregion

    #region Fields

    [Header("Configuration")]
    [SerializeField] private float _updateInterval = 1f;
    [SerializeField] private int _maxRequestsPerUpdate = 10;

    // Stockages enregistres
    private List<StorageNode> _storages;
    private List<ResourceRequest> _requests;
    private float _updateTimer = 0f;

    #endregion

    #region Events

    public event Action<StorageBuilding> OnStorageRegistered;
    public event Action<StorageBuilding> OnStorageUnregistered;
    public event Action<ResourceRequest> OnRequestFulfilled;

    #endregion

    #region Properties

    /// <summary>Nombre de stockages.</summary>
    public int StorageCount => _storages.Count;

    /// <summary>Requetes en attente.</summary>
    public int PendingRequests => _requests.Count;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        _updateTimer += Time.deltaTime;

        if (_updateTimer >= _updateInterval)
        {
            _updateTimer = 0f;
            ProcessRequests();
        }
    }

    #endregion

    #region Public Methods - Stockages

    /// <summary>
    /// Enregistre un stockage dans le reseau.
    /// </summary>
    public void RegisterStorage(StorageBuilding storage, StoragePriority priority = StoragePriority.Normal)
    {
        if (storage == null) return;

        // Verifier si deja enregistre
        foreach (var node in _storages)
        {
            if (node.storage == storage) return;
        }

        var newNode = new StorageNode
        {
            storage = storage,
            priority = priority,
            isInput = true,
            isOutput = true
        };

        _storages.Add(newNode);
        SortStorages();

        OnStorageRegistered?.Invoke(storage);
    }

    /// <summary>
    /// Retire un stockage du reseau.
    /// </summary>
    public void UnregisterStorage(StorageBuilding storage)
    {
        for (int i = _storages.Count - 1; i >= 0; i--)
        {
            if (_storages[i].storage == storage)
            {
                _storages.RemoveAt(i);
                OnStorageUnregistered?.Invoke(storage);
                break;
            }
        }
    }

    /// <summary>
    /// Change la priorite d'un stockage.
    /// </summary>
    public void SetStoragePriority(StorageBuilding storage, StoragePriority priority)
    {
        for (int i = 0; i < _storages.Count; i++)
        {
            if (_storages[i].storage == storage)
            {
                var node = _storages[i];
                node.priority = priority;
                _storages[i] = node;
                SortStorages();
                break;
            }
        }
    }

    /// <summary>
    /// Configure un stockage comme entree/sortie.
    /// </summary>
    public void SetStorageMode(StorageBuilding storage, bool isInput, bool isOutput)
    {
        for (int i = 0; i < _storages.Count; i++)
        {
            if (_storages[i].storage == storage)
            {
                var node = _storages[i];
                node.isInput = isInput;
                node.isOutput = isOutput;
                _storages[i] = node;
                break;
            }
        }
    }

    #endregion

    #region Public Methods - Requetes

    /// <summary>
    /// Demande une ressource au reseau.
    /// </summary>
    public bool RequestResource(ResourceType type, int amount, StorageBuilding destination)
    {
        if (destination == null || amount <= 0) return false;

        var request = new ResourceRequest
        {
            resourceType = type,
            amount = amount,
            destination = destination,
            priority = RequestPriority.Normal,
            createdTime = Time.time
        };

        _requests.Add(request);
        return true;
    }

    /// <summary>
    /// Demande une ressource avec priorite.
    /// </summary>
    public bool RequestResource(ResourceType type, int amount, StorageBuilding destination, RequestPriority priority)
    {
        if (destination == null || amount <= 0) return false;

        var request = new ResourceRequest
        {
            resourceType = type,
            amount = amount,
            destination = destination,
            priority = priority,
            createdTime = Time.time
        };

        _requests.Add(request);
        SortRequests();
        return true;
    }

    /// <summary>
    /// Annule toutes les requetes vers une destination.
    /// </summary>
    public void CancelRequests(StorageBuilding destination)
    {
        _requests.RemoveAll(r => r.destination == destination);
    }

    #endregion

    #region Public Methods - Recherche

    /// <summary>
    /// Trouve un stockage contenant une ressource.
    /// </summary>
    public StorageBuilding FindStorageWith(ResourceType type, int minAmount = 1)
    {
        foreach (var node in _storages)
        {
            if (!node.isOutput) continue;
            if (node.storage == null) continue;

            if (node.storage.HasResource(type, minAmount))
            {
                return node.storage;
            }
        }
        return null;
    }

    /// <summary>
    /// Trouve un stockage avec de l'espace.
    /// </summary>
    public StorageBuilding FindStorageWithSpace(ResourceType type, int amount = 1)
    {
        foreach (var node in _storages)
        {
            if (!node.isInput) continue;
            if (node.storage == null) continue;

            if (node.storage.GetAvailableSpace(type) >= amount)
            {
                return node.storage;
            }
        }
        return null;
    }

    /// <summary>
    /// Calcule le total d'une ressource dans le reseau.
    /// </summary>
    public int GetTotalResourceCount(ResourceType type)
    {
        int total = 0;

        foreach (var node in _storages)
        {
            if (node.storage != null)
            {
                total += node.storage.GetResourceCount(type);
            }
        }

        return total;
    }

    /// <summary>
    /// Calcule l'espace total disponible.
    /// </summary>
    public int GetTotalAvailableSpace(ResourceType type)
    {
        int total = 0;

        foreach (var node in _storages)
        {
            if (node.storage != null && node.isInput)
            {
                total += node.storage.GetAvailableSpace(type);
            }
        }

        return total;
    }

    #endregion

    #region Private Methods

    private void ProcessRequests()
    {
        int processed = 0;

        for (int i = _requests.Count - 1; i >= 0 && processed < _maxRequestsPerUpdate; i--)
        {
            var request = _requests[i];

            if (request.destination == null)
            {
                _requests.RemoveAt(i);
                continue;
            }

            // Chercher une source
            var source = FindStorageWith(request.resourceType, request.amount);

            if (source != null && source != request.destination)
            {
                // Transferer
                if (source.RemoveResource(request.resourceType, request.amount))
                {
                    if (request.destination.AddResource(request.resourceType, request.amount))
                    {
                        _requests.RemoveAt(i);
                        OnRequestFulfilled?.Invoke(request);
                        processed++;
                    }
                    else
                    {
                        // Remettre si la destination n'accepte pas
                        source.AddResource(request.resourceType, request.amount);
                    }
                }
            }
        }
    }

    private void SortStorages()
    {
        _storages.Sort((a, b) => ((int)b.priority).CompareTo((int)a.priority));
    }

    private void SortRequests()
    {
        _requests.Sort((a, b) =>
        {
            int priorityCompare = ((int)b.priority).CompareTo((int)a.priority);
            if (priorityCompare != 0) return priorityCompare;
            return a.createdTime.CompareTo(b.createdTime);
        });
    }

    #endregion
}

/// <summary>
/// Noeud de stockage dans le reseau.
/// </summary>
public struct StorageNode
{
    public StorageBuilding storage;
    public StoragePriority priority;
    public bool isInput;
    public bool isOutput;
}

/// <summary>
/// Priorite de stockage.
/// </summary>
public enum StoragePriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Requete de ressource.
/// </summary>
public struct ResourceRequest
{
    public ResourceType resourceType;
    public int amount;
    public StorageBuilding destination;
    public RequestPriority priority;
    public float createdTime;
}

/// <summary>
/// Priorite de requete.
/// </summary>
public enum RequestPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}
