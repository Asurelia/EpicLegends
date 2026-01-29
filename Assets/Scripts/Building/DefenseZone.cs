using System;
using UnityEngine;

/// <summary>
/// Zone de defense a proteger.
/// Definit une zone ou les tours sont efficaces.
/// </summary>
public class DefenseZone : MonoBehaviour
{
    #region Fields

    [Header("Configuration")]
    [SerializeField] private ZoneShape _shape = ZoneShape.Circle;
    [SerializeField] private float _radius = 20f;
    [SerializeField] private Vector3 _size = new Vector3(20f, 10f, 20f);
    [SerializeField] private Color _gizmoColor = new Color(0f, 1f, 0f, 0.3f);

    [Header("Defense")]
    [SerializeField] private int _priorityLevel = 1;
    [SerializeField] private bool _isActive = true;

    [Header("Objectif")]
    [SerializeField] private Transform _objective;
    [SerializeField] private float _objectiveHealth = 100f;
    private float _currentHealth;

    #endregion

    #region Events

    public event Action<DefenseZone> OnZoneBreached;
    public event Action<DefenseZone, float> OnZoneDamaged;
    public event Action<DefenseZone> OnZoneDestroyed;

    #endregion

    #region Properties

    /// <summary>Forme de la zone.</summary>
    public ZoneShape Shape => _shape;

    /// <summary>Rayon (pour cercle/sphere).</summary>
    public float Radius => _radius;

    /// <summary>Taille (pour boite).</summary>
    public Vector3 Size => _size;

    /// <summary>Niveau de priorite.</summary>
    public int PriorityLevel => _priorityLevel;

    /// <summary>Zone active?</summary>
    public bool IsActive => _isActive;

    /// <summary>Sante de l'objectif.</summary>
    public float ObjectiveHealth => _currentHealth;

    /// <summary>Sante max de l'objectif.</summary>
    public float MaxObjectiveHealth => _objectiveHealth;

    /// <summary>Pourcentage de sante.</summary>
    public float HealthPercent => _objectiveHealth > 0 ? _currentHealth / _objectiveHealth : 0f;

    /// <summary>Objectif detruit?</summary>
    public bool IsDestroyed => _currentHealth <= 0f;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _currentHealth = _objectiveHealth;
    }

    private void Start()
    {
        if (DefenseManager.Instance != null)
        {
            DefenseManager.Instance.RegisterZone(this);
        }
    }

    private void OnDestroy()
    {
        if (DefenseManager.Instance != null)
        {
            DefenseManager.Instance.UnregisterZone(this);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = _gizmoColor;

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

        // Dessiner l'objectif
        if (_objective != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_objective.position, 1f);
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

    #region Public Methods

    /// <summary>
    /// Verifie si un point est dans la zone.
    /// </summary>
    public bool ContainsPoint(Vector3 point)
    {
        if (!_isActive) return false;

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
    /// Inflige des degats a la zone/objectif.
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (IsDestroyed) return;
        if (damage <= 0) return;

        float oldHealth = _currentHealth;
        _currentHealth = Mathf.Max(0f, _currentHealth - damage);

        OnZoneDamaged?.Invoke(this, damage);

        if (_currentHealth <= 0f && oldHealth > 0f)
        {
            OnZoneDestroyed?.Invoke(this);
        }
    }

    /// <summary>
    /// Soigne la zone.
    /// </summary>
    public void Heal(float amount)
    {
        if (amount <= 0) return;
        _currentHealth = Mathf.Min(_objectiveHealth, _currentHealth + amount);
    }

    /// <summary>
    /// Signale une breche dans la zone.
    /// </summary>
    public void SignalBreach()
    {
        OnZoneBreached?.Invoke(this);
    }

    /// <summary>
    /// Active/desactive la zone.
    /// </summary>
    public void SetActive(bool active)
    {
        _isActive = active;
    }

    /// <summary>
    /// Reinitialise la zone.
    /// </summary>
    public void Reset()
    {
        _currentHealth = _objectiveHealth;
        _isActive = true;
    }

    /// <summary>
    /// Obtient la distance a l'objectif.
    /// </summary>
    public float GetDistanceToObjective(Vector3 point)
    {
        if (_objective != null)
        {
            return Vector3.Distance(point, _objective.position);
        }
        return Vector3.Distance(point, transform.position);
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Configure la zone.
    /// </summary>
    public void Configure(ZoneShape shape, float radius, Vector3 size)
    {
        _shape = shape;
        _radius = radius;
        _size = size;
    }

    /// <summary>
    /// Configure l'objectif.
    /// </summary>
    public void SetObjective(Transform objective, float health)
    {
        _objective = objective;
        _objectiveHealth = health;
        _currentHealth = health;
    }

    #endregion
}

/// <summary>
/// Formes de zone de defense.
/// </summary>
public enum ZoneShape
{
    /// <summary>Cercle 2D (ignore la hauteur).</summary>
    Circle,

    /// <summary>Boite 3D.</summary>
    Box,

    /// <summary>Sphere 3D.</summary>
    Sphere
}
