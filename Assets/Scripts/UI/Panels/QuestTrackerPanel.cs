using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel de suivi des quetes actives (HUD).
/// Affiche les quetes suivies et leur progression en temps reel.
/// </summary>
public class QuestTrackerPanel : MonoBehaviour
{
    #region Serialized Fields

    [Header("Container")]
    [SerializeField] private Transform _questEntryContainer;
    [SerializeField] private GameObject _questEntryPrefab;
    [SerializeField] private int _maxDisplayedQuests = 3;

    [Header("Animation")]
    [SerializeField] private float _updateAnimDuration = 0.3f;
    [SerializeField] private float _pulseScale = 1.1f;

    [Header("Colors")]
    [SerializeField] private Color _mainQuestColor = new Color(1f, 0.84f, 0f);
    [SerializeField] private Color _sideQuestColor = Color.white;
    [SerializeField] private Color _dailyQuestColor = new Color(0.5f, 0.8f, 1f);
    [SerializeField] private Color _completedObjectiveColor = Color.green;
    [SerializeField] private Color _incompleteObjectiveColor = Color.gray;

    [Header("Audio")]
    [SerializeField] private AudioClip _objectiveCompleteSound;
    [SerializeField] private AudioClip _questCompleteSound;

    #endregion

    #region Private Fields

    private List<QuestTrackerEntryUI> _entries = new List<QuestTrackerEntryUI>();
    private QuestManager _questManager;
    private AudioSource _audioSource;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Start()
    {
        _questManager = QuestManager.Instance;

        if (_questManager != null)
        {
            _questManager.OnQuestAccepted += OnQuestAccepted;
            _questManager.OnObjectiveUpdated += OnObjectiveUpdated;
            _questManager.OnQuestCompleted += OnQuestCompleted;
            _questManager.OnQuestAbandoned += OnQuestAbandoned;
        }

        RefreshTracker();
    }

    private void OnDestroy()
    {
        if (_questManager != null)
        {
            _questManager.OnQuestAccepted -= OnQuestAccepted;
            _questManager.OnObjectiveUpdated -= OnObjectiveUpdated;
            _questManager.OnQuestCompleted -= OnQuestCompleted;
            _questManager.OnQuestAbandoned -= OnQuestAbandoned;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Rafraichit l'affichage du tracker.
    /// </summary>
    public void RefreshTracker()
    {
        ClearEntries();

        if (_questManager == null) return;

        var trackedQuests = _questManager.GetTrackedQuests();

        // Limiter le nombre affiche
        int count = Mathf.Min(trackedQuests.Count, _maxDisplayedQuests);

        for (int i = 0; i < count; i++)
        {
            CreateQuestEntry(trackedQuests[i]);
        }
    }

    /// <summary>
    /// Affiche ou masque le tracker.
    /// </summary>
    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    #endregion

    #region Private Methods - UI

    private void ClearEntries()
    {
        foreach (var entry in _entries)
        {
            if (entry != null)
            {
                Destroy(entry.gameObject);
            }
        }
        _entries.Clear();
    }

    private QuestTrackerEntryUI CreateQuestEntry(QuestProgress progress)
    {
        if (_questEntryContainer == null || _questEntryPrefab == null) return null;

        var entryObj = Instantiate(_questEntryPrefab, _questEntryContainer);
        var entry = entryObj.GetComponent<QuestTrackerEntryUI>();

        if (entry == null)
        {
            entry = entryObj.AddComponent<QuestTrackerEntryUI>();
        }

        entry.Initialize(progress, GetQuestColor(progress.QuestData.questType));
        _entries.Add(entry);

        return entry;
    }

    private QuestTrackerEntryUI FindEntry(string questId)
    {
        foreach (var entry in _entries)
        {
            if (entry != null && entry.QuestId == questId)
            {
                return entry;
            }
        }
        return null;
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

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }

    #endregion

    #region Event Handlers

    private void OnQuestAccepted(QuestData quest)
    {
        // Auto-track si place disponible
        if (_questManager != null && _entries.Count < _maxDisplayedQuests)
        {
            _questManager.TrackQuest(quest.questId);
        }

        RefreshTracker();
    }

    private void OnObjectiveUpdated(QuestData quest, int objectiveIndex, int newProgress)
    {
        var entry = FindEntry(quest.questId);
        if (entry != null)
        {
            var progress = _questManager?.GetQuestProgress(quest.questId);
            if (progress != null)
            {
                entry.UpdateProgress(progress);

                // Animation de mise a jour
                entry.PlayUpdateAnimation(_updateAnimDuration, _pulseScale);

                // Son si objectif complete
                if (progress.IsObjectiveComplete(objectiveIndex))
                {
                    PlaySound(_objectiveCompleteSound);
                }
            }
        }
    }

    private void OnQuestCompleted(QuestData quest)
    {
        var entry = FindEntry(quest.questId);
        if (entry != null)
        {
            entry.PlayCompleteAnimation(() =>
            {
                RefreshTracker();
            });
        }

        PlaySound(_questCompleteSound);
    }

    private void OnQuestAbandoned(QuestData quest)
    {
        RefreshTracker();
    }

    #endregion
}

/// <summary>
/// Entree UI pour une quete dans le tracker.
/// </summary>
public class QuestTrackerEntryUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _questNameText;
    [SerializeField] private Transform _objectiveContainer;
    [SerializeField] private GameObject _objectivePrefab;
    [SerializeField] private Image _questTypeIndicator;
    [SerializeField] private CanvasGroup _canvasGroup;

    private string _questId;
    private List<QuestObjectiveUI> _objectiveUIs = new List<QuestObjectiveUI>();

    public string QuestId => _questId;

    public void Initialize(QuestProgress progress, Color questColor)
    {
        if (progress == null || progress.QuestData == null) return;

        _questId = progress.QuestData.questId;

        if (_questNameText != null)
        {
            _questNameText.text = progress.QuestData.questName;
            _questNameText.color = questColor;
        }

        if (_questTypeIndicator != null)
        {
            _questTypeIndicator.color = questColor;
        }

        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        PopulateObjectives(progress);
    }

    public void UpdateProgress(QuestProgress progress)
    {
        if (progress == null) return;

        var quest = progress.QuestData;
        if (quest?.objectives == null) return;

        for (int i = 0; i < _objectiveUIs.Count && i < quest.objectives.Length; i++)
        {
            var obj = quest.objectives[i];
            int current = progress.GetObjectiveProgress(i);
            bool complete = progress.IsObjectiveComplete(i);

            _objectiveUIs[i].UpdateProgress(current, obj.requiredAmount, complete);
        }
    }

    public void PlayUpdateAnimation(float duration, float pulseScale)
    {
        // Animation simple de pulse
        StartCoroutine(PulseAnimation(duration, pulseScale));
    }

    public void PlayCompleteAnimation(System.Action onComplete)
    {
        StartCoroutine(FadeOutAnimation(0.5f, onComplete));
    }

    private void PopulateObjectives(QuestProgress progress)
    {
        // Clear existing
        foreach (var obj in _objectiveUIs)
        {
            if (obj != null) Destroy(obj.gameObject);
        }
        _objectiveUIs.Clear();

        if (_objectiveContainer == null || _objectivePrefab == null) return;

        var quest = progress.QuestData;
        if (quest?.objectives == null) return;

        // Afficher le prochain objectif non complete (ou le dernier si tous completes)
        int nextIndex = quest.GetNextObjectiveIndex(progress);
        int displayStart = Mathf.Max(0, nextIndex);
        int displayEnd = Mathf.Min(quest.objectives.Length, displayStart + 2); // Afficher 2 max

        for (int i = displayStart; i < displayEnd; i++)
        {
            var obj = quest.objectives[i];
            int current = progress.GetObjectiveProgress(i);
            bool complete = progress.IsObjectiveComplete(i);

            var objUI = CreateObjectiveUI(obj, current, complete);
            if (objUI != null)
            {
                _objectiveUIs.Add(objUI);
            }
        }
    }

    private QuestObjectiveUI CreateObjectiveUI(QuestObjective objective, int current, bool complete)
    {
        var objObj = Instantiate(_objectivePrefab, _objectiveContainer);
        var objUI = objObj.GetComponent<QuestObjectiveUI>();

        if (objUI == null)
        {
            objUI = objObj.AddComponent<QuestObjectiveUI>();
        }

        objUI.Initialize(objective.description, current, objective.requiredAmount, complete);
        return objUI;
    }

    private System.Collections.IEnumerator PulseAnimation(float duration, float scale)
    {
        var rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null) yield break;

        Vector3 originalScale = rectTransform.localScale;
        Vector3 targetScale = originalScale * scale;

        float halfDuration = duration * 0.5f;
        float elapsed = 0f;

        // Scale up
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            rectTransform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            yield return null;
        }

        // Scale down
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            rectTransform.localScale = Vector3.Lerp(targetScale, originalScale, t);
            yield return null;
        }

        rectTransform.localScale = originalScale;
    }

    private System.Collections.IEnumerator FadeOutAnimation(float duration, System.Action onComplete)
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = 1f - (elapsed / duration);
            yield return null;
        }

        onComplete?.Invoke();
    }
}

/// <summary>
/// UI d'un objectif de quete dans le tracker.
/// </summary>
public class QuestObjectiveUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _progressText;
    [SerializeField] private Image _checkmark;
    [SerializeField] private Color _completeColor = Color.green;
    [SerializeField] private Color _incompleteColor = Color.gray;

    public void Initialize(string description, int current, int required, bool complete)
    {
        if (_descriptionText != null)
        {
            _descriptionText.text = description;
            _descriptionText.color = complete ? _completeColor : _incompleteColor;

            if (complete)
            {
                _descriptionText.fontStyle = FontStyles.Strikethrough;
            }
        }

        UpdateProgress(current, required, complete);
    }

    public void UpdateProgress(int current, int required, bool complete)
    {
        if (_progressText != null)
        {
            if (required > 1)
            {
                _progressText.text = $"{current}/{required}";
            }
            else
            {
                _progressText.text = complete ? "Done" : "";
            }
            _progressText.color = complete ? _completeColor : _incompleteColor;
        }

        if (_checkmark != null)
        {
            _checkmark.enabled = complete;
        }

        if (_descriptionText != null)
        {
            _descriptionText.color = complete ? _completeColor : _incompleteColor;
            _descriptionText.fontStyle = complete ? FontStyles.Strikethrough : FontStyles.Normal;
        }
    }
}
