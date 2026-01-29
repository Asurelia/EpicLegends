using System;
using UnityEngine;

/// <summary>
/// Gère le dash/esquive du joueur avec i-frames (invincibilité temporaire).
/// Supporte 8 directions et s'intègre avec le système de stamina.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DashAbility : MonoBehaviour
{
    #region Serialized Fields

    [Header("Paramètres du Dash")]
    [SerializeField] private float _dashDistance = 5f;
    [SerializeField] private float _dashDuration = 0.2f;
    [SerializeField] private float _dashCooldown = 0.8f;

    [Header("I-Frames")]
    [SerializeField] private float _iFrameDuration = 0.15f;

    [Header("Stamina")]
    [SerializeField] private float _staminaCost = 20f;

    [Header("Référence Caméra")]
    [SerializeField] private Transform _cameraTransform;

    #endregion

    #region Private Fields

    private Rigidbody _rb;
    private float _dashTimer;
    private float _cooldownTimer;
    private float _iFrameTimer;
    private Vector3 _dashDirection;
    private Vector3 _dashStartPosition;
    private bool _isDashing;
    private bool _isInvincible;

    #endregion

    #region Events

    /// <summary>
    /// Déclenché au début du dash.
    /// </summary>
    public event Action OnDashStarted;

    /// <summary>
    /// Déclenché à la fin du dash.
    /// </summary>
    public event Action OnDashEnded;

    /// <summary>
    /// Déclenché quand les i-frames se terminent.
    /// </summary>
    public event Action OnInvincibilityEnded;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

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
        UpdateCooldown();
        UpdateDash();
        UpdateIFrames();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Tente d'exécuter un dash dans la direction spécifiée.
    /// </summary>
    /// <param name="inputDirection">Direction 2D du stick/clavier (peut être zéro pour dash vers l'avant)</param>
    /// <returns>True si le dash a été exécuté</returns>
    public bool TryDash(Vector2 inputDirection)
    {
        if (!CanDash)
        {
            return false;
        }

        // Calculer la direction 3D du dash
        _dashDirection = CalculateDashDirection(inputDirection);
        _dashStartPosition = transform.position;

        // Démarrer le dash
        _isDashing = true;
        _dashTimer = _dashDuration;
        _cooldownTimer = _dashCooldown;

        // Activer les i-frames
        _isInvincible = true;
        _iFrameTimer = _iFrameDuration;

        OnDashStarted?.Invoke();

        return true;
    }

    /// <summary>
    /// Force l'arrêt du dash (utilisé en cas de collision).
    /// </summary>
    public void CancelDash()
    {
        if (_isDashing)
        {
            EndDash();
        }
    }

    #endregion

    #region Private Methods

    private Vector3 CalculateDashDirection(Vector2 inputDirection)
    {
        // Si pas d'input, utiliser la direction actuelle du personnage
        if (inputDirection.sqrMagnitude < 0.01f)
        {
            return transform.forward;
        }

        // Calculer la direction relative à la caméra
        Vector3 forward = _cameraTransform != null
            ? _cameraTransform.forward
            : transform.forward;
        Vector3 right = _cameraTransform != null
            ? _cameraTransform.right
            : transform.right;

        // Projeter sur le plan horizontal
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        // Combiner et normaliser
        Vector3 direction = (forward * inputDirection.y + right * inputDirection.x).normalized;
        return direction;
    }

    private void UpdateCooldown()
    {
        if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.deltaTime;
        }
    }

    private void UpdateDash()
    {
        if (!_isDashing)
        {
            return;
        }

        _dashTimer -= Time.deltaTime;

        if (_dashTimer <= 0f)
        {
            EndDash();
            return;
        }

        // Calculer le mouvement du dash
        float dashSpeed = _dashDistance / _dashDuration;
        Vector3 movement = _dashDirection * dashSpeed * Time.deltaTime;

        // Appliquer le mouvement via Rigidbody
        _rb.MovePosition(_rb.position + movement);

        // Orienter le personnage dans la direction du dash
        if (_dashDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(_dashDirection);
            _rb.rotation = targetRotation;
        }
    }

    private void UpdateIFrames()
    {
        if (!_isInvincible)
        {
            return;
        }

        _iFrameTimer -= Time.deltaTime;

        if (_iFrameTimer <= 0f)
        {
            _isInvincible = false;
            OnInvincibilityEnded?.Invoke();
        }
    }

    private void EndDash()
    {
        _isDashing = false;
        _dashTimer = 0f;
        OnDashEnded?.Invoke();
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// True si le personnage est en train de dasher.
    /// </summary>
    public bool IsDashing => _isDashing;

    /// <summary>
    /// True si le personnage est actuellement invincible (i-frames).
    /// </summary>
    public bool IsInvincible => _isInvincible;

    /// <summary>
    /// True si le dash est disponible.
    /// </summary>
    public bool CanDash => !_isDashing && _cooldownTimer <= 0f;

    /// <summary>
    /// Temps restant avant que le dash soit à nouveau disponible.
    /// </summary>
    public float CooldownRemaining => Mathf.Max(0f, _cooldownTimer);

    /// <summary>
    /// Pourcentage du cooldown écoulé (0 = vient de dasher, 1 = prêt).
    /// </summary>
    public float CooldownProgress => _dashCooldown > 0f
        ? 1f - (CooldownRemaining / _dashCooldown)
        : 1f;

    /// <summary>
    /// Coût en stamina du dash.
    /// </summary>
    public float StaminaCost => _staminaCost;

    /// <summary>
    /// Direction actuelle du dash (normalisée).
    /// </summary>
    public Vector3 CurrentDashDirection => _dashDirection;

    /// <summary>
    /// Distance totale du dash.
    /// </summary>
    public float DashDistance => _dashDistance;

    /// <summary>
    /// Durée du dash en secondes.
    /// </summary>
    public float DashDuration => _dashDuration;

    /// <summary>
    /// Durée d'invincibilité en secondes.
    /// </summary>
    public float IFrameDuration => _iFrameDuration;

    #endregion
}
