using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gestionnaire de placement de batiments.
/// Gere la preview, la validation et le placement.
/// </summary>
public class BuildingPlacer : MonoBehaviour
{
    #region Fields

    [Header("References")]
    [SerializeField] private BuildingGrid _grid;
    [SerializeField] private Camera _camera;
    [SerializeField] private LayerMask _groundLayer;

    [Header("Preview Settings")]
    [SerializeField] private Material _validPreviewMaterial;
    [SerializeField] private Material _invalidPreviewMaterial;
    [SerializeField] private float _previewYOffset = 0.1f;

    [Header("Placement Settings")]
    [SerializeField] private float _maxPlaceDistance = 50f;

    // Etat
    private BuildingData _selectedBuilding;
    private GameObject _previewObject;
    private int _currentRotation = 0;
    private int _currentRotationIndex = 0;
    private bool _canPlace = false;
    private Vector2Int _currentGridPosition;

    // Input
    private InputAction _rotateAction;
    private InputAction _placeAction;
    private InputAction _cancelAction;

    #endregion

    #region Events

    public event Action<BuildingData> OnBuildingSelected;
    public event Action<Building> OnBuildingPlaced;
    public event Action OnPlacementCancelled;
    public event Action<string> OnPlacementFailed;

    #endregion

    #region Properties

    public BuildingData SelectedBuilding => _selectedBuilding;
    public int CurrentRotation => _currentRotation;
    public bool IsPlacing => _selectedBuilding != null;
    public bool CanPlace => _canPlace;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (_camera == null)
            _camera = Camera.main;

        if (_grid == null)
            _grid = FindFirstObjectByType<BuildingGrid>();

        _currentRotation = 0;
        _currentRotationIndex = 0;

        SetupInputActions();
    }

    private void OnEnable()
    {
        _rotateAction?.Enable();
        _placeAction?.Enable();
        _cancelAction?.Enable();
    }

    private void OnDisable()
    {
        _rotateAction?.Disable();
        _placeAction?.Disable();
        _cancelAction?.Disable();
    }

    private void SetupInputActions()
    {
        _rotateAction = new InputAction("BuildingRotate", InputActionType.Button);
        _rotateAction.AddBinding("<Keyboard>/r");
        _rotateAction.AddBinding("<Gamepad>/rightShoulder");
        _rotateAction.performed += _ => { if (IsPlacing) RotateBuilding(); };

        _placeAction = new InputAction("BuildingPlace", InputActionType.Button);
        _placeAction.AddBinding("<Mouse>/leftButton");
        _placeAction.AddBinding("<Gamepad>/buttonSouth");
        _placeAction.performed += _ => { if (IsPlacing && _canPlace) TryPlace(); };

        _cancelAction = new InputAction("BuildingCancel", InputActionType.Button);
        _cancelAction.AddBinding("<Mouse>/rightButton");
        _cancelAction.AddBinding("<Keyboard>/escape");
        _cancelAction.AddBinding("<Gamepad>/buttonEast");
        _cancelAction.performed += _ => { if (IsPlacing) CancelPlacement(); };
    }

    private void Update()
    {
        if (!IsPlacing) return;

        UpdatePreviewPosition();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Selectionne un batiment a placer.
    /// </summary>
    public void SelectBuilding(BuildingData data)
    {
        // Nettoyer la preview precedente
        CancelPlacement();

        _selectedBuilding = data;

        if (data != null)
        {
            CreatePreview();
            OnBuildingSelected?.Invoke(data);
        }
    }

    /// <summary>
    /// Tourne le batiment.
    /// </summary>
    public void RotateBuilding()
    {
        if (_selectedBuilding == null) return;
        if (!_selectedBuilding.canRotate) return;

        _currentRotationIndex = (_currentRotationIndex + 1) % _selectedBuilding.rotationAngles.Length;
        _currentRotation = _selectedBuilding.rotationAngles[_currentRotationIndex];

        if (_previewObject != null)
        {
            _previewObject.transform.rotation = Quaternion.Euler(0, _currentRotation, 0);
        }
    }

    /// <summary>
    /// Tourne le batiment dans l'autre sens.
    /// </summary>
    public void RotateBuildingCounterClockwise()
    {
        if (_selectedBuilding == null) return;
        if (!_selectedBuilding.canRotate) return;

        _currentRotationIndex = (_currentRotationIndex - 1 + _selectedBuilding.rotationAngles.Length)
                                % _selectedBuilding.rotationAngles.Length;
        _currentRotation = _selectedBuilding.rotationAngles[_currentRotationIndex];

        if (_previewObject != null)
        {
            _previewObject.transform.rotation = Quaternion.Euler(0, _currentRotation, 0);
        }
    }

    /// <summary>
    /// Tente de placer le batiment.
    /// </summary>
    public bool TryPlace()
    {
        if (!IsPlacing || !_canPlace) return false;

        // Verifier les ressources via ResourceManager
        if (!CanAffordBuilding(_selectedBuilding))
        {
            OnPlacementFailed?.Invoke("Ressources insuffisantes");
            return false;
        }

        // Consommer les ressources
        if (!ConsumeResources(_selectedBuilding))
        {
            OnPlacementFailed?.Invoke("Erreur lors de la consommation des ressources");
            return false;
        }

        // Placer le batiment
        Building building = PlaceBuilding();

        if (building != null)
        {
            OnBuildingPlaced?.Invoke(building);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Verifie si le joueur peut payer le cout du batiment.
    /// </summary>
    public bool CanAffordBuilding(BuildingData data)
    {
        if (data == null || data.buildCosts == null || data.buildCosts.Length == 0)
            return true;

        if (ResourceManager.Instance == null)
        {
            Debug.LogWarning("[BuildingPlacer] ResourceManager non trouve, placement autorise");
            return true;
        }

        return ResourceManager.Instance.HasResources(data.buildCosts);
    }

    /// <summary>
    /// Consomme les ressources pour construire.
    /// </summary>
    private bool ConsumeResources(BuildingData data)
    {
        if (data == null || data.buildCosts == null || data.buildCosts.Length == 0)
            return true;

        if (ResourceManager.Instance == null)
            return true;

        return ResourceManager.Instance.RemoveResources(data.buildCosts);
    }

    /// <summary>
    /// Obtient le cout manquant pour un batiment.
    /// </summary>
    public ResourceCost[] GetMissingResources(BuildingData data)
    {
        if (data == null || data.buildCosts == null || ResourceManager.Instance == null)
            return new ResourceCost[0];

        var missing = new System.Collections.Generic.List<ResourceCost>();

        foreach (var cost in data.buildCosts)
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

        return missing.ToArray();
    }

    /// <summary>
    /// Annule le placement.
    /// </summary>
    public void CancelPlacement()
    {
        if (_previewObject != null)
        {
            SafeDestroy(_previewObject);
            _previewObject = null;
        }

        _selectedBuilding = null;
        _currentRotation = 0;
        _currentRotationIndex = 0;

        OnPlacementCancelled?.Invoke();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Detruit un objet de maniere compatible avec l'editeur.
    /// </summary>
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

    private void CreatePreview()
    {
        if (_selectedBuilding.previewPrefab != null)
        {
            _previewObject = Instantiate(_selectedBuilding.previewPrefab);
        }
        else if (_selectedBuilding.prefab != null)
        {
            _previewObject = Instantiate(_selectedBuilding.prefab);

            // Desactiver les composants non-visuels
            foreach (var collider in _previewObject.GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }
            foreach (var rb in _previewObject.GetComponentsInChildren<Rigidbody>())
            {
                SafeDestroy(rb);
            }
        }
        else
        {
            // Creer un cube de preview par defaut
            _previewObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SafeDestroy(_previewObject.GetComponent<Collider>());

            Vector2Int size = _selectedBuilding.gridSize;
            float cellSize = _grid != null ? _grid.CellSize : 1f;
            _previewObject.transform.localScale = new Vector3(
                size.x * cellSize,
                _selectedBuilding.height,
                size.y * cellSize
            );
        }

        _previewObject.name = "BuildingPreview";

        // Appliquer le materiau de preview
        ApplyPreviewMaterial(_canPlace);
    }

    private void UpdatePreviewPosition()
    {
        if (_previewObject == null || _camera == null) return;

        Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        Ray ray = _camera.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, _maxPlaceDistance, _groundLayer))
        {
            // Snaper sur la grille
            Vector3 snappedPosition;
            if (_grid != null)
            {
                _currentGridPosition = _grid.WorldToGrid(hit.point);
                snappedPosition = _grid.GridToWorld(_currentGridPosition, _selectedBuilding.gridSize);
                snappedPosition.y = hit.point.y + _previewYOffset;
            }
            else
            {
                snappedPosition = hit.point;
                snappedPosition.y += _previewYOffset;
            }

            _previewObject.transform.position = snappedPosition;

            // Valider le placement
            _canPlace = ValidatePlacement();
            ApplyPreviewMaterial(_canPlace);
        }
    }

    private bool ValidatePlacement()
    {
        // Verifier le placement sur la grille
        if (_grid != null)
        {
            if (!PlacementValidator.CanPlace(
                _selectedBuilding,
                _currentGridPosition,
                _currentRotation,
                _grid))
            {
                return false;
            }
        }

        // Verifier les ressources disponibles
        if (!CanAffordBuilding(_selectedBuilding))
        {
            return false;
        }

        return true;
    }

    private void ApplyPreviewMaterial(bool isValid)
    {
        if (_previewObject == null) return;

        Material mat = isValid ? _validPreviewMaterial : _invalidPreviewMaterial;
        if (mat == null) return;

        foreach (var renderer in _previewObject.GetComponentsInChildren<Renderer>())
        {
            var materials = new Material[renderer.materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = mat;
            }
            renderer.materials = materials;
        }
    }

    private Building PlaceBuilding()
    {
        if (_selectedBuilding.prefab == null)
        {
            Debug.LogError($"Pas de prefab pour {_selectedBuilding.buildingName}");
            return null;
        }

        // Position finale
        Vector3 position = _previewObject.transform.position;
        position.y -= _previewYOffset;

        // Creer le batiment
        GameObject buildingGO = Instantiate(_selectedBuilding.prefab, position,
            Quaternion.Euler(0, _currentRotation, 0));

        buildingGO.name = _selectedBuilding.buildingName;

        // Ajouter/configurer le composant Building
        Building building = buildingGO.GetComponent<Building>();
        if (building == null)
        {
            building = buildingGO.AddComponent<Building>();
        }

        building.Initialize(_selectedBuilding, _currentGridPosition, _currentRotation);

        // Occuper les cellules
        if (_grid != null)
        {
            _grid.OccupyCells(_currentGridPosition, _selectedBuilding.gridSize, buildingGO);
        }

        // Jouer le son
        if (_selectedBuilding.placeSound != null)
        {
            AudioSource.PlayClipAtPoint(_selectedBuilding.placeSound, position);
        }

        // Effet visuel
        if (_selectedBuilding.buildVFX != null)
        {
            Instantiate(_selectedBuilding.buildVFX, position, Quaternion.identity);
        }

        return building;
    }

    #endregion
}
