using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel d'affichage des dialogues.
/// </summary>
public class DialoguePanel : UIPanel
{
    #region Serialized Fields

    [Header("Speaker")]
    [SerializeField] private Image _speakerPortrait;
    [SerializeField] private TextMeshProUGUI _speakerName;

    [Header("Dialogue")]
    [SerializeField] private TextMeshProUGUI _dialogueText;
    [SerializeField] private GameObject _continueIndicator;

    [Header("Choices")]
    [SerializeField] private Transform _choicesContainer;
    [SerializeField] private GameObject _choiceButtonPrefab;
    [SerializeField] private int _maxChoicesDisplayed = 4;

    [Header("Animation")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _showTrigger = "Show";
    [SerializeField] private string _hideTrigger = "Hide";

    #endregion

    #region Private Fields

    private DialogueManager _dialogueManager;
    private List<Button> _choiceButtons = new List<Button>();

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        // Pre-creer les boutons de choix
        CreateChoiceButtons();
    }

    private void Start()
    {

        // S'abonner aux events du DialogueManager
        _dialogueManager = DialogueManager.Instance;
        if (_dialogueManager != null)
        {
            _dialogueManager.OnDialogueStarted += OnDialogueStarted;
            _dialogueManager.OnDialogueEnded += OnDialogueEnded;
            _dialogueManager.OnNodeDisplayed += OnNodeDisplayed;
            _dialogueManager.OnTextUpdated += OnTextUpdated;
            _dialogueManager.OnChoicesDisplayed += OnChoicesDisplayed;
        }

        // Cacher au demarrage
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (_dialogueManager != null)
        {
            _dialogueManager.OnDialogueStarted -= OnDialogueStarted;
            _dialogueManager.OnDialogueEnded -= OnDialogueEnded;
            _dialogueManager.OnNodeDisplayed -= OnNodeDisplayed;
            _dialogueManager.OnTextUpdated -= OnTextUpdated;
            _dialogueManager.OnChoicesDisplayed -= OnChoicesDisplayed;
        }

        // CRITICAL FIX: Clean up choice button listeners
        if (_choiceButtons != null)
        {
            foreach (var button in _choiceButtons)
            {
                if (button != null) button.onClick.RemoveAllListeners();
            }
        }
    }

    #endregion

    #region UIPanel Overrides

    public override void Show()
    {
        base.Show();

        if (_animator != null)
        {
            _animator.SetTrigger(_showTrigger);
        }
    }

    public override void Hide()
    {
        if (_animator != null)
        {
            _animator.SetTrigger(_hideTrigger);
            // La desactivation sera faite par un event d'animation
        }
        else
        {
            base.Hide();
        }
    }

    private void HideImmediate()
    {
        gameObject.SetActive(false);
    }

    #endregion

    #region Event Handlers

    private void OnDialogueStarted(DialogueData dialogue)
    {
        Show();
        HideChoices();
        HideContinueIndicator();
    }

    private void OnDialogueEnded()
    {
        Hide();
    }

    private void OnNodeDisplayed(DialogueNode node)
    {
        if (node == null) return;

        // Afficher le speaker
        if (_speakerName != null)
        {
            _speakerName.text = node.speakerName;
        }

        if (_speakerPortrait != null)
        {
            if (node.speakerPortrait != null)
            {
                _speakerPortrait.sprite = node.speakerPortrait;
                _speakerPortrait.enabled = true;
            }
            else
            {
                _speakerPortrait.enabled = false;
            }
        }

        // Cacher les choix pendant le typing
        HideChoices();
        HideContinueIndicator();
    }

    private void OnTextUpdated(string text)
    {
        if (_dialogueText != null)
        {
            _dialogueText.text = text;
        }

        // Si le texte est complet et pas de choix, afficher l'indicateur
        if (_dialogueManager != null && !_dialogueManager.IsTyping)
        {
            var currentNode = _dialogueManager.CurrentNode;
            if (currentNode != null && !currentNode.HasChoices)
            {
                ShowContinueIndicator();
            }
        }
    }

    private void OnChoicesDisplayed(DialogueNode node)
    {
        if (node == null || node.choices == null) return;

        HideContinueIndicator();
        DisplayChoices(node.choices);
    }

    #endregion

    #region Private Methods - Choices

    private void CreateChoiceButtons()
    {
        if (_choicesContainer == null || _choiceButtonPrefab == null) return;

        // Nettoyer
        foreach (Transform child in _choicesContainer)
        {
            Destroy(child.gameObject);
        }
        _choiceButtons.Clear();

        // Creer les boutons
        for (int i = 0; i < _maxChoicesDisplayed; i++)
        {
            var buttonObj = Instantiate(_choiceButtonPrefab, _choicesContainer);
            var button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                int choiceIndex = i;
                button.onClick.AddListener(() => OnChoiceSelected(choiceIndex));
                _choiceButtons.Add(button);
            }
            buttonObj.SetActive(false);
        }
    }

    private void DisplayChoices(DialogueChoice[] choices)
    {
        for (int i = 0; i < _choiceButtons.Count; i++)
        {
            if (i < choices.Length)
            {
                var choice = choices[i];
                var button = _choiceButtons[i];

                // Configurer le texte
                var text = button.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = choice.choiceText;
                }

                // Verifier si le choix est disponible
                bool available = IsChoiceAvailable(choice);
                button.interactable = available;

                button.gameObject.SetActive(true);
            }
            else
            {
                _choiceButtons[i].gameObject.SetActive(false);
            }
        }
    }

    private void HideChoices()
    {
        foreach (var button in _choiceButtons)
        {
            button.gameObject.SetActive(false);
        }
    }

    private bool IsChoiceAvailable(DialogueChoice choice)
    {
        // Verifier les conditions du choix
        if (choice.condition.type == DialogueConditionType.None)
            return true;

        // La verification complete est faite dans DialogueManager
        // Ici on fait une verification simplifiee
        return true;
    }

    private void OnChoiceSelected(int index)
    {
        if (_dialogueManager != null)
        {
            _dialogueManager.SelectChoice(index);
            HideChoices();
        }
    }

    #endregion

    #region Private Methods - Continue Indicator

    private void ShowContinueIndicator()
    {
        if (_continueIndicator != null)
        {
            _continueIndicator.SetActive(true);
        }
    }

    private void HideContinueIndicator()
    {
        if (_continueIndicator != null)
        {
            _continueIndicator.SetActive(false);
        }
    }

    #endregion

    #region Animation Events

    /// <summary>
    /// Appele par l'animation de hide quand elle est terminee.
    /// </summary>
    public void OnHideAnimationComplete()
    {
        base.Hide();
    }

    #endregion
}
