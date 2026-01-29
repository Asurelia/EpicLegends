using UnityEngine;

/// <summary>
/// Donnees de configuration d'un ennemi.
/// </summary>
[CreateAssetMenu(fileName = "NewEnemy", menuName = "EpicLegends/Enemies/Enemy Data")]
public class EnemyData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("Nom de l'ennemi")]
    public string enemyName;

    [Tooltip("Description")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Type d'ennemi")]
    public EnemyType enemyType = EnemyType.Basic;

    [Tooltip("Comportement de base")]
    public AIBehavior baseBehavior = AIBehavior.Aggressive;

    [Tooltip("Prefab de l'ennemi")]
    public GameObject prefab;

    #endregion

    #region Stats de Base

    [Header("Stats")]
    [Tooltip("Points de vie maximum")]
    public float maxHealth = 100f;

    [Tooltip("Defense")]
    public float defense = 10f;

    [Tooltip("Degats d'attaque")]
    public float attackDamage = 20f;

    [Tooltip("Vitesse de deplacement")]
    public float moveSpeed = 3.5f;

    [Tooltip("Vitesse de poursuite")]
    public float chaseSpeed = 5f;

    #endregion

    #region Detection

    [Header("Detection")]
    [Tooltip("Portee de detection")]
    public float detectionRange = 10f;

    [Tooltip("Portee d'attaque")]
    public float attackRange = 2f;

    [Tooltip("Champ de vision en degres")]
    public float fieldOfView = 120f;

    [Tooltip("Distance de fuite (pour Cowardly)")]
    public float fleeDistance = 15f;

    #endregion

    #region Combat

    [Header("Combat")]
    [Tooltip("Cooldown entre attaques")]
    public float attackCooldown = 1.5f;

    [Tooltip("Type de degats")]
    public DamageType damageType = DamageType.Physical;

    [Tooltip("Valeur de stagger")]
    public float staggerValue = 10f;

    [Tooltip("Force de knockback")]
    public float knockbackForce = 3f;

    [Tooltip("Patterns d'attaque")]
    public AttackPattern[] attackPatterns;

    #endregion

    #region Resistances

    [Header("Resistances")]
    [Tooltip("Resistance physique")]
    [Range(-1f, 1f)]
    public float physicalResistance = 0f;

    [Tooltip("Resistance au feu")]
    [Range(-1f, 1f)]
    public float fireResistance = 0f;

    [Tooltip("Resistance a l'eau")]
    [Range(-1f, 1f)]
    public float waterResistance = 0f;

    [Tooltip("Resistance a la glace")]
    [Range(-1f, 1f)]
    public float iceResistance = 0f;

    [Tooltip("Resistance electrique")]
    [Range(-1f, 1f)]
    public float electricResistance = 0f;

    #endregion

    #region Loot

    [Header("Loot")]
    [Tooltip("Experience donnee")]
    public int experienceReward = 50;

    [Tooltip("Or minimum")]
    public int goldMin = 10;

    [Tooltip("Or maximum")]
    public int goldMax = 30;

    [Tooltip("Table de loot")]
    public LootTable lootTable;

    #endregion

    #region Audio/Visual

    [Header("Audio/Visual")]
    [Tooltip("Sons d'attaque")]
    public AudioClip[] attackSounds;

    [Tooltip("Sons de degats")]
    public AudioClip[] hitSounds;

    [Tooltip("Son de mort")]
    public AudioClip deathSound;

    #endregion

    #region Public Methods

    /// <summary>
    /// Cree une DamageInfo pour cet ennemi.
    /// </summary>
    public DamageInfo CreateDamageInfo(GameObject attacker, Vector3 hitPoint)
    {
        return new DamageInfo
        {
            baseDamage = attackDamage,
            damageType = damageType,
            attacker = attacker,
            hitPoint = hitPoint,
            knockbackForce = knockbackForce,
            staggerValue = staggerValue
        };
    }

    /// <summary>
    /// Obtient le multiplicateur selon le type.
    /// </summary>
    public float GetTypeMultiplier()
    {
        return enemyType switch
        {
            EnemyType.Basic => 1f,
            EnemyType.Elite => 1.5f,
            EnemyType.MiniBoss => 2f,
            EnemyType.Boss => 3f,
            _ => 1f
        };
    }

    #endregion
}

/// <summary>
/// Reference a une table de loot.
/// Definit les items pouvant etre obtenus et leurs probabilites.
/// </summary>
[System.Serializable]
public class LootTable
{
    [Tooltip("Nom de la table de loot")]
    public string tableName;

    [Tooltip("Entrees de la table de loot")]
    public LootTableEntry[] entries;
}

/// <summary>
/// Entree dans une table de loot.
/// </summary>
[System.Serializable]
public struct LootTableEntry
{
    [Tooltip("Item a obtenir")]
    public ItemData item;

    [Tooltip("Chance de drop (0-1)")]
    [Range(0f, 1f)]
    public float dropChance;

    [Tooltip("Quantite minimum")]
    [Min(1)]
    public int minAmount;

    [Tooltip("Quantite maximum")]
    [Min(1)]
    public int maxAmount;
}
