using UnityEngine;

/// <summary>
/// Donnees d'une classe de personnage.
/// Definit les bonus, competences et restrictions d'une classe.
/// </summary>
[CreateAssetMenu(fileName = "NewClass", menuName = "EpicLegends/Progression/Class Data")]
public class ClassData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("Nom de la classe")]
    public string className;

    [Tooltip("Description de la classe")]
    [TextArea(3, 5)]
    public string description;

    [Tooltip("Type de classe")]
    public ClassType classType;

    [Tooltip("Tier de la classe")]
    public ClassTier tier = ClassTier.Base;

    [Tooltip("Icone de la classe")]
    public Sprite classIcon;

    #endregion

    #region Pre-requis

    [Header("Pre-requis")]
    [Tooltip("Niveau minimum pour debloquer")]
    public int requiredLevel = 1;

    [Tooltip("Classes pre-requises")]
    public ClassData[] requiredClasses;

    [Tooltip("Niveau de maitrise requis dans les classes pre-requises")]
    public int requiredMasteryLevel = 10;

    #endregion

    #region Stats

    [Header("Stats de base")]
    [Tooltip("Bonus de stats par niveau")]
    public StatBonus[] statBonuses;

    [Tooltip("Multiplicateur de vie")]
    public float healthMultiplier = 1f;

    [Tooltip("Multiplicateur de mana")]
    public float manaMultiplier = 1f;

    [Tooltip("Multiplicateur de stamina")]
    public float staminaMultiplier = 1f;

    #endregion

    #region Competences

    [Header("Competences")]
    [Tooltip("Competences de classe")]
    public ClassSkill[] classSkills;

    [Tooltip("Competence ultime (niveau max)")]
    public SkillData ultimateSkill;

    [Tooltip("Niveau requis pour l'ultime")]
    public int ultimateSkillLevel = 50;

    #endregion

    #region Equipment

    [Header("Equipement")]
    [Tooltip("Types d'armes utilisables")]
    public WeaponType[] usableWeapons;

    [Tooltip("Types d'armures utilisables")]
    public ArmorType[] usableArmors;

    #endregion

    #region Maitrise

    [Header("Maitrise")]
    [Tooltip("Niveau maximum de classe")]
    public int maxClassLevel = 50;

    [Tooltip("Bonus de maitrise (niveau max atteint)")]
    public MasteryBonus[] masteryBonuses;

    #endregion

    #region Public Methods

    /// <summary>
    /// Obtient le bonus de stat total pour un niveau donne.
    /// </summary>
    /// <param name="stat">Type de stat.</param>
    /// <param name="classLevel">Niveau de classe.</param>
    /// <returns>Bonus total.</returns>
    public float GetStatBonusForLevel(StatType stat, int classLevel)
    {
        if (statBonuses == null) return 0f;

        foreach (var bonus in statBonuses)
        {
            if (bonus.statType == stat)
            {
                return bonus.baseBonus + (bonus.bonusPerLevel * classLevel);
            }
        }

        return 0f;
    }

    /// <summary>
    /// Verifie si une arme est utilisable par cette classe.
    /// </summary>
    /// <param name="weaponType">Type d'arme.</param>
    /// <returns>True si utilisable.</returns>
    public bool CanUseWeapon(WeaponType weaponType)
    {
        if (usableWeapons == null || usableWeapons.Length == 0) return true;

        foreach (var weapon in usableWeapons)
        {
            if (weapon == weaponType) return true;
        }

        return false;
    }

    /// <summary>
    /// Verifie si une armure est utilisable par cette classe.
    /// </summary>
    /// <param name="armorType">Type d'armure.</param>
    /// <returns>True si utilisable.</returns>
    public bool CanUseArmor(ArmorType armorType)
    {
        if (usableArmors == null || usableArmors.Length == 0) return true;

        foreach (var armor in usableArmors)
        {
            if (armor == armorType) return true;
        }

        return false;
    }

    /// <summary>
    /// Obtient les competences debloquees a un niveau.
    /// </summary>
    /// <param name="classLevel">Niveau de classe.</param>
    /// <returns>Competences debloquees.</returns>
    public SkillData[] GetUnlockedSkillsAtLevel(int classLevel)
    {
        if (classSkills == null) return new SkillData[0];

        var unlocked = new System.Collections.Generic.List<SkillData>();

        foreach (var skill in classSkills)
        {
            if (skill.unlockLevel <= classLevel && skill.skill != null)
            {
                unlocked.Add(skill.skill);
            }
        }

        return unlocked.ToArray();
    }

    #endregion
}

/// <summary>
/// Types de classes de base.
/// </summary>
public enum ClassType
{
    /// <summary>Guerrier - DPS melee, tank.</summary>
    Warrior,

    /// <summary>Mage - DPS magique, support.</summary>
    Mage,

    /// <summary>Voleur - DPS rapide, evasion.</summary>
    Rogue,

    /// <summary>Ranger - DPS distance, creatures.</summary>
    Ranger,

    // Classes avancees
    /// <summary>Paladin - Guerrier + Mage support.</summary>
    Paladin,

    /// <summary>Berserker - Guerrier offensif.</summary>
    Berserker,

    /// <summary>Sorcier - Mage offensif.</summary>
    Sorcerer,

    /// <summary>Enchanteur - Mage support.</summary>
    Enchanter,

    /// <summary>Assassin - Voleur offensif.</summary>
    Assassin,

    /// <summary>Ombre - Voleur furtif.</summary>
    Shadow,

    /// <summary>Chasseur - Ranger + creatures.</summary>
    Hunter,

    /// <summary>Archer - Ranger precision.</summary>
    Archer,

    // Classes maitre
    /// <summary>Champion - Guerrier ultime.</summary>
    Champion,

    /// <summary>Archimage - Mage ultime.</summary>
    Archmage,

    /// <summary>Maitre Ombre - Voleur ultime.</summary>
    ShadowMaster,

    /// <summary>Maitre des Betes - Ranger ultime.</summary>
    BeastMaster
}

/// <summary>
/// Tiers de classe.
/// </summary>
public enum ClassTier
{
    /// <summary>Classe de base.</summary>
    Base,

    /// <summary>Classe avancee.</summary>
    Advanced,

    /// <summary>Classe maitre.</summary>
    Master
}

/// <summary>
/// Bonus de stat pour une classe.
/// </summary>
[System.Serializable]
public struct StatBonus
{
    [Tooltip("Type de stat")]
    public StatType statType;

    [Tooltip("Bonus de base")]
    public float baseBonus;

    [Tooltip("Bonus par niveau de classe")]
    public float bonusPerLevel;
}

/// <summary>
/// Competence de classe avec niveau de deblocage.
/// </summary>
[System.Serializable]
public struct ClassSkill
{
    [Tooltip("Competence")]
    public SkillData skill;

    [Tooltip("Niveau de deblocage")]
    public int unlockLevel;
}

/// <summary>
/// Bonus de maitrise de classe.
/// </summary>
[System.Serializable]
public struct MasteryBonus
{
    [Tooltip("Description du bonus")]
    public string description;

    [Tooltip("Type de bonus")]
    public MasteryBonusType bonusType;

    [Tooltip("Valeur du bonus")]
    public float value;
}

/// <summary>
/// Types de bonus de maitrise.
/// </summary>
public enum MasteryBonusType
{
    /// <summary>Bonus de stat permanent.</summary>
    StatBonus,

    /// <summary>Competence transferable.</summary>
    CrossClassSkill,

    /// <summary>Trait passif.</summary>
    PassiveTrait,

    /// <summary>Bonus de degats.</summary>
    DamageBonus,

    /// <summary>Bonus de survie.</summary>
    SurvivalBonus
}

/// <summary>
/// Types d'armures.
/// </summary>
public enum ArmorType
{
    /// <summary>Tissu - Mages.</summary>
    Cloth,

    /// <summary>Cuir - Voleurs, Rangers.</summary>
    Leather,

    /// <summary>Mailles - Guerriers legers.</summary>
    Mail,

    /// <summary>Plaques - Guerriers lourds.</summary>
    Plate
}
