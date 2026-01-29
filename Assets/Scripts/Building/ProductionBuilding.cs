using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Composant pour les batiments de production (workbench, furnace, etc.).
/// Gere le crafting et la production de ressources.
/// </summary>
public class ProductionBuilding : MonoBehaviour
{
    #region Fields

    [Header("Configuration")]
    [SerializeField] private BuildingSubCategory _stationType = BuildingSubCategory.Workbench;
    [SerializeField] private int _stationLevel = 1;
    [SerializeField] private int _queueSize = 5;

    [Header("Input/Output")]
    [SerializeField] private StorageBuilding _inputStorage;
    [SerializeField] private StorageBuilding _outputStorage;

    [Header("Energie")]
    [SerializeField] private bool _requiresEnergy = false;
    [SerializeField] private float _energyConsumption = 0f;
    [SerializeField] private bool _hasPower = true;

    // Etat de production
    private CraftingRecipeData _currentRecipe;
    private float _craftProgress = 0f;
    private bool _isProducing = false;
    private Queue<CraftingRecipeData> _craftQueue = new Queue<CraftingRecipeData>();

    // Recettes disponibles
    private List<CraftingRecipeData> _availableRecipes = new List<CraftingRecipeData>();

    #endregion

    #region Events

    public event Action<CraftingRecipeData> OnCraftStarted;
    public event Action<CraftingRecipeData> OnCraftCompleted;
    public event Action<float> OnCraftProgress;
    public event Action OnQueueChanged;

    #endregion

    #region Properties

    /// <summary>Type de station.</summary>
    public BuildingSubCategory StationType => _stationType;

    /// <summary>Niveau de la station.</summary>
    public int StationLevel => _stationLevel;

    /// <summary>En production?</summary>
    public bool IsProducing => _isProducing;

    /// <summary>Recette en cours.</summary>
    public CraftingRecipeData CurrentRecipe => _currentRecipe;

    /// <summary>Progression (0-1).</summary>
    public float CraftProgress => _craftProgress;

    /// <summary>Taille de la queue.</summary>
    public int QueueSize => _queueSize;

    /// <summary>Items en queue.</summary>
    public int QueueCount => _craftQueue.Count;

    /// <summary>Queue pleine?</summary>
    public bool IsQueueFull => _craftQueue.Count >= _queueSize;

    /// <summary>A l'energie?</summary>
    public bool HasPower => !_requiresEnergy || _hasPower;

    /// <summary>Recettes disponibles.</summary>
    public IReadOnlyList<CraftingRecipeData> AvailableRecipes => _availableRecipes;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _craftQueue = new Queue<CraftingRecipeData>();
        _availableRecipes = new List<CraftingRecipeData>();
    }

    private void Update()
    {
        if (_isProducing && HasPower)
        {
            UpdateProduction(Time.deltaTime);
        }
        else if (!_isProducing && _craftQueue.Count > 0 && HasPower)
        {
            StartNextCraft();
        }
    }

    #endregion

    #region Public Methods - Recettes

    /// <summary>
    /// Ajoute des recettes disponibles.
    /// </summary>
    public void AddRecipes(CraftingRecipeData[] recipes)
    {
        if (recipes == null) return;

        foreach (var recipe in recipes)
        {
            if (recipe != null && !_availableRecipes.Contains(recipe))
            {
                _availableRecipes.Add(recipe);
            }
        }
    }

    /// <summary>
    /// Verifie si une recette est disponible a cette station.
    /// </summary>
    public bool CanCraftRecipe(CraftingRecipeData recipe)
    {
        if (recipe == null) return false;

        // Verifier le type de station
        if (recipe.requiredStation != _stationType) return false;

        // Verifier le niveau
        if (recipe.requiredStationLevel > _stationLevel) return false;

        // Verifier l'energie
        if (recipe.requiresEnergy && !HasPower) return false;

        return true;
    }

    /// <summary>
    /// Verifie si on a les ingredients.
    /// </summary>
    public bool HasIngredients(CraftingRecipeData recipe)
    {
        if (recipe == null || _inputStorage == null) return false;

        return recipe.HasIngredients(_inputStorage);
    }

    #endregion

    #region Public Methods - Production

    /// <summary>
    /// Ajoute une recette a la queue de production.
    /// </summary>
    public bool QueueRecipe(CraftingRecipeData recipe)
    {
        if (recipe == null) return false;
        if (!CanCraftRecipe(recipe)) return false;
        if (IsQueueFull) return false;

        _craftQueue.Enqueue(recipe);
        OnQueueChanged?.Invoke();

        if (!_isProducing)
        {
            StartNextCraft();
        }

        return true;
    }

    /// <summary>
    /// Demarre immediatement une production (bypass queue).
    /// </summary>
    public bool StartCraft(CraftingRecipeData recipe)
    {
        if (recipe == null) return false;
        if (!CanCraftRecipe(recipe)) return false;
        if (!HasIngredients(recipe)) return false;
        if (_isProducing) return false;

        // Consommer les ingredients
        if (_inputStorage != null)
        {
            if (!recipe.ConsumeIngredients(_inputStorage))
                return false;
        }

        _currentRecipe = recipe;
        _craftProgress = 0f;
        _isProducing = true;

        OnCraftStarted?.Invoke(recipe);

        return true;
    }

    /// <summary>
    /// Annule la production en cours.
    /// </summary>
    public bool CancelCraft()
    {
        if (!_isProducing) return false;

        // Rembourser les ingredients (partiellement selon la progression)
        if (_inputStorage != null && _currentRecipe != null)
        {
            foreach (var ingredient in _currentRecipe.ingredients)
            {
                int refund = Mathf.CeilToInt(ingredient.amount * (1f - _craftProgress));
                _inputStorage.AddResource(ingredient.resourceType, refund);
            }
        }

        _currentRecipe = null;
        _craftProgress = 0f;
        _isProducing = false;

        return true;
    }

    /// <summary>
    /// Vide la queue de production.
    /// </summary>
    public void ClearQueue()
    {
        _craftQueue.Clear();
        OnQueueChanged?.Invoke();
    }

    /// <summary>
    /// Retourne les recettes en queue.
    /// </summary>
    public CraftingRecipeData[] GetQueuedRecipes()
    {
        return _craftQueue.ToArray();
    }

    #endregion

    #region Public Methods - Energie

    /// <summary>
    /// Met a jour l'etat d'alimentation.
    /// </summary>
    public void SetPower(bool hasPower)
    {
        _hasPower = hasPower;
    }

    #endregion

    #region Private Methods

    private void UpdateProduction(float deltaTime)
    {
        if (_currentRecipe == null) return;

        _craftProgress += deltaTime / _currentRecipe.craftTime;
        OnCraftProgress?.Invoke(_craftProgress);

        if (_craftProgress >= 1f)
        {
            CompleteCraft();
        }
    }

    private void CompleteCraft()
    {
        if (_currentRecipe == null) return;

        // Produire le resultat
        if (_outputStorage != null)
        {
            if (_currentRecipe.outputItem != null)
            {
                // TODO: Ajouter l'item a l'inventaire
            }
            else
            {
                _outputStorage.AddResource(
                    _currentRecipe.outputResourceType,
                    _currentRecipe.outputAmount
                );
            }
        }

        var completedRecipe = _currentRecipe;

        _currentRecipe = null;
        _craftProgress = 0f;
        _isProducing = false;

        OnCraftCompleted?.Invoke(completedRecipe);

        // Demarrer le prochain craft de la queue
        if (_craftQueue.Count > 0)
        {
            StartNextCraft();
        }
    }

    private void StartNextCraft()
    {
        while (_craftQueue.Count > 0)
        {
            var nextRecipe = _craftQueue.Peek();

            if (HasIngredients(nextRecipe))
            {
                _craftQueue.Dequeue();
                OnQueueChanged?.Invoke();
                StartCraft(nextRecipe);
                return;
            }
            else
            {
                // Pas assez d'ingredients, garder dans la queue
                break;
            }
        }
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Configure la station de production.
    /// </summary>
    public void Configure(BuildingSubCategory stationType, int level, int queueSize)
    {
        _stationType = stationType;
        _stationLevel = level;
        _queueSize = queueSize;
    }

    /// <summary>
    /// Lie les storages d'entree/sortie.
    /// </summary>
    public void SetStorages(StorageBuilding input, StorageBuilding output)
    {
        _inputStorage = input;
        _outputStorage = output;
    }

    #endregion
}
