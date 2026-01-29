using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests unitaires pour le systeme d'IA des creatures.
/// </summary>
public class CreatureAITests
{
    #region CreatureAI Tests

    private GameObject _testObject;
    private CreatureAI _creatureAI;

    [SetUp]
    public void Setup()
    {
        _testObject = new GameObject("TestCreatureAI");
        _testObject.AddComponent<Rigidbody>();
        _creatureAI = _testObject.AddComponent<CreatureAI>();

        // Appeler Awake
        var awakeMethod = typeof(CreatureAI).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(_creatureAI, null);
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_testObject);
    }

    [Test]
    public void CreatureAI_CanBeAdded()
    {
        // Assert
        Assert.IsNotNull(_creatureAI);
    }

    [Test]
    public void CreatureAI_StartsInIdleState()
    {
        // Assert
        Assert.AreEqual(CreatureAIState.Idle, _creatureAI.CurrentState);
    }

    [Test]
    public void CreatureAI_HasFollowDistance()
    {
        // Arrange
        var field = typeof(CreatureAI).GetField("_followDistance",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        float distance = (float)field.GetValue(_creatureAI);

        // Assert
        Assert.Greater(distance, 0f);
    }

    [Test]
    public void CreatureAI_HasAttackRange()
    {
        // Arrange
        var field = typeof(CreatureAI).GetField("_attackRange",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        float range = (float)field.GetValue(_creatureAI);

        // Assert
        Assert.Greater(range, 0f);
    }

    [Test]
    public void CreatureAI_CanSetOwner()
    {
        // Arrange
        var owner = new GameObject("Owner");

        // Act
        _creatureAI.SetOwner(owner.transform);

        // Assert
        Assert.AreEqual(owner.transform, _creatureAI.Owner);

        // Cleanup
        Object.DestroyImmediate(owner);
    }

    [Test]
    public void CreatureAI_CanProcessCommand()
    {
        // Act & Assert - ne doit pas lever d'exception
        Assert.DoesNotThrow(() => _creatureAI.ProcessCommand(CreatureCommand.Stay));
        Assert.DoesNotThrow(() => _creatureAI.ProcessCommand(CreatureCommand.Follow));
        Assert.DoesNotThrow(() => _creatureAI.ProcessCommand(CreatureCommand.Attack));
    }

    #endregion

    #region CreatureBehavior Tests

    [Test]
    public void CreatureBehavior_HasAllBehaviors()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(CreatureBehavior), "Passive"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CreatureBehavior), "Defensive"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CreatureBehavior), "Aggressive"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CreatureBehavior), "Support"));
    }

    #endregion

    #region MountSystem Tests

    private GameObject _mountObject;
    private MountSystem _mountSystem;

    [Test]
    public void MountSystem_CanBeAdded()
    {
        // Arrange
        _mountObject = new GameObject("MountTest");
        _mountSystem = _mountObject.AddComponent<MountSystem>();

        // Assert
        Assert.IsNotNull(_mountSystem);

        // Cleanup
        Object.DestroyImmediate(_mountObject);
    }

    [Test]
    public void MountSystem_HasMountPoint()
    {
        // Arrange
        _mountObject = new GameObject("MountTest");
        _mountSystem = _mountObject.AddComponent<MountSystem>();
        var mountPoint = new GameObject("MountPoint");
        mountPoint.transform.SetParent(_mountObject.transform);

        SetField(_mountSystem, "_mountPoint", mountPoint.transform);

        // Act
        var field = typeof(MountSystem).GetField("_mountPoint",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var point = (Transform)field.GetValue(_mountSystem);

        // Assert
        Assert.IsNotNull(point);

        // Cleanup
        Object.DestroyImmediate(_mountObject);
    }

    [Test]
    public void MountSystem_CanCheckMountable()
    {
        // Arrange
        _mountObject = new GameObject("MountTest");
        _mountObject.AddComponent<Rigidbody>();
        var controller = _mountObject.AddComponent<CreatureController>();

        var data = ScriptableObject.CreateInstance<CreatureData>();
        // Utiliser les champs publics directement
        data.canBeMounted = true;
        data.size = CreatureSize.Large;
        data.baseHealth = 100f;
        data.maxLevel = 100;

        var instance = new CreatureInstance(data, 10);

        // Appeler Awake du controller
        var controllerAwake = typeof(CreatureController).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        controllerAwake?.Invoke(controller, null);

        controller.Initialize(instance, true);

        _mountSystem = _mountObject.AddComponent<MountSystem>();

        // Appeler Awake du MountSystem - cela va detecter le controller
        var awakeMethod = typeof(MountSystem).GetMethod("Awake",
            BindingFlags.NonPublic | BindingFlags.Instance);
        awakeMethod?.Invoke(_mountSystem, null);

        // Mettre le lastMountTime dans le passe pour eviter le cooldown
        SetField(_mountSystem, "_lastMountTime", -100f);

        // Act
        bool canMount = _mountSystem.CanBeMounted();

        // Assert - La creature PEUT etre montee
        Assert.IsTrue(canMount, "La creature devrait pouvoir etre montee");

        // Cleanup
        Object.DestroyImmediate(data);
        Object.DestroyImmediate(_mountObject);
    }

    #endregion

    #region CreatureCommand Tests

    [Test]
    public void CreatureCommand_HasAllCommands()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(CreatureCommand), "Follow"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CreatureCommand), "Stay"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CreatureCommand), "Attack"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CreatureCommand), "Return"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(CreatureCommand), "UseAbility"));
    }

    #endregion

    #region Helper Methods

    private void SetField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        field?.SetValue(obj, value);
    }

    #endregion
}
