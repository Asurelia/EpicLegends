using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mouvement simple du joueur pour les tests.
/// Utilise le nouveau Input System.
/// </summary>
public class SimplePlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 10f;
    [SerializeField] private float _sprintMultiplier = 1.5f;
    [SerializeField] private float _jumpForce = 8f;
    [SerializeField] private float _rotationSpeed = 10f;

    [Header("Ground Check")]
    [SerializeField] private float _groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask _groundLayer = -1;

    [Header("Camera")]
    [SerializeField] private Transform _cameraTransform;

    private Rigidbody _rb;
    private bool _isGrounded;
    private Vector3 _moveDirection;
    private Vector2 _moveInput;
    private bool _sprintPressed;
    private bool _jumpRequested; // CRITICAL FIX: Renamed to indicate it's a request, not a state

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (_rb != null)
        {
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    private void Start()
    {
        // Trouver la camera principale si non assignee
        if (_cameraTransform == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                _cameraTransform = cam.transform;
            }
        }

        // Lock cursor pour le jeu
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // Read input using new Input System
        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;

        if (keyboard != null)
        {
            // Movement WASD
            float horizontal = 0f;
            float vertical = 0f;

            if (keyboard.wKey.isPressed) vertical += 1f;
            if (keyboard.sKey.isPressed) vertical -= 1f;
            if (keyboard.dKey.isPressed) horizontal += 1f;
            if (keyboard.aKey.isPressed) horizontal -= 1f;

            _moveInput = new Vector2(horizontal, vertical);
            _sprintPressed = keyboard.leftShiftKey.isPressed;
            // CRITICAL FIX: Use |= to not lose the input between frames before FixedUpdate
            if (keyboard.spaceKey.wasPressedThisFrame)
                _jumpRequested = true;

            // Escape to unlock cursor
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked
                    ? CursorLockMode.None
                    : CursorLockMode.Locked;
                Cursor.visible = !Cursor.visible;
            }
        }
        else if (gamepad != null)
        {
            _moveInput = gamepad.leftStick.ReadValue();
            _sprintPressed = gamepad.leftTrigger.isPressed;
            // CRITICAL FIX: Use |= to not lose the input between frames before FixedUpdate
            if (gamepad.buttonSouth.wasPressedThisFrame)
                _jumpRequested = true;
        }

        // Direction relative a la camera
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        if (_cameraTransform != null)
        {
            forward = _cameraTransform.forward;
            right = _cameraTransform.right;
        }

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        _moveDirection = (forward * _moveInput.y + right * _moveInput.x).normalized;

        // CRITICAL FIX: Jump is now handled in FixedUpdate (physics operation)
        // Input is captured here but force is applied in FixedUpdate

        // Rotate toward movement direction
        if (_moveDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(_moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        // Ground check
        _isGrounded = Physics.Raycast(transform.position, Vector3.down, _groundCheckDistance + 0.1f, _groundLayer);

        // CRITICAL FIX: Jump (physics operation) now in FixedUpdate
        if (_jumpRequested && _isGrounded)
        {
            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
        }
        _jumpRequested = false; // Reset after processing

        // Apply movement
        if (_moveDirection.magnitude > 0.1f)
        {
            float speed = _moveSpeed;
            if (_sprintPressed)
            {
                speed *= _sprintMultiplier;
            }

            Vector3 velocity = _moveDirection * speed;
            velocity.y = _rb.linearVelocity.y;
            _rb.linearVelocity = velocity;
        }
        else
        {
            // Stop horizontal movement when not moving
            Vector3 velocity = _rb.linearVelocity;
            velocity.x = Mathf.Lerp(velocity.x, 0, 10f * Time.fixedDeltaTime);
            velocity.z = Mathf.Lerp(velocity.z, 0, 10f * Time.fixedDeltaTime);
            _rb.linearVelocity = velocity;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * _groundCheckDistance);
    }
}
