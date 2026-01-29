using UnityEngine;

/// <summary>
/// Donnees de configuration d'une tour de defense.
/// </summary>
[CreateAssetMenu(fileName = "NewTower", menuName = "EpicLegends/Building/Tower Data")]
public class TowerData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("Nom de la tour")]
    public string towerName;

    [Tooltip("Description")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Icone")]
    public Sprite icon;

    [Tooltip("Type de tour")]
    public TowerType towerType = TowerType.Arrow;

    #endregion

    #region Stats

    [Header("Stats de base")]
    [Tooltip("Portee de la tour")]
    public float range = 10f;

    [Tooltip("Degats par coup")]
    public float damage = 10f;

    [Tooltip("Tirs par seconde")]
    public float fireRate = 1f;

    [Tooltip("Type de degats")]
    public DamageType damageType = DamageType.Physical;

    #endregion

    #region Ciblage

    [Header("Ciblage")]
    [Tooltip("Mode de ciblage par defaut")]
    public TargetingMode defaultTargetingMode = TargetingMode.Closest;

    [Tooltip("Peut cibler les ennemis volants")]
    public bool canTargetFlying = true;

    [Tooltip("Peut cibler les ennemis au sol")]
    public bool canTargetGround = true;

    [Tooltip("Nombre maximum de cibles simultanees")]
    public int maxTargets = 1;

    #endregion

    #region Projectile

    [Header("Projectile")]
    [Tooltip("Prefab du projectile")]
    public GameObject projectilePrefab;

    [Tooltip("Vitesse du projectile")]
    public float projectileSpeed = 20f;

    [Tooltip("Le projectile suit sa cible?")]
    public bool isHoming = true;

    [Tooltip("Rayon d'explosion (0 = pas d'explosion)")]
    public float explosionRadius = 0f;

    #endregion

    #region Effets speciaux

    [Header("Effets speciaux")]
    [Tooltip("Ralentissement applique (0-1)")]
    [Range(0f, 1f)]
    public float slowAmount = 0f;

    [Tooltip("Duree du ralentissement")]
    public float slowDuration = 0f;

    [Tooltip("Degats sur la duree")]
    public float dotDamage = 0f;

    [Tooltip("Duree des degats sur la duree")]
    public float dotDuration = 0f;

    [Tooltip("Chance de critique (0-1)")]
    [Range(0f, 1f)]
    public float critChance = 0f;

    [Tooltip("Multiplicateur critique")]
    public float critMultiplier = 2f;

    #endregion

    #region Amelioration

    [Header("Amelioration")]
    [Tooltip("Niveau maximum")]
    public int maxLevel = 5;

    [Tooltip("Multiplicateur de degats par niveau")]
    public float damagePerLevel = 0.2f;

    [Tooltip("Multiplicateur de portee par niveau")]
    public float rangePerLevel = 0.1f;

    [Tooltip("Multiplicateur de cadence par niveau")]
    public float fireRatePerLevel = 0.15f;

    [Tooltip("Couts d'amelioration par niveau")]
    public ResourceCost[] upgradeCosts;

    #endregion

    #region Cout

    [Header("Cout")]
    [Tooltip("Cout de construction")]
    public ResourceCost[] buildCosts;

    [Tooltip("Temps de construction")]
    public float buildTime = 5f;

    #endregion

    #region Public Methods

    /// <summary>
    /// Calcule les degats pour un niveau.
    /// </summary>
    public float GetDamageAtLevel(int level)
    {
        return damage * (1f + damagePerLevel * (level - 1));
    }

    /// <summary>
    /// Calcule la portee pour un niveau.
    /// </summary>
    public float GetRangeAtLevel(int level)
    {
        return range * (1f + rangePerLevel * (level - 1));
    }

    /// <summary>
    /// Calcule la cadence pour un niveau.
    /// </summary>
    public float GetFireRateAtLevel(int level)
    {
        return fireRate * (1f + fireRatePerLevel * (level - 1));
    }

    /// <summary>
    /// Obtient le DPS theorique.
    /// </summary>
    public float GetDPS(int level = 1)
    {
        return GetDamageAtLevel(level) * GetFireRateAtLevel(level);
    }

    #endregion
}
