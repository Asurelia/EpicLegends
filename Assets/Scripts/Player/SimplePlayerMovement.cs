using UnityEngine;

/// <summary>
/// Mouvement simple du joueur pour les tests.
/// Utilise les anciennes inputs pour compatibilite maximale.
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
        // Input
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

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

        _moveDirection = (forward * vertical + right * horizontal).normalized;

        // Sprint
        float speed = _moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            speed *= _sprintMultiplier;
        }

        // Jump
        if (Input.GetKeyDown(KeyCode.Space) && _isGrounded)
        {
            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
        }

        // Rotate toward movement direction
        if (_moveDirection.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(_moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }

        // Escape to unlock cursor
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked
                ? CursorLockMode.None
                : CursorLockMode.Locked;
            Cursor.visible = !Cursor.visible;
        }
    }

    private void FixedUpdate()
    {
        // Ground check
        _isGrounded = Physics.Raycast(transform.position, Vector3.down, _groundCheckDistance + 0.1f, _groundLayer);

        // Apply movement
        if (_moveDirection.magnitude > 0.1f)
        {
            float speed = _moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift))
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
