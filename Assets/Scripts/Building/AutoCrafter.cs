using System;
using UnityEngine;

/// <summary>
/// Composant pour l'automatisation de la production.
/// Extrait automatiquement les ressources et lance les crafts.
/// </summary>
public class AutoCrafter : MonoBehaviour
{
    #region Fields

    [Header("Configuration")]
    [SerializeField] private ProductionBuilding _productionBuilding;
    [SerializeField] private CraftingRecipeData _targetRecipe;
    [SerializeField] private bool _autoStart = true;
    [SerializeField] private float _checkInterval = 2f;

    [Header("Mode")]
    [SerializeField] private AutoCraftMode _mode = AutoCraftMode.Continuous;
    [SerializeField] private int _targetAmount = 0;

    [Header("Input")]
    [SerializeField] private bool _autoExtractResources = true;
    [SerializeField] private LogisticsNetwork _logisticsNetwork;

    // Etat
    private bool _isActive = true;
    private int _producedCount = 0;
    private float _checkTimer = 0f;

    #endregion

    #region Events

    public event Action<CraftingRecipeData> OnAutoCraftStarted;
    public event Action<int> OnTargetReached;

    #endregion

    #region Properties

    /// <summary>Auto-crafter actif?</summary>
    public bool IsActive => _isActive;

    /// <summary>Recette cible.</summary>
    public CraftingRecipeData TargetRecipe => _targetRecipe;

    /// <summary>Mode de production.</summary>
    public AutoCraftMode Mode => _mode;

    /// <summary>Nombre produit.</summary>
    public int ProducedCount => _producedCount;

    /// <summary>Quantite cible.</summary>
    public int TargetAmount => _targetAmount;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (_productionBuilding == null)
        {
            _productionBuilding = GetComponent<ProductionBuilding>();
        }
    }

    private void Start()
    {
        if (_productionBuilding != null)
        {
            _productionBuilding.OnCraftCompleted += HandleCraftCompleted;
        }
    }

    private void OnDestroy()
    {
        if (_productionBuilding != null)
        {
            _productionBuilding.OnCraftCompleted -= HandleCraftCompleted;
        }
    }

    private void Update()
    {
        if (!_isActive) return;
        if (_productionBuilding == null) return;
        if (_targetRecipe == null) return;

        _checkTimer += Time.deltaTime;
        if (_checkTimer >= _checkInterval)
        {
            _checkTimer = 0f;
            CheckAndStartCraft();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Active/desactive l'auto-crafting.
    /// </summary>
    public void SetActive(bool active)
    {
        _isActive = active;
    }

    /// <summary>
    /// Configure la recette cible.
    /// </summary>
    public void SetTargetRecipe(CraftingRecipeData recipe)
    {
        _targetRecipe = recipe;
        _producedCount = 0;
    }

    /// <summary>
    /// Configure le mode de production.
    /// </summary>
    public void SetMode(AutoCraftMode mode, int targetAmount = 0)
    {
        _mode = mode;
        _targetAmount = targetAmount;
        _producedCount = 0;
    }

    /// <summary>
    /// Configure l'extraction automatique.
    /// </summary>
    public void SetAutoExtract(bool autoExtract, LogisticsNetwork network = null)
    {
        _autoExtractResources = autoExtract;
        _logisticsNetwork = network;
    }

    /// <summary>
    /// Force un check immediat.
    /// </summary>
    public void ForceCheck()
    {
        CheckAndStartCraft();
    }

    /// <summary>
    /// Reinitialise le compteur.
    /// </summary>
    public void ResetCount()
    {
        _producedCount = 0;
    }

    #endregion

    #region Private Methods

    private void CheckAndStartCraft()
    {
        if (_productionBuilding == null) return;
        if (_targetRecipe == null) return;

        // Verifier le mode
        if (_mode == AutoCraftMode.TargetAmount && _producedCount >= _targetAmount)
        {
            return; // Cible atteinte
        }

        // Ne pas demarrer si deja en production ou queue pleine
        if (_productionBuilding.IsProducing && _productionBuilding.IsQueueFull)
        {
            return;
        }

        // Verifier les ingredients
        if (!_productionBuilding.HasIngredients(_targetRecipe))
        {
            if (_autoExtractResources)
            {
                TryExtractResources();
            }
            return;
        }

        // Lancer la production
        if (_productionBuilding.QueueRecipe(_targetRecipe))
        {
            OnAutoCraftStarted?.Invoke(_targetRecipe);
        }
    }

    private void TryExtractResources()
    {
        if (_logisticsNetwork == null) return;
        if (_targetRecipe == null) return;
        if (_targetRecipe.ingredients == null) return;

        // Obtenir le storage d'entree du batiment
        var inputField = typeof(ProductionBuilding).GetField("_inputStorage",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (inputField == null) return;

        var inputStorage = inputField.GetValue(_productionBuilding) as StorageBuilding;
        if (inputStorage == null) return;

        // Demander les ressources manquantes
        foreach (var ingredient in _targetRecipe.ingredients)
        {
            int current = inputStorage.GetResourceCount(ingredient.resourceType);
            int needed = ingredient.amount - current;

            if (needed > 0)
            {
                _logisticsNetwork.RequestResource(ingredient.resourceType, needed, inputStorage);
            }
        }
    }

    private void HandleCraftCompleted(CraftingRecipeData recipe)
    {
        if (recipe == _targetRecipe)
        {
            _producedCount++;

            if (_mode == AutoCraftMode.TargetAmount && _producedCount >= _targetAmount)
            {
                OnTargetReached?.Invoke(_producedCount);
            }
        }
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Lie un batiment de production.
    /// </summary>
    public void SetProductionBuilding(ProductionBuilding building)
    {
        // Detacher de l'ancien
        if (_productionBuilding != null)
        {
            _productionBuilding.OnCraftCompleted -= HandleCraftCompleted;
        }

        _productionBuilding = building;

        // Attacher au nouveau
        if (_productionBuilding != null)
        {
            _productionBuilding.OnCraftCompleted += HandleCraftCompleted;
        }
    }

    #endregion
}

/// <summary>
/// Modes d'auto-crafting.
/// </summary>
public enum AutoCraftMode
{
    /// <summary>Production continue.</summary>
    Continuous,

    /// <summary>Production jusqu'a une quantite cible.</summary>
    TargetAmount,

    /// <summary>Production tant qu'il y a des ressources.</summary>
    WhileResources
}
