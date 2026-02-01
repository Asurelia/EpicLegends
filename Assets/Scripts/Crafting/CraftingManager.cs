using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire global du crafting.
/// </summary>
public class CraftingManager : MonoBehaviour
{
    #region Singleton

    public static CraftingManager Instance { get; private set; }

    #endregion

    #region Events

    public event Action<CraftingRecipeData> OnRecipeUnlocked;
    public event Action<CraftingRecipeData, int> OnItemCrafted; // recipe, quantity
    public event Action<CraftingRecipeData> OnCraftingStarted;
    public event Action<CraftingRecipeData> OnCraftingCompleted;
    public event Action<CraftingRecipeData> OnCraftingCancelled;

    #endregion

    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private CraftingRecipeData[] _allRecipes;

    [Header("Audio")]
    [SerializeField] private AudioClip _craftStartSound;
    [SerializeField] private AudioClip _craftCompleteSound;

    #endregion

    #region Private Fields

    private HashSet<string> _unlockedRecipes = new HashSet<string>();
    private Dictionary<string, int> _craftedCounts = new Dictionary<string, int>();
    private Queue<CraftingJob> _craftingQueue = new Queue<CraftingJob>();
    private CraftingJob _currentJob;
    private AudioSource _audioSource;

    #endregion

    #region Properties

    public bool IsCrafting => _currentJob != null;
    public CraftingJob CurrentJob => _currentJob;
    public int QueuedJobCount => _craftingQueue.Count;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        InitializeRecipes();
    }

    private void Update()
    {
        UpdateCrafting();
    }

    #endregion

    #region Initialization

    private void InitializeRecipes()
    {
        if (_allRecipes == null) return;

        foreach (var recipe in _allRecipes)
        {
            if (recipe != null && recipe.unlockedByDefault)
            {
                _unlockedRecipes.Add(recipe.recipeName);
            }
        }
    }

    #endregion

    #region Public Methods - Recipes

    /// <summary>
    /// Verifie si une recette est debloquee.
    /// </summary>
    public bool IsRecipeUnlocked(CraftingRecipeData recipe)
    {
        if (recipe == null) return false;
        if (recipe.unlockedByDefault) return true;
        return _unlockedRecipes.Contains(recipe.recipeName);
    }

    /// <summary>
    /// Debloque une recette.
    /// </summary>
    public void UnlockRecipe(CraftingRecipeData recipe)
    {
        if (recipe == null || IsRecipeUnlocked(recipe)) return;

        _unlockedRecipes.Add(recipe.recipeName);
        OnRecipeUnlocked?.Invoke(recipe);

        // Notify achievement system
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnRecipeLearned();
        }

        Debug.Log($"[CraftingManager] Recipe unlocked: {recipe.recipeName}");
    }

    /// <summary>
    /// Obtient le nombre de fois qu'une recette a ete craftee.
    /// </summary>
    public int GetCraftedCount(CraftingRecipeData recipe)
    {
        if (recipe == null) return 0;
        return _craftedCounts.TryGetValue(recipe.recipeName, out int count) ? count : 0;
    }

    /// <summary>
    /// Obtient toutes les recettes.
    /// </summary>
    public CraftingRecipeData[] GetAllRecipes()
    {
        return _allRecipes;
    }

    /// <summary>
    /// Obtient les recettes debloquees.
    /// </summary>
    public List<CraftingRecipeData> GetUnlockedRecipes()
    {
        var unlocked = new List<CraftingRecipeData>();
        if (_allRecipes == null) return unlocked;

        foreach (var recipe in _allRecipes)
        {
            if (recipe != null && IsRecipeUnlocked(recipe))
            {
                unlocked.Add(recipe);
            }
        }
        return unlocked;
    }

    /// <summary>
    /// Obtient les recettes par categorie.
    /// </summary>
    public List<CraftingRecipeData> GetRecipesByCategory(RecipeCategory category)
    {
        var recipes = new List<CraftingRecipeData>();
        if (_allRecipes == null) return recipes;

        foreach (var recipe in _allRecipes)
        {
            if (recipe != null && recipe.category == category && IsRecipeUnlocked(recipe))
            {
                recipes.Add(recipe);
            }
        }
        return recipes;
    }

    #endregion

    #region Public Methods - Crafting

    /// <summary>
    /// Verifie si une recette peut etre craftee.
    /// </summary>
    public bool CanCraft(CraftingRecipeData recipe, IResourceContainer resources)
    {
        if (recipe == null || resources == null) return false;
        if (!IsRecipeUnlocked(recipe)) return false;

        return recipe.HasIngredients(resources);
    }

    /// <summary>
    /// Verifie si une recette peut etre craftee avec le ResourceManager global.
    /// </summary>
    public bool CanCraft(CraftingRecipeData recipe)
    {
        if (recipe == null) return false;
        if (!IsRecipeUnlocked(recipe)) return false;

        if (ResourceManager.Instance == null)
        {
            Debug.LogWarning("[CraftingManager] ResourceManager not found");
            return false;
        }

        return recipe.HasIngredients(ResourceManager.Instance);
    }

    /// <summary>
    /// Obtient les ingredients manquants pour une recette.
    /// </summary>
    public ResourceCost[] GetMissingIngredients(CraftingRecipeData recipe)
    {
        if (recipe == null || recipe.ingredients == null) return new ResourceCost[0];

        var missing = new List<ResourceCost>();
        var resources = ResourceManager.Instance;

        if (resources == null) return recipe.ingredients;

        foreach (var ingredient in recipe.ingredients)
        {
            int have = resources.GetResourceCount(ingredient.resourceType);
            if (have < ingredient.amount)
            {
                missing.Add(new ResourceCost
                {
                    resourceType = ingredient.resourceType,
                    amount = ingredient.amount - have
                });
            }
        }

        return missing.ToArray();
    }

    /// <summary>
    /// Demarre le crafting d'une recette.
    /// </summary>
    public bool StartCrafting(CraftingRecipeData recipe, IResourceContainer resources, int quantity = 1)
    {
        if (!CanCraft(recipe, resources)) return false;

        // Consommer les ingredients
        for (int i = 0; i < quantity; i++)
        {
            if (!recipe.ConsumeIngredients(resources))
            {
                Debug.LogWarning($"[CraftingManager] Failed to consume ingredients for {recipe.recipeName}");
                break;
            }

            var job = new CraftingJob
            {
                recipe = recipe,
                startTime = Time.time,
                duration = recipe.craftTime
            };

            if (_currentJob == null)
            {
                _currentJob = job;
                OnCraftingStarted?.Invoke(recipe);
                PlaySound(_craftStartSound);
            }
            else
            {
                _craftingQueue.Enqueue(job);
            }
        }

        return true;
    }

    /// <summary>
    /// Demarre le crafting d'une recette avec le ResourceManager global.
    /// </summary>
    public bool StartCrafting(CraftingRecipeData recipe, int quantity = 1)
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogWarning("[CraftingManager] ResourceManager not found");
            return false;
        }

        return StartCrafting(recipe, ResourceManager.Instance, quantity);
    }

    /// <summary>
    /// Annule le crafting en cours.
    /// </summary>
    public void CancelCrafting()
    {
        if (_currentJob == null) return;

        var recipe = _currentJob.recipe;

        // TODO: Rembourser les ingredients?

        _currentJob = null;
        _craftingQueue.Clear();

        OnCraftingCancelled?.Invoke(recipe);
    }

    /// <summary>
    /// Obtient la progression du crafting actuel (0-1).
    /// </summary>
    public float GetCraftingProgress()
    {
        if (_currentJob == null) return 0f;
        return _currentJob.GetProgress();
    }

    #endregion

    #region Private Methods

    private void UpdateCrafting()
    {
        if (_currentJob == null) return;

        if (_currentJob.IsComplete())
        {
            CompleteCrafting();
        }
    }

    private void CompleteCrafting()
    {
        if (_currentJob == null) return;

        var recipe = _currentJob.recipe;

        // Produire l'output
        ProduceOutput(recipe);

        // Incrementer le compteur
        if (!_craftedCounts.ContainsKey(recipe.recipeName))
            _craftedCounts[recipe.recipeName] = 0;
        _craftedCounts[recipe.recipeName]++;

        PlaySound(_craftCompleteSound);
        OnCraftingCompleted?.Invoke(recipe);
        OnItemCrafted?.Invoke(recipe, recipe.outputAmount);

        // Achievement
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnItemCrafted();
        }

        Debug.Log($"[CraftingManager] Crafted: {recipe.recipeName}");

        // Passer au job suivant
        _currentJob = null;
        if (_craftingQueue.Count > 0)
        {
            _currentJob = _craftingQueue.Dequeue();
            OnCraftingStarted?.Invoke(_currentJob.recipe);
        }
    }

    private void ProduceOutput(CraftingRecipeData recipe)
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        // Si c'est un item
        if (recipe.outputItem != null)
        {
            var inventory = player.GetComponent<Inventory>();
            if (inventory != null)
            {
                inventory.AddItem(recipe.outputItem, recipe.outputAmount);
            }
        }
        // Si c'est une ressource
        else if (recipe.outputResourceType != 0)
        {
            if (ResourceManager.Instance != null)
            {
                ResourceManager.Instance.AddResource(recipe.outputResourceType, recipe.outputAmount);
            }
            else
            {
                Debug.LogWarning("[CraftingManager] ResourceManager not found for resource output");
            }
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }

    #endregion

    #region Save/Load

    public CraftingSaveData GetSaveData()
    {
        return new CraftingSaveData
        {
            unlockedRecipes = new List<string>(_unlockedRecipes),
            craftedCounts = new Dictionary<string, int>(_craftedCounts)
        };
    }

    public void LoadSaveData(CraftingSaveData data)
    {
        if (data == null) return;

        _unlockedRecipes.Clear();
        if (data.unlockedRecipes != null)
        {
            foreach (var name in data.unlockedRecipes)
            {
                _unlockedRecipes.Add(name);
            }
        }

        _craftedCounts.Clear();
        if (data.craftedCounts != null)
        {
            foreach (var kvp in data.craftedCounts)
            {
                _craftedCounts[kvp.Key] = kvp.Value;
            }
        }
    }

    #endregion
}

/// <summary>
/// Job de crafting en cours.
/// </summary>
public class CraftingJob
{
    public CraftingRecipeData recipe;
    public float startTime;
    public float duration;

    public float GetProgress()
    {
        return Mathf.Clamp01((Time.time - startTime) / duration);
    }

    public bool IsComplete()
    {
        return Time.time >= startTime + duration;
    }

    public float GetRemainingTime()
    {
        return Mathf.Max(0, (startTime + duration) - Time.time);
    }
}

/// <summary>
/// Donnees de sauvegarde du crafting.
/// </summary>
[System.Serializable]
public class CraftingSaveData
{
    public List<string> unlockedRecipes;
    public Dictionary<string, int> craftedCounts;
}
