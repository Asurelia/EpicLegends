using UnityEngine;

/// <summary>
/// Donnees de configuration d'une vague d'ennemis.
/// </summary>
[CreateAssetMenu(fileName = "NewWave", menuName = "EpicLegends/Building/Wave Data")]
public class WaveData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("Numero de la vague")]
    public int waveNumber;

    [Tooltip("Nom de la vague")]
    public string waveName;

    [Tooltip("Description")]
    [TextArea(2, 4)]
    public string description;

    #endregion

    #region Ennemis

    [Header("Ennemis")]
    [Tooltip("Groupes d'ennemis dans cette vague")]
    public EnemyGroup[] enemyGroups;

    #endregion

    #region Timing

    [Header("Timing")]
    [Tooltip("Delai avant le debut de la vague")]
    public float startDelay = 5f;

    [Tooltip("Delai entre chaque spawn")]
    public float spawnInterval = 1f;

    [Tooltip("Delai apres la vague avant la suivante")]
    public float endDelay = 10f;

    #endregion

    #region Difficulte

    [Header("Difficulte")]
    [Tooltip("Multiplicateur de vie des ennemis")]
    public float healthMultiplier = 1f;

    [Tooltip("Multiplicateur de degats des ennemis")]
    public float damageMultiplier = 1f;

    [Tooltip("Multiplicateur de vitesse des ennemis")]
    public float speedMultiplier = 1f;

    [Tooltip("Multiplicateur de recompenses")]
    public float rewardMultiplier = 1f;

    #endregion

    #region Bonus

    [Header("Bonus")]
    [Tooltip("Recompenses pour avoir termine la vague")]
    public ResourceCost[] completionRewards;

    [Tooltip("Bonus de temps pour completion rapide")]
    public float bonusTimeLimit = 60f;

    [Tooltip("Recompenses bonus si complete dans le temps")]
    public ResourceCost[] bonusRewards;

    #endregion

    #region Evenements speciaux

    [Header("Evenements speciaux")]
    [Tooltip("Est une vague de boss?")]
    public bool isBossWave = false;

    [Tooltip("Prefab du boss (si vague de boss)")]
    public GameObject bossPrefab;

    [Tooltip("Activer un evenement special?")]
    public bool hasSpecialEvent = false;

    [Tooltip("Type d'evenement special")]
    public SpecialEventType specialEvent = SpecialEventType.None;

    #endregion

    #region Public Methods

    /// <summary>
    /// Calcule le nombre total d'ennemis.
    /// </summary>
    public int GetTotalEnemyCount()
    {
        int total = 0;

        if (enemyGroups != null)
        {
            foreach (var group in enemyGroups)
            {
                total += group.count;
            }
        }

        if (isBossWave && bossPrefab != null)
        {
            total++;
        }

        return total;
    }

    /// <summary>
    /// Calcule la duree estimee de la vague.
    /// </summary>
    public float GetEstimatedDuration()
    {
        int totalEnemies = GetTotalEnemyCount();
        return startDelay + (totalEnemies * spawnInterval) + endDelay;
    }

    /// <summary>
    /// Calcule la difficulte globale.
    /// </summary>
    public float GetDifficultyScore()
    {
        return healthMultiplier * damageMultiplier * speedMultiplier * GetTotalEnemyCount();
    }

    #endregion
}

/// <summary>
/// Groupe d'ennemis dans une vague.
/// </summary>
[System.Serializable]
public struct EnemyGroup
{
    [Tooltip("Prefab de l'ennemi")]
    public GameObject enemyPrefab;

    [Tooltip("Nombre d'ennemis")]
    public int count;

    [Tooltip("Delai avant ce groupe")]
    public float spawnDelay;

    [Tooltip("Intervalle entre chaque spawn du groupe")]
    public float spawnInterval;

    [Tooltip("Point de spawn specifique (null = aleatoire)")]
    public Transform spawnPoint;
}

/// <summary>
/// Types d'evenements speciaux.
/// </summary>
public enum SpecialEventType
{
    /// <summary>Pas d'evenement.</summary>
    None,

    /// <summary>Double recompenses.</summary>
    DoubleRewards,

    /// <summary>Ennemis invisibles.</summary>
    InvisibleEnemies,

    /// <summary>Ennemis rapides.</summary>
    FastEnemies,

    /// <summary>Ennemis resistant.</summary>
    ArmoredEnemies,

    /// <summary>Regeneration des ennemis.</summary>
    HealingEnemies,

    /// <summary>Ennemis explosifs.</summary>
    ExplosiveEnemies,

    /// <summary>Mode survie (pas de fin).</summary>
    Endless
}
