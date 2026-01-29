using System;

/// <summary>
/// Instance d'un objet dans l'inventaire.
/// Contient les données spécifiques à cette instance (quantité, durabilité, etc.).
/// </summary>
[Serializable]
public class ItemInstance
{
    /// <summary>
    /// Données de base de l'objet.
    /// </summary>
    public ItemData Data { get; private set; }

    /// <summary>
    /// Quantité dans cette pile.
    /// </summary>
    public int Quantity { get; private set; }

    /// <summary>
    /// ID unique de cette instance (pour sauvegarde/synchronisation).
    /// </summary>
    public string InstanceId { get; private set; }

    /// <summary>
    /// Crée une nouvelle instance d'objet.
    /// </summary>
    public ItemInstance(ItemData data, int quantity = 1)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Quantity = Math.Clamp(quantity, 1, data.maxStackSize);
        InstanceId = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Espace restant dans la pile.
    /// </summary>
    public int RemainingSpace => Data.maxStackSize - Quantity;

    /// <summary>
    /// La pile est-elle pleine?
    /// </summary>
    public bool IsFull => Quantity >= Data.maxStackSize;

    /// <summary>
    /// La pile est-elle vide?
    /// </summary>
    public bool IsEmpty => Quantity <= 0;

    /// <summary>
    /// Ajoute des items à la pile.
    /// </summary>
    /// <param name="amount">Quantité à ajouter</param>
    /// <returns>Quantité qui n'a pas pu être ajoutée (overflow)</returns>
    public int Add(int amount)
    {
        if (amount <= 0) return 0;

        int canAdd = Math.Min(amount, RemainingSpace);
        Quantity += canAdd;
        return amount - canAdd;
    }

    /// <summary>
    /// Retire des items de la pile.
    /// </summary>
    /// <param name="amount">Quantité à retirer</param>
    /// <returns>Quantité effectivement retirée</returns>
    public int Remove(int amount)
    {
        if (amount <= 0) return 0;

        int canRemove = Math.Min(amount, Quantity);
        Quantity -= canRemove;
        return canRemove;
    }

    /// <summary>
    /// Divise la pile en deux.
    /// </summary>
    /// <param name="amount">Quantité à séparer</param>
    /// <returns>Nouvelle instance avec la quantité séparée, ou null si impossible</returns>
    public ItemInstance Split(int amount)
    {
        if (amount <= 0 || amount >= Quantity) return null;

        Quantity -= amount;
        return new ItemInstance(Data, amount);
    }

    /// <summary>
    /// Tente de fusionner avec une autre pile du même type.
    /// </summary>
    /// <param name="other">Autre pile à fusionner</param>
    /// <returns>True si la fusion a consommé toute l'autre pile</returns>
    public bool TryMerge(ItemInstance other)
    {
        if (other == null || other.Data != Data || !Data.isStackable) return false;

        int overflow = Add(other.Quantity);
        other.Quantity = overflow;
        return overflow == 0;
    }

    /// <summary>
    /// Crée une copie de cette instance.
    /// </summary>
    public ItemInstance Clone()
    {
        return new ItemInstance(Data, Quantity);
    }

    public override string ToString()
    {
        return $"{Data.displayName} x{Quantity}";
    }
}
