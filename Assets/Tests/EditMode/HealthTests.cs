using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Unit tests for the Health component.
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
        SetPrivateField(_health, "_destroyOnDeath", false);
    }

    private void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(obj, value);
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
        _health.TakeDamage(10f, DamageType.Physical);

        // Assert
        Assert.Less(_health.CurrentHealth, initialHealth);
    }

    [Test]
    public void TakeDamage_WhenHealthReachesZero_IsDead()
    {
        // Arrange - damage more than max health
        float damage = _health.MaxHealth + 10f;

        // Act
        _health.TakeDamage(damage, DamageType.Physical);

        // Assert
        Assert.IsTrue(_health.IsDead);
        Assert.AreEqual(0, _health.CurrentHealth);
    }

    [Test]
    public void Heal_RestoresHealth()
    {
        // Arrange
        _health.TakeDamage(50f, DamageType.Physical);
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
        _health.TakeDamage(_health.MaxHealth, DamageType.Physical); // Kill
        Assert.IsTrue(_health.IsDead);

        // Act
        _health.TakeDamage(100f, DamageType.Physical); // Try to damage again

        // Assert
        Assert.AreEqual(0, _health.CurrentHealth); // Still 0
    }

    [Test]
    public void ResetHealth_RestoresToMax()
    {
        // Arrange
        _health.TakeDamage(50f, DamageType.Physical);

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
        _health.TakeDamage(maxHealth / 2, DamageType.Physical); // 50% damage

        // Assert
        Assert.AreEqual(0.5f, _health.HealthPercent, 0.01f);
    }
}
