using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Système d'inventaire grid-based.
/// Gère le stockage, l'ajout, le retrait et l'organisation des objets.
/// </summary>
public class Inventory : MonoBehaviour
{
    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private int _capacity = 20;
    [SerializeField] private bool _autoStack = true;

    #endregion

    #region Private Fields

    private List<ItemInstance> _items = new List<ItemInstance>();

    #endregion

    #region Events

    /// <summary>
    /// Déclenché quand un objet est ajouté.
    /// </summary>
    public event Action<ItemInstance, int> OnItemAdded; // item, slot

    /// <summary>
    /// Déclenché quand un objet est retiré.
    /// </summary>
    public event Action<ItemInstance, int> OnItemRemoved; // item, slot

    /// <summary>
    /// Déclenché quand l'inventaire change.
    /// </summary>
    public event Action OnInventoryChanged;

    /// <summary>
    /// Déclenché quand la capacité change.
    /// </summary>
    public event Action<int> OnCapacityChanged;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        Initialize();
    }

    #endregion

    #region Initialization

    private bool _initialized = false;

    private void Initialize()
    {
        if (_initialized) return;

        // Initialiser avec des slots vides
        _items.Clear();
        for (int i = 0; i < _capacity; i++)
        {
            _items.Add(null);
        }
        _initialized = true;
    }

    /// <summary>
    /// Assure que l'inventaire est initialisé (pour les tests en EditMode).
    /// </summary>
    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }

    #endregion

    #region Public Methods - Add Items

    /// <summary>
    /// Tente d'ajouter un objet à l'inventaire.
    /// </summary>
    /// <param name="data">Données de l'objet</param>
    /// <param name="quantity">Quantité à ajouter</param>
    /// <returns>Quantité qui n'a pas pu être ajoutée</returns>
    public int TryAddItem(ItemData data, int quantity = 1)
    {
        EnsureInitialized();
        if (data == null || quantity <= 0) return quantity;

        int remaining = quantity;

        // D'abord, essayer de stack sur des piles existantes
        if (_autoStack && data.isStackable)
        {
            for (int i = 0; i < _items.Count && remaining > 0; i++)
            {
                var item = _items[i];
                if (item != null && item.Data == data && !item.IsFull)
                {
                    int overflow = item.Add(remaining);
                    remaining = overflow;
                    OnInventoryChanged?.Invoke();
                }
            }
        }

        // Ensuite, créer de nouvelles piles dans les slots vides
        while (remaining > 0)
        {
            int emptySlot = FindEmptySlot();
            if (emptySlot == -1) break;

            int toAdd = Math.Min(remaining, data.maxStackSize);
            var newItem = new ItemInstance(data, toAdd);
            _items[emptySlot] = newItem;
            remaining -= toAdd;

            OnItemAdded?.Invoke(newItem, emptySlot);
            OnInventoryChanged?.Invoke();
        }

        return remaining;
    }

    /// <summary>
    /// Ajoute une instance d'objet existante.
    /// </summary>
    public bool TryAddItemInstance(ItemInstance item, int preferredSlot = -1)
    {
        EnsureInitialized();
        if (item == null || item.IsEmpty) return false;

        // Essayer le slot préféré
        if (preferredSlot >= 0 && preferredSlot < _items.Count && _items[preferredSlot] == null)
        {
            _items[preferredSlot] = item;
            OnItemAdded?.Invoke(item, preferredSlot);
            OnInventoryChanged?.Invoke();
            return true;
        }

        // Sinon, trouver un slot vide
        int emptySlot = FindEmptySlot();
        if (emptySlot == -1) return false;

        _items[emptySlot] = item;
        OnItemAdded?.Invoke(item, emptySlot);
        OnInventoryChanged?.Invoke();
        return true;
    }

    #endregion

    #region Public Methods - Remove Items

    /// <summary>
    /// Retire une quantité d'un type d'objet.
    /// </summary>
    public int RemoveItem(ItemData data, int quantity = 1)
    {
        EnsureInitialized();
        if (data == null || quantity <= 0) return 0;

        int removed = 0;

        for (int i = _items.Count - 1; i >= 0 && removed < quantity; i--)
        {
            var item = _items[i];
            if (item != null && item.Data == data)
            {
                int toRemove = Math.Min(quantity - removed, item.Quantity);
                item.Remove(toRemove);
                removed += toRemove;

                if (item.IsEmpty)
                {
                    OnItemRemoved?.Invoke(item, i);
                    _items[i] = null;
                }

                OnInventoryChanged?.Invoke();
            }
        }

        return removed;
    }

    /// <summary>
    /// Retire l'objet à un slot spécifique.
    /// </summary>
    public ItemInstance RemoveItemAt(int slot, int quantity = -1)
    {
        EnsureInitialized();
        if (slot < 0 || slot >= _items.Count) return null;

        var item = _items[slot];
        if (item == null) return null;

        if (quantity < 0 || quantity >= item.Quantity)
        {
            // Retirer tout
            _items[slot] = null;
            OnItemRemoved?.Invoke(item, slot);
            OnInventoryChanged?.Invoke();
            return item;
        }
        else
        {
            // Retirer partiellement
            var split = item.Split(quantity);
            OnInventoryChanged?.Invoke();
            return split;
        }
    }

    /// <summary>
    /// Vide complètement un slot.
    /// </summary>
    public ItemInstance ClearSlot(int slot)
    {
        return RemoveItemAt(slot);
    }

    #endregion

    #region Public Methods - Move Items

    /// <summary>
    /// Déplace un objet d'un slot à un autre.
    /// </summary>
    public bool MoveItem(int fromSlot, int toSlot)
    {
        EnsureInitialized();
        if (!IsValidSlot(fromSlot) || !IsValidSlot(toSlot)) return false;
        if (fromSlot == toSlot) return true;

        var fromItem = _items[fromSlot];
        var toItem = _items[toSlot];

        if (fromItem == null) return false;

        // Si le slot destination est vide, simple déplacement
        if (toItem == null)
        {
            _items[toSlot] = fromItem;
            _items[fromSlot] = null;
            OnInventoryChanged?.Invoke();
            return true;
        }

        // Si même type d'item et stackable, fusionner
        if (fromItem.Data == toItem.Data && fromItem.Data.isStackable)
        {
            if (toItem.TryMerge(fromItem))
            {
                _items[fromSlot] = null;
            }
            OnInventoryChanged?.Invoke();
            return true;
        }

        // Sinon, échanger
        _items[fromSlot] = toItem;
        _items[toSlot] = fromItem;
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Trie l'inventaire par catégorie et rareté.
    /// </summary>
    public void Sort()
    {
        EnsureInitialized();
        var sorted = _items
            .Where(i => i != null)
            .OrderBy(i => i.Data.category)
            .ThenByDescending(i => i.Data.rarity)
            .ThenBy(i => i.Data.displayName)
            .ToList();

        // Réorganiser l'inventaire
        for (int i = 0; i < _items.Count; i++)
        {
            _items[i] = i < sorted.Count ? sorted[i] : null;
        }

        OnInventoryChanged?.Invoke();
    }

    #endregion

    #region Public Methods - Query

    /// <summary>
    /// Obtient l'objet à un slot donné.
    /// </summary>
    public ItemInstance GetItemAt(int slot)
    {
        EnsureInitialized();
        return IsValidSlot(slot) ? _items[slot] : null;
    }

    /// <summary>
    /// Indexer pour accéder aux slots.
    /// </summary>
    public ItemInstance this[int slot] => GetItemAt(slot);

    /// <summary>
    /// Vérifie si l'inventaire contient un type d'objet.
    /// </summary>
    public bool HasItem(ItemData data, int quantity = 1)
    {
        EnsureInitialized();
        return CountItem(data) >= quantity;
    }

    /// <summary>
    /// Compte le nombre total d'un type d'objet.
    /// </summary>
    public int CountItem(ItemData data)
    {
        EnsureInitialized();
        if (data == null) return 0;

        return _items
            .Where(i => i != null && i.Data == data)
            .Sum(i => i.Quantity);
    }

    /// <summary>
    /// Trouve le premier slot contenant un type d'objet.
    /// </summary>
    public int FindItem(ItemData data)
    {
        EnsureInitialized();
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i] != null && _items[i].Data == data)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Trouve tous les slots contenant un type d'objet.
    /// </summary>
    public List<int> FindAllItems(ItemData data)
    {
        EnsureInitialized();
        var slots = new List<int>();
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i] != null && _items[i].Data == data)
            {
                slots.Add(i);
            }
        }
        return slots;
    }

    /// <summary>
    /// Trouve le premier slot vide.
    /// </summary>
    public int FindEmptySlot()
    {
        EnsureInitialized();
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i] == null)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Retourne tous les objets de l'inventaire.
    /// </summary>
    public IReadOnlyList<ItemInstance> GetAllItems()
    {
        EnsureInitialized();
        return _items.AsReadOnly();
    }

    /// <summary>
    /// Retourne tous les objets non-null.
    /// </summary>
    public IEnumerable<ItemInstance> GetItems()
    {
        EnsureInitialized();
        return _items.Where(i => i != null);
    }

    /// <summary>
    /// Filtre les objets par catégorie.
    /// </summary>
    public IEnumerable<ItemInstance> GetItemsByCategory(ItemCategory category)
    {
        EnsureInitialized();
        return _items.Where(i => i != null && i.Data.category == category);
    }

    #endregion

    #region Public Methods - Capacity

    /// <summary>
    /// Capacité actuelle de l'inventaire.
    /// </summary>
    public int Capacity
    {
        get => _capacity;
        set
        {
            EnsureInitialized();
            if (value == _capacity || value < 1) return;

            if (value > _capacity)
            {
                // Agrandir
                while (_items.Count < value)
                {
                    _items.Add(null);
                }
            }
            else
            {
                // Réduire (ne supprime pas les objets existants)
                // Les objets au-delà de la nouvelle capacité restent
            }

            _capacity = value;
            OnCapacityChanged?.Invoke(_capacity);
            OnInventoryChanged?.Invoke();
        }
    }

    /// <summary>
    /// Nombre de slots utilisés.
    /// </summary>
    public int UsedSlots
    {
        get
        {
            EnsureInitialized();
            return _items.Count(i => i != null);
        }
    }

    /// <summary>
    /// Nombre de slots disponibles.
    /// </summary>
    public int FreeSlots
    {
        get
        {
            EnsureInitialized();
            return _capacity - UsedSlots;
        }
    }

    /// <summary>
    /// L'inventaire est-il plein?
    /// </summary>
    public bool IsFull
    {
        get
        {
            EnsureInitialized();
            return FreeSlots == 0;
        }
    }

    /// <summary>
    /// L'inventaire est-il vide?
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            EnsureInitialized();
            return UsedSlots == 0;
        }
    }

    #endregion

    #region Private Methods

    private bool IsValidSlot(int slot)
    {
        return slot >= 0 && slot < _items.Count;
    }

    #endregion
}
