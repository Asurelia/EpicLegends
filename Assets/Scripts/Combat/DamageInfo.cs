using UnityEngine;

/// <summary>
/// Structure contenant toutes les informations d'une instance de degats.
/// Utilisee pour transmettre les donnees entre l'attaquant et la cible.
/// </summary>
[System.Serializable]
public struct DamageInfo
{
    /// <summary>
    /// Degats de base avant modifications.
    /// </summary>
    public float baseDamage;

    /// <summary>
    /// Type de degats (physique ou elementaire).
    /// </summary>
    public DamageType damageType;

    /// <summary>
    /// GameObject qui inflige les degats.
    /// </summary>
    public GameObject attacker;

    /// <summary>
    /// Point d'impact des degats.
    /// </summary>
    public Vector3 hitPoint;

    /// <summary>
    /// Normale de la surface au point d'impact.
    /// </summary>
    public Vector3 hitNormal;

    /// <summary>
    /// Force de recul a appliquer.
    /// </summary>
    public float knockbackForce;

    /// <summary>
    /// Direction du recul.
    /// </summary>
    public Vector3 knockbackDirection;

    /// <summary>
    /// Valeur de stagger a appliquer contre la poise.
    /// </summary>
    public float staggerValue;

    /// <summary>
    /// Est-ce un coup critique?
    /// </summary>
    public bool isCritical;

    /// <summary>
    /// Multiplicateur de degats critiques.
    /// </summary>
    public float criticalMultiplier;

    /// <summary>
    /// Peut-on parer cette attaque?
    /// </summary>
    public bool canBeParried;

    /// <summary>
    /// Peut-on bloquer cette attaque?
    /// </summary>
    public bool canBeBlocked;

    /// <summary>
    /// L'attaque brise-t-elle la garde?
    /// </summary>
    public bool isGuardBreak;

    /// <summary>
    /// Index de l'attaque dans le combo.
    /// </summary>
    public int comboIndex;

    /// <summary>
    /// Constructeur simplifie.
    /// </summary>
    public DamageInfo(float damage, DamageType type, GameObject source = null)
    {
        baseDamage = damage;
        damageType = type;
        attacker = source;
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;
        knockbackForce = 0f;
        knockbackDirection = Vector3.zero;
        staggerValue = damage * 0.25f; // Par defaut, 25% des degats en stagger
        isCritical = false;
        criticalMultiplier = 1.5f;
        canBeParried = true;
        canBeBlocked = true;
        isGuardBreak = false;
        comboIndex = 0;
    }

    /// <summary>
    /// Calcule les degats finaux avec le multiplicateur critique.
    /// </summary>
    public float GetEffectiveDamage()
    {
        return isCritical ? baseDamage * criticalMultiplier : baseDamage;
    }
}
