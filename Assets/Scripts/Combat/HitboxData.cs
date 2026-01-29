using UnityEngine;

/// <summary>
/// Donnees de configuration d'une hitbox d'attaque.
/// Definit la taille, les frames actives et les modificateurs.
/// </summary>
[CreateAssetMenu(fileName = "NewHitbox", menuName = "EpicLegends/Combat/Hitbox Data")]
public class HitboxData : ScriptableObject
{
    [Header("Forme")]
    [Tooltip("Type de forme de la hitbox")]
    public HitboxShape shape = HitboxShape.Box;

    [Tooltip("Taille de la hitbox (pour Box)")]
    public Vector3 size = Vector3.one;

    [Tooltip("Rayon de la hitbox (pour Sphere)")]
    public float radius = 0.5f;

    [Tooltip("Decalage local par rapport au point d'ancrage")]
    public Vector3 offset = Vector3.zero;

    [Header("Timing")]
    [Tooltip("Frame de debut d'activite")]
    public int startFrame = 5;

    [Tooltip("Frame de fin d'activite")]
    public int endFrame = 15;

    [Header("Degats")]
    [Tooltip("Multiplicateur de degats pour cette hitbox")]
    public float damageMultiplier = 1f;

    [Tooltip("Multiplicateur de knockback")]
    public float knockbackMultiplier = 1f;

    [Tooltip("Multiplicateur de stagger")]
    public float staggerMultiplier = 1f;

    [Header("Comportement")]
    [Tooltip("Peut toucher plusieurs cibles")]
    public bool multiHit = true;

    [Tooltip("Nombre max de cibles")]
    public int maxTargets = 5;

    [Tooltip("Peut toucher la meme cible plusieurs fois")]
    public bool canRehit = false;

    [Tooltip("Delai entre rehits (si active)")]
    public float rehitDelay = 0.5f;

    [Header("Effets")]
    [Tooltip("Prefab d'effet a spawn au hit")]
    public GameObject hitEffectPrefab;

    [Tooltip("Son a jouer au hit")]
    public AudioClip hitSound;

    /// <summary>
    /// Verifie si la hitbox est active a une frame donnee.
    /// </summary>
    public bool IsActiveAtFrame(int frame)
    {
        return frame >= startFrame && frame <= endFrame;
    }

    /// <summary>
    /// Duree d'activite en frames.
    /// </summary>
    public int ActiveFrameCount => endFrame - startFrame + 1;
}

/// <summary>
/// Types de formes de hitbox.
/// </summary>
public enum HitboxShape
{
    Box,
    Sphere,
    Capsule
}
