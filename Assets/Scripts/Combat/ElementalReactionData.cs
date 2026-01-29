using UnityEngine;

/// <summary>
/// Donnees de configuration pour une reaction elementaire.
/// </summary>
[CreateAssetMenu(fileName = "NewElementalReaction", menuName = "EpicLegends/Combat/Elemental Reaction")]
public class ElementalReactionData : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Type de reaction")]
    public ElementalReactionType reactionType;

    [Tooltip("Nom affichable")]
    public string displayName;

    [Tooltip("Description de l'effet")]
    [TextArea(2, 4)]
    public string description;

    [Header("Degats")]
    [Tooltip("Multiplicateur de degats")]
    public float damageMultiplier = 1f;

    [Tooltip("Fait des degats en zone?")]
    public bool isAoE;

    [Tooltip("Rayon de l'effet AoE")]
    public float aoERadius = 3f;

    [Header("Effets")]
    [Tooltip("Duree de l'effet en secondes")]
    public float effectDuration = 0f;

    [Tooltip("Cree un bouclier?")]
    public bool createsShield;

    [Tooltip("Valeur du bouclier (% de la vie max)")]
    [Range(0f, 1f)]
    public float shieldValue = 0.1f;

    [Tooltip("Immobilise la cible?")]
    public bool freezesTarget;

    [Tooltip("Reduit la defense?")]
    public bool reducesDefense;

    [Tooltip("Reduction de defense (%)")]
    [Range(0f, 1f)]
    public float defenseReduction = 0.4f;

    [Tooltip("Fait des degats sur la duree?")]
    public bool isDamageOverTime;

    [Tooltip("Intervalle entre chaque tick de DoT")]
    public float dotTickRate = 0.5f;

    [Header("Visuel")]
    [Tooltip("Couleur de l'effet")]
    public Color effectColor = Color.white;

    [Tooltip("Prefab de l'effet visuel")]
    public GameObject vfxPrefab;

    [Tooltip("Son de la reaction")]
    public AudioClip reactionSound;

    /// <summary>
    /// Calcule les degats de la reaction.
    /// </summary>
    public float CalculateDamage(float baseDamage, float elementalMastery = 0f)
    {
        float masteryBonus = 1f + (elementalMastery / 100f) * 0.5f;
        return baseDamage * damageMultiplier * masteryBonus;
    }
}
