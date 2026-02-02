using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Composant qui gere les reactions elementaires sur une entite.
/// Detecte les combinaisons d'elements et declenche les effets.
/// </summary>
[RequireComponent(typeof(ElementalStatus))]
public class ElementalReactionHandler : MonoBehaviour
{
    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private float _elementalMastery = 0f;
    [SerializeField] private LayerMask _affectedLayers = -1;

    [Header("Reaction Data (Optionnel)")]
    [SerializeField] private List<ElementalReactionData> _reactionConfigs = new List<ElementalReactionData>();

    #endregion

    #region Private Fields

    private ElementalStatus _status;
    private Dictionary<ElementalReactionType, ElementalReactionData> _reactionDataMap;

    // Effets actifs
    private bool _isFrozen;
    private float _frozenTimer;
    private bool _hasDefenseReduction;
    private float _defenseReductionTimer;
    private float _defenseReductionAmount;

    #endregion

    #region Events

    /// <summary>
    /// Declenche quand une reaction se produit.
    /// </summary>
    public event Action<ElementalReactionType, float> OnReactionTriggered;

    /// <summary>
    /// Declenche quand l'entite est gelee.
    /// </summary>
    public event Action<float> OnFrozen;

    /// <summary>
    /// Declenche quand le gel se termine.
    /// </summary>
    public event Action OnThawed;

    /// <summary>
    /// Declenche quand un bouclier crystallize est cree.
    /// </summary>
    public event Action<float> OnShieldCreated;

    /// <summary>
    /// Declenche quand une explosion AoE se produit.
    /// </summary>
    public event Action<Vector3, float, ElementalReactionType> OnAoETriggered;

    #endregion

    #region Properties

    /// <summary>
    /// Maitrise elementaire (augmente les degats des reactions).
    /// </summary>
    public float ElementalMastery
    {
        get => _elementalMastery;
        set => _elementalMastery = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Est-ce que l'entite est gelee?
    /// </summary>
    public bool IsFrozen => _isFrozen;

    /// <summary>
    /// Reduction de defense active?
    /// </summary>
    public bool HasDefenseReduction => _hasDefenseReduction;

    /// <summary>
    /// Montant de la reduction de defense.
    /// </summary>
    public float DefenseReductionAmount => _hasDefenseReduction ? _defenseReductionAmount : 0f;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _status = GetComponent<ElementalStatus>();
        BuildReactionDataMap();
    }

    private void Update()
    {
        UpdateFreezeStatus();
        UpdateDefenseReduction();
    }

    private void OnDestroy()
    {
        // MAJOR FIX: Stop all coroutines to prevent memory leaks
        StopAllCoroutines();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Tente de declencher une reaction avec un nouvel element.
    /// </summary>
    /// <param name="incomingElement">Element entrant</param>
    /// <param name="baseDamage">Degats de base</param>
    /// <returns>Les degats modifies par la reaction</returns>
    public float TriggerReaction(ElementType incomingElement, float baseDamage)
    {
        if (!_status.HasElement)
        {
            // Pas d'element present, appliquer simplement le nouvel element
            _status.ApplyElement(incomingElement, 1f);
            return baseDamage;
        }

        // Determiner la reaction
        ElementalReactionType reaction = ElementalReactionCalculator.GetReaction(
            incomingElement,
            _status.CurrentElement
        );

        if (reaction == ElementalReactionType.None)
        {
            // Pas de reaction, remplacer l'element
            _status.ApplyElement(incomingElement, 1f);
            return baseDamage;
        }

        // Calculer les degats de la reaction
        float reactionDamage = ElementalReactionCalculator.CalculateReactionDamage(
            baseDamage,
            reaction,
            _elementalMastery
        );

        // Appliquer les effets de la reaction
        ApplyReactionEffects(reaction, reactionDamage, incomingElement);

        // Consommer l'element si necessaire
        if (ElementalReactionCalculator.ConsumesElement(reaction))
        {
            _status.ClearElement();
        }

        // Declencher l'event
        OnReactionTriggered?.Invoke(reaction, reactionDamage);

        return reactionDamage;
    }

    /// <summary>
    /// Brise le gel (par exemple avec une attaque physique forte).
    /// </summary>
    public void BreakFreeze()
    {
        if (!_isFrozen) return;

        _isFrozen = false;
        _frozenTimer = 0f;
        OnThawed?.Invoke();
    }

    #endregion

    #region Private Methods

    private void BuildReactionDataMap()
    {
        _reactionDataMap = new Dictionary<ElementalReactionType, ElementalReactionData>();
        foreach (var config in _reactionConfigs)
        {
            if (config != null && !_reactionDataMap.ContainsKey(config.reactionType))
            {
                _reactionDataMap[config.reactionType] = config;
            }
        }
    }

    private void ApplyReactionEffects(ElementalReactionType reaction, float damage, ElementType triggerElement)
    {
        // Effets AoE
        if (ElementalReactionCalculator.IsAoEReaction(reaction))
        {
            float radius = ElementalReactionCalculator.GetAoERadius(reaction);
            TriggerAoE(reaction, damage, radius, triggerElement);
        }

        // Effets specifiques
        switch (reaction)
        {
            case ElementalReactionType.Frozen:
                ApplyFreeze(ElementalReactionCalculator.GetReactionDuration(reaction));
                break;

            case ElementalReactionType.Superconduct:
                ApplyDefenseReduction(0.4f, ElementalReactionCalculator.GetReactionDuration(reaction));
                break;

            case ElementalReactionType.Crystallize:
                CreateCrystallizeShield(damage * 0.2f);
                break;

            case ElementalReactionType.ElectroCharged:
                StartElectroCharged(damage);
                break;

            case ElementalReactionType.Melt:
                // Melt brise automatiquement le gel
                if (_isFrozen) BreakFreeze();
                break;
        }
    }

    private void TriggerAoE(ElementalReactionType reaction, float damage, float radius, ElementType swirlElement)
    {
        Vector3 center = transform.position;
        OnAoETriggered?.Invoke(center, radius, reaction);

        // Trouver les cibles dans le rayon
        Collider[] hitColliders = Physics.OverlapSphere(center, radius, _affectedLayers);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.gameObject == gameObject) continue;

            // Appliquer les degats AoE
            if (hitCollider.TryGetComponent<IDamageable>(out var damageable))
            {
                var damageInfo = new DamageInfo
                {
                    baseDamage = damage * 0.5f, // Degats reduits en AoE
                    damageType = GetDamageTypeFromElement(swirlElement),
                    attacker = gameObject,
                    hitPoint = hitCollider.transform.position
                };
                damageable.TakeDamage(damageInfo);
            }

            // Swirl propage l'element
            if (reaction == ElementalReactionType.Swirl)
            {
                var element = ElementalReactionCalculator.GetSwirlElement(swirlElement);
                if (element.HasValue && hitCollider.TryGetComponent<ElementalStatus>(out var targetStatus))
                {
                    targetStatus.ApplyElement(element.Value, 0.5f);
                }
            }
        }
    }

    private void ApplyFreeze(float duration)
    {
        _isFrozen = true;
        _frozenTimer = duration;
        OnFrozen?.Invoke(duration);
    }

    private void ApplyDefenseReduction(float amount, float duration)
    {
        _hasDefenseReduction = true;
        _defenseReductionAmount = amount;
        _defenseReductionTimer = duration;
    }

    private void CreateCrystallizeShield(float shieldValue)
    {
        OnShieldCreated?.Invoke(shieldValue);

        // Integrer avec le StatusEffectManager pour creer un vrai bouclier
        var statusManager = GetComponent<StatusEffectManager>();
        if (statusManager != null)
        {
            // Creer un effet de bouclier dynamique
            var shieldData = ScriptableObject.CreateInstance<StatusEffectData>();
            shieldData.effectId = "crystallize_shield";
            shieldData.effectName = "Crystallize Shield";
            shieldData.effectType = StatusEffectType.Shield;
            shieldData.category = StatusEffectCategory.Buff;
            shieldData.baseDuration = 10f; // Duree du bouclier crystallize
            shieldData.value = shieldValue;
            shieldData.canRefresh = true;
            shieldData.canStack = false;

            statusManager.ApplyEffect(shieldData, gameObject);
        }
    }

    private void StartElectroCharged(float damage)
    {
        // ElectroCharged applique des degats sur la duree (3 ticks sur 2 secondes)
        StartCoroutine(ElectroChargedCoroutine(damage));
    }

    private System.Collections.IEnumerator ElectroChargedCoroutine(float totalDamage)
    {
        const int tickCount = 3;
        const float tickInterval = 0.7f;
        float damagePerTick = totalDamage / tickCount;

        for (int i = 0; i < tickCount; i++)
        {
            yield return new WaitForSeconds(tickInterval);

            // Appliquer les degats electriques
            if (TryGetComponent<IDamageable>(out var damageable))
            {
                var damageInfo = new DamageInfo
                {
                    baseDamage = damagePerTick,
                    damageType = DamageType.Electric,
                    attacker = gameObject,
                    hitPoint = transform.position
                };
                damageable.TakeDamage(damageInfo);
            }

            // Propager aux entites proches dans l'eau (comportement ElectroCharged)
            PropagateElectroChargedToNearbyWetTargets(damagePerTick * 0.3f);
        }

        // Consommer les elements apres la reaction
        _status.ClearElement();
    }

    private void PropagateElectroChargedToNearbyWetTargets(float damage)
    {
        float propagationRadius = 3f;
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, propagationRadius, _affectedLayers);

        foreach (var collider in nearbyColliders)
        {
            if (collider.gameObject == gameObject) continue;

            // Verifier si la cible a de l'eau appliquee
            if (collider.TryGetComponent<ElementalStatus>(out var targetStatus))
            {
                if (targetStatus.CurrentElement == ElementType.Water)
                {
                    // Appliquer des degats reduits
                    if (collider.TryGetComponent<IDamageable>(out var damageable))
                    {
                        var damageInfo = new DamageInfo
                        {
                            baseDamage = damage,
                            damageType = DamageType.Electric,
                            attacker = gameObject,
                            hitPoint = collider.transform.position
                        };
                        damageable.TakeDamage(damageInfo);
                    }
                }
            }
        }
    }

    private void UpdateFreezeStatus()
    {
        if (!_isFrozen) return;

        _frozenTimer -= Time.deltaTime;
        if (_frozenTimer <= 0f)
        {
            _isFrozen = false;
            OnThawed?.Invoke();
        }
    }

    private void UpdateDefenseReduction()
    {
        if (!_hasDefenseReduction) return;

        _defenseReductionTimer -= Time.deltaTime;
        if (_defenseReductionTimer <= 0f)
        {
            _hasDefenseReduction = false;
            _defenseReductionAmount = 0f;
        }
    }

    private DamageType GetDamageTypeFromElement(ElementType element)
    {
        return element switch
        {
            ElementType.Fire => DamageType.Fire,
            ElementType.Water => DamageType.Water,
            ElementType.Ice => DamageType.Ice,
            ElementType.Electric => DamageType.Electric,
            ElementType.Wind => DamageType.Wind,
            ElementType.Earth => DamageType.Earth,
            ElementType.Light => DamageType.Light,
            ElementType.Dark => DamageType.Dark,
            _ => DamageType.Physical
        };
    }

    #endregion
}
