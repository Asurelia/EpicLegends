using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel d'inventaire avec grille d'items.
/// </summary>
public class InventoryPanel : UIPanel
{
    #region Serialized Fields

    [Header("Grid")]
    [SerializeField] private Transform _slotContainer;
    [SerializeField] private GameObject _slotPrefab;
    [SerializeField] private int _slotsPerRow = 5;

    [Header("Item Details")]
    [SerializeField] private GameObject _detailsPanel;
    [SerializeField] private Image _detailsIcon;
    [SerializeField] private TextMeshProUGUI _detailsName;
    [SerializeField] private TextMeshProUGUI _detailsDescription;
    [SerializeField] private TextMeshProUGUI _detailsStats;
    [SerializeField] private Button _useButton;
    [SerializeField] private Button _equipButton;
    [SerializeField] private Button _dropButton;

    [Header("Info")]
    [SerializeField] private TextMeshProUGUI _goldText;
    [SerializeField] private TextMeshProUGUI _capacityText;

    [Header("Filters")]
    [SerializeField] private Button _filterAllButton;
    [SerializeField] private Button _filterWeaponsButton;
    [SerializeField] private Button _filterArmorButton;
    [SerializeField] private Button _filterConsumablesButton;
    [SerializeField] private Button _sortButton;

    #endregion

    #region Private Fields

    private Inventory _inventory;
    private List<InventorySlotUI> _slots = new List<InventorySlotUI>();
    private int _selectedSlot = -1;
    private ItemCategory? _currentFilter = null;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {

        // Setup boutons
        if (_useButton != null) _useButton.onClick.AddListener(OnUseClicked);
        if (_equipButton != null) _equipButton.onClick.AddListener(OnEquipClicked);
        if (_dropButton != null) _dropButton.onClick.AddListener(OnDropClicked);
        if (_sortButton != null) _sortButton.onClick.AddListener(OnSortClicked);

        // Setup filtres
        if (_filterAllButton != null) _filterAllButton.onClick.AddListener(() => SetFilter(null));
        if (_filterWeaponsButton != null) _filterWeaponsButton.onClick.AddListener(() => SetFilter(ItemCategory.Weapon));
        if (_filterArmorButton != null) _filterArmorButton.onClick.AddListener(() => SetFilter(ItemCategory.Armor));
        if (_filterConsumablesButton != null) _filterConsumablesButton.onClick.AddListener(() => SetFilter(ItemCategory.Consumable));
    }

    #endregion

    #region UIPanel Overrides

    public override void Show()
    {
        base.Show();

        // Trouver l'inventaire du joueur
        var player = GameManager.Instance?.Player;
        if (player != null)
        {
            _inventory = player.GetComponent<Inventory>();
        }

        if (_inventory != null)
        {
            _inventory.OnInventoryChanged += RefreshDisplay;
            CreateSlots();
            RefreshDisplay();
        }

        HideDetails();
    }

    public override void Hide()
    {
        base.Hide();

        if (_inventory != null)
        {
            _inventory.OnInventoryChanged -= RefreshDisplay;
        }

        _selectedSlot = -1;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Rafraichit l'affichage complet.
    /// </summary>
    public void RefreshDisplay()
    {
        if (_inventory == null) return;

        RefreshSlots();
        RefreshInfo();
    }

    /// <summary>
    /// Selectionne un slot.
    /// </summary>
    public void SelectSlot(int slotIndex)
    {
        _selectedSlot = slotIndex;

        // Deselectionner les autres
        for (int i = 0; i < _slots.Count; i++)
        {
            _slots[i].SetSelected(i == slotIndex);
        }

        // Afficher les details
        var item = _inventory?.GetItemAt(slotIndex);
        if (item != null)
        {
            ShowDetails(item);
        }
        else
        {
            HideDetails();
        }
    }

    #endregion

    #region Private Methods - Slots

    private void CreateSlots()
    {
        if (_slotContainer == null || _slotPrefab == null) return;

        // Nettoyer les slots existants
        foreach (Transform child in _slotContainer)
        {
            Destroy(child.gameObject);
        }
        _slots.Clear();

        // Creer les slots
        int capacity = _inventory?.Capacity ?? 20;
        for (int i = 0; i < capacity; i++)
        {
            var slotObj = Instantiate(_slotPrefab, _slotContainer);
            var slotUI = slotObj.GetComponent<InventorySlotUI>();
            if (slotUI == null)
            {
                slotUI = slotObj.AddComponent<InventorySlotUI>();
            }

            int slotIndex = i;
            slotUI.Initialize(slotIndex, () => SelectSlot(slotIndex));
            _slots.Add(slotUI);
        }
    }

    private void RefreshSlots()
    {
        if (_inventory == null) return;

        for (int i = 0; i < _slots.Count; i++)
        {
            var item = _inventory.GetItemAt(i);

            // Appliquer le filtre
            bool visible = true;
            if (_currentFilter.HasValue && item != null)
            {
                visible = item.Data.category == _currentFilter.Value;
            }

            _slots[i].SetItem(item);
            _slots[i].gameObject.SetActive(visible || item == null);
        }
    }

    private void RefreshInfo()
    {
        var playerStats = GameManager.Instance?.PlayerStats;

        if (_goldText != null && playerStats != null)
        {
            _goldText.text = $"{playerStats.Gold:N0} Gold";
        }

        if (_capacityText != null && _inventory != null)
        {
            _capacityText.text = $"{_inventory.UsedSlots}/{_inventory.Capacity}";
        }
    }

    #endregion

    #region Private Methods - Details

    private void ShowDetails(ItemInstance item)
    {
        if (_detailsPanel == null || item?.Data == null) return;

        _detailsPanel.SetActive(true);

        if (_detailsIcon != null) _detailsIcon.sprite = item.Data.icon;
        if (_detailsName != null)
        {
            _detailsName.text = item.Data.displayName;
            _detailsName.color = GetRarityColor(item.Data.rarity);
        }
        if (_detailsDescription != null) _detailsDescription.text = item.Data.description;
        if (_detailsStats != null) _detailsStats.text = GetItemStats(item.Data);

        // Configurer les boutons selon le type d'item
        bool canUse = item.Data.category == ItemCategory.Consumable;
        bool canEquip = item.Data.category == ItemCategory.Weapon ||
                       item.Data.category == ItemCategory.Armor ||
                       item.Data.category == ItemCategory.Accessory;

        if (_useButton != null) _useButton.gameObject.SetActive(canUse);
        if (_equipButton != null) _equipButton.gameObject.SetActive(canEquip);
        if (_dropButton != null) _dropButton.gameObject.SetActive(true);
    }

    private void HideDetails()
    {
        if (_detailsPanel != null)
        {
            _detailsPanel.SetActive(false);
        }
    }

    private string GetItemStats(ItemData data)
    {
        var stats = new List<string>();

        if (data.attackPower > 0) stats.Add($"Attack: +{data.attackPower}");
        if (data.defensePower > 0) stats.Add($"Defense: +{data.defensePower}");
        if (data.healthBonus > 0) stats.Add($"Health: +{data.healthBonus}");
        if (data.manaBonus > 0) stats.Add($"Mana: +{data.manaBonus}");

        if (data.category == ItemCategory.Consumable)
        {
            if (data.healAmount > 0) stats.Add($"Heals: {data.healAmount} HP");
            if (data.manaRestoreAmount > 0) stats.Add($"Restores: {data.manaRestoreAmount} MP");
        }

        return string.Join("\n", stats);
    }

    private Color GetRarityColor(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common => Color.white,
            ItemRarity.Uncommon => Color.green,
            ItemRarity.Rare => new Color(0.3f, 0.5f, 1f),
            ItemRarity.Epic => new Color(0.6f, 0f, 0.8f),
            ItemRarity.Legendary => new Color(1f, 0.5f, 0f),
            ItemRarity.Mythic => Color.red,
            _ => Color.gray
        };
    }

    #endregion

    #region Private Methods - Filters

    private void SetFilter(ItemCategory? category)
    {
        _currentFilter = category;
        RefreshSlots();
    }

    #endregion

    #region Button Callbacks

    private void OnUseClicked()
    {
        if (_selectedSlot < 0 || _inventory == null) return;

        var item = _inventory.GetItemAt(_selectedSlot);
        if (item?.Data == null || item.Data.category != ItemCategory.Consumable) return;

        // Utiliser le consommable
        var playerStats = GameManager.Instance?.PlayerStats;
        if (playerStats != null)
        {
            if (item.Data.healAmount > 0)
            {
                playerStats.Heal(item.Data.healAmount);
            }
            if (item.Data.manaRestoreAmount > 0)
            {
                playerStats.RestoreMana(item.Data.manaRestoreAmount);
            }
        }

        // Retirer l'item
        _inventory.RemoveItemAt(_selectedSlot, 1);

        // Mettre a jour l'affichage
        RefreshDisplay();
        if (_inventory.GetItemAt(_selectedSlot) == null)
        {
            HideDetails();
            _selectedSlot = -1;
        }
    }

    private void OnEquipClicked()
    {
        if (_selectedSlot < 0 || _inventory == null) return;

        var item = _inventory.GetItemAt(_selectedSlot);
        if (item?.Data == null) return;

        // Verifier que l'item est equippable
        if (item.Data.equipSlot == EquipmentSlot.None)
        {
            Debug.LogWarning($"[InventoryPanel] {item.Data.displayName} ne peut pas etre equipe");
            return;
        }

        // Trouver l'EquipmentManager du joueur
        var player = GameManager.Instance?.Player;
        var equipmentManager = player?.GetComponent<EquipmentManager>();

        if (equipmentManager == null)
        {
            Debug.LogWarning("[InventoryPanel] EquipmentManager non trouve sur le joueur");
            return;
        }

        // Convertir l'ItemData en EquipmentInstance via l'adaptateur
        var equipInstance = ItemEquipmentAdapter.CreateEquipmentInstance(item.Data);
        if (equipInstance == null)
        {
            Debug.LogWarning($"[InventoryPanel] Impossible de convertir {item.Data.displayName} en equipement");
            return;
        }

        // Recuperer l'item actuellement equipe dans ce slot (s'il y en a un)
        var currentEquipped = equipmentManager.GetEquipped(item.Data.equipSlot);

        // Equiper le nouvel item
        if (equipmentManager.Equip(equipInstance))
        {
            Debug.Log($"[InventoryPanel] Equipe: {item.Data.displayName} (slot: {item.Data.equipSlot})");

            // Retirer l'item de l'inventaire
            _inventory.RemoveItemAt(_selectedSlot);

            // Si un item etait deja equipe, le remettre dans l'inventaire
            // Note: Necessite une conversion inverse EquipmentInstance -> ItemData
            // Pour l'instant, l'ancien item est perdu (a ameliorer)
            if (currentEquipped != null)
            {
                Debug.Log($"[InventoryPanel] Ancien equipement desequipe: {currentEquipped.DisplayName}");
            }
        }
        else
        {
            Debug.LogWarning($"[InventoryPanel] Echec de l'equipement de {item.Data.displayName}");
        }

        RefreshDisplay();
        HideDetails();
        _selectedSlot = -1;
    }

    private void OnDropClicked()
    {
        if (_selectedSlot < 0 || _inventory == null) return;

        var item = _inventory.RemoveItemAt(_selectedSlot);
        if (item != null)
        {
            // TODO: Spawner l'item dans le monde
            Debug.Log($"[InventoryPanel] Item dropped: {item.Data.displayName}");
        }

        RefreshDisplay();
        HideDetails();
        _selectedSlot = -1;
    }

    private void OnSortClicked()
    {
        _inventory?.Sort();
        RefreshDisplay();
    }

    #endregion
}

/// <summary>
/// UI d'un slot d'inventaire.
/// </summary>
public class InventorySlotUI : MonoBehaviour
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private TextMeshProUGUI _quantityText;
    [SerializeField] private Image _rarityBorder;
    [SerializeField] private Image _selectionHighlight;
    [SerializeField] private Button _button;

    private int _slotIndex;
    private System.Action _onClick;

    public void Initialize(int index, System.Action onClick)
    {
        _slotIndex = index;
        _onClick = onClick;

        if (_button == null) _button = GetComponent<Button>();
        if (_button != null) _button.onClick.AddListener(() => _onClick?.Invoke());

        Clear();
    }

    public void SetItem(ItemInstance item)
    {
        if (item == null || item.Data == null)
        {
            Clear();
            return;
        }

        if (_iconImage != null)
        {
            _iconImage.sprite = item.Data.icon;
            _iconImage.enabled = true;
        }

        if (_quantityText != null)
        {
            _quantityText.text = item.Quantity > 1 ? item.Quantity.ToString() : "";
            _quantityText.enabled = item.Quantity > 1;
        }

        if (_rarityBorder != null)
        {
            _rarityBorder.color = GetRarityColor(item.Data.rarity);
            _rarityBorder.enabled = true;
        }
    }

    public void Clear()
    {
        if (_iconImage != null) _iconImage.enabled = false;
        if (_quantityText != null) _quantityText.enabled = false;
        if (_rarityBorder != null) _rarityBorder.enabled = false;
    }

    public void SetSelected(bool selected)
    {
        if (_selectionHighlight != null)
        {
            _selectionHighlight.enabled = selected;
        }
    }

    private Color GetRarityColor(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common => Color.white,
            ItemRarity.Uncommon => Color.green,
            ItemRarity.Rare => new Color(0.3f, 0.5f, 1f),
            ItemRarity.Epic => new Color(0.6f, 0f, 0.8f),
            ItemRarity.Legendary => new Color(1f, 0.5f, 0f),
            ItemRarity.Mythic => Color.red,
            _ => Color.gray
        };
    }
}
