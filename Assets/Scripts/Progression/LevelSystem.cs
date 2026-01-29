using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Systeme de niveau et d'experience du joueur.
/// Gere la progression, les points de stats et de competences.
/// </summary>
public class LevelSystem : MonoBehaviour
{
    #region Constants

    /// <summary>Niveau maximum atteignable.</summary>
    public const int MAX_LEVEL = 100;

    /// <summary>Points de stats par niveau.</summary>
    private const int STAT_POINTS_PER_LEVEL = 3;

    /// <summary>Points de competences par niveau.</summary>
    private const int SKILL_POINTS_PER_LEVEL = 1;

    /// <summary>XP de base pour le niveau 2.</summary>
    private const int BASE_XP = 100;

    /// <summary>Facteur de croissance exponentielle.</summary>
    private const float XP_GROWTH_FACTOR = 1.15f;

    #endregion

    #region Fields

    [Header("Etat actuel")]
    [SerializeField] private int _currentLevel = 1;
    [SerializeField] private int _currentXP = 0;
    [SerializeField] private int _totalXP = 0;

    [Header("Points disponibles")]
    [SerializeField] private int _availableStatPoints = 0;
    [SerializeField] private int _availableSkillPoints = 0;

    [Header("Configuration")]
    [SerializeField] private bool _showLevelUpEffects = true;
    [SerializeField] private GameObject _levelUpEffectPrefab;

    // Points de stats alloues
    private Dictionary<StatType, int> _allocatedStats;

    // Cache XP par niveau
    private int[] _xpTable;

    #endregion

    #region Events

    /// <summary>Declenche lors d'un gain d'XP.</summary>
    public event Action<int, XPSource> OnXPGained;

    /// <summary>Declenche lors d'un level up.</summary>
    public event Action<int> OnLevelUp;

    /// <summary>Declenche lors d'une allocation de points.</summary>
    public event Action<StatType, int> OnStatPointAllocated;

    #endregion

    #region Properties

    /// <summary>Niveau actuel du joueur.</summary>
    public int CurrentLevel => _currentLevel;

    /// <summary>XP actuelle dans le niveau courant.</summary>
    public int CurrentXP => _currentXP;

    /// <summary>XP totale accumulee.</summary>
    public int TotalXP => _totalXP;

    /// <summary>Points de stats disponibles.</summary>
    public int AvailableStatPoints => _availableStatPoints;

    /// <summary>Points de competences disponibles.</summary>
    public int AvailableSkillPoints => _availableSkillPoints;

    /// <summary>XP requise pour le prochain niveau.</summary>
    public int XPToNextLevel => GetXPForLevel(_currentLevel + 1) - GetTotalXPForLevel(_currentLevel);

    /// <summary>Progression vers le prochain niveau (0-1).</summary>
    public float LevelProgress => (float)_currentXP / XPToNextLevel;

    /// <summary>Niveau maximum atteint?</summary>
    public bool IsMaxLevel => _currentLevel >= MAX_LEVEL;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _allocatedStats = new Dictionary<StatType, int>();
        InitializeXPTable();
        InitializeAllocatedStats();
    }

    #endregion

    #region Public Methods - XP

    /// <summary>
    /// Ajoute de l'experience au joueur.
    /// </summary>
    /// <param name="amount">Quantite d'XP a ajouter.</param>
    /// <param name="source">Source de l'XP.</param>
    public void AddXP(int amount, XPSource source = XPSource.Combat)
    {
        if (amount <= 0 || IsMaxLevel) return;

        _currentXP += amount;
        _totalXP += amount;

        OnXPGained?.Invoke(amount, source);

        // Verifier les level ups
        CheckLevelUp();
    }

    /// <summary>
    /// Obtient l'XP requise pour atteindre un niveau specifique.
    /// </summary>
    /// <param name="level">Niveau cible.</param>
    /// <returns>XP requise depuis le niveau precedent.</returns>
    public int GetXPForLevel(int level)
    {
        if (level <= 1) return 0;
        if (level > MAX_LEVEL) return int.MaxValue;

        if (_xpTable == null) InitializeXPTable();

        return _xpTable[level - 1];
    }

    /// <summary>
    /// Obtient l'XP totale requise depuis le niveau 1.
    /// </summary>
    /// <param name="level">Niveau cible.</param>
    /// <returns>XP totale requise.</returns>
    public int GetTotalXPForLevel(int level)
    {
        if (level <= 1) return 0;

        int total = 0;
        for (int i = 2; i <= level; i++)
        {
            total += GetXPForLevel(i);
        }
        return total;
    }

    #endregion

    #region Public Methods - Stat Points

    /// <summary>
    /// Alloue un point de stat a une caracteristique.
    /// </summary>
    /// <param name="stat">Type de stat.</param>
    /// <returns>True si l'allocation a reussi.</returns>
    public bool AllocateStatPoint(StatType stat)
    {
        if (_availableStatPoints <= 0) return false;

        if (!_allocatedStats.ContainsKey(stat))
        {
            _allocatedStats[stat] = 0;
        }

        _allocatedStats[stat]++;
        _availableStatPoints--;

        OnStatPointAllocated?.Invoke(stat, _allocatedStats[stat]);

        return true;
    }

    /// <summary>
    /// Alloue plusieurs points a une stat.
    /// </summary>
    /// <param name="stat">Type de stat.</param>
    /// <param name="points">Nombre de points.</param>
    /// <returns>Nombre de points effectivement alloues.</returns>
    public int AllocateStatPoints(StatType stat, int points)
    {
        int allocated = 0;
        for (int i = 0; i < points && _availableStatPoints > 0; i++)
        {
            if (AllocateStatPoint(stat))
            {
                allocated++;
            }
        }
        return allocated;
    }

    /// <summary>
    /// Obtient le nombre de points alloues a une stat.
    /// </summary>
    /// <param name="stat">Type de stat.</param>
    /// <returns>Points alloues.</returns>
    public int GetAllocatedPoints(StatType stat)
    {
        if (_allocatedStats == null) return 0;
        return _allocatedStats.TryGetValue(stat, out int points) ? points : 0;
    }

    /// <summary>
    /// Reinitialise tous les points de stats.
    /// </summary>
    /// <returns>Nombre total de points recuperes.</returns>
    public int ResetStatPoints()
    {
        int totalRecovered = 0;

        if (_allocatedStats != null)
        {
            foreach (var kvp in _allocatedStats)
            {
                totalRecovered += kvp.Value;
            }
            _allocatedStats.Clear();
        }

        _availableStatPoints += totalRecovered;
        return totalRecovered;
    }

    #endregion

    #region Public Methods - Utility

    /// <summary>
    /// Force un niveau specifique (pour debug/save).
    /// </summary>
    /// <param name="level">Niveau a definir.</param>
    /// <param name="grantPoints">Accorder les points de stat/skill.</param>
    public void SetLevel(int level, bool grantPoints = true)
    {
        int previousLevel = _currentLevel;
        _currentLevel = Mathf.Clamp(level, 1, MAX_LEVEL);
        _currentXP = 0;
        _totalXP = GetTotalXPForLevel(_currentLevel);

        if (grantPoints && _currentLevel > previousLevel)
        {
            int levelDiff = _currentLevel - previousLevel;
            _availableStatPoints += levelDiff * STAT_POINTS_PER_LEVEL;
            _availableSkillPoints += levelDiff * SKILL_POINTS_PER_LEVEL;
        }
    }

    /// <summary>
    /// Reinitialise le systeme de niveau.
    /// </summary>
    public void ResetToLevel1()
    {
        _currentLevel = 1;
        _currentXP = 0;
        _totalXP = 0;
        _availableStatPoints = 0;
        _availableSkillPoints = 0;
        _allocatedStats?.Clear();
    }

    #endregion

    #region Private Methods

    private void InitializeXPTable()
    {
        _xpTable = new int[MAX_LEVEL];
        _xpTable[0] = 0; // Level 1 = 0 XP

        for (int i = 1; i < MAX_LEVEL; i++)
        {
            // Formule exponentielle: XP = BASE * (FACTOR ^ level)
            _xpTable[i] = Mathf.RoundToInt(BASE_XP * Mathf.Pow(XP_GROWTH_FACTOR, i));
        }
    }

    private void InitializeAllocatedStats()
    {
        if (_allocatedStats == null)
        {
            _allocatedStats = new Dictionary<StatType, int>();
        }

        // Initialiser toutes les stats a 0
        foreach (StatType stat in Enum.GetValues(typeof(StatType)))
        {
            if (!_allocatedStats.ContainsKey(stat))
            {
                _allocatedStats[stat] = 0;
            }
        }
    }

    private void CheckLevelUp()
    {
        while (!IsMaxLevel && _currentXP >= XPToNextLevel)
        {
            // Soustraire l'XP requise
            _currentXP -= XPToNextLevel;

            // Incrementer le niveau
            _currentLevel++;

            // Accorder les points
            _availableStatPoints += STAT_POINTS_PER_LEVEL;
            _availableSkillPoints += SKILL_POINTS_PER_LEVEL;

            // Effets visuels
            if (_showLevelUpEffects && _levelUpEffectPrefab != null)
            {
                Instantiate(_levelUpEffectPrefab, transform.position, Quaternion.identity);
            }

            OnLevelUp?.Invoke(_currentLevel);
        }

        // Eviter XP negatif au niveau max
        if (IsMaxLevel && _currentXP > 0)
        {
            _currentXP = 0;
        }
    }

    #endregion
}

/// <summary>
/// Sources d'experience possibles.
/// </summary>
public enum XPSource
{
    /// <summary>XP de combat.</summary>
    Combat,

    /// <summary>XP de quete.</summary>
    Quest,

    /// <summary>XP d'exploration.</summary>
    Exploration,

    /// <summary>XP de craft.</summary>
    Crafting,

    /// <summary>XP de decouverte.</summary>
    Discovery,

    /// <summary>XP de capture de creature.</summary>
    Capture,

    /// <summary>XP de defense de base.</summary>
    Defense,

    /// <summary>XP d'evenement.</summary>
    Event
}

// Note: StatType est deja defini dans Core/Stats/StatType.cs
