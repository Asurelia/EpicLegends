using UnityEngine;

/// <summary>
/// Donnees d'un ensemble d'equipement.
/// Definit les bonus accordes pour le port de plusieurs pieces.
/// </summary>
[CreateAssetMenu(fileName = "NewEquipmentSet", menuName = "EpicLegends/Progression/Equipment Set")]
public class EquipmentSetData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("Nom de l'ensemble")]
    public string setName;

    [Tooltip("Description de l'ensemble")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Icone de l'ensemble")]
    public Sprite setIcon;

    #endregion

    #region Pieces

    [Header("Pieces de l'ensemble")]
    [Tooltip("Equipements faisant partie de l'ensemble")]
    public EquipmentData[] setPieces;

    #endregion

    #region Bonus

    [Header("Bonus d'ensemble")]
    [Tooltip("Bonus accordes selon le nombre de pieces")]
    public SetBonus[] setBonuses;

    #endregion

    #region Public Methods

    /// <summary>
    /// Obtient le nombre total de pieces dans l'ensemble.
    /// </summary>
    /// <returns>Nombre de pieces.</returns>
    public int GetTotalPieces()
    {
        return setPieces?.Length ?? 0;
    }

    /// <summary>
    /// Verifie si un equipement fait partie de l'ensemble.
    /// </summary>
    /// <param name="equipment">Equipement a verifier.</param>
    /// <returns>True si partie de l'ensemble.</returns>
    public bool ContainsEquipment(EquipmentData equipment)
    {
        if (equipment == null || setPieces == null) return false;

        foreach (var piece in setPieces)
        {
            if (piece == equipment) return true;
        }

        return false;
    }

    /// <summary>
    /// Obtient les bonus actifs pour un nombre de pieces.
    /// </summary>
    /// <param name="equippedCount">Nombre de pieces equipees.</param>
    /// <returns>Bonus actifs.</returns>
    public SetBonus[] GetActiveBonuses(int equippedCount)
    {
        if (setBonuses == null) return new SetBonus[0];

        var active = new System.Collections.Generic.List<SetBonus>();

        foreach (var bonus in setBonuses)
        {
            if (bonus.piecesRequired <= equippedCount)
            {
                active.Add(bonus);
            }
        }

        return active.ToArray();
    }

    /// <summary>
    /// Obtient la description formatee de tous les bonus.
    /// </summary>
    /// <param name="equippedCount">Pieces actuellement equipees.</param>
    /// <returns>Description formatee.</returns>
    public string GetBonusDescription(int equippedCount)
    {
        if (setBonuses == null || setBonuses.Length == 0)
        {
            return "Aucun bonus d'ensemble.";
        }

        var sb = new System.Text.StringBuilder();

        foreach (var bonus in setBonuses)
        {
            bool isActive = bonus.piecesRequired <= equippedCount;
            string status = isActive ? "[Actif]" : $"({bonus.piecesRequired} pieces)";

            sb.AppendLine($"{status} {bonus.bonusDescription}");
        }

        return sb.ToString();
    }

    #endregion
}

/// <summary>
/// Bonus d'un ensemble d'equipement.
/// </summary>
[System.Serializable]
public struct SetBonus
{
    [Tooltip("Nombre de pieces requises")]
    public int piecesRequired;

    [Tooltip("Description du bonus")]
    public string bonusDescription;

    [Tooltip("Type de bonus")]
    public SetBonusType bonusType;

    [Tooltip("Stat affectee (si applicable)")]
    public StatType affectedStat;

    [Tooltip("Valeur du bonus")]
    public float bonusValue;

    [Tooltip("Est un pourcentage?")]
    public bool isPercentage;
}

/// <summary>
/// Types de bonus d'ensemble.
/// </summary>
public enum SetBonusType
{
    /// <summary>Bonus de stat.</summary>
    StatBonus,

    /// <summary>Bonus de degats.</summary>
    DamageBonus,

    /// <summary>Bonus de defense.</summary>
    DefenseBonus,

    /// <summary>Effet special.</summary>
    SpecialEffect,

    /// <summary>Bonus de regeneration.</summary>
    RegenBonus,

    /// <summary>Bonus de vitesse.</summary>
    SpeedBonus
}
