using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire global des ressources du joueur.
/// </summary>
public class ResourceManager : MonoBehaviour, IResourceContainer
{
    #region Singleton

    public static ResourceManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // DontDestroyOnLoad uniquement en mode play
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }

        _resources = new Dictionary<ResourceType, int>();
    }

    #endregion

    #region Fields

    [Header("Configuration")]
    [SerializeField] private int _defaultMaxStack = 9999;
    [SerializeField] private ResourceData[] _resourceDatabase;

    // Stockage
    private Dictionary<ResourceType, int> _resources;
    private Dictionary<ResourceType, ResourceData> _dataLookup;

    #endregion

    #region Events

    public event Action<ResourceType, int, int> OnResourceChanged;
    public event Action<ResourceType, int> OnResourceGained;
    public event Action<ResourceType, int> OnResourceSpent;

    #endregion

    #region Properties

    /// <summary>Nombre de types de ressources possedes.</summary>
    public int UniqueResourceCount => _resources.Count;

    /// <summary>Quantite totale de ressources.</summary>
    public int TotalResourceCount
    {
        get
        {
            int total = 0;
            foreach (var amount in _resources.Values)
                total += amount;
            return total;
        }
    }

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        BuildDataLookup();
    }

    #endregion

    #region IResourceContainer Implementation

    public bool HasResource(ResourceType type, int amount)
    {
        return _resources.TryGetValue(type, out int current) && current >= amount;
    }

    public bool AddResource(ResourceType type, int amount)
    {
        if (amount <= 0) return false;

        int oldAmount = GetResourceCount(type);
        int newAmount = oldAmount + amount;

        // Verifier le max stack
        int maxStack = GetMaxStack(type);
        if (newAmount > maxStack)
        {
            newAmount = maxStack;
            amount = newAmount - oldAmount;
            if (amount <= 0) return false;
        }

        _resources[type] = newAmount;

        OnResourceGained?.Invoke(type, amount);
        OnResourceChanged?.Invoke(type, oldAmount, newAmount);

        return true;
    }

    public bool RemoveResource(ResourceType type, int amount)
    {
        if (amount <= 0) return false;
        if (!HasResource(type, amount)) return false;

        int oldAmount = _resources[type];
        int newAmount = oldAmount - amount;

        if (newAmount <= 0)
        {
            _resources.Remove(type);
            newAmount = 0;
        }
        else
        {
            _resources[type] = newAmount;
        }

        OnResourceSpent?.Invoke(type, amount);
        OnResourceChanged?.Invoke(type, oldAmount, newAmount);

        return true;
    }

    public int GetResourceCount(ResourceType type)
    {
        return _resources.TryGetValue(type, out int amount) ? amount : 0;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Ajoute plusieurs ressources a la fois.
    /// </summary>
    public void AddResources(ResourceCost[] resources)
    {
        if (resources == null) return;

        foreach (var resource in resources)
        {
            AddResource(resource.resourceType, resource.amount);
        }
    }

    /// <summary>
    /// Verifie si on a toutes les ressources.
    /// </summary>
    public bool HasResources(ResourceCost[] costs)
    {
        if (costs == null) return true;

        foreach (var cost in costs)
        {
            if (!HasResource(cost.resourceType, cost.amount))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Retire plusieurs ressources a la fois.
    /// </summary>
    public bool RemoveResources(ResourceCost[] costs)
    {
        if (!HasResources(costs)) return false;

        foreach (var cost in costs)
        {
            RemoveResource(cost.resourceType, cost.amount);
        }
        return true;
    }

    /// <summary>
    /// Obtient toutes les ressources possedees.
    /// </summary>
    public Dictionary<ResourceType, int> GetAllResources()
    {
        return new Dictionary<ResourceType, int>(_resources);
    }

    /// <summary>
    /// Obtient les ressources d'une categorie.
    /// </summary>
    public Dictionary<ResourceType, int> GetResourcesByCategory(ResourceCategory category)
    {
        var result = new Dictionary<ResourceType, int>();

        foreach (var kvp in _resources)
        {
            var data = GetResourceData(kvp.Key);
            if (data != null && data.category == category)
            {
                result[kvp.Key] = kvp.Value;
            }
        }

        return result;
    }

    /// <summary>
    /// Obtient les donnees d'une ressource.
    /// </summary>
    public ResourceData GetResourceData(ResourceType type)
    {
        if (_dataLookup == null) BuildDataLookup();

        return _dataLookup.TryGetValue(type, out var data) ? data : null;
    }

    /// <summary>
    /// Obtient le stack maximum pour un type.
    /// </summary>
    public int GetMaxStack(ResourceType type)
    {
        var data = GetResourceData(type);
        return data != null ? data.maxStackSize : _defaultMaxStack;
    }

    /// <summary>
    /// Calcule la valeur totale des ressources.
    /// </summary>
    public int GetTotalValue()
    {
        int total = 0;

        foreach (var kvp in _resources)
        {
            var data = GetResourceData(kvp.Key);
            if (data != null)
            {
                total += data.GetValue(kvp.Value);
            }
            else
            {
                total += kvp.Value;
            }
        }

        return total;
    }

    /// <summary>
    /// Efface toutes les ressources.
    /// </summary>
    public void ClearAll()
    {
        var keys = new List<ResourceType>(_resources.Keys);

        foreach (var type in keys)
        {
            int oldAmount = _resources[type];
            _resources.Remove(type);
            OnResourceChanged?.Invoke(type, oldAmount, 0);
        }
    }

    #endregion

    #region Private Methods

    private void BuildDataLookup()
    {
        _dataLookup = new Dictionary<ResourceType, ResourceData>();

        if (_resourceDatabase != null)
        {
            foreach (var data in _resourceDatabase)
            {
                if (data != null)
                {
                    _dataLookup[data.resourceType] = data;
                }
            }
        }
    }

    #endregion
}
