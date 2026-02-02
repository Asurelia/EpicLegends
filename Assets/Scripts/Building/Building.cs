using System;
using UnityEngine;

/// <summary>
/// Composant attache a chaque batiment place dans le monde.
/// Gere l'etat, la vie, les ameliorations et la destruction.
/// </summary>
public class Building : MonoBehaviour, IDamageable
{
    #region Fields

    [Header("Data")]
    [SerializeField] private BuildingData _data;
    [SerializeField] private Vector2Int _gridPosition;
    [SerializeField] private int _rotation;

    [Header("State")]
    [SerializeField] private float _currentHealth;
    [SerializeField] private BuildingTier _currentTier;
    [SerializeField] private bool _isPowered = true;
    [SerializeField] private bool _isActive = true;

    [Header("References")]
    [SerializeField] private BuildingGrid _grid;

    // Construction
    private bool _isBuilding = false;
    private float _buildProgress = 0f;

    // CRITICAL FIX: Track runtime materials for cleanup
    private Material _runtimeMaterial;

    #endregion

    #region Events

    public event Action<float, float> OnHealthChanged;
    public event Action<BuildingTier> OnTierChanged;
    public event Action<Building> OnDestroyed;
    public event Action<float> OnBuildProgressChanged;

    #endregion

    #region Properties

    public BuildingData Data => _data;
    public Vector2Int GridPosition => _gridPosition;
    public int Rotation => _rotation;
    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _data != null ? _data.GetHealthForTier(_currentTier) : 0f;
    public float HealthPercent => MaxHealth > 0 ? _currentHealth / MaxHealth : 0f;
    public BuildingTier CurrentTier => _currentTier;
    public bool IsPowered => _isPowered;
    public bool IsActive => _isActive && _isPowered;
    public bool IsBuilding => _isBuilding;
    public float BuildProgress => _buildProgress;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (_grid == null)
            _grid = FindFirstObjectByType<BuildingGrid>();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Initialise le batiment.
    /// </summary>
    public void Initialize(BuildingData data, Vector2Int gridPosition, int rotation)
    {
        _data = data;
        _gridPosition = gridPosition;
        _rotation = rotation;
        _currentTier = data.baseTier;
        _currentHealth = data.GetHealthForTier(_currentTier);

        // Demarrer la construction si temps > 0
        if (data.buildTime > 0)
        {
            StartBuilding();
        }
    }

    /// <summary>
    /// Demarre la construction.
    /// </summary>
    public void StartBuilding()
    {
        _isBuilding = true;
        _buildProgress = 0f;
        _isActive = false;
    }

    /// <summary>
    /// Met a jour la progression de construction.
    /// </summary>
    public void UpdateBuildProgress(float deltaTime)
    {
        if (!_isBuilding) return;

        _buildProgress += deltaTime / _data.buildTime;
        OnBuildProgressChanged?.Invoke(_buildProgress);

        if (_buildProgress >= 1f)
        {
            CompleteBuild();
        }
    }

    /// <summary>
    /// Complete la construction.
    /// </summary>
    public void CompleteBuild()
    {
        _isBuilding = false;
        _buildProgress = 1f;
        _isActive = true;

        OnBuildProgressChanged?.Invoke(1f);
    }

    /// <summary>
    /// Termine instantanement la construction.
    /// </summary>
    public void InstantBuild()
    {
        _isBuilding = false;
        _buildProgress = 1f;
        _isActive = true;
    }

    #endregion

    #region IDamageable Implementation

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (_currentHealth <= 0) return;

        // Calculer les degats avec defense
        float defense = _data.GetDefenseForTier(_currentTier);
        float finalDamage = DamageCalculator.CalculateFinalDamage(damageInfo, defense);

        _currentHealth = Mathf.Max(0, _currentHealth - finalDamage);
        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);

        if (_currentHealth <= 0)
        {
            DestroyBuilding();
        }
    }

    public bool IsDead => _currentHealth <= 0;

    #endregion

    #region Public Methods - Upgrade

    /// <summary>
    /// Ameliore le batiment vers un tier superieur.
    /// </summary>
    public bool Upgrade(BuildingTier targetTier)
    {
        if (!CanUpgrade(targetTier)) return false;

        // Sauvegarder le pourcentage de vie
        float healthPercent = HealthPercent;

        // Changer le tier
        BuildingTier oldTier = _currentTier;
        _currentTier = targetTier;

        // Mettre a jour la vie
        _currentHealth = MaxHealth * healthPercent;

        // Mettre a jour le visuel (TODO)
        UpdateVisualForTier();

        OnTierChanged?.Invoke(_currentTier);
        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);

        return true;
    }

    /// <summary>
    /// Verifie si le batiment peut etre ameliore.
    /// </summary>
    public bool CanUpgrade(BuildingTier targetTier)
    {
        if (_data == null) return false;
        if (!_data.canUpgrade) return false;
        if (targetTier <= _currentTier) return false;
        if (targetTier > _data.maxTier) return false;
        if (_isBuilding) return false;

        return true;
    }

    /// <summary>
    /// Obtient le prochain tier disponible.
    /// </summary>
    public BuildingTier? GetNextTier()
    {
        if (!_data.canUpgrade) return null;
        if (_currentTier >= _data.maxTier) return null;

        return (BuildingTier)((int)_currentTier + 1);
    }

    #endregion

    #region Public Methods - Repair

    /// <summary>
    /// Repare le batiment.
    /// </summary>
    public void Repair(float amount)
    {
        if (_currentHealth >= MaxHealth) return;

        _currentHealth = Mathf.Min(MaxHealth, _currentHealth + amount);
        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
    }

    /// <summary>
    /// Repare completement le batiment.
    /// </summary>
    public void FullRepair()
    {
        _currentHealth = MaxHealth;
        OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
    }

    #endregion

    #region Public Methods - Power

    /// <summary>
    /// Active/desactive l'alimentation.
    /// </summary>
    public void SetPowered(bool powered)
    {
        _isPowered = powered;
    }

    /// <summary>
    /// Active/desactive le batiment.
    /// </summary>
    public void SetActive(bool active)
    {
        _isActive = active;
    }

    #endregion

    #region Private Methods

    private void DestroyBuilding()
    {
        // Liberer les cellules
        if (_grid != null)
        {
            _grid.FreeCells(_gridPosition, _data.gridSize);
        }

        // Effet de destruction
        if (_data.destroyVFX != null)
        {
            Instantiate(_data.destroyVFX, transform.position, Quaternion.identity);
        }

        // Son de destruction
        if (_data.destroySound != null)
        {
            AudioSource.PlayClipAtPoint(_data.destroySound, transform.position);
        }

        OnDestroyed?.Invoke(this);

        // Detruire le GameObject
        Destroy(gameObject);
    }

    private void UpdateVisualForTier()
    {
        // TODO: Changer le mesh/materiau selon le tier
        // Pour l'instant, juste changer la couleur
        var renderer = GetComponentInChildren<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            Color tierColor = _currentTier switch
            {
                BuildingTier.Wood => new Color(0.6f, 0.4f, 0.2f),
                BuildingTier.Stone => Color.gray,
                BuildingTier.Metal => new Color(0.7f, 0.7f, 0.8f),
                BuildingTier.Tech => Color.cyan,
                _ => Color.white
            };

            // CRITICAL FIX: Destroy previous material to prevent leak
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
            }

            var mat = new Material(renderer.material);
            _runtimeMaterial = mat; // Track for cleanup
            // URP uses _BaseColor, fallback to _Color for legacy
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", tierColor);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", tierColor);
            renderer.material = mat;
        }
    }

    // CRITICAL FIX: Cleanup runtime material on destroy
    private void OnDisable()
    {
        if (_runtimeMaterial != null)
        {
            Destroy(_runtimeMaterial);
            _runtimeMaterial = null;
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (_data == null) return;

        // Dessiner la zone occupee
        float cellSize = _grid != null ? _grid.CellSize : 1f;
        Vector3 size = new Vector3(
            _data.gridSize.x * cellSize,
            _data.height,
            _data.gridSize.y * cellSize
        );

        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawCube(transform.position + Vector3.up * _data.height * 0.5f, size);
    }

    #endregion
}
