using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests unitaires pour le système de caméra.
/// Note: Les tests Cinemachine complets nécessitent PlayMode.
/// </summary>
public class CameraControllerTests
{
    private GameObject _cameraObject;
    private CameraController _cameraController;
    private GameObject _playerObject;

    [SetUp]
    public void Setup()
    {
        _cameraObject = new GameObject("TestCamera");
        _cameraObject.AddComponent<Camera>();
        _cameraController = _cameraObject.AddComponent<CameraController>();

        _playerObject = new GameObject("Player");
        _playerObject.tag = "Player";
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(_cameraObject);
        Object.DestroyImmediate(_playerObject);
    }

    #region Camera Mode Tests

    [Test]
    public void CurrentMode_DefaultIsExploration()
    {
        // Assert
        Assert.AreEqual(CameraMode.Exploration, _cameraController.CurrentMode);
    }

    [Test]
    public void SetCameraMode_ChangesModeToComba()
    {
        // Act
        _cameraController.SetCameraMode(CameraMode.Combat);

        // Assert
        Assert.AreEqual(CameraMode.Combat, _cameraController.CurrentMode);
    }

    [Test]
    public void SetCameraMode_ChangesModeTocinematic()
    {
        // Act
        _cameraController.SetCameraMode(CameraMode.Cinematic);

        // Assert
        Assert.AreEqual(CameraMode.Cinematic, _cameraController.CurrentMode);
    }

    [Test]
    public void SetCameraMode_FiresEventOnChange()
    {
        // Arrange
        CameraMode receivedMode = CameraMode.Exploration;
        _cameraController.OnCameraModeChanged += mode => receivedMode = mode;

        // Act
        _cameraController.SetCameraMode(CameraMode.Combat);

        // Assert
        Assert.AreEqual(CameraMode.Combat, receivedMode);
    }

    [Test]
    public void SetCameraMode_DoesNotFireEventIfSameMode()
    {
        // Arrange
        bool eventFired = false;
        _cameraController.OnCameraModeChanged += mode => eventFired = true;

        // Act
        _cameraController.SetCameraMode(CameraMode.Exploration); // Same as default

        // Assert
        Assert.IsFalse(eventFired);
    }

    #endregion

    #region Lock-On Tests

    [Test]
    public void IsLockedOn_DefaultIsFalse()
    {
        // Assert
        Assert.IsFalse(_cameraController.IsLockedOn);
    }

    [Test]
    public void CurrentLockTarget_DefaultIsNull()
    {
        // Assert
        Assert.IsNull(_cameraController.CurrentLockTarget);
    }

    [Test]
    public void TryLockOn_WhenNoTargetsAvailable_ReturnsFalse()
    {
        // Act
        bool result = _cameraController.TryLockOn();

        // Assert
        Assert.IsFalse(result);
        Assert.IsFalse(_cameraController.IsLockedOn);
    }

    #endregion

    #region Settings Tests

    [Test]
    public void Settings_IsNotNull()
    {
        // Assert
        Assert.IsNotNull(_cameraController.Settings);
    }

    [Test]
    public void UpdateSensitivity_ClampsValues()
    {
        // Act
        _cameraController.UpdateSensitivity(0.05f, 15f);

        // Assert
        Assert.GreaterOrEqual(_cameraController.Settings.horizontalSensitivity, 0.1f);
        Assert.LessOrEqual(_cameraController.Settings.verticalSensitivity, 10f);
    }

    [Test]
    public void SetInvertY_UpdatesSettings()
    {
        // Act
        _cameraController.SetInvertY(true);

        // Assert
        Assert.IsTrue(_cameraController.Settings.invertY);
    }

    #endregion
}

/// <summary>
/// Tests pour CameraSettings ScriptableObject.
/// </summary>
public class CameraSettingsTests
{
    [Test]
    public void CameraSettings_CanBeCreated()
    {
        // Act
        var settings = ScriptableObject.CreateInstance<CameraSettings>();

        // Assert
        Assert.IsNotNull(settings);

        // Cleanup
        Object.DestroyImmediate(settings);
    }

    [Test]
    public void CameraSettings_HasDefaultValues()
    {
        // Arrange
        var settings = ScriptableObject.CreateInstance<CameraSettings>();

        // Assert
        Assert.Greater(settings.explorationDistance, 0f);
        Assert.Greater(settings.combatDistance, 0f);
        Assert.Greater(settings.horizontalSensitivity, 0f);
        Assert.Greater(settings.verticalSensitivity, 0f);

        // Cleanup
        Object.DestroyImmediate(settings);
    }

    [Test]
    public void CameraSettings_LockOnDistanceIsPositive()
    {
        // Arrange
        var settings = ScriptableObject.CreateInstance<CameraSettings>();

        // Assert
        Assert.Greater(settings.lockOnMaxDistance, 0f);

        // Cleanup
        Object.DestroyImmediate(settings);
    }
}
