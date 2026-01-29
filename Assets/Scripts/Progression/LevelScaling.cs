using UnityEngine;

/// <summary>
/// Utilitaires pour le scaling de niveau des ennemis et recompenses.
/// </summary>
public static class LevelScaling
{
    #region Constants

    /// <summary>Difference de niveau maximale pour le scaling XP.</summary>
    private const int MAX_LEVEL_DIFF = 10;

    /// <summary>Penalite XP par niveau de difference (si joueur plus fort).</summary>
    private const float XP_PENALTY_PER_LEVEL = 0.1f;

    /// <summary>Bonus XP par niveau de difference (si ennemi plus fort).</summary>
    private const float XP_BONUS_PER_LEVEL = 0.05f;

    /// <summary>Minimum XP meme pour ennemis faibles.</summary>
    private const float MIN_XP_MULTIPLIER = 0.1f;

    /// <summary>Maximum bonus XP pour ennemis forts.</summary>
    private const float MAX_XP_MULTIPLIER = 1.5f;

    #endregion

    #region Enemy Level Scaling

    /// <summary>
    /// Calcule le niveau d'un ennemi avec scaling.
    /// </summary>
    /// <param name="playerLevel">Niveau du joueur.</param>
    /// <param name="minLevel">Niveau minimum de l'ennemi.</param>
    /// <param name="maxLevel">Niveau maximum de l'ennemi.</param>
    /// <param name="scalingFactor">Facteur de scaling (0-1).</param>
    /// <returns>Niveau de l'ennemi.</returns>
    public static int GetScaledEnemyLevel(int playerLevel, int minLevel, int maxLevel, float scalingFactor = 0.5f)
    {
        // Niveau de base = moyenne entre min et max
        int baseLevel = (minLevel + maxLevel) / 2;

        // Appliquer le scaling vers le joueur
        int scaledLevel = Mathf.RoundToInt(Mathf.Lerp(baseLevel, playerLevel, scalingFactor));

        // Clamper entre min et max
        return Mathf.Clamp(scaledLevel, minLevel, maxLevel);
    }

    /// <summary>
    /// Calcule le multiplicateur de stats pour un ennemi scale.
    /// </summary>
    /// <param name="baseLevel">Niveau de base de l'ennemi.</param>
    /// <param name="scaledLevel">Niveau apres scaling.</param>
    /// <returns>Multiplicateur de stats.</returns>
    public static float GetStatMultiplier(int baseLevel, int scaledLevel)
    {
        if (baseLevel <= 0) return 1f;

        float levelRatio = (float)scaledLevel / baseLevel;
        return Mathf.Clamp(levelRatio, 0.5f, 2f);
    }

    #endregion

    #region XP Scaling

    /// <summary>
    /// Calcule l'XP avec scaling en fonction de la difference de niveau.
    /// </summary>
    /// <param name="baseXP">XP de base de l'ennemi.</param>
    /// <param name="playerLevel">Niveau du joueur.</param>
    /// <param name="enemyLevel">Niveau de l'ennemi.</param>
    /// <returns>XP ajustee.</returns>
    public static int GetScaledXPReward(int baseXP, int playerLevel, int enemyLevel)
    {
        int levelDiff = playerLevel - enemyLevel;
        float multiplier;

        if (levelDiff > 0)
        {
            // Joueur plus fort = penalite
            float penalty = Mathf.Min(levelDiff * XP_PENALTY_PER_LEVEL, 1f - MIN_XP_MULTIPLIER);
            multiplier = Mathf.Max(1f - penalty, MIN_XP_MULTIPLIER);
        }
        else if (levelDiff < 0)
        {
            // Ennemi plus fort = bonus
            float bonus = Mathf.Min(-levelDiff * XP_BONUS_PER_LEVEL, MAX_XP_MULTIPLIER - 1f);
            multiplier = Mathf.Min(1f + bonus, MAX_XP_MULTIPLIER);
        }
        else
        {
            // Meme niveau = XP de base
            multiplier = 1f;
        }

        return Mathf.RoundToInt(baseXP * multiplier);
    }

    /// <summary>
    /// Calcule si l'XP devrait etre nulle (ennemi trop faible).
    /// </summary>
    /// <param name="playerLevel">Niveau du joueur.</param>
    /// <param name="enemyLevel">Niveau de l'ennemi.</param>
    /// <returns>True si aucune XP ne devrait etre accordee.</returns>
    public static bool ShouldGrantNoXP(int playerLevel, int enemyLevel)
    {
        return playerLevel - enemyLevel > MAX_LEVEL_DIFF;
    }

    #endregion

    #region Damage Scaling

    /// <summary>
    /// Calcule le multiplicateur de degats en fonction de la difference de niveau.
    /// </summary>
    /// <param name="attackerLevel">Niveau de l'attaquant.</param>
    /// <param name="defenderLevel">Niveau du defenseur.</param>
    /// <returns>Multiplicateur de degats.</returns>
    public static float GetDamageMultiplier(int attackerLevel, int defenderLevel)
    {
        int levelDiff = attackerLevel - defenderLevel;

        // 5% de bonus/malus par niveau de difference
        float modifier = levelDiff * 0.05f;

        // Clamper entre 0.5x et 2x
        return Mathf.Clamp(1f + modifier, 0.5f, 2f);
    }

    /// <summary>
    /// Calcule la reduction de degats pour les ennemis de haut niveau.
    /// </summary>
    /// <param name="playerLevel">Niveau du joueur.</param>
    /// <param name="enemyLevel">Niveau de l'ennemi.</param>
    /// <returns>Multiplicateur de reduction (0-1).</returns>
    public static float GetDamageReduction(int playerLevel, int enemyLevel)
    {
        if (enemyLevel <= playerLevel) return 1f;

        int levelDiff = enemyLevel - playerLevel;

        // 10% de reduction par niveau de difference
        float reduction = levelDiff * 0.1f;

        // Minimum 20% des degats
        return Mathf.Max(1f - reduction, 0.2f);
    }

    #endregion

    #region Drop Rate Scaling

    /// <summary>
    /// Calcule le multiplicateur de drop rate.
    /// </summary>
    /// <param name="playerLevel">Niveau du joueur.</param>
    /// <param name="enemyLevel">Niveau de l'ennemi.</param>
    /// <returns>Multiplicateur de drop rate.</returns>
    public static float GetDropRateMultiplier(int playerLevel, int enemyLevel)
    {
        int levelDiff = playerLevel - enemyLevel;

        if (levelDiff > MAX_LEVEL_DIFF)
        {
            // Pas de drops pour ennemis tres faibles
            return 0f;
        }
        else if (levelDiff > 5)
        {
            // Drop rate reduit
            return 0.5f;
        }
        else if (levelDiff < -5)
        {
            // Bonus pour ennemis plus forts
            return 1.25f;
        }

        return 1f;
    }

    #endregion

    #region Zone Scaling

    /// <summary>
    /// Calcule le niveau recommande pour une zone.
    /// </summary>
    /// <param name="zoneBaseLevel">Niveau de base de la zone.</param>
    /// <param name="difficulty">Difficulte (0=facile, 1=normal, 2=difficile).</param>
    /// <returns>Niveau recommande.</returns>
    public static int GetRecommendedLevel(int zoneBaseLevel, int difficulty = 1)
    {
        int modifier = (difficulty - 1) * 5;
        return Mathf.Max(1, zoneBaseLevel + modifier);
    }

    /// <summary>
    /// Determine si une zone est appropriee pour le niveau du joueur.
    /// </summary>
    /// <param name="playerLevel">Niveau du joueur.</param>
    /// <param name="zoneLevel">Niveau de la zone.</param>
    /// <returns>Classification de difficulte.</returns>
    public static ZoneDifficultyRating GetZoneRating(int playerLevel, int zoneLevel)
    {
        int diff = playerLevel - zoneLevel;

        if (diff >= 10) return ZoneDifficultyRating.Trivial;
        if (diff >= 5) return ZoneDifficultyRating.Easy;
        if (diff >= -2) return ZoneDifficultyRating.Normal;
        if (diff >= -5) return ZoneDifficultyRating.Challenging;
        if (diff >= -10) return ZoneDifficultyRating.Hard;
        return ZoneDifficultyRating.Deadly;
    }

    #endregion
}

/// <summary>
/// Classification de difficulte d'une zone.
/// </summary>
public enum ZoneDifficultyRating
{
    /// <summary>Zone triviale, pas de defi.</summary>
    Trivial,

    /// <summary>Zone facile.</summary>
    Easy,

    /// <summary>Zone normale, difficulte appropriee.</summary>
    Normal,

    /// <summary>Zone stimulante.</summary>
    Challenging,

    /// <summary>Zone difficile.</summary>
    Hard,

    /// <summary>Zone mortelle, danger extreme.</summary>
    Deadly
}
