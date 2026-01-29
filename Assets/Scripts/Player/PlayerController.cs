using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls player movement, rotation and basic actions.
/// Uses the new Input System and Rigidbody-based physics.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _sprintMultiplier = 1.5f;
    [SerializeField] private float _rotationSpeed = 10f;

    [Header("Jump Settings")]
    [SerializeField] private float _jumpForce = 8f;
    [SerializeField] private float _groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask _groundLayer;

    [Header("Camera Reference")]
    [SerializeField] private Transform _cameraTransform;

    // Cached components
    private Rigidbody _rb;
    private CapsuleCollider _capsuleCollider;

    // Input values
    private Vector2 _moveInput;
    private bool _isSprinting;
    private bool _jumpRequested;

    // State
    private bool _isGrounded;

    #region Unity Callbacks

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _capsuleCollider = GetComponent<CapsuleCollider>();

        // Configure rigidbody for character movement
        _rb.freezeRotation = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        // If no camera assigned, try to find main camera
        if (_cameraTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                _cameraTransform = mainCam.transform;
            }
        }
    }

    private void Update()
    {
        // Ground check in Update for responsiveness
        CheckGrounded();
    }

    private void FixedUpdate()
    {
        // Physics-based movement in FixedUpdate
        HandleMovement();
        HandleJump();
    }

    #endregion

    #region Input System Callbacks

    /// <summary>
    /// Called by Input System when Move action is performed.
    /// </summary>
    public void OnMove(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }

    /// <summary>
    /// Called by Input System when Sprint action is performed.
    /// </summary>
    public void OnSprint(InputAction.CallbackContext context)
    {
        _isSprinting = context.performed;
    }

    /// <summary>
    /// Called by Input System when Jump action is performed.
    /// </summary>
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.started && _isGrounded)
        {
            _jumpRequested = true;
        }
    }

    #endregion

    #region Movement Logic

    private void HandleMovement()
    {
        if (_moveInput.sqrMagnitude < 0.01f)
        {
            return;
        }

        // Calculate movement direction relative to camera
        Vector3 forward = _cameraTransform != null
            ? _cameraTransform.forward
            : transform.forward;
        Vector3 right = _cameraTransform != null
            ? _cameraTransform.right
            : transform.right;

        // Remove vertical component for ground movement
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        // Calculate move direction
        Vector3 moveDirection = (forward * _moveInput.y + right * _moveInput.x).normalized;

        // Calculate speed with sprint modifier
        float currentSpeed = _isSprinting ? _moveSpeed * _sprintMultiplier : _moveSpeed;

        // Apply movement
        Vector3 targetPosition = _rb.position + moveDirection * currentSpeed * Time.fixedDeltaTime;
        _rb.MovePosition(targetPosition);

        // Rotate to face movement direction
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            _rb.rotation = Quaternion.Slerp(_rb.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime);
        }
    }

    private void HandleJump()
    {
        if (_jumpRequested && _isGrounded)
        {
            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            _jumpRequested = false;
        }
    }

    private void CheckGrounded()
    {
        // Raycast from center of capsule collider
        Vector3 origin = transform.position + Vector3.up * (_capsuleCollider.radius);

        _isGrounded = Physics.Raycast(
            origin,
            Vector3.down,
            _capsuleCollider.radius + _groundCheckDistance,
            _groundLayer
        );
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Returns true if player is currently on the ground.
    /// </summary>
    public bool IsGrounded => _isGrounded;

    /// <summary>
    /// Returns true if player is currently sprinting.
    /// </summary>
    public bool IsSprinting => _isSprinting;

    /// <summary>
    /// Returns the current movement speed (including sprint).
    /// </summary>
    public float CurrentSpeed => _isSprinting ? _moveSpeed * _sprintMultiplier : _moveSpeed;

    #endregion

    #region Debug

    private void OnDrawGizmosSelected()
    {
        // Visualize ground check
        if (_capsuleCollider == null) return;

        Vector3 origin = transform.position + Vector3.up * (_capsuleCollider.radius);
        float distance = _capsuleCollider.radius + _groundCheckDistance;

        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(origin, origin + Vector3.down * distance);
    }

    #endregion
}
