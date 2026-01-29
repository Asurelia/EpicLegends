using UnityEngine;

/// <summary>
/// Systeme de monture pour les creatures.
/// Permet au joueur de monter les creatures qui le permettent.
/// </summary>
public class MountSystem : MonoBehaviour
{
    #region Fields

    [Header("References")]
    [SerializeField] private CreatureController _creatureController;
    [SerializeField] private Transform _mountPoint;
    [SerializeField] private Transform _dismountPoint;

    [Header("Settings")]
    [SerializeField] private float _mountSpeed = 8f;
    [SerializeField] private float _sprintMultiplier = 1.5f;
    [SerializeField] private float _turnSpeed = 120f;
    [SerializeField] private float _mountCooldown = 1f;

    [Header("Flying")]
    [SerializeField] private float _flySpeed = 12f;
    [SerializeField] private float _flyAscendSpeed = 5f;
    [SerializeField] private float _flyDescendSpeed = 8f;
    [SerializeField] private float _maxFlightHeight = 50f;

    [Header("Animation")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _mountTrigger = "Mount";
    [SerializeField] private string _dismountTrigger = "Dismount";
    [SerializeField] private string _flyTrigger = "Fly";

    // Etat
    private Transform _rider;
    private bool _isMounted;
    private bool _isFlying;
    private float _lastMountTime;
    private Vector3 _originalRiderPosition;
    private Transform _originalRiderParent;

    // Cache
    private Rigidbody _rb;
    private CharacterController _riderController;

    #endregion

    #region Events

    public event System.Action<Transform> OnMounted;
    public event System.Action<Transform> OnDismounted;
    public event System.Action OnFlightStarted;
    public event System.Action OnFlightEnded;

    #endregion

    #region Properties

    public bool IsMounted => _isMounted;
    public bool IsFlying => _isFlying;
    public Transform Rider => _rider;
    public float CurrentSpeed => _isMounted ? (_isFlying ? _flySpeed : _mountSpeed) : 0f;

    public bool CanFly => _creatureController?.Data?.canFly ?? false;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (_creatureController == null)
            _creatureController = GetComponent<CreatureController>();

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        // Creer le point de montage par defaut
        if (_mountPoint == null)
        {
            var mountGO = new GameObject("MountPoint");
            mountGO.transform.SetParent(transform);
            mountGO.transform.localPosition = Vector3.up * 1.5f;
            _mountPoint = mountGO.transform;
        }

        // Creer le point de demontage par defaut
        if (_dismountPoint == null)
        {
            var dismountGO = new GameObject("DismountPoint");
            dismountGO.transform.SetParent(transform);
            dismountGO.transform.localPosition = Vector3.right * 2f;
            _dismountPoint = dismountGO.transform;
        }
    }

    private void Update()
    {
        if (_isMounted && _rider != null)
        {
            UpdateRiderPosition();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Verifie si la creature peut etre montee.
    /// </summary>
    public bool CanBeMounted()
    {
        if (_creatureController == null) return false;
        if (_creatureController.Data == null) return false;
        if (_creatureController.IsFainted) return false;
        if (_isMounted) return false;
        if (Time.time - _lastMountTime < _mountCooldown) return false;

        return _creatureController.Data.CanMount();
    }

    /// <summary>
    /// Monte sur la creature.
    /// </summary>
    public bool Mount(Transform rider)
    {
        if (!CanBeMounted()) return false;
        if (rider == null) return false;

        _rider = rider;
        _isMounted = true;
        _lastMountTime = Time.time;

        // Sauvegarder la position originale
        _originalRiderPosition = rider.position;
        _originalRiderParent = rider.parent;

        // Attacher le cavalier
        rider.SetParent(_mountPoint);
        rider.localPosition = Vector3.zero;
        rider.localRotation = Quaternion.identity;

        // Desactiver le controller du cavalier
        _riderController = rider.GetComponent<CharacterController>();
        if (_riderController != null)
        {
            _riderController.enabled = false;
        }

        // Animation
        if (_animator != null)
        {
            _animator.SetTrigger(_mountTrigger);
        }

        OnMounted?.Invoke(rider);
        return true;
    }

    /// <summary>
    /// Descend de la creature.
    /// </summary>
    public bool Dismount()
    {
        if (!_isMounted || _rider == null) return false;

        // Position de demontage
        Vector3 dismountPos = _dismountPoint != null
            ? _dismountPoint.position
            : transform.position + transform.right * 2f;

        // Verifier que la position est valide (pas dans le sol)
        if (Physics.Raycast(dismountPos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 5f))
        {
            dismountPos = hit.point;
        }

        // Detacher le cavalier
        _rider.SetParent(_originalRiderParent);
        _rider.position = dismountPos;

        // Reactiver le controller du cavalier
        if (_riderController != null)
        {
            _riderController.enabled = true;
        }

        // Arreter le vol
        if (_isFlying)
        {
            StopFlying();
        }

        // Animation
        if (_animator != null)
        {
            _animator.SetTrigger(_dismountTrigger);
        }

        OnDismounted?.Invoke(_rider);

        _rider = null;
        _isMounted = false;
        _riderController = null;

        return true;
    }

    /// <summary>
    /// Commence a voler.
    /// </summary>
    public bool StartFlying()
    {
        if (!_isMounted) return false;
        if (!CanFly) return false;
        if (_isFlying) return false;

        _isFlying = true;

        // Animation
        if (_animator != null)
        {
            _animator.SetTrigger(_flyTrigger);
            _animator.SetBool("IsFlying", true);
        }

        // Desactiver la gravite
        if (_rb != null)
        {
            _rb.useGravity = false;
        }

        OnFlightStarted?.Invoke();
        return true;
    }

    /// <summary>
    /// Arrete de voler.
    /// </summary>
    public bool StopFlying()
    {
        if (!_isFlying) return false;

        _isFlying = false;

        // Animation
        if (_animator != null)
        {
            _animator.SetBool("IsFlying", false);
        }

        // Reactiver la gravite
        if (_rb != null)
        {
            _rb.useGravity = true;
        }

        OnFlightEnded?.Invoke();
        return true;
    }

    /// <summary>
    /// Deplace la monture.
    /// </summary>
    public void Move(Vector3 direction, bool sprint = false)
    {
        if (!_isMounted) return;

        float speed = _isFlying ? _flySpeed : _mountSpeed;
        if (sprint) speed *= _sprintMultiplier;

        // Appliquer la vitesse de la creature
        if (_creatureController?.Data != null)
        {
            speed *= _isFlying
                ? _creatureController.Data.flySpeed / 12f
                : _creatureController.Data.mountSpeed / 8f;
        }

        Vector3 movement = direction.normalized * speed;

        if (_rb != null)
        {
            _rb.linearVelocity = new Vector3(movement.x, _rb.linearVelocity.y, movement.z);
        }
        else
        {
            transform.position += movement * Time.deltaTime;
        }

        // Rotation vers la direction
        if (direction.magnitude > 0.1f)
        {
            Vector3 lookDir = new Vector3(direction.x, 0, direction.z);
            if (lookDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    _turnSpeed * Time.deltaTime
                );
            }
        }

        // Animation
        if (_animator != null)
        {
            _animator.SetFloat("Speed", direction.magnitude);
            _animator.SetBool("IsSprinting", sprint);
        }
    }

    /// <summary>
    /// Monte ou descend en vol.
    /// </summary>
    public void FlyVertical(float direction)
    {
        if (!_isFlying) return;

        float speed = direction > 0 ? _flyAscendSpeed : _flyDescendSpeed;

        // Limiter la hauteur
        if (direction > 0 && transform.position.y >= _maxFlightHeight)
        {
            return;
        }

        // Verifier le sol
        if (direction < 0)
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2f))
            {
                // Atterrir
                StopFlying();
                return;
            }
        }

        if (_rb != null)
        {
            Vector3 velocity = _rb.linearVelocity;
            velocity.y = direction * speed;
            _rb.linearVelocity = velocity;
        }
        else
        {
            transform.position += Vector3.up * direction * speed * Time.deltaTime;
        }
    }

    /// <summary>
    /// Fait tourner la monture.
    /// </summary>
    public void Turn(float amount)
    {
        if (!_isMounted) return;

        transform.Rotate(Vector3.up, amount * _turnSpeed * Time.deltaTime);
    }

    #endregion

    #region Private Methods

    private void UpdateRiderPosition()
    {
        if (_rider == null || _mountPoint == null) return;

        // S'assurer que le cavalier reste au point de montage
        _rider.position = _mountPoint.position;
        _rider.rotation = _mountPoint.rotation;
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        // Point de montage
        if (_mountPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_mountPoint.position, 0.3f);
        }

        // Point de demontage
        if (_dismountPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_dismountPoint.position, 0.3f);
        }

        // Hauteur max de vol
        if (_creatureController?.Data?.canFly ?? false)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(
                transform.position,
                transform.position + Vector3.up * _maxFlightHeight
            );
        }
    }

    #endregion
}
