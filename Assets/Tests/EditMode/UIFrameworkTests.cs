using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tests unitaires pour le framework UI.
/// </summary>
public class UIManagerTests
{
    private GameObject _testObject;
    private UIManager _uiManager;

    [SetUp]
    public void Setup()
    {
        _testObject = new GameObject("TestUIManager");
        _uiManager = _testObject.AddComponent<UIManager>();
    }

    [TearDown]
    public void Teardown()
    {
        UnityEngine.Object.DestroyImmediate(_testObject);
    }

    #region Panel Registration Tests

    [Test]
    public void RegisterPanel_AddsPanel()
    {
        // Arrange
        var panelObject = new GameObject("TestPanel");
        var panel = panelObject.AddComponent<TestUIPanel>();

        // Act
        _uiManager.RegisterPanel("test", panel);

        // Assert
        Assert.IsTrue(_uiManager.HasPanel("test"));

        UnityEngine.Object.DestroyImmediate(panelObject);
    }

    [Test]
    public void UnregisterPanel_RemovesPanel()
    {
        // Arrange
        var panelObject = new GameObject("TestPanel");
        var panel = panelObject.AddComponent<TestUIPanel>();
        _uiManager.RegisterPanel("test", panel);

        // Act
        _uiManager.UnregisterPanel("test");

        // Assert
        Assert.IsFalse(_uiManager.HasPanel("test"));

        UnityEngine.Object.DestroyImmediate(panelObject);
    }

    [Test]
    public void GetPanel_ReturnsRegisteredPanel()
    {
        // Arrange
        var panelObject = new GameObject("TestPanel");
        var panel = panelObject.AddComponent<TestUIPanel>();
        _uiManager.RegisterPanel("test", panel);

        // Act
        var retrieved = _uiManager.GetPanel<TestUIPanel>("test");

        // Assert
        Assert.AreEqual(panel, retrieved);

        UnityEngine.Object.DestroyImmediate(panelObject);
    }

    [Test]
    public void GetPanel_ReturnsNull_WhenNotRegistered()
    {
        // Act
        var retrieved = _uiManager.GetPanel<TestUIPanel>("nonexistent");

        // Assert
        Assert.IsNull(retrieved);
    }

    #endregion

    #region Panel Stack Tests

    [Test]
    public void PushPanel_AddsToStack()
    {
        // Arrange
        var panelObject = new GameObject("TestPanel");
        var panel = panelObject.AddComponent<TestUIPanel>();
        _uiManager.RegisterPanel("test", panel);

        // Act
        _uiManager.PushPanel("test");

        // Assert
        Assert.AreEqual(1, _uiManager.PanelStackCount);

        UnityEngine.Object.DestroyImmediate(panelObject);
    }

    [Test]
    public void PopPanel_RemovesFromStack()
    {
        // Arrange
        var panelObject = new GameObject("TestPanel");
        var panel = panelObject.AddComponent<TestUIPanel>();
        _uiManager.RegisterPanel("test", panel);
        _uiManager.PushPanel("test");

        // Act
        _uiManager.PopPanel();

        // Assert
        Assert.AreEqual(0, _uiManager.PanelStackCount);

        UnityEngine.Object.DestroyImmediate(panelObject);
    }

    [Test]
    public void PopAllPanels_ClearsStack()
    {
        // Arrange
        var panelObject1 = new GameObject("Panel1");
        var panel1 = panelObject1.AddComponent<TestUIPanel>();
        var panelObject2 = new GameObject("Panel2");
        var panel2 = panelObject2.AddComponent<TestUIPanel>();
        _uiManager.RegisterPanel("panel1", panel1);
        _uiManager.RegisterPanel("panel2", panel2);
        _uiManager.PushPanel("panel1");
        _uiManager.PushPanel("panel2");

        // Act
        _uiManager.PopAllPanels();

        // Assert
        Assert.AreEqual(0, _uiManager.PanelStackCount);

        UnityEngine.Object.DestroyImmediate(panelObject1);
        UnityEngine.Object.DestroyImmediate(panelObject2);
    }

    [Test]
    public void CurrentPanel_ReturnsTopPanel()
    {
        // Arrange
        var panelObject1 = new GameObject("Panel1");
        var panel1 = panelObject1.AddComponent<TestUIPanel>();
        var panelObject2 = new GameObject("Panel2");
        var panel2 = panelObject2.AddComponent<TestUIPanel>();
        _uiManager.RegisterPanel("panel1", panel1);
        _uiManager.RegisterPanel("panel2", panel2);
        _uiManager.PushPanel("panel1");
        _uiManager.PushPanel("panel2");

        // Act
        var current = _uiManager.CurrentPanel;

        // Assert
        Assert.AreEqual(panel2, current);

        UnityEngine.Object.DestroyImmediate(panelObject1);
        UnityEngine.Object.DestroyImmediate(panelObject2);
    }

    #endregion

    #region UI State Tests

    [Test]
    public void IsUIOpen_ReturnsFalse_WhenNoPanel()
    {
        // Assert
        Assert.IsFalse(_uiManager.IsUIOpen);
    }

    [Test]
    public void IsUIOpen_ReturnsTrue_WhenPanelOpen()
    {
        // Arrange
        var panelObject = new GameObject("TestPanel");
        var panel = panelObject.AddComponent<TestUIPanel>();
        _uiManager.RegisterPanel("test", panel);
        _uiManager.PushPanel("test");

        // Assert
        Assert.IsTrue(_uiManager.IsUIOpen);

        UnityEngine.Object.DestroyImmediate(panelObject);
    }

    #endregion
}

/// <summary>
/// Tests pour UIPanel.
/// </summary>
public class UIPanelTests
{
    private GameObject _testObject;
    private TestUIPanel _panel;

    [SetUp]
    public void Setup()
    {
        _testObject = new GameObject("TestPanel");
        _panel = _testObject.AddComponent<TestUIPanel>();
    }

    [TearDown]
    public void Teardown()
    {
        UnityEngine.Object.DestroyImmediate(_testObject);
    }

    [Test]
    public void Show_ActivatesGameObject()
    {
        // Arrange
        _testObject.SetActive(false);

        // Act
        _panel.Show();

        // Assert
        Assert.IsTrue(_testObject.activeSelf);
    }

    [Test]
    public void Hide_DeactivatesGameObject()
    {
        // Arrange
        _testObject.SetActive(true);

        // Act
        _panel.Hide();

        // Assert
        Assert.IsFalse(_testObject.activeSelf);
    }

    [Test]
    public void IsVisible_ReturnsCorrectState()
    {
        // Arrange
        _testObject.SetActive(true);

        // Assert
        Assert.IsTrue(_panel.IsVisible);

        // Act
        _panel.Hide();

        // Assert
        Assert.IsFalse(_panel.IsVisible);
    }

    [Test]
    public void OnShow_EventFires()
    {
        // Arrange
        bool eventFired = false;
        _panel.OnPanelShown += () => eventFired = true;
        _testObject.SetActive(false);

        // Act
        _panel.Show();

        // Assert
        Assert.IsTrue(eventFired);
    }

    [Test]
    public void OnHide_EventFires()
    {
        // Arrange
        bool eventFired = false;
        _panel.OnPanelHidden += () => eventFired = true;
        _testObject.SetActive(true);

        // Act
        _panel.Hide();

        // Assert
        Assert.IsTrue(eventFired);
    }

    [Test]
    public void Toggle_FlipsVisibility()
    {
        // Arrange
        _testObject.SetActive(false);

        // Act & Assert
        _panel.Toggle();
        Assert.IsTrue(_panel.IsVisible);

        _panel.Toggle();
        Assert.IsFalse(_panel.IsVisible);
    }
}

/// <summary>
/// Tests pour UINotification.
/// </summary>
public class UINotificationTests
{
    [Test]
    public void NotificationData_StoresMessage()
    {
        // Arrange & Act
        var notification = new NotificationData("Test message", NotificationType.Info);

        // Assert
        Assert.AreEqual("Test message", notification.message);
        Assert.AreEqual(NotificationType.Info, notification.type);
    }

    [Test]
    public void NotificationData_DefaultDuration()
    {
        // Arrange & Act
        var notification = new NotificationData("Test", NotificationType.Info);

        // Assert
        Assert.Greater(notification.duration, 0);
    }

    [Test]
    public void NotificationData_CustomDuration()
    {
        // Arrange & Act
        var notification = new NotificationData("Test", NotificationType.Warning, 5f);

        // Assert
        Assert.AreEqual(5f, notification.duration);
    }
}

/// <summary>
/// Tests pour UITooltip.
/// </summary>
public class UITooltipTests
{
    [Test]
    public void TooltipData_StoresContent()
    {
        // Arrange & Act
        var tooltip = new TooltipData("Title", "Description");

        // Assert
        Assert.AreEqual("Title", tooltip.title);
        Assert.AreEqual("Description", tooltip.description);
    }

    [Test]
    public void TooltipData_SupportsIcon()
    {
        // Arrange & Act
        var tooltip = new TooltipData("Title", "Description");
        var testSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0, 0, 4, 4),
            Vector2.zero
        );
        tooltip.icon = testSprite;

        // Assert
        Assert.IsNotNull(tooltip.icon);

        UnityEngine.Object.DestroyImmediate(testSprite);
    }

    [Test]
    public void TooltipData_SupportsRarity()
    {
        // Arrange & Act
        var tooltip = new TooltipData("Epic Sword", "A legendary weapon");
        tooltip.rarity = ItemRarity.Epic;

        // Assert
        Assert.AreEqual(ItemRarity.Epic, tooltip.rarity);
    }
}

/// <summary>
/// Tests pour UIProgressBar.
/// </summary>
public class UIProgressBarTests
{
    private GameObject _testObject;
    private UIProgressBar _progressBar;

    [SetUp]
    public void Setup()
    {
        _testObject = new GameObject("TestProgressBar");
        _progressBar = _testObject.AddComponent<UIProgressBar>();
    }

    [TearDown]
    public void Teardown()
    {
        UnityEngine.Object.DestroyImmediate(_testObject);
    }

    [Test]
    public void SetProgress_ClampsValue()
    {
        // Act - Use immediate mode for EditMode tests
        _progressBar.SetProgress(-0.5f, immediate: true);

        // Assert
        Assert.AreEqual(0f, _progressBar.Progress);

        // Act
        _progressBar.SetProgress(1.5f, immediate: true);

        // Assert
        Assert.AreEqual(1f, _progressBar.Progress);
    }

    [Test]
    public void SetProgress_SetsCorrectValue()
    {
        // Act - Use immediate mode for EditMode tests
        _progressBar.SetProgress(0.5f, immediate: true);

        // Assert
        Assert.AreEqual(0.5f, _progressBar.Progress);
    }

    [Test]
    public void SetValue_CalculatesProgress()
    {
        // Act - Use immediate mode for EditMode tests
        _progressBar.SetValue(50f, 100f, immediate: true);

        // Assert
        Assert.AreEqual(0.5f, _progressBar.Progress);
    }

    [Test]
    public void SetValue_HandlesZeroMax()
    {
        // Act - Use immediate mode for EditMode tests
        _progressBar.SetValue(50f, 0f, immediate: true);

        // Assert
        Assert.AreEqual(0f, _progressBar.Progress);
    }

    [Test]
    public void OnProgressChanged_EventFires()
    {
        // Arrange
        float lastProgress = -1f;
        _progressBar.OnProgressChanged += (p) => lastProgress = p;

        // Act - Use immediate mode for EditMode tests
        _progressBar.SetProgress(0.75f, immediate: true);

        // Assert
        Assert.AreEqual(0.75f, lastProgress);
    }
}

/// <summary>
/// Panel de test pour les tests unitaires.
/// </summary>
public class TestUIPanel : UIPanel
{
    // Classe de test vide
}
