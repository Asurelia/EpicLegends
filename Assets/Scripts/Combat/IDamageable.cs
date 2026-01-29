/// <summary>
/// Interface pour toute entite qui peut recevoir des degats.
/// A implementer sur les joueurs, ennemis, objets destructibles, etc.
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// Applique des degats a cette entite.
    /// </summary>
    /// <param name="damageInfo">Informations de degats completes</param>
    void TakeDamage(DamageInfo damageInfo);

    /// <summary>
    /// Retourne vrai si l'entite est morte.
    /// </summary>
    bool IsDead { get; }
}
