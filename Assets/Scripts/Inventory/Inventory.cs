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

        // Notify achievements if items were added
        int added = quantity - remaining;
        if (added > 0 && AchievementManager.Instance != null)
        {
            for (int i = 0; i < added; i++)
            {
                AchievementManager.Instance.OnItemCollected();
            }
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

        // MAJOR FIX: Replace LINQ with manual loop to avoid GC allocation
        int count = 0;
        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item != null && item.Data == data)
            {
                count += item.Quantity;
            }
        }
        return count;
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
            // MAJOR FIX: Replace LINQ with manual loop to avoid GC allocation
            int count = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] != null) count++;
            }
            return count;
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

    #region Public Methods - Currency

    private int _gold = 0;

    /// <summary>
    /// Or du joueur.
    /// </summary>
    public int Gold
    {
        get => _gold;
        set
        {
            if (value < 0) value = 0;
            _gold = value;
            OnInventoryChanged?.Invoke();
        }
    }

    /// <summary>
    /// Ajoute de l'or.
    /// </summary>
    public void AddGold(int amount)
    {
        if (amount > 0)
        {
            Gold += amount;

            // Notify achievements
            if (AchievementManager.Instance != null)
            {
                AchievementManager.Instance.OnGoldEarned(amount);
            }
        }
    }

    /// <summary>
    /// Retire de l'or.
    /// </summary>
    /// <returns>True si l'or a pu etre retire.</returns>
    public bool RemoveGold(int amount)
    {
        if (amount <= 0) return true;
        if (_gold < amount) return false;

        Gold -= amount;
        return true;
    }

    #endregion

    #region Public Methods - Convenience

    /// <summary>
    /// Ajoute un item a l'inventaire (version simplifiee).
    /// </summary>
    /// <returns>True si l'item a ete ajoute completement</returns>
    public bool AddItem(ItemData data, int quantity = 1)
    {
        return TryAddItem(data, quantity) == 0;
    }

    /// <summary>
    /// Compte le nombre d'items par ID.
    /// </summary>
    public int GetItemCount(string itemId)
    {
        EnsureInitialized();
        if (string.IsNullOrEmpty(itemId)) return 0;

        int count = 0;
        foreach (var item in _items)
        {
            if (item != null && item.Data != null && item.Data.itemId == itemId)
            {
                count += item.Quantity;
            }
        }
        return count;
    }

    /// <summary>
    /// Vide completement l'inventaire.
    /// </summary>
    public void Clear()
    {
        EnsureInitialized();

        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i] != null)
            {
                OnItemRemoved?.Invoke(_items[i], i);
                _items[i] = null;
            }
        }

        _gold = 0;
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Retire des items par leur ID.
    /// </summary>
    /// <returns>True si la quantite demandee a ete retiree</returns>
    public bool RemoveItemById(string itemId, int quantity = 1)
    {
        EnsureInitialized();
        if (string.IsNullOrEmpty(itemId) || quantity <= 0) return false;

        int remaining = quantity;

        for (int i = 0; i < _items.Count && remaining > 0; i++)
        {
            var item = _items[i];
            if (item == null || item.Data == null) continue;
            if (item.Data.itemId != itemId) continue;

            if (item.Quantity <= remaining)
            {
                remaining -= item.Quantity;
                _items[i] = null;
                OnItemRemoved?.Invoke(item, i);
            }
            else
            {
                item.Remove(remaining);
                remaining = 0;
            }
        }

        if (remaining < quantity)
        {
            OnInventoryChanged?.Invoke();
        }

        return remaining == 0;
    }

    #endregion

    #region Save/Load

    /// <summary>
    /// Definit le montant d'or directement.
    /// </summary>
    public void SetGold(int amount)
    {
        _gold = Mathf.Max(0, amount);
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Charge les donnees de sauvegarde.
    /// </summary>
    public void LoadSaveData(InventorySaveData data, ItemData[] allItems)
    {
        if (data == null || allItems == null) return;

        EnsureInitialized();
        Clear();

        // Restaurer l'or
        _gold = data.gold;

        // Restaurer les items
        foreach (var itemSave in data.items)
        {
            if (string.IsNullOrEmpty(itemSave.itemId)) continue;

            var itemData = System.Array.Find(allItems, i => i.itemId == itemSave.itemId);
            if (itemData == null)
            {
                Debug.LogWarning($"[Inventory] Item not found in database: {itemSave.itemId}");
                continue;
            }

            // Creer l'instance
            var instance = new ItemInstance(itemData, itemSave.quantity);

            // Placer dans le slot specifique si possible
            if (itemSave.slotIndex >= 0 && itemSave.slotIndex < _items.Count && _items[itemSave.slotIndex] == null)
            {
                _items[itemSave.slotIndex] = instance;
                OnItemAdded?.Invoke(instance, itemSave.slotIndex);
            }
            else
            {
                // Trouver un slot libre
                TryAddItemInstance(instance);
            }
        }

        OnInventoryChanged?.Invoke();
        Debug.Log($"[Inventory] Loaded {data.items.Count} items, {data.gold} gold");
    }

    /// <summary>
    /// Obtient les donnees de sauvegarde.
    /// </summary>
    public InventorySaveData GetSaveData()
    {
        EnsureInitialized();

        var data = new InventorySaveData
        {
            capacity = _capacity,
            gold = _gold,
            items = new System.Collections.Generic.List<ItemSaveData>()
        };

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item != null && item.Data != null)
            {
                data.items.Add(new ItemSaveData
                {
                    itemId = item.Data.itemId,
                    quantity = item.Quantity,
                    slotIndex = i,
                    instanceId = item.InstanceId
                });
            }
        }

        return data;
    }

    #endregion

    #region Private Methods

    private bool IsValidSlot(int slot)
    {
        return slot >= 0 && slot < _items.Count;
    }

    #endregion
}
