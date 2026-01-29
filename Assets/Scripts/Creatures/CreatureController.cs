using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controleur pour une creature dans le monde.
/// Gere l'affichage, les animations et le comportement de base.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CreatureController : MonoBehaviour
{
    #region Fields

    [Header("Instance")]
    [SerializeField] private CreatureInstance _instance;

    [Header("References")]
    [SerializeField] private Animator _animator;
    [SerializeField] private NavMeshAgent _navAgent;
    [SerializeField] private Collider _collider;

    [Header("Settings")]
    [SerializeField] private float _interactionRange = 2f;
    [SerializeField] private float _followDistance = 3f;

    // Etat
    private CreatureControllerState _state = CreatureControllerState.Idle;
    private Transform _followTarget;
    private Transform _attackTarget;
    private bool _isOwnedByPlayer;

    // Cache
    private Rigidbody _rb;
    private Health _health;

    #endregion

    #region Events

    public event System.Action<CreatureController> OnCreatureFainted;
    public event System.Action<CreatureController, CreatureControllerState> OnStateChanged;

    #endregion

    #region Properties

    public CreatureInstance Instance => _instance;
    public CreatureData Data => _instance?.Data;
    public CreatureControllerState State => _state;
    public bool IsOwnedByPlayer => _isOwnedByPlayer;
    public bool IsFainted => _instance?.IsFainted ?? true;
    public Transform FollowTarget => _followTarget;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _health = GetComponent<Health>();

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        if (_navAgent == null)
            _navAgent = GetComponent<NavMeshAgent>();

        if (_collider == null)
            _collider = GetComponent<Collider>();
    }

    private void Start()
    {
        // Synchroniser la vie avec l'instance
        if (_instance != null && _health != null)
        {
            _health.SetMaxHealth(_instance.MaxHealth);
            _health.SetCurrentHealth(_instance.CurrentHealth);
        }
    }

    private void Update()
    {
        if (_instance == null || IsFainted) return;

        UpdateState();
        UpdateAnimations();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Initialise le controleur avec une instance.
    /// </summary>
    public void Initialize(CreatureInstance instance, bool ownedByPlayer = false)
    {
        _instance = instance;
        _isOwnedByPlayer = ownedByPlayer;

        // Mettre a jour le visuel
        if (instance.Data.prefab != null)
        {
            // Le prefab devrait etre instancie par le spawner
        }

        // Ecouter les evenements
        _instance.OnHealthChanged += OnInstanceHealthChanged;
        _instance.OnLevelUp += OnInstanceLevelUp;

        SetState(CreatureControllerState.Idle);
    }

    /// <summary>
    /// Definit la cible a suivre.
    /// </summary>
    public void SetFollowTarget(Transform target)
    {
        _followTarget = target;
        if (target != null)
        {
            SetState(CreatureControllerState.Following);
        }
    }

    /// <summary>
    /// Definit la cible a attaquer.
    /// </summary>
    public void SetAttackTarget(Transform target)
    {
        _attackTarget = target;
        if (target != null)
        {
            SetState(CreatureControllerState.Attacking);
        }
    }

    /// <summary>
    /// Commande d'attaque.
    /// </summary>
    public void CommandAttack(Transform target)
    {
        if (IsFainted) return;
        SetAttackTarget(target);
    }

    /// <summary>
    /// Commande de retour.
    /// </summary>
    public void CommandReturn()
    {
        if (IsFainted) return;
        _attackTarget = null;
        SetState(CreatureControllerState.Following);
    }

    /// <summary>
    /// Commande d'attente.
    /// </summary>
    public void CommandWait()
    {
        if (IsFainted) return;
        _attackTarget = null;
        SetState(CreatureControllerState.Idle);
    }

    /// <summary>
    /// Applique des degats a la creature.
    /// </summary>
    public void TakeDamage(DamageInfo damageInfo)
    {
        if (_instance == null || IsFainted) return;

        float baseDamage = damageInfo.GetEffectiveDamage();
        float effectiveDamage = DamageCalculator.CalculateFinalDamage(damageInfo, _instance.Defense);
        _instance.TakeDamage(effectiveDamage);

        if (_instance.IsFainted)
        {
            SetState(CreatureControllerState.Fainted);
            OnCreatureFainted?.Invoke(this);
        }
    }

    /// <summary>
    /// Soigne la creature.
    /// </summary>
    public void Heal(float amount)
    {
        if (_instance == null) return;
        _instance.Heal(amount);
    }

    /// <summary>
    /// Soigne completement.
    /// </summary>
    public void FullHeal()
    {
        if (_instance == null) return;
        _instance.FullHeal();

        if (_state == CreatureControllerState.Fainted)
        {
            SetState(CreatureControllerState.Idle);
        }
    }

    /// <summary>
    /// Ajoute de l'experience.
    /// </summary>
    public void AddExperience(int amount)
    {
        _instance?.AddExperience(amount);
    }

    /// <summary>
    /// Utilise une ability.
    /// </summary>
    public bool UseAbility(int abilityIndex, Transform target)
    {
        if (_instance == null || IsFainted) return false;

        var abilities = _instance.GetAvailableAbilities();
        if (abilityIndex < 0 || abilityIndex >= abilities.Length) return false;

        var ability = abilities[abilityIndex];
        if (!_instance.UseMana(ability.manaCost)) return false;

        // Executer l'ability
        ExecuteAbility(ability, target);
        return true;
    }

    #endregion

    #region Private Methods

    private void SetState(CreatureControllerState newState)
    {
        if (_state == newState) return;

        var oldState = _state;
        _state = newState;
        OnStateChanged?.Invoke(this, newState);
    }

    private void UpdateState()
    {
        switch (_state)
        {
            case CreatureControllerState.Idle:
                UpdateIdle();
                break;
            case CreatureControllerState.Following:
                UpdateFollowing();
                break;
            case CreatureControllerState.Attacking:
                UpdateAttacking();
                break;
            case CreatureControllerState.Fainted:
                // Rien a faire
                break;
        }
    }

    private void UpdateIdle()
    {
        // Comportement idle
        if (_navAgent != null && _navAgent.enabled)
        {
            _navAgent.isStopped = true;
        }
    }

    private void UpdateFollowing()
    {
        if (_followTarget == null)
        {
            SetState(CreatureControllerState.Idle);
            return;
        }

        float distance = Vector3.Distance(transform.position, _followTarget.position);

        if (_navAgent != null && _navAgent.enabled)
        {
            if (distance > _followDistance)
            {
                _navAgent.isStopped = false;
                _navAgent.SetDestination(_followTarget.position);
            }
            else
            {
                _navAgent.isStopped = true;
            }
        }
    }

    private void UpdateAttacking()
    {
        if (_attackTarget == null)
        {
            SetState(CreatureControllerState.Following);
            return;
        }

        // Verifier si la cible est toujours valide
        var targetHealth = _attackTarget.GetComponent<Health>();
        if (targetHealth != null && targetHealth.IsDead)
        {
            _attackTarget = null;
            SetState(CreatureControllerState.Following);
            return;
        }

        float distance = Vector3.Distance(transform.position, _attackTarget.position);

        if (_navAgent != null && _navAgent.enabled)
        {
            if (distance > _interactionRange)
            {
                _navAgent.isStopped = false;
                _navAgent.SetDestination(_attackTarget.position);
            }
            else
            {
                _navAgent.isStopped = true;
                // Attaquer
                PerformAttack();
            }
        }
    }

    private void PerformAttack()
    {
        // Animation d'attaque
        if (_animator != null)
        {
            _animator.SetTrigger("Attack");
        }

        // Degats de base
        if (_attackTarget != null)
        {
            var targetDamageable = _attackTarget.GetComponent<IDamageable>();
            if (targetDamageable != null)
            {
                var damageInfo = new DamageInfo
                {
                    baseDamage = _instance.Attack,
                    damageType = DamageType.Physical,
                    attacker = gameObject,
                    hitPoint = _attackTarget.position
                };
                targetDamageable.TakeDamage(damageInfo);
            }
        }
    }

    private void ExecuteAbility(SkillData ability, Transform target)
    {
        // Animation
        if (_animator != null)
        {
            _animator.SetTrigger("Skill");
        }

        // Effet de l'ability selon le type
        if (target != null && ability.targetType != SkillTargetType.Self)
        {
            var targetDamageable = target.GetComponent<IDamageable>();
            if (targetDamageable != null && ability.baseDamage > 0)
            {
                var damageInfo = new DamageInfo
                {
                    baseDamage = ability.baseDamage * (1f + _instance.Attack / 100f),
                    damageType = ability.damageType,
                    attacker = gameObject,
                    hitPoint = target.position
                };
                targetDamageable.TakeDamage(damageInfo);
            }
        }
    }

    private void UpdateAnimations()
    {
        if (_animator == null) return;

        // Vitesse de deplacement
        float speed = 0f;
        if (_navAgent != null && _navAgent.enabled)
        {
            speed = _navAgent.velocity.magnitude / _navAgent.speed;
        }
        _animator.SetFloat("Speed", speed);

        // Etat
        _animator.SetBool("IsFainted", IsFainted);
    }

    private void OnInstanceHealthChanged(float current, float max)
    {
        if (_health != null)
        {
            _health.SetMaxHealth(max);
            _health.SetCurrentHealth(current);
        }
    }

    private void OnInstanceLevelUp(int newLevel)
    {
        // Effet visuel de level up
        if (_animator != null)
        {
            _animator.SetTrigger("LevelUp");
        }
    }

    private void OnDestroy()
    {
        if (_instance != null)
        {
            _instance.OnHealthChanged -= OnInstanceHealthChanged;
            _instance.OnLevelUp -= OnInstanceLevelUp;
        }
    }

    #endregion
}

/// <summary>
/// Etats du controleur de creature.
/// </summary>
public enum CreatureControllerState
{
    Idle,
    Following,
    Attacking,
    Fainted
}
