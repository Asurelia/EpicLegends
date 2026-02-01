using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Système de blueprints pour sauvegarder et charger des configurations de bâtiments.
/// </summary>
public class BlueprintSystem : MonoBehaviour
{
    #region Singleton

    private static BlueprintSystem _instance;
    public static BlueprintSystem Instance
    {
        get => _instance;
        private set => _instance = value;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _blueprints = new Dictionary<string, Blueprint>();
        _activePreview = new List<GameObject>();
    }

    #endregion

    #region Events

    /// <summary>Déclenché quand un blueprint est créé.</summary>
    public event Action<Blueprint> OnBlueprintCreated;

    /// <summary>Déclenché quand un blueprint est supprimé.</summary>
    public event Action<string> OnBlueprintDeleted;

    /// <summary>Déclenché quand un blueprint est placé.</summary>
    public event Action<Blueprint> OnBlueprintPlaced;

    #endregion

    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private int _maxBlueprintSize = 100;
    [SerializeField] private float _previewAlpha = 0.5f;
    [SerializeField] private Material _previewMaterial;
    [SerializeField] private Color _validPlacementColor = new Color(0, 1, 0, 0.5f);
    [SerializeField] private Color _invalidPlacementColor = new Color(1, 0, 0, 0.5f);

    [Header("References")]
    [SerializeField] private BuildingData[] _allBuildingData;

    #endregion

    #region Private Fields

    private Dictionary<string, Blueprint> _blueprints;
    private Blueprint _currentBlueprint;
    private List<GameObject> _activePreview;
    private bool _isPlacingBlueprint;
    private Vector3 _placementPosition;
    private float _placementRotation;

    #endregion

    #region Properties

    /// <summary>Nombre de blueprints sauvegardés.</summary>
    public int BlueprintCount => _blueprints?.Count ?? 0;

    /// <summary>Est en mode placement de blueprint?</summary>
    public bool IsPlacingBlueprint => _isPlacingBlueprint;

    /// <summary>Blueprint actuellement sélectionné.</summary>
    public Blueprint CurrentBlueprint => _currentBlueprint;

    #endregion

    #region Public Methods - Blueprint Creation

    /// <summary>
    /// Crée un blueprint à partir des bâtiments sélectionnés.
    /// </summary>
    public Blueprint CreateBlueprint(string name, List<Building> buildings)
    {
        if (string.IsNullOrEmpty(name) || buildings == null || buildings.Count == 0)
            return null;

        if (buildings.Count > _maxBlueprintSize)
        {
            Debug.LogWarning($"[BlueprintSystem] Blueprint too large: {buildings.Count} > {_maxBlueprintSize}");
            return null;
        }

        // Calculer le centre
        Vector3 center = CalculateCenter(buildings);

        // Créer les entrées
        var entries = new List<BlueprintEntry>();
        foreach (var building in buildings)
        {
            if (building == null || building.Data == null) continue;

            entries.Add(new BlueprintEntry
            {
                buildingDataName = building.Data.buildingName,
                relativePosition = building.transform.position - center,
                rotation = building.transform.eulerAngles.y,
                tier = building.CurrentTier
            });
        }

        var blueprint = new Blueprint
        {
            id = Guid.NewGuid().ToString(),
            name = name,
            createdAt = DateTime.Now,
            entries = entries,
            totalCost = CalculateTotalCost(entries)
        };

        _blueprints[blueprint.id] = blueprint;
        OnBlueprintCreated?.Invoke(blueprint);

        Debug.Log($"[BlueprintSystem] Created blueprint '{name}' with {entries.Count} buildings");
        return blueprint;
    }

    /// <summary>
    /// Crée un blueprint à partir d'une zone.
    /// </summary>
    public Blueprint CreateBlueprintFromArea(string name, Bounds area)
    {
        var buildings = FindBuildingsInArea(area);
        return CreateBlueprint(name, buildings);
    }

    #endregion

    #region Public Methods - Blueprint Management

    /// <summary>
    /// Obtient un blueprint par son ID.
    /// </summary>
    public Blueprint GetBlueprint(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        _blueprints.TryGetValue(id, out var blueprint);
        return blueprint;
    }

    /// <summary>
    /// Obtient tous les blueprints.
    /// </summary>
    public List<Blueprint> GetAllBlueprints()
    {
        return new List<Blueprint>(_blueprints.Values);
    }

    /// <summary>
    /// Renomme un blueprint.
    /// </summary>
    public bool RenameBlueprint(string id, string newName)
    {
        if (!_blueprints.TryGetValue(id, out var blueprint)) return false;
        if (string.IsNullOrEmpty(newName)) return false;

        blueprint.name = newName;
        return true;
    }

    /// <summary>
    /// Supprime un blueprint.
    /// </summary>
    public bool DeleteBlueprint(string id)
    {
        if (!_blueprints.ContainsKey(id)) return false;

        _blueprints.Remove(id);
        OnBlueprintDeleted?.Invoke(id);

        Debug.Log($"[BlueprintSystem] Deleted blueprint {id}");
        return true;
    }

    /// <summary>
    /// Duplique un blueprint.
    /// </summary>
    public Blueprint DuplicateBlueprint(string id, string newName)
    {
        if (!_blueprints.TryGetValue(id, out var original)) return null;

        var copy = new Blueprint
        {
            id = Guid.NewGuid().ToString(),
            name = newName ?? $"{original.name} (Copy)",
            createdAt = DateTime.Now,
            entries = new List<BlueprintEntry>(original.entries),
            totalCost = new List<ResourceCost>(original.totalCost)
        };

        _blueprints[copy.id] = copy;
        OnBlueprintCreated?.Invoke(copy);

        return copy;
    }

    #endregion

    #region Public Methods - Blueprint Placement

    /// <summary>
    /// Commence le placement d'un blueprint.
    /// </summary>
    public bool StartPlacement(string blueprintId)
    {
        if (!_blueprints.TryGetValue(blueprintId, out var blueprint)) return false;

        _currentBlueprint = blueprint;
        _isPlacingBlueprint = true;
        _placementPosition = Vector3.zero;
        _placementRotation = 0f;

        CreatePreview();

        Debug.Log($"[BlueprintSystem] Started placing blueprint '{blueprint.name}'");
        return true;
    }

    /// <summary>
    /// Met à jour la position de placement.
    /// </summary>
    public void UpdatePlacementPosition(Vector3 position)
    {
        if (!_isPlacingBlueprint) return;

        _placementPosition = position;
        UpdatePreviewPosition();
    }

    /// <summary>
    /// Tourne le blueprint.
    /// </summary>
    public void RotatePlacement(float angle)
    {
        if (!_isPlacingBlueprint) return;

        _placementRotation += angle;
        _placementRotation = _placementRotation % 360f;
        UpdatePreviewPosition();
    }

    /// <summary>
    /// Confirme le placement du blueprint.
    /// </summary>
    public bool ConfirmPlacement()
    {
        if (!_isPlacingBlueprint || _currentBlueprint == null) return false;

        // Vérifier si on a les ressources
        if (!HasRequiredResources(_currentBlueprint))
        {
            Debug.LogWarning("[BlueprintSystem] Not enough resources");
            return false;
        }

        // Vérifier si le placement est valide
        if (!IsPlacementValid())
        {
            Debug.LogWarning("[BlueprintSystem] Invalid placement");
            return false;
        }

        // Consommer les ressources
        ConsumeResources(_currentBlueprint);

        // Placer les bâtiments
        PlaceBuildings();

        OnBlueprintPlaced?.Invoke(_currentBlueprint);

        CancelPlacement();
        return true;
    }

    /// <summary>
    /// Annule le placement.
    /// </summary>
    public void CancelPlacement()
    {
        _isPlacingBlueprint = false;
        _currentBlueprint = null;
        ClearPreview();
    }

    /// <summary>
    /// Vérifie si le placement actuel est valide.
    /// </summary>
    public bool IsPlacementValid()
    {
        if (!_isPlacingBlueprint || _currentBlueprint == null) return false;

        // Vérifier chaque bâtiment du blueprint
        foreach (var entry in _currentBlueprint.entries)
        {
            var position = GetWorldPosition(entry);
            var buildingData = GetBuildingData(entry.buildingDataName);

            if (buildingData == null) return false;

            // Utiliser le PlacementValidator (static class) avec le BuildingGrid existant
            var grid = FindFirstObjectByType<BuildingGrid>();
            if (grid != null)
            {
                var gridPos = grid.WorldToGrid(position);
                int rotationInt = Mathf.RoundToInt(entry.rotation / 90f) * 90;
                if (!PlacementValidator.CanPlace(buildingData, gridPos, rotationInt, grid))
                {
                    return false;
                }
            }
        }

        return true;
    }

    #endregion

    #region Public Methods - Resources

    /// <summary>
    /// Vérifie si on a les ressources pour un blueprint.
    /// </summary>
    public bool HasRequiredResources(Blueprint blueprint)
    {
        if (blueprint == null || ResourceManager.Instance == null) return false;

        foreach (var cost in blueprint.totalCost)
        {
            if (ResourceManager.Instance.GetResourceCount(cost.resourceType) < cost.amount)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Obtient les ressources manquantes pour un blueprint.
    /// </summary>
    public List<ResourceCost> GetMissingResources(Blueprint blueprint)
    {
        var missing = new List<ResourceCost>();
        if (blueprint == null || ResourceManager.Instance == null) return missing;

        foreach (var cost in blueprint.totalCost)
        {
            int have = ResourceManager.Instance.GetResourceCount(cost.resourceType);
            if (have < cost.amount)
            {
                missing.Add(new ResourceCost
                {
                    resourceType = cost.resourceType,
                    amount = cost.amount - have
                });
            }
        }
        return missing;
    }

    #endregion

    #region Public Methods - Save/Load

    /// <summary>
    /// Obtient les données de sauvegarde.
    /// </summary>
    public BlueprintSaveData GetSaveData()
    {
        return new BlueprintSaveData
        {
            blueprints = new List<Blueprint>(_blueprints.Values)
        };
    }

    /// <summary>
    /// Charge les données de sauvegarde.
    /// </summary>
    public void LoadSaveData(BlueprintSaveData data)
    {
        if (data == null) return;

        _blueprints.Clear();
        if (data.blueprints != null)
        {
            foreach (var blueprint in data.blueprints)
            {
                _blueprints[blueprint.id] = blueprint;
            }
        }
    }

    #endregion

    #region Private Methods

    private Vector3 CalculateCenter(List<Building> buildings)
    {
        if (buildings == null || buildings.Count == 0) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        int count = 0;

        foreach (var building in buildings)
        {
            if (building != null)
            {
                sum += building.transform.position;
                count++;
            }
        }

        return count > 0 ? sum / count : Vector3.zero;
    }

    private List<ResourceCost> CalculateTotalCost(List<BlueprintEntry> entries)
    {
        var costDict = new Dictionary<ResourceType, int>();

        foreach (var entry in entries)
        {
            var buildingData = GetBuildingData(entry.buildingDataName);
            if (buildingData?.buildCosts == null) continue;

            foreach (var cost in buildingData.buildCosts)
            {
                if (!costDict.ContainsKey(cost.resourceType))
                {
                    costDict[cost.resourceType] = 0;
                }
                costDict[cost.resourceType] += cost.amount;
            }
        }

        var result = new List<ResourceCost>();
        foreach (var kvp in costDict)
        {
            result.Add(new ResourceCost
            {
                resourceType = kvp.Key,
                amount = kvp.Value
            });
        }
        return result;
    }

    private List<Building> FindBuildingsInArea(Bounds area)
    {
        var buildings = new List<Building>();
        var allBuildings = FindObjectsByType<Building>(FindObjectsSortMode.None);

        foreach (var building in allBuildings)
        {
            if (area.Contains(building.transform.position))
            {
                buildings.Add(building);
            }
        }

        return buildings;
    }

    private BuildingData GetBuildingData(string name)
    {
        if (_allBuildingData == null || string.IsNullOrEmpty(name)) return null;

        foreach (var data in _allBuildingData)
        {
            if (data != null && data.buildingName == name)
            {
                return data;
            }
        }
        return null;
    }

    private Vector3 GetWorldPosition(BlueprintEntry entry)
    {
        // Appliquer rotation puis translation
        var rotatedPos = Quaternion.Euler(0, _placementRotation, 0) * entry.relativePosition;
        return _placementPosition + rotatedPos;
    }

    private void CreatePreview()
    {
        ClearPreview();

        if (_currentBlueprint == null) return;

        foreach (var entry in _currentBlueprint.entries)
        {
            var buildingData = GetBuildingData(entry.buildingDataName);
            if (buildingData?.previewPrefab == null && buildingData?.prefab == null) continue;

            var prefab = buildingData.previewPrefab ?? buildingData.prefab;
            var preview = Instantiate(prefab);

            // Appliquer material de preview
            ApplyPreviewMaterial(preview);

            _activePreview.Add(preview);
        }

        UpdatePreviewPosition();
    }

    private void UpdatePreviewPosition()
    {
        if (_currentBlueprint == null) return;

        bool isValid = IsPlacementValid();
        var color = isValid ? _validPlacementColor : _invalidPlacementColor;

        for (int i = 0; i < _activePreview.Count && i < _currentBlueprint.entries.Count; i++)
        {
            var preview = _activePreview[i];
            var entry = _currentBlueprint.entries[i];

            if (preview == null) continue;

            preview.transform.position = GetWorldPosition(entry);
            preview.transform.rotation = Quaternion.Euler(0, entry.rotation + _placementRotation, 0);

            // Mettre à jour la couleur
            UpdatePreviewColor(preview, color);
        }
    }

    private void ApplyPreviewMaterial(GameObject obj)
    {
        if (_previewMaterial == null) return;

        var renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            var materials = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = new Material(_previewMaterial);
            }
            renderer.materials = materials;
        }
    }

    private void UpdatePreviewColor(GameObject obj, Color color)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            foreach (var material in renderer.materials)
            {
                material.color = color;
            }
        }
    }

    private void ClearPreview()
    {
        foreach (var preview in _activePreview)
        {
            if (preview != null)
            {
                Destroy(preview);
            }
        }
        _activePreview.Clear();
    }

    private void ConsumeResources(Blueprint blueprint)
    {
        if (ResourceManager.Instance == null) return;

        foreach (var cost in blueprint.totalCost)
        {
            ResourceManager.Instance.RemoveResource(cost.resourceType, cost.amount);
        }
    }

    private void PlaceBuildings()
    {
        if (_currentBlueprint == null) return;

        foreach (var entry in _currentBlueprint.entries)
        {
            var buildingData = GetBuildingData(entry.buildingDataName);
            if (buildingData?.prefab == null) continue;

            var position = GetWorldPosition(entry);
            var rotation = Quaternion.Euler(0, entry.rotation + _placementRotation, 0);

            var building = Instantiate(buildingData.prefab, position, rotation);

            // Initialiser le composant Building si présent
            var buildingComponent = building.GetComponent<Building>();
            if (buildingComponent != null)
            {
                var grid = FindFirstObjectByType<BuildingGrid>();
                var gridPos = grid != null ? grid.WorldToGrid(position) : Vector2Int.zero;
                int rotationInt = Mathf.RoundToInt(entry.rotation / 90f) * 90;
                buildingComponent.Initialize(buildingData, gridPos, rotationInt);

                // Appliquer le tier via upgrade si nécessaire
                if (entry.tier > buildingComponent.CurrentTier && buildingComponent.CanUpgrade(entry.tier))
                {
                    buildingComponent.Upgrade(entry.tier);
                }
            }
        }

        Debug.Log($"[BlueprintSystem] Placed {_currentBlueprint.entries.Count} buildings");
    }

    #endregion
}

/// <summary>
/// Données d'un blueprint.
/// </summary>
[Serializable]
public class Blueprint
{
    public string id;
    public string name;
    public DateTime createdAt;
    public List<BlueprintEntry> entries;
    public List<ResourceCost> totalCost;
}

/// <summary>
/// Entrée d'un bâtiment dans un blueprint.
/// </summary>
[Serializable]
public class BlueprintEntry
{
    public string buildingDataName;
    public Vector3 relativePosition;
    public float rotation;
    public BuildingTier tier;
}

/// <summary>
/// Données de sauvegarde des blueprints.
/// </summary>
[Serializable]
public class BlueprintSaveData
{
    public List<Blueprint> blueprints = new List<Blueprint>();
}
