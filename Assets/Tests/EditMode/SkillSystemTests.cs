using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests unitaires pour le systeme de competences.
/// </summary>
public class SkillSystemTests
{
    #region SkillType Tests

    [Test]
    public void SkillType_HasAllTypes()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(SkillType), "Active"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(SkillType), "Passive"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(SkillType), "Ultimate"));
    }

    [Test]
    public void SkillTargetType_HasAllTypes()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(SkillTargetType), "Self"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(SkillTargetType), "SingleEnemy"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(SkillTargetType), "AllEnemies"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(SkillTargetType), "SingleAlly"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(SkillTargetType), "AllAllies"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(SkillTargetType), "Area"));
    }

    #endregion

    #region SkillData Tests

    [Test]
    public void SkillData_CanBeCreated()
    {
        // Act
        var skill = ScriptableObject.CreateInstance<SkillData>();

        // Assert
        Assert.IsNotNull(skill);

        // Cleanup
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void SkillData_HasDefaults()
    {
        // Arrange
        var skill = ScriptableObject.CreateInstance<SkillData>();

        // Assert
        Assert.GreaterOrEqual(skill.cooldown, 0f);

        // Cleanup
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void SkillData_CalculateDamage_ReturnsValue()
    {
        // Arrange
        var skill = ScriptableObject.CreateInstance<SkillData>();
        SetField(skill, "baseDamage", 100f);
        SetField(skill, "damageScaling", 0.5f);

        // Act
        float damage = skill.CalculateDamage(100f);

        // Assert
        Assert.Greater(damage, 100f); // Devrait etre augmente par le scaling

        // Cleanup
        Object.DestroyImmediate(skill);
    }

    private void SetField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        field?.SetValue(obj, value);
    }

    #endregion

    #region SkillSlot Tests

    private GameObject _testObject;
    private SkillController _skillController;

    [SetUp]
    public void Setup()
    {
        _testObject = new GameObject("TestSkillUser");
        _skillController = _testObject.AddComponent<SkillController>();

        // Appeler Awake manuellement (EditMode ne l'appelle pas)
        var awakeMethod = typeof(SkillController).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(_skillController, null);
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_testObject);
    }

    [Test]
    public void SkillController_HasSkillSlots()
    {
        // Assert
        Assert.IsNotNull(_skillController);
    }

    [Test]
    public void SkillController_CanEquipSkill()
    {
        // Arrange
        var skill = ScriptableObject.CreateInstance<SkillData>();
        SetField(skill, "skillName", "TestSkill");

        // Act
        bool result = _skillController.EquipSkill(skill, 0);

        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(skill, _skillController.GetSkillInSlot(0));

        // Cleanup
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void SkillController_CanUnequipSkill()
    {
        // Arrange
        var skill = ScriptableObject.CreateInstance<SkillData>();
        _skillController.EquipSkill(skill, 0);

        // Act
        _skillController.UnequipSkill(0);

        // Assert
        Assert.IsNull(_skillController.GetSkillInSlot(0));

        // Cleanup
        Object.DestroyImmediate(skill);
    }

    #endregion

    #region Cooldown Tests

    [Test]
    public void SkillController_TracksCooldown()
    {
        // Arrange
        var skill = ScriptableObject.CreateInstance<SkillData>();
        SetField(skill, "cooldown", 5f);
        SetField(skill, "manaCost", 0f);
        _skillController.EquipSkill(skill, 0);

        // Act
        _skillController.UseSkill(0);

        // Assert
        Assert.IsFalse(_skillController.IsSkillReady(0));

        // Cleanup
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void SkillController_GetCooldownRemaining()
    {
        // Arrange
        var skill = ScriptableObject.CreateInstance<SkillData>();
        SetField(skill, "cooldown", 5f);
        SetField(skill, "manaCost", 0f);
        _skillController.EquipSkill(skill, 0);
        _skillController.UseSkill(0);

        // Act
        float remaining = _skillController.GetCooldownRemaining(0);

        // Assert
        Assert.Greater(remaining, 0f);

        // Cleanup
        Object.DestroyImmediate(skill);
    }

    #endregion

    #region Mana Cost Tests

    [Test]
    public void SkillData_HasManaCost()
    {
        // Arrange
        var skill = ScriptableObject.CreateInstance<SkillData>();
        SetField(skill, "manaCost", 20f);

        // Assert
        Assert.AreEqual(20f, skill.manaCost);

        // Cleanup
        Object.DestroyImmediate(skill);
    }

    #endregion

    #region Passive Skill Tests

    [Test]
    public void PassiveSkill_HasNoManaCost()
    {
        // Arrange
        var skill = ScriptableObject.CreateInstance<SkillData>();
        SetField(skill, "skillType", SkillType.Passive);
        SetField(skill, "manaCost", 0f);

        // Assert
        Assert.AreEqual(0f, skill.manaCost);
        Assert.AreEqual(SkillType.Passive, skill.skillType);

        // Cleanup
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void PassiveSkill_ProvidesStatBonus()
    {
        // Arrange
        var skill = ScriptableObject.CreateInstance<SkillData>();
        SetField(skill, "skillType", SkillType.Passive);
        SetField(skill, "passiveAttackBonus", 10f);
        SetField(skill, "passiveDefenseBonus", 5f);

        // Assert
        Assert.AreEqual(10f, skill.passiveAttackBonus);
        Assert.AreEqual(5f, skill.passiveDefenseBonus);

        // Cleanup
        Object.DestroyImmediate(skill);
    }

    #endregion

    #region Ultimate Skill Tests

    [Test]
    public void UltimateSkill_HasHighCost()
    {
        // Arrange
        var skill = ScriptableObject.CreateInstance<SkillData>();
        SetField(skill, "skillType", SkillType.Ultimate);
        SetField(skill, "manaCost", 100f);
        SetField(skill, "cooldown", 60f);

        // Assert
        Assert.AreEqual(SkillType.Ultimate, skill.skillType);
        Assert.GreaterOrEqual(skill.manaCost, 50f);
        Assert.GreaterOrEqual(skill.cooldown, 30f);

        // Cleanup
        Object.DestroyImmediate(skill);
    }

    #endregion

    #region Skill Upgrade Tests

    [Test]
    public void SkillData_HasLevels()
    {
        // Arrange
        var skill = ScriptableObject.CreateInstance<SkillData>();
        SetField(skill, "currentLevel", 1);
        SetField(skill, "maxLevel", 10);

        // Assert
        Assert.AreEqual(1, skill.currentLevel);
        Assert.AreEqual(10, skill.maxLevel);

        // Cleanup
        Object.DestroyImmediate(skill);
    }

    [Test]
    public void SkillData_CanLevelUp()
    {
        // Arrange
        var skill = ScriptableObject.CreateInstance<SkillData>();
        SetField(skill, "currentLevel", 1);
        SetField(skill, "maxLevel", 10);
        SetField(skill, "damagePerLevel", 5f);

        // Act
        bool canLevel = skill.CanLevelUp();

        // Assert
        Assert.IsTrue(canLevel);

        // Cleanup
        Object.DestroyImmediate(skill);
    }

    #endregion
}
