using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire des chaines de production automatisees.
/// Lie les batiments de production pour une production continue.
/// </summary>
public class ProductionChainManager : MonoBehaviour
{
    #region Singleton

    public static ProductionChainManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _chains = new List<ProductionChain>();
        _productionBuildings = new List<ProductionBuilding>();
    }

    #endregion

    #region Fields

    [Header("Configuration")]
    [SerializeField] private float _updateInterval = 1f;
    [SerializeField] private int _maxChainsPerUpdate = 5;

    // Chaines enregistrees
    private List<ProductionChain> _chains;
    private List<ProductionBuilding> _productionBuildings;
    private float _updateTimer = 0f;

    #endregion

    #region Events

    public event Action<ProductionChain> OnChainCreated;
    public event Action<ProductionChain> OnChainRemoved;
    public event Action<ProductionChain, CraftingRecipeData> OnChainProduced;

    #endregion

    #region Properties

    /// <summary>Nombre de chaines actives.</summary>
    public int ChainCount => _chains.Count;

    /// <summary>Batiments de production enregistres.</summary>
    public int ProductionBuildingCount => _productionBuildings.Count;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        _updateTimer += Time.deltaTime;

        if (_updateTimer >= _updateInterval)
        {
            _updateTimer = 0f;
            ProcessChains();
        }
    }

    #endregion

    #region Public Methods - Chaines

    /// <summary>
    /// Cree une nouvelle chaine de production.
    /// </summary>
    public ProductionChain CreateChain(string name)
    {
        var chain = new ProductionChain
        {
            name = name,
            isActive = true,
            nodes = new List<ChainNode>()
        };

        _chains.Add(chain);
        OnChainCreated?.Invoke(chain);

        return chain;
    }

    /// <summary>
    /// Supprime une chaine de production.
    /// </summary>
    public bool RemoveChain(ProductionChain chain)
    {
        if (chain == null) return false;

        bool removed = _chains.Remove(chain);
        if (removed)
        {
            OnChainRemoved?.Invoke(chain);
        }
        return removed;
    }

    /// <summary>
    /// Ajoute un noeud a une chaine.
    /// </summary>
    public bool AddNodeToChain(ProductionChain chain, ProductionBuilding building, CraftingRecipeData recipe)
    {
        if (chain == null || building == null || recipe == null) return false;

        var node = new ChainNode
        {
            building = building,
            recipe = recipe,
            priority = chain.nodes.Count
        };

        chain.nodes.Add(node);
        return true;
    }

    /// <summary>
    /// Retire un noeud d'une chaine.
    /// </summary>
    public bool RemoveNodeFromChain(ProductionChain chain, ProductionBuilding building)
    {
        if (chain == null || building == null) return false;

        for (int i = chain.nodes.Count - 1; i >= 0; i--)
        {
            if (chain.nodes[i].building == building)
            {
                chain.nodes.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Active/desactive une chaine.
    /// </summary>
    public void SetChainActive(ProductionChain chain, bool active)
    {
        if (chain != null)
        {
            chain.isActive = active;
        }
    }

    /// <summary>
    /// Obtient toutes les chaines.
    /// </summary>
    public IReadOnlyList<ProductionChain> GetAllChains()
    {
        return _chains;
    }

    #endregion

    #region Public Methods - Batiments

    /// <summary>
    /// Enregistre un batiment de production.
    /// </summary>
    public void RegisterBuilding(ProductionBuilding building)
    {
        if (building == null) return;
        if (_productionBuildings.Contains(building)) return;

        _productionBuildings.Add(building);
    }

    /// <summary>
    /// Retire un batiment de production.
    /// </summary>
    public void UnregisterBuilding(ProductionBuilding building)
    {
        if (building == null) return;

        _productionBuildings.Remove(building);

        // Retirer des chaines
        foreach (var chain in _chains)
        {
            RemoveNodeFromChain(chain, building);
        }
    }

    /// <summary>
    /// Trouve les batiments pouvant produire une recette.
    /// </summary>
    public List<ProductionBuilding> FindBuildingsForRecipe(CraftingRecipeData recipe)
    {
        var result = new List<ProductionBuilding>();

        foreach (var building in _productionBuildings)
        {
            if (building.CanCraftRecipe(recipe))
            {
                result.Add(building);
            }
        }

        return result;
    }

    #endregion

    #region Private Methods

    private void ProcessChains()
    {
        int processed = 0;

        foreach (var chain in _chains)
        {
            if (!chain.isActive) continue;
            if (processed >= _maxChainsPerUpdate) break;

            ProcessChain(chain);
            processed++;
        }
    }

    private void ProcessChain(ProductionChain chain)
    {
        foreach (var node in chain.nodes)
        {
            if (node.building == null) continue;
            if (node.recipe == null) continue;

            // Si le batiment ne produit pas, essayer de lancer
            if (!node.building.IsProducing && !node.building.IsQueueFull)
            {
                if (node.building.HasIngredients(node.recipe))
                {
                    if (node.building.QueueRecipe(node.recipe))
                    {
                        OnChainProduced?.Invoke(chain, node.recipe);
                    }
                }
            }
        }
    }

    #endregion
}

/// <summary>
/// Chaine de production automatisee.
/// </summary>
public class ProductionChain
{
    public string name;
    public bool isActive;
    public List<ChainNode> nodes;
}

/// <summary>
/// Noeud dans une chaine de production.
/// </summary>
public struct ChainNode
{
    public ProductionBuilding building;
    public CraftingRecipeData recipe;
    public int priority;
}
