using System;
using UnityEngine;

/// <summary>
/// Composant pour recevoir et appliquer les effets de recul.
/// </summary>
public class KnockbackReceiver : MonoBehaviour
{
    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private float _knockbackResistance = 0f;
    [SerializeField] private bool _isImmune = false;

    [Header("Physics")]
    [SerializeField] private float _knockbackDuration = 0.3f;
    [SerializeField] private AnimationCurve _knockbackCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    #endregion

    #region Private Fields

    private Rigidbody _rb;
    private CharacterController _characterController;
    private Vector3 _pendingKnockback;
    private float _pendingForce;
    private bool _hasPendingKnockback;
    private float _knockbackTimer;
    private Vector3 _knockbackVelocity;

    #endregion

    #region Events

    /// <summary>
    /// Declenche quand un knockback est applique.
    /// </summary>
    public event Action<Vector3, float> OnKnockbackApplied;

    /// <summary>
    /// Declenche quand le knockback se termine.
    /// </summary>
    public event Action OnKnockbackEnded;

    #endregion

    #region Properties

    /// <summary>
    /// Resistance au knockback (0-1). 1 = immunite totale.
    /// </summary>
    public float KnockbackResistance
    {
        get => _knockbackResistance;
        set => _knockbackResistance = Mathf.Clamp01(value);
    }

    /// <summary>
    /// Immunite complete au knockback.
    /// </summary>
    public bool IsImmune
    {
        get => _isImmune;
        set => _isImmune = value;
    }

    /// <summary>
    /// Y a-t-il un knockback en attente?
    /// </summary>
    public bool HasPendingKnockback => _hasPendingKnockback;

    /// <summary>
    /// Force du knockback en attente.
    /// </summary>
    public float PendingKnockbackForce => _pendingForce;

    /// <summary>
    /// Est-ce que le knockback est en cours?
    /// </summary>
    public bool IsKnockbackActive => _knockbackTimer > 0f;

    /// <summary>
    /// Velocite actuelle du knockback.
    /// </summary>
    public Vector3 KnockbackVelocity => _knockbackVelocity;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _characterController = GetComponent<CharacterController>();
    }

    private void FixedUpdate()
    {
        if (_hasPendingKnockback)
        {
            ExecuteKnockback();
        }

        if (_knockbackTimer > 0f)
        {
            UpdateKnockback();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Applique un knockback.
    /// </summary>
    public void ApplyKnockback(Vector3 direction, float force)
    {
        if (_isImmune) return;

        // Appliquer la resistance
        float effectiveForce = force * (1f - _knockbackResistance);
        if (effectiveForce <= 0f) return;

        _pendingKnockback = direction.normalized;
        _pendingForce = effectiveForce;
        _hasPendingKnockback = true;
    }

    /// <summary>
    /// Applique un knockback depuis une DamageInfo.
    /// </summary>
    public void ApplyKnockback(DamageInfo damageInfo)
    {
        if (damageInfo.knockbackForce > 0f)
        {
            Vector3 direction = damageInfo.knockbackDirection;
            if (direction == Vector3.zero && damageInfo.attacker != null)
            {
                direction = (transform.position - damageInfo.attacker.transform.position).normalized;
            }
            ApplyKnockback(direction, damageInfo.knockbackForce);
        }
    }

    /// <summary>
    /// Annule le knockback en cours.
    /// </summary>
    public void CancelKnockback()
    {
        _hasPendingKnockback = false;
        _knockbackTimer = 0f;
        _knockbackVelocity = Vector3.zero;
        OnKnockbackEnded?.Invoke();
    }

    #endregion

    #region Private Methods

    private void ExecuteKnockback()
    {
        _hasPendingKnockback = false;
        _knockbackTimer = _knockbackDuration;
        _knockbackVelocity = _pendingKnockback * _pendingForce;

        OnKnockbackApplied?.Invoke(_pendingKnockback, _pendingForce);

        // Appliquer immediatement si on a un Rigidbody
        if (_rb != null)
        {
            _rb.AddForce(_knockbackVelocity, ForceMode.VelocityChange);
        }
    }

    private void UpdateKnockback()
    {
        _knockbackTimer -= Time.fixedDeltaTime;

        if (_knockbackTimer <= 0f)
        {
            _knockbackVelocity = Vector3.zero;
            OnKnockbackEnded?.Invoke();
            return;
        }

        // Appliquer le mouvement progressivement pour CharacterController
        if (_characterController != null)
        {
            float t = 1f - (_knockbackTimer / _knockbackDuration);
            float curveValue = _knockbackCurve.Evaluate(t);
            Vector3 movement = _knockbackVelocity * curveValue * Time.fixedDeltaTime;
            _characterController.Move(movement);
        }
    }

    #endregion
}
