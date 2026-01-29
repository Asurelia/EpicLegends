using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Systeme de gestion des classes du personnage.
/// Gere le changement de classe, la progression et les competences cross-class.
/// </summary>
public class ClassSystem : MonoBehaviour
{
    #region Constants

    /// <summary>XP de base pour le niveau de classe 2.</summary>
    private const int BASE_CLASS_XP = 50;

    /// <summary>Facteur de croissance XP de classe.</summary>
    private const float CLASS_XP_GROWTH = 1.12f;

    #endregion

    #region Fields

    [Header("Classe actuelle")]
    [SerializeField] private ClassData _currentClass;
    [SerializeField] private int _currentClassLevel = 1;
    [SerializeField] private int _currentClassXP = 0;

    [Header("Historique")]
    [SerializeField] private List<ClassMasteryRecord> _masteredClasses;

    [Header("Cross-Class")]
    [SerializeField] private int _maxCrossClassSkills = 3;

    // Competences cross-class equipees
    private List<SkillData> _equippedCrossClassSkills;

    // Cache XP
    private int[] _classXPTable;

    #endregion

    #region Events

    /// <summary>Declenche lors d'un changement de classe.</summary>
    public event Action<ClassData, ClassData> OnClassChanged;

    /// <summary>Declenche lors d'un level up de classe.</summary>
    public event Action<int> OnClassLevelUp;

    /// <summary>Declenche lors de la maitrise d'une classe.</summary>
    public event Action<ClassData> OnClassMastered;

    #endregion

    #region Properties

    /// <summary>Classe actuelle.</summary>
    public ClassData CurrentClass => _currentClass;

    /// <summary>Niveau de classe actuel.</summary>
    public int CurrentClassLevel => _currentClassLevel;

    /// <summary>XP de classe actuelle.</summary>
    public int CurrentClassXP => _currentClassXP;

    /// <summary>XP pour le prochain niveau de classe.</summary>
    public int XPToNextClassLevel => GetClassXPForLevel(_currentClassLevel + 1);

    /// <summary>Progression vers le prochain niveau de classe.</summary>
    public float ClassLevelProgress => XPToNextClassLevel > 0
        ? (float)_currentClassXP / XPToNextClassLevel
        : 0f;

    /// <summary>Classe maitrisee?</summary>
    public bool IsCurrentClassMastered => _currentClass != null
        && _currentClassLevel >= _currentClass.maxClassLevel;

    /// <summary>Nombre de classes maitrisees.</summary>
    public int MasteredClassCount => _masteredClasses?.Count ?? 0;

    /// <summary>Competences cross-class equipees.</summary>
    public IReadOnlyList<SkillData> CrossClassSkills => _equippedCrossClassSkills;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _masteredClasses = _masteredClasses ?? new List<ClassMasteryRecord>();
        _equippedCrossClassSkills = new List<SkillData>();
        InitializeClassXPTable();
    }

    #endregion

    #region Public Methods - Classe

    /// <summary>
    /// Change la classe du personnage.
    /// </summary>
    /// <param name="newClass">Nouvelle classe.</param>
    /// <returns>True si le changement a reussi.</returns>
    public bool SetClass(ClassData newClass)
    {
        if (newClass == null) return false;

        var previousClass = _currentClass;

        // Sauvegarder la progression de la classe actuelle
        if (_currentClass != null)
        {
            SaveClassProgress();
        }

        // Appliquer la nouvelle classe
        _currentClass = newClass;

        // Restaurer la progression si deja jouee
        var existingRecord = FindClassRecord(newClass);
        if (existingRecord != null)
        {
            _currentClassLevel = existingRecord.level;
            _currentClassXP = existingRecord.xp;
        }
        else
        {
            _currentClassLevel = 1;
            _currentClassXP = 0;
        }

        OnClassChanged?.Invoke(previousClass, newClass);

        return true;
    }

    /// <summary>
    /// Verifie si une classe peut etre deverrouillee.
    /// </summary>
    /// <param name="classData">Classe a verifier.</param>
    /// <param name="playerLevel">Niveau du joueur.</param>
    /// <returns>True si la classe est accessible.</returns>
    public bool CanUnlockClass(ClassData classData, int playerLevel)
    {
        if (classData == null) return false;

        // Verifier le niveau requis
        if (playerLevel < classData.requiredLevel) return false;

        // Verifier les classes pre-requises
        if (classData.requiredClasses != null)
        {
            foreach (var required in classData.requiredClasses)
            {
                if (!HasMasteredClass(required, classData.requiredMasteryLevel))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Verifie si une classe a ete maitrisee.
    /// </summary>
    /// <param name="classData">Classe a verifier.</param>
    /// <param name="minLevel">Niveau minimum atteint.</param>
    /// <returns>True si maitrisee au niveau requis.</returns>
    public bool HasMasteredClass(ClassData classData, int minLevel = 1)
    {
        if (classData == null) return false;

        var record = FindClassRecord(classData);
        return record != null && record.level >= minLevel;
    }

    #endregion

    #region Public Methods - XP

    /// <summary>
    /// Ajoute de l'XP de classe.
    /// </summary>
    /// <param name="amount">Quantite d'XP.</param>
    public void AddClassXP(int amount)
    {
        if (amount <= 0 || _currentClass == null) return;
        if (IsCurrentClassMastered) return;

        _currentClassXP += amount;

        CheckClassLevelUp();
    }

    /// <summary>
    /// Obtient l'XP requise pour un niveau de classe.
    /// </summary>
    /// <param name="level">Niveau cible.</param>
    /// <returns>XP requise.</returns>
    public int GetClassXPForLevel(int level)
    {
        if (level <= 1) return 0;
        if (_classXPTable == null) InitializeClassXPTable();

        int maxLevel = _currentClass?.maxClassLevel ?? 50;
        if (level > maxLevel) return int.MaxValue;

        return _classXPTable[Mathf.Min(level - 1, _classXPTable.Length - 1)];
    }

    #endregion

    #region Public Methods - Cross-Class Skills

    /// <summary>
    /// Equipe une competence cross-class.
    /// </summary>
    /// <param name="skill">Competence a equiper.</param>
    /// <returns>True si equipee.</returns>
    public bool EquipCrossClassSkill(SkillData skill)
    {
        if (skill == null) return false;
        if (_equippedCrossClassSkills == null)
            _equippedCrossClassSkills = new List<SkillData>();

        if (_equippedCrossClassSkills.Count >= _maxCrossClassSkills) return false;
        if (_equippedCrossClassSkills.Contains(skill)) return false;

        // Verifier si la competence est disponible en cross-class
        if (!IsCrossClassSkillAvailable(skill)) return false;

        _equippedCrossClassSkills.Add(skill);
        return true;
    }

    /// <summary>
    /// Retire une competence cross-class.
    /// </summary>
    /// <param name="skill">Competence a retirer.</param>
    /// <returns>True si retiree.</returns>
    public bool UnequipCrossClassSkill(SkillData skill)
    {
        if (skill == null || _equippedCrossClassSkills == null) return false;

        return _equippedCrossClassSkills.Remove(skill);
    }

    /// <summary>
    /// Verifie si une competence est disponible en cross-class.
    /// </summary>
    /// <param name="skill">Competence a verifier.</param>
    /// <returns>True si disponible.</returns>
    public bool IsCrossClassSkillAvailable(SkillData skill)
    {
        if (skill == null || _masteredClasses == null) return false;

        // Parcourir les classes maitrisees
        foreach (var record in _masteredClasses)
        {
            if (record.classData?.masteryBonuses == null) continue;

            foreach (var bonus in record.classData.masteryBonuses)
            {
                if (bonus.bonusType == MasteryBonusType.CrossClassSkill)
                {
                    // La competence est disponible via maitrise
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Obtient toutes les competences cross-class disponibles.
    /// </summary>
    /// <returns>Liste des competences.</returns>
    public List<SkillData> GetAvailableCrossClassSkills()
    {
        var skills = new List<SkillData>();

        if (_masteredClasses == null) return skills;

        foreach (var record in _masteredClasses)
        {
            if (record.classData?.classSkills == null) continue;

            foreach (var classSkill in record.classData.classSkills)
            {
                if (classSkill.skill != null && classSkill.unlockLevel <= record.level)
                {
                    if (!skills.Contains(classSkill.skill))
                    {
                        skills.Add(classSkill.skill);
                    }
                }
            }
        }

        return skills;
    }

    #endregion

    #region Public Methods - Stats

    /// <summary>
    /// Obtient le bonus de stat total de la classe actuelle.
    /// </summary>
    /// <param name="stat">Type de stat.</param>
    /// <returns>Bonus total.</returns>
    public float GetClassStatBonus(StatType stat)
    {
        if (_currentClass == null) return 0f;

        return _currentClass.GetStatBonusForLevel(stat, _currentClassLevel);
    }

    /// <summary>
    /// Obtient les bonus de toutes les classes maitrisees.
    /// </summary>
    /// <param name="stat">Type de stat.</param>
    /// <returns>Bonus total de maitrise.</returns>
    public float GetMasteryStatBonus(StatType stat)
    {
        float total = 0f;

        if (_masteredClasses == null) return total;

        foreach (var record in _masteredClasses)
        {
            if (record.classData?.masteryBonuses == null) continue;

            foreach (var bonus in record.classData.masteryBonuses)
            {
                if (bonus.bonusType == MasteryBonusType.StatBonus)
                {
                    // Simplification: ajouter la valeur
                    total += bonus.value;
                }
            }
        }

        return total;
    }

    #endregion

    #region Private Methods

    private void InitializeClassXPTable()
    {
        int maxLevel = 50;
        _classXPTable = new int[maxLevel];
        _classXPTable[0] = 0;

        for (int i = 1; i < maxLevel; i++)
        {
            _classXPTable[i] = Mathf.RoundToInt(BASE_CLASS_XP * Mathf.Pow(CLASS_XP_GROWTH, i));
        }
    }

    private void CheckClassLevelUp()
    {
        if (_currentClass == null) return;

        while (_currentClassLevel < _currentClass.maxClassLevel &&
               _currentClassXP >= XPToNextClassLevel)
        {
            _currentClassXP -= XPToNextClassLevel;
            _currentClassLevel++;

            OnClassLevelUp?.Invoke(_currentClassLevel);

            // Verifier maitrise
            if (_currentClassLevel >= _currentClass.maxClassLevel)
            {
                MarkClassAsMastered();
            }
        }
    }

    private void SaveClassProgress()
    {
        if (_currentClass == null) return;

        var record = FindClassRecord(_currentClass);
        if (record != null)
        {
            record.level = Mathf.Max(record.level, _currentClassLevel);
            record.xp = _currentClassXP;
        }
        else
        {
            _masteredClasses.Add(new ClassMasteryRecord
            {
                classData = _currentClass,
                level = _currentClassLevel,
                xp = _currentClassXP,
                isMastered = _currentClassLevel >= _currentClass.maxClassLevel
            });
        }
    }

    private ClassMasteryRecord FindClassRecord(ClassData classData)
    {
        if (_masteredClasses == null || classData == null) return null;

        foreach (var record in _masteredClasses)
        {
            if (record.classData == classData)
            {
                return record;
            }
        }

        return null;
    }

    private void MarkClassAsMastered()
    {
        if (_currentClass == null) return;

        var record = FindClassRecord(_currentClass);
        if (record != null)
        {
            record.isMastered = true;
        }
        else
        {
            _masteredClasses.Add(new ClassMasteryRecord
            {
                classData = _currentClass,
                level = _currentClassLevel,
                xp = _currentClassXP,
                isMastered = true
            });
        }

        OnClassMastered?.Invoke(_currentClass);
    }

    #endregion
}

/// <summary>
/// Enregistrement de progression d'une classe.
/// </summary>
[System.Serializable]
public class ClassMasteryRecord
{
    public ClassData classData;
    public int level;
    public int xp;
    public bool isMastered;
}
