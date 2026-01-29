using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Type de notification.
/// </summary>
public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error,
    Achievement,
    Quest,
    Item
}

/// <summary>
/// Donnees d'une notification.
/// </summary>
[Serializable]
public class NotificationData
{
    /// <summary>
    /// Message de la notification.
    /// </summary>
    public string message;

    /// <summary>
    /// Type de notification.
    /// </summary>
    public NotificationType type;

    /// <summary>
    /// Duree d'affichage en secondes.
    /// </summary>
    public float duration;

    /// <summary>
    /// Icone optionnelle.
    /// </summary>
    public Sprite icon;

    /// <summary>
    /// Titre optionnel.
    /// </summary>
    public string title;

    /// <summary>
    /// Duree par defaut pour chaque type.
    /// </summary>
    public static float GetDefaultDuration(NotificationType type)
    {
        return type switch
        {
            NotificationType.Info => 3f,
            NotificationType.Success => 3f,
            NotificationType.Warning => 4f,
            NotificationType.Error => 5f,
            NotificationType.Achievement => 5f,
            NotificationType.Quest => 4f,
            NotificationType.Item => 2f,
            _ => 3f
        };
    }

    /// <summary>
    /// Couleur associee a chaque type.
    /// </summary>
    public static Color GetColor(NotificationType type)
    {
        return type switch
        {
            NotificationType.Info => Color.white,
            NotificationType.Success => new Color(0.2f, 0.8f, 0.2f),
            NotificationType.Warning => new Color(1f, 0.8f, 0f),
            NotificationType.Error => new Color(1f, 0.3f, 0.3f),
            NotificationType.Achievement => new Color(1f, 0.8f, 0f),
            NotificationType.Quest => new Color(0.3f, 0.7f, 1f),
            NotificationType.Item => new Color(0.7f, 0.7f, 0.7f),
            _ => Color.white
        };
    }

    public NotificationData(string message, NotificationType type, float duration = -1f)
    {
        this.message = message;
        this.type = type;
        this.duration = duration > 0 ? duration : GetDefaultDuration(type);
    }

    public NotificationData(string title, string message, NotificationType type, float duration = -1f)
        : this(message, type, duration)
    {
        this.title = title;
    }
}

/// <summary>
/// Composant d'affichage d'une notification individuelle.
/// </summary>
public class UINotification : MonoBehaviour
{
    #region Serialized Fields

    [SerializeField] private TextMeshProUGUI _messageText;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private Image _iconImage;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Animation")]
#pragma warning disable CS0414 // Field is assigned but never used - reserved for animation
    [SerializeField] private float _fadeInDuration = 0.3f;
    [SerializeField] private float _fadeOutDuration = 0.3f;
#pragma warning restore CS0414

    #endregion

    #region Private Fields

    private NotificationData _data;
    private float _timer;
    private bool _isFadingOut;

    #endregion

    #region Events

    public event Action<UINotification> OnComplete;

    #endregion

    #region Public Methods

    /// <summary>
    /// Initialise la notification avec les donnees.
    /// </summary>
    public void Setup(NotificationData data)
    {
        _data = data;
        _timer = data.duration;
        _isFadingOut = false;

        if (_messageText != null)
            _messageText.text = data.message;

        if (_titleText != null)
        {
            _titleText.text = data.title;
            _titleText.gameObject.SetActive(!string.IsNullOrEmpty(data.title));
        }

        if (_iconImage != null)
        {
            _iconImage.sprite = data.icon;
            _iconImage.gameObject.SetActive(data.icon != null);
        }

        if (_backgroundImage != null)
        {
            _backgroundImage.color = NotificationData.GetColor(data.type);
        }

        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;

        // Demarrer le fade in
        StartFadeIn();
    }

    #endregion

    #region Private Methods

    private void Update()
    {
        if (_isFadingOut) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0)
        {
            StartFadeOut();
        }
    }

    private void StartFadeIn()
    {
        // Animation simplifiee sans coroutine pour EditMode
        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;
    }

    private void StartFadeOut()
    {
        _isFadingOut = true;
        // Animation simplifiee
        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;

        Complete();
    }

    private void Complete()
    {
        OnComplete?.Invoke(this);
        Destroy(gameObject);
    }

    #endregion
}
