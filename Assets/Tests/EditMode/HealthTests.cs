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
        _health.TakeDamage(10f);

        // Assert
        Assert.Less(_health.CurrentHealth, initialHealth);
    }

    [Test]
    public void TakeDamage_WhenHealthReachesZero_IsDead()
    {
        // Arrange - damage more than max health
        float damage = _health.MaxHealth + 10f;

        // Act
        _health.TakeDamage(damage);

        // Assert
        Assert.IsTrue(_health.IsDead);
        Assert.AreEqual(0, _health.CurrentHealth);
    }

    [Test]
    public void Heal_RestoresHealth()
    {
        // Arrange
        _health.TakeDamage(50f);
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
        _health.TakeDamage(_health.MaxHealth); // Kill
        Assert.IsTrue(_health.IsDead);

        // Act
        _health.TakeDamage(100f); // Try to damage again

        // Assert
        Assert.AreEqual(0, _health.CurrentHealth); // Still 0
    }

    [Test]
    public void ResetHealth_RestoresToMax()
    {
        // Arrange
        _health.TakeDamage(50f);

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
        _health.TakeDamage(maxHealth / 2); // 50% damage

        // Assert
        Assert.AreEqual(0.5f, _health.HealthPercent, 0.01f);
    }
}
