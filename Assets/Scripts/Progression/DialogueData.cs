using UnityEngine;

/// <summary>
/// Donnees d'un dialogue.
/// Definit les noeuds, choix et conditions d'un dialogue.
/// </summary>
[CreateAssetMenu(fileName = "NewDialogue", menuName = "EpicLegends/Progression/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("ID unique du dialogue")]
    public string dialogueId;

    [Tooltip("Titre du dialogue")]
    public string dialogueTitle;

    [Tooltip("NPC associe")]
    public string npcId;

    #endregion

    #region Nodes

    [Header("Noeuds")]
    [Tooltip("Noeuds de dialogue")]
    public DialogueNode[] nodes;

    [Tooltip("ID du premier noeud")]
    public string startNodeId;

    #endregion

    #region Settings

    [Header("Parametres")]
    [Tooltip("Peut etre saute")]
    public bool isSkippable = true;

    [Tooltip("Affiche le nom du speaker")]
    public bool showSpeakerName = true;

    [Tooltip("Vitesse du texte")]
    public float textSpeed = 1f;

    [Tooltip("Delai automatique entre phrases")]
    public float autoAdvanceDelay = 0f;

    #endregion

    #region Audio

    [Header("Audio")]
    [Tooltip("Musique de fond")]
    public AudioClip backgroundMusic;

    [Tooltip("Son de texte")]
    public AudioClip textSound;

    #endregion

    #region Public Methods

    /// <summary>
    /// Obtient un noeud par son ID.
    /// </summary>
    /// <param name="nodeId">ID du noeud.</param>
    /// <returns>Noeud ou null.</returns>
    public DialogueNode GetNode(string nodeId)
    {
        if (nodes == null || string.IsNullOrEmpty(nodeId)) return null;

        foreach (var node in nodes)
        {
            if (node.nodeId == nodeId) return node;
        }

        return null;
    }

    /// <summary>
    /// Obtient le premier noeud.
    /// </summary>
    /// <returns>Premier noeud ou null.</returns>
    public DialogueNode GetStartNode()
    {
        if (!string.IsNullOrEmpty(startNodeId))
        {
            return GetNode(startNodeId);
        }

        if (nodes != null && nodes.Length > 0)
        {
            return nodes[0];
        }

        return null;
    }

    /// <summary>
    /// Obtient le prochain noeud apres un choix.
    /// </summary>
    /// <param name="currentNode">Noeud actuel.</param>
    /// <param name="choiceIndex">Index du choix (ou -1 pour suivant par defaut).</param>
    /// <returns>Prochain noeud ou null.</returns>
    public DialogueNode GetNextNode(DialogueNode currentNode, int choiceIndex = -1)
    {
        if (currentNode == null) return null;

        string nextId = null;

        if (choiceIndex >= 0 && currentNode.choices != null &&
            choiceIndex < currentNode.choices.Length)
        {
            nextId = currentNode.choices[choiceIndex].nextNodeId;
        }
        else if (!string.IsNullOrEmpty(currentNode.defaultNextNodeId))
        {
            nextId = currentNode.defaultNextNodeId;
        }

        return !string.IsNullOrEmpty(nextId) ? GetNode(nextId) : null;
    }

    #endregion
}

/// <summary>
/// Noeud de dialogue.
/// </summary>
[System.Serializable]
public class DialogueNode
{
    [Tooltip("ID unique du noeud")]
    public string nodeId;

    [Tooltip("Nom du speaker")]
    public string speakerName;

    [Tooltip("Portrait du speaker")]
    public Sprite speakerPortrait;

    [Tooltip("Texte du dialogue")]
    [TextArea(3, 6)]
    public string dialogueText;

    [Tooltip("Clip audio voiceover")]
    public AudioClip voiceClip;

    [Tooltip("Animation du speaker")]
    public string speakerAnimation;

    [Tooltip("Choix disponibles (vide = pas de choix)")]
    public DialogueChoice[] choices;

    [Tooltip("ID du prochain noeud par defaut")]
    public string defaultNextNodeId;

    [Tooltip("Condition pour afficher ce noeud")]
    public DialogueCondition condition;

    [Tooltip("Effets a appliquer")]
    public DialogueEffect[] effects;

    /// <summary>Noeud terminal (fin du dialogue)?</summary>
    public bool IsEndNode => string.IsNullOrEmpty(defaultNextNodeId) &&
                              (choices == null || choices.Length == 0);

    /// <summary>Noeud avec choix?</summary>
    public bool HasChoices => choices != null && choices.Length > 0;
}

/// <summary>
/// Choix de dialogue.
/// </summary>
[System.Serializable]
public struct DialogueChoice
{
    [Tooltip("Texte du choix")]
    public string choiceText;

    [Tooltip("ID du prochain noeud")]
    public string nextNodeId;

    [Tooltip("Condition pour afficher")]
    public DialogueCondition condition;

    [Tooltip("Effets du choix")]
    public DialogueEffect[] effects;

    [Tooltip("Choix deja selectionne (grise)")]
    public bool wasSelected;
}

/// <summary>
/// Condition de dialogue.
/// </summary>
[System.Serializable]
public struct DialogueCondition
{
    [Tooltip("Type de condition")]
    public DialogueConditionType type;

    [Tooltip("Cible de la condition")]
    public string targetId;

    [Tooltip("Valeur requise")]
    public int requiredValue;

    [Tooltip("Comparaison")]
    public ComparisonType comparison;
}

/// <summary>
/// Types de conditions.
/// </summary>
public enum DialogueConditionType
{
    /// <summary>Aucune condition.</summary>
    None,

    /// <summary>Quete completee.</summary>
    QuestCompleted,

    /// <summary>Quete active.</summary>
    QuestActive,

    /// <summary>Niveau minimum.</summary>
    PlayerLevel,

    /// <summary>Reputation minimum.</summary>
    Reputation,

    /// <summary>Item possede.</summary>
    HasItem,

    /// <summary>Flag global.</summary>
    GlobalFlag,

    /// <summary>Classe du joueur.</summary>
    PlayerClass,

    /// <summary>Creature capturee.</summary>
    HasCreature
}

/// <summary>
/// Type de comparaison.
/// </summary>
public enum ComparisonType
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual
}

/// <summary>
/// Effet d'un dialogue.
/// </summary>
[System.Serializable]
public struct DialogueEffect
{
    [Tooltip("Type d'effet")]
    public DialogueEffectType type;

    [Tooltip("Cible")]
    public string targetId;

    [Tooltip("Valeur")]
    public int value;

    [Tooltip("Valeur string")]
    public string stringValue;
}

/// <summary>
/// Types d'effets.
/// </summary>
public enum DialogueEffectType
{
    /// <summary>Aucun effet.</summary>
    None,

    /// <summary>Donner une quete.</summary>
    GiveQuest,

    /// <summary>Completer un objectif.</summary>
    CompleteObjective,

    /// <summary>Donner un item.</summary>
    GiveItem,

    /// <summary>Retirer un item.</summary>
    RemoveItem,

    /// <summary>Modifier la reputation.</summary>
    ChangeReputation,

    /// <summary>Donner de l'XP.</summary>
    GiveXP,

    /// <summary>Donner de l'or.</summary>
    GiveGold,

    /// <summary>Definir un flag.</summary>
    SetFlag,

    /// <summary>Jouer une animation.</summary>
    PlayAnimation,

    /// <summary>Changer de scene.</summary>
    ChangeScene,

    /// <summary>Debloquer un lieu.</summary>
    UnlockLocation
}
