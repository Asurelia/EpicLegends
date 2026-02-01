using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represente un territoire capturable dans le jeu.
/// </summary>
public class Territory : MonoBehaviour
{
    #region Events

    /// <summary>Declenche quand le proprietaire change.</summary>
    public event Action<TerritoryOwner, TerritoryOwner> OnOwnerChanged;

    /// <summary>Declenche quand le statut de contestation change.</summary>
    public event Action<bool> OnContestedChanged;

    #endregion

    #region Serialized Fields

    [Header("Identity")]
    [SerializeField] private string _territoryId;
    [SerializeField] private string _displayName;

    [Header("Zone")]
    [SerializeField] private ZoneShape _shape = ZoneShape.Circle;
    [SerializeField] private float _radius = 20f;
    [SerializeField] private Vector3 _size = new Vector3(30f, 10f, 30f);

    [Header("State")]
    [SerializeField] private TerritoryOwner _owner = TerritoryOwner.Neutral;
    [SerializeField] private bool _isCaptureable = true;

    [Header("Bonuses")]
    [SerializeField] private TerritoryBonus[] _bonuses;

    [Header("Visuals")]
    [SerializeField] private Color _neutralColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    [SerializeField] private Color _playerColor = new Color(0f, 0.5f, 1f, 0.3f);
    [SerializeField] private Color _enemyColor = new Color(1f, 0.2f, 0.2f, 0.3f);
    [SerializeField] private Color _contestedColor = new Color(1f, 0.8f, 0f, 0.3f);
    [SerializeField] private GameObject _flagObject;
    [SerializeField] private Renderer _zoneRenderer;

    [Header("Detection")]
    [SerializeField] private LayerMask _enemyLayers;
    [SerializeField] private float _detectionInterval = 0.5f;

    #endregion

    #region Private Fields

    private bool _isContested;
    private TerritoryOwner _contestingFaction;
    private float _detectionTimer;
    private bool _hasEnemiesInRange;
    private List<GameObject> _enemiesInRange = new List<GameObject>();

    #endregion

    #region Properties

    /// <summary>ID unique du territoire.</summary>
    public string TerritoryId => string.IsNullOrEmpty(_territoryId) ? gameObject.name : _territoryId;

    /// <summary>Nom affiche.</summary>
    public string DisplayName => string.IsNullOrEmpty(_displayName) ? TerritoryId : _displayName;

    /// <summary>Proprietaire actuel.</summary>
    public TerritoryOwner Owner => _owner;

    /// <summary>Le territoire peut etre capture?</summary>
    public bool IsCaptureable => _isCaptureable;

    /// <summary>Le territoire est conteste?</summary>
    public bool IsContested => _isContested;

    /// <summary>Faction qui conteste.</summary>
    public TerritoryOwner ContestingFaction => _contestingFaction;

    /// <summary>Rayon de la zone.</summary>
    public float Radius => _radius;

    /// <summary>Taille de la zone.</summary>
    public Vector3 Size => _size;

    /// <summary>Forme de la zone.</summary>
    public ZoneShape Shape => _shape;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (string.IsNullOrEmpty(_territoryId))
        {
            _territoryId = gameObject.name + "_" + GetInstanceID();
        }
    }

    private void Start()
    {
        if (TerritoryControl.Instance != null)
        {
            TerritoryControl.Instance.RegisterTerritory(this);
        }

        UpdateVisuals();
    }

    private void Update()
    {
        _detectionTimer += Time.deltaTime;
        if (_detectionTimer >= _detectionInterval)
        {
            _detectionTimer = 0f;
            DetectEntities();
        }
    }

    private void OnDestroy()
    {
        if (TerritoryControl.Instance != null)
        {
            TerritoryControl.Instance.UnregisterTerritory(this);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = GetGizmoColor();

        switch (_shape)
        {
            case ZoneShape.Circle:
                DrawCircleGizmo();
                break;
            case ZoneShape.Box:
                Gizmos.DrawCube(transform.position, _size);
                break;
            case ZoneShape.Sphere:
                Gizmos.DrawSphere(transform.position, _radius);
                break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        switch (_shape)
        {
            case ZoneShape.Circle:
                DrawCircleGizmo();
                break;
            case ZoneShape.Box:
                Gizmos.DrawWireCube(transform.position, _size);
                break;
            case ZoneShape.Sphere:
                Gizmos.DrawWireSphere(transform.position, _radius);
                break;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Verifie si un point est dans le territoire.
    /// </summary>
    public bool ContainsPoint(Vector3 point)
    {
        switch (_shape)
        {
            case ZoneShape.Circle:
                Vector3 flatPoint = new Vector3(point.x, transform.position.y, point.z);
                return Vector3.Distance(flatPoint, transform.position) <= _radius;

            case ZoneShape.Box:
                Vector3 localPoint = transform.InverseTransformPoint(point);
                return Mathf.Abs(localPoint.x) <= _size.x / 2f &&
                       Mathf.Abs(localPoint.y) <= _size.y / 2f &&
                       Mathf.Abs(localPoint.z) <= _size.z / 2f;

            case ZoneShape.Sphere:
                return Vector3.Distance(point, transform.position) <= _radius;

            default:
                return false;
        }
    }

    /// <summary>
    /// Definit le proprietaire.
    /// </summary>
    public void SetOwner(TerritoryOwner newOwner)
    {
        if (_owner == newOwner) return;

        var oldOwner = _owner;
        _owner = newOwner;

        UpdateVisuals();
        OnOwnerChanged?.Invoke(oldOwner, newOwner);
    }

    /// <summary>
    /// Definit l'etat de contestation.
    /// </summary>
    public void SetContested(bool contested, TerritoryOwner contestingFaction)
    {
        if (_isContested == contested && _contestingFaction == contestingFaction) return;

        _isContested = contested;
        _contestingFaction = contestingFaction;

        UpdateVisuals();
        OnContestedChanged?.Invoke(contested);
    }

    /// <summary>
    /// Verifie s'il y a des ennemis dans la zone.
    /// </summary>
    public bool HasEnemiesInRange()
    {
        return _hasEnemiesInRange;
    }

    /// <summary>
    /// Obtient les ennemis dans la zone.
    /// </summary>
    public List<GameObject> GetEnemiesInRange()
    {
        return new List<GameObject>(_enemiesInRange);
    }

    /// <summary>
    /// Obtient les bonus du territoire.
    /// </summary>
    public TerritoryBonus[] GetBonuses()
    {
        return _bonuses ?? new TerritoryBonus[0];
    }

    /// <summary>
    /// Calcule le bonus pour un type specifique.
    /// </summary>
    public float GetBonusValue(TerritoryBonusType bonusType)
    {
        if (_bonuses == null) return 0f;

        float total = 0f;
        foreach (var bonus in _bonuses)
        {
            if (bonus != null && bonus.bonusType == bonusType)
            {
                total += bonus.value;
            }
        }
        return total;
    }

    #endregion

    #region Private Methods

    private void DetectEntities()
    {
        _enemiesInRange.Clear();

        Collider[] colliders;

        switch (_shape)
        {
            case ZoneShape.Circle:
            case ZoneShape.Sphere:
                colliders = Physics.OverlapSphere(transform.position, _radius, _enemyLayers);
                break;

            case ZoneShape.Box:
                colliders = Physics.OverlapBox(transform.position, _size / 2f, transform.rotation, _enemyLayers);
                break;

            default:
                colliders = new Collider[0];
                break;
        }

        foreach (var col in colliders)
        {
            if (col != null && col.gameObject != null)
            {
                var enemy = col.GetComponent<EnemyAI>();
                if (enemy != null && !enemy.IsDead)
                {
                    _enemiesInRange.Add(col.gameObject);
                }
            }
        }

        _hasEnemiesInRange = _enemiesInRange.Count > 0;
    }

    private void UpdateVisuals()
    {
        Color targetColor;

        if (_isContested)
        {
            targetColor = _contestedColor;
        }
        else
        {
            switch (_owner)
            {
                case TerritoryOwner.Player:
                    targetColor = _playerColor;
                    break;
                case TerritoryOwner.Enemy:
                    targetColor = _enemyColor;
                    break;
                default:
                    targetColor = _neutralColor;
                    break;
            }
        }

        // Appliquer au renderer de zone
        if (_zoneRenderer != null)
        {
            var material = _zoneRenderer.material;
            if (material != null)
            {
                material.color = targetColor;
            }
        }

        // Mettre a jour le drapeau
        UpdateFlag();
    }

    private void UpdateFlag()
    {
        if (_flagObject == null) return;

        // Rotation ou changement de couleur du drapeau selon proprietaire
        var flagRenderer = _flagObject.GetComponent<Renderer>();
        if (flagRenderer != null)
        {
            Color flagColor;
            switch (_owner)
            {
                case TerritoryOwner.Player:
                    flagColor = Color.blue;
                    break;
                case TerritoryOwner.Enemy:
                    flagColor = Color.red;
                    break;
                default:
                    flagColor = Color.white;
                    break;
            }

            if (_isContested)
            {
                flagColor = Color.Lerp(flagColor, Color.yellow, 0.5f);
            }

            var material = flagRenderer.material;
            if (material != null)
            {
                material.color = flagColor;
            }
        }
    }

    private Color GetGizmoColor()
    {
        if (_isContested) return _contestedColor;

        switch (_owner)
        {
            case TerritoryOwner.Player: return _playerColor;
            case TerritoryOwner.Enemy: return _enemyColor;
            default: return _neutralColor;
        }
    }

    private void DrawCircleGizmo()
    {
        int segments = 32;
        Vector3 center = transform.position;

        for (int i = 0; i < segments; i++)
        {
            float angle1 = i * Mathf.PI * 2 / segments;
            float angle2 = (i + 1) * Mathf.PI * 2 / segments;

            Vector3 p1 = center + new Vector3(Mathf.Cos(angle1), 0, Mathf.Sin(angle1)) * _radius;
            Vector3 p2 = center + new Vector3(Mathf.Cos(angle2), 0, Mathf.Sin(angle2)) * _radius;

            Gizmos.DrawLine(p1, p2);
        }
    }

    #endregion
}

/// <summary>
/// Bonus accorde par un territoire.
/// </summary>
[Serializable]
public class TerritoryBonus
{
    public TerritoryBonusType bonusType;
    public float value;
    public bool isPercentage;
}

/// <summary>
/// Types de bonus de territoire.
/// </summary>
public enum TerritoryBonusType
{
    ResourceProduction,
    ExperienceGain,
    DefenseBonus,
    AttackBonus,
    HealthRegen,
    ManaRegen,
    MovementSpeed,
    DropRate
}
