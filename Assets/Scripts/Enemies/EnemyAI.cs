using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Advanced enemy AI with configurable behaviors.
/// Supports patrol, chase, attack patterns, and tactical behaviors.
/// Uses Unity NavMesh for navigation.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class EnemyAI : MonoBehaviour
{
    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private EnemyData _enemyData;

    [Header("Detection Overrides")]
    [SerializeField] private LayerMask _playerLayer = 1 << 6;
    [SerializeField] private LayerMask _obstacleLayer = 1 << 0;

    [Header("Patrol")]
    [SerializeField] private Transform[] _patrolPoints;
    [SerializeField] private float _patrolWaitTime = 2f;

    [Header("Combat")]
    [SerializeField] private Transform _attackPoint;
    [SerializeField] private float _attackRadius = 1f;

    [Header("Stagger")]
    [SerializeField] private float _staggerThreshold = 30f;
    [SerializeField] private float _staggerDuration = 1.5f;
    [SerializeField] private float _staggerRecoveryTime = 3f;

    [Header("Ranged Combat")]
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private Transform _projectileSpawnPoint;
    [SerializeField] private float _projectileSpeed = 15f;

    [Header("Flee Behavior")]
    [SerializeField] private float _lowHealthPercent = 0.25f;

    #endregion

    #region Private Fields

    // State
    private EnemyAIState _currentState = EnemyAIState.Idle;
    private EnemyAIState _previousState;
    private Vector3 _spawnPosition;
    private float _staggerAccumulator;
    private float _staggerRecoveryTimer;
    private float _staggerEndTime;

    // Cached components
    private NavMeshAgent _agent;
    private Health _health;
    private Animator _animator;
    private AggroSystem _aggro;

    // Target
    private Transform _currentTarget;
    private int _currentPatrolIndex;
    private float _waitTimer;
    private float _attackTimer;
    private float _lastAttackTime;

    // Attack patterns
    private AttackPattern _currentPattern;
    private int _currentAttackIndex;
    private Dictionary<AttackPattern, float> _patternCooldowns = new Dictionary<AttackPattern, float>();

    // Runtime values from EnemyData
    private float _detectionRange;
    private float _attackRange;
    private float _fieldOfView;
    private float _patrolSpeed;
    private float _chaseSpeed;
    private float _attackDamage;
    private float _attackCooldown;
    private DamageType _damageType;
    private AIBehavior _behavior;

    #endregion

    #region Properties

    public EnemyAIState CurrentState => _currentState;
    public EnemyData Data => _enemyData;
    public Transform CurrentTarget => _currentTarget;
    public bool IsStaggered => _currentState == EnemyAIState.Staggered;
    public bool IsDead => _currentState == EnemyAIState.Dead;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _health = GetComponent<Health>();
        _animator = GetComponent<Animator>();
        _aggro = GetComponent<AggroSystem>();
        _spawnPosition = transform.position;
    }

    private void Start()
    {
        InitializeFromData();
        SubscribeToEvents();
        StartPatrolOrIdle();
    }

    private void Update()
    {
        if (_currentState == EnemyAIState.Dead) return;

        UpdateTimers();
        UpdatePatternCooldowns();
        UpdateTarget();
        UpdateState();
        ExecuteState();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    #endregion

    #region Initialization

    private void InitializeFromData()
    {
        if (_enemyData != null)
        {
            _detectionRange = _enemyData.detectionRange;
            _attackRange = _enemyData.attackRange;
            _fieldOfView = _enemyData.fieldOfView;
            _patrolSpeed = _enemyData.moveSpeed;
            _chaseSpeed = _enemyData.chaseSpeed;
            _attackDamage = _enemyData.attackDamage;
            _attackCooldown = _enemyData.attackCooldown;
            _damageType = _enemyData.damageType;
            _behavior = _enemyData.baseBehavior;

            // Initialize health
            if (_health != null)
            {
                _health.SetMaxHealth(_enemyData.maxHealth);
            }

            // Initialize pattern cooldowns
            if (_enemyData.attackPatterns != null)
            {
                foreach (var pattern in _enemyData.attackPatterns)
                {
                    if (pattern != null)
                    {
                        _patternCooldowns[pattern] = 0f;
                    }
                }
            }
        }
        else
        {
            // Default values
            _detectionRange = 10f;
            _attackRange = 2f;
            _fieldOfView = 120f;
            _patrolSpeed = 2f;
            _chaseSpeed = 4f;
            _attackDamage = 10f;
            _attackCooldown = 1.5f;
            _damageType = DamageType.Physical;
            _behavior = AIBehavior.Aggressive;
        }
    }

    private void SubscribeToEvents()
    {
        if (_health != null)
        {
            _health.OnDeath += HandleDeath;
            _health.OnDamaged += HandleDamageInfo;
        }
        if (_aggro != null)
        {
            _aggro.OnTargetChanged += HandleAggroTargetChanged;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (_health != null)
        {
            _health.OnDeath -= HandleDeath;
            _health.OnDamaged -= HandleDamageInfo;
        }
        if (_aggro != null)
        {
            _aggro.OnTargetChanged -= HandleAggroTargetChanged;
        }
    }

    private void StartPatrolOrIdle()
    {
        if (_patrolPoints != null && _patrolPoints.Length > 0)
        {
            ChangeState(EnemyAIState.Patrol);
            SetDestination(_patrolPoints[0].position);
        }
        else
        {
            ChangeState(EnemyAIState.Idle);
        }
    }

    #endregion

    #region State Machine

    private void ChangeState(EnemyAIState newState)
    {
        if (_currentState == newState) return;

        _previousState = _currentState;
        _currentState = newState;

        // Animation triggers
        if (_animator != null)
        {
            _animator.SetInteger("State", (int)newState);
        }
    }

    private void UpdateTarget()
    {
        // Use aggro system if available
        if (_aggro != null && _aggro.HasTargets)
        {
            var aggroTarget = _aggro.CurrentTarget;
            if (aggroTarget != null)
            {
                _currentTarget = aggroTarget.transform;
                return;
            }
        }

        // Fallback: find player
        if (_currentTarget == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && CanSeeTarget(player.transform))
            {
                _currentTarget = player.transform;
            }
        }
    }

    private void UpdateState()
    {
        // Don't update state while staggered
        if (_currentState == EnemyAIState.Staggered)
        {
            if (Time.time >= _staggerEndTime)
            {
                ChangeState(_previousState != EnemyAIState.Staggered ? _previousState : EnemyAIState.Idle);
            }
            return;
        }

        // Behavior-specific state transitions
        switch (_behavior)
        {
            case AIBehavior.Aggressive:
                UpdateAggressiveState();
                break;
            case AIBehavior.Defensive:
                UpdateDefensiveState();
                break;
            case AIBehavior.Cowardly:
                UpdateCowardlyState();
                break;
            case AIBehavior.Ranged:
                UpdateRangedState();
                break;
            case AIBehavior.Support:
                UpdateSupportState();
                break;
            case AIBehavior.Scout:
                UpdateScoutState();
                break;
            default:
                UpdateAggressiveState();
                break;
        }
    }

    #endregion

    #region Behavior Updates

    private void UpdateAggressiveState()
    {
        float distanceToTarget = _currentTarget != null
            ? Vector3.Distance(transform.position, _currentTarget.position)
            : float.MaxValue;
        bool canSeeTarget = _currentTarget != null && CanSeeTarget(_currentTarget);

        switch (_currentState)
        {
            case EnemyAIState.Idle:
            case EnemyAIState.Patrol:
                if (canSeeTarget && distanceToTarget <= _detectionRange)
                {
                    ChangeState(EnemyAIState.Chase);
                    _agent.speed = _chaseSpeed;
                }
                break;

            case EnemyAIState.Chase:
                if (!canSeeTarget || distanceToTarget > _detectionRange * 1.5f)
                {
                    ChangeState(EnemyAIState.Return);
                }
                else if (distanceToTarget <= _attackRange)
                {
                    ChangeState(EnemyAIState.Combat);
                }
                break;

            case EnemyAIState.Combat:
                if (_currentTarget == null || distanceToTarget > _attackRange * 1.5f)
                {
                    ChangeState(EnemyAIState.Chase);
                }
                break;

            case EnemyAIState.Return:
                float distanceToSpawn = Vector3.Distance(transform.position, _spawnPosition);
                if (distanceToSpawn < 1f)
                {
                    _currentTarget = null;
                    ChangeState(_patrolPoints.Length > 0 ? EnemyAIState.Patrol : EnemyAIState.Idle);
                    _agent.speed = _patrolSpeed;
                }
                else if (canSeeTarget && distanceToTarget <= _detectionRange)
                {
                    ChangeState(EnemyAIState.Chase);
                    _agent.speed = _chaseSpeed;
                }
                break;
        }
    }

    private void UpdateDefensiveState()
    {
        float distanceToTarget = _currentTarget != null
            ? Vector3.Distance(transform.position, _currentTarget.position)
            : float.MaxValue;
        bool canSeeTarget = _currentTarget != null && CanSeeTarget(_currentTarget);
        float healthPercent = _health != null ? _health.CurrentHealth / _health.MaxHealth : 1f;

        switch (_currentState)
        {
            case EnemyAIState.Idle:
            case EnemyAIState.Patrol:
                if (canSeeTarget && distanceToTarget <= _detectionRange)
                {
                    // Defensive: wait for target to get close before chasing
                    if (distanceToTarget <= _attackRange * 2f)
                    {
                        ChangeState(EnemyAIState.Combat);
                    }
                }
                break;

            case EnemyAIState.Combat:
                // Flee if low health
                if (healthPercent < _lowHealthPercent)
                {
                    ChangeState(EnemyAIState.Flee);
                }
                else if (_currentTarget == null || distanceToTarget > _attackRange * 2f)
                {
                    ChangeState(EnemyAIState.Return);
                }
                break;

            case EnemyAIState.Flee:
                float fleeDistanceValue = _enemyData != null ? _enemyData.fleeDistance : 15f;
                if (distanceToTarget > fleeDistanceValue || healthPercent > _lowHealthPercent * 1.5f)
                {
                    ChangeState(EnemyAIState.Return);
                }
                break;

            case EnemyAIState.Return:
                float distanceToSpawn = Vector3.Distance(transform.position, _spawnPosition);
                if (distanceToSpawn < 1f)
                {
                    _currentTarget = null;
                    ChangeState(EnemyAIState.Idle);
                }
                break;
        }
    }

    private void UpdateCowardlyState()
    {
        float distanceToTarget = _currentTarget != null
            ? Vector3.Distance(transform.position, _currentTarget.position)
            : float.MaxValue;
        bool canSeeTarget = _currentTarget != null && CanSeeTarget(_currentTarget);

        switch (_currentState)
        {
            case EnemyAIState.Idle:
            case EnemyAIState.Patrol:
                if (canSeeTarget && distanceToTarget <= _detectionRange)
                {
                    ChangeState(EnemyAIState.Flee);
                    _agent.speed = _chaseSpeed;
                }
                break;

            case EnemyAIState.Flee:
                float cowardFleeDistance = _enemyData != null ? _enemyData.fleeDistance : 15f;
                if (!canSeeTarget || distanceToTarget > cowardFleeDistance)
                {
                    ChangeState(EnemyAIState.Return);
                    _agent.speed = _patrolSpeed;
                }
                break;

            case EnemyAIState.Return:
                float distanceToSpawn = Vector3.Distance(transform.position, _spawnPosition);
                if (distanceToSpawn < 1f)
                {
                    _currentTarget = null;
                    ChangeState(_patrolPoints.Length > 0 ? EnemyAIState.Patrol : EnemyAIState.Idle);
                }
                else if (canSeeTarget && distanceToTarget <= _detectionRange * 0.5f)
                {
                    ChangeState(EnemyAIState.Flee);
                    _agent.speed = _chaseSpeed;
                }
                break;
        }
    }

    private void UpdateRangedState()
    {
        float distanceToTarget = _currentTarget != null
            ? Vector3.Distance(transform.position, _currentTarget.position)
            : float.MaxValue;
        bool canSeeTarget = _currentTarget != null && CanSeeTarget(_currentTarget);
        float optimalRange = _attackRange * 0.7f;

        switch (_currentState)
        {
            case EnemyAIState.Idle:
            case EnemyAIState.Patrol:
                if (canSeeTarget && distanceToTarget <= _detectionRange)
                {
                    ChangeState(EnemyAIState.Chase);
                    _agent.speed = _chaseSpeed;
                }
                break;

            case EnemyAIState.Chase:
                if (!canSeeTarget || distanceToTarget > _detectionRange * 1.5f)
                {
                    ChangeState(EnemyAIState.Return);
                }
                else if (distanceToTarget <= _attackRange && distanceToTarget >= optimalRange)
                {
                    ChangeState(EnemyAIState.Combat);
                }
                break;

            case EnemyAIState.Combat:
                if (_currentTarget == null)
                {
                    ChangeState(EnemyAIState.Return);
                }
                else if (distanceToTarget < optimalRange)
                {
                    // Too close, back up
                    ChangeState(EnemyAIState.Flee);
                }
                else if (distanceToTarget > _attackRange)
                {
                    ChangeState(EnemyAIState.Chase);
                }
                break;

            case EnemyAIState.Flee:
                if (distanceToTarget >= optimalRange)
                {
                    ChangeState(EnemyAIState.Combat);
                }
                break;

            case EnemyAIState.Return:
                float distanceToSpawn = Vector3.Distance(transform.position, _spawnPosition);
                if (distanceToSpawn < 1f)
                {
                    _currentTarget = null;
                    ChangeState(_patrolPoints.Length > 0 ? EnemyAIState.Patrol : EnemyAIState.Idle);
                    _agent.speed = _patrolSpeed;
                }
                break;
        }
    }

    private void UpdateSupportState()
    {
        // Support enemies prioritize helping allies
        // Simplified version: acts like defensive
        UpdateDefensiveState();
    }

    private void UpdateScoutState()
    {
        // Scouts alert other enemies and flee
        // Simplified version: acts like cowardly but faster
        UpdateCowardlyState();
    }

    #endregion

    #region State Execution

    private void ExecuteState()
    {
        switch (_currentState)
        {
            case EnemyAIState.Idle:
                ExecuteIdle();
                break;
            case EnemyAIState.Patrol:
                ExecutePatrol();
                break;
            case EnemyAIState.Chase:
                ExecuteChase();
                break;
            case EnemyAIState.Combat:
                ExecuteCombat();
                break;
            case EnemyAIState.Flee:
                ExecuteFlee();
                break;
            case EnemyAIState.Return:
                ExecuteReturn();
                break;
            case EnemyAIState.Staggered:
                ExecuteStaggered();
                break;
        }
    }

    private void ExecuteIdle()
    {
        _agent.isStopped = true;
    }

    private void ExecutePatrol()
    {
        if (_patrolPoints == null || _patrolPoints.Length == 0) return;

        _agent.isStopped = false;
        _agent.speed = _patrolSpeed;

        if (!_agent.pathPending && _agent.remainingDistance < 0.5f)
        {
            _waitTimer -= Time.deltaTime;
            if (_waitTimer <= 0)
            {
                _currentPatrolIndex = (_currentPatrolIndex + 1) % _patrolPoints.Length;
                SetDestination(_patrolPoints[_currentPatrolIndex].position);
                _waitTimer = _patrolWaitTime;
            }
        }
    }

    private void ExecuteChase()
    {
        if (_currentTarget == null) return;

        _agent.isStopped = false;
        _agent.speed = _chaseSpeed;
        SetDestination(_currentTarget.position);
    }

    private void ExecuteCombat()
    {
        // Face the target
        FaceTarget();

        // For ranged: don't stop, maintain distance
        if (_behavior == AIBehavior.Ranged)
        {
            _agent.isStopped = false;
            // Circle strafe or maintain position
            if (_currentTarget != null)
            {
                float distance = Vector3.Distance(transform.position, _currentTarget.position);
                float optimalRange = _attackRange * 0.7f;
                if (Mathf.Abs(distance - optimalRange) > 1f)
                {
                    Vector3 direction = (transform.position - _currentTarget.position).normalized;
                    SetDestination(transform.position + direction * (optimalRange - distance));
                }
            }
        }
        else
        {
            _agent.isStopped = true;
        }

        // Attack when ready
        if (_attackTimer <= 0)
        {
            PerformAttack();
            _attackTimer = _attackCooldown;
        }
    }

    private void ExecuteFlee()
    {
        if (_currentTarget == null)
        {
            ChangeState(EnemyAIState.Return);
            return;
        }

        _agent.isStopped = false;
        _agent.speed = _chaseSpeed * 1.2f; // Run faster when fleeing

        // Run away from target
        Vector3 fleeDirection = (transform.position - _currentTarget.position).normalized;
        Vector3 fleeDestination = transform.position + fleeDirection * 10f;
        SetDestination(fleeDestination);
    }

    private void ExecuteReturn()
    {
        _agent.isStopped = false;
        _agent.speed = _patrolSpeed;
        SetDestination(_spawnPosition);
    }

    private void ExecuteStaggered()
    {
        _agent.isStopped = true;
        // Animation handles the stagger visual
    }

    #endregion

    #region Combat

    private void PerformAttack()
    {
        if (_currentTarget == null) return;

        float distance = Vector3.Distance(transform.position, _currentTarget.position);
        if (distance > _attackRange) return;

        // Try to use attack pattern
        var pattern = SelectAttackPattern();
        if (pattern != null && pattern.AttackCount > 0)
        {
            ExecutePatternAttack(pattern);
        }
        else
        {
            // Fallback to basic attack
            ExecuteBasicAttack();
        }

        // Play attack sound
        PlayAttackSound();
    }

    private AttackPattern SelectAttackPattern()
    {
        if (_enemyData?.attackPatterns == null) return null;

        float healthPercent = _health != null ? _health.CurrentHealth / _health.MaxHealth : 1f;
        float distanceToTarget = _currentTarget != null
            ? Vector3.Distance(transform.position, _currentTarget.position)
            : 0f;

        // Find available patterns
        var availablePatterns = new List<AttackPattern>();
        float totalWeight = 0f;

        foreach (var pattern in _enemyData.attackPatterns)
        {
            if (pattern == null) continue;

            float cooldown = _patternCooldowns.ContainsKey(pattern) ? _patternCooldowns[pattern] : 0f;
            if (pattern.CanUse(healthPercent, distanceToTarget, cooldown))
            {
                availablePatterns.Add(pattern);
                totalWeight += pattern.weight;
            }
        }

        if (availablePatterns.Count == 0) return null;

        // Sort by priority, then select weighted random
        availablePatterns.Sort((a, b) => b.priority.CompareTo(a.priority));

        // If highest priority has multiple, select by weight
        int highestPriority = availablePatterns[0].priority;
        var topPriorityPatterns = availablePatterns.FindAll(p => p.priority == highestPriority);

        if (topPriorityPatterns.Count == 1)
        {
            return topPriorityPatterns[0];
        }

        // Weighted random selection
        float weight = Random.Range(0f, topPriorityPatterns.Count);
        float cumulative = 0f;
        foreach (var pattern in topPriorityPatterns)
        {
            cumulative += pattern.weight;
            if (weight <= cumulative)
            {
                return pattern;
            }
        }

        return topPriorityPatterns[0];
    }

    private void ExecutePatternAttack(AttackPattern pattern)
    {
        _currentPattern = pattern;
        _currentAttackIndex = 0;

        var attack = pattern.GetAttack(0);
        if (attack != null)
        {
            ExecuteAttackData(attack);
        }

        // Set cooldown
        _patternCooldowns[pattern] = pattern.cooldown;
    }

    private void ExecuteAttackData(AttackData attack)
    {
        if (_currentTarget == null) return;

        // For ranged behavior, fire projectile
        if (_behavior == AIBehavior.Ranged && _projectilePrefab != null)
        {
            FireProjectile(attack);
        }
        else
        {
            // Melee attack
            var damageInfo = attack.CreateDamageInfo(gameObject);
            damageInfo.hitPoint = _currentTarget.position;

            if (_currentTarget.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(damageInfo);
            }
        }

        // Trigger animation
        if (_animator != null && !string.IsNullOrEmpty(attack.animationTrigger))
        {
            _animator.SetTrigger(attack.animationTrigger);
        }
    }

    private void ExecuteBasicAttack()
    {
        if (_currentTarget == null) return;

        // Create damage info
        DamageInfo damageInfo;
        if (_enemyData != null)
        {
            damageInfo = _enemyData.CreateDamageInfo(gameObject, _currentTarget.position);
        }
        else
        {
            damageInfo = new DamageInfo
            {
                baseDamage = _attackDamage,
                damageType = _damageType,
                attacker = gameObject,
                hitPoint = _currentTarget.position
            };
        }

        // For ranged behavior, fire projectile
        if (_behavior == AIBehavior.Ranged && _projectilePrefab != null)
        {
            FireProjectile(damageInfo);
        }
        else
        {
            // Apply damage to target
            if (_currentTarget.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(damageInfo);
            }
            else if (_currentTarget.TryGetComponent<PlayerStats>(out var stats))
            {
                stats.TakeDamage(_attackDamage);
            }
        }

        // Trigger animation
        if (_animator != null)
        {
            _animator.SetTrigger("Attack");
        }
    }

    private void FireProjectile(AttackData attack)
    {
        FireProjectile(attack.CreateDamageInfo(gameObject));
    }

    private void FireProjectile(DamageInfo damageInfo)
    {
        if (_projectilePrefab == null || _currentTarget == null) return;

        var spawnPoint = _projectileSpawnPoint != null ? _projectileSpawnPoint : transform;
        var projectile = Instantiate(_projectilePrefab, spawnPoint.position, spawnPoint.rotation);

        // Calculate direction to target
        Vector3 direction = (_currentTarget.position - spawnPoint.position).normalized;

        // Setup projectile
        if (projectile.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = direction * _projectileSpeed;
        }

        // Setup damage on projectile if it has a damage component
        if (projectile.TryGetComponent<Projectile>(out var proj))
        {
            proj.Initialize(_currentTarget, _projectileSpeed, damageInfo);
        }

        // Destroy after time
        Destroy(projectile, 10f);
    }

    private void PlayAttackSound()
    {
        if (_enemyData?.attackSounds == null || _enemyData.attackSounds.Length == 0) return;

        var sound = _enemyData.attackSounds[Random.Range(0, _enemyData.attackSounds.Length)];
        if (sound != null)
        {
            AudioSource.PlayClipAtPoint(sound, transform.position);
        }
    }

    #endregion

    #region Detection

    private bool CanSeeTarget(Transform target)
    {
        if (target == null) return false;

        Vector3 directionToTarget = (target.position - transform.position).normalized;
        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        // Check range
        if (distanceToTarget > _detectionRange) return false;

        // Check FOV
        float angle = Vector3.Angle(transform.forward, directionToTarget);
        if (angle > _fieldOfView / 2f) return false;

        // Check obstacles
        Vector3 rayStart = transform.position + Vector3.up;
        Vector3 rayEnd = target.position + Vector3.up;
        if (Physics.Linecast(rayStart, rayEnd, _obstacleLayer))
        {
            return false;
        }

        return true;
    }

    #endregion

    #region Event Handlers

    private void HandleDeath()
    {
        ChangeState(EnemyAIState.Dead);
        _agent.isStopped = true;
        enabled = false;

        // Play death sound
        if (_enemyData?.deathSound != null)
        {
            AudioSource.PlayClipAtPoint(_enemyData.deathSound, transform.position);
        }

        // Notify achievement system
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnEnemyKilled();

            // Check if boss
            if (_enemyData != null && _enemyData.isBoss)
            {
                AchievementManager.Instance.OnBossDefeated();
            }
        }
    }

    private void HandleDamageInfo(DamageInfo damageInfo)
    {
        float damage = damageInfo.baseDamage;

        // Add to aggro if we have a system
        if (_aggro != null && damageInfo.attacker != null)
        {
            _aggro.AddThreat(damageInfo.attacker, damage);
        }

        // Accumulate stagger
        _staggerAccumulator += damageInfo.staggerValue > 0 ? damageInfo.staggerValue : damage;
        _staggerRecoveryTimer = _staggerRecoveryTime;

        if (_staggerAccumulator >= _staggerThreshold && _currentState != EnemyAIState.Staggered)
        {
            ApplyStagger();
        }
    }

    private void HandleAggroTargetChanged(GameObject oldTarget, GameObject newTarget)
    {
        _currentTarget = newTarget?.transform;
    }

    #endregion

    #region Stagger

    public void ApplyStagger()
    {
        _staggerAccumulator = 0f;
        _staggerEndTime = Time.time + _staggerDuration;
        ChangeState(EnemyAIState.Staggered);

        if (_animator != null)
        {
            _animator.SetTrigger("Stagger");
        }
    }

    #endregion

    #region Helpers

    private void FaceTarget()
    {
        if (_currentTarget == null) return;

        Vector3 direction = (_currentTarget.position - transform.position).normalized;
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

        // Stagger recovery
        if (_staggerRecoveryTimer > 0)
        {
            _staggerRecoveryTimer -= Time.deltaTime;
            if (_staggerRecoveryTimer <= 0)
            {
                _staggerAccumulator = 0f;
            }
        }
    }

    private void UpdatePatternCooldowns()
    {
        var keys = new List<AttackPattern>(_patternCooldowns.Keys);
        foreach (var key in keys)
        {
            if (_patternCooldowns[key] > 0)
            {
                _patternCooldowns[key] -= Time.deltaTime;
            }
        }
    }

    #endregion

    #region Debug

    private void OnDrawGizmosSelected()
    {
        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _enemyData != null ? _enemyData.detectionRange : _detectionRange);

        // Attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _enemyData != null ? _enemyData.attackRange : _attackRange);

        // Field of view
        float fov = _enemyData != null ? _enemyData.fieldOfView : _fieldOfView;
        float range = _enemyData != null ? _enemyData.detectionRange : _detectionRange;
        Gizmos.color = Color.blue;
        Vector3 leftBound = Quaternion.Euler(0, -fov / 2f, 0) * transform.forward * range;
        Vector3 rightBound = Quaternion.Euler(0, fov / 2f, 0) * transform.forward * range;
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

        // Spawn position
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(_spawnPosition != Vector3.zero ? _spawnPosition : transform.position, Vector3.one * 0.5f);
    }

    #endregion

    #region Wave Modifiers

    /// <summary>
    /// Applique un multiplicateur de vitesse.
    /// </summary>
    public void ApplySpeedMultiplier(float multiplier)
    {
        _patrolSpeed *= multiplier;
        _chaseSpeed *= multiplier;

        if (_agent != null)
        {
            _agent.speed = _currentState == EnemyAIState.Chase ? _chaseSpeed : _patrolSpeed;
        }
    }

    /// <summary>
    /// Applique un multiplicateur de degats.
    /// </summary>
    public void ApplyDamageMultiplier(float multiplier)
    {
        _attackDamage *= multiplier;
    }

    /// <summary>
    /// Applique tous les multiplicateurs de vague.
    /// </summary>
    public void ApplyWaveModifiers(float healthMult, float damageMult, float speedMult)
    {
        // Modifier la sante via le composant Health
        if (_health != null)
        {
            float newMaxHealth = _health.MaxHealth * healthMult;
            _health.SetMaxHealth(newMaxHealth);
            _health.ResetHealth();
        }

        ApplyDamageMultiplier(damageMult);
        ApplySpeedMultiplier(speedMult);
    }

    #endregion
}
