using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Basic enemy AI with patrol, chase, and attack behaviors.
/// Uses Unity NavMesh for navigation.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class EnemyAI : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float _detectionRange = 10f;
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private float _fieldOfView = 120f;
    [SerializeField] private LayerMask _playerLayer;
    [SerializeField] private LayerMask _obstacleLayer;

    [Header("Combat")]
    [SerializeField] private float _attackDamage = 10f;
    [SerializeField] private float _attackCooldown = 1.5f;
    [SerializeField] private DamageType _damageType = DamageType.Physical;

    [Header("Patrol")]
    [SerializeField] private Transform[] _patrolPoints;
    [SerializeField] private float _patrolWaitTime = 2f;

    [Header("Movement")]
    [SerializeField] private float _patrolSpeed = 2f;
    [SerializeField] private float _chaseSpeed = 4f;

    // State
    private enum AIState { Idle, Patrol, Chase, Attack }
    private AIState _currentState = AIState.Idle;

    // Cached components
    private NavMeshAgent _agent;
    private Health _health;

    // Target
    private Transform _player;
    private int _currentPatrolIndex;
    private float _waitTimer;
    private float _attackTimer;

    #region Unity Callbacks

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _health = GetComponent<Health>();
    }

    private void Start()
    {
        // Find player
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _player = playerObj.transform;
        }

        // Subscribe to death event
        _health.OnDeath += HandleDeath;

        // Start patrolling if patrol points exist
        if (_patrolPoints != null && _patrolPoints.Length > 0)
        {
            _currentState = AIState.Patrol;
            SetDestination(_patrolPoints[0].position);
        }
    }

    private void Update()
    {
        if (_health.IsDead) return;

        UpdateTimers();
        UpdateState();
        ExecuteState();
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.OnDeath -= HandleDeath;
        }
    }

    #endregion

    #region State Machine

    private void UpdateState()
    {
        if (_player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        bool canSeePlayer = CanSeePlayer();

        // State transitions
        switch (_currentState)
        {
            case AIState.Idle:
            case AIState.Patrol:
                if (canSeePlayer && distanceToPlayer <= _detectionRange)
                {
                    _currentState = AIState.Chase;
                    _agent.speed = _chaseSpeed;
                }
                break;

            case AIState.Chase:
                if (!canSeePlayer || distanceToPlayer > _detectionRange * 1.5f)
                {
                    // Lost sight of player, return to patrol
                    _currentState = _patrolPoints.Length > 0 ? AIState.Patrol : AIState.Idle;
                    _agent.speed = _patrolSpeed;
                }
                else if (distanceToPlayer <= _attackRange)
                {
                    _currentState = AIState.Attack;
                }
                break;

            case AIState.Attack:
                if (distanceToPlayer > _attackRange * 1.2f)
                {
                    _currentState = AIState.Chase;
                }
                break;
        }
    }

    private void ExecuteState()
    {
        switch (_currentState)
        {
            case AIState.Idle:
                _agent.isStopped = true;
                break;

            case AIState.Patrol:
                ExecutePatrol();
                break;

            case AIState.Chase:
                ExecuteChase();
                break;

            case AIState.Attack:
                ExecuteAttack();
                break;
        }
    }

    #endregion

    #region Behaviors

    private void ExecutePatrol()
    {
        if (_patrolPoints == null || _patrolPoints.Length == 0) return;

        _agent.isStopped = false;
        _agent.speed = _patrolSpeed;

        // Check if reached patrol point
        if (!_agent.pathPending && _agent.remainingDistance < 0.5f)
        {
            _waitTimer -= Time.deltaTime;

            if (_waitTimer <= 0)
            {
                // Move to next patrol point
                _currentPatrolIndex = (_currentPatrolIndex + 1) % _patrolPoints.Length;
                SetDestination(_patrolPoints[_currentPatrolIndex].position);
                _waitTimer = _patrolWaitTime;
            }
        }
    }

    private void ExecuteChase()
    {
        if (_player == null) return;

        _agent.isStopped = false;
        _agent.speed = _chaseSpeed;
        SetDestination(_player.position);
    }

    private void ExecuteAttack()
    {
        _agent.isStopped = true;

        // Face the player
        if (_player != null)
        {
            Vector3 direction = (_player.position - transform.position).normalized;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction),
                    10f * Time.deltaTime
                );
            }
        }

        // Attack when cooldown is ready
        if (_attackTimer <= 0)
        {
            PerformAttack();
            _attackTimer = _attackCooldown;
        }
    }

    private void PerformAttack()
    {
        if (_player == null) return;

        // Check if player is still in range
        float distance = Vector3.Distance(transform.position, _player.position);
        if (distance > _attackRange) return;

        // Try to damage player
        if (_player.TryGetComponent<IDamageable>(out var damageable))
        {
            damageable.TakeDamage(_attackDamage, _damageType);
            Debug.Log($"{gameObject.name} attacked player for {_attackDamage} damage!");
        }
        else if (_player.TryGetComponent<PlayerStats>(out var stats))
        {
            stats.TakeDamage(_attackDamage);
            Debug.Log($"{gameObject.name} attacked player for {_attackDamage} damage!");
        }
    }

    #endregion

    #region Detection

    private bool CanSeePlayer()
    {
        if (_player == null) return false;

        Vector3 directionToPlayer = (_player.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);

        // Check if in detection range
        if (distanceToPlayer > _detectionRange) return false;

        // Check if in field of view
        float angle = Vector3.Angle(transform.forward, directionToPlayer);
        if (angle > _fieldOfView / 2f) return false;

        // Check for obstacles
        Vector3 rayStart = transform.position + Vector3.up;
        Vector3 rayEnd = _player.position + Vector3.up;
        if (Physics.Linecast(rayStart, rayEnd, _obstacleLayer))
        {
            return false;
        }

        return true;
    }

    #endregion

    #region Helpers

    private void SetDestination(Vector3 destination)
    {
        if (_agent.isOnNavMesh)
        {
            _agent.SetDestination(destination);
        }
    }

    private void UpdateTimers()
    {
        if (_attackTimer > 0)
        {
            _attackTimer -= Time.deltaTime;
        }
    }

    private void HandleDeath()
    {
        _agent.isStopped = true;
        enabled = false;
        // Add death effects, loot drops, etc. here
    }

    #endregion

    #region Debug

    private void OnDrawGizmosSelected()
    {
        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);

        // Attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);

        // Field of view
        Gizmos.color = Color.blue;
        Vector3 leftBound = Quaternion.Euler(0, -_fieldOfView / 2f, 0) * transform.forward * _detectionRange;
        Vector3 rightBound = Quaternion.Euler(0, _fieldOfView / 2f, 0) * transform.forward * _detectionRange;
        Gizmos.DrawLine(transform.position, transform.position + leftBound);
        Gizmos.DrawLine(transform.position, transform.position + rightBound);

        // Patrol points
        if (_patrolPoints != null)
        {
            Gizmos.color = Color.green;
            foreach (var point in _patrolPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawSphere(point.position, 0.3f);
                }
            }
        }
    }

    #endregion
}
