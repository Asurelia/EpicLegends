using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests unitaires pour le composant Health.
/// </summary>
public class HealthTests
{
    private GameObject _testObject;
    private Health _health;

    [SetUp]
    public void Setup()
    {
        _testObject = new GameObject("TestObject");
        _health = _testObject.AddComponent<Health>();

        // Initialiser les SerializeFields manuellement car Unity ne le fait pas en EditMode
        SetPrivateField(_health, "_maxHealth", 100f);
        SetPrivateField(_health, "_currentHealth", 100f);
        SetPrivateField(_health, "_defense", 0f);
        SetPrivateField(_health, "_destroyOnDeath", false);
    }

    private void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(obj, value);
    }

    private DamageInfo CreateDamage(float amount, DamageType type = DamageType.Physical)
    {
        return new DamageInfo
        {
            baseDamage = amount,
            damageType = type
        };
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_testObject);
    }

    [Test]
    public void TakeDamage_ReducesHealth()
    {
        // Arrange
        float initialHealth = _health.CurrentHealth;

        // Act
        _health.TakeDamage(CreateDamage(10f));

        // Assert
        Assert.Less(_health.CurrentHealth, initialHealth);
    }

    [Test]
    public void TakeDamage_WhenHealthReachesZero_IsDead()
    {
        // Arrange - degats superieurs a la vie max
        float damage = _health.MaxHealth + 10f;

        // Act
        _health.TakeDamage(CreateDamage(damage));

        // Assert
        Assert.IsTrue(_health.IsDead);
        Assert.AreEqual(0, _health.CurrentHealth);
    }

    [Test]
    public void Heal_RestoresHealth()
    {
        // Arrange
        _health.TakeDamage(CreateDamage(50f));
        float damagedHealth = _health.CurrentHealth;

        // Act
        _health.Heal(25f);

        // Assert
        Assert.Greater(_health.CurrentHealth, damagedHealth);
    }

    [Test]
    public void Heal_DoesNotExceedMaxHealth()
    {
        // Arrange
        float maxHealth = _health.MaxHealth;

        // Act
        _health.Heal(1000f);

        // Assert
        Assert.AreEqual(maxHealth, _health.CurrentHealth);
    }

    [Test]
    public void TakeDamage_WhenDead_DoesNothing()
    {
        // Arrange
        _health.TakeDamage(CreateDamage(_health.MaxHealth)); // Tuer
        Assert.IsTrue(_health.IsDead);

        // Act
        _health.TakeDamage(CreateDamage(100f)); // Essayer de faire des degats

        // Assert
        Assert.AreEqual(0, _health.CurrentHealth); // Toujours 0
    }

    [Test]
    public void ResetHealth_RestoresToMax()
    {
        // Arrange
        _health.TakeDamage(CreateDamage(50f));

        // Act
        _health.ResetHealth();

        // Assert
        Assert.AreEqual(_health.MaxHealth, _health.CurrentHealth);
    }

    [Test]
    public void HealthPercent_ReturnsCorrectValue()
    {
        // Arrange
        float maxHealth = _health.MaxHealth;

        // Act
        _health.TakeDamage(CreateDamage(maxHealth / 2)); // 50% degats

        // Assert
        Assert.AreEqual(0.5f, _health.HealthPercent, 0.01f);
    }

    [Test]
    public void TakeDamage_WithDamageInfo_AppliesCorrectDamage()
    {
        // Arrange
        var damageInfo = new DamageInfo
        {
            baseDamage = 25f,
            damageType = DamageType.Fire
        };

        // Act
        _health.TakeDamage(damageInfo);

        // Assert
        Assert.Less(_health.CurrentHealth, _health.MaxHealth);
    }

    [Test]
    public void TakeDamage_TrueDamage_IgnoresResistances()
    {
        // Arrange
        SetPrivateField(_health, "_physicalResistance", 0.5f);
        var trueDamage = CreateDamage(50f, DamageType.True);

        // Act
        _health.TakeDamage(trueDamage);

        // Assert - degats purs font 50 degats minimum
        Assert.LessOrEqual(_health.CurrentHealth, 50f);
    }
}
