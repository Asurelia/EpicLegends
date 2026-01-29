using UnityEngine;

/// <summary>
/// Utilitaire statique pour calculer les reactions elementaires.
/// </summary>
public static class ElementalReactionCalculator
{
    #region Reaction Lookup

    /// <summary>
    /// Determine la reaction entre deux elements.
    /// </summary>
    /// <param name="trigger">Element qui declenche la reaction</param>
    /// <param name="target">Element deja present sur la cible</param>
    /// <returns>Type de reaction</returns>
    public static ElementalReactionType GetReaction(ElementType trigger, ElementType target)
    {
        // Meme element = pas de reaction
        if (trigger == target) return ElementalReactionType.None;

        // Fire reactions
        if (trigger == ElementType.Fire)
        {
            if (target == ElementType.Water) return ElementalReactionType.Vaporize;
            if (target == ElementType.Ice) return ElementalReactionType.Melt;
            if (target == ElementType.Electric) return ElementalReactionType.Overload;
        }

        // Water reactions
        if (trigger == ElementType.Water)
        {
            if (target == ElementType.Fire) return ElementalReactionType.Vaporize;
            if (target == ElementType.Ice) return ElementalReactionType.Frozen;
            if (target == ElementType.Electric) return ElementalReactionType.ElectroCharged;
        }

        // Ice reactions
        if (trigger == ElementType.Ice)
        {
            if (target == ElementType.Fire) return ElementalReactionType.Melt;
            if (target == ElementType.Water) return ElementalReactionType.Frozen;
            if (target == ElementType.Electric) return ElementalReactionType.Superconduct;
        }

        // Electric reactions
        if (trigger == ElementType.Electric)
        {
            if (target == ElementType.Fire) return ElementalReactionType.Overload;
            if (target == ElementType.Water) return ElementalReactionType.ElectroCharged;
            if (target == ElementType.Ice) return ElementalReactionType.Superconduct;
        }

        // Wind reactions (Swirl avec les 4 elements de base)
        if (trigger == ElementType.Wind)
        {
            if (target == ElementType.Fire || target == ElementType.Water ||
                target == ElementType.Ice || target == ElementType.Electric)
            {
                return ElementalReactionType.Swirl;
            }
        }

        // Earth reactions (Crystallize avec les 4 elements de base)
        if (trigger == ElementType.Earth)
        {
            if (target == ElementType.Fire || target == ElementType.Water ||
                target == ElementType.Ice || target == ElementType.Electric)
            {
                return ElementalReactionType.Crystallize;
            }
        }

        // Light/Dark reactions
        if (trigger == ElementType.Light && target == ElementType.Dark)
            return ElementalReactionType.Radiance;

        if (trigger == ElementType.Dark && target == ElementType.Light)
            return ElementalReactionType.Eclipse;

        return ElementalReactionType.None;
    }

    #endregion

    #region Reaction Multipliers

    /// <summary>
    /// Retourne le multiplicateur de degats pour une reaction.
    /// </summary>
    public static float GetReactionMultiplier(ElementalReactionType reaction)
    {
        return reaction switch
        {
            ElementalReactionType.Vaporize => 2f,
            ElementalReactionType.Melt => 2f,
            ElementalReactionType.Overload => 1.5f,
            ElementalReactionType.Superconduct => 1.5f,
            ElementalReactionType.ElectroCharged => 1.2f,
            ElementalReactionType.Frozen => 1f, // Pas de bonus de degats, mais immobilise
            ElementalReactionType.Swirl => 1.25f,
            ElementalReactionType.Crystallize => 1f, // Pas de bonus de degats, cree bouclier
            ElementalReactionType.Radiance => 2.5f,
            ElementalReactionType.Eclipse => 2.5f,
            _ => 1f
        };
    }

    /// <summary>
    /// Retourne la duree de l'effet de la reaction.
    /// </summary>
    public static float GetReactionDuration(ElementalReactionType reaction)
    {
        return reaction switch
        {
            ElementalReactionType.Frozen => 3f,
            ElementalReactionType.ElectroCharged => 2f,
            ElementalReactionType.Superconduct => 5f, // Reduction de defense
            ElementalReactionType.Crystallize => 10f, // Duree du bouclier
            _ => 0f
        };
    }

    /// <summary>
    /// Determine si la reaction cause des degats AoE.
    /// </summary>
    public static bool IsAoEReaction(ElementalReactionType reaction)
    {
        return reaction switch
        {
            ElementalReactionType.Overload => true,
            ElementalReactionType.Swirl => true,
            ElementalReactionType.Radiance => true,
            ElementalReactionType.Eclipse => true,
            _ => false
        };
    }

    /// <summary>
    /// Retourne le rayon de l'effet AoE.
    /// </summary>
    public static float GetAoERadius(ElementalReactionType reaction)
    {
        return reaction switch
        {
            ElementalReactionType.Overload => 4f,
            ElementalReactionType.Swirl => 6f,
            ElementalReactionType.Radiance => 5f,
            ElementalReactionType.Eclipse => 5f,
            _ => 0f
        };
    }

    /// <summary>
    /// Determine si la reaction consomme l'element de la cible.
    /// </summary>
    public static bool ConsumesElement(ElementalReactionType reaction)
    {
        return reaction switch
        {
            ElementalReactionType.Vaporize => true,
            ElementalReactionType.Melt => true,
            ElementalReactionType.Overload => true,
            ElementalReactionType.Superconduct => true,
            ElementalReactionType.Radiance => true,
            ElementalReactionType.Eclipse => true,
            _ => false
        };
    }

    /// <summary>
    /// Retourne l'element propage par Swirl.
    /// </summary>
    public static ElementType? GetSwirlElement(ElementType swirlTarget)
    {
        // Swirl propage Fire, Water, Ice, Electric
        if (swirlTarget == ElementType.Fire ||
            swirlTarget == ElementType.Water ||
            swirlTarget == ElementType.Ice ||
            swirlTarget == ElementType.Electric)
        {
            return swirlTarget;
        }
        return null;
    }

    #endregion

    #region Damage Calculation

    /// <summary>
    /// Calcule les degats finaux avec la reaction elementaire.
    /// </summary>
    public static float CalculateReactionDamage(
        float baseDamage,
        ElementalReactionType reaction,
        float elementalMastery = 0f)
    {
        float multiplier = GetReactionMultiplier(reaction);

        // Bonus de maitrise elementaire
        float masteryBonus = 1f + (elementalMastery / 100f) * 0.5f;

        return baseDamage * multiplier * masteryBonus;
    }

    /// <summary>
    /// Calcule les degats de DoT pour ElectroCharged.
    /// </summary>
    public static float CalculateElectroChargedDamage(float baseDamage, float tickRate = 0.5f)
    {
        // ElectroCharged fait des degats par tick
        return baseDamage * 0.3f * tickRate;
    }

    #endregion
}
