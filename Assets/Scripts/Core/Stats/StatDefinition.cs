using UnityEngine;

/// <summary>
/// Définition d'une statistique avec ses valeurs par défaut et formules.
/// ScriptableObject pour configuration dans l'éditeur.
/// </summary>
[CreateAssetMenu(fileName = "NewStat", menuName = "EpicLegends/Stats/Stat Definition")]
public class StatDefinition : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Type de la statistique")]
    public StatType statType;

    [Tooltip("Nom affiché dans le jeu")]
    public string displayName;

    [Tooltip("Description de la statistique")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Icône de la statistique")]
    public Sprite icon;

    [Header("Valeurs")]
    [Tooltip("Valeur de base par défaut")]
    public float baseValue = 10f;

    [Tooltip("Valeur minimum")]
    public float minValue = 0f;

    [Tooltip("Valeur maximum (-1 = pas de limite)")]
    public float maxValue = -1f;

    [Header("Affichage")]
    [Tooltip("Afficher comme pourcentage")]
    public bool showAsPercent = false;

    [Tooltip("Nombre de décimales à afficher")]
    [Range(0, 3)]
    public int decimalPlaces = 0;

    [Header("Croissance par niveau")]
    [Tooltip("Valeur ajoutée par niveau")]
    public float growthPerLevel = 1f;

    [Tooltip("Multiplicateur de croissance exponentielle")]
    public float growthMultiplier = 1.0f;

    /// <summary>
    /// Calcule la valeur de la stat pour un niveau donné.
    /// </summary>
    public float CalculateValueForLevel(int level)
    {
        if (level <= 1) return baseValue;

        // Formule: base + (level-1) * growth * multiplier^(level-1)
        float levelBonus = (level - 1) * growthPerLevel;
        float multiplier = Mathf.Pow(growthMultiplier, level - 1);
        return baseValue + levelBonus * multiplier;
    }

    /// <summary>
    /// Applique les limites min/max à une valeur.
    /// </summary>
    public float ClampValue(float value)
    {
        if (value < minValue) return minValue;
        if (maxValue >= 0 && value > maxValue) return maxValue;
        return value;
    }

    /// <summary>
    /// Formate la valeur pour l'affichage.
    /// </summary>
    public string FormatValue(float value)
    {
        string format = "F" + decimalPlaces;
        if (showAsPercent)
        {
            return (value * 100).ToString(format) + "%";
        }
        return value.ToString(format);
    }
}
