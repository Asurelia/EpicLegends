using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Barre de raccourcis (Hotbar) pour acces rapide aux consommables et skills.
/// Permet d'utiliser des items avec les touches 1-9.
/// </summary>
public class QuickSlotBar : MonoBehaviour
{
    #region Events

    public event Action<int, ItemData> OnSlotUsed;
    public event Action<int, ItemData> OnSlotChanged;

    #endregion

    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private int _slotCount = 8;
    [SerializeField] private KeyCode[] _slotKeyCodes = new KeyCode[]
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
        KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8
    };

    [Header("UI References")]
    [SerializeField] private Transform _slotContainer;
    [SerializeField] private GameObject _slotPrefab;

    [Header("Cooldown")]
    [SerializeField] private float _globalCooldown = 0.5f;

    #endregion

    #region Private Fields

    private Inventory _inventory;
    private PlayerStats _playerStats;
    private List<QuickSlotUI> _slotUIs = new List<QuickSlotUI>();
    private Dictionary<int, ItemData> _assignedItems = new Dictionary<int, ItemData>();
    private Dictionary<int, float> _cooldowns = new Dictionary<int, float>();
    private float _lastUseTime;

    #endregion

    #region Properties

    public int SlotCount => _slotCount;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        CreateSlots();
    }

    private void Start()
    {
        // Trouver les references du joueur
        var player = GameManager.Instance?.Player;
        if (player != null)
        {
            _inventory = player.GetComponent<Inventory>();
            _playerStats = player.GetComponent<PlayerStats>();
        }

        if (_inventory != null)
        {
            _inventory.OnInventoryChanged += RefreshSlots;
        }
    }

    private void OnDestroy()
    {
        if (_inventory != null)
        {
            _inventory.OnInventoryChanged -= RefreshSlots;
        }
    }

    private void Update()
    {
        HandleInput();
        UpdateCooldowns();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Assigne un item a un slot de raccourci.
    /// </summary>
    /// <param name="slotIndex">Index du slot (0-7).</param>
    /// <param name="itemData">Item a assigner.</param>
    public void AssignItem(int slotIndex, ItemData itemData)
    {
        if (slotIndex < 0 || slotIndex >= _slotCount) return;

        // Verifier que l'item est utilisable (consommable)
        if (itemData != null && itemData.category != ItemCategory.Consumable)
        {
            Debug.LogWarning($"[QuickSlotBar] Seuls les consommables peuvent etre assignes a la hotbar");
            return;
        }

        _assignedItems[slotIndex] = itemData;
        RefreshSlot(slotIndex);
        OnSlotChanged?.Invoke(slotIndex, itemData);
    }

    /// <summary>
    /// Retire l'item d'un slot.
    /// </summary>
    /// <param name="slotIndex">Index du slot.</param>
    public void ClearSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slotCount) return;

        _assignedItems.Remove(slotIndex);
        RefreshSlot(slotIndex);
        OnSlotChanged?.Invoke(slotIndex, (ItemData)null);
    }

    /// <summary>
    /// Utilise l'item dans un slot.
    /// </summary>
    /// <param name="slotIndex">Index du slot.</param>
    /// <returns>True si utilise avec succes.</returns>
    public bool UseSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slotCount) return false;

        // Verifier le cooldown global
        if (Time.time - _lastUseTime < _globalCooldown) return false;

        // Verifier le cooldown du slot
        if (_cooldowns.TryGetValue(slotIndex, out float cooldownEnd) && Time.time < cooldownEnd)
        {
            return false;
        }

        // Verifier qu'un item est assigne
        if (!_assignedItems.TryGetValue(slotIndex, out var itemData) || itemData == null)
        {
            return false;
        }

        // Verifier qu'on a l'item dans l'inventaire
        if (_inventory == null) return false;
        int itemIndex = _inventory.FindItem(itemData);
        if (itemIndex < 0)
        {
            Debug.Log($"[QuickSlotBar] Pas de {itemData.displayName} dans l'inventaire");
            return false;
        }
        var itemInstance = _inventory.GetItemAt(itemIndex);

        // Utiliser le consommable
        if (!UseConsumable(itemData, itemInstance))
        {
            return false;
        }

        // Appliquer les cooldowns
        _lastUseTime = Time.time;
        if (itemData.useCooldown > 0)
        {
            _cooldowns[slotIndex] = Time.time + itemData.useCooldown;
        }

        // Declencher l'event
        OnSlotUsed?.Invoke(slotIndex, itemData);

        // Rafraichir l'affichage
        RefreshSlot(slotIndex);

        return true;
    }

    /// <summary>
    /// Obtient l'item assigne a un slot.
    /// </summary>
    /// <param name="slotIndex">Index du slot.</param>
    /// <returns>ItemData ou null.</returns>
    public ItemData GetAssignedItem(int slotIndex)
    {
        return _assignedItems.TryGetValue(slotIndex, out var item) ? item : null;
    }

    /// <summary>
    /// Obtient l'instance d'item correspondant au slot.
    /// </summary>
    /// <param name="slotIndex">Index du slot.</param>
    /// <returns>ItemInstance ou null.</returns>
    public ItemInstance GetItemInstance(int slotIndex)
    {
        if (!_assignedItems.TryGetValue(slotIndex, out var itemData) || itemData == null)
            return null;

        int itemIndex = _inventory?.FindItem(itemData) ?? -1;
        return itemIndex >= 0 ? _inventory.GetItemAt(itemIndex) : null;
    }

    /// <summary>
    /// Obtient le cooldown restant d'un slot.
    /// </summary>
    /// <param name="slotIndex">Index du slot.</param>
    /// <returns>Temps restant en secondes.</returns>
    public float GetCooldownRemaining(int slotIndex)
    {
        if (!_cooldowns.TryGetValue(slotIndex, out float cooldownEnd))
            return 0f;

        return Mathf.Max(0f, cooldownEnd - Time.time);
    }

    /// <summary>
    /// Sauvegarde la configuration de la hotbar.
    /// </summary>
    public QuickSlotSaveData GetSaveData()
    {
        var data = new QuickSlotSaveData();
        data.assignedItemIds = new Dictionary<int, string>();

        foreach (var kvp in _assignedItems)
        {
            if (kvp.Value != null)
            {
                data.assignedItemIds[kvp.Key] = kvp.Value.itemId;
            }
        }

        return data;
    }

    /// <summary>
    /// Charge la configuration de la hotbar.
    /// </summary>
    public void LoadSaveData(QuickSlotSaveData data, ItemData[] allItems)
    {
        if (data?.assignedItemIds == null || allItems == null) return;

        _assignedItems.Clear();

        foreach (var kvp in data.assignedItemIds)
        {
            var itemData = System.Array.Find(allItems, i => i.itemId == kvp.Value);
            if (itemData != null)
            {
                _assignedItems[kvp.Key] = itemData;
            }
        }

        RefreshAllSlots();
    }

    #endregion

    #region Private Methods

    private void CreateSlots()
    {
        if (_slotContainer == null || _slotPrefab == null) return;

        // Nettoyer les slots existants
        foreach (Transform child in _slotContainer)
        {
            Destroy(child.gameObject);
        }
        _slotUIs.Clear();

        // Creer les slots
        for (int i = 0; i < _slotCount; i++)
        {
            var slotObj = Instantiate(_slotPrefab, _slotContainer);
            var slotUI = slotObj.GetComponent<QuickSlotUI>();
            if (slotUI == null)
            {
                slotUI = slotObj.AddComponent<QuickSlotUI>();
            }

            int slotIndex = i;
            string keyLabel = (i + 1).ToString();
            if (i < _slotKeyCodes.Length)
            {
                keyLabel = GetKeyLabel(_slotKeyCodes[i]);
            }

            slotUI.Initialize(slotIndex, keyLabel, () => UseSlot(slotIndex));
            _slotUIs.Add(slotUI);
        }
    }

    private void HandleInput()
    {
        for (int i = 0; i < _slotKeyCodes.Length && i < _slotCount; i++)
        {
            if (Input.GetKeyDown(_slotKeyCodes[i]))
            {
                UseSlot(i);
            }
        }
    }

    private void UpdateCooldowns()
    {
        // Mettre a jour l'affichage des cooldowns
        for (int i = 0; i < _slotUIs.Count; i++)
        {
            float remaining = GetCooldownRemaining(i);
            float total = 0f;

            if (_assignedItems.TryGetValue(i, out var itemData) && itemData != null)
            {
                total = itemData.useCooldown;
            }

            _slotUIs[i].UpdateCooldown(remaining, total);
        }
    }

    private bool UseConsumable(ItemData itemData, ItemInstance itemInstance)
    {
        if (_playerStats == null) return false;

        bool used = false;

        // Appliquer les effets du consommable
        if (itemData.healAmount > 0)
        {
            _playerStats.Heal(itemData.healAmount);
            used = true;
        }

        if (itemData.manaRestoreAmount > 0)
        {
            _playerStats.RestoreMana(itemData.manaRestoreAmount);
            used = true;
        }

        // Jouer le son d'utilisation
        if (used && itemData.useSound != null)
        {
            AudioSource.PlayClipAtPoint(itemData.useSound, Camera.main.transform.position);
        }

        // Retirer l'item de l'inventaire
        if (used && _inventory != null)
        {
            _inventory.RemoveItem(itemData, 1);
        }

        return used;
    }

    private void RefreshSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slotUIs.Count) return;

        var itemData = GetAssignedItem(slotIndex);
        var itemInstance = GetItemInstance(slotIndex);

        _slotUIs[slotIndex].SetItem(itemData, itemInstance?.Quantity ?? 0);
    }

    private void RefreshSlots()
    {
        RefreshAllSlots();
    }

    private void RefreshAllSlots()
    {
        for (int i = 0; i < _slotCount; i++)
        {
            RefreshSlot(i);
        }
    }

    private string GetKeyLabel(KeyCode keyCode)
    {
        return keyCode switch
        {
            KeyCode.Alpha1 => "1",
            KeyCode.Alpha2 => "2",
            KeyCode.Alpha3 => "3",
            KeyCode.Alpha4 => "4",
            KeyCode.Alpha5 => "5",
            KeyCode.Alpha6 => "6",
            KeyCode.Alpha7 => "7",
            KeyCode.Alpha8 => "8",
            KeyCode.Alpha9 => "9",
            KeyCode.Alpha0 => "0",
            _ => keyCode.ToString()
        };
    }

    #endregion
}

/// <summary>
/// UI d'un slot de raccourci.
/// </summary>
public class QuickSlotUI : MonoBehaviour
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private TextMeshProUGUI _quantityText;
    [SerializeField] private TextMeshProUGUI _keyText;
    [SerializeField] private Image _cooldownOverlay;
    [SerializeField] private Button _button;

    private int _slotIndex;
    private Action _onClick;

    public void Initialize(int index, string keyLabel, Action onClick)
    {
        _slotIndex = index;
        _onClick = onClick;

        if (_button == null) _button = GetComponent<Button>();
        if (_button != null) _button.onClick.AddListener(() => _onClick?.Invoke());

        if (_keyText != null)
        {
            _keyText.text = keyLabel;
        }

        Clear();
    }

    public void SetItem(ItemData itemData, int quantity)
    {
        if (itemData == null)
        {
            Clear();
            return;
        }

        if (_iconImage != null)
        {
            _iconImage.sprite = itemData.icon;
            _iconImage.enabled = itemData.icon != null;
        }

        if (_quantityText != null)
        {
            _quantityText.text = quantity > 0 ? quantity.ToString() : "";
            _quantityText.enabled = quantity > 0;

            // Griser si pas d'item disponible
            if (_iconImage != null)
            {
                _iconImage.color = quantity > 0 ? Color.white : new Color(1f, 1f, 1f, 0.5f);
            }
        }
    }

    public void UpdateCooldown(float remaining, float total)
    {
        if (_cooldownOverlay == null) return;

        if (remaining > 0 && total > 0)
        {
            _cooldownOverlay.enabled = true;
            _cooldownOverlay.fillAmount = remaining / total;
        }
        else
        {
            _cooldownOverlay.enabled = false;
        }
    }

    public void Clear()
    {
        if (_iconImage != null)
        {
            _iconImage.enabled = false;
        }
        if (_quantityText != null)
        {
            _quantityText.enabled = false;
        }
        if (_cooldownOverlay != null)
        {
            _cooldownOverlay.enabled = false;
        }
    }
}

/// <summary>
/// Donnees de sauvegarde de la hotbar.
/// </summary>
[System.Serializable]
public class QuickSlotSaveData
{
    public Dictionary<int, string> assignedItemIds;
}
