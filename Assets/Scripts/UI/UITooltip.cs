using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Donnees d'un tooltip.
/// </summary>
[Serializable]
public class TooltipData
{
    /// <summary>
    /// Titre du tooltip.
    /// </summary>
    public string title;

    /// <summary>
    /// Description du tooltip.
    /// </summary>
    public string description;

    /// <summary>
    /// Icone optionnelle.
    /// </summary>
    public Sprite icon;

    /// <summary>
    /// Rarete (pour les items).
    /// </summary>
    public ItemRarity rarity = ItemRarity.Common;

    /// <summary>
    /// Sous-titre optionnel.
    /// </summary>
    public string subtitle;

    /// <summary>
    /// Texte supplementaire (stats, effets, etc.).
    /// </summary>
    public string additionalInfo;

    public TooltipData(string title, string description)
    {
        this.title = title;
        this.description = description;
    }

    public TooltipData(string title, string description, Sprite icon)
        : this(title, description)
    {
        this.icon = icon;
    }

    public TooltipData(string title, string description, ItemRarity rarity)
        : this(title, description)
    {
        this.rarity = rarity;
    }
}

/// <summary>
/// Composant d'affichage de tooltip.
/// </summary>
public class UITooltip : MonoBehaviour
{
    #region Singleton

    private static UITooltip _instance;
    public static UITooltip Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<UITooltip>();
            }
            return _instance;
        }
    }

    #endregion

    #region Serialized Fields

    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _subtitleText;
    [SerializeField] private TextMeshProUGUI _additionalInfoText;
    [SerializeField] private Image _iconImage;
    [SerializeField] private Image _rarityBorder;
    [SerializeField] private RectTransform _tooltipRect;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Settings")]
    [SerializeField] private Vector2 _offset = new Vector2(10f, 10f);
    [SerializeField] private float _showDelay = 0.5f;
    [SerializeField] private bool _followMouse = true;

    #endregion

    #region Private Fields

    private TooltipData _currentData;
    private bool _isVisible;
    private float _showTimer;
    private bool _isWaiting;
    private Canvas _parentCanvas;

    #endregion

    #region Properties

    public bool IsVisible => _isVisible;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        _parentCanvas = GetComponentInParent<Canvas>();
        Hide();
    }

    private void Update()
    {
        if (_isWaiting)
        {
            _showTimer -= Time.deltaTime;
            if (_showTimer <= 0)
            {
                ShowImmediate();
            }
        }

        if (_isVisible && _followMouse)
        {
            UpdatePosition();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Prepare l'affichage du tooltip apres le delai.
    /// </summary>
    public void Show(TooltipData data)
    {
        _currentData = data;
        _showTimer = _showDelay;
        _isWaiting = true;
    }

    /// <summary>
    /// Affiche immediatement le tooltip.
    /// </summary>
    public void ShowImmediate(TooltipData data)
    {
        _currentData = data;
        ShowImmediate();
    }

    /// <summary>
    /// Cache le tooltip.
    /// </summary>
    public void Hide()
    {
        _isVisible = false;
        _isWaiting = false;

        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;

        gameObject.SetActive(false);
    }

    #endregion

    #region Private Methods

    private void ShowImmediate()
    {
        if (_currentData == null) return;

        _isWaiting = false;
        _isVisible = true;
        gameObject.SetActive(true);

        // Mettre a jour le contenu
        if (_titleText != null)
        {
            _titleText.text = _currentData.title;
            _titleText.color = ItemData.GetRarityColor(_currentData.rarity);
        }

        if (_descriptionText != null)
            _descriptionText.text = _currentData.description;

        if (_subtitleText != null)
        {
            _subtitleText.text = _currentData.subtitle;
            _subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(_currentData.subtitle));
        }

        if (_additionalInfoText != null)
        {
            _additionalInfoText.text = _currentData.additionalInfo;
            _additionalInfoText.gameObject.SetActive(!string.IsNullOrEmpty(_currentData.additionalInfo));
        }

        if (_iconImage != null)
        {
            _iconImage.sprite = _currentData.icon;
            _iconImage.gameObject.SetActive(_currentData.icon != null);
        }

        if (_rarityBorder != null)
        {
            _rarityBorder.color = ItemData.GetRarityColor(_currentData.rarity);
        }

        if (_canvasGroup != null)
            _canvasGroup.alpha = 1f;

        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (_tooltipRect == null || _parentCanvas == null) return;

        Vector2 mousePos = Input.mousePosition;
        Vector2 tooltipPos = mousePos + _offset;

        // S'assurer que le tooltip reste dans l'ecran
        Vector2 tooltipSize = _tooltipRect.sizeDelta;
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        if (tooltipPos.x + tooltipSize.x > screenWidth)
            tooltipPos.x = mousePos.x - tooltipSize.x - _offset.x;

        if (tooltipPos.y + tooltipSize.y > screenHeight)
            tooltipPos.y = mousePos.y - tooltipSize.y - _offset.y;

        if (tooltipPos.x < 0)
            tooltipPos.x = _offset.x;

        if (tooltipPos.y < 0)
            tooltipPos.y = _offset.y;

        _tooltipRect.position = tooltipPos;
    }

    #endregion
}
