using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Component qui gere les drops de loot a la mort d'une entite.
/// Attacher aux ennemis pour qu'ils droppent du loot.
/// </summary>
public class LootDropper : MonoBehaviour
{
    #region Serialized Fields

    [Header("Loot Configuration")]
    [SerializeField] private LootTableData _lootTable;

    [Header("Drop Settings")]
    [SerializeField] private float _dropRadius = 1f;
    [SerializeField] private float _dropHeight = 1f;
    [SerializeField] private float _dropForce = 3f;

    [Header("VFX")]
    [SerializeField] private GameObject _dropVfxPrefab;

    [Header("Prefabs")]
    [SerializeField] private GameObject _worldItemPrefab;
    [SerializeField] private GameObject _goldPrefab;

    #endregion

    #region Private Fields

    private Health _health;
    private bool _hasDropped = false;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        if (_health != null)
        {
            _health.OnDeath += OnDeath;
        }
        _hasDropped = false;
    }

    private void OnDisable()
    {
        if (_health != null)
        {
            _health.OnDeath -= OnDeath;
        }
    }

    #endregion

    #region Event Handlers

    private void OnDeath()
    {
        if (_hasDropped) return;
        _hasDropped = true;

        DropLoot();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Force le drop de loot (pour tests ou autres triggers).
    /// </summary>
    public void DropLoot()
    {
        if (_lootTable == null)
        {
            Debug.LogWarning($"[LootDropper] {gameObject.name} n'a pas de LootTableData assignee!");
            return;
        }

        // Obtenir le niveau du joueur pour le scaling
        int playerLevel = 1;
        if (GameManager.Instance != null)
        {
            var playerStats = GameManager.Instance.Player?.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                playerLevel = playerStats.Level;
            }
        }

        // Generer le loot
        List<LootResult> loot = _lootTable.GenerateLoot(playerLevel);
        int gold = _lootTable.GenerateGold();

        // Spawner les items
        foreach (var drop in loot)
        {
            SpawnWorldItem(drop.item, drop.quantity);
        }

        // Spawner l'or
        if (gold > 0)
        {
            SpawnGold(gold);
        }

        // VFX de drop
        if (_dropVfxPrefab != null && (loot.Count > 0 || gold > 0))
        {
            Instantiate(_dropVfxPrefab, transform.position, Quaternion.identity);
        }

        Debug.Log($"[LootDropper] {gameObject.name} dropped {loot.Count} items and {gold} gold");
    }

    /// <summary>
    /// Definit la table de loot dynamiquement.
    /// </summary>
    public void SetLootTableData(LootTableData table)
    {
        _lootTable = table;
    }

    #endregion

    #region Private Methods

    private void SpawnWorldItem(ItemData item, int quantity)
    {
        if (item == null) return;

        Vector3 spawnPos = GetRandomDropPosition();

        GameObject worldItem;

        if (_worldItemPrefab != null)
        {
            worldItem = Instantiate(_worldItemPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // Creer un placeholder si pas de prefab
            worldItem = CreatePlaceholderWorldItem(item);
            worldItem.transform.position = spawnPos;
        }

        // Configurer le WorldItem
        var worldItemComponent = worldItem.GetComponent<WorldItem>();
        if (worldItemComponent != null)
        {
            worldItemComponent.Initialize(item, quantity);
        }

        // Ajouter une force pour faire "sauter" l'item
        var rb = worldItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 randomDir = Random.insideUnitSphere;
            randomDir.y = Mathf.Abs(randomDir.y);
            rb.AddForce(randomDir * _dropForce, ForceMode.Impulse);
        }
    }

    private void SpawnGold(int amount)
    {
        Vector3 spawnPos = GetRandomDropPosition();

        GameObject goldObj;

        if (_goldPrefab != null)
        {
            goldObj = Instantiate(_goldPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // Creer un placeholder or
            goldObj = CreatePlaceholderGold();
            goldObj.transform.position = spawnPos;
        }

        // Configurer la valeur de l'or
        var goldPickup = goldObj.GetComponent<GoldPickup>();
        if (goldPickup != null)
        {
            goldPickup.Initialize(amount);
        }

        // Ajouter une force
        var rb = goldObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 randomDir = Random.insideUnitSphere;
            randomDir.y = Mathf.Abs(randomDir.y);
            rb.AddForce(randomDir * _dropForce, ForceMode.Impulse);
        }
    }

    private Vector3 GetRandomDropPosition()
    {
        Vector2 randomCircle = Random.insideUnitCircle * _dropRadius;
        return transform.position + new Vector3(randomCircle.x, _dropHeight, randomCircle.y);
    }

    private GameObject CreatePlaceholderWorldItem(ItemData item)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = $"WorldItem_{item.displayName}";
        obj.transform.localScale = Vector3.one * 0.3f;

        // Couleur selon rarete
        var renderer = obj.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = GetRarityColor(item.rarity);
            renderer.material = mat;
        }

        // Ajouter components necessaires
        var rb = obj.AddComponent<Rigidbody>();
        rb.mass = 0.5f;

        obj.GetComponent<Collider>().isTrigger = true;

        var worldItem = obj.AddComponent<WorldItem>();

        return obj;
    }

    private GameObject CreatePlaceholderGold()
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = "Gold";
        obj.transform.localScale = Vector3.one * 0.2f;

        var renderer = obj.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(1f, 0.84f, 0f); // Or
            mat.SetFloat("_Metallic", 1f);
            mat.SetFloat("_Smoothness", 0.8f);
            renderer.material = mat;
        }

        var rb = obj.AddComponent<Rigidbody>();
        rb.mass = 0.2f;

        obj.GetComponent<Collider>().isTrigger = true;

        obj.AddComponent<GoldPickup>();

        return obj;
    }

    private Color GetRarityColor(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common => Color.white,
            ItemRarity.Uncommon => Color.green,
            ItemRarity.Rare => Color.blue,
            ItemRarity.Epic => new Color(0.6f, 0f, 0.8f), // Violet
            ItemRarity.Legendary => new Color(1f, 0.5f, 0f), // Orange
            ItemRarity.Mythic => Color.red,
            _ => Color.gray
        };
    }

    #endregion
}
