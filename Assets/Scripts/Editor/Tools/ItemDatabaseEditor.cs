using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Editeur de base de donnees d'items avec TreeView et filtres avances.
/// Menu: EpicLegends > Tools > Item Database Editor
/// </summary>
public class ItemDatabaseEditor : EditorWindow
{
    #region Types

    [System.Serializable]
    public class ItemEntry
    {
        public string id;
        public string name;
        public ItemCategory category;
        public ItemRarity rarity;
        public string description;
        public Sprite icon;
        public int stackSize = 99;
        public int buyPrice;
        public int sellPrice;
        public bool isQuestItem;
        public bool isTradeable = true;

        // Equipment stats (for weapons/armor)
        public int attack;
        public int defense;
        public int health;
        public float critRate;
        public float critDamage;

        // Consumable effects
        public int healAmount;
        public int manaRestore;
        public float duration;
        public string effectId;

        // Crafting
        public List<CraftingIngredient> craftingRecipe = new List<CraftingIngredient>();

        // Drop info
        public List<DropSource> dropSources = new List<DropSource>();

        // Meta
        public bool isExpanded = false;
        public bool isDirty = false;
    }

    [System.Serializable]
    public class CraftingIngredient
    {
        public string itemId;
        public int amount;
    }

    [System.Serializable]
    public class DropSource
    {
        public string sourceId;
        public string sourceName;
        public float dropRate;
    }

    public enum ItemCategory
    {
        Weapon,
        Armor,
        Accessory,
        Consumable,
        Material,
        QuestItem,
        Currency,
        KeyItem
    }

    public enum ItemRarity
    {
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        Epic = 4,
        Legendary = 5,
        Mythic = 6
    }

    #endregion

    #region State

    private List<ItemEntry> _items = new List<ItemEntry>();
    private List<ItemEntry> _filteredItems = new List<ItemEntry>();
    private ItemEntry _selectedItem;

    // Filtering
    private string _searchQuery = "";
    private ItemCategory? _filterCategory = null;
    private ItemRarity? _filterRarity = null;
    private bool _showQuestItemsOnly = false;

    // Sorting
    private string _sortField = "name";
    private bool _sortAscending = true;

    // UI
    private Vector2 _listScrollPos;
    private Vector2 _detailScrollPos;
    private float _listWidth = 300f;
    private bool _isDraggingSplitter = false;

    // Tabs for detail view
    private int _detailTab = 0;
    private readonly string[] DETAIL_TABS = { "Basic", "Stats", "Crafting", "Drops", "Raw" };

    // Batch operations
    private bool _showBatchPanel = false;
    private float _batchPriceMultiplier = 1f;
    private int _batchRarityChange = 0;

    // Statistics
    private int _totalItems = 0;
    private Dictionary<ItemCategory, int> _categoryCount = new Dictionary<ItemCategory, int>();

    #endregion

    [MenuItem("EpicLegends/Tools/Item Database Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<ItemDatabaseEditor>("Item Database");
        window.minSize = new Vector2(900, 600);
    }

    private void OnEnable()
    {
        LoadItemsFromProject();
        ApplyFilters();
    }

    private void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.BeginHorizontal();

        // Left panel: Item list
        EditorGUILayout.BeginVertical(GUILayout.Width(_listWidth));
        DrawFilterPanel();
        DrawItemList();
        EditorGUILayout.EndVertical();

        // Splitter
        DrawSplitter();

        // Right panel: Detail view
        EditorGUILayout.BeginVertical();
        DrawDetailPanel();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        // Bottom status bar
        DrawStatusBar();
    }

    #region GUI Sections

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("+ New Item", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            CreateNewItem();
        }

        if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            DuplicateSelected();
        }

        if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            DeleteSelected();
        }

        GUILayout.Space(20);

        if (GUILayout.Button("Import CSV", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            ImportFromCSV();
        }

        if (GUILayout.Button("Export CSV", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            ExportToCSV();
        }

        GUILayout.Space(20);

        _showBatchPanel = GUILayout.Toggle(_showBatchPanel, "Batch Ops", EditorStyles.toolbarButton, GUILayout.Width(70));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Save All", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            SaveAllItems();
        }

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            LoadItemsFromProject();
            ApplyFilters();
        }

        EditorGUILayout.EndHorizontal();

        // Batch operations panel
        if (_showBatchPanel)
        {
            DrawBatchPanel();
        }
    }

    private void DrawBatchPanel()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        EditorGUILayout.LabelField("Batch:", GUILayout.Width(40));

        _batchPriceMultiplier = EditorGUILayout.FloatField("Price ×", _batchPriceMultiplier, GUILayout.Width(100));

        if (GUILayout.Button("Apply to Filtered", GUILayout.Width(110)))
        {
            ApplyBatchPriceChange();
        }

        GUILayout.Space(20);

        _batchRarityChange = EditorGUILayout.IntField("Rarity +", _batchRarityChange, GUILayout.Width(100));

        if (GUILayout.Button("Apply", GUILayout.Width(60)))
        {
            ApplyBatchRarityChange();
        }

        GUILayout.FlexibleSpace();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawFilterPanel()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Search
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        _searchQuery = EditorGUILayout.TextField(_searchQuery, EditorStyles.toolbarSearchField);
        if (EditorGUI.EndChangeCheck())
        {
            ApplyFilters();
        }

        if (GUILayout.Button("×", GUILayout.Width(20)))
        {
            _searchQuery = "";
            GUI.FocusControl(null);
            ApplyFilters();
        }
        EditorGUILayout.EndHorizontal();

        // Category filter
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Category:", GUILayout.Width(60));

        EditorGUI.BeginChangeCheck();
        if (GUILayout.Toggle(!_filterCategory.HasValue, "All", EditorStyles.miniButtonLeft, GUILayout.Width(40)))
        {
            _filterCategory = null;
        }

        foreach (ItemCategory cat in System.Enum.GetValues(typeof(ItemCategory)))
        {
            bool isSelected = _filterCategory == cat;
            if (GUILayout.Toggle(isSelected, cat.ToString().Substring(0, 3), EditorStyles.miniButtonMid, GUILayout.Width(35)))
            {
                _filterCategory = cat;
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            ApplyFilters();
        }

        EditorGUILayout.EndHorizontal();

        // Rarity filter
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Rarity:", GUILayout.Width(60));

        EditorGUI.BeginChangeCheck();
        if (GUILayout.Toggle(!_filterRarity.HasValue, "All", EditorStyles.miniButtonLeft, GUILayout.Width(40)))
        {
            _filterRarity = null;
        }

        foreach (ItemRarity rarity in System.Enum.GetValues(typeof(ItemRarity)))
        {
            bool isSelected = _filterRarity == rarity;
            GUI.backgroundColor = isSelected ? GetRarityColor(rarity) : Color.white;

            if (GUILayout.Toggle(isSelected, ((int)rarity).ToString() + "★", EditorStyles.miniButtonMid, GUILayout.Width(30)))
            {
                _filterRarity = rarity;
            }

            GUI.backgroundColor = Color.white;
        }

        if (EditorGUI.EndChangeCheck())
        {
            ApplyFilters();
        }

        EditorGUILayout.EndHorizontal();

        // Quest items toggle
        EditorGUI.BeginChangeCheck();
        _showQuestItemsOnly = EditorGUILayout.Toggle("Quest Items Only", _showQuestItemsOnly);
        if (EditorGUI.EndChangeCheck())
        {
            ApplyFilters();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawItemList()
    {
        // Header
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Name", EditorStyles.toolbarButton))
        {
            ToggleSort("name");
        }

        if (GUILayout.Button("Cat", EditorStyles.toolbarButton, GUILayout.Width(40)))
        {
            ToggleSort("category");
        }

        if (GUILayout.Button("★", EditorStyles.toolbarButton, GUILayout.Width(25)))
        {
            ToggleSort("rarity");
        }

        if (GUILayout.Button("$", EditorStyles.toolbarButton, GUILayout.Width(25)))
        {
            ToggleSort("price");
        }

        EditorGUILayout.EndHorizontal();

        // Item list
        _listScrollPos = EditorGUILayout.BeginScrollView(_listScrollPos);

        foreach (var item in _filteredItems)
        {
            DrawItemListEntry(item);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawItemListEntry(ItemEntry item)
    {
        bool isSelected = item == _selectedItem;

        EditorGUILayout.BeginHorizontal(isSelected ? EditorStyles.helpBox : GUIStyle.none);

        // Rarity color bar
        EditorGUI.DrawRect(GUILayoutUtility.GetRect(4, 20, GUILayout.Width(4)), GetRarityColor(item.rarity));

        // Icon
        if (item.icon != null)
        {
            GUILayout.Label(item.icon.texture, GUILayout.Width(20), GUILayout.Height(20));
        }
        else
        {
            GUILayout.Label(GetCategoryIcon(item.category), GUILayout.Width(20));
        }

        // Name
        if (GUILayout.Button(item.name, EditorStyles.label))
        {
            _selectedItem = item;
        }

        // Dirty indicator
        if (item.isDirty)
        {
            GUILayout.Label("*", GUILayout.Width(10));
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSplitter()
    {
        Rect splitterRect = GUILayoutUtility.GetRect(5f, position.height - 50);
        EditorGUI.DrawRect(splitterRect, new Color(0.2f, 0.2f, 0.2f));

        EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

        if (Event.current.type == EventType.MouseDown && splitterRect.Contains(Event.current.mousePosition))
        {
            _isDraggingSplitter = true;
        }

        if (_isDraggingSplitter)
        {
            _listWidth = Mathf.Clamp(Event.current.mousePosition.x, 200f, position.width - 400f);
            Repaint();
        }

        if (Event.current.type == EventType.MouseUp)
        {
            _isDraggingSplitter = false;
        }
    }

    private void DrawDetailPanel()
    {
        if (_selectedItem == null)
        {
            EditorGUILayout.HelpBox("Select an item to view/edit its details", MessageType.Info);
            return;
        }

        // Item header
        EditorGUILayout.BeginHorizontal();

        if (_selectedItem.icon != null)
        {
            GUILayout.Label(_selectedItem.icon.texture, GUILayout.Width(64), GUILayout.Height(64));
        }

        EditorGUILayout.BeginVertical();

        GUI.backgroundColor = GetRarityColor(_selectedItem.rarity);
        EditorGUILayout.LabelField(_selectedItem.name, new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 });
        GUI.backgroundColor = Color.white;

        EditorGUILayout.LabelField($"{_selectedItem.category} • {_selectedItem.rarity} ({(int)_selectedItem.rarity}★)");
        EditorGUILayout.LabelField($"ID: {_selectedItem.id}", EditorStyles.miniLabel);

        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Tabs
        _detailTab = GUILayout.Toolbar(_detailTab, DETAIL_TABS);

        _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos);

        EditorGUI.BeginChangeCheck();

        switch (_detailTab)
        {
            case 0: DrawBasicTab(); break;
            case 1: DrawStatsTab(); break;
            case 2: DrawCraftingTab(); break;
            case 3: DrawDropsTab(); break;
            case 4: DrawRawTab(); break;
        }

        if (EditorGUI.EndChangeCheck())
        {
            _selectedItem.isDirty = true;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawBasicTab()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        _selectedItem.id = EditorGUILayout.TextField("ID", _selectedItem.id);
        _selectedItem.name = EditorGUILayout.TextField("Name", _selectedItem.name);
        _selectedItem.category = (ItemCategory)EditorGUILayout.EnumPopup("Category", _selectedItem.category);
        _selectedItem.rarity = (ItemRarity)EditorGUILayout.EnumPopup("Rarity", _selectedItem.rarity);

        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("Description");
        _selectedItem.description = EditorGUILayout.TextArea(_selectedItem.description, GUILayout.Height(60));

        EditorGUILayout.Space(5);

        _selectedItem.icon = (Sprite)EditorGUILayout.ObjectField("Icon", _selectedItem.icon, typeof(Sprite), false);
        _selectedItem.stackSize = EditorGUILayout.IntField("Stack Size", _selectedItem.stackSize);

        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("Pricing", EditorStyles.boldLabel);
        _selectedItem.buyPrice = EditorGUILayout.IntField("Buy Price", _selectedItem.buyPrice);
        _selectedItem.sellPrice = EditorGUILayout.IntField("Sell Price", _selectedItem.sellPrice);

        // Auto-calculate sell price
        if (GUILayout.Button("Auto Sell (50% of Buy)"))
        {
            _selectedItem.sellPrice = _selectedItem.buyPrice / 2;
        }

        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("Flags", EditorStyles.boldLabel);
        _selectedItem.isQuestItem = EditorGUILayout.Toggle("Quest Item", _selectedItem.isQuestItem);
        _selectedItem.isTradeable = EditorGUILayout.Toggle("Tradeable", _selectedItem.isTradeable);

        EditorGUILayout.EndVertical();
    }

    private void DrawStatsTab()
    {
        bool isEquipment = _selectedItem.category == ItemCategory.Weapon ||
                          _selectedItem.category == ItemCategory.Armor ||
                          _selectedItem.category == ItemCategory.Accessory;

        bool isConsumable = _selectedItem.category == ItemCategory.Consumable;

        if (isEquipment)
        {
            EditorGUILayout.LabelField("Equipment Stats", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _selectedItem.attack = EditorGUILayout.IntField("Attack", _selectedItem.attack);
            _selectedItem.defense = EditorGUILayout.IntField("Defense", _selectedItem.defense);
            _selectedItem.health = EditorGUILayout.IntField("Health", _selectedItem.health);

            EditorGUILayout.Space(5);

            _selectedItem.critRate = EditorGUILayout.Slider("Crit Rate %", _selectedItem.critRate, 0f, 100f);
            _selectedItem.critDamage = EditorGUILayout.Slider("Crit Damage %", _selectedItem.critDamage, 0f, 300f);

            EditorGUILayout.EndVertical();

            // Stat visualization
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Stat Preview", EditorStyles.boldLabel);

            DrawStatBar("ATK", _selectedItem.attack, 500, Color.red);
            DrawStatBar("DEF", _selectedItem.defense, 500, Color.blue);
            DrawStatBar("HP", _selectedItem.health, 5000, Color.green);
        }

        if (isConsumable)
        {
            EditorGUILayout.LabelField("Consumable Effects", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _selectedItem.healAmount = EditorGUILayout.IntField("Heal Amount", _selectedItem.healAmount);
            _selectedItem.manaRestore = EditorGUILayout.IntField("Mana Restore", _selectedItem.manaRestore);
            _selectedItem.duration = EditorGUILayout.FloatField("Effect Duration (s)", _selectedItem.duration);
            _selectedItem.effectId = EditorGUILayout.TextField("Effect ID", _selectedItem.effectId);

            EditorGUILayout.EndVertical();
        }

        if (!isEquipment && !isConsumable)
        {
            EditorGUILayout.HelpBox("This item category doesn't have stats.", MessageType.Info);
        }
    }

    private void DrawStatBar(string label, float value, float max, Color color)
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(label, GUILayout.Width(40));

        Rect barRect = GUILayoutUtility.GetRect(150, 16);
        EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f));

        float fillWidth = Mathf.Clamp01(value / max) * barRect.width;
        EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, fillWidth, barRect.height), color);

        EditorGUILayout.LabelField(value.ToString(), GUILayout.Width(50));

        EditorGUILayout.EndHorizontal();
    }

    private void DrawCraftingTab()
    {
        EditorGUILayout.LabelField("Crafting Recipe", EditorStyles.boldLabel);

        if (GUILayout.Button("+ Add Ingredient"))
        {
            _selectedItem.craftingRecipe.Add(new CraftingIngredient());
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        for (int i = 0; i < _selectedItem.craftingRecipe.Count; i++)
        {
            var ingredient = _selectedItem.craftingRecipe[i];

            EditorGUILayout.BeginHorizontal();

            ingredient.itemId = EditorGUILayout.TextField(ingredient.itemId, GUILayout.Width(150));
            ingredient.amount = EditorGUILayout.IntField(ingredient.amount, GUILayout.Width(50));

            // Find item name
            var foundItem = _items.FirstOrDefault(it => it.id == ingredient.itemId);
            if (foundItem != null)
            {
                EditorGUILayout.LabelField($"({foundItem.name})", EditorStyles.miniLabel);
            }

            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                _selectedItem.craftingRecipe.RemoveAt(i);
                break;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (_selectedItem.craftingRecipe.Count == 0)
        {
            EditorGUILayout.LabelField("No crafting recipe defined", EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.EndVertical();

        // Crafting cost summary
        if (_selectedItem.craftingRecipe.Count > 0)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Crafting Cost Summary", EditorStyles.boldLabel);

            int totalCost = 0;
            foreach (var ingredient in _selectedItem.craftingRecipe)
            {
                var ingItem = _items.FirstOrDefault(it => it.id == ingredient.itemId);
                if (ingItem != null)
                {
                    totalCost += ingItem.buyPrice * ingredient.amount;
                }
            }

            EditorGUILayout.LabelField($"Total material cost: {totalCost:N0} gold");
            EditorGUILayout.LabelField($"Profit margin: {_selectedItem.sellPrice - totalCost:N0} gold");
        }
    }

    private void DrawDropsTab()
    {
        EditorGUILayout.LabelField("Drop Sources", EditorStyles.boldLabel);

        if (GUILayout.Button("+ Add Drop Source"))
        {
            _selectedItem.dropSources.Add(new DropSource());
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        for (int i = 0; i < _selectedItem.dropSources.Count; i++)
        {
            var source = _selectedItem.dropSources[i];

            EditorGUILayout.BeginHorizontal();

            source.sourceId = EditorGUILayout.TextField(source.sourceId, GUILayout.Width(100));
            source.sourceName = EditorGUILayout.TextField(source.sourceName, GUILayout.Width(120));
            source.dropRate = EditorGUILayout.Slider(source.dropRate, 0f, 100f, GUILayout.Width(150));
            EditorGUILayout.LabelField("%", GUILayout.Width(20));

            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                _selectedItem.dropSources.RemoveAt(i);
                break;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (_selectedItem.dropSources.Count == 0)
        {
            EditorGUILayout.LabelField("No drop sources defined", EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawRawTab()
    {
        EditorGUILayout.LabelField("Raw JSON Data", EditorStyles.boldLabel);

        string json = JsonUtility.ToJson(_selectedItem, true);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.TextArea(json, GUILayout.Height(300));
        EditorGUILayout.EndVertical();

        if (GUILayout.Button("Copy to Clipboard"))
        {
            EditorGUIUtility.systemCopyBuffer = json;
        }
    }

    private void DrawStatusBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        EditorGUILayout.LabelField($"Total: {_totalItems} items | Showing: {_filteredItems.Count}");

        GUILayout.FlexibleSpace();

        // Category breakdown
        foreach (var kvp in _categoryCount)
        {
            EditorGUILayout.LabelField($"{kvp.Key}: {kvp.Value}", EditorStyles.miniLabel, GUILayout.Width(80));
        }

        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region Logic

    private void LoadItemsFromProject()
    {
        _items.Clear();

        // Find all ItemData ScriptableObjects
        string[] guids = AssetDatabase.FindAssets("t:ItemData");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemData itemData = AssetDatabase.LoadAssetAtPath<ItemData>(path);

            if (itemData != null)
            {
                _items.Add(ConvertFromItemData(itemData, path));
            }
        }

        // Also create some dummy items if none found
        if (_items.Count == 0)
        {
            CreateSampleItems();
        }

        UpdateStatistics();
    }

    private ItemEntry ConvertFromItemData(ItemData data, string path)
    {
        return new ItemEntry
        {
            id = data.itemId ?? data.displayName.ToLower().Replace(" ", "_"),
            name = data.displayName,
            category = ConvertCategory(data.category),
            rarity = ConvertRarity(data.rarity),
            description = data.description,
            icon = data.icon,
            stackSize = data.maxStackSize,
            buyPrice = data.buyPrice,
            sellPrice = data.sellPrice
        };
    }

    private ItemCategory ConvertCategory(global::ItemCategory category)
    {
        switch (category)
        {
            case global::ItemCategory.Weapon: return ItemCategory.Weapon;
            case global::ItemCategory.Armor: return ItemCategory.Armor;
            case global::ItemCategory.Consumable: return ItemCategory.Consumable;
            case global::ItemCategory.Quest: return ItemCategory.QuestItem;
            case global::ItemCategory.Material: return ItemCategory.Material;
            default: return ItemCategory.Material;
        }
    }

    private ItemRarity ConvertRarity(global::ItemRarity rarity)
    {
        switch (rarity)
        {
            case global::ItemRarity.Common: return ItemRarity.Common;
            case global::ItemRarity.Uncommon: return ItemRarity.Uncommon;
            case global::ItemRarity.Rare: return ItemRarity.Rare;
            case global::ItemRarity.Epic: return ItemRarity.Epic;
            case global::ItemRarity.Legendary: return ItemRarity.Legendary;
            default: return ItemRarity.Common;
        }
    }

    private void CreateSampleItems()
    {
        _items.Add(new ItemEntry { id = "sword_iron", name = "Iron Sword", category = ItemCategory.Weapon, rarity = ItemRarity.Common, attack = 50, buyPrice = 100 });
        _items.Add(new ItemEntry { id = "sword_steel", name = "Steel Sword", category = ItemCategory.Weapon, rarity = ItemRarity.Uncommon, attack = 100, buyPrice = 300 });
        _items.Add(new ItemEntry { id = "sword_dragon", name = "Dragon Slayer", category = ItemCategory.Weapon, rarity = ItemRarity.Legendary, attack = 500, critRate = 15, critDamage = 50, buyPrice = 10000 });

        _items.Add(new ItemEntry { id = "armor_leather", name = "Leather Armor", category = ItemCategory.Armor, rarity = ItemRarity.Common, defense = 30, buyPrice = 80 });
        _items.Add(new ItemEntry { id = "armor_plate", name = "Plate Armor", category = ItemCategory.Armor, rarity = ItemRarity.Rare, defense = 150, health = 500, buyPrice = 1500 });

        _items.Add(new ItemEntry { id = "potion_hp", name = "Health Potion", category = ItemCategory.Consumable, rarity = ItemRarity.Common, healAmount = 100, buyPrice = 20 });
        _items.Add(new ItemEntry { id = "potion_mp", name = "Mana Potion", category = ItemCategory.Consumable, rarity = ItemRarity.Common, manaRestore = 50, buyPrice = 25 });

        _items.Add(new ItemEntry { id = "mat_iron", name = "Iron Ore", category = ItemCategory.Material, rarity = ItemRarity.Common, buyPrice = 5 });
        _items.Add(new ItemEntry { id = "mat_crystal", name = "Magic Crystal", category = ItemCategory.Material, rarity = ItemRarity.Rare, buyPrice = 100 });

        _items.Add(new ItemEntry { id = "quest_letter", name = "Mysterious Letter", category = ItemCategory.QuestItem, rarity = ItemRarity.Common, isQuestItem = true, isTradeable = false });
    }

    private void ApplyFilters()
    {
        _filteredItems = _items.Where(item =>
        {
            // Search query
            if (!string.IsNullOrEmpty(_searchQuery))
            {
                if (!item.name.ToLower().Contains(_searchQuery.ToLower()) &&
                    !item.id.ToLower().Contains(_searchQuery.ToLower()))
                {
                    return false;
                }
            }

            // Category filter
            if (_filterCategory.HasValue && item.category != _filterCategory.Value)
            {
                return false;
            }

            // Rarity filter
            if (_filterRarity.HasValue && item.rarity != _filterRarity.Value)
            {
                return false;
            }

            // Quest items only
            if (_showQuestItemsOnly && !item.isQuestItem)
            {
                return false;
            }

            return true;
        }).ToList();

        // Apply sorting
        ApplySort();
    }

    private void ToggleSort(string field)
    {
        if (_sortField == field)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortField = field;
            _sortAscending = true;
        }

        ApplySort();
    }

    private void ApplySort()
    {
        switch (_sortField)
        {
            case "name":
                _filteredItems = _sortAscending
                    ? _filteredItems.OrderBy(i => i.name).ToList()
                    : _filteredItems.OrderByDescending(i => i.name).ToList();
                break;

            case "category":
                _filteredItems = _sortAscending
                    ? _filteredItems.OrderBy(i => i.category).ToList()
                    : _filteredItems.OrderByDescending(i => i.category).ToList();
                break;

            case "rarity":
                _filteredItems = _sortAscending
                    ? _filteredItems.OrderBy(i => i.rarity).ToList()
                    : _filteredItems.OrderByDescending(i => i.rarity).ToList();
                break;

            case "price":
                _filteredItems = _sortAscending
                    ? _filteredItems.OrderBy(i => i.buyPrice).ToList()
                    : _filteredItems.OrderByDescending(i => i.buyPrice).ToList();
                break;
        }
    }

    private void UpdateStatistics()
    {
        _totalItems = _items.Count;
        _categoryCount.Clear();

        foreach (ItemCategory cat in System.Enum.GetValues(typeof(ItemCategory)))
        {
            _categoryCount[cat] = _items.Count(i => i.category == cat);
        }
    }

    private void CreateNewItem()
    {
        var newItem = new ItemEntry
        {
            id = $"item_{_items.Count + 1}",
            name = "New Item",
            category = ItemCategory.Material,
            rarity = ItemRarity.Common,
            isDirty = true
        };

        _items.Add(newItem);
        _selectedItem = newItem;
        ApplyFilters();
        UpdateStatistics();
    }

    private void DuplicateSelected()
    {
        if (_selectedItem == null) return;

        var copy = JsonUtility.FromJson<ItemEntry>(JsonUtility.ToJson(_selectedItem));
        copy.id = _selectedItem.id + "_copy";
        copy.name = _selectedItem.name + " (Copy)";
        copy.isDirty = true;

        _items.Add(copy);
        _selectedItem = copy;
        ApplyFilters();
        UpdateStatistics();
    }

    private void DeleteSelected()
    {
        if (_selectedItem == null) return;

        if (EditorUtility.DisplayDialog("Delete Item",
            $"Are you sure you want to delete '{_selectedItem.name}'?", "Delete", "Cancel"))
        {
            _items.Remove(_selectedItem);
            _selectedItem = null;
            ApplyFilters();
            UpdateStatistics();
        }
    }

    private void ApplyBatchPriceChange()
    {
        foreach (var item in _filteredItems)
        {
            item.buyPrice = Mathf.RoundToInt(item.buyPrice * _batchPriceMultiplier);
            item.sellPrice = Mathf.RoundToInt(item.sellPrice * _batchPriceMultiplier);
            item.isDirty = true;
        }

        Debug.Log($"[ItemDatabaseEditor] Applied price multiplier {_batchPriceMultiplier}x to {_filteredItems.Count} items");
    }

    private void ApplyBatchRarityChange()
    {
        foreach (var item in _filteredItems)
        {
            int newRarity = Mathf.Clamp((int)item.rarity + _batchRarityChange, 1, 6);
            item.rarity = (ItemRarity)newRarity;
            item.isDirty = true;
        }

        Debug.Log($"[ItemDatabaseEditor] Changed rarity by {_batchRarityChange} for {_filteredItems.Count} items");
    }

    private void SaveAllItems()
    {
        int saved = 0;
        foreach (var item in _items.Where(i => i.isDirty))
        {
            // Would save to ScriptableObject here
            item.isDirty = false;
            saved++;
        }

        Debug.Log($"[ItemDatabaseEditor] Saved {saved} items");
    }

    private void ImportFromCSV()
    {
        string path = EditorUtility.OpenFilePanel("Import Items CSV", "", "csv");
        if (string.IsNullOrEmpty(path)) return;

        // Parse CSV and create items
        Debug.Log($"[ItemDatabaseEditor] Import from {path}");
    }

    private void ExportToCSV()
    {
        string path = EditorUtility.SaveFilePanel("Export Items CSV", "", "items", "csv");
        if (string.IsNullOrEmpty(path)) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ID,Name,Category,Rarity,BuyPrice,SellPrice,ATK,DEF,HP");

        foreach (var item in _items)
        {
            sb.AppendLine($"{item.id},{item.name},{item.category},{item.rarity},{item.buyPrice},{item.sellPrice},{item.attack},{item.defense},{item.health}");
        }

        System.IO.File.WriteAllText(path, sb.ToString());
        Debug.Log($"[ItemDatabaseEditor] Exported {_items.Count} items to {path}");
    }

    #endregion

    #region Helpers

    private Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return Color.gray;
            case ItemRarity.Uncommon: return Color.green;
            case ItemRarity.Rare: return new Color(0.3f, 0.5f, 1f);
            case ItemRarity.Epic: return new Color(0.6f, 0.2f, 0.8f);
            case ItemRarity.Legendary: return new Color(1f, 0.7f, 0.2f);
            case ItemRarity.Mythic: return new Color(1f, 0.3f, 0.3f);
            default: return Color.white;
        }
    }

    private string GetCategoryIcon(ItemCategory category)
    {
        switch (category)
        {
            case ItemCategory.Weapon: return "⚔";
            case ItemCategory.Armor: return "🛡";
            case ItemCategory.Accessory: return "💍";
            case ItemCategory.Consumable: return "🧪";
            case ItemCategory.Material: return "📦";
            case ItemCategory.QuestItem: return "📜";
            case ItemCategory.Currency: return "💰";
            case ItemCategory.KeyItem: return "🔑";
            default: return "?";
        }
    }

    #endregion
}
