using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Component pour les NPCs interactifs.
/// Permet d'interagir avec les NPCs pour declencher des dialogues.
/// </summary>
public class NPCInteractable : MonoBehaviour
{
    #region Serialized Fields

    [Header("NPC Info")]
    [SerializeField] private string _npcName = "NPC";
    [SerializeField] private string _npcId;
    [SerializeField] private NPCType _npcType = NPCType.Generic;

    [Header("Dialogue")]
    [SerializeField] private DialogueData _defaultDialogue;
    [SerializeField] private DialogueData[] _conditionalDialogues;

    [Header("Interaction")]
    [SerializeField] private float _interactionRadius = 2f;
    [SerializeField] private KeyCode _interactKey = KeyCode.E;
    [SerializeField] private bool _facePlayerOnInteract = true;
    [SerializeField] private float _rotationSpeed = 5f;

    [Header("UI")]
    [SerializeField] private GameObject _interactionPrompt;
    [SerializeField] private string _promptText = "Appuyez sur E pour parler";

    [Header("Visual")]
    [SerializeField] private GameObject _questMarker;
    [SerializeField] private GameObject _exclamationMark;
    [SerializeField] private GameObject _questionMark;

    #endregion

    #region Private Fields

    private Transform _player;
    private bool _playerInRange;
    private bool _isInteracting;
    private InputAction _interactAction;

    #endregion

    #region Properties

    public string NPCName => _npcName;
    public string NPCID => _npcId;
    public NPCType Type => _npcType;
    public bool IsInteracting => _isInteracting;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        // Trouver le joueur
        if (GameManager.Instance != null && GameManager.Instance.Player != null)
        {
            _player = GameManager.Instance.Player.transform;
        }
        else
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                _player = playerObj.transform;
            }
        }

        // Cacher le prompt au debut
        if (_interactionPrompt != null)
        {
            _interactionPrompt.SetActive(false);
        }

        // Mettre a jour les marqueurs de quete
        UpdateQuestMarkers();

        // Setup Input Action
        SetupInputAction();
    }

    private void SetupInputAction()
    {
        _interactAction = new InputAction("NPCInteract", InputActionType.Button);
        _interactAction.AddBinding("<Keyboard>/e");
        _interactAction.AddBinding("<Gamepad>/buttonNorth"); // Y button
        _interactAction.performed += OnInteractPerformed;
        _interactAction.Enable();
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (_playerInRange && !_isInteracting)
        {
            Interact();
        }
    }

    private void Update()
    {
        if (_player == null) return;

        // Verifier la distance
        float distance = Vector3.Distance(transform.position, _player.position);
        bool wasInRange = _playerInRange;
        _playerInRange = distance <= _interactionRadius;

        // Gerer le prompt
        if (_playerInRange != wasInRange)
        {
            if (_interactionPrompt != null)
            {
                _interactionPrompt.SetActive(_playerInRange && !_isInteracting);
            }
        }

        // Rotation vers le joueur pendant l'interaction
        if (_isInteracting && _facePlayerOnInteract)
        {
            FacePlayer();
        }
    }

    private void OnEnable()
    {
        // S'abonner aux events du DialogueManager
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueEnded += OnDialogueEnded;
        }
        _interactAction?.Enable();
    }

    private void OnDisable()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueEnded -= OnDialogueEnded;
        }
        _interactAction?.Disable();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Interagit avec le NPC.
    /// </summary>
    public void Interact()
    {
        if (_isInteracting) return;

        _isInteracting = true;

        // Cacher le prompt
        if (_interactionPrompt != null)
        {
            _interactionPrompt.SetActive(false);
        }

        // Trouver le bon dialogue
        DialogueData dialogueToUse = GetAppropriateDialogue();

        if (dialogueToUse != null)
        {
            DialogueManager.Instance?.StartDialogue(dialogueToUse);
        }
        else
        {
            Debug.LogWarning($"[NPCInteractable] {_npcName} n'a pas de dialogue!");
            _isInteracting = false;
        }
    }

    /// <summary>
    /// Force l'affichage d'un dialogue specifique.
    /// </summary>
    public void TriggerDialogue(DialogueData dialogue)
    {
        if (dialogue == null) return;

        _isInteracting = true;

        if (_interactionPrompt != null)
        {
            _interactionPrompt.SetActive(false);
        }

        DialogueManager.Instance?.StartDialogue(dialogue);
    }

    /// <summary>
    /// Met a jour les marqueurs de quete visuels.
    /// </summary>
    public void UpdateQuestMarkers()
    {
        bool hasNewQuest = false;
        bool hasActiveQuest = false;

        // Verifier si le NPC a une quete a donner
        if (_conditionalDialogues != null)
        {
            foreach (var dialogue in _conditionalDialogues)
            {
                if (dialogue == null || dialogue.nodes == null) continue;

                foreach (var node in dialogue.nodes)
                {
                    if (node.effects == null) continue;

                    foreach (var effect in node.effects)
                    {
                        if (effect.type == DialogueEffectType.GiveQuest)
                        {
                            hasNewQuest = true;
                            break;
                        }
                    }
                }
            }
        }

        // Verifier si le NPC est lie a une quete active
        if (QuestManager.Instance != null)
        {
            // TODO: Verifier si ce NPC est un objectif de quete active
        }

        // Mettre a jour les marqueurs
        if (_exclamationMark != null)
        {
            _exclamationMark.SetActive(hasNewQuest);
        }
        if (_questionMark != null)
        {
            _questionMark.SetActive(hasActiveQuest && !hasNewQuest);
        }
    }

    #endregion

    #region Private Methods

    private void OnDialogueEnded()
    {
        _isInteracting = false;

        // Reafficher le prompt si le joueur est toujours a portee
        if (_playerInRange && _interactionPrompt != null)
        {
            _interactionPrompt.SetActive(true);
        }

        // Mettre a jour les marqueurs
        UpdateQuestMarkers();
    }

    private DialogueData GetAppropriateDialogue()
    {
        // Verifier les dialogues conditionnels d'abord
        if (_conditionalDialogues != null)
        {
            foreach (var dialogue in _conditionalDialogues)
            {
                if (dialogue == null) continue;

                // Verifier les conditions du dialogue
                // TODO: Ajouter systeme de conditions sur DialogueData

                // Pour l'instant, retourner le premier dialogue valide
                var startNode = dialogue.GetStartNode();
                if (startNode != null)
                {
                    return dialogue;
                }
            }
        }

        // Retourner le dialogue par defaut
        return _defaultDialogue;
    }

    private void FacePlayer()
    {
        if (_player == null) return;

        Vector3 direction = (_player.position - transform.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                _rotationSpeed * Time.deltaTime
            );
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _interactionRadius);
    }

    #endregion
}

/// <summary>
/// Types de NPCs.
/// </summary>
public enum NPCType
{
    Generic,
    QuestGiver,
    Merchant,
    Blacksmith,
    Healer,
    Trainer,
    Guard,
    Innkeeper
}
