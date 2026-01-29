using UnityEngine;

/// <summary>
/// Donnees de base d'une espece de creature.
/// Definit les stats, abilities et caracteristiques de l'espece.
/// </summary>
[CreateAssetMenu(fileName = "NewCreature", menuName = "EpicLegends/Creatures/Creature Data")]
public class CreatureData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("Nom de la creature")]
    public string creatureName;

    [Tooltip("Description")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Type de creature")]
    public CreatureType creatureType = CreatureType.Beast;

    [Tooltip("Rarete")]
    public CreatureRarity rarity = CreatureRarity.Common;

    [Tooltip("Role en combat")]
    public CreatureRole role = CreatureRole.Balanced;

    [Tooltip("Taille")]
    public CreatureSize size = CreatureSize.Medium;

    [Tooltip("Icone")]
    public Sprite icon;

    [Tooltip("Prefab 3D")]
    public GameObject prefab;

    #endregion

    #region Stats de Base

    [Header("Stats de Base (Niveau 1)")]
    [Tooltip("Points de vie de base")]
    public float baseHealth = 100f;

    [Tooltip("Points de mana de base")]
    public float baseMana = 50f;

    [Tooltip("Attaque de base")]
    public float baseAttack = 30f;

    [Tooltip("Defense de base")]
    public float baseDefense = 20f;

    [Tooltip("Vitesse de base")]
    public float baseSpeed = 10f;

    #endregion

    #region Croissance par Niveau

    [Header("Croissance par Niveau")]
    [Tooltip("Vie par niveau")]
    public float healthPerLevel = 10f;

    [Tooltip("Mana par niveau")]
    public float manaPerLevel = 5f;

    [Tooltip("Attaque par niveau")]
    public float attackPerLevel = 3f;

    [Tooltip("Defense par niveau")]
    public float defensePerLevel = 2f;

    [Tooltip("Vitesse par niveau")]
    public float speedPerLevel = 0.5f;

    [Tooltip("Niveau maximum")]
    public int maxLevel = 100;

    #endregion

    #region Element et Affinites

    [Header("Element")]
    [Tooltip("Element principal")]
    public ElementType primaryElement = ElementType.Fire;

    [Tooltip("Element secondaire (optionnel)")]
    public ElementType? secondaryElement = null;

    [Header("Resistances")]
    [Range(-1f, 1f)]
    public float fireResistance = 0f;
    [Range(-1f, 1f)]
    public float waterResistance = 0f;
    [Range(-1f, 1f)]
    public float iceResistance = 0f;
    [Range(-1f, 1f)]
    public float electricResistance = 0f;
    [Range(-1f, 1f)]
    public float lightResistance = 0f;
    [Range(-1f, 1f)]
    public float darkResistance = 0f;

    #endregion

    #region Abilities

    [Header("Abilities")]
    [Tooltip("Abilities apprises")]
    public SkillData[] abilities;

    [Tooltip("Niveaux auxquels les abilities sont apprises")]
    public int[] abilityLearnLevels;

    [Tooltip("Ability ultime")]
    public SkillData ultimateAbility;

    [Tooltip("Niveau requis pour l'ultime")]
    public int ultimateUnlockLevel = 50;

    #endregion

    #region Mount

    [Header("Mount")]
    [Tooltip("Peut etre monte?")]
    public bool canBeMounted = false;

    [Tooltip("Vitesse de deplacement monte")]
    public float mountSpeed = 8f;

    [Tooltip("Peut voler?")]
    public bool canFly = false;

    [Tooltip("Vitesse de vol")]
    public float flySpeed = 12f;

    #endregion

    #region Capture

    [Header("Capture")]
    [Tooltip("Taux de capture de base")]
    [Range(0f, 1f)]
    public float baseCaptureRate = 0.3f;

    [Tooltip("Experience donnee si vaincu")]
    public int defeatExperience = 100;

    #endregion

    #region Audio/Visual

    [Header("Audio/Visual")]
    public AudioClip callSound;
    public AudioClip attackSound;
    public AudioClip hurtSound;

    #endregion

    #region Public Methods

    /// <summary>
    /// Calcule la vie a un niveau donne.
    /// </summary>
    public float GetHealthAtLevel(int level)
    {
        return baseHealth + (level - 1) * healthPerLevel;
    }

    /// <summary>
    /// Calcule le mana a un niveau donne.
    /// </summary>
    public float GetManaAtLevel(int level)
    {
        return baseMana + (level - 1) * manaPerLevel;
    }

    /// <summary>
    /// Calcule l'attaque a un niveau donne.
    /// </summary>
    public float GetAttackAtLevel(int level)
    {
        return baseAttack + (level - 1) * attackPerLevel;
    }

    /// <summary>
    /// Calcule la defense a un niveau donne.
    /// </summary>
    public float GetDefenseAtLevel(int level)
    {
        return baseDefense + (level - 1) * defensePerLevel;
    }

    /// <summary>
    /// Calcule la vitesse a un niveau donne.
    /// </summary>
    public float GetSpeedAtLevel(int level)
    {
        return baseSpeed + (level - 1) * speedPerLevel;
    }

    /// <summary>
    /// Obtient les abilities disponibles a un niveau.
    /// </summary>
    public SkillData[] GetAbilitiesAtLevel(int level)
    {
        if (abilities == null || abilityLearnLevels == null)
            return new SkillData[0];

        int count = 0;
        for (int i = 0; i < abilityLearnLevels.Length && i < abilities.Length; i++)
        {
            if (abilityLearnLevels[i] <= level) count++;
        }

        var result = new SkillData[count];
        int index = 0;
        for (int i = 0; i < abilityLearnLevels.Length && i < abilities.Length; i++)
        {
            if (abilityLearnLevels[i] <= level)
            {
                result[index++] = abilities[i];
            }
        }

        return result;
    }

    /// <summary>
    /// Peut monter cette creature?
    /// </summary>
    public bool CanMount()
    {
        return canBeMounted && size >= CreatureSize.Medium;
    }

    /// <summary>
    /// Obtient le multiplicateur de rarete.
    /// </summary>
    public float GetRarityMultiplier()
    {
        return rarity switch
        {
            CreatureRarity.Common => 1f,
            CreatureRarity.Uncommon => 1.1f,
            CreatureRarity.Rare => 1.25f,
            CreatureRarity.Epic => 1.5f,
            CreatureRarity.Legendary => 2f,
            CreatureRarity.Mythic => 3f,
            _ => 1f
        };
    }

    #endregion
}
