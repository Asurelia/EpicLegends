using UnityEngine;

/// <summary>
/// Handles player animation based on movement state.
/// Requires an Animator component on the player model child object.
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator _animator;
    [SerializeField] private PlayerController _playerController;

    [Header("Animation Parameters")]
    [SerializeField] private string _speedParam = "Speed";
    [SerializeField] private string _isGroundedParam = "IsGrounded";
    [SerializeField] private string _isSprintingParam = "IsSprinting";
    [SerializeField] private string _jumpTrigger = "Jump";
    [SerializeField] private string _attackTrigger = "Attack";

    // Animation state hashes for performance
    private int _speedHash;
    private int _isGroundedHash;
    private int _isSprintingHash;
    private int _jumpHash;
    private int _attackHash;

    // State tracking
    private bool _wasGrounded = true;
    private Vector3 _lastPosition;
    private float _currentSpeed;

    private void Awake()
    {
        // Cache parameter hashes
        _speedHash = Animator.StringToHash(_speedParam);
        _isGroundedHash = Animator.StringToHash(_isGroundedParam);
        _isSprintingHash = Animator.StringToHash(_isSprintingParam);
        _jumpHash = Animator.StringToHash(_jumpTrigger);
        _attackHash = Animator.StringToHash(_attackTrigger);

        // Auto-find components if not assigned
        if (_playerController == null)
        {
            _playerController = GetComponent<PlayerController>();
        }

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }

        _lastPosition = transform.position;
    }

    private void Update()
    {
        if (_animator == null || _playerController == null) return;

        UpdateMovementAnimation();
        UpdateGroundedState();
    }

    private void UpdateMovementAnimation()
    {
        // Calculate actual movement speed
        Vector3 horizontalMovement = transform.position - _lastPosition;
        horizontalMovement.y = 0f;
        float targetSpeed = horizontalMovement.magnitude / Time.deltaTime;

        // Smooth speed transition
        _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, Time.deltaTime * 10f);

        // Normalize speed (0 = idle, 0.5 = walk, 1 = run)
        // Avoid division by zero if CurrentSpeed is 0
        float maxSpeed = _playerController.CurrentSpeed;
        float normalizedSpeed = maxSpeed > 0.01f ? Mathf.Clamp01(_currentSpeed / maxSpeed) : 0f;

        // Update animator
        _animator.SetFloat(_speedHash, normalizedSpeed);
        _animator.SetBool(_isSprintingHash, _playerController.IsSprinting && normalizedSpeed > 0.1f);

        _lastPosition = transform.position;
    }

    private void UpdateGroundedState()
    {
        bool isGrounded = _playerController.IsGrounded;
        _animator.SetBool(_isGroundedHash, isGrounded);

        // Detect landing
        if (isGrounded && !_wasGrounded)
        {
            OnLanded();
        }
        // Detect jump start
        else if (!isGrounded && _wasGrounded)
        {
            OnJumpStart();
        }

        _wasGrounded = isGrounded;
    }

    private void OnJumpStart()
    {
        _animator.SetTrigger(_jumpHash);
    }

    private void OnLanded()
    {
        // Could trigger landing animation or effects here
    }

    /// <summary>
    /// Trigger attack animation. Called by CombatController.
    /// </summary>
    public void PlayAttackAnimation()
    {
        if (_animator != null)
        {
            _animator.SetTrigger(_attackHash);
        }
    }

    /// <summary>
    /// Play a specific animation by trigger name.
    /// </summary>
    public void PlayTrigger(string triggerName)
    {
        if (_animator != null)
        {
            _animator.SetTrigger(triggerName);
        }
    }

    /// <summary>
    /// Set animation speed multiplier.
    /// </summary>
    public void SetAnimationSpeed(float speed)
    {
        if (_animator != null)
        {
            _animator.speed = speed;
        }
    }
}
