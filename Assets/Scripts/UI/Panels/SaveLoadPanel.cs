using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel de sauvegarde et chargement.
/// Affiche les slots de sauvegarde et permet de sauvegarder/charger.
/// </summary>
public class SaveLoadPanel : UIPanel
{
    #region Serialized Fields

    [Header("Mode")]
    [SerializeField] private bool _isSaveMode = true;

    [Header("Slot Container")]
    [SerializeField] private Transform _slotContainer;
    [SerializeField] private GameObject _slotPrefab;

    [Header("Actions")]
    [SerializeField] private Button _actionButton;
    [SerializeField] private Button _deleteButton;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private TextMeshProUGUI _actionButtonText;

    [Header("Confirmation")]
    [SerializeField] private GameObject _confirmationPopup;
    [SerializeField] private TextMeshProUGUI _confirmationText;
    [SerializeField] private Button _confirmYesButton;
    [SerializeField] private Button _confirmNoButton;

    [Header("Loading Indicator")]
    [SerializeField] private GameObject _loadingIndicator;
    [SerializeField] private TextMeshProUGUI _loadingText;

    [Header("Audio")]
    [SerializeField] private AudioClip _selectSound;
    [SerializeField] private AudioClip _errorSound;

    #endregion

    #region Private Fields

    private SaveSlotUI[] _slotUIs;
    private int _selectedSlot = -1;
    private GameSaveManager _saveManager;
    private AudioSource _audioSource;
    private System.Action _pendingConfirmAction;

    #endregion

    #region Properties

    public bool IsSaveMode
    {
        get => _isSaveMode;
        set
        {
            _isSaveMode = value;
            UpdateActionButtonText();
        }
    }

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        SetupButtons();
        CreateSlots();
    }

    #endregion

    #region UIPanel Overrides

    public override void Show()
    {
        base.Show();

        _saveManager = GameSaveManager.Instance;

        if (_saveManager != null)
        {
            _saveManager.OnSaveStarted += OnSaveStarted;
            _saveManager.OnSaveCompleted += OnSaveCompleted;
            _saveManager.OnLoadStarted += OnLoadStarted;
            _saveManager.OnLoadCompleted += OnLoadCompleted;
        }

        RefreshSlots();
        _selectedSlot = -1;
        UpdateActionButtons();
        HideConfirmation();
        HideLoading();
    }

    public override void Hide()
    {
        base.Hide();

        if (_saveManager != null)
        {
            _saveManager.OnSaveStarted -= OnSaveStarted;
            _saveManager.OnSaveCompleted -= OnSaveCompleted;
            _saveManager.OnLoadStarted -= OnLoadStarted;
            _saveManager.OnLoadCompleted -= OnLoadCompleted;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Configure le panel en mode sauvegarde.
    /// </summary>
    public void SetSaveMode()
    {
        IsSaveMode = true;
    }

    /// <summary>
    /// Configure le panel en mode chargement.
    /// </summary>
    public void SetLoadMode()
    {
        IsSaveMode = false;
    }

    #endregion

    #region Private Methods - Setup

    private void SetupButtons()
    {
        if (_actionButton != null)
            _actionButton.onClick.AddListener(OnActionClicked);

        if (_deleteButton != null)
            _deleteButton.onClick.AddListener(OnDeleteClicked);

        if (_cancelButton != null)
            _cancelButton.onClick.AddListener(OnCancelClicked);

        if (_confirmYesButton != null)
            _confirmYesButton.onClick.AddListener(OnConfirmYes);

        if (_confirmNoButton != null)
            _confirmNoButton.onClick.AddListener(OnConfirmNo);

        UpdateActionButtonText();
    }

    private void CreateSlots()
    {
        if (_slotContainer == null || _slotPrefab == null) return;

        _slotUIs = new SaveSlotUI[SaveManager.MAX_SAVE_SLOTS];

        for (int i = 0; i < SaveManager.MAX_SAVE_SLOTS; i++)
        {
            int slotIndex = i + 1;
            var slotObj = Instantiate(_slotPrefab, _slotContainer);
            var slotUI = slotObj.GetComponent<SaveSlotUI>();

            if (slotUI == null)
            {
                slotUI = slotObj.AddComponent<SaveSlotUI>();
            }

            slotUI.Initialize(slotIndex, () => OnSlotSelected(slotIndex));
            _slotUIs[i] = slotUI;
        }
    }

    #endregion

    #region Private Methods - Slots

    private void RefreshSlots()
    {
        if (_saveManager == null || _slotUIs == null) return;

        var slotInfos = _saveManager.GetAllSlotInfos();

        for (int i = 0; i < _slotUIs.Length && i < slotInfos.Length; i++)
        {
            _slotUIs[i].SetSlotInfo(slotInfos[i]);
        }
    }

    private void OnSlotSelected(int slotIndex)
    {
        _selectedSlot = slotIndex;

        // Update visual selection
        for (int i = 0; i < _slotUIs.Length; i++)
        {
            _slotUIs[i].SetSelected(i + 1 == slotIndex);
        }

        PlaySound(_selectSound);
        UpdateActionButtons();
    }

    #endregion

    #region Private Methods - Actions

    private void UpdateActionButtonText()
    {
        if (_actionButtonText != null)
        {
            _actionButtonText.text = _isSaveMode ? "Sauvegarder" : "Charger";
        }
    }

    private void UpdateActionButtons()
    {
        bool hasSelection = _selectedSlot > 0;
        bool slotHasData = hasSelection && _saveManager != null && _saveManager.HasSaveInSlot(_selectedSlot);

        if (_actionButton != null)
        {
            // En mode save, on peut toujours sauvegarder dans un slot selectionne
            // En mode load, on ne peut charger que si le slot a des donnees
            _actionButton.interactable = hasSelection && (_isSaveMode || slotHasData);
        }

        if (_deleteButton != null)
        {
            _deleteButton.interactable = slotHasData;
        }
    }

    private void OnActionClicked()
    {
        if (_selectedSlot <= 0 || _saveManager == null) return;

        if (_isSaveMode)
        {
            // Verifier si le slot a deja des donnees
            if (_saveManager.HasSaveInSlot(_selectedSlot))
            {
                ShowConfirmation("Ecraser cette sauvegarde?", () =>
                {
                    _saveManager.SaveToSlot(_selectedSlot);
                });
            }
            else
            {
                _saveManager.SaveToSlot(_selectedSlot);
            }
        }
        else
        {
            ShowConfirmation("Charger cette sauvegarde?\nLa progression non sauvegardee sera perdue.", () =>
            {
                _saveManager.LoadFromSlot(_selectedSlot);
            });
        }
    }

    private void OnDeleteClicked()
    {
        if (_selectedSlot <= 0 || _saveManager == null) return;

        ShowConfirmation("Supprimer cette sauvegarde?\nCette action est irreversible.", () =>
        {
            _saveManager.DeleteSlot(_selectedSlot);
            RefreshSlots();
            _selectedSlot = -1;
            UpdateActionButtons();
        });
    }

    private void OnCancelClicked()
    {
        Hide();
    }

    #endregion

    #region Private Methods - Confirmation

    private void ShowConfirmation(string message, System.Action onConfirm)
    {
        if (_confirmationPopup == null) return;

        _confirmationPopup.SetActive(true);

        if (_confirmationText != null)
        {
            _confirmationText.text = message;
        }

        _pendingConfirmAction = onConfirm;
    }

    private void HideConfirmation()
    {
        if (_confirmationPopup != null)
        {
            _confirmationPopup.SetActive(false);
        }
        _pendingConfirmAction = null;
    }

    private void OnConfirmYes()
    {
        var action = _pendingConfirmAction;
        HideConfirmation();
        action?.Invoke();
    }

    private void OnConfirmNo()
    {
        HideConfirmation();
    }

    #endregion

    #region Private Methods - Loading

    private void ShowLoading(string text)
    {
        if (_loadingIndicator != null)
        {
            _loadingIndicator.SetActive(true);
        }

        if (_loadingText != null)
        {
            _loadingText.text = text;
        }
    }

    private void HideLoading()
    {
        if (_loadingIndicator != null)
        {
            _loadingIndicator.SetActive(false);
        }
    }

    #endregion

    #region Event Handlers

    private void OnSaveStarted()
    {
        ShowLoading("Sauvegarde en cours...");
    }

    private void OnSaveCompleted(bool success)
    {
        HideLoading();

        if (success)
        {
            RefreshSlots();
        }
        else
        {
            PlaySound(_errorSound);
            // TODO: Afficher message d'erreur
        }
    }

    private void OnLoadStarted()
    {
        ShowLoading("Chargement en cours...");
    }

    private void OnLoadCompleted(bool success)
    {
        HideLoading();

        if (success)
        {
            Hide();
        }
        else
        {
            PlaySound(_errorSound);
            // TODO: Afficher message d'erreur
        }
    }

    #endregion

    #region Private Methods - Audio

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }

    #endregion
}

/// <summary>
/// UI d'un slot de sauvegarde.
/// </summary>
public class SaveSlotUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _slotNumberText;
    [SerializeField] private TextMeshProUGUI _saveNameText;
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _playTimeText;
    [SerializeField] private TextMeshProUGUI _dateText;
    [SerializeField] private TextMeshProUGUI _locationText;
    [SerializeField] private GameObject _emptyLabel;
    [SerializeField] private GameObject _dataContainer;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private Button _button;
    [SerializeField] private Color _selectedColor = new Color(1f, 0.8f, 0.3f);
    [SerializeField] private Color _normalColor = new Color(0.2f, 0.2f, 0.2f);
    [SerializeField] private Color _emptyColor = new Color(0.15f, 0.15f, 0.15f);

    private int _slotIndex;
    private bool _isEmpty = true;
    private bool _isSelected;
    private System.Action _onClick;

    public int SlotIndex => _slotIndex;
    public bool IsEmpty => _isEmpty;

    public void Initialize(int slotIndex, System.Action onClick)
    {
        _slotIndex = slotIndex;
        _onClick = onClick;

        if (_slotNumberText != null)
        {
            _slotNumberText.text = $"Slot {slotIndex}";
        }

        if (_button == null) _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => _onClick?.Invoke());
        }

        SetEmpty();
    }

    public void SetSlotInfo(SaveSlotInfo info)
    {
        if (info == null || info.isEmpty)
        {
            SetEmpty();
            return;
        }

        _isEmpty = false;

        if (_emptyLabel != null) _emptyLabel.SetActive(false);
        if (_dataContainer != null) _dataContainer.SetActive(true);

        if (_saveNameText != null)
            _saveNameText.text = info.saveName;

        if (_levelText != null)
            _levelText.text = $"Niveau {info.playerLevel}";

        if (_playTimeText != null)
            _playTimeText.text = info.GetFormattedPlayTime();

        if (_dateText != null)
            _dateText.text = info.GetFormattedDate();

        if (_locationText != null)
            _locationText.text = info.locationName;

        UpdateVisual();
    }

    public void SetEmpty()
    {
        _isEmpty = true;

        if (_emptyLabel != null) _emptyLabel.SetActive(true);
        if (_dataContainer != null) _dataContainer.SetActive(false);

        UpdateVisual();
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (_backgroundImage != null)
        {
            if (_isSelected)
            {
                _backgroundImage.color = _selectedColor;
            }
            else if (_isEmpty)
            {
                _backgroundImage.color = _emptyColor;
            }
            else
            {
                _backgroundImage.color = _normalColor;
            }
        }
    }
}
