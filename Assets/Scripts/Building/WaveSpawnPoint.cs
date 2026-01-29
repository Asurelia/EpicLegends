using UnityEngine;

/// <summary>
/// Point de spawn pour les vagues de defense.
/// Definit ou les ennemis apparaissent pendant les vagues de tower defense.
/// </summary>
public class WaveSpawnPoint : MonoBehaviour
{
    #region Fields

    [Header("Configuration")]
    [SerializeField] private WaveSpawnType _spawnType = WaveSpawnType.Point;
    [SerializeField] private float _spawnRadius = 1f;
    [SerializeField] private Vector3 _spawnArea = Vector3.one;
    [SerializeField] private bool _isActive = true;

    [Header("Visuel")]
    [SerializeField] private Color _gizmoColor = Color.red;
    [SerializeField] private bool _showGizmo = true;

    [Header("Options")]
    [SerializeField] private bool _randomRotation = true;
    [SerializeField] private bool _snapToGround = true;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private float _groundCheckHeight = 10f;

    #endregion

    #region Properties

    /// <summary>Type de spawn point.</summary>
    public WaveSpawnType SpawnType => _spawnType;

    /// <summary>Actif?</summary>
    public bool IsActive => _isActive;

    /// <summary>Position centrale.</summary>
    public Vector3 Position => transform.position;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (WaveManager.Instance != null)
        {
            // Le WaveManager utilise ses propres points de spawn
        }
    }

    private void OnDrawGizmos()
    {
        if (!_showGizmo) return;

        Gizmos.color = _gizmoColor;

        switch (_spawnType)
        {
            case WaveSpawnType.Point:
                Gizmos.DrawWireSphere(transform.position, 0.5f);
                Gizmos.DrawRay(transform.position, transform.forward * 2f);
                break;

            case WaveSpawnType.Circle:
                DrawCircleGizmo();
                break;

            case WaveSpawnType.Box:
                Gizmos.DrawWireCube(transform.position, _spawnArea);
                break;

            case WaveSpawnType.Edge:
                Vector3 start = transform.position - transform.right * (_spawnArea.x / 2f);
                Vector3 end = transform.position + transform.right * (_spawnArea.x / 2f);
                Gizmos.DrawLine(start, end);
                Gizmos.DrawWireSphere(start, 0.2f);
                Gizmos.DrawWireSphere(end, 0.2f);
                break;
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

            Vector3 p1 = center + new Vector3(Mathf.Cos(angle1), 0, Mathf.Sin(angle1)) * _spawnRadius;
            Vector3 p2 = center + new Vector3(Mathf.Cos(angle2), 0, Mathf.Sin(angle2)) * _spawnRadius;

            Gizmos.DrawLine(p1, p2);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Obtient une position de spawn.
    /// </summary>
    public Vector3 GetSpawnPosition()
    {
        if (!_isActive) return transform.position;

        Vector3 position;

        switch (_spawnType)
        {
            case WaveSpawnType.Point:
                position = transform.position;
                break;

            case WaveSpawnType.Circle:
                Vector2 randomCircle = Random.insideUnitCircle * _spawnRadius;
                position = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
                break;

            case WaveSpawnType.Box:
                position = transform.position + new Vector3(
                    Random.Range(-_spawnArea.x / 2f, _spawnArea.x / 2f),
                    Random.Range(-_spawnArea.y / 2f, _spawnArea.y / 2f),
                    Random.Range(-_spawnArea.z / 2f, _spawnArea.z / 2f)
                );
                break;

            case WaveSpawnType.Edge:
                float t = Random.value;
                Vector3 start = transform.position - transform.right * (_spawnArea.x / 2f);
                Vector3 end = transform.position + transform.right * (_spawnArea.x / 2f);
                position = Vector3.Lerp(start, end, t);
                break;

            default:
                position = transform.position;
                break;
        }

        // Snap au sol si active
        if (_snapToGround)
        {
            position = SnapToGround(position);
        }

        return position;
    }

    /// <summary>
    /// Obtient une rotation de spawn.
    /// </summary>
    public Quaternion GetSpawnRotation()
    {
        if (_randomRotation)
        {
            return Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        }
        return transform.rotation;
    }

    /// <summary>
    /// Active/desactive le point de spawn.
    /// </summary>
    public void SetActive(bool active)
    {
        _isActive = active;
    }

    /// <summary>
    /// Configure le point de spawn.
    /// </summary>
    public void Configure(WaveSpawnType type, float radius, Vector3 area)
    {
        _spawnType = type;
        _spawnRadius = radius;
        _spawnArea = area;
    }

    #endregion

    #region Private Methods

    private Vector3 SnapToGround(Vector3 position)
    {
        Vector3 rayStart = position + Vector3.up * _groundCheckHeight;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, _groundCheckHeight * 2f, _groundLayer))
        {
            return hit.point;
        }

        return position;
    }

    #endregion
}

/// <summary>
/// Types de point de spawn pour les vagues.
/// </summary>
public enum WaveSpawnType
{
    /// <summary>Point unique.</summary>
    Point,

    /// <summary>Zone circulaire.</summary>
    Circle,

    /// <summary>Zone rectangulaire.</summary>
    Box,

    /// <summary>Ligne/bord.</summary>
    Edge
}
