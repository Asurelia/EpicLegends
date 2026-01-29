using UnityEngine;

/// <summary>
/// Donnees de configuration d'une competence.
/// </summary>
[CreateAssetMenu(fileName = "NewSkill", menuName = "EpicLegends/Skills/Skill Data")]
public class SkillData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("Nom de la competence")]
    public string skillName;

    [Tooltip("Description de la competence")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Icone de la competence")]
    public Sprite icon;

    [Tooltip("Type de competence")]
    public SkillType skillType = SkillType.Active;

    [Tooltip("Categorie pour l'arbre de competences")]
    public SkillCategory category = SkillCategory.Offense;

    #endregion

    #region Couts et Cooldown

    [Header("Couts")]
    [Tooltip("Cout en mana")]
    public float manaCost = 20f;

    [Tooltip("Cout en stamina")]
    public float staminaCost = 0f;

    [Tooltip("Temps de recharge en secondes")]
    public float cooldown = 5f;

    [Tooltip("Temps de charge avant utilisation")]
    public float castTime = 0f;

    #endregion

    #region Ciblage

    [Header("Ciblage")]
    [Tooltip("Type de cible")]
    public SkillTargetType targetType = SkillTargetType.SingleEnemy;

    [Tooltip("Portee de la competence")]
    public float range = 5f;

    [Tooltip("Rayon de la zone d'effet")]
    public float areaRadius = 3f;

    [Tooltip("Angle du cone (pour Cone)")]
    public float coneAngle = 45f;

    #endregion

    #region Degats

    [Header("Degats")]
    [Tooltip("Degats de base")]
    public float baseDamage = 50f;

    [Tooltip("Type de degats")]
    public DamageType damageType = DamageType.Physical;

    [Tooltip("Scaling avec la stat d'attaque (0-1)")]
    [Range(0f, 2f)]
    public float damageScaling = 0.5f;

    [Tooltip("Nombre de coups")]
    public int hitCount = 1;

    [Tooltip("Intervalle entre les coups")]
    public float hitInterval = 0.2f;

    #endregion

    #region Effets

    [Header("Effets")]
    [Tooltip("Applique un element?")]
    public bool appliesElement = false;

    [Tooltip("Element a appliquer")]
    public ElementType elementType;

    [Tooltip("Jauge d'element appliquee")]
    public float elementGauge = 1f;

    [Tooltip("Soigne au lieu de faire des degats?")]
    public bool isHeal = false;

    [Tooltip("Quantite de soin de base")]
    public float baseHeal = 0f;

    [Tooltip("Applique un buff/debuff?")]
    public bool appliesStatusEffect = false;

    [Tooltip("Duree du buff/debuff")]
    public float statusDuration = 5f;

    #endregion

    #region Passif

    [Header("Bonus Passifs (pour Passive)")]
    [Tooltip("Bonus d'attaque passif")]
    public float passiveAttackBonus = 0f;

    [Tooltip("Bonus de defense passif")]
    public float passiveDefenseBonus = 0f;

    [Tooltip("Bonus de vitesse passif")]
    public float passiveSpeedBonus = 0f;

    [Tooltip("Bonus de critique passif")]
    public float passiveCritBonus = 0f;

    #endregion

    #region Progression

    [Header("Progression")]
    [Tooltip("Niveau actuel")]
    public int currentLevel = 1;

    [Tooltip("Niveau maximum")]
    public int maxLevel = 10;

    [Tooltip("Degats bonus par niveau")]
    public float damagePerLevel = 5f;

    [Tooltip("Reduction du cooldown par niveau")]
    public float cooldownReductionPerLevel = 0.1f;

    [Tooltip("Competences requises pour debloquer")]
    public SkillData[] prerequisites;

    #endregion

    #region Animation et VFX

    [Header("Animation et VFX")]
    [Tooltip("Trigger d'animation")]
    public string animationTrigger;

    [Tooltip("Prefab d'effet visuel")]
    public GameObject vfxPrefab;

    [Tooltip("Son de la competence")]
    public AudioClip soundEffect;

    #endregion

    #region Public Methods

    /// <summary>
    /// Calcule les degats de la competence.
    /// </summary>
    public float CalculateDamage(float attackStat)
    {
        float levelBonus = (currentLevel - 1) * damagePerLevel;
        float scaledDamage = (baseDamage + levelBonus) * (1f + attackStat * damageScaling / 100f);
        return scaledDamage;
    }

    /// <summary>
    /// Calcule le soin de la competence.
    /// </summary>
    public float CalculateHeal(float healingStat)
    {
        float levelBonus = (currentLevel - 1) * damagePerLevel; // Utilise le meme scaling
        return (baseHeal + levelBonus) * (1f + healingStat / 100f);
    }

    /// <summary>
    /// Obtient le cooldown effectif avec le niveau.
    /// </summary>
    public float GetEffectiveCooldown()
    {
        float reduction = (currentLevel - 1) * cooldownReductionPerLevel;
        return Mathf.Max(1f, cooldown - reduction);
    }

    /// <summary>
    /// Peut-on augmenter le niveau?
    /// </summary>
    public bool CanLevelUp()
    {
        return currentLevel < maxLevel;
    }

    /// <summary>
    /// Augmente le niveau.
    /// </summary>
    public bool LevelUp()
    {
        if (!CanLevelUp()) return false;
        currentLevel++;
        return true;
    }

    /// <summary>
    /// Verifie si les prerequis sont satisfaits.
    /// </summary>
    public bool ArePrerequisitesMet(SkillData[] unlockedSkills)
    {
        if (prerequisites == null || prerequisites.Length == 0)
            return true;

        foreach (var prereq in prerequisites)
        {
            bool found = false;
            foreach (var unlocked in unlockedSkills)
            {
                if (unlocked == prereq)
                {
                    found = true;
                    break;
                }
            }
            if (!found) return false;
        }
        return true;
    }

    /// <summary>
    /// Cree une copie de la competence avec les memes stats.
    /// </summary>
    public SkillData CreateCopy()
    {
        var copy = CreateInstance<SkillData>();

        copy.skillName = skillName;
        copy.description = description;
        copy.icon = icon;
        copy.skillType = skillType;
        copy.category = category;
        copy.manaCost = manaCost;
        copy.staminaCost = staminaCost;
        copy.cooldown = cooldown;
        copy.castTime = castTime;
        copy.targetType = targetType;
        copy.range = range;
        copy.areaRadius = areaRadius;
        copy.coneAngle = coneAngle;
        copy.baseDamage = baseDamage;
        copy.damageType = damageType;
        copy.damageScaling = damageScaling;
        copy.hitCount = hitCount;
        copy.hitInterval = hitInterval;
        copy.appliesElement = appliesElement;
        copy.elementType = elementType;
        copy.elementGauge = elementGauge;
        copy.isHeal = isHeal;
        copy.baseHeal = baseHeal;
        copy.passiveAttackBonus = passiveAttackBonus;
        copy.passiveDefenseBonus = passiveDefenseBonus;
        copy.passiveSpeedBonus = passiveSpeedBonus;
        copy.passiveCritBonus = passiveCritBonus;
        copy.currentLevel = currentLevel;
        copy.maxLevel = maxLevel;
        copy.damagePerLevel = damagePerLevel;
        copy.cooldownReductionPerLevel = cooldownReductionPerLevel;
        copy.animationTrigger = animationTrigger;
        copy.vfxPrefab = vfxPrefab;
        copy.soundEffect = soundEffect;

        return copy;
    }

    #endregion
}
