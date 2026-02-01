using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Type de noeud dans l'arbre de competences.
/// </summary>
public enum SkillNodeType
{
    Skill,          // Debloque une competence active
    Passive,        // Bonus passif permanent
    StatBonus,      // Augmentation de stats
    Mastery         // Amelioration d'une competence existante
}

/// <summary>
/// Donnees d'un noeud dans l'arbre de competences.
/// </summary>
[Serializable]
public class SkillTreeNode
{
    [Header("Identification")]
    public string nodeId;
    public string nodeName;
    [TextArea(2, 3)]
    public string description;
    public Sprite icon;

    [Header("Type & Skill")]
    public SkillNodeType nodeType;
    public SkillData skillToUnlock;

    [Header("Stat Bonuses")]
    public float healthBonus;
    public float manaBonus;
    public float staminaBonus;
    public float attackBonus;
    public float defenseBonus;
    public float critChanceBonus;
    public float critDamageBonus;

    [Header("Requirements")]
    public int requiredLevel = 1;
    public int skillPointCost = 1;
    public string[] prerequisiteNodeIds;

    [Header("Position (for UI)")]
    public Vector2 position;
    public int tier; // Rang dans l'arbre (0 = base, 1 = tier 1, etc.)

    [Header("Mastery")]
    public int maxRank = 1;
    public float bonusPerRank = 0.1f;
}

/// <summary>
/// Arbre de competences complet.
/// </summary>
[CreateAssetMenu(fileName = "NewSkillTree", menuName = "EpicLegends/Skill Tree")]
public class SkillTreeData : ScriptableObject
{
    [Header("Identification")]
    public string treeId;
    public string treeName;
    [TextArea(2, 4)]
    public string description;
    public Sprite treeIcon;

    [Header("Associated Class")]
    public ClassData associatedClass;

    [Header("Nodes")]
    public List<SkillTreeNode> nodes = new List<SkillTreeNode>();

    /// <summary>
    /// Trouve un noeud par son ID.
    /// </summary>
    public SkillTreeNode GetNode(string nodeId)
    {
        return nodes.Find(n => n.nodeId == nodeId);
    }

    /// <summary>
    /// Obtient les noeuds racines (sans prerequis).
    /// </summary>
    public List<SkillTreeNode> GetRootNodes()
    {
        return nodes.FindAll(n => n.prerequisiteNodeIds == null || n.prerequisiteNodeIds.Length == 0);
    }

    /// <summary>
    /// Obtient les noeuds d'un tier specifique.
    /// </summary>
    public List<SkillTreeNode> GetNodesByTier(int tier)
    {
        return nodes.FindAll(n => n.tier == tier);
    }

    /// <summary>
    /// Obtient les noeuds qui dependent d'un noeud donne.
    /// </summary>
    public List<SkillTreeNode> GetDependentNodes(string nodeId)
    {
        return nodes.FindAll(n =>
            n.prerequisiteNodeIds != null &&
            Array.Exists(n.prerequisiteNodeIds, id => id == nodeId));
    }

    /// <summary>
    /// Valide la structure de l'arbre.
    /// </summary>
    public bool Validate(out List<string> errors)
    {
        errors = new List<string>();

        HashSet<string> nodeIds = new HashSet<string>();

        foreach (var node in nodes)
        {
            // Verifier les IDs uniques
            if (string.IsNullOrEmpty(node.nodeId))
            {
                errors.Add($"Node sans ID trouve");
                continue;
            }

            if (nodeIds.Contains(node.nodeId))
            {
                errors.Add($"ID duplique: {node.nodeId}");
            }
            nodeIds.Add(node.nodeId);

            // Verifier les prerequis
            if (node.prerequisiteNodeIds != null)
            {
                foreach (var prereqId in node.prerequisiteNodeIds)
                {
                    if (!string.IsNullOrEmpty(prereqId) && GetNode(prereqId) == null)
                    {
                        errors.Add($"Prerequis invalide '{prereqId}' pour le noeud '{node.nodeId}'");
                    }
                }
            }

            // Verifier les skills
            if (node.nodeType == SkillNodeType.Skill && node.skillToUnlock == null)
            {
                errors.Add($"Noeud skill '{node.nodeId}' sans skill assigne");
            }
        }

        return errors.Count == 0;
    }
}
