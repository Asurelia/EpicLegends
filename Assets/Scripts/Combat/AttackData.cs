using UnityEngine;

/// <summary>
/// Donnees d'une attaque individuelle.
/// Definit les degats, timing, effets et comportement.
/// </summary>
[CreateAssetMenu(fileName = "NewAttack", menuName = "EpicLegends/Combat/Attack Data")]
public class AttackData : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Nom de l'attaque")]
    public string attackName;

    [Tooltip("Description de l'attaque")]
    [TextArea(2, 4)]
    public string description;

    [Header("Degats")]
    [Tooltip("Degats de base de l'attaque")]
    public float baseDamage = 10f;

    [Tooltip("Type de degats")]
    public DamageType damageType = DamageType.Physical;

    [Tooltip("Multiplicateur de degats critiques")]
    public float criticalMultiplier = 1.5f;

    [Header("Knockback")]
    [Tooltip("Force de recul")]
    public float knockbackForce = 3f;

    [Tooltip("Direction du knockback (local)")]
    public Vector3 knockbackDirection = Vector3.forward;

    [Header("Stagger")]
    [Tooltip("Valeur de stagger infligee")]
    public float staggerValue = 10f;

    [Header("Animation")]
    [Tooltip("Trigger d'animation a declencher")]
    public string animationTrigger;

    [Tooltip("Duree totale de l'animation en secondes")]
    public float animationDuration = 0.8f;

    [Tooltip("Temps avant de pouvoir annuler l'attaque")]
    public float cancelTime = 0.5f;

    [Header("Hitbox")]
    [Tooltip("Donnees de la hitbox")]
    public HitboxData hitboxData;

    [Header("Attaque Chargee")]
    [Tooltip("Est-ce une attaque chargeable?")]
    public bool isChargedAttack;

    [Tooltip("Temps de charge minimum")]
    public float minChargeTime = 0.5f;

    [Tooltip("Temps de charge maximum")]
    public float maxChargeTime = 2f;

    [Tooltip("Multiplicateur a charge max")]
    public float chargeMultiplier = 2.5f;

    [Header("Comportement")]
    [Tooltip("L'attaque peut-elle etre paree?")]
    public bool canBeParried = true;

    [Tooltip("L'attaque peut-elle etre bloquee?")]
    public bool canBeBlocked = true;

    [Tooltip("L'attaque brise-t-elle la garde?")]
    public bool isGuardBreak;

    [Tooltip("Cout en stamina")]
    public float staminaCost = 10f;

    [Header("Combo")]
    [Tooltip("Peut enchainer vers un combo?")]
    public bool canCombo = true;

    [Tooltip("Fenetre de combo en secondes")]
    public float comboWindowStart = 0.3f;
    public float comboWindowEnd = 0.6f;

    [Header("Effets")]
    [Tooltip("Prefab d'effet au debut de l'attaque")]
    public GameObject startEffectPrefab;

    [Tooltip("Son au debut de l'attaque")]
    public AudioClip attackSound;

    /// <summary>
    /// Cree une DamageInfo a partir de cette attaque.
    /// </summary>
    public DamageInfo CreateDamageInfo(GameObject attacker, float chargePercent = 0f)
    {
        float damage = baseDamage;
        if (isChargedAttack && chargePercent > 0f)
        {
            damage *= Mathf.Lerp(1f, chargeMultiplier, chargePercent);
        }

        return new DamageInfo(damage, damageType, attacker)
        {
            knockbackForce = knockbackForce,
            knockbackDirection = knockbackDirection,
            staggerValue = staggerValue,
            criticalMultiplier = criticalMultiplier,
            canBeParried = canBeParried,
            canBeBlocked = canBeBlocked,
            isGuardBreak = isGuardBreak
        };
    }

    /// <summary>
    /// Verifie si on est dans la fenetre de combo.
    /// </summary>
    public bool IsInComboWindow(float normalizedTime)
    {
        return canCombo && normalizedTime >= comboWindowStart && normalizedTime <= comboWindowEnd;
    }
}
