using UnityEngine;

/// <summary>
/// Configuration visuelle pour chaque tier de batiment.
/// </summary>
[CreateAssetMenu(fileName = "NewTierVisual", menuName = "EpicLegends/Building/Tier Visual Config")]
public class TierVisualConfig : ScriptableObject
{
    [Header("Materiaux par Tier")]
    [Tooltip("Materiau pour le tier Bois")]
    public Material woodMaterial;

    [Tooltip("Materiau pour le tier Pierre")]
    public Material stoneMaterial;

    [Tooltip("Materiau pour le tier Metal")]
    public Material metalMaterial;

    [Tooltip("Materiau pour le tier Tech")]
    public Material techMaterial;

    [Header("Couleurs par Tier")]
    public Color woodColor = new Color(0.6f, 0.4f, 0.2f);
    public Color stoneColor = Color.gray;
    public Color metalColor = new Color(0.7f, 0.7f, 0.8f);
    public Color techColor = Color.cyan;

    [Header("Effets de Particules")]
    public GameObject woodParticles;
    public GameObject stoneParticles;
    public GameObject metalParticles;
    public GameObject techParticles;

    [Header("Multiplicateurs de Stats")]
    public TierStats woodStats = new TierStats(1f, 0f);
    public TierStats stoneStats = new TierStats(1.5f, 10f);
    public TierStats metalStats = new TierStats(2.5f, 25f);
    public TierStats techStats = new TierStats(4f, 50f);

    /// <summary>
    /// Obtient le materiau pour un tier.
    /// </summary>
    public Material GetMaterial(BuildingTier tier)
    {
        return tier switch
        {
            BuildingTier.Wood => woodMaterial,
            BuildingTier.Stone => stoneMaterial,
            BuildingTier.Metal => metalMaterial,
            BuildingTier.Tech => techMaterial,
            _ => null
        };
    }

    /// <summary>
    /// Obtient la couleur pour un tier.
    /// </summary>
    public Color GetColor(BuildingTier tier)
    {
        return tier switch
        {
            BuildingTier.Wood => woodColor,
            BuildingTier.Stone => stoneColor,
            BuildingTier.Metal => metalColor,
            BuildingTier.Tech => techColor,
            _ => Color.white
        };
    }

    /// <summary>
    /// Obtient les particules pour un tier.
    /// </summary>
    public GameObject GetParticles(BuildingTier tier)
    {
        return tier switch
        {
            BuildingTier.Wood => woodParticles,
            BuildingTier.Stone => stoneParticles,
            BuildingTier.Metal => metalParticles,
            BuildingTier.Tech => techParticles,
            _ => null
        };
    }

    /// <summary>
    /// Obtient les stats pour un tier.
    /// </summary>
    public TierStats GetStats(BuildingTier tier)
    {
        return tier switch
        {
            BuildingTier.Wood => woodStats,
            BuildingTier.Stone => stoneStats,
            BuildingTier.Metal => metalStats,
            BuildingTier.Tech => techStats,
            _ => new TierStats(1f, 0f)
        };
    }
}

/// <summary>
/// Stats par tier.
/// </summary>
[System.Serializable]
public struct TierStats
{
    [Tooltip("Multiplicateur de vie")]
    public float healthMultiplier;

    [Tooltip("Bonus de defense")]
    public float defenseBonus;

    public TierStats(float healthMult, float defBonus)
    {
        healthMultiplier = healthMult;
        defenseBonus = defBonus;
    }
}
