using UnityEngine;

/// <summary>
/// Donnees d'un item de capture (Pokeball-like).
/// </summary>
[CreateAssetMenu(fileName = "NewCaptureItem", menuName = "EpicLegends/Creatures/Capture Item")]
public class CaptureItemData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("Nom de l'item")]
    public string itemName;

    [Tooltip("Description")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Icone")]
    public Sprite icon;

    [Tooltip("Qualite de l'item")]
    public CaptureItemQuality quality = CaptureItemQuality.Basic;

    #endregion

    #region Capture Stats

    [Header("Stats de Capture")]
    [Tooltip("Bonus au taux de capture (0.0 = aucun bonus, 0.5 = +50%)")]
    [Range(0f, 2f)]
    public float captureRateBonus = 0f;

    [Tooltip("Multiplicateur de capture")]
    [Range(1f, 5f)]
    public float captureMultiplier = 1f;

    [Tooltip("Nombre d'oscillations avant confirmation")]
    public int oscillationCount = 3;

    #endregion

    #region Type Bonus

    [Header("Bonus de Type")]
    [Tooltip("A un bonus contre un type specifique?")]
    public bool hasBonusAgainstType = false;

    [Tooltip("Type beneficiant du bonus")]
    public CreatureType bonusAgainstType;

    [Tooltip("Multiplicateur contre ce type")]
    [Range(1f, 5f)]
    public float typeBonusMultiplier = 1.5f;

    #endregion

    #region Rarete Bonus

    [Header("Bonus de Rarete")]
    [Tooltip("A un bonus contre une rarete specifique?")]
    public bool hasBonusAgainstRarity = false;

    [Tooltip("Rarete beneficiant du bonus")]
    public CreatureRarity bonusAgainstRarity;

    [Tooltip("Multiplicateur contre cette rarete")]
    [Range(1f, 5f)]
    public float rarityBonusMultiplier = 1.5f;

    #endregion

    #region Conditions Speciales

    [Header("Conditions Speciales")]
    [Tooltip("Bonus si vie basse (< 25%)")]
    public bool bonusOnLowHealth = false;

    [Tooltip("Multiplicateur si vie basse")]
    [Range(1f, 5f)]
    public float lowHealthMultiplier = 2f;

    [Tooltip("Bonus si status altere")]
    public bool bonusOnStatusEffect = false;

    [Tooltip("Multiplicateur si status altere")]
    [Range(1f, 5f)]
    public float statusEffectMultiplier = 1.5f;

    [Tooltip("Capture garantie (pour Master Ball)")]
    public bool guaranteedCapture = false;

    #endregion

    #region Visuel

    [Header("Visuel")]
    [Tooltip("Prefab de l'item lance")]
    public GameObject throwPrefab;

    [Tooltip("Effet visuel de capture reussie")]
    public GameObject successVFX;

    [Tooltip("Effet visuel de capture ratee")]
    public GameObject failVFX;

    [Tooltip("Son de lancer")]
    public AudioClip throwSound;

    [Tooltip("Son de capture reussie")]
    public AudioClip successSound;

    [Tooltip("Son de capture ratee")]
    public AudioClip failSound;

    #endregion

    #region Economie

    [Header("Economie")]
    [Tooltip("Prix d'achat")]
    public int buyPrice = 100;

    [Tooltip("Prix de vente")]
    public int sellPrice = 50;

    [Tooltip("Disponible dans les magasins?")]
    public bool availableInShops = true;

    [Tooltip("Niveau de magasin requis")]
    public int shopLevelRequired = 1;

    #endregion

    #region Public Methods

    /// <summary>
    /// Calcule le multiplicateur total pour une creature donnee.
    /// </summary>
    public float GetTotalMultiplier(CreatureInstance target, bool hasStatusEffect = false)
    {
        if (guaranteedCapture) return 100f; // Capture garantie

        float multiplier = captureMultiplier;

        // Bonus de type
        if (hasBonusAgainstType && target.Data.creatureType == bonusAgainstType)
        {
            multiplier *= typeBonusMultiplier;
        }

        // Bonus de rarete
        if (hasBonusAgainstRarity && target.Data.rarity == bonusAgainstRarity)
        {
            multiplier *= rarityBonusMultiplier;
        }

        // Bonus vie basse
        if (bonusOnLowHealth && target.HealthPercent < 0.25f)
        {
            multiplier *= lowHealthMultiplier;
        }

        // Bonus status
        if (bonusOnStatusEffect && hasStatusEffect)
        {
            multiplier *= statusEffectMultiplier;
        }

        return multiplier;
    }

    #endregion
}

/// <summary>
/// Qualite des items de capture.
/// </summary>
public enum CaptureItemQuality
{
    /// <summary>Item basique, peu efficace</summary>
    Basic = 0,

    /// <summary>Item ameliore</summary>
    Great = 1,

    /// <summary>Item superieur</summary>
    Ultra = 2,

    /// <summary>Item specialise (type, condition)</summary>
    Specialized = 3,

    /// <summary>Item legendaire (Master Ball)</summary>
    Master = 4
}
