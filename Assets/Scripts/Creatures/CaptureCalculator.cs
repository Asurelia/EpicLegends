using UnityEngine;

/// <summary>
/// Calculateur de taux de capture.
/// Formule inspiree de Pokemon avec modifications.
/// </summary>
public static class CaptureCalculator
{
    #region Constants

    /// <summary>Taux de capture minimum</summary>
    public const float MIN_CAPTURE_RATE = 0.01f;

    /// <summary>Taux de capture maximum</summary>
    public const float MAX_CAPTURE_RATE = 1f;

    /// <summary>Bonus pour vie critique (< 25%)</summary>
    public const float CRITICAL_HEALTH_BONUS = 2f;

    /// <summary>Bonus pour vie basse (< 50%)</summary>
    public const float LOW_HEALTH_BONUS = 1.5f;

    /// <summary>Reduction par niveau (par rapport au niveau max)</summary>
    public const float LEVEL_PENALTY_FACTOR = 0.5f;

    #endregion

    #region Public Methods

    /// <summary>
    /// Calcule le taux de capture pour une creature.
    /// </summary>
    /// <param name="target">Creature cible</param>
    /// <param name="captureItem">Item utilise (peut etre null)</param>
    /// <param name="playerLevelBonus">Bonus du niveau joueur (1.0 = neutre)</param>
    /// <returns>Taux de capture entre 0 et 1</returns>
    public static float CalculateCaptureRate(
        CreatureInstance target,
        CaptureItemData captureItem,
        float playerLevelBonus = 1f)
    {
        if (target == null) return 0f;

        // Taux de base de la creature
        float baseRate = target.Data.baseCaptureRate;

        // Multiplicateur de vie (plus la creature est blessee, plus facile)
        float healthMultiplier = GetHealthMultiplier(target.HealthPercent);

        // Multiplicateur de niveau (plus haut niveau = plus difficile)
        float levelMultiplier = GetLevelMultiplier(target.Level, target.Data.maxLevel);

        // Multiplicateur de rarete (plus rare = plus difficile)
        float rarityMultiplier = GetRarityMultiplier(target.Data.rarity);

        // Multiplicateur de l'item
        float itemMultiplier = 1f;
        float itemBonus = 0f;
        if (captureItem != null)
        {
            itemMultiplier = captureItem.GetTotalMultiplier(target);
            itemBonus = captureItem.captureRateBonus;
        }

        // Formule finale
        float captureRate = baseRate * healthMultiplier * levelMultiplier * rarityMultiplier
                           * itemMultiplier * playerLevelBonus + itemBonus;

        // Clamper entre min et max
        return Mathf.Clamp(captureRate, MIN_CAPTURE_RATE, MAX_CAPTURE_RATE);
    }

    /// <summary>
    /// Tente une capture et retourne si elle reussit.
    /// </summary>
    public static bool AttemptCapture(
        CreatureInstance target,
        CaptureItemData captureItem,
        float playerLevelBonus = 1f)
    {
        // Capture garantie
        if (captureItem != null && captureItem.guaranteedCapture)
        {
            return true;
        }

        float captureRate = CalculateCaptureRate(target, captureItem, playerLevelBonus);
        float roll = Random.Range(0f, 1f);

        return roll <= captureRate;
    }

    /// <summary>
    /// Effectue plusieurs oscillations pour determiner la capture.
    /// Style Pokemon avec 3 oscillations.
    /// </summary>
    public static CaptureResult AttemptCaptureWithOscillations(
        CreatureInstance target,
        CaptureItemData captureItem,
        float playerLevelBonus = 1f)
    {
        if (captureItem != null && captureItem.guaranteedCapture)
        {
            return new CaptureResult
            {
                success = true,
                oscillations = captureItem.oscillationCount,
                captureRate = 1f
            };
        }

        float captureRate = CalculateCaptureRate(target, captureItem, playerLevelBonus);
        int oscillationCount = captureItem?.oscillationCount ?? 3;

        // Taux par oscillation (racine n-ieme du taux total)
        float oscillationRate = Mathf.Pow(captureRate, 1f / oscillationCount);

        int successfulOscillations = 0;
        for (int i = 0; i < oscillationCount; i++)
        {
            float roll = Random.Range(0f, 1f);
            if (roll <= oscillationRate)
            {
                successfulOscillations++;
            }
            else
            {
                break; // Echec, la creature s'echappe
            }
        }

        return new CaptureResult
        {
            success = successfulOscillations == oscillationCount,
            oscillations = successfulOscillations,
            captureRate = captureRate
        };
    }

    /// <summary>
    /// Calcule l'experience bonus pour affaiblir une creature avant capture.
    /// Plus la creature est affaiblie, plus le bonus est eleve.
    /// </summary>
    public static float CalculateWeakeningBonus(CreatureInstance target)
    {
        if (target == null) return 0f;

        // Bonus base sur la vie manquante (0% vie = 100% bonus, 100% vie = 0% bonus)
        float missingHealthPercent = 1f - target.HealthPercent;

        // Bonus de 0 a 50% d'XP supplementaire
        return missingHealthPercent * 0.5f;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Calcule le multiplicateur base sur la vie restante.
    /// </summary>
    private static float GetHealthMultiplier(float healthPercent)
    {
        if (healthPercent <= 0.1f) return 2.5f; // Presque mort
        if (healthPercent <= 0.25f) return CRITICAL_HEALTH_BONUS; // Critique
        if (healthPercent <= 0.5f) return LOW_HEALTH_BONUS; // Basse
        if (healthPercent <= 0.75f) return 1.25f; // Moyenne
        return 1f; // Pleine vie
    }

    /// <summary>
    /// Calcule le multiplicateur base sur le niveau.
    /// </summary>
    private static float GetLevelMultiplier(int level, int maxLevel)
    {
        // Plus le niveau est eleve par rapport au max, plus c'est difficile
        float levelRatio = (float)level / maxLevel;

        // Multiplicateur de 1.0 (niveau 1) a 0.5 (niveau max)
        return 1f - (levelRatio * LEVEL_PENALTY_FACTOR);
    }

    /// <summary>
    /// Calcule le multiplicateur base sur la rarete.
    /// </summary>
    private static float GetRarityMultiplier(CreatureRarity rarity)
    {
        return rarity switch
        {
            CreatureRarity.Common => 1f,
            CreatureRarity.Uncommon => 0.8f,
            CreatureRarity.Rare => 0.5f,
            CreatureRarity.Epic => 0.3f,
            CreatureRarity.Legendary => 0.1f,
            CreatureRarity.Mythic => 0.05f,
            _ => 1f
        };
    }

    #endregion
}

/// <summary>
/// Resultat d'une tentative de capture.
/// </summary>
public struct CaptureResult
{
    /// <summary>La capture a-t-elle reussi?</summary>
    public bool success;

    /// <summary>Nombre d'oscillations avant echec/succes</summary>
    public int oscillations;

    /// <summary>Taux de capture calcule</summary>
    public float captureRate;
}
