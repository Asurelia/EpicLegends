using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Donnees d'un combo complet.
/// Definit la sequence d'attaques et les conditions d'enchainement.
/// </summary>
[CreateAssetMenu(fileName = "NewCombo", menuName = "EpicLegends/Combat/Combo Data")]
public class ComboData : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Nom du combo")]
    public string comboName;

    [Tooltip("Description du combo")]
    [TextArea(2, 4)]
    public string description;

    [Header("Sequence")]
    [Tooltip("Liste des attaques dans l'ordre")]
    public List<AttackData> attacks = new List<AttackData>();

    [Header("Timing")]
    [Tooltip("Duree de la fenetre d'input pour continuer le combo")]
    public float inputWindowDuration = 0.5f;

    [Tooltip("Temps de recuperation apres la fin du combo")]
    public float recoveryTime = 0.3f;

    [Header("Finisher")]
    [Tooltip("Attaque speciale de finition (optionnelle)")]
    public AttackData finisherAttack;

    [Tooltip("Nombre d'attaques requises pour le finisher")]
    public int finisherRequiredHits = 3;

    [Header("Bonus")]
    [Tooltip("Multiplicateur de degats progressif par attaque")]
    public float comboScaling = 1.1f;

    [Tooltip("Multiplicateur max du combo")]
    public float maxComboMultiplier = 2f;

    /// <summary>
    /// Nombre d'attaques dans le combo.
    /// </summary>
    public int Length => attacks?.Count ?? 0;

    /// <summary>
    /// Obtient l'attaque a un index donne.
    /// </summary>
    public AttackData GetAttack(int index)
    {
        if (attacks == null || index < 0 || index >= attacks.Count)
            return null;
        return attacks[index];
    }

    /// <summary>
    /// Calcule le multiplicateur de degats pour une position dans le combo.
    /// </summary>
    public float GetDamageMultiplier(int comboIndex)
    {
        float multiplier = Mathf.Pow(comboScaling, comboIndex);
        return Mathf.Min(multiplier, maxComboMultiplier);
    }

    /// <summary>
    /// Verifie si le finisher peut etre declenche.
    /// </summary>
    public bool CanTriggerFinisher(int hitCount)
    {
        return finisherAttack != null && hitCount >= finisherRequiredHits;
    }
}
