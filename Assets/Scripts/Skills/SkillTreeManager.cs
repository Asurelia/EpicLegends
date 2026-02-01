using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire des arbres de competences du joueur.
/// </summary>
public class SkillTreeManager : MonoBehaviour
{
    #region Singleton

    public static SkillTreeManager Instance { get; private set; }

    #endregion

    #region Events

    public event Action<SkillTreeNode> OnNodeUnlocked;
    public event Action<SkillTreeNode, int> OnNodeRankUp; // node, newRank
    public event Action<int> OnSkillPointsChanged;

    #endregion

    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private SkillTreeData[] _availableTrees;

    #endregion

    #region Private Fields

    private int _skillPoints = 0;
    private Dictionary<string, NodeProgress> _nodeProgress = new Dictionary<string, NodeProgress>();
    private HashSet<string> _unlockedNodes = new HashSet<string>();

    #endregion

    #region Properties

    public int SkillPoints
    {
        get => _skillPoints;
        private set
        {
            _skillPoints = value;
            OnSkillPointsChanged?.Invoke(_skillPoints);
        }
    }

    public SkillTreeData[] AvailableTrees => _availableTrees;

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
    }

    private void Start()
    {
        // S'abonner aux events de level up pour gagner des points
        var player = GameManager.Instance?.Player;
        if (player != null)
        {
            var stats = player.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.OnLevelUp += OnPlayerLevelUp;
            }
        }
    }

    #endregion

    #region Public Methods - Node Unlocking

    /// <summary>
    /// Tente de debloquer un noeud.
    /// </summary>
    public bool TryUnlockNode(string treeId, string nodeId)
    {
        var tree = GetTree(treeId);
        if (tree == null) return false;

        var node = tree.GetNode(nodeId);
        if (node == null) return false;

        return TryUnlockNode(tree, node);
    }

    /// <summary>
    /// Tente de debloquer un noeud.
    /// </summary>
    public bool TryUnlockNode(SkillTreeData tree, SkillTreeNode node)
    {
        // Verifier si deja debloque
        if (IsNodeUnlocked(node.nodeId))
        {
            // Verifier si on peut augmenter le rang
            if (node.maxRank > 1)
            {
                return TryRankUpNode(node);
            }
            return false;
        }

        // Verifier les prerequis
        if (!ArePrerequisitesMet(node))
        {
            Debug.Log($"[SkillTreeManager] Prerequis non remplis pour {node.nodeName}");
            return false;
        }

        // Verifier le niveau requis
        var playerStats = GameManager.Instance?.PlayerStats;
        if (playerStats != null && playerStats.Level < node.requiredLevel)
        {
            Debug.Log($"[SkillTreeManager] Niveau insuffisant pour {node.nodeName}");
            return false;
        }

        // Verifier les points de competence
        if (SkillPoints < node.skillPointCost)
        {
            Debug.Log($"[SkillTreeManager] Points insuffisants pour {node.nodeName}");
            return false;
        }

        // Debloquer le noeud
        UnlockNode(node);
        return true;
    }

    /// <summary>
    /// Tente d'augmenter le rang d'un noeud.
    /// </summary>
    public bool TryRankUpNode(SkillTreeNode node)
    {
        if (!IsNodeUnlocked(node.nodeId)) return false;

        var progress = GetNodeProgress(node.nodeId);
        if (progress.currentRank >= node.maxRank) return false;

        if (SkillPoints < node.skillPointCost) return false;

        SkillPoints -= node.skillPointCost;
        progress.currentRank++;

        ApplyNodeBonuses(node, 1);

        OnNodeRankUp?.Invoke(node, progress.currentRank);

        Debug.Log($"[SkillTreeManager] {node.nodeName} rang {progress.currentRank}");

        return true;
    }

    /// <summary>
    /// Ajoute des points de competence.
    /// </summary>
    public void AddSkillPoints(int amount)
    {
        if (amount > 0)
        {
            SkillPoints += amount;
        }
    }

    #endregion

    #region Public Methods - Query

    /// <summary>
    /// Verifie si un noeud est debloque.
    /// </summary>
    public bool IsNodeUnlocked(string nodeId)
    {
        return _unlockedNodes.Contains(nodeId);
    }

    /// <summary>
    /// Verifie si les prerequis d'un noeud sont remplis.
    /// </summary>
    public bool ArePrerequisitesMet(SkillTreeNode node)
    {
        if (node.prerequisiteNodeIds == null || node.prerequisiteNodeIds.Length == 0)
            return true;

        foreach (var prereqId in node.prerequisiteNodeIds)
        {
            if (!string.IsNullOrEmpty(prereqId) && !IsNodeUnlocked(prereqId))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Verifie si un noeud peut etre debloque.
    /// </summary>
    public bool CanUnlockNode(SkillTreeNode node)
    {
        if (IsNodeUnlocked(node.nodeId))
        {
            // Peut-on augmenter le rang?
            var progress = GetNodeProgress(node.nodeId);
            return progress.currentRank < node.maxRank && SkillPoints >= node.skillPointCost;
        }

        if (!ArePrerequisitesMet(node)) return false;

        var playerStats = GameManager.Instance?.PlayerStats;
        if (playerStats != null && playerStats.Level < node.requiredLevel)
            return false;

        return SkillPoints >= node.skillPointCost;
    }

    /// <summary>
    /// Obtient un arbre par son ID.
    /// </summary>
    public SkillTreeData GetTree(string treeId)
    {
        if (_availableTrees == null) return null;

        foreach (var tree in _availableTrees)
        {
            if (tree != null && tree.treeId == treeId)
                return tree;
        }
        return null;
    }

    /// <summary>
    /// Obtient la progression d'un noeud.
    /// </summary>
    public NodeProgress GetNodeProgress(string nodeId)
    {
        if (!_nodeProgress.TryGetValue(nodeId, out var progress))
        {
            progress = new NodeProgress { nodeId = nodeId, currentRank = 0 };
            _nodeProgress[nodeId] = progress;
        }
        return progress;
    }

    /// <summary>
    /// Obtient les noeuds debloques d'un arbre.
    /// </summary>
    public List<SkillTreeNode> GetUnlockedNodes(SkillTreeData tree)
    {
        var unlocked = new List<SkillTreeNode>();
        foreach (var node in tree.nodes)
        {
            if (IsNodeUnlocked(node.nodeId))
            {
                unlocked.Add(node);
            }
        }
        return unlocked;
    }

    /// <summary>
    /// Compte le nombre total de noeuds debloques.
    /// </summary>
    public int GetTotalUnlockedNodes()
    {
        return _unlockedNodes.Count;
    }

    #endregion

    #region Public Methods - Save/Load

    /// <summary>
    /// Obtient les donnees de sauvegarde.
    /// </summary>
    public SkillTreeSaveData GetSaveData()
    {
        return new SkillTreeSaveData
        {
            skillPoints = _skillPoints,
            unlockedNodes = new List<string>(_unlockedNodes),
            nodeProgressList = new List<NodeProgress>(_nodeProgress.Values)
        };
    }

    /// <summary>
    /// Charge les donnees de sauvegarde.
    /// </summary>
    public void LoadSaveData(SkillTreeSaveData data)
    {
        if (data == null) return;

        _skillPoints = data.skillPoints;

        _unlockedNodes.Clear();
        if (data.unlockedNodes != null)
        {
            foreach (var nodeId in data.unlockedNodes)
            {
                _unlockedNodes.Add(nodeId);
            }
        }

        _nodeProgress.Clear();
        if (data.nodeProgressList != null)
        {
            foreach (var progress in data.nodeProgressList)
            {
                _nodeProgress[progress.nodeId] = progress;
            }
        }
    }

    /// <summary>
    /// Reinitialise l'arbre de competences (refund).
    /// </summary>
    public void ResetAllTrees()
    {
        // Calculer les points a rembourser
        int refundPoints = 0;
        foreach (var nodeId in _unlockedNodes)
        {
            foreach (var tree in _availableTrees)
            {
                var node = tree?.GetNode(nodeId);
                if (node != null)
                {
                    var progress = GetNodeProgress(nodeId);
                    refundPoints += node.skillPointCost * progress.currentRank;
                    break;
                }
            }
        }

        // Retirer les bonus
        foreach (var nodeId in _unlockedNodes)
        {
            foreach (var tree in _availableTrees)
            {
                var node = tree?.GetNode(nodeId);
                if (node != null)
                {
                    var progress = GetNodeProgress(nodeId);
                    RemoveNodeBonuses(node, progress.currentRank);
                    break;
                }
            }
        }

        _unlockedNodes.Clear();
        _nodeProgress.Clear();
        SkillPoints += refundPoints;

        Debug.Log($"[SkillTreeManager] Arbres reinitialises, {refundPoints} points rembourses");
    }

    #endregion

    #region Private Methods

    private void UnlockNode(SkillTreeNode node)
    {
        SkillPoints -= node.skillPointCost;
        _unlockedNodes.Add(node.nodeId);

        var progress = GetNodeProgress(node.nodeId);
        progress.currentRank = 1;

        // Appliquer les effets du noeud
        ApplyNodeBonuses(node, 1);

        // Si c'est un skill, le debloquer
        if (node.nodeType == SkillNodeType.Skill && node.skillToUnlock != null)
        {
            UnlockSkill(node.skillToUnlock);
        }

        OnNodeUnlocked?.Invoke(node);

        Debug.Log($"[SkillTreeManager] Noeud debloque: {node.nodeName}");
    }

    private void ApplyNodeBonuses(SkillTreeNode node, int ranks)
    {
        var playerStats = GameManager.Instance?.PlayerStats;
        if (playerStats == null) return;

        // Les bonus de stats sont appliques via un systeme de modificateurs
        // TODO: Implementer un systeme de stat modifiers plus robuste
        // Pour l'instant, on log les bonus
        float multiplier = ranks * (node.maxRank > 1 ? node.bonusPerRank : 1f);

        if (node.healthBonus > 0)
            Debug.Log($"[SkillTreeManager] +{node.healthBonus * multiplier} Health");
        if (node.manaBonus > 0)
            Debug.Log($"[SkillTreeManager] +{node.manaBonus * multiplier} Mana");
        if (node.attackBonus > 0)
            Debug.Log($"[SkillTreeManager] +{node.attackBonus * multiplier}% Attack");
        if (node.defenseBonus > 0)
            Debug.Log($"[SkillTreeManager] +{node.defenseBonus * multiplier}% Defense");
    }

    private void RemoveNodeBonuses(SkillTreeNode node, int ranks)
    {
        // Inverse de ApplyNodeBonuses
        // TODO: Implementer quand le systeme de stat modifiers sera en place
    }

    private void UnlockSkill(SkillData skill)
    {
        // Ajouter le skill au controller du joueur
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        var skillController = player.GetComponent<SkillController>();
        if (skillController != null)
        {
            // TODO: Implementer SkillController.UnlockSkill()
            Debug.Log($"[SkillTreeManager] Skill debloque: {skill.skillName}");
        }
    }

    private void OnPlayerLevelUp(int newLevel)
    {
        // Donner 1 point de competence par niveau
        AddSkillPoints(1);
    }

    #endregion
}

/// <summary>
/// Progression d'un noeud.
/// </summary>
[Serializable]
public class NodeProgress
{
    public string nodeId;
    public int currentRank;
}

/// <summary>
/// Donnees de sauvegarde de l'arbre de competences.
/// </summary>
[Serializable]
public class SkillTreeSaveData
{
    public int skillPoints;
    public List<string> unlockedNodes;
    public List<NodeProgress> nodeProgressList;
}
