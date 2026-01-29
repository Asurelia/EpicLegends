using System;
using UnityEngine;

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

    #endregion

    #region Events

    public event Action<BuildingData> OnBuildingSelected;
    public event Action<Building> OnBuildingPlaced;
    public event Action OnPlacementCancelled;

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
    }

    private void Update()
    {
        if (!IsPlacing) return;

        UpdatePreviewPosition();
        HandleInput();
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

        // Verifier les ressources (TODO: integration avec inventaire)

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

        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);

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
        if (_grid == null) return true;

        return PlacementValidator.CanPlace(
            _selectedBuilding,
            _currentGridPosition,
            _currentRotation,
            _grid
        );
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

    private void HandleInput()
    {
        // Rotation
        if (Input.GetKeyDown(KeyCode.R))
        {
            RotateBuilding();
        }

        // Placement
        if (Input.GetMouseButtonDown(0) && _canPlace)
        {
            TryPlace();
        }

        // Annulation
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPlacement();
        }
    }

    #endregion
}
