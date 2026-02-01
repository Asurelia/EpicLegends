using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gestionnaire de dialogues. Singleton qui gere l'affichage des conversations.
/// Compatible avec DialogueData et son systeme de noeuds.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    #region Singleton

    public static DialogueManager Instance { get; private set; }

    #endregion

    #region Events

    /// <summary>Declenche quand un dialogue commence.</summary>
    public event Action<DialogueData> OnDialogueStarted;

    /// <summary>Declenche quand un dialogue se termine.</summary>
    public event Action OnDialogueEnded;

    /// <summary>Declenche quand un noeud s'affiche.</summary>
    public event Action<DialogueNode> OnNodeDisplayed;

    /// <summary>Declenche quand le texte est mis a jour (typing).</summary>
    public event Action<string> OnTextUpdated;

    /// <summary>Declenche quand des choix sont affiches.</summary>
    public event Action<DialogueNode> OnChoicesDisplayed;

    /// <summary>Declenche quand un choix est fait.</summary>
    public event Action<int> OnChoiceMade;

    #endregion

    #region Serialized Fields

    [Header("Settings")]
    [SerializeField] private float _defaultTextSpeed = 0.03f;
    [SerializeField] private bool _pauseGameDuringDialogue = true;

    [Header("Audio")]
    [SerializeField] private AudioClip _textSound;
    [SerializeField] private float _textSoundInterval = 0.05f;

    #endregion

    #region Private Fields

    private DialogueData _currentDialogue;
    private DialogueNode _currentNode;
    private bool _isDialogueActive;
    private bool _isTyping;
    private bool _skipRequested;
    private Coroutine _typingCoroutine;
    private AudioSource _audioSource;
    private float _previousTimeScale;

    // Input System
    private InputAction _advanceAction;
    private InputAction _skipAction;

    #endregion

    #region Properties

    /// <summary>Un dialogue est-il en cours?</summary>
    public bool IsDialogueActive => _isDialogueActive;

    /// <summary>Le texte est-il en train de s'afficher?</summary>
    public bool IsTyping => _isTyping;

    /// <summary>Dialogue actuel.</summary>
    public DialogueData CurrentDialogue => _currentDialogue;

    /// <summary>Noeud actuel.</summary>
    public DialogueNode CurrentNode => _currentNode;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        SetupInputActions();
    }

    private void OnEnable()
    {
        _advanceAction?.Enable();
        _skipAction?.Enable();
    }

    private void OnDisable()
    {
        _advanceAction?.Disable();
        _skipAction?.Disable();
    }

    private void SetupInputActions()
    {
        // Action pour avancer le dialogue
        _advanceAction = new InputAction("DialogueAdvance", InputActionType.Button);
        _advanceAction.AddBinding("<Keyboard>/space");
        _advanceAction.AddBinding("<Keyboard>/enter");
        _advanceAction.AddBinding("<Mouse>/leftButton");
        _advanceAction.AddBinding("<Gamepad>/buttonSouth"); // A button
        _advanceAction.performed += OnAdvanceDialogue;
        _advanceAction.Enable();

        // Action pour skipper le dialogue
        _skipAction = new InputAction("DialogueSkip", InputActionType.Button);
        _skipAction.AddBinding("<Keyboard>/escape");
        _skipAction.AddBinding("<Gamepad>/buttonEast"); // B button
        _skipAction.performed += OnSkipDialogue;
        _skipAction.Enable();
    }

    private void OnAdvanceDialogue(InputAction.CallbackContext context)
    {
        if (!_isDialogueActive) return;

        if (_isTyping)
        {
            _skipRequested = true;
        }
        else if (_currentNode != null && !_currentNode.HasChoices)
        {
            AdvanceDialogue();
        }
    }

    private void OnSkipDialogue(InputAction.CallbackContext context)
    {
        if (!_isDialogueActive) return;

        if (_currentDialogue != null && _currentDialogue.isSkippable)
        {
            EndDialogue();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Demarre un dialogue.
    /// </summary>
    public void StartDialogue(DialogueData dialogue)
    {
        if (dialogue == null)
        {
            Debug.LogWarning("[DialogueManager] Dialogue null");
            return;
        }

        var startNode = dialogue.GetStartNode();
        if (startNode == null)
        {
            Debug.LogWarning($"[DialogueManager] Dialogue {dialogue.dialogueTitle} n'a pas de noeud de depart");
            return;
        }

        // Si un dialogue est deja en cours, le terminer
        if (_isDialogueActive)
        {
            EndDialogue();
        }

        _currentDialogue = dialogue;
        _isDialogueActive = true;

        // Pause le jeu
        if (_pauseGameDuringDialogue)
        {
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        OnDialogueStarted?.Invoke(dialogue);

        // Afficher le premier noeud
        DisplayNode(startNode);

        Debug.Log($"[DialogueManager] Started dialogue: {dialogue.dialogueTitle}");
    }

    /// <summary>
    /// Avance au prochain noeud.
    /// </summary>
    public void AdvanceDialogue()
    {
        if (!_isDialogueActive || _currentNode == null) return;

        // Si le noeud a des choix, ne pas avancer automatiquement
        if (_currentNode.HasChoices) return;

        // Obtenir le prochain noeud
        var nextNode = _currentDialogue.GetNextNode(_currentNode);

        if (nextNode == null || _currentNode.IsEndNode)
        {
            EndDialogue();
        }
        else
        {
            DisplayNode(nextNode);
        }
    }

    /// <summary>
    /// Selectionne un choix.
    /// </summary>
    public void SelectChoice(int choiceIndex)
    {
        if (_currentNode == null || !_currentNode.HasChoices) return;
        if (choiceIndex < 0 || choiceIndex >= _currentNode.choices.Length) return;

        var choice = _currentNode.choices[choiceIndex];

        // Appliquer les effets du choix
        ApplyEffects(choice.effects);

        OnChoiceMade?.Invoke(choiceIndex);

        // Aller au prochain noeud
        var nextNode = _currentDialogue.GetNextNode(_currentNode, choiceIndex);

        if (nextNode == null)
        {
            EndDialogue();
        }
        else
        {
            DisplayNode(nextNode);
        }
    }

    /// <summary>
    /// Termine le dialogue.
    /// </summary>
    public void EndDialogue()
    {
        if (!_isDialogueActive) return;

        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
        }

        _isDialogueActive = false;
        _isTyping = false;

        if (_pauseGameDuringDialogue)
        {
            Time.timeScale = _previousTimeScale;
        }

        OnDialogueEnded?.Invoke();

        Debug.Log("[DialogueManager] Dialogue ended");

        _currentDialogue = null;
        _currentNode = null;
    }

    /// <summary>
    /// Skip le typing en cours.
    /// </summary>
    public void SkipTyping()
    {
        _skipRequested = true;
    }

    #endregion

    #region Private Methods

    private void DisplayNode(DialogueNode node)
    {
        if (node == null) return;

        // Verifier la condition du noeud
        if (!CheckCondition(node.condition))
        {
            // Sauter ce noeud
            var nextNode = _currentDialogue.GetNextNode(node);
            if (nextNode != null)
            {
                DisplayNode(nextNode);
            }
            else
            {
                EndDialogue();
            }
            return;
        }

        _currentNode = node;

        // Arreter le typing precedent
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
        }

        // Appliquer les effets du noeud
        ApplyEffects(node.effects);

        // Notifier l'UI
        OnNodeDisplayed?.Invoke(node);

        // Demarrer le typing
        _typingCoroutine = StartCoroutine(TypeTextCoroutine(node));

        // Jouer le voiceover si disponible
        if (node.voiceClip != null && _audioSource != null)
        {
            _audioSource.clip = node.voiceClip;
            _audioSource.Play();
        }
    }

    private IEnumerator TypeTextCoroutine(DialogueNode node)
    {
        _isTyping = true;
        _skipRequested = false;

        string fullText = node.dialogueText;
        string displayedText = "";
        float textSpeed = _currentDialogue != null && _currentDialogue.textSpeed > 0 ?
            1f / (_currentDialogue.textSpeed * 30f) : _defaultTextSpeed;
        float lastSoundTime = 0f;

        OnTextUpdated?.Invoke("");

        yield return null;

        for (int i = 0; i < fullText.Length; i++)
        {
            if (_skipRequested)
            {
                displayedText = fullText;
                OnTextUpdated?.Invoke(displayedText);
                break;
            }

            displayedText += fullText[i];
            OnTextUpdated?.Invoke(displayedText);

            // Son de texte
            if (_textSound != null && Time.unscaledTime - lastSoundTime > _textSoundInterval)
            {
                _audioSource.PlayOneShot(_textSound, 0.3f);
                lastSoundTime = Time.unscaledTime;
            }

            // Pause selon ponctuation
            float delay = textSpeed;
            char c = fullText[i];
            if (c == '.' || c == '!' || c == '?') delay *= 4f;
            else if (c == ',') delay *= 2f;

            yield return new WaitForSecondsRealtime(delay);
        }

        _isTyping = false;

        // Afficher les choix si disponibles
        if (node.HasChoices)
        {
            OnChoicesDisplayed?.Invoke(node);
        }

        // Auto-advance si configure
        if (_currentDialogue != null && _currentDialogue.autoAdvanceDelay > 0 && !node.HasChoices)
        {
            yield return new WaitForSecondsRealtime(_currentDialogue.autoAdvanceDelay);
            AdvanceDialogue();
        }
    }

    private bool CheckCondition(DialogueCondition condition)
    {
        if (condition.type == DialogueConditionType.None) return true;

        // Obtenir les references necessaires
        var player = GameManager.Instance?.Player;
        PlayerStats playerStats = player?.GetComponent<PlayerStats>();
        Inventory inventory = player?.GetComponent<Inventory>();

        switch (condition.type)
        {
            case DialogueConditionType.PlayerLevel:
                if (playerStats == null) return false;
                return CompareValues(playerStats.Level, condition.requiredValue, condition.comparison);

            case DialogueConditionType.HasItem:
                if (inventory == null) return false;
                int itemCount = inventory.GetItemCount(condition.targetId);
                return CompareValues(itemCount, condition.requiredValue, condition.comparison);

            case DialogueConditionType.QuestCompleted:
                if (QuestManager.Instance == null) return false;
                bool completed = QuestManager.Instance.IsQuestCompleted(condition.targetId);
                return condition.comparison == ComparisonType.Equal ? completed : !completed;

            case DialogueConditionType.QuestActive:
                if (QuestManager.Instance == null) return false;
                bool active = QuestManager.Instance.IsQuestActive(condition.targetId);
                return condition.comparison == ComparisonType.Equal ? active : !active;

            case DialogueConditionType.Reputation:
                // TODO: Implementer systeme de reputation
                return true;

            case DialogueConditionType.GlobalFlag:
                // TODO: Implementer systeme de flags globaux
                return true;

            default:
                return true;
        }
    }

    private bool CompareValues(int a, int b, ComparisonType comparison)
    {
        return comparison switch
        {
            ComparisonType.Equal => a == b,
            ComparisonType.NotEqual => a != b,
            ComparisonType.GreaterThan => a > b,
            ComparisonType.GreaterOrEqual => a >= b,
            ComparisonType.LessThan => a < b,
            ComparisonType.LessOrEqual => a <= b,
            _ => true
        };
    }

    private void ApplyEffects(DialogueEffect[] effects)
    {
        if (effects == null) return;

        var player = GameManager.Instance?.Player;
        PlayerStats playerStats = player?.GetComponent<PlayerStats>();
        Inventory inventory = player?.GetComponent<Inventory>();

        foreach (var effect in effects)
        {
            switch (effect.type)
            {
                case DialogueEffectType.GiveQuest:
                    if (QuestManager.Instance != null)
                    {
                        var quest = Resources.Load<QuestData>($"Quests/{effect.targetId}");
                        if (quest != null)
                        {
                            QuestManager.Instance.AcceptQuest(quest);
                        }
                    }
                    break;

                case DialogueEffectType.GiveItem:
                    if (inventory != null)
                    {
                        var item = Resources.Load<ItemData>($"Items/{effect.targetId}");
                        if (item != null)
                        {
                            inventory.AddItem(item, effect.value > 0 ? effect.value : 1);
                        }
                    }
                    break;

                case DialogueEffectType.RemoveItem:
                    if (inventory != null)
                    {
                        inventory.RemoveItemById(effect.targetId, effect.value > 0 ? effect.value : 1);
                    }
                    break;

                case DialogueEffectType.GiveXP:
                    if (playerStats != null)
                    {
                        playerStats.AddExperience(effect.value);
                    }
                    break;

                case DialogueEffectType.GiveGold:
                    if (playerStats != null)
                    {
                        playerStats.AddGold(effect.value);
                    }
                    break;

                case DialogueEffectType.CompleteObjective:
                    if (QuestManager.Instance != null)
                    {
                        // Mettre a jour l'objectif avec le type Talk/Interact
                        QuestManager.Instance.UpdateObjective(QuestObjectiveType.Talk, effect.targetId, effect.value > 0 ? effect.value : 1);
                    }
                    break;

                case DialogueEffectType.PlayAnimation:
                    // TODO: Jouer animation sur NPC
                    break;

                case DialogueEffectType.ChangeScene:
                    // TODO: Changer de scene
                    break;

                default:
                    break;
            }
        }
    }

    #endregion
}
