using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests unitaires pour le systeme d'IA ennemie.
/// </summary>
public class EnemyAISystemTests
{
    #region EnemyType Tests

    [Test]
    public void EnemyType_HasAllTypes()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(EnemyType), "Basic"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(EnemyType), "Elite"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(EnemyType), "Boss"));
    }

    [Test]
    public void AIBehavior_HasAllBehaviors()
    {
        // Assert
        Assert.IsTrue(System.Enum.IsDefined(typeof(AIBehavior), "Aggressive"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(AIBehavior), "Defensive"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(AIBehavior), "Cowardly"));
        Assert.IsTrue(System.Enum.IsDefined(typeof(AIBehavior), "Ranged"));
    }

    #endregion

    #region EnemyData Tests

    [Test]
    public void EnemyData_CanBeCreated()
    {
        // Act
        var data = ScriptableObject.CreateInstance<EnemyData>();

        // Assert
        Assert.IsNotNull(data);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    [Test]
    public void EnemyData_HasStats()
    {
        // Arrange
        var data = ScriptableObject.CreateInstance<EnemyData>();
        SetField(data, "maxHealth", 100f);
        SetField(data, "attackDamage", 20f);
        SetField(data, "defense", 10f);

        // Assert
        Assert.AreEqual(100f, data.maxHealth);
        Assert.AreEqual(20f, data.attackDamage);
        Assert.AreEqual(10f, data.defense);

        // Cleanup
        Object.DestroyImmediate(data);
    }

    private void SetField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        field?.SetValue(obj, value);
    }

    #endregion

    #region AggroSystem Tests

    [Test]
    public void AggroSystem_CanBeCreated()
    {
        // Arrange
        var go = new GameObject();

        // Act
        var aggro = go.AddComponent<AggroSystem>();

        // Assert
        Assert.IsNotNull(aggro);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    [Test]
    public void AggroSystem_AddsThreat()
    {
        // Arrange
        var go = new GameObject();
        var aggro = go.AddComponent<AggroSystem>();
        var target = new GameObject("Target");

        // Act
        aggro.AddThreat(target, 50f);

        // Assert
        Assert.AreEqual(50f, aggro.GetThreat(target));

        // Cleanup
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void AggroSystem_TracksHighestThreat()
    {
        // Arrange
        var go = new GameObject();
        var aggro = go.AddComponent<AggroSystem>();
        var target1 = new GameObject("Target1");
        var target2 = new GameObject("Target2");

        // Act
        aggro.AddThreat(target1, 50f);
        aggro.AddThreat(target2, 100f);

        // Assert
        Assert.AreEqual(target2, aggro.GetHighestThreatTarget());

        // Cleanup
        Object.DestroyImmediate(target1);
        Object.DestroyImmediate(target2);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void AggroSystem_ClearsThreat()
    {
        // Arrange
        var go = new GameObject();
        var aggro = go.AddComponent<AggroSystem>();
        var target = new GameObject("Target");
        aggro.AddThreat(target, 50f);

        // Act
        aggro.ClearThreat(target);

        // Assert
        Assert.AreEqual(0f, aggro.GetThreat(target));

        // Cleanup
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(go);
    }

    #endregion

    #region AttackPattern Tests

    [Test]
    public void AttackPattern_CanBeCreated()
    {
        // Act
        var pattern = ScriptableObject.CreateInstance<AttackPattern>();

        // Assert
        Assert.IsNotNull(pattern);

        // Cleanup
        Object.DestroyImmediate(pattern);
    }

    [Test]
    public void AttackPattern_HasAttacks()
    {
        // Arrange
        var pattern = ScriptableObject.CreateInstance<AttackPattern>();
        var attack = ScriptableObject.CreateInstance<AttackData>();
        SetField(pattern, "attacks", new AttackData[] { attack });

        // Act
        var attacks = pattern.attacks;

        // Assert
        Assert.IsNotNull(attacks);
        Assert.AreEqual(1, attacks.Length);

        // Cleanup
        Object.DestroyImmediate(attack);
        Object.DestroyImmediate(pattern);
    }

    #endregion

    #region EnemySpawner Tests

    [Test]
    public void EnemySpawner_CanBeCreated()
    {
        // Arrange
        var go = new GameObject();

        // Act
        var spawner = go.AddComponent<EnemySpawner>();

        // Assert
        Assert.IsNotNull(spawner);

        // Cleanup
        Object.DestroyImmediate(go);
    }

    #endregion
}
