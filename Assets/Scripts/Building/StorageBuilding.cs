using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Composant pour les batiments de stockage.
/// Gere l'inventaire et le stockage de ressources.
/// </summary>
public class StorageBuilding : MonoBehaviour, IResourceContainer
{
    #region Fields

    [Header("Configuration")]
    [SerializeField] private int _slotCount = 20;
    [SerializeField] private int _maxStackSize = 100;

    [Header("Filtrage")]
    [SerializeField] private bool _filterEnabled = false;
    [SerializeField] private ResourceType[] _allowedTypes;

    // Donnees de stockage
    private Dictionary<ResourceType, int> _resources = new Dictionary<ResourceType, int>();

    #endregion

    #region Events

    public event Action<ResourceType, int> OnResourceAdded;
    public event Action<ResourceType, int> OnResourceRemoved;
    public event Action OnStorageChanged;

    #endregion

    #region Properties

    /// <summary>Nombre de slots disponibles.</summary>
    public int SlotCount => _slotCount;

    /// <summary>Taille max d'un stack.</summary>
    public int MaxStackSize => _maxStackSize;

    /// <summary>Nombre de types stockes.</summary>
    public int UsedSlots => _resources.Count;

    /// <summary>Slots libres.</summary>
    public int FreeSlots => _slotCount - _resources.Count;

    /// <summary>Capacite totale.</summary>
    public int TotalCapacity => _slotCount * _maxStackSize;

    /// <summary>Quantite totale stockee.</summary>
    public int TotalStored
    {
        get
        {
            int total = 0;
            foreach (var amount in _resources.Values)
                total += amount;
            return total;
        }
    }

    /// <summary>Pourcentage de remplissage.</summary>
    public float FillPercentage => TotalCapacity > 0 ? (float)TotalStored / TotalCapacity : 0f;

    /// <summary>Filtrage actif?</summary>
    public bool FilterEnabled => _filterEnabled;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _resources = new Dictionary<ResourceType, int>();
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

        // Verifier le filtre
        if (_filterEnabled && _allowedTypes != null && _allowedTypes.Length > 0)
        {
            bool allowed = false;
            foreach (var allowedType in _allowedTypes)
            {
                if (allowedType == type)
                {
                    allowed = true;
                    break;
                }
            }
            if (!allowed) return false;
        }

        // Verifier l'espace
        if (!_resources.ContainsKey(type))
        {
            // Nouveau type, besoin d'un nouveau slot
            if (FreeSlots <= 0) return false;
            _resources[type] = 0;
        }

        // Verifier la capacite du stack
        int current = _resources[type];
        int maxCanAdd = _maxStackSize - current;
        if (amount > maxCanAdd) return false;

        _resources[type] = current + amount;

        OnResourceAdded?.Invoke(type, amount);
        OnStorageChanged?.Invoke();

        return true;
    }

    public bool RemoveResource(ResourceType type, int amount)
    {
        if (amount <= 0) return false;
        if (!HasResource(type, amount)) return false;

        _resources[type] -= amount;

        if (_resources[type] <= 0)
        {
            _resources.Remove(type);
        }

        OnResourceRemoved?.Invoke(type, amount);
        OnStorageChanged?.Invoke();

        return true;
    }

    public int GetResourceCount(ResourceType type)
    {
        return _resources.TryGetValue(type, out int amount) ? amount : 0;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Tente d'ajouter le maximum possible.
    /// </summary>
    /// <returns>Quantite effectivement ajoutee.</returns>
    public int TryAddMax(ResourceType type, int amount)
    {
        if (amount <= 0) return 0;

        // Verifier le filtre
        if (_filterEnabled && _allowedTypes != null && _allowedTypes.Length > 0)
        {
            bool allowed = false;
            foreach (var allowedType in _allowedTypes)
            {
                if (allowedType == type)
                {
                    allowed = true;
                    break;
                }
            }
            if (!allowed) return 0;
        }

        // Creer le slot si necessaire
        if (!_resources.ContainsKey(type))
        {
            if (FreeSlots <= 0) return 0;
            _resources[type] = 0;
        }

        // Ajouter le maximum possible
        int current = _resources[type];
        int canAdd = Mathf.Min(amount, _maxStackSize - current);

        if (canAdd > 0)
        {
            _resources[type] = current + canAdd;
            OnResourceAdded?.Invoke(type, canAdd);
            OnStorageChanged?.Invoke();
        }

        return canAdd;
    }

    /// <summary>
    /// Verifie si un type de ressource est autorise.
    /// </summary>
    public bool IsTypeAllowed(ResourceType type)
    {
        if (!_filterEnabled || _allowedTypes == null || _allowedTypes.Length == 0)
            return true;

        foreach (var allowed in _allowedTypes)
        {
            if (allowed == type) return true;
        }
        return false;
    }

    /// <summary>
    /// Definit le filtre de ressources.
    /// </summary>
    public void SetFilter(bool enabled, ResourceType[] allowedTypes = null)
    {
        _filterEnabled = enabled;
        _allowedTypes = allowedTypes;
    }

    /// <summary>
    /// Retourne la liste des ressources stockees.
    /// </summary>
    public Dictionary<ResourceType, int> GetAllResources()
    {
        return new Dictionary<ResourceType, int>(_resources);
    }

    /// <summary>
    /// Vide completement le stockage.
    /// </summary>
    public void Clear()
    {
        _resources.Clear();
        OnStorageChanged?.Invoke();
    }

    /// <summary>
    /// Calcule l'espace disponible pour un type.
    /// </summary>
    public int GetAvailableSpace(ResourceType type)
    {
        if (!IsTypeAllowed(type)) return 0;

        if (_resources.ContainsKey(type))
        {
            return _maxStackSize - _resources[type];
        }

        // Nouveau type, verifier si un slot est disponible
        return FreeSlots > 0 ? _maxStackSize : 0;
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Configure le stockage.
    /// </summary>
    public void Configure(int slotCount, int maxStackSize)
    {
        _slotCount = slotCount;
        _maxStackSize = maxStackSize;
    }

    #endregion
}
