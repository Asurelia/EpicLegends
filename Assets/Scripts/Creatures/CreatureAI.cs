using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Intelligence artificielle pour les creatures du joueur.
/// Gere les comportements de suivi, combat assisté et commandes.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CreatureAI : MonoBehaviour
{
    #region Fields

    [Header("References")]
    [SerializeField] private CreatureController _creatureController;
    [SerializeField] private NavMeshAgent _navAgent;
    [SerializeField] private Animator _animator;

    [Header("Follow Settings")]
    [SerializeField] private float _followDistance = 3f;
    [SerializeField] private float _followSpeed = 5f;
    [SerializeField] private float _teleportDistance = 20f;

    [Header("Combat Settings")]
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private float _detectionRange = 10f;
    [SerializeField] private float _attackCooldown = 1.5f;
    [SerializeField] private LayerMask _enemyLayer;

    [Header("Behavior")]
    [SerializeField] private CreatureBehavior _behavior = CreatureBehavior.Defensive;

    // Etat
    private CreatureAIState _currentState = CreatureAIState.Idle;
    private Transform _owner;
    private Transform _currentTarget;
    private float _lastAttackTime;
    private CreatureCommand _pendingCommand;
    private int _pendingAbilityIndex = -1;

    // Cache
    private Rigidbody _rb;

    #endregion

    #region Events

    public event System.Action<CreatureAIState> OnStateChanged;
    public event System.Action<Transform> OnTargetChanged;
    public event System.Action<CreatureCommand> OnCommandReceived;

    #endregion

    #region Properties

    public CreatureAIState CurrentState => _currentState;
    public Transform Owner => _owner;
    public Transform CurrentTarget => _currentTarget;
    public CreatureBehavior Behavior => _behavior;
    public float FollowDistance => _followDistance;
    public float AttackRange => _attackRange;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (_creatureController == null)
            _creatureController = GetComponent<CreatureController>();

        if (_navAgent == null)
            _navAgent = GetComponent<NavMeshAgent>();

        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (_creatureController == null) return;
        if (_creatureController.IsFainted)
        {
            SetState(CreatureAIState.Idle);
            return;
        }

        UpdateState();
        UpdateAnimations();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Definit le proprietaire de la creature.
    /// </summary>
    public void SetOwner(Transform owner)
    {
        _owner = owner;
        if (owner != null)
        {
            SetState(CreatureAIState.Following);
        }
    }

    /// <summary>
    /// Traite une commande du joueur.
    /// </summary>
    public void ProcessCommand(CreatureCommand command)
    {
        _pendingCommand = command;
        OnCommandReceived?.Invoke(command);

        switch (command)
        {
            case CreatureCommand.Follow:
                _currentTarget = null;
                SetState(CreatureAIState.Following);
                break;

            case CreatureCommand.Stay:
                _currentTarget = null;
                SetState(CreatureAIState.Idle);
                break;

            case CreatureCommand.Attack:
                // Chercher la cible la plus proche
                var target = FindNearestEnemy();
                if (target != null)
                {
                    SetTarget(target);
                    SetState(CreatureAIState.Attacking);
                }
                break;

            case CreatureCommand.Return:
                _currentTarget = null;
                SetState(CreatureAIState.Returning);
                break;

            case CreatureCommand.Defend:
                SetState(CreatureAIState.Defending);
                break;

            case CreatureCommand.UseAbility:
                if (_pendingAbilityIndex >= 0)
                {
                    UseAbility(_pendingAbilityIndex);
                }
                break;
        }
    }

    /// <summary>
    /// Ordonne d'attaquer une cible specifique.
    /// </summary>
    public void CommandAttackTarget(Transform target)
    {
        if (target == null) return;
        SetTarget(target);
        SetState(CreatureAIState.Attacking);
    }

    /// <summary>
    /// Ordonne d'utiliser une ability.
    /// </summary>
    public void CommandUseAbility(int abilityIndex, Transform target = null)
    {
        _pendingAbilityIndex = abilityIndex;
        if (target != null)
        {
            SetTarget(target);
        }
        ProcessCommand(CreatureCommand.UseAbility);
    }

    /// <summary>
    /// Change le comportement de l'IA.
    /// </summary>
    public void SetBehavior(CreatureBehavior behavior)
    {
        _behavior = behavior;
    }

    /// <summary>
    /// Force le retour au proprietaire.
    /// </summary>
    public void ForceReturn()
    {
        ProcessCommand(CreatureCommand.Return);
    }

    #endregion

    #region Private Methods - State Machine

    private void SetState(CreatureAIState newState)
    {
        if (_currentState == newState) return;

        _currentState = newState;
        OnStateChanged?.Invoke(newState);

        // Actions d'entree dans l'etat
        switch (newState)
        {
            case CreatureAIState.Following:
                if (_navAgent != null) _navAgent.isStopped = false;
                break;
            case CreatureAIState.Idle:
                if (_navAgent != null) _navAgent.isStopped = true;
                break;
        }
    }

    private void SetTarget(Transform target)
    {
        _currentTarget = target;
        OnTargetChanged?.Invoke(target);
    }

    private void UpdateState()
    {
        switch (_currentState)
        {
            case CreatureAIState.Idle:
                UpdateIdle();
                break;
            case CreatureAIState.Following:
                UpdateFollowing();
                break;
            case CreatureAIState.Attacking:
                UpdateAttacking();
                break;
            case CreatureAIState.Defending:
                UpdateDefending();
                break;
            case CreatureAIState.Returning:
                UpdateReturning();
                break;
            case CreatureAIState.UsingAbility:
                UpdateUsingAbility();
                break;
        }

        // Comportement automatique base sur le mode
        if (_behavior == CreatureBehavior.Aggressive && _currentState == CreatureAIState.Following)
        {
            CheckForEnemies();
        }
    }

    private void UpdateIdle()
    {
        // Attendre les commandes
        if (_navAgent != null && _navAgent.enabled)
        {
            _navAgent.isStopped = true;
        }
    }

    private void UpdateFollowing()
    {
        if (_owner == null)
        {
            SetState(CreatureAIState.Idle);
            return;
        }

        float distance = Vector3.Distance(transform.position, _owner.position);

        // Teleporter si trop loin
        if (distance > _teleportDistance)
        {
            TeleportToOwner();
            return;
        }

        // Suivre si pas assez proche
        if (_navAgent != null && _navAgent.enabled)
        {
            if (distance > _followDistance)
            {
                _navAgent.isStopped = false;
                _navAgent.speed = _followSpeed;
                _navAgent.SetDestination(_owner.position);
            }
            else
            {
                _navAgent.isStopped = true;
            }
        }

        // Verifier les menaces en mode defensif
        if (_behavior == CreatureBehavior.Defensive)
        {
            CheckForThreatsToOwner();
        }
    }

    private void UpdateAttacking()
    {
        if (_currentTarget == null)
        {
            SetState(CreatureAIState.Following);
            return;
        }

        // Verifier si la cible est morte
        var targetHealth = _currentTarget.GetComponent<Health>();
        if (targetHealth != null && targetHealth.IsDead)
        {
            _currentTarget = null;
            SetState(CreatureAIState.Following);
            return;
        }

        float distance = Vector3.Distance(transform.position, _currentTarget.position);

        if (_navAgent != null && _navAgent.enabled)
        {
            if (distance > _attackRange)
            {
                // Se rapprocher
                _navAgent.isStopped = false;
                _navAgent.SetDestination(_currentTarget.position);
            }
            else
            {
                // A portee, attaquer
                _navAgent.isStopped = true;
                TryAttack();
            }
        }
    }

    private void UpdateDefending()
    {
        if (_owner == null)
        {
            SetState(CreatureAIState.Idle);
            return;
        }

        // Rester proche du proprietaire
        float distanceToOwner = Vector3.Distance(transform.position, _owner.position);

        if (_navAgent != null && _navAgent.enabled)
        {
            if (distanceToOwner > _followDistance * 1.5f)
            {
                _navAgent.isStopped = false;
                _navAgent.SetDestination(_owner.position);
            }
            else
            {
                _navAgent.isStopped = true;
            }
        }

        // Attaquer les ennemis qui menacent le proprietaire
        CheckForThreatsToOwner();
    }

    private void UpdateReturning()
    {
        if (_owner == null)
        {
            SetState(CreatureAIState.Idle);
            return;
        }

        float distance = Vector3.Distance(transform.position, _owner.position);

        if (distance <= _followDistance)
        {
            SetState(CreatureAIState.Following);
            return;
        }

        if (_navAgent != null && _navAgent.enabled)
        {
            _navAgent.isStopped = false;
            _navAgent.speed = _followSpeed * 1.5f; // Plus rapide en retour
            _navAgent.SetDestination(_owner.position);
        }
    }

    private void UpdateUsingAbility()
    {
        // L'ability est en cours, attendre la fin
        // Transition automatique apres l'animation
        SetState(CreatureAIState.Following);
    }

    #endregion

    #region Private Methods - Combat

    private void TryAttack()
    {
        if (Time.time - _lastAttackTime < _attackCooldown) return;
        if (_currentTarget == null) return;

        // Attaquer
        _lastAttackTime = Time.time;

        // Regarder la cible
        Vector3 lookDir = _currentTarget.position - transform.position;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDir);
        }

        // Animation
        if (_animator != null)
        {
            _animator.SetTrigger("Attack");
        }

        // Appliquer les degats via le controleur
        if (_creatureController != null)
        {
            var targetDamageable = _currentTarget.GetComponent<IDamageable>();
            if (targetDamageable != null)
            {
                var damageInfo = new DamageInfo
                {
                    baseDamage = _creatureController.Instance.Attack,
                    damageType = DamageType.Physical,
                    attacker = gameObject,
                    hitPoint = _currentTarget.position
                };
                targetDamageable.TakeDamage(damageInfo);
            }
        }
    }

    private void UseAbility(int abilityIndex)
    {
        if (_creatureController == null) return;

        SetState(CreatureAIState.UsingAbility);

        // Animation
        if (_animator != null)
        {
            _animator.SetTrigger("Ability");
        }

        // Utiliser l'ability
        _creatureController.UseAbility(abilityIndex, _currentTarget);

        _pendingAbilityIndex = -1;
    }

    private Transform FindNearestEnemy()
    {
        var colliders = Physics.OverlapSphere(transform.position, _detectionRange, _enemyLayer);

        Transform nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (var col in colliders)
        {
            var health = col.GetComponent<Health>();
            if (health == null || health.IsDead) continue;

            float distance = Vector3.Distance(transform.position, col.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = col.transform;
            }
        }

        return nearest;
    }

    private void CheckForEnemies()
    {
        var enemy = FindNearestEnemy();
        if (enemy != null)
        {
            SetTarget(enemy);
            SetState(CreatureAIState.Attacking);
        }
    }

    private void CheckForThreatsToOwner()
    {
        if (_owner == null) return;

        var colliders = Physics.OverlapSphere(_owner.position, _detectionRange, _enemyLayer);

        foreach (var col in colliders)
        {
            var health = col.GetComponent<Health>();
            if (health == null || health.IsDead) continue;

            // Si un ennemi est proche du proprietaire, l'attaquer
            float distanceToOwner = Vector3.Distance(col.transform.position, _owner.position);
            if (distanceToOwner < _detectionRange * 0.5f)
            {
                SetTarget(col.transform);
                SetState(CreatureAIState.Attacking);
                return;
            }
        }
    }

    #endregion

    #region Private Methods - Movement

    private void TeleportToOwner()
    {
        if (_owner == null) return;

        // Position derriere le proprietaire
        Vector3 offset = -_owner.forward * _followDistance;
        transform.position = _owner.position + offset;

        if (_navAgent != null && _navAgent.enabled)
        {
            _navAgent.Warp(transform.position);
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

        // Etat de combat
        _animator.SetBool("InCombat", _currentState == CreatureAIState.Attacking);
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        // Portee de detection
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);

        // Portee d'attaque
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);

        // Distance de suivi
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _followDistance);
    }

    #endregion
}

/// <summary>
/// Etats de l'IA de creature.
/// </summary>
public enum CreatureAIState
{
    Idle,
    Following,
    Attacking,
    Defending,
    Returning,
    UsingAbility
}

/// <summary>
/// Comportements de l'IA.
/// </summary>
public enum CreatureBehavior
{
    /// <summary>N'attaque jamais automatiquement</summary>
    Passive,

    /// <summary>Attaque seulement si le proprietaire est menace</summary>
    Defensive,

    /// <summary>Attaque tout ennemi a portee</summary>
    Aggressive,

    /// <summary>Priorise le soutien du proprietaire</summary>
    Support
}

/// <summary>
/// Commandes que le joueur peut donner.
/// </summary>
public enum CreatureCommand
{
    /// <summary>Suivre le proprietaire</summary>
    Follow,

    /// <summary>Rester sur place</summary>
    Stay,

    /// <summary>Attaquer la cible</summary>
    Attack,

    /// <summary>Retourner au proprietaire</summary>
    Return,

    /// <summary>Defendre le proprietaire</summary>
    Defend,

    /// <summary>Utiliser une ability</summary>
    UseAbility
}
