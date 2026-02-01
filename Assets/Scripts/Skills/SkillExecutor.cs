using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Executeur de competences. Gere l'execution reelle des skills:
/// - Recherche de cibles selon le SkillTargetType
/// - Application des degats/soins
/// - Spawn des VFX
/// - Declenchement des animations
/// - Lecture des sons
/// </summary>
public class SkillExecutor : MonoBehaviour
{
    #region Singleton

    public static SkillExecutor Instance { get; private set; }

    #endregion

    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private LayerMask _enemyLayer;
    [SerializeField] private LayerMask _allyLayer;
    [SerializeField] private float _defaultTargetSearchRadius = 15f;

    [Header("VFX Settings")]
    [SerializeField] private Transform _vfxContainer;
    [SerializeField] private float _vfxAutoDestroyTime = 5f;

    [Header("Feedback")]
    [SerializeField] private GameObject _damageNumberPrefab;
    [SerializeField] private GameObject _healNumberPrefab;
    [SerializeField] private float _numberFloatHeight = 2f;
    [SerializeField] private float _numberDuration = 1f;

    #endregion

    #region Private Fields

    private AudioSource _audioSource;
    private Camera _mainCamera;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        _mainCamera = Camera.main;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Execute une competence depuis un caster.
    /// </summary>
    /// <param name="skill">Donnees de la competence</param>
    /// <param name="caster">Transform du lanceur</param>
    /// <param name="casterStats">Stats du lanceur (optionnel)</param>
    /// <param name="targetOverride">Cible specifique (optionnel)</param>
    /// <returns>True si la competence a ete executee</returns>
    public bool Execute(SkillData skill, Transform caster, PlayerStats casterStats = null, Transform targetOverride = null)
    {
        if (skill == null || caster == null)
        {
            Debug.LogWarning("[SkillExecutor] Skill ou caster null");
            return false;
        }

        // Gerer selon le type de skill
        switch (skill.skillType)
        {
            case SkillType.Active:
                return ExecuteActiveSkill(skill, caster, casterStats, targetOverride);

            case SkillType.Ultimate:
                return ExecuteUltimateSkill(skill, caster, casterStats, targetOverride);

            case SkillType.Passive:
                // Les passifs ne s'executent pas activement
                Debug.Log($"[SkillExecutor] {skill.skillName} est un passif, pas d'execution active");
                return false;

            default:
                return ExecuteActiveSkill(skill, caster, casterStats, targetOverride);
        }
    }

    /// <summary>
    /// Execute une competence avec plusieurs coups (hit count > 1).
    /// </summary>
    public void ExecuteMultiHit(SkillData skill, Transform caster, PlayerStats casterStats, List<IDamageable> targets)
    {
        if (skill.hitCount <= 1)
        {
            ApplyDamageToTargets(skill, caster, casterStats, targets);
            return;
        }

        StartCoroutine(MultiHitCoroutine(skill, caster, casterStats, targets));
    }

    #endregion

    #region Private Methods - Execution

    private bool ExecuteActiveSkill(SkillData skill, Transform caster, PlayerStats casterStats, Transform targetOverride)
    {
        // 1. Jouer l'animation si definie
        TriggerAnimation(caster, skill.animationTrigger);

        // 2. Jouer le son
        PlaySkillSound(skill.soundEffect);

        // 3. Trouver les cibles selon le targetType
        List<IDamageable> targets = FindTargets(skill, caster, targetOverride);

        // 4. Spawner le VFX
        SpawnVFX(skill, caster, targets);

        // 5. Appliquer l'effet (degats ou soin)
        if (skill.isHeal)
        {
            ApplyHealToTargets(skill, caster, casterStats, targets);
        }
        else
        {
            ExecuteMultiHit(skill, caster, casterStats, targets);
        }

        // 6. Appliquer les effets de statut si applicable
        if (skill.appliesStatusEffect)
        {
            ApplyStatusEffects(skill, targets);
        }

        // 7. Appliquer l'element si applicable
        if (skill.appliesElement)
        {
            ApplyElementToTargets(skill, targets);
        }

        return true;
    }

    private bool ExecuteUltimateSkill(SkillData skill, Transform caster, PlayerStats casterStats, Transform targetOverride)
    {
        // Les ultimates sont similaires aux actifs mais avec plus de feedback
        // Camera shake, slowdown, etc.

        // Petit ralenti pour effet dramatique
        StartCoroutine(UltimateSlowmoEffect());

        // Executer comme un skill actif
        return ExecuteActiveSkill(skill, caster, casterStats, targetOverride);
    }

    private IEnumerator MultiHitCoroutine(SkillData skill, Transform caster, PlayerStats casterStats, List<IDamageable> targets)
    {
        for (int i = 0; i < skill.hitCount; i++)
        {
            // Verifier que les cibles sont toujours valides
            targets.RemoveAll(t => t == null || t.IsDead);

            if (targets.Count == 0) break;

            ApplyDamageToTargets(skill, caster, casterStats, targets, skill.hitCount);

            if (i < skill.hitCount - 1)
            {
                yield return new WaitForSeconds(skill.hitInterval);
            }
        }
    }

    private IEnumerator UltimateSlowmoEffect()
    {
        float originalTimeScale = Time.timeScale;
        Time.timeScale = 0.3f;
        yield return new WaitForSecondsRealtime(0.15f);
        Time.timeScale = originalTimeScale;
    }

    #endregion

    #region Private Methods - Target Finding

    private List<IDamageable> FindTargets(SkillData skill, Transform caster, Transform targetOverride)
    {
        List<IDamageable> targets = new List<IDamageable>();
        Vector3 origin = caster.position;

        switch (skill.targetType)
        {
            case SkillTargetType.Self:
                // Cibler soi-meme
                var selfDamageable = caster.GetComponent<IDamageable>();
                if (selfDamageable != null)
                {
                    targets.Add(selfDamageable);
                }
                // Aussi verifier PlayerStats pour les heals
                var selfStats = caster.GetComponent<PlayerStats>();
                if (selfStats != null && skill.isHeal)
                {
                    // On va gerer ca differemment pour les soins
                }
                break;

            case SkillTargetType.SingleEnemy:
                // Trouver l'ennemi le plus proche ou utiliser la cible override
                IDamageable singleTarget = null;
                if (targetOverride != null)
                {
                    singleTarget = targetOverride.GetComponent<IDamageable>();
                }
                else
                {
                    singleTarget = FindClosestEnemy(origin, skill.range);
                }
                if (singleTarget != null && !singleTarget.IsDead)
                {
                    targets.Add(singleTarget);
                }
                break;

            case SkillTargetType.AllEnemies:
                // Tous les ennemis dans la portee
                targets.AddRange(FindAllEnemiesInRange(origin, skill.range));
                break;

            case SkillTargetType.Area:
                // Zone d'effet autour du caster ou de la cible
                Vector3 areaCenter = targetOverride != null ? targetOverride.position : origin + caster.forward * (skill.range * 0.5f);
                targets.AddRange(FindAllEnemiesInRange(areaCenter, skill.areaRadius));
                break;

            case SkillTargetType.Cone:
                // Cone devant le caster
                targets.AddRange(FindEnemiesInCone(origin, caster.forward, skill.range, skill.coneAngle));
                break;

            case SkillTargetType.Line:
                // Ligne devant le caster
                targets.AddRange(FindEnemiesInLine(origin, caster.forward, skill.range, 1f));
                break;

            case SkillTargetType.SingleAlly:
                // Trouver l'allie le plus proche ou soi-meme
                var allyTarget = targetOverride != null ?
                    targetOverride.GetComponent<IDamageable>() :
                    caster.GetComponent<IDamageable>();
                if (allyTarget != null)
                {
                    targets.Add(allyTarget);
                }
                break;

            case SkillTargetType.AllAllies:
                // Tous les allies dans la portee
                targets.AddRange(FindAllAlliesInRange(origin, skill.range));
                // Inclure soi-meme
                var selfAlly = caster.GetComponent<IDamageable>();
                if (selfAlly != null && !targets.Contains(selfAlly))
                {
                    targets.Add(selfAlly);
                }
                break;
        }

        return targets;
    }

    private IDamageable FindClosestEnemy(Vector3 origin, float range)
    {
        Collider[] colliders = Physics.OverlapSphere(origin, range, _enemyLayer);

        IDamageable closest = null;
        float closestDistance = float.MaxValue;

        foreach (var col in colliders)
        {
            var damageable = col.GetComponent<IDamageable>();
            if (damageable == null || damageable.IsDead) continue;

            // Aussi verifier sur le parent
            if (damageable == null)
            {
                damageable = col.GetComponentInParent<IDamageable>();
            }
            if (damageable == null || damageable.IsDead) continue;

            float dist = Vector3.Distance(origin, col.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                closest = damageable;
            }
        }

        return closest;
    }

    private List<IDamageable> FindAllEnemiesInRange(Vector3 origin, float range)
    {
        List<IDamageable> enemies = new List<IDamageable>();
        Collider[] colliders = Physics.OverlapSphere(origin, range, _enemyLayer);

        foreach (var col in colliders)
        {
            var damageable = col.GetComponent<IDamageable>();
            if (damageable == null)
            {
                damageable = col.GetComponentInParent<IDamageable>();
            }

            if (damageable != null && !damageable.IsDead && !enemies.Contains(damageable))
            {
                enemies.Add(damageable);
            }
        }

        return enemies;
    }

    private List<IDamageable> FindEnemiesInCone(Vector3 origin, Vector3 direction, float range, float angle)
    {
        List<IDamageable> enemies = new List<IDamageable>();
        Collider[] colliders = Physics.OverlapSphere(origin, range, _enemyLayer);

        float halfAngle = angle * 0.5f;

        foreach (var col in colliders)
        {
            Vector3 toTarget = (col.transform.position - origin).normalized;
            float angleToTarget = Vector3.Angle(direction, toTarget);

            if (angleToTarget <= halfAngle)
            {
                var damageable = col.GetComponent<IDamageable>();
                if (damageable == null)
                {
                    damageable = col.GetComponentInParent<IDamageable>();
                }

                if (damageable != null && !damageable.IsDead && !enemies.Contains(damageable))
                {
                    enemies.Add(damageable);
                }
            }
        }

        return enemies;
    }

    private List<IDamageable> FindEnemiesInLine(Vector3 origin, Vector3 direction, float length, float width)
    {
        List<IDamageable> enemies = new List<IDamageable>();

        // Utiliser un BoxCast pour la ligne
        RaycastHit[] hits = Physics.BoxCastAll(
            origin,
            new Vector3(width, width, 0.1f),
            direction,
            Quaternion.LookRotation(direction),
            length,
            _enemyLayer
        );

        foreach (var hit in hits)
        {
            var damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable == null)
            {
                damageable = hit.collider.GetComponentInParent<IDamageable>();
            }

            if (damageable != null && !damageable.IsDead && !enemies.Contains(damageable))
            {
                enemies.Add(damageable);
            }
        }

        return enemies;
    }

    private List<IDamageable> FindAllAlliesInRange(Vector3 origin, float range)
    {
        List<IDamageable> allies = new List<IDamageable>();
        Collider[] colliders = Physics.OverlapSphere(origin, range, _allyLayer);

        foreach (var col in colliders)
        {
            var damageable = col.GetComponent<IDamageable>();
            if (damageable == null)
            {
                damageable = col.GetComponentInParent<IDamageable>();
            }

            if (damageable != null && !damageable.IsDead && !allies.Contains(damageable))
            {
                allies.Add(damageable);
            }
        }

        return allies;
    }

    #endregion

    #region Private Methods - Damage/Heal Application

    private void ApplyDamageToTargets(SkillData skill, Transform caster, PlayerStats casterStats, List<IDamageable> targets, int totalHits = 1)
    {
        float attackStat = casterStats != null ? casterStats.Strength : 0f;
        float baseDamage = skill.CalculateDamage(attackStat);

        // Diviser les degats si multi-hit
        float damagePerHit = baseDamage / totalHits;

        foreach (var target in targets)
        {
            if (target == null || target.IsDead) continue;

            // Creer le DamageInfo
            DamageInfo damageInfo = new DamageInfo
            {
                baseDamage = damagePerHit,
                damageType = skill.damageType,
                attacker = caster.gameObject,
                hitPoint = GetTargetPosition(target),
                hitNormal = -caster.forward,
                isCritical = Random.value < (casterStats != null ? casterStats.CritChance : 0.05f),
                criticalMultiplier = casterStats != null ? casterStats.CritDamage : 1.5f
            };

            // Appliquer les degats
            target.TakeDamage(damageInfo);

            // Afficher le nombre de degats
            ShowDamageNumber(GetTargetPosition(target), damageInfo.GetEffectiveDamage(), damageInfo.isCritical);
        }
    }

    private void ApplyHealToTargets(SkillData skill, Transform caster, PlayerStats casterStats, List<IDamageable> targets)
    {
        float healingStat = casterStats != null ? casterStats.Intelligence : 0f;
        float healAmount = skill.CalculateHeal(healingStat);

        // Si on cible soi-meme et qu'on a des PlayerStats
        if (skill.targetType == SkillTargetType.Self && casterStats != null)
        {
            casterStats.Heal(healAmount);
            ShowHealNumber(caster.position, healAmount);
            return;
        }

        // Sinon, soigner les cibles qui ont Health
        foreach (var target in targets)
        {
            if (target == null || target.IsDead) continue;

            // Verifier si c'est un Health component
            var health = (target as MonoBehaviour)?.GetComponent<Health>();
            if (health != null)
            {
                health.Heal(healAmount);
                ShowHealNumber(GetTargetPosition(target), healAmount);
            }
        }
    }

    private void ApplyStatusEffects(SkillData skill, List<IDamageable> targets)
    {
        // TODO: Implementer le systeme de status effects (Buff/Debuff)
        // Pour l'instant, log seulement
        foreach (var target in targets)
        {
            Debug.Log($"[SkillExecutor] Applying status effect from {skill.skillName} for {skill.statusDuration}s");
        }
    }

    private void ApplyElementToTargets(SkillData skill, List<IDamageable> targets)
    {
        foreach (var target in targets)
        {
            if (target == null) continue;

            // Chercher l'ElementalStatus sur la cible
            var mono = target as MonoBehaviour;
            if (mono == null) continue;

            var elementalStatus = mono.GetComponent<ElementalStatus>();
            if (elementalStatus != null)
            {
                elementalStatus.ApplyElement(skill.elementType, skill.elementGauge);
            }
        }
    }

    private Vector3 GetTargetPosition(IDamageable target)
    {
        var mono = target as MonoBehaviour;
        if (mono != null)
        {
            return mono.transform.position + Vector3.up;
        }
        return Vector3.zero;
    }

    #endregion

    #region Private Methods - VFX/Animation/Sound

    private void TriggerAnimation(Transform caster, string triggerName)
    {
        if (string.IsNullOrEmpty(triggerName)) return;

        var animator = caster.GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetTrigger(triggerName);
        }
    }

    private void PlaySkillSound(AudioClip clip)
    {
        if (clip == null || _audioSource == null) return;
        _audioSource.PlayOneShot(clip);
    }

    private void SpawnVFX(SkillData skill, Transform caster, List<IDamageable> targets)
    {
        if (skill.vfxPrefab == null) return;

        Vector3 spawnPosition;
        Quaternion spawnRotation = caster.rotation;

        switch (skill.targetType)
        {
            case SkillTargetType.Self:
                spawnPosition = caster.position;
                break;

            case SkillTargetType.SingleEnemy:
            case SkillTargetType.SingleAlly:
                if (targets.Count > 0)
                {
                    spawnPosition = GetTargetPosition(targets[0]);
                }
                else
                {
                    spawnPosition = caster.position + caster.forward * skill.range;
                }
                break;

            case SkillTargetType.Area:
                spawnPosition = caster.position + caster.forward * (skill.range * 0.5f);
                break;

            case SkillTargetType.Cone:
            case SkillTargetType.Line:
                spawnPosition = caster.position + caster.forward;
                break;

            default:
                spawnPosition = caster.position;
                break;
        }

        Transform parent = _vfxContainer != null ? _vfxContainer : null;
        var vfx = Instantiate(skill.vfxPrefab, spawnPosition, spawnRotation, parent);

        // Auto-destruction
        Destroy(vfx, _vfxAutoDestroyTime);
    }

    private void ShowDamageNumber(Vector3 position, float damage, bool isCritical)
    {
        if (_damageNumberPrefab == null)
        {
            // Fallback: creer un nombre simple
            CreateSimpleDamageNumber(position, damage, isCritical, false);
            return;
        }

        var numObj = Instantiate(_damageNumberPrefab, position, Quaternion.identity);
        // Configurer le nombre (suppose qu'il a un component DamageNumber ou similaire)
        var textMesh = numObj.GetComponentInChildren<TMPro.TextMeshPro>();
        if (textMesh != null)
        {
            textMesh.text = Mathf.RoundToInt(damage).ToString();
            textMesh.color = isCritical ? Color.yellow : Color.white;
        }

        StartCoroutine(FloatAndDestroyNumber(numObj, _numberDuration));
    }

    private void ShowHealNumber(Vector3 position, float heal)
    {
        if (_healNumberPrefab == null)
        {
            CreateSimpleDamageNumber(position, heal, false, true);
            return;
        }

        var numObj = Instantiate(_healNumberPrefab, position, Quaternion.identity);
        var textMesh = numObj.GetComponentInChildren<TMPro.TextMeshPro>();
        if (textMesh != null)
        {
            textMesh.text = "+" + Mathf.RoundToInt(heal).ToString();
            textMesh.color = Color.green;
        }

        StartCoroutine(FloatAndDestroyNumber(numObj, _numberDuration));
    }

    private void CreateSimpleDamageNumber(Vector3 position, float value, bool isCritical, bool isHeal)
    {
        // Creer un simple GameObject avec TextMeshPro
        var numObj = new GameObject("DamageNumber");
        numObj.transform.position = position;

        var textMesh = numObj.AddComponent<TMPro.TextMeshPro>();
        textMesh.text = isHeal ? "+" + Mathf.RoundToInt(value).ToString() : Mathf.RoundToInt(value).ToString();
        textMesh.fontSize = isCritical ? 8f : 6f;
        textMesh.alignment = TMPro.TextAlignmentOptions.Center;

        if (isHeal)
            textMesh.color = Color.green;
        else if (isCritical)
            textMesh.color = Color.yellow;
        else
            textMesh.color = Color.white;

        // Faire face a la camera
        if (_mainCamera != null)
        {
            numObj.transform.LookAt(_mainCamera.transform);
            numObj.transform.Rotate(0, 180, 0);
        }

        StartCoroutine(FloatAndDestroyNumber(numObj, _numberDuration));
    }

    private IEnumerator FloatAndDestroyNumber(GameObject numObj, float duration)
    {
        float elapsed = 0f;
        Vector3 startPos = numObj.transform.position;
        Vector3 endPos = startPos + Vector3.up * _numberFloatHeight;

        while (elapsed < duration)
        {
            if (numObj == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            numObj.transform.position = Vector3.Lerp(startPos, endPos, t);

            // Fade out
            var textMesh = numObj.GetComponentInChildren<TMPro.TextMeshPro>();
            if (textMesh != null)
            {
                Color c = textMesh.color;
                c.a = 1f - t;
                textMesh.color = c;
            }

            yield return null;
        }

        if (numObj != null)
        {
            Destroy(numObj);
        }
    }

    #endregion
}
