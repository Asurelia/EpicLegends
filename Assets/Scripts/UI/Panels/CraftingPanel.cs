using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel de crafting pour la fabrication d'objets.
/// </summary>
public class CraftingPanel : UIPanel
{
    #region Serialized Fields

    [Header("Recipe List")]
    [SerializeField] private Transform _recipeListContainer;
    [SerializeField] private GameObject _recipeEntryPrefab;
    [SerializeField] private ScrollRect _recipeScrollRect;

    [Header("Category Filters")]
    [SerializeField] private Transform _categoryButtonContainer;
    [SerializeField] private GameObject _categoryButtonPrefab;

    [Header("Recipe Details")]
    [SerializeField] private GameObject _detailsPanel;
    [SerializeField] private Image _recipeIcon;
    [SerializeField] private TextMeshProUGUI _recipeName;
    [SerializeField] private TextMeshProUGUI _recipeDescription;
    [SerializeField] private Transform _ingredientListContainer;
    [SerializeField] private GameObject _ingredientEntryPrefab;
    [SerializeField] private TextMeshProUGUI _craftTimeText;
    [SerializeField] private TextMeshProUGUI _outputText;

    [Header("Crafting")]
    [SerializeField] private Button _craftButton;
    [SerializeField] private Button _craftAllButton;
    [SerializeField] private Slider _craftProgressBar;
    [SerializeField] private TextMeshProUGUI _craftProgressText;

    [Header("Search")]
    [SerializeField] private TMP_InputField _searchInput;

    [Header("Data")]
    [SerializeField] private CraftingRecipeData[] _allRecipes;

    #endregion

    #region Private Fields

    private List<CraftingRecipeData> _filteredRecipes = new List<CraftingRecipeData>();
    private List<CraftingRecipeEntryUI> _recipeEntries = new List<CraftingRecipeEntryUI>();
    private CraftingRecipeData _selectedRecipe;
    private RecipeCategory? _currentCategory = null;
    private string _searchText = "";
    private bool _isCrafting = false;
    private float _craftProgress = 0f;
    private Inventory _playerInventory;
    private CraftingManager _craftingManager;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (_craftButton != null) _craftButton.onClick.AddListener(OnCraftClicked);
        if (_craftAllButton != null) _craftAllButton.onClick.AddListener(OnCraftAllClicked);
        if (_searchInput != null) _searchInput.onValueChanged.AddListener(OnSearchChanged);

        CreateCategoryButtons();
    }

    private void Update()
    {
        if (_isCrafting)
        {
            UpdateCraftProgress();
        }
    }

    #endregion

    #region UIPanel Overrides

    public override void Show()
    {
        base.Show();

        var player = GameManager.Instance?.Player;
        if (player != null)
        {
            _playerInventory = player.GetComponent<Inventory>();
        }

        _craftingManager = CraftingManager.Instance;

        RefreshRecipeList();
        HideDetails();
    }

    public override void Hide()
    {
        base.Hide();
        _selectedRecipe = null;
        _isCrafting = false;
    }

    #endregion

    #region Private Methods - Category Buttons

    private void CreateCategoryButtons()
    {
        if (_categoryButtonContainer == null || _categoryButtonPrefab == null) return;

        // Bouton "Tous"
        CreateCategoryButton("All", null);

        // Un bouton par categorie
        foreach (RecipeCategory category in System.Enum.GetValues(typeof(RecipeCategory)))
        {
            CreateCategoryButton(category.ToString(), category);
        }
    }

    private void CreateCategoryButton(string label, RecipeCategory? category)
    {
        var buttonObj = Instantiate(_categoryButtonPrefab, _categoryButtonContainer);
        var button = buttonObj.GetComponent<Button>();
        var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();

        if (text != null) text.text = label;
        if (button != null)
        {
            button.onClick.AddListener(() => SetCategory(category));
        }
    }

    private void SetCategory(RecipeCategory? category)
    {
        _currentCategory = category;
        RefreshRecipeList();
    }

    #endregion

    #region Private Methods - Recipe List

    private void RefreshRecipeList()
    {
        FilterRecipes();
        PopulateRecipeList();
    }

    private void FilterRecipes()
    {
        _filteredRecipes.Clear();

        if (_allRecipes == null) return;

        foreach (var recipe in _allRecipes)
        {
            if (recipe == null) continue;

            // Filtre par categorie
            if (_currentCategory.HasValue && recipe.category != _currentCategory.Value)
                continue;

            // Filtre par recherche
            if (!string.IsNullOrEmpty(_searchText))
            {
                if (!recipe.recipeName.ToLower().Contains(_searchText.ToLower()))
                    continue;
            }

            // Verifier si debloquee
            if (!IsRecipeUnlocked(recipe))
                continue;

            _filteredRecipes.Add(recipe);
        }
    }

    private void PopulateRecipeList()
    {
        // Nettoyer
        foreach (var entry in _recipeEntries)
        {
            if (entry != null) Destroy(entry.gameObject);
        }
        _recipeEntries.Clear();

        if (_recipeListContainer == null || _recipeEntryPrefab == null) return;

        foreach (var recipe in _filteredRecipes)
        {
            var entryObj = Instantiate(_recipeEntryPrefab, _recipeListContainer);
            var entry = entryObj.GetComponent<CraftingRecipeEntryUI>();
            if (entry == null) entry = entryObj.AddComponent<CraftingRecipeEntryUI>();

            bool canCraft = CanCraftRecipe(recipe);
            entry.Initialize(recipe, canCraft, () => SelectRecipe(recipe));
            _recipeEntries.Add(entry);
        }
    }

    private bool IsRecipeUnlocked(CraftingRecipeData recipe)
    {
        if (recipe.unlockedByDefault) return true;

        // Verifier niveau
        var playerStats = GameManager.Instance?.PlayerStats;
        if (playerStats != null && playerStats.Level < recipe.requiredPlayerLevel)
            return false;

        // Verifier prerequis
        if (recipe.prerequisites != null)
        {
            foreach (var prereq in recipe.prerequisites)
            {
                // TODO: Verifier si la recette prerequise a ete craftee
            }
        }

        return true;
    }

    private bool CanCraftRecipe(CraftingRecipeData recipe)
    {
        if (recipe == null) return false;

        // Pour l'instant, verifier juste si on a les ressources via l'inventaire
        // TODO: Implementer IResourceContainer sur Inventory ou utiliser CraftingManager
        return true;
    }

    #endregion

    #region Private Methods - Selection

    private void SelectRecipe(CraftingRecipeData recipe)
    {
        _selectedRecipe = recipe;

        // Deselectionner les autres
        foreach (var entry in _recipeEntries)
        {
            entry.SetSelected(entry.Recipe == recipe);
        }

        ShowDetails(recipe);
    }

    private void ShowDetails(CraftingRecipeData recipe)
    {
        if (_detailsPanel == null || recipe == null) return;

        _detailsPanel.SetActive(true);

        if (_recipeIcon != null) _recipeIcon.sprite = recipe.icon;
        if (_recipeName != null) _recipeName.text = recipe.recipeName;
        if (_recipeDescription != null) _recipeDescription.text = recipe.description;
        if (_craftTimeText != null) _craftTimeText.text = $"Time: {recipe.craftTime:F1}s";

        // Output
        if (_outputText != null)
        {
            if (recipe.outputItem != null)
                _outputText.text = $"Produces: {recipe.outputAmount}x {recipe.outputItem.displayName}";
            else
                _outputText.text = $"Produces: {recipe.outputAmount}x {recipe.outputResourceType}";
        }

        // Ingredients
        PopulateIngredients(recipe);

        // Boutons
        bool canCraft = CanCraftRecipe(recipe);
        if (_craftButton != null) _craftButton.interactable = canCraft && !_isCrafting;
        if (_craftAllButton != null) _craftAllButton.interactable = canCraft && !_isCrafting;
    }

    private void PopulateIngredients(CraftingRecipeData recipe)
    {
        if (_ingredientListContainer == null) return;

        // Nettoyer
        foreach (Transform child in _ingredientListContainer)
        {
            Destroy(child.gameObject);
        }

        if (recipe.ingredients == null || _ingredientEntryPrefab == null) return;

        foreach (var ingredient in recipe.ingredients)
        {
            var entryObj = Instantiate(_ingredientEntryPrefab, _ingredientListContainer);
            var text = entryObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                // TODO: Obtenir le nombre actuel de ressources
                int currentAmount = 0; // GetResourceCount(ingredient.resourceType);
                string color = currentAmount >= ingredient.amount ? "white" : "red";
                text.text = $"<color={color}>{ingredient.resourceType}: {currentAmount}/{ingredient.amount}</color>";
            }
        }
    }

    private void HideDetails()
    {
        if (_detailsPanel != null)
        {
            _detailsPanel.SetActive(false);
        }
        _selectedRecipe = null;
    }

    #endregion

    #region Private Methods - Crafting

    private void OnCraftClicked()
    {
        if (_selectedRecipe == null || _isCrafting) return;

        StartCrafting(_selectedRecipe);
    }

    private void OnCraftAllClicked()
    {
        if (_selectedRecipe == null || _isCrafting) return;

        // TODO: Calculer combien on peut crafter et lancer une queue
        StartCrafting(_selectedRecipe);
    }

    private void StartCrafting(CraftingRecipeData recipe)
    {
        _isCrafting = true;
        _craftProgress = 0f;

        if (_craftProgressBar != null)
        {
            _craftProgressBar.gameObject.SetActive(true);
            _craftProgressBar.value = 0f;
        }

        if (_craftButton != null) _craftButton.interactable = false;
        if (_craftAllButton != null) _craftAllButton.interactable = false;

        Debug.Log($"[CraftingPanel] Starting craft: {recipe.recipeName}");
    }

    private void UpdateCraftProgress()
    {
        if (_selectedRecipe == null)
        {
            _isCrafting = false;
            return;
        }

        _craftProgress += Time.deltaTime;
        float progress = _craftProgress / _selectedRecipe.craftTime;

        if (_craftProgressBar != null) _craftProgressBar.value = progress;
        if (_craftProgressText != null)
            _craftProgressText.text = $"{_craftProgress:F1}s / {_selectedRecipe.craftTime:F1}s";

        if (_craftProgress >= _selectedRecipe.craftTime)
        {
            CompleteCrafting();
        }
    }

    private void CompleteCrafting()
    {
        _isCrafting = false;

        if (_craftProgressBar != null) _craftProgressBar.gameObject.SetActive(false);

        // Produire l'output
        if (_selectedRecipe.outputItem != null && _playerInventory != null)
        {
            _playerInventory.AddItem(_selectedRecipe.outputItem, _selectedRecipe.outputAmount);
            Debug.Log($"[CraftingPanel] Crafted {_selectedRecipe.outputAmount}x {_selectedRecipe.outputItem.displayName}");
        }

        // Reactiver les boutons
        if (_craftButton != null) _craftButton.interactable = true;
        if (_craftAllButton != null) _craftAllButton.interactable = true;

        // Rafraichir la liste (ressources consommees)
        RefreshRecipeList();
        if (_selectedRecipe != null) ShowDetails(_selectedRecipe);
    }

    private void OnSearchChanged(string text)
    {
        _searchText = text;
        RefreshRecipeList();
    }

    #endregion
}

/// <summary>
/// UI d'une entree de recette dans la liste.
/// </summary>
public class CraftingRecipeEntryUI : MonoBehaviour
{
    [SerializeField] private Image _icon;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private Color _canCraftColor = Color.white;
    [SerializeField] private Color _cannotCraftColor = new Color(0.5f, 0.5f, 0.5f);
    [SerializeField] private Color _selectedColor = new Color(1f, 0.8f, 0.3f);
    [SerializeField] private Button _button;

    public CraftingRecipeData Recipe { get; private set; }
    private bool _isSelected;
    private bool _canCraft;

    public void Initialize(CraftingRecipeData recipe, bool canCraft, System.Action onClick)
    {
        Recipe = recipe;
        _canCraft = canCraft;

        if (_icon != null) _icon.sprite = recipe.icon;
        if (_nameText != null) _nameText.text = recipe.recipeName;

        UpdateVisual();

        if (_button == null) _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => onClick?.Invoke());
        }
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (_backgroundImage != null)
        {
            if (_isSelected)
                _backgroundImage.color = _selectedColor;
            else if (_canCraft)
                _backgroundImage.color = _canCraftColor;
            else
                _backgroundImage.color = _cannotCraftColor;
        }
    }
}
