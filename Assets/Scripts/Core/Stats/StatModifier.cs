using System;

/// <summary>
/// Type de modification appliquée à une statistique.
/// L'ordre d'application est: Flat -> PercentAdd -> PercentMult
/// </summary>
public enum ModifierType
{
    /// <summary>
    /// Ajoute une valeur fixe à la stat de base.
    /// Ex: +10 Force
    /// </summary>
    Flat = 100,

    /// <summary>
    /// Ajoute un pourcentage de la stat de base.
    /// Les bonus PercentAdd sont additionnés avant d'être appliqués.
    /// Ex: +20% Force (deux bonus de 20% = +40%)
    /// </summary>
    PercentAdd = 200,

    /// <summary>
    /// Multiplie la stat finale par un pourcentage.
    /// Chaque bonus PercentMult est appliqué séparément.
    /// Ex: +50% Force multiplicatif
    /// </summary>
    PercentMult = 300
}

/// <summary>
/// Représente une modification temporaire ou permanente d'une statistique.
/// </summary>
[Serializable]
public struct StatModifier : IEquatable<StatModifier>
{
    /// <summary>
    /// Valeur de la modification.
    /// Pour Flat: valeur absolue
    /// Pour PercentAdd/PercentMult: 0.1 = 10%
    /// </summary>
    public float Value;

    /// <summary>
    /// Type de modification (Flat, PercentAdd, PercentMult).
    /// </summary>
    public ModifierType Type;

    /// <summary>
    /// Ordre d'application au sein du même type.
    /// Les valeurs plus basses sont appliquées en premier.
    /// </summary>
    public int Order;

    /// <summary>
    /// Source de la modification (pour identification et suppression).
    /// </summary>
    public object Source;

    /// <summary>
    /// Crée un modificateur de stat.
    /// </summary>
    public StatModifier(float value, ModifierType type, int order = 0, object source = null)
    {
        Value = value;
        Type = type;
        Order = order;
        Source = source;
    }

    /// <summary>
    /// Crée un modificateur flat.
    /// </summary>
    public static StatModifier Flat(float value, object source = null)
    {
        return new StatModifier(value, ModifierType.Flat, 0, source);
    }

    /// <summary>
    /// Crée un modificateur pourcentage additif.
    /// </summary>
    /// <param name="percent">Pourcentage (ex: 0.2 pour 20%)</param>
    public static StatModifier PercentAdd(float percent, object source = null)
    {
        return new StatModifier(percent, ModifierType.PercentAdd, 0, source);
    }

    /// <summary>
    /// Crée un modificateur pourcentage multiplicatif.
    /// </summary>
    /// <param name="percent">Pourcentage (ex: 0.5 pour 50%)</param>
    public static StatModifier PercentMult(float percent, object source = null)
    {
        return new StatModifier(percent, ModifierType.PercentMult, 0, source);
    }

    public bool Equals(StatModifier other)
    {
        return Value.Equals(other.Value) &&
               Type == other.Type &&
               Order == other.Order &&
               Equals(Source, other.Source);
    }

    public override bool Equals(object obj)
    {
        return obj is StatModifier other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value, (int)Type, Order, Source);
    }

    public override string ToString()
    {
        string sign = Value >= 0 ? "+" : "";
        return Type switch
        {
            ModifierType.Flat => $"{sign}{Value:F0}",
            ModifierType.PercentAdd => $"{sign}{Value * 100:F0}%",
            ModifierType.PercentMult => $"x{1 + Value:F2}",
            _ => Value.ToString()
        };
    }
}
