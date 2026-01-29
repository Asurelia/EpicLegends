using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire du deblocage des recettes.
/// Gere la progression et les prerequis des recettes.
/// </summary>
public class RecipeUnlockManager : MonoBehaviour
{
    #region Singleton

    private static RecipeUnlockManager _instance;
    public static RecipeUnlockManager Instance
    {
        get => _instance;
        private set => _instance = value;
    }

    private void Awake()
    {
        // Initialiser les collections avant la verification du singleton
        _unlockedRecipes = new HashSet<CraftingRecipeData>();
        _researchProgress = new Dictionary<string, float>();

        if (Instance != null && Instance != this)
        {
            SafeDestroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(obj);
        }
        else
        {
            Destroy(obj);
        }
#else
        Destroy(obj);
#endif
    }

    #endregion

    #region Fields

    [Header("Configuration")]
    [SerializeField] private CraftingRecipeData[] _allRecipes;
    [SerializeField] private CraftingRecipeData[] _defaultUnlockedRecipes;

    // Recettes debloquees
    private HashSet<CraftingRecipeData> _unlockedRecipes;
    private Dictionary<string, float> _researchProgress;

    #endregion

    #region Events

    public event Action<CraftingRecipeData> OnRecipeUnlocked;
    public event Action<string, float> OnResearchProgress;
    public event Action<string> OnResearchCompleted;

    #endregion

    #region Properties

    /// <summary>Nombre de recettes debloquees.</summary>
    public int UnlockedCount => _unlockedRecipes?.Count ?? 0;

    /// <summary>Nombre total de recettes.</summary>
    public int TotalRecipes => _allRecipes?.Length ?? 0;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        UnlockDefaultRecipes();
    }

    #endregion

    #region Public Methods - Recettes

    /// <summary>
    /// Verifie si une recette est debloquee.
    /// </summary>
    public bool IsRecipeUnlocked(CraftingRecipeData recipe)
    {
        if (recipe == null) return false;
        if (_unlockedRecipes == null) return false;
        return _unlockedRecipes.Contains(recipe);
    }

    /// <summary>
    /// Debloque une recette.
    /// </summary>
    public bool UnlockRecipe(CraftingRecipeData recipe)
    {
        if (recipe == null) return false;
        if (_unlockedRecipes == null) return false;
        if (_unlockedRecipes.Contains(recipe)) return false;

        // Verifier les prerequis
        if (!CanUnlockRecipe(recipe)) return false;

        _unlockedRecipes.Add(recipe);
        OnRecipeUnlocked?.Invoke(recipe);
        return true;
    }

    /// <summary>
    /// Verifie si une recette peut etre debloquee.
    /// </summary>
    public bool CanUnlockRecipe(CraftingRecipeData recipe)
    {
        if (recipe == null) return false;
        if (_unlockedRecipes == null) return false;

        // Deja debloquee?
        if (_unlockedRecipes.Contains(recipe)) return false;

        // Verifier les prerequis
        if (recipe.prerequisites != null)
        {
            foreach (var prereq in recipe.prerequisites)
            {
                if (prereq != null && !IsRecipeUnlocked(prereq))
                {
                    return false;
                }
            }
        }

        // Verifier la recherche requise
        if (!string.IsNullOrEmpty(recipe.requiredResearch))
        {
            if (!IsResearchCompleted(recipe.requiredResearch))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Obtient toutes les recettes debloquees.
    /// </summary>
    public List<CraftingRecipeData> GetUnlockedRecipes()
    {
        return new List<CraftingRecipeData>(_unlockedRecipes);
    }

    /// <summary>
    /// Obtient les recettes d'une categorie.
    /// </summary>
    public List<CraftingRecipeData> GetRecipesByCategory(RecipeCategory category)
    {
        var result = new List<CraftingRecipeData>();

        foreach (var recipe in _unlockedRecipes)
        {
            if (recipe.category == category)
            {
                result.Add(recipe);
            }
        }

        return result;
    }

    /// <summary>
    /// Obtient les recettes disponibles pour une station.
    /// </summary>
    public List<CraftingRecipeData> GetRecipesForStation(BuildingSubCategory stationType, int stationLevel)
    {
        var result = new List<CraftingRecipeData>();

        foreach (var recipe in _unlockedRecipes)
        {
            if (recipe.requiredStation == stationType && recipe.requiredStationLevel <= stationLevel)
            {
                result.Add(recipe);
            }
        }

        return result;
    }

    #endregion

    #region Public Methods - Recherche

    /// <summary>
    /// Ajoute de la progression a une recherche.
    /// </summary>
    public void AddResearchProgress(string researchId, float amount)
    {
        if (string.IsNullOrEmpty(researchId)) return;
        if (amount <= 0) return;
        if (_researchProgress == null) return;

        if (!_researchProgress.ContainsKey(researchId))
        {
            _researchProgress[researchId] = 0f;
        }

        float oldProgress = _researchProgress[researchId];
        float newProgress = Mathf.Min(1f, oldProgress + amount);
        _researchProgress[researchId] = newProgress;

        OnResearchProgress?.Invoke(researchId, newProgress);

        if (newProgress >= 1f && oldProgress < 1f)
        {
            OnResearchCompleted?.Invoke(researchId);
            UnlockRecipesForResearch(researchId);
        }
    }

    /// <summary>
    /// Obtient la progression d'une recherche.
    /// </summary>
    public float GetResearchProgress(string researchId)
    {
        if (string.IsNullOrEmpty(researchId)) return 0f;
        if (_researchProgress == null) return 0f;
        return _researchProgress.TryGetValue(researchId, out float progress) ? progress : 0f;
    }

    /// <summary>
    /// Verifie si une recherche est completee.
    /// </summary>
    public bool IsResearchCompleted(string researchId)
    {
        return GetResearchProgress(researchId) >= 1f;
    }

    /// <summary>
    /// Complete une recherche immediatement.
    /// </summary>
    public void CompleteResearch(string researchId)
    {
        AddResearchProgress(researchId, 1f);
    }

    #endregion

    #region Private Methods

    private void UnlockDefaultRecipes()
    {
        // Debloquer les recettes par defaut configurees
        if (_defaultUnlockedRecipes != null)
        {
            foreach (var recipe in _defaultUnlockedRecipes)
            {
                if (recipe != null)
                {
                    _unlockedRecipes.Add(recipe);
                }
            }
        }

        // Debloquer les recettes marquees comme par defaut
        if (_allRecipes != null)
        {
            foreach (var recipe in _allRecipes)
            {
                if (recipe != null && recipe.unlockedByDefault)
                {
                    _unlockedRecipes.Add(recipe);
                }
            }
        }
    }

    private void UnlockRecipesForResearch(string researchId)
    {
        if (_allRecipes == null) return;

        foreach (var recipe in _allRecipes)
        {
            if (recipe != null && recipe.requiredResearch == researchId)
            {
                UnlockRecipe(recipe);
            }
        }
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Configure les recettes disponibles.
    /// </summary>
    public void SetRecipes(CraftingRecipeData[] allRecipes, CraftingRecipeData[] defaultUnlocked = null)
    {
        _allRecipes = allRecipes;
        _defaultUnlockedRecipes = defaultUnlocked;
    }

    /// <summary>
    /// Force le deblocage d'une recette (bypass prerequis).
    /// </summary>
    public void ForceUnlock(CraftingRecipeData recipe)
    {
        if (recipe != null && _unlockedRecipes != null)
        {
            _unlockedRecipes.Add(recipe);
            OnRecipeUnlocked?.Invoke(recipe);
        }
    }

    /// <summary>
    /// Reinitialise toutes les recettes.
    /// </summary>
    public void Reset()
    {
        _unlockedRecipes.Clear();
        _researchProgress.Clear();
        UnlockDefaultRecipes();
    }

    #endregion
}
