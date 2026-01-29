using UnityEngine;

/// <summary>
/// Projectile tire par les tours de defense.
/// </summary>
public class Projectile : MonoBehaviour
{
    #region Fields

    [Header("Configuration")]
    [SerializeField] private float _speed = 20f;
    [SerializeField] private float _maxLifetime = 5f;
    [SerializeField] private bool _isHoming = true;
    [SerializeField] private float _homingStrength = 5f;

    [Header("Effets")]
    [SerializeField] private GameObject _impactVFX;
    [SerializeField] private AudioClip _impactSound;
    [SerializeField] private bool _destroyOnImpact = true;

    // Etat
    private Transform _target;
    private DamageInfo _damageInfo;
    private float _lifetime = 0f;
    private Vector3 _lastTargetPosition;
    private bool _isInitialized = false;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        if (!_isInitialized) return;

        _lifetime += Time.deltaTime;

        if (_lifetime >= _maxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        MoveTowardsTarget();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isInitialized) return;

        var damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            ApplyDamage(damageable, other.transform.position);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Initialise le projectile.
    /// </summary>
    public void Initialize(Transform target, float speed, DamageInfo damageInfo)
    {
        _target = target;
        _speed = speed;
        _damageInfo = damageInfo;
        _isInitialized = true;

        if (target != null)
        {
            _lastTargetPosition = target.position;
        }
    }

    #endregion

    #region Private Methods

    private void MoveTowardsTarget()
    {
        Vector3 targetPosition;

        if (_target != null)
        {
            targetPosition = _target.position;
            _lastTargetPosition = targetPosition;
        }
        else
        {
            // La cible a ete detruite, continuer vers la derniere position connue
            targetPosition = _lastTargetPosition;
        }

        Vector3 direction = (targetPosition - transform.position).normalized;

        if (_isHoming && _target != null)
        {
            // Rotation vers la cible
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                _homingStrength * Time.deltaTime
            );

            // Se deplacer vers l'avant
            transform.position += transform.forward * _speed * Time.deltaTime;
        }
        else
        {
            // Deplacement direct
            transform.position += direction * _speed * Time.deltaTime;
        }

        // Verifier si on a atteint la cible (pour les projectiles non-homing)
        if (Vector3.Distance(transform.position, targetPosition) < 0.5f)
        {
            if (_target != null)
            {
                var damageable = _target.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    ApplyDamage(damageable, targetPosition);
                    return;
                }
            }

            // Impact au sol
            OnImpact(targetPosition);
        }
    }

    private void ApplyDamage(IDamageable target, Vector3 hitPoint)
    {
        _damageInfo.hitPoint = hitPoint;
        target.TakeDamage(_damageInfo);

        OnImpact(hitPoint);
    }

    private void OnImpact(Vector3 position)
    {
        // Effets d'impact
        if (_impactVFX != null)
        {
            Instantiate(_impactVFX, position, Quaternion.identity);
        }

        if (_impactSound != null)
        {
            AudioSource.PlayClipAtPoint(_impactSound, position);
        }

        if (_destroyOnImpact)
        {
            Destroy(gameObject);
        }
    }

    #endregion
}
