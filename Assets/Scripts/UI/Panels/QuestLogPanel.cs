using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel de journal des quetes.
/// Affiche toutes les quetes actives et completees avec details.
/// </summary>
public class QuestLogPanel : UIPanel
{
    #region Serialized Fields

    [Header("Quest List")]
    [SerializeField] private Transform _questListContainer;
    [SerializeField] private GameObject _questEntryPrefab;
    [SerializeField] private ScrollRect _questScrollRect;

    [Header("Category Tabs")]
    [SerializeField] private Button _activeTabButton;
    [SerializeField] private Button _completedTabButton;
    [SerializeField] private Button _mainQuestsButton;
    [SerializeField] private Button _sideQuestsButton;
    [SerializeField] private Button _dailyQuestsButton;

    [Header("Quest Details")]
    [SerializeField] private GameObject _detailsPanel;
    [SerializeField] private Image _questIcon;
    [SerializeField] private TextMeshProUGUI _questNameText;
    [SerializeField] private TextMeshProUGUI _questDescriptionText;
    [SerializeField] private TextMeshProUGUI _questTypeText;
    [SerializeField] private TextMeshProUGUI _levelRecommendedText;
    [SerializeField] private Transform _objectivesContainer;
    [SerializeField] private GameObject _objectiveEntryPrefab;

    [Header("Rewards Preview")]
    [SerializeField] private TextMeshProUGUI _xpRewardText;
    [SerializeField] private TextMeshProUGUI _goldRewardText;
    [SerializeField] private Transform _itemRewardsContainer;
    [SerializeField] private GameObject _itemRewardPrefab;

    [Header("Actions")]
    [SerializeField] private Button _trackButton;
    [SerializeField] private Button _untrackButton;
    [SerializeField] private Button _abandonButton;
    [SerializeField] private Button _turnInButton;

    [Header("Stats")]
    [SerializeField] private TextMeshProUGUI _activeCountText;
    [SerializeField] private TextMeshProUGUI _completedCountText;

    [Header("Colors")]
    [SerializeField] private Color _mainQuestColor = new Color(1f, 0.84f, 0f);
    [SerializeField] private Color _sideQuestColor = Color.white;
    [SerializeField] private Color _dailyQuestColor = new Color(0.5f, 0.8f, 1f);

    #endregion

    #region Private Fields

    private enum TabType { Active, Completed }
    private enum FilterType { All, Main, Side, Daily }

    private TabType _currentTab = TabType.Active;
    private FilterType _currentFilter = FilterType.All;
    private List<QuestLogEntryUI> _questEntries = new List<QuestLogEntryUI>();
    private QuestProgress _selectedQuest;
    private QuestManager _questManager;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        SetupTabButtons();
        SetupActionButtons();
    }

    #endregion

    #region UIPanel Overrides

    public override void Show()
    {
        base.Show();

        _questManager = QuestManager.Instance;

        if (_questManager != null)
        {
            _questManager.OnQuestAccepted += OnQuestChanged;
            _questManager.OnQuestCompleted += OnQuestChanged;
            _questManager.OnQuestAbandoned += OnQuestChanged;
            _questManager.OnObjectiveUpdated += OnObjectiveUpdated;
        }

        RefreshQuestList();
        UpdateStats();
        HideDetails();
    }

    public override void Hide()
    {
        base.Hide();

        if (_questManager != null)
        {
            _questManager.OnQuestAccepted -= OnQuestChanged;
            _questManager.OnQuestCompleted -= OnQuestChanged;
            _questManager.OnQuestAbandoned -= OnQuestChanged;
            _questManager.OnObjectiveUpdated -= OnObjectiveUpdated;
        }
    }

    #endregion

    #region Private Methods - Setup

    private void SetupTabButtons()
    {
        if (_activeTabButton != null)
            _activeTabButton.onClick.AddListener(() => SetTab(TabType.Active));

        if (_completedTabButton != null)
            _completedTabButton.onClick.AddListener(() => SetTab(TabType.Completed));

        if (_mainQuestsButton != null)
            _mainQuestsButton.onClick.AddListener(() => SetFilter(FilterType.Main));

        if (_sideQuestsButton != null)
            _sideQuestsButton.onClick.AddListener(() => SetFilter(FilterType.Side));

        if (_dailyQuestsButton != null)
            _dailyQuestsButton.onClick.AddListener(() => SetFilter(FilterType.Daily));
    }

    private void SetupActionButtons()
    {
        if (_trackButton != null)
            _trackButton.onClick.AddListener(OnTrackClicked);

        if (_untrackButton != null)
            _untrackButton.onClick.AddListener(OnUntrackClicked);

        if (_abandonButton != null)
            _abandonButton.onClick.AddListener(OnAbandonClicked);

        if (_turnInButton != null)
            _turnInButton.onClick.AddListener(OnTurnInClicked);
    }

    #endregion

    #region Private Methods - Navigation

    private void SetTab(TabType tab)
    {
        _currentTab = tab;
        RefreshQuestList();
        HideDetails();
    }

    private void SetFilter(FilterType filter)
    {
        _currentFilter = filter;
        RefreshQuestList();
    }

    #endregion

    #region Private Methods - Quest List

    private void RefreshQuestList()
    {
        ClearQuestEntries();

        if (_questManager == null) return;

        List<QuestProgress> quests;

        if (_currentTab == TabType.Active)
        {
            quests = GetFilteredActiveQuests();
        }
        else
        {
            // Pour les completees, on n'a pas de QuestProgress
            // On afficherait juste les IDs - simplification
            quests = new List<QuestProgress>();
        }

        foreach (var quest in quests)
        {
            CreateQuestEntry(quest);
        }
    }

    private List<QuestProgress> GetFilteredActiveQuests()
    {
        var allActive = _questManager.GetActiveQuests();

        if (_currentFilter == FilterType.All)
        {
            return allActive;
        }

        var filtered = new List<QuestProgress>();
        foreach (var progress in allActive)
        {
            bool match = _currentFilter switch
            {
                FilterType.Main => progress.QuestData.questType == QuestType.Main,
                FilterType.Side => progress.QuestData.questType == QuestType.Side,
                FilterType.Daily => progress.QuestData.questType == QuestType.Daily,
                _ => true
            };

            if (match) filtered.Add(progress);
        }

        return filtered;
    }

    private void ClearQuestEntries()
    {
        foreach (var entry in _questEntries)
        {
            if (entry != null) Destroy(entry.gameObject);
        }
        _questEntries.Clear();
    }

    private void CreateQuestEntry(QuestProgress progress)
    {
        if (_questListContainer == null || _questEntryPrefab == null) return;

        var entryObj = Instantiate(_questEntryPrefab, _questListContainer);
        var entry = entryObj.GetComponent<QuestLogEntryUI>();

        if (entry == null)
        {
            entry = entryObj.AddComponent<QuestLogEntryUI>();
        }

        Color color = GetQuestColor(progress.QuestData.questType);
        bool isTracked = IsQuestTracked(progress.QuestData.questId);

        entry.Initialize(progress, color, isTracked, () => SelectQuest(progress));
        _questEntries.Add(entry);
    }

    private bool IsQuestTracked(string questId)
    {
        if (_questManager == null) return false;

        var tracked = _questManager.GetTrackedQuests();
        foreach (var q in tracked)
        {
            if (q.QuestData.questId == questId) return true;
        }
        return false;
    }

    private Color GetQuestColor(QuestType type)
    {
        return type switch
        {
            QuestType.Main => _mainQuestColor,
            QuestType.Daily => _dailyQuestColor,
            _ => _sideQuestColor
        };
    }

    #endregion

    #region Private Methods - Selection

    private void SelectQuest(QuestProgress progress)
    {
        _selectedQuest = progress;

        // Deselectionner les autres
        foreach (var entry in _questEntries)
        {
            entry.SetSelected(entry.QuestId == progress.QuestData.questId);
        }

        ShowDetails(progress);
    }

    private void ShowDetails(QuestProgress progress)
    {
        if (_detailsPanel == null || progress == null) return;

        _detailsPanel.SetActive(true);

        var quest = progress.QuestData;

        if (_questIcon != null && quest.questIcon != null)
            _questIcon.sprite = quest.questIcon;

        if (_questNameText != null)
        {
            _questNameText.text = quest.questName;
            _questNameText.color = GetQuestColor(quest.questType);
        }

        if (_questDescriptionText != null)
            _questDescriptionText.text = quest.fullDescription;

        if (_questTypeText != null)
            _questTypeText.text = $"Type: {quest.questType}";

        if (_levelRecommendedText != null)
            _levelRecommendedText.text = $"Niveau recommande: {quest.recommendedLevel}";

        PopulateObjectives(progress);
        PopulateRewards(quest);
        UpdateActionButtons(progress);
    }

    private void HideDetails()
    {
        if (_detailsPanel != null)
        {
            _detailsPanel.SetActive(false);
        }
        _selectedQuest = null;
    }

    private void PopulateObjectives(QuestProgress progress)
    {
        if (_objectivesContainer == null) return;

        // Clear
        foreach (Transform child in _objectivesContainer)
        {
            Destroy(child.gameObject);
        }

        var quest = progress.QuestData;
        if (quest.objectives == null || _objectiveEntryPrefab == null) return;

        for (int i = 0; i < quest.objectives.Length; i++)
        {
            var obj = quest.objectives[i];
            int current = progress.GetObjectiveProgress(i);
            bool complete = progress.IsObjectiveComplete(i);

            var entryObj = Instantiate(_objectiveEntryPrefab, _objectivesContainer);
            var text = entryObj.GetComponentInChildren<TextMeshProUGUI>();

            if (text != null)
            {
                string status = complete ? "<color=green>[Complete]</color>" : $"({current}/{obj.requiredAmount})";
                text.text = $"- {obj.description} {status}";
            }
        }
    }

    private void PopulateRewards(QuestData quest)
    {
        if (_xpRewardText != null)
            _xpRewardText.text = quest.xpReward > 0 ? $"XP: {quest.xpReward}" : "";

        if (_goldRewardText != null)
            _goldRewardText.text = quest.goldReward > 0 ? $"Or: {quest.goldReward}" : "";

        // Item rewards
        if (_itemRewardsContainer != null)
        {
            foreach (Transform child in _itemRewardsContainer)
            {
                Destroy(child.gameObject);
            }

            if (quest.itemRewards != null && _itemRewardPrefab != null)
            {
                foreach (var reward in quest.itemRewards)
                {
                    if (reward.item == null) continue;

                    var rewardObj = Instantiate(_itemRewardPrefab, _itemRewardsContainer);
                    var text = rewardObj.GetComponentInChildren<TextMeshProUGUI>();
                    var icon = rewardObj.GetComponentInChildren<Image>();

                    if (text != null)
                        text.text = $"{reward.amount}x {reward.item.displayName}";

                    if (icon != null && reward.item.icon != null)
                        icon.sprite = reward.item.icon;
                }
            }
        }
    }

    private void UpdateActionButtons(QuestProgress progress)
    {
        bool isTracked = IsQuestTracked(progress.QuestData.questId);
        bool canComplete = progress.QuestData.AreAllObjectivesComplete(progress);

        if (_trackButton != null)
            _trackButton.gameObject.SetActive(!isTracked);

        if (_untrackButton != null)
            _untrackButton.gameObject.SetActive(isTracked);

        if (_abandonButton != null)
            _abandonButton.gameObject.SetActive(_currentTab == TabType.Active);

        if (_turnInButton != null)
        {
            _turnInButton.gameObject.SetActive(canComplete);
            _turnInButton.interactable = canComplete;
        }
    }

    #endregion

    #region Private Methods - Stats

    private void UpdateStats()
    {
        if (_questManager == null) return;

        if (_activeCountText != null)
            _activeCountText.text = $"Actives: {_questManager.ActiveQuestCount}";

        if (_completedCountText != null)
            _completedCountText.text = $"Completees: {_questManager.CompletedQuestCount}";
    }

    #endregion

    #region Event Handlers

    private void OnQuestChanged(QuestData quest)
    {
        RefreshQuestList();
        UpdateStats();

        if (_selectedQuest != null && _selectedQuest.QuestData.questId == quest.questId)
        {
            HideDetails();
        }
    }

    private void OnObjectiveUpdated(QuestData quest, int objectiveIndex, int progress)
    {
        if (_selectedQuest != null && _selectedQuest.QuestData.questId == quest.questId)
        {
            ShowDetails(_selectedQuest);
        }

        // Refresh l'entree dans la liste
        foreach (var entry in _questEntries)
        {
            if (entry.QuestId == quest.questId)
            {
                var questProgress = _questManager.GetQuestProgress(quest.questId);
                if (questProgress != null)
                {
                    entry.UpdateProgress(questProgress);
                }
                break;
            }
        }
    }

    private void OnTrackClicked()
    {
        if (_selectedQuest == null || _questManager == null) return;

        _questManager.TrackQuest(_selectedQuest.QuestData.questId);
        UpdateActionButtons(_selectedQuest);

        // Update entry
        foreach (var entry in _questEntries)
        {
            if (entry.QuestId == _selectedQuest.QuestData.questId)
            {
                entry.SetTracked(true);
                break;
            }
        }
    }

    private void OnUntrackClicked()
    {
        if (_selectedQuest == null || _questManager == null) return;

        _questManager.UntrackQuest(_selectedQuest.QuestData.questId);
        UpdateActionButtons(_selectedQuest);

        // Update entry
        foreach (var entry in _questEntries)
        {
            if (entry.QuestId == _selectedQuest.QuestData.questId)
            {
                entry.SetTracked(false);
                break;
            }
        }
    }

    private void OnAbandonClicked()
    {
        if (_selectedQuest == null || _questManager == null) return;

        // TODO: Afficher confirmation popup
        _questManager.AbandonQuest(_selectedQuest.QuestData.questId);
    }

    private void OnTurnInClicked()
    {
        if (_selectedQuest == null || _questManager == null) return;

        _questManager.CompleteQuest(_selectedQuest.QuestData.questId);
    }

    #endregion
}

/// <summary>
/// Entree UI pour une quete dans le journal.
/// </summary>
public class QuestLogEntryUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _progressText;
    [SerializeField] private Image _typeIndicator;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private Image _trackingIcon;
    [SerializeField] private Button _button;
    [SerializeField] private Color _selectedColor = new Color(1f, 0.8f, 0.3f);
    [SerializeField] private Color _normalColor = new Color(0.2f, 0.2f, 0.2f);

    private string _questId;
    private bool _isSelected;
    private System.Action _onClick;

    public string QuestId => _questId;

    public void Initialize(QuestProgress progress, Color typeColor, bool isTracked, System.Action onClick)
    {
        if (progress == null) return;

        _questId = progress.QuestData.questId;
        _onClick = onClick;

        if (_nameText != null)
            _nameText.text = progress.QuestData.questName;

        if (_typeIndicator != null)
            _typeIndicator.color = typeColor;

        SetTracked(isTracked);
        UpdateProgress(progress);

        if (_button == null) _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => _onClick?.Invoke());
        }
    }

    public void UpdateProgress(QuestProgress progress)
    {
        if (_progressText != null && progress != null)
        {
            float percent = progress.QuestData.GetCompletionPercentage(progress);
            _progressText.text = $"{percent:F0}%";
        }
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;

        if (_backgroundImage != null)
        {
            _backgroundImage.color = selected ? _selectedColor : _normalColor;
        }
    }

    public void SetTracked(bool tracked)
    {
        if (_trackingIcon != null)
        {
            _trackingIcon.enabled = tracked;
        }
    }
}
