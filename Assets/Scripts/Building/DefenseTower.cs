using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Composant pour les tours de defense.
/// Gere le ciblage et l'attaque des ennemis.
/// </summary>
public class DefenseTower : MonoBehaviour
{
    #region Fields

    [Header("Configuration")]
    [SerializeField] private TowerType _towerType = TowerType.Arrow;
    [SerializeField] private float _range = 10f;
    [SerializeField] private float _damage = 10f;
    [SerializeField] private float _fireRate = 1f;
    [SerializeField] private DamageType _damageType = DamageType.Physical;

    [Header("Ciblage")]
    [SerializeField] private TargetingMode _targetingMode = TargetingMode.Closest;
    [SerializeField] private LayerMask _targetLayer;
    [SerializeField] private Transform _turretHead;
    [SerializeField] private float _rotationSpeed = 180f;

    [Header("Projectile")]
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private float _projectileSpeed = 20f;

    [Header("Effets")]
    [SerializeField] private AudioClip _fireSound;
    [SerializeField] private GameObject _muzzleFlash;

    // Etat
    private Transform _currentTarget;
    private float _fireCooldown = 0f;
    private bool _isActive = true;
    private List<Transform> _targetsInRange = new List<Transform>();

    #endregion

    #region Properties

    /// <summary>Type de tour.</summary>
    public TowerType TowerType => _towerType;

    /// <summary>Portee.</summary>
    public float Range => _range;

    /// <summary>Degats.</summary>
    public float Damage => _damage;

    /// <summary>Cadence de tir.</summary>
    public float FireRate => _fireRate;

    /// <summary>Cible actuelle.</summary>
    public Transform CurrentTarget => _currentTarget;

    /// <summary>Active?</summary>
    public bool IsActive => _isActive;

    /// <summary>Pret a tirer?</summary>
    public bool CanFire => _fireCooldown <= 0f && _currentTarget != null && _isActive;

    /// <summary>Nombre de cibles en range.</summary>
    public int TargetsInRange => _targetsInRange.Count;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        if (!_isActive) return;

        UpdateCooldown();
        UpdateTargets();
        SelectTarget();
        RotateTurret();

        if (CanFire)
        {
            Fire();
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Dessiner la portee
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _range);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Active/desactive la tour.
    /// </summary>
    public void SetActive(bool active)
    {
        _isActive = active;

        if (!active)
        {
            _currentTarget = null;
        }
    }

    /// <summary>
    /// Change le mode de ciblage.
    /// </summary>
    public void SetTargetingMode(TargetingMode mode)
    {
        _targetingMode = mode;
    }

    /// <summary>
    /// Force un changement de cible.
    /// </summary>
    public void ForceRetarget()
    {
        _currentTarget = null;
        SelectTarget();
    }

    /// <summary>
    /// Ameliore les stats de la tour.
    /// </summary>
    public void Upgrade(float damageMultiplier, float rangeMultiplier, float fireRateMultiplier)
    {
        _damage *= damageMultiplier;
        _range *= rangeMultiplier;
        _fireRate *= fireRateMultiplier;
    }

    #endregion

    #region Private Methods

    private void UpdateCooldown()
    {
        if (_fireCooldown > 0)
        {
            _fireCooldown -= Time.deltaTime;
        }
    }

    private void UpdateTargets()
    {
        _targetsInRange.Clear();

        Collider[] colliders = Physics.OverlapSphere(transform.position, _range, _targetLayer);

        foreach (var collider in colliders)
        {
            // Verifier que c'est un ennemi valide
            var damageable = collider.GetComponent<IDamageable>();
            if (damageable != null && !damageable.IsDead)
            {
                _targetsInRange.Add(collider.transform);
            }
        }

        // Verifier que la cible actuelle est toujours valide
        if (_currentTarget != null)
        {
            if (!_targetsInRange.Contains(_currentTarget))
            {
                _currentTarget = null;
            }
        }
    }

    private void SelectTarget()
    {
        if (_targetsInRange.Count == 0)
        {
            _currentTarget = null;
            return;
        }

        // Si on a deja une cible valide, la garder
        if (_currentTarget != null && _targetsInRange.Contains(_currentTarget))
        {
            return;
        }

        // Selectionner une nouvelle cible
        _currentTarget = _targetingMode switch
        {
            TargetingMode.Closest => GetClosestTarget(),
            TargetingMode.Farthest => GetFarthestTarget(),
            TargetingMode.LowestHealth => GetLowestHealthTarget(),
            TargetingMode.HighestHealth => GetHighestHealthTarget(),
            TargetingMode.First => _targetsInRange[0],
            TargetingMode.Random => _targetsInRange[Random.Range(0, _targetsInRange.Count)],
            _ => GetClosestTarget()
        };
    }

    private Transform GetClosestTarget()
    {
        Transform closest = null;
        float minDist = float.MaxValue;

        foreach (var target in _targetsInRange)
        {
            float dist = Vector3.Distance(transform.position, target.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = target;
            }
        }

        return closest;
    }

    private Transform GetFarthestTarget()
    {
        Transform farthest = null;
        float maxDist = 0f;

        foreach (var target in _targetsInRange)
        {
            float dist = Vector3.Distance(transform.position, target.position);
            if (dist > maxDist)
            {
                maxDist = dist;
                farthest = target;
            }
        }

        return farthest;
    }

    private Transform GetLowestHealthTarget()
    {
        Transform lowest = null;
        float minHealth = float.MaxValue;

        foreach (var target in _targetsInRange)
        {
            var health = target.GetComponent<Health>();
            if (health != null && health.CurrentHealth < minHealth)
            {
                minHealth = health.CurrentHealth;
                lowest = target;
            }
        }

        return lowest ?? GetClosestTarget();
    }

    private Transform GetHighestHealthTarget()
    {
        Transform highest = null;
        float maxHealth = 0f;

        foreach (var target in _targetsInRange)
        {
            var health = target.GetComponent<Health>();
            if (health != null && health.CurrentHealth > maxHealth)
            {
                maxHealth = health.CurrentHealth;
                highest = target;
            }
        }

        return highest ?? GetClosestTarget();
    }

    private void RotateTurret()
    {
        if (_turretHead == null || _currentTarget == null) return;

        Vector3 direction = _currentTarget.position - _turretHead.position;
        direction.y = 0; // Garder la rotation horizontale

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            _turretHead.rotation = Quaternion.RotateTowards(
                _turretHead.rotation,
                targetRotation,
                _rotationSpeed * Time.deltaTime
            );
        }
    }

    private void Fire()
    {
        if (_currentTarget == null) return;

        // Creer le projectile
        if (_projectilePrefab != null && _firePoint != null)
        {
            var projectileGO = Instantiate(_projectilePrefab, _firePoint.position, _firePoint.rotation);
            var projectile = projectileGO.GetComponent<Projectile>();

            if (projectile != null)
            {
                var damageInfo = new DamageInfo(_damage, _damageType, gameObject);
                damageInfo.hitPoint = _currentTarget.position;

                projectile.Initialize(_currentTarget, _projectileSpeed, damageInfo);
            }
        }
        else
        {
            // Tir direct (hitscan)
            var damageable = _currentTarget.GetComponent<IDamageable>();
            if (damageable != null)
            {
                var damageInfo = new DamageInfo(_damage, _damageType, gameObject);
                damageInfo.hitPoint = _currentTarget.position;

                damageable.TakeDamage(damageInfo);
            }
        }

        // Effets
        if (_fireSound != null)
        {
            AudioSource.PlayClipAtPoint(_fireSound, transform.position);
        }

        if (_muzzleFlash != null && _firePoint != null)
        {
            var flash = Instantiate(_muzzleFlash, _firePoint.position, _firePoint.rotation);
            Destroy(flash, 0.5f);
        }

        // Cooldown
        _fireCooldown = 1f / _fireRate;
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Configure la tour.
    /// </summary>
    public void Configure(TowerType type, float range, float damage, float fireRate)
    {
        _towerType = type;
        _range = range;
        _damage = damage;
        _fireRate = fireRate;
    }

    #endregion
}

/// <summary>
/// Types de tours de defense.
/// </summary>
public enum TowerType
{
    /// <summary>Tour a fleches, degats physiques.</summary>
    Arrow,

    /// <summary>Canon, degats explosifs.</summary>
    Cannon,

    /// <summary>Tour magique, degats elementaires.</summary>
    Magic,

    /// <summary>Tour de givre, ralentit les ennemis.</summary>
    Frost,

    /// <summary>Tour eclair, attaque en chaine.</summary>
    Lightning,

    /// <summary>Tour de soutien, buff les autres tours.</summary>
    Support
}

/// <summary>
/// Modes de ciblage.
/// </summary>
public enum TargetingMode
{
    /// <summary>Cible la plus proche.</summary>
    Closest,

    /// <summary>Cible la plus eloignee.</summary>
    Farthest,

    /// <summary>Cible avec le moins de vie.</summary>
    LowestHealth,

    /// <summary>Cible avec le plus de vie.</summary>
    HighestHealth,

    /// <summary>Premier ennemi detecte.</summary>
    First,

    /// <summary>Cible aleatoire.</summary>
    Random
}
