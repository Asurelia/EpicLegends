using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Definition d'un biome avec ses proprietes et objets a placer.
/// </summary>
[CreateAssetMenu(fileName = "NewBiome", menuName = "EpicLegends/World/Biome Data")]
public class BiomeData : ScriptableObject
{
    #region Identification

    [Header("Identification")]
    [Tooltip("Nom du biome")]
    public string biomeName = "New Biome";

    [Tooltip("Description du biome")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Couleur de preview dans l'editeur")]
    public Color previewColor = Color.green;

    #endregion

    #region Conditions

    [Header("Spawn Conditions")]
    [Tooltip("Hauteur minimum (0-1)")]
    [Range(0f, 1f)]
    public float minHeight = 0f;

    [Tooltip("Hauteur maximum (0-1)")]
    [Range(0f, 1f)]
    public float maxHeight = 1f;

    [Tooltip("Temperature minimum (0=froid, 1=chaud)")]
    [Range(0f, 1f)]
    public float minTemperature = 0f;

    [Tooltip("Temperature maximum")]
    [Range(0f, 1f)]
    public float maxTemperature = 1f;

    [Tooltip("Humidite minimum (0=sec, 1=humide)")]
    [Range(0f, 1f)]
    public float minHumidity = 0f;

    [Tooltip("Humidite maximum")]
    [Range(0f, 1f)]
    public float maxHumidity = 1f;

    #endregion

    #region Terrain

    [Header("Terrain Textures")]
    [Tooltip("Texture principale du sol")]
    public Texture2D groundTexture;

    [Tooltip("Texture de detail (herbe, etc.)")]
    public Texture2D detailTexture;

    [Tooltip("Couleur de la texture du terrain")]
    public Color terrainTint = Color.white;

    #endregion

    #region Vegetation

    [Header("Vegetation")]
    [Tooltip("Objets de vegetation a placer")]
    public BiomeObject[] vegetation;

    [Tooltip("Densite globale de vegetation (objets par 100m²)")]
    [Range(0f, 50f)]
    public float vegetationDensity = 10f;

    #endregion

    #region Props

    [Header("Props & Decoration")]
    [Tooltip("Props decoratifs (rochers, debris, etc.)")]
    public BiomeObject[] props;

    [Tooltip("Densite de props")]
    [Range(0f, 20f)]
    public float propsDensity = 5f;

    #endregion

    #region Enemies

    [Header("Enemies")]
    [Tooltip("Types d'ennemis pouvant spawn dans ce biome")]
    public BiomeEnemy[] enemies;

    [Tooltip("Chance de spawn d'ennemi par zone")]
    [Range(0f, 1f)]
    public float enemySpawnChance = 0.3f;

    #endregion

    #region Ambiance

    [Header("Ambiance")]
    [Tooltip("Couleur du brouillard")]
    public Color fogColor = new Color(0.7f, 0.8f, 0.9f);

    [Tooltip("Densite du brouillard")]
    [Range(0f, 0.1f)]
    public float fogDensity = 0.01f;

    [Tooltip("Couleur ambiante")]
    public Color ambientColor = new Color(0.5f, 0.5f, 0.5f);

    [Tooltip("Musique d'ambiance")]
    public AudioClip ambientMusic;

    [Tooltip("Sons d'ambiance")]
    public AudioClip[] ambientSounds;

    #endregion

    #region Public Methods

    /// <summary>
    /// Verifie si les conditions correspondent a ce biome.
    /// </summary>
    public bool MatchesConditions(float height, float temperature, float humidity)
    {
        return height >= minHeight && height <= maxHeight &&
               temperature >= minTemperature && temperature <= maxTemperature &&
               humidity >= minHumidity && humidity <= maxHumidity;
    }

    /// <summary>
    /// Calcule le score de correspondance (plus haut = meilleur match).
    /// </summary>
    public float GetMatchScore(float height, float temperature, float humidity)
    {
        if (!MatchesConditions(height, temperature, humidity))
            return 0f;

        // Score base sur la proximite du centre des ranges
        float heightCenter = (minHeight + maxHeight) / 2f;
        float tempCenter = (minTemperature + maxTemperature) / 2f;
        float humidCenter = (minHumidity + maxHumidity) / 2f;

        float heightScore = 1f - Mathf.Abs(height - heightCenter);
        float tempScore = 1f - Mathf.Abs(temperature - tempCenter);
        float humidScore = 1f - Mathf.Abs(humidity - humidCenter);

        return (heightScore + tempScore + humidScore) / 3f;
    }

    /// <summary>
    /// Selectionne un objet de vegetation aleatoire selon les poids.
    /// </summary>
    public GameObject GetRandomVegetation(System.Random rng)
    {
        if (vegetation == null || vegetation.Length == 0)
            return null;

        int totalWeight = 0;
        foreach (var veg in vegetation)
            totalWeight += veg.weight;

        int roll = rng.Next(0, totalWeight);
        int cumulative = 0;

        foreach (var veg in vegetation)
        {
            cumulative += veg.weight;
            if (roll < cumulative)
                return veg.prefab;
        }

        return vegetation[0].prefab;
    }

    /// <summary>
    /// Selectionne un prop aleatoire selon les poids.
    /// </summary>
    public GameObject GetRandomProp(System.Random rng)
    {
        if (props == null || props.Length == 0)
            return null;

        int totalWeight = 0;
        foreach (var prop in props)
            totalWeight += prop.weight;

        int roll = rng.Next(0, totalWeight);
        int cumulative = 0;

        foreach (var prop in props)
        {
            cumulative += prop.weight;
            if (roll < cumulative)
                return prop.prefab;
        }

        return props[0].prefab;
    }

    #endregion
}

/// <summary>
/// Objet pouvant etre place dans un biome.
/// </summary>
[System.Serializable]
public class BiomeObject
{
    [Tooltip("Prefab a instancier")]
    public GameObject prefab;

    [Tooltip("Poids de selection (plus haut = plus frequent)")]
    [Range(1, 100)]
    public int weight = 10;

    [Tooltip("Echelle minimum")]
    public float minScale = 0.8f;

    [Tooltip("Echelle maximum")]
    public float maxScale = 1.2f;

    [Tooltip("Rotation aleatoire sur Y")]
    public bool randomYRotation = true;

    [Tooltip("Aligner avec la normale du terrain")]
    public bool alignToSurface = false;

    [Tooltip("Offset vertical")]
    public float yOffset = 0f;
}

/// <summary>
/// Ennemi pouvant spawn dans un biome.
/// </summary>
[System.Serializable]
public class BiomeEnemy
{
    [Tooltip("Data de l'ennemi")]
    public EnemyData enemyData;

    [Tooltip("Prefab de l'ennemi")]
    public GameObject prefab;

    [Tooltip("Poids de spawn")]
    [Range(1, 100)]
    public int weight = 10;

    [Tooltip("Niveau minimum")]
    public int minLevel = 1;

    [Tooltip("Niveau maximum")]
    public int maxLevel = 10;

    [Tooltip("Peut apparaitre en groupe")]
    public bool canSpawnInGroup = true;

    [Tooltip("Taille du groupe min")]
    public int minGroupSize = 1;

    [Tooltip("Taille du groupe max")]
    public int maxGroupSize = 3;
}
