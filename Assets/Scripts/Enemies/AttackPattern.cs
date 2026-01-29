using UnityEngine;

/// <summary>
/// Pattern d'attaque pour un ennemi.
/// Definit une sequence d'attaques et les conditions d'utilisation.
/// </summary>
[CreateAssetMenu(fileName = "NewAttackPattern", menuName = "EpicLegends/Enemies/Attack Pattern")]
public class AttackPattern : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Nom du pattern")]
    public string patternName;

    [Tooltip("Description")]
    [TextArea(2, 3)]
    public string description;

    [Header("Conditions")]
    [Tooltip("Seuil de vie minimum pour utiliser (0-1)")]
    [Range(0f, 1f)]
    public float healthThresholdMin = 0f;

    [Tooltip("Seuil de vie maximum pour utiliser (0-1)")]
    [Range(0f, 1f)]
    public float healthThresholdMax = 1f;

    [Tooltip("Distance minimum a la cible")]
    public float minDistance = 0f;

    [Tooltip("Distance maximum a la cible")]
    public float maxDistance = 10f;

    [Tooltip("Priorite (plus haut = plus prioritaire)")]
    public int priority = 0;

    [Tooltip("Poids pour selection aleatoire")]
    public float weight = 1f;

    [Header("Attaques")]
    [Tooltip("Liste des attaques dans l'ordre")]
    public AttackData[] attacks;

    [Tooltip("Delai entre chaque attaque")]
    public float attackInterval = 0.5f;

    [Header("Cooldown")]
    [Tooltip("Temps de recharge apres utilisation")]
    public float cooldown = 5f;

    [Tooltip("Peut etre interrompu?")]
    public bool canBeInterrupted = true;

    [Header("Animation")]
    [Tooltip("Trigger d'animation de preparation")]
    public string telegraphTrigger;

    [Tooltip("Duree du telegraph avant l'attaque")]
    public float telegraphDuration = 0.5f;

    /// <summary>
    /// Verifie si le pattern peut etre utilise.
    /// </summary>
    public bool CanUse(float healthPercent, float distanceToTarget, float currentCooldown)
    {
        if (currentCooldown > 0f) return false;
        if (healthPercent < healthThresholdMin || healthPercent > healthThresholdMax) return false;
        if (distanceToTarget < minDistance || distanceToTarget > maxDistance) return false;
        return true;
    }

    /// <summary>
    /// Obtient une attaque du pattern.
    /// </summary>
    public AttackData GetAttack(int index)
    {
        if (attacks == null || index < 0 || index >= attacks.Length)
            return null;
        return attacks[index];
    }

    /// <summary>
    /// Nombre d'attaques dans le pattern.
    /// </summary>
    public int AttackCount => attacks?.Length ?? 0;
}
