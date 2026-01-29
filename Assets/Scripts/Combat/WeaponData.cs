using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Donnees de configuration d'une arme.
/// Definit les stats, comportements et combos d'une arme.
/// </summary>
[CreateAssetMenu(fileName = "NewWeapon", menuName = "EpicLegends/Combat/Weapon Data")]
public class WeaponData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("Nom de l'arme")]
    public string weaponName;

    [Tooltip("Description de l'arme")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Type d'arme")]
    public WeaponType weaponType = WeaponType.Sword;

    [Tooltip("Icone de l'arme")]
    public Sprite icon;

    [Tooltip("Prefab 3D de l'arme")]
    public GameObject prefab;

    #endregion

    #region Stats de Base

    [Header("Stats de Base")]
    [Tooltip("Degats de base de l'arme")]
    public float baseDamage = 50f;

    [Tooltip("Vitesse d'attaque (1 = normale)")]
    public float attackSpeed = 1f;

    [Tooltip("Portee de l'arme en metres")]
    public float range = 2f;

    [Tooltip("Est-ce une arme a distance?")]
    public bool isRanged = false;

    [Tooltip("Type de degats de base")]
    public DamageType damageType = DamageType.Physical;

    #endregion

    #region Critiques

    [Header("Critiques")]
    [Tooltip("Chance de critique additionnelle")]
    [Range(0f, 1f)]
    public float critChance = 0.05f;

    [Tooltip("Multiplicateur de degats critiques")]
    public float critMultiplier = 1.5f;

    #endregion

    #region Stagger et Knockback

    [Header("Stagger et Knockback")]
    [Tooltip("Valeur de stagger de base")]
    public float staggerValue = 10f;

    [Tooltip("Force de knockback de base")]
    public float knockbackForce = 3f;

    [Tooltip("Peut briser les gardes?")]
    public bool canBreakGuard = false;

    #endregion

    #region Charge

    [Header("Attaque Chargee")]
    [Tooltip("Peut charger les attaques?")]
    public bool canCharge = false;

    [Tooltip("Temps minimum de charge")]
    public float minChargeTime = 0.5f;

    [Tooltip("Temps maximum de charge")]
    public float maxChargeTime = 2f;

    [Tooltip("Multiplicateur de degats a charge complete")]
    public float chargeMultiplier = 2f;

    #endregion

    #region Combos

    [Header("Combos")]
    [Tooltip("Combo d'attaques legeres")]
    public ComboData lightCombo;

    [Tooltip("Combo d'attaques lourdes")]
    public ComboData heavyCombo;

    [Tooltip("Attaque speciale")]
    public AttackData specialAttack;

    #endregion

    #region Modificateurs

    [Header("Modificateurs")]
    [Tooltip("Bonus de vie drain (0 = aucun)")]
    [Range(0f, 1f)]
    public float lifesteal = 0f;

    [Tooltip("Bonus elementaire additionnel")]
    public ElementType? bonusElement = null;

    [Tooltip("Degats bonus elementaires")]
    public float bonusElementDamage = 0f;

    #endregion

    #region Audio/Visual

    [Header("Audio/Visual")]
    [Tooltip("Sons d'attaque")]
    public AudioClip[] attackSounds;

    [Tooltip("Son de charge complete")]
    public AudioClip chargeCompleteSound;

    [Tooltip("Effet de trail")]
    public GameObject trailEffect;

    #endregion

    #region Public Methods

    /// <summary>
    /// Calcule les degats avec les stats du personnage.
    /// </summary>
    /// <param name="attackStat">Stat d'attaque du personnage</param>
    /// <returns>Degats calcules</returns>
    public float GetDamageWithStats(float attackStat)
    {
        // Formule: baseDamage * (1 + attack/100)
        return baseDamage * (1f + attackStat / 100f);
    }

    /// <summary>
    /// Calcule les degats d'une attaque chargee.
    /// </summary>
    public float GetChargedDamage(float chargeTime, float attackStat = 0f)
    {
        if (!canCharge) return GetDamageWithStats(attackStat);

        float chargePercent = Mathf.Clamp01((chargeTime - minChargeTime) / (maxChargeTime - minChargeTime));
        float multiplier = Mathf.Lerp(1f, chargeMultiplier, chargePercent);

        return GetDamageWithStats(attackStat) * multiplier;
    }

    /// <summary>
    /// Cree une DamageInfo pour cette arme.
    /// </summary>
    public DamageInfo CreateDamageInfo(GameObject attacker, float attackStat = 0f, bool isCritical = false)
    {
        float damage = GetDamageWithStats(attackStat);
        if (isCritical)
        {
            damage *= critMultiplier;
        }

        return new DamageInfo
        {
            baseDamage = damage,
            damageType = damageType,
            attacker = attacker,
            knockbackForce = knockbackForce,
            staggerValue = staggerValue,
            isCritical = isCritical,
            criticalMultiplier = critMultiplier
        };
    }

    /// <summary>
    /// Retourne la categorie de portee.
    /// </summary>
    public WeaponRangeCategory GetRangeCategory()
    {
        if (isRanged) return WeaponRangeCategory.Ranged;
        if (range > 2.5f) return WeaponRangeCategory.MeleeExtended;
        return WeaponRangeCategory.Melee;
    }

    /// <summary>
    /// Obtient les stats par defaut selon le type d'arme.
    /// </summary>
    public static WeaponData CreateDefault(WeaponType type)
    {
        var data = CreateInstance<WeaponData>();
        data.weaponType = type;

        switch (type)
        {
            case WeaponType.Sword:
                data.weaponName = "Epee";
                data.baseDamage = 50f;
                data.attackSpeed = 1f;
                data.range = 2f;
                break;

            case WeaponType.Greatsword:
                data.weaponName = "Grande Epee";
                data.baseDamage = 100f;
                data.attackSpeed = 0.6f;
                data.range = 2.5f;
                data.staggerValue = 25f;
                data.knockbackForce = 5f;
                data.canBreakGuard = true;
                break;

            case WeaponType.DualBlades:
                data.weaponName = "Doubles Lames";
                data.baseDamage = 30f;
                data.attackSpeed = 1.5f;
                data.range = 1.5f;
                data.staggerValue = 5f;
                data.knockbackForce = 1f;
                break;

            case WeaponType.Spear:
                data.weaponName = "Lance";
                data.baseDamage = 45f;
                data.attackSpeed = 0.9f;
                data.range = 3.5f;
                data.staggerValue = 15f;
                break;

            case WeaponType.Bow:
                data.weaponName = "Arc";
                data.baseDamage = 40f;
                data.attackSpeed = 0.8f;
                data.range = 20f;
                data.isRanged = true;
                data.canCharge = true;
                data.chargeMultiplier = 2f;
                break;

            case WeaponType.Staff:
                data.weaponName = "Baton";
                data.baseDamage = 35f;
                data.attackSpeed = 0.7f;
                data.range = 15f;
                data.isRanged = true;
                data.damageType = DamageType.Fire; // Par defaut
                break;

            case WeaponType.Scythe:
                data.weaponName = "Faux";
                data.baseDamage = 65f;
                data.attackSpeed = 0.75f;
                data.range = 2.8f;
                data.lifesteal = 0.1f;
                break;
        }

        return data;
    }

    #endregion
}
