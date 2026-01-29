using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests unitaires pour le système de Dash avec i-frames.
/// </summary>
public class DashAbilityTests
{
    private GameObject _testObject;
    private DashAbility _dashAbility;
    private Rigidbody _rigidbody;

    [SetUp]
    public void Setup()
    {
        _testObject = new GameObject("TestPlayer");
        _rigidbody = _testObject.AddComponent<Rigidbody>();
        _rigidbody.useGravity = false;
        _dashAbility = _testObject.AddComponent<DashAbility>();
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_testObject);
    }

    #region Dash Execution Tests

    [Test]
    public void TryDash_WhenReady_ReturnsTrue()
    {
        // Arrange
        Vector2 direction = new Vector2(1f, 0f);

        // Act
        bool result = _dashAbility.TryDash(direction);

        // Assert
        Assert.IsTrue(result);
    }

    [Test]
    public void TryDash_WhenOnCooldown_ReturnsFalse()
    {
        // Arrange
        Vector2 direction = new Vector2(1f, 0f);
        _dashAbility.TryDash(direction); // First dash

        // Act
        bool result = _dashAbility.TryDash(direction); // Second dash immediately

        // Assert
        Assert.IsFalse(result);
    }

    [Test]
    public void TryDash_WhenAlreadyDashing_ReturnsFalse()
    {
        // Arrange
        Vector2 direction = new Vector2(1f, 0f);
        _dashAbility.TryDash(direction);

        // Act - Try to dash again while still dashing
        bool result = _dashAbility.TryDash(direction);

        // Assert
        Assert.IsFalse(result);
    }

    [Test]
    public void TryDash_SetsIsDashingTrue()
    {
        // Arrange
        Vector2 direction = new Vector2(1f, 0f);

        // Act
        _dashAbility.TryDash(direction);

        // Assert
        Assert.IsTrue(_dashAbility.IsDashing);
    }

    [Test]
    public void TryDash_WithZeroDirection_UsesFacingDirection()
    {
        // Arrange
        _testObject.transform.forward = Vector3.right;
        Vector2 zeroDirection = Vector2.zero;

        // Act
        bool result = _dashAbility.TryDash(zeroDirection);

        // Assert
        Assert.IsTrue(result);
    }

    #endregion

    #region I-Frames Tests

    [Test]
    public void TryDash_SetsIsInvincibleTrue()
    {
        // Arrange
        Vector2 direction = new Vector2(1f, 0f);

        // Act
        _dashAbility.TryDash(direction);

        // Assert
        Assert.IsTrue(_dashAbility.IsInvincible);
    }

    [Test]
    public void IsInvincible_WhenNotDashing_IsFalse()
    {
        // Assert
        Assert.IsFalse(_dashAbility.IsInvincible);
    }

    #endregion

    #region Cooldown Tests

    [Test]
    public void CooldownRemaining_AfterDash_IsGreaterThanZero()
    {
        // Arrange
        Vector2 direction = new Vector2(1f, 0f);

        // Act
        _dashAbility.TryDash(direction);

        // Assert
        Assert.Greater(_dashAbility.CooldownRemaining, 0f);
    }

    [Test]
    public void CooldownRemaining_WhenReady_IsZero()
    {
        // Assert
        Assert.AreEqual(0f, _dashAbility.CooldownRemaining);
    }

    [Test]
    public void CanDash_WhenReady_IsTrue()
    {
        // Assert
        Assert.IsTrue(_dashAbility.CanDash);
    }

    [Test]
    public void CanDash_WhenOnCooldown_IsFalse()
    {
        // Arrange
        _dashAbility.TryDash(Vector2.right);

        // Assert
        Assert.IsFalse(_dashAbility.CanDash);
    }

    #endregion

    #region Direction Tests

    [Test]
    public void TryDash_WithRightDirection_MovesRight()
    {
        // Arrange
        Vector3 initialPosition = _testObject.transform.position;
        Vector2 direction = new Vector2(1f, 0f);

        // Act
        _dashAbility.TryDash(direction);
        Vector3 dashDirection = _dashAbility.CurrentDashDirection;

        // Assert
        Assert.Greater(dashDirection.x, 0f);
    }

    [Test]
    public void TryDash_WithDiagonalDirection_NormalizesDirection()
    {
        // Arrange
        Vector2 direction = new Vector2(1f, 1f);

        // Act
        _dashAbility.TryDash(direction);
        Vector3 dashDirection = _dashAbility.CurrentDashDirection;

        // Assert
        Assert.AreEqual(1f, dashDirection.magnitude, 0.01f);
    }

    #endregion

    #region Stamina Integration Tests

    [Test]
    public void StaminaCost_IsConfigurable()
    {
        // Assert
        Assert.Greater(_dashAbility.StaminaCost, 0f);
    }

    #endregion

    #region Events Tests

    [Test]
    public void OnDashStarted_FiresWhenDashing()
    {
        // Arrange
        bool eventFired = false;
        _dashAbility.OnDashStarted += () => eventFired = true;

        // Act
        _dashAbility.TryDash(Vector2.right);

        // Assert
        Assert.IsTrue(eventFired);
    }

    [Test]
    public void OnDashStarted_DoesNotFireWhenCannotDash()
    {
        // Arrange
        _dashAbility.TryDash(Vector2.right); // First dash
        bool eventFired = false;
        _dashAbility.OnDashStarted += () => eventFired = true;

        // Act
        _dashAbility.TryDash(Vector2.right); // Try again

        // Assert
        Assert.IsFalse(eventFired);
    }

    #endregion
}
