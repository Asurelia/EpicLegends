/// <summary>
/// Types de competences.
/// </summary>
public enum SkillType
{
    /// <summary>Competence active a utiliser manuellement</summary>
    Active = 0,

    /// <summary>Competence passive toujours active</summary>
    Passive = 1,

    /// <summary>Competence ultime puissante avec long cooldown</summary>
    Ultimate = 2
}

/// <summary>
/// Types de cibles pour les competences.
/// </summary>
public enum SkillTargetType
{
    /// <summary>Cible soi-meme</summary>
    Self = 0,

    /// <summary>Cible un seul ennemi</summary>
    SingleEnemy = 1,

    /// <summary>Cible tous les ennemis</summary>
    AllEnemies = 2,

    /// <summary>Cible un seul allie</summary>
    SingleAlly = 3,

    /// <summary>Cible tous les allies</summary>
    AllAllies = 4,

    /// <summary>Zone d'effet</summary>
    Area = 5,

    /// <summary>Ligne de tir</summary>
    Line = 6,

    /// <summary>Cone devant le lanceur</summary>
    Cone = 7
}

/// <summary>
/// Categories de competences pour l'arbre.
/// </summary>
public enum SkillCategory
{
    /// <summary>Competences offensives</summary>
    Offense = 0,

    /// <summary>Competences defensives</summary>
    Defense = 1,

    /// <summary>Competences de support</summary>
    Support = 2,

    /// <summary>Competences de mobilite</summary>
    Mobility = 3,

    /// <summary>Competences de creature</summary>
    Creature = 4
}
