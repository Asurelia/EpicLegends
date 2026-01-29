using UnityEngine;

/// <summary>
/// Validateur de placement pour les batiments.
/// Verifie les conditions de placement.
/// </summary>
public static class PlacementValidator
{
    #region Public Methods

    /// <summary>
    /// Verifie si un batiment peut etre place a une position.
    /// </summary>
    public static bool CanPlace(BuildingData data, Vector2Int gridPosition, int rotation, BuildingGrid grid)
    {
        if (data == null || grid == null) return false;

        // Calculer la taille effective avec rotation
        Vector2Int effectiveSize = GetRotatedSize(data.gridSize, rotation);

        // Verifier les limites
        if (!grid.IsInBounds(gridPosition, effectiveSize))
            return false;

        // Verifier l'occupation
        if (grid.IsAreaOccupied(gridPosition, effectiveSize))
            return false;

        // Verifier la fondation si requise
        if (data.requiresFoundation && !HasFoundation(gridPosition, effectiveSize, grid))
            return false;

        // Verifier le terrain plat (optionnel)
        if (data.canPlaceOnGround && !grid.IsTerrainFlat(gridPosition, effectiveSize))
        {
            // Tolerance plus elevee pour les petits batiments
            if (!grid.IsTerrainFlat(gridPosition, effectiveSize, 1f))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Verifie si une fondation existe sous le batiment.
    /// </summary>
    public static bool HasFoundation(Vector2Int gridPosition, Vector2Int size, BuildingGrid grid)
    {
        // Verifier qu'il y a un batiment de type fondation en dessous
        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                Vector2Int checkPos = new Vector2Int(gridPosition.x + x, gridPosition.y + z);
                GameObject below = grid.GetBuildingAt(checkPos, -1); // Niveau en dessous

                if (below == null)
                {
                    // Verifier au niveau 0 si c'est une fondation
                    below = grid.GetBuildingAt(checkPos, 0);
                    if (below == null) return false;

                    var building = below.GetComponent<Building>();
                    if (building == null) return false;
                    if (building.Data.subCategory != BuildingSubCategory.Foundation &&
                        building.Data.subCategory != BuildingSubCategory.Floor)
                        return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Verifie si un batiment peut se connecter a un autre.
    /// </summary>
    public static bool CanConnect(Building source, Building target, SnapPoint sourceSnap, SnapPoint targetSnap)
    {
        // Verifier la compatibilite des types de snap
        if (!AreSnapTypesCompatible(sourceSnap.type, targetSnap.type))
            return false;

        // Verifier que les directions sont opposees
        float dot = Vector3.Dot(sourceSnap.direction, targetSnap.direction);
        if (dot > -0.9f) // Doit etre quasi-oppose
            return false;

        return true;
    }

    /// <summary>
    /// Trouve le point de snap le plus proche.
    /// </summary>
    public static SnapPoint? FindNearestSnapPoint(BuildingData data, Vector3 worldPosition, float maxDistance)
    {
        if (data.snapPoints == null || data.snapPoints.Length == 0)
            return null;

        SnapPoint? nearest = null;
        float nearestDist = maxDistance;

        foreach (var snap in data.snapPoints)
        {
            float dist = Vector3.Distance(snap.localPosition, worldPosition);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = snap;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Calcule la taille apres rotation.
    /// </summary>
    public static Vector2Int GetRotatedSize(Vector2Int size, int rotation)
    {
        // Rotation de 90 ou 270 degres inverse X et Z
        if (rotation == 90 || rotation == 270)
        {
            return new Vector2Int(size.y, size.x);
        }
        return size;
    }

    /// <summary>
    /// Obtient les raisons d'invalidite du placement.
    /// </summary>
    public static string GetInvalidReason(BuildingData data, Vector2Int gridPosition, int rotation, BuildingGrid grid)
    {
        if (data == null) return "Aucun batiment selectionne";
        if (grid == null) return "Grille non trouvee";

        Vector2Int effectiveSize = GetRotatedSize(data.gridSize, rotation);

        if (!grid.IsInBounds(gridPosition, effectiveSize))
            return "Hors limites de la zone constructible";

        if (grid.IsAreaOccupied(gridPosition, effectiveSize))
            return "Espace deja occupe";

        if (data.requiresFoundation && !HasFoundation(gridPosition, effectiveSize, grid))
            return "Necessite une fondation";

        if (data.canPlaceOnGround && !grid.IsTerrainFlat(gridPosition, effectiveSize, 1f))
            return "Terrain trop irregulier";

        return string.Empty;
    }

    #endregion

    #region Private Methods

    private static bool AreSnapTypesCompatible(SnapPointType typeA, SnapPointType typeB)
    {
        // Mur du bas avec mur du haut
        if (typeA == SnapPointType.WallBottom && typeB == SnapPointType.WallTop)
            return true;
        if (typeA == SnapPointType.WallTop && typeB == SnapPointType.WallBottom)
            return true;

        // Mur cote avec mur cote
        if (typeA == SnapPointType.WallSide && typeB == SnapPointType.WallSide)
            return true;

        // Sol avec sol
        if (typeA == SnapPointType.FloorEdge && typeB == SnapPointType.FloorEdge)
            return true;

        // Porte avec cadre
        if (typeA == SnapPointType.DoorFrame && typeB == SnapPointType.WallSide)
            return true;
        if (typeA == SnapPointType.WallSide && typeB == SnapPointType.DoorFrame)
            return true;

        return false;
    }

    #endregion
}
