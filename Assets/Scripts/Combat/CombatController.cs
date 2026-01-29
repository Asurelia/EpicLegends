using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controleur principal du combat pour une entite.
/// Gere les etats, combos, attaques et defenses.
/// </summary>
public class CombatController : MonoBehaviour
{
    #region Serialized Fields

    [Header("References")]
    [SerializeField] private Animator _animator;
    [SerializeField] private Hitbox _hitbox;
    [SerializeField] private Hurtbox _hurtbox;
    [SerializeField] private StaggerHandler _staggerHandler;
    [SerializeField] private KnockbackReceiver _knockbackReceiver;

    [Header("Combos")]
    [SerializeField] private List<ComboData> _combos = new List<ComboData>();

    [Header("Timing")]
    [SerializeField] private float _parryWindowDuration = 0.2f;
    [SerializeField] private float _dodgeIFrameDuration = 0.3f;
    [SerializeField] private float _blockStaminaCost = 5f;

    #endregion

    #region Private Fields

    private CombatState _currentState = CombatState.Idle;
    private ComboData _currentCombo;
    private int _comboIndex;
    private float _attackTimer;
    private float _comboWindowTimer;
    private float _chargeTimer;
    private bool _inputBuffered;
    private bool _heavyInputBuffered;

    #endregion

    #region Events

    /// <summary>
    /// Declenche quand l'etat de combat change.
    /// </summary>
    public event Action<CombatState> OnStateChanged;

    /// <summary>
    /// Declenche quand une attaque commence.
    /// </summary>
    public event Action<AttackData> OnAttackStarted;

    /// <summary>
    /// Declenche quand une attaque touche.
    /// </summary>
    public event Action<DamageInfo> OnAttackHit;

    /// <summary>
    /// Declenche quand le combo se termine.
    /// </summary>
    public event Action<int> OnComboEnded;

    #endregion

    #region Properties

    /// <summary>
    /// Etat actuel du combat.
    /// </summary>
    public CombatState CurrentState => _currentState;

    /// <summary>
    /// Index actuel dans le combo.
    /// </summary>
    public int CurrentComboCount => _comboIndex;

    /// <summary>
    /// Est en train d'attaquer?
    /// </summary>
    public bool IsAttacking => _currentState == CombatState.Attacking || _currentState == CombatState.Charging;

    /// <summary>
    /// Est en defense?
    /// </summary>
    public bool IsDefending => _currentState == CombatState.Blocking || _currentState == CombatState.Parrying;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        // Auto-detection des composants
        if (_animator == null)
            _animator = GetComponent<Animator>();
        if (_hitbox == null)
            _hitbox = GetComponentInChildren<Hitbox>();
        if (_hurtbox == null)
            _hurtbox = GetComponentInChildren<Hurtbox>();
        if (_staggerHandler == null)
            _staggerHandler = GetComponent<StaggerHandler>();
        if (_knockbackReceiver == null)
            _knockbackReceiver = GetComponent<KnockbackReceiver>();

        // Configurer les events
        if (_hurtbox != null)
        {
            _hurtbox.OnDamageReceived += HandleDamageReceived;
        }
        if (_staggerHandler != null)
        {
            _staggerHandler.OnStaggered += HandleStaggered;
            _staggerHandler.OnStaggerRecovered += HandleStaggerRecovered;
        }
        if (_hitbox != null)
        {
            _hitbox.Owner = gameObject;
            _hitbox.OnHit += HandleHitLanded;
        }
    }

    private void Update()
    {
        UpdateTimers();
        UpdateState();
    }

    private void OnDestroy()
    {
        if (_hurtbox != null)
        {
            _hurtbox.OnDamageReceived -= HandleDamageReceived;
        }
        if (_staggerHandler != null)
        {
            _staggerHandler.OnStaggered -= HandleStaggered;
            _staggerHandler.OnStaggerRecovered -= HandleStaggerRecovered;
        }
        if (_hitbox != null)
        {
            _hitbox.OnHit -= HandleHitLanded;
        }
    }

    #endregion

    #region Public Methods - Queries

    /// <summary>
    /// Peut-on attaquer actuellement?
    /// </summary>
    public bool CanAttack()
    {
        return _currentState == CombatState.Idle ||
               (_currentState == CombatState.Attacking && _comboWindowTimer > 0);
    }

    /// <summary>
    /// Peut-on bloquer actuellement?
    /// </summary>
    public bool CanBlock()
    {
        return _currentState == CombatState.Idle;
    }

    /// <summary>
    /// Peut-on esquiver actuellement?
    /// </summary>
    public bool CanDodge()
    {
        return _currentState == CombatState.Idle ||
               _currentState == CombatState.Blocking;
    }

    /// <summary>
    /// Peut-on parer actuellement?
    /// </summary>
    public bool CanParry()
    {
        return _currentState == CombatState.Blocking;
    }

    #endregion

    #region Public Methods - Actions

    /// <summary>
    /// Demarre une attaque legere.
    /// </summary>
    public void LightAttack()
    {
        if (!CanAttack())
        {
            _inputBuffered = true;
            return;
        }

        StartAttack(false);
    }

    /// <summary>
    /// Demarre une attaque lourde.
    /// </summary>
    public void HeavyAttack()
    {
        if (!CanAttack())
        {
            _heavyInputBuffered = true;
            return;
        }

        StartAttack(true);
    }

    /// <summary>
    /// Commence a charger une attaque.
    /// </summary>
    public void StartCharging()
    {
        if (_currentState != CombatState.Idle) return;

        SetState(CombatState.Charging);
        _chargeTimer = 0f;
    }

    /// <summary>
    /// Relache l'attaque chargee.
    /// </summary>
    public void ReleaseCharge()
    {
        if (_currentState != CombatState.Charging) return;

        // TODO: Implementer l'attaque chargee
        SetState(CombatState.Idle);
    }

    /// <summary>
    /// Commence a bloquer.
    /// </summary>
    public void StartBlock()
    {
        if (!CanBlock()) return;

        SetState(CombatState.Blocking);
        if (_hurtbox != null)
        {
            _hurtbox.SetState(HurtboxState.Blocking);
        }
    }

    /// <summary>
    /// Arrete de bloquer.
    /// </summary>
    public void EndBlock()
    {
        if (_currentState != CombatState.Blocking) return;

        SetState(CombatState.Idle);
        if (_hurtbox != null)
        {
            _hurtbox.SetState(HurtboxState.Vulnerable);
        }
    }

    /// <summary>
    /// Tente une parade.
    /// </summary>
    public void TryParry()
    {
        if (_currentState != CombatState.Blocking) return;

        SetState(CombatState.Parrying);
        if (_hurtbox != null)
        {
            _hurtbox.SetState(HurtboxState.Parrying);
        }

        // La parade dure un temps limite
        Invoke(nameof(EndParry), _parryWindowDuration);
    }

    /// <summary>
    /// Effectue une esquive.
    /// </summary>
    public void Dodge(Vector3 direction)
    {
        if (!CanDodge()) return;

        SetState(CombatState.Dodging);
        if (_hurtbox != null)
        {
            _hurtbox.SetState(HurtboxState.Invincible);
        }

        // Les i-frames durent un temps limite
        Invoke(nameof(EndDodge), _dodgeIFrameDuration);
    }

    /// <summary>
    /// Reinitialise le combat.
    /// </summary>
    public void ResetCombat()
    {
        SetState(CombatState.Idle);
        _currentCombo = null;
        _comboIndex = 0;
        _attackTimer = 0f;
        _comboWindowTimer = 0f;
        _inputBuffered = false;
        _heavyInputBuffered = false;

        if (_hitbox != null)
            _hitbox.Deactivate();
        if (_hurtbox != null)
            _hurtbox.SetState(HurtboxState.Vulnerable);
    }

    #endregion

    #region Private Methods

    private void SetState(CombatState newState)
    {
        if (_currentState == newState) return;

        _currentState = newState;
        OnStateChanged?.Invoke(newState);
    }

    private void StartAttack(bool isHeavy)
    {
        // Determiner le combo et l'attaque
        if (_currentCombo == null || _comboIndex >= _currentCombo.Length)
        {
            // Nouveau combo
            _currentCombo = GetComboForInput(isHeavy);
            _comboIndex = 0;
        }
        else
        {
            // Continuer le combo
            _comboIndex++;
        }

        if (_currentCombo == null || _comboIndex >= _currentCombo.Length)
        {
            EndCombo();
            return;
        }

        var attack = _currentCombo.GetAttack(_comboIndex);
        if (attack == null)
        {
            EndCombo();
            return;
        }

        ExecuteAttack(attack);
    }

    private void ExecuteAttack(AttackData attack)
    {
        SetState(CombatState.Attacking);
        _attackTimer = attack.animationDuration;
        _comboWindowTimer = 0f;
        _inputBuffered = false;
        _heavyInputBuffered = false;

        // Activer la hitbox
        if (_hitbox != null)
        {
            var damageInfo = attack.CreateDamageInfo(gameObject);
            damageInfo.comboIndex = _comboIndex;
            _hitbox.Activate(damageInfo);
        }

        // Jouer l'animation
        if (_animator != null && !string.IsNullOrEmpty(attack.animationTrigger))
        {
            _animator.SetTrigger(attack.animationTrigger);
        }

        OnAttackStarted?.Invoke(attack);
    }

    private ComboData GetComboForInput(bool isHeavy)
    {
        // Pour l'instant, retourne le premier combo disponible
        // TODO: Ajouter la logique de selection de combo
        return _combos.Count > 0 ? _combos[0] : null;
    }

    private void EndCombo()
    {
        int hitCount = _comboIndex;
        _currentCombo = null;
        _comboIndex = 0;
        SetState(CombatState.Recovering);

        // Recuperation courte
        Invoke(nameof(EndRecovery), 0.2f);

        OnComboEnded?.Invoke(hitCount);
    }

    private void EndRecovery()
    {
        if (_currentState == CombatState.Recovering)
        {
            SetState(CombatState.Idle);
        }
    }

    private void EndParry()
    {
        if (_currentState == CombatState.Parrying)
        {
            SetState(CombatState.Blocking);
            if (_hurtbox != null)
            {
                _hurtbox.SetState(HurtboxState.Blocking);
            }
        }
    }

    private void EndDodge()
    {
        if (_currentState == CombatState.Dodging)
        {
            SetState(CombatState.Idle);
            if (_hurtbox != null)
            {
                _hurtbox.SetState(HurtboxState.Vulnerable);
            }
        }
    }

    private void UpdateTimers()
    {
        if (_attackTimer > 0f)
        {
            _attackTimer -= Time.deltaTime;

            // Ouvrir la fenetre de combo
            if (_currentCombo != null && _comboIndex < _currentCombo.Length)
            {
                var attack = _currentCombo.GetAttack(_comboIndex);
                if (attack != null)
                {
                    float normalizedTime = 1f - (_attackTimer / attack.animationDuration);
                    if (attack.IsInComboWindow(normalizedTime))
                    {
                        _comboWindowTimer = attack.comboWindowEnd - normalizedTime;
                    }
                }
            }

            if (_attackTimer <= 0f)
            {
                if (_hitbox != null)
                    _hitbox.Deactivate();

                if (_comboWindowTimer > 0f)
                {
                    // Fenetre de combo ouverte
                }
                else
                {
                    EndCombo();
                }
            }
        }

        if (_comboWindowTimer > 0f)
        {
            _comboWindowTimer -= Time.deltaTime;
            if (_comboWindowTimer <= 0f && _currentState == CombatState.Attacking)
            {
                EndCombo();
            }
        }

        if (_currentState == CombatState.Charging)
        {
            _chargeTimer += Time.deltaTime;
        }
    }

    private void UpdateState()
    {
        // Traiter les inputs bufferises
        if (_currentState == CombatState.Idle || _comboWindowTimer > 0f)
        {
            if (_inputBuffered)
            {
                _inputBuffered = false;
                LightAttack();
            }
            else if (_heavyInputBuffered)
            {
                _heavyInputBuffered = false;
                HeavyAttack();
            }
        }
    }

    private void HandleDamageReceived(DamageInfo damageInfo)
    {
        // Appliquer le stagger
        if (_staggerHandler != null)
        {
            _staggerHandler.ApplyStagger(damageInfo.staggerValue);
        }

        // Appliquer le knockback
        if (_knockbackReceiver != null)
        {
            _knockbackReceiver.ApplyKnockback(damageInfo);
        }
    }

    private void HandleStaggered()
    {
        SetState(CombatState.Staggered);
        if (_hitbox != null)
            _hitbox.Deactivate();
    }

    private void HandleStaggerRecovered()
    {
        if (_currentState == CombatState.Staggered)
        {
            SetState(CombatState.Idle);
        }
    }

    private void HandleHitLanded(GameObject target, DamageInfo damageInfo)
    {
        OnAttackHit?.Invoke(damageInfo);
    }

    #endregion
}
