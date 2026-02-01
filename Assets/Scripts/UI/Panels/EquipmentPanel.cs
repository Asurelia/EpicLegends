using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel d'equipement du personnage.
/// </summary>
public class EquipmentPanel : UIPanel
{
    #region Serialized Fields

    [Header("Equipment Slots")]
    [SerializeField] private EquipmentSlotUI _mainHandSlot;
    [SerializeField] private EquipmentSlotUI _offHandSlot;
    [SerializeField] private EquipmentSlotUI _headSlot;
    [SerializeField] private EquipmentSlotUI _bodySlot;
    [SerializeField] private EquipmentSlotUI _handsSlot;
    [SerializeField] private EquipmentSlotUI _legsSlot;
    [SerializeField] private EquipmentSlotUI _feetSlot;
    [SerializeField] private EquipmentSlotUI _accessory1Slot;
    [SerializeField] private EquipmentSlotUI _accessory2Slot;

    [Header("Character Preview")]
    [SerializeField] private RawImage _characterPreview;

    [Header("Stats Display")]
    [SerializeField] private TextMeshProUGUI _powerScoreText;
    [SerializeField] private TextMeshProUGUI _armorText;
    [SerializeField] private TextMeshProUGUI _attackText;
    [SerializeField] private TextMeshProUGUI _defenseText;

    [Header("Item Details")]
    [SerializeField] private GameObject _detailsPanel;
    [SerializeField] private Image _detailsIcon;
    [SerializeField] private TextMeshProUGUI _detailsName;
    [SerializeField] private TextMeshProUGUI _detailsDescription;
    [SerializeField] private TextMeshProUGUI _detailsStats;
    [SerializeField] private Button _unequipButton;

    [Header("Set Bonuses")]
    [SerializeField] private Transform _setBonusContainer;
    [SerializeField] private GameObject _setBonusPrefab;

    #endregion

    #region Private Fields

    private EquipmentManager _equipmentManager;
    private Dictionary<EquipmentSlot, EquipmentSlotUI> _slotUIs;
    private EquipmentSlot? _selectedSlot = null;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        // Mapper les slots
        _slotUIs = new Dictionary<EquipmentSlot, EquipmentSlotUI>();

        if (_mainHandSlot != null) _slotUIs[EquipmentSlot.MainHand] = _mainHandSlot;
        if (_offHandSlot != null) _slotUIs[EquipmentSlot.OffHand] = _offHandSlot;
        if (_headSlot != null) _slotUIs[EquipmentSlot.Head] = _headSlot;
        if (_bodySlot != null) _slotUIs[EquipmentSlot.Body] = _bodySlot;
        if (_handsSlot != null) _slotUIs[EquipmentSlot.Hands] = _handsSlot;
        if (_legsSlot != null) _slotUIs[EquipmentSlot.Legs] = _legsSlot;
        if (_feetSlot != null) _slotUIs[EquipmentSlot.Feet] = _feetSlot;
        if (_accessory1Slot != null) _slotUIs[EquipmentSlot.Accessory1] = _accessory1Slot;
        if (_accessory2Slot != null) _slotUIs[EquipmentSlot.Accessory2] = _accessory2Slot;

        // Setup callbacks
        foreach (var kvp in _slotUIs)
        {
            if (kvp.Value != null)
            {
                var slot = kvp.Key;
                kvp.Value.Initialize(slot, () => OnSlotClicked(slot));
            }
        }

        if (_unequipButton != null)
        {
            _unequipButton.onClick.AddListener(OnUnequipClicked);
        }
    }

    #endregion

    #region UIPanel Overrides

    public override void Show()
    {
        base.Show();

        // Trouver les composants du joueur
        var player = GameManager.Instance?.Player;
        if (player != null)
        {
            _equipmentManager = player.GetComponent<EquipmentManager>();
        }

        if (_equipmentManager != null)
        {
            _equipmentManager.OnStatsChanged += RefreshDisplay;
        }

        RefreshDisplay();
        HideDetails();
    }

    public override void Hide()
    {
        base.Hide();

        if (_equipmentManager != null)
        {
            _equipmentManager.OnStatsChanged -= RefreshDisplay;
        }

        _selectedSlot = null;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Rafraichit l'affichage complet.
    /// </summary>
    public void RefreshDisplay()
    {
        RefreshSlots();
        RefreshStats();
    }

    #endregion

    #region Private Methods - Slots

    private void RefreshSlots()
    {
        if (_equipmentManager == null) return;

        foreach (var kvp in _slotUIs)
        {
            if (kvp.Value == null) continue;

            var item = _equipmentManager.GetEquipped(kvp.Key);
            kvp.Value.SetEquipmentItem(item);
        }
    }

    private void OnSlotClicked(EquipmentSlot slot)
    {
        _selectedSlot = slot;

        // Deselectionner tous les slots
        foreach (var kvp in _slotUIs)
        {
            kvp.Value?.SetSelected(kvp.Key == slot);
        }

        // Afficher les details
        var item = _equipmentManager?.GetEquipped(slot);
        if (item != null)
        {
            ShowDetails(item, slot);
        }
        else
        {
            HideDetails();
        }
    }

    #endregion

    #region Private Methods - Stats

    private void RefreshStats()
    {
        if (_equipmentManager == null) return;

        if (_powerScoreText != null)
            _powerScoreText.text = $"Power: {_equipmentManager.TotalPowerScore}";

        if (_armorText != null)
            _armorText.text = $"Armor: {_equipmentManager.TotalArmor}";

        if (_attackText != null)
        {
            float attack = _equipmentManager.GetTotalStatBonus(StatType.Attack);
            _attackText.text = $"Attack: +{attack:F0}";
        }

        if (_defenseText != null)
        {
            float defense = _equipmentManager.GetTotalStatBonus(StatType.Defense);
            _defenseText.text = $"Defense: +{defense:F0}";
        }
    }

    #endregion

    #region Private Methods - Details

    private void ShowDetails(EquipmentInstance item, EquipmentSlot slot)
    {
        if (_detailsPanel == null || item?.BaseData == null) return;

        _detailsPanel.SetActive(true);

        if (_detailsIcon != null && item.BaseData.icon != null)
            _detailsIcon.sprite = item.BaseData.icon;

        if (_detailsName != null)
        {
            _detailsName.text = item.BaseData.equipmentName;
            _detailsName.color = GetRarityColor(item.BaseData.rarity);
        }

        if (_detailsDescription != null)
            _detailsDescription.text = item.BaseData.description;

        if (_detailsStats != null)
            _detailsStats.text = GetItemStats(item);

        if (_unequipButton != null)
            _unequipButton.gameObject.SetActive(true);
    }

    private void HideDetails()
    {
        if (_detailsPanel != null)
        {
            _detailsPanel.SetActive(false);
        }
    }

    private string GetItemStats(EquipmentInstance item)
    {
        if (item == null) return "";

        var stats = new List<string>();

        // Afficher les stats principales
        stats.Add($"Power Score: {item.GetPowerScore()}");

        if (item.GetTotalArmor() > 0)
            stats.Add($"Armor: {item.GetTotalArmor()}");

        // Afficher les bonus de stats
        foreach (StatType statType in System.Enum.GetValues(typeof(StatType)))
        {
            float value = item.GetTotalStat(statType);
            if (value != 0)
            {
                string sign = value > 0 ? "+" : "";
                stats.Add($"{statType}: {sign}{value:F0}");
            }
        }

        return string.Join("\n", stats);
    }

    private Color GetRarityColor(EquipmentRarity rarity)
    {
        return rarity switch
        {
            EquipmentRarity.Common => Color.white,
            EquipmentRarity.Uncommon => Color.green,
            EquipmentRarity.Rare => new Color(0.3f, 0.5f, 1f),
            EquipmentRarity.Epic => new Color(0.6f, 0f, 0.8f),
            EquipmentRarity.Legendary => new Color(1f, 0.5f, 0f),
            // No Mythic/Unique rarity in EquipmentRarity
            _ => Color.gray
        };
    }

    #endregion

    #region Event Handlers

    private void OnUnequipClicked()
    {
        if (!_selectedSlot.HasValue || _equipmentManager == null) return;

        var item = _equipmentManager.Unequip(_selectedSlot.Value);
        if (item != null)
        {
            // Ajouter a l'inventaire si possible
            var player = GameManager.Instance?.Player;
            var inventory = player?.GetComponent<Inventory>();
            if (inventory != null && item.BaseData != null)
            {
                // Convertir en ItemData si possible
                // Note: Necessite une integration plus poussee
                Debug.Log($"[EquipmentPanel] Desequipe: {item.BaseData.equipmentName}");
            }
        }

        HideDetails();
        _selectedSlot = null;
        RefreshDisplay();
    }

    #endregion
}

/// <summary>
/// UI d'un slot d'equipement.
/// </summary>
public class EquipmentSlotUI : MonoBehaviour
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private Image _emptySlotIcon;
    [SerializeField] private Image _rarityBorder;
    [SerializeField] private Image _selectionHighlight;
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _slotLabel;

    private EquipmentSlot _slot;
    private System.Action _onClick;

    public void Initialize(EquipmentSlot slot, System.Action onClick)
    {
        _slot = slot;
        _onClick = onClick;

        if (_button == null) _button = GetComponent<Button>();
        if (_button != null) _button.onClick.AddListener(() => _onClick?.Invoke());

        if (_slotLabel != null)
        {
            _slotLabel.text = slot.ToString();
        }

        Clear();
    }

    public void SetEquipmentItem(EquipmentInstance item)
    {
        if (item == null || item.BaseData == null)
        {
            Clear();
            return;
        }

        if (_iconImage != null)
        {
            _iconImage.sprite = item.BaseData.icon;
            _iconImage.enabled = item.BaseData.icon != null;
        }

        if (_emptySlotIcon != null)
        {
            _emptySlotIcon.enabled = false;
        }

        if (_rarityBorder != null)
        {
            _rarityBorder.color = GetRarityColor(item.BaseData.rarity);
            _rarityBorder.enabled = true;
        }
    }

    public void Clear()
    {
        if (_iconImage != null) _iconImage.enabled = false;
        if (_emptySlotIcon != null) _emptySlotIcon.enabled = true;
        if (_rarityBorder != null) _rarityBorder.enabled = false;
    }

    public void SetSelected(bool selected)
    {
        if (_selectionHighlight != null)
        {
            _selectionHighlight.enabled = selected;
        }
    }

    private Color GetRarityColor(EquipmentRarity rarity)
    {
        return rarity switch
        {
            EquipmentRarity.Common => Color.white,
            EquipmentRarity.Uncommon => Color.green,
            EquipmentRarity.Rare => new Color(0.3f, 0.5f, 1f),
            EquipmentRarity.Epic => new Color(0.6f, 0f, 0.8f),
            EquipmentRarity.Legendary => new Color(1f, 0.5f, 0f),
            // No Mythic/Unique rarity in EquipmentRarity
            _ => Color.gray
        };
    }
}
