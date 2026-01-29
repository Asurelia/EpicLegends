using System;
using UnityEngine;

/// <summary>
/// Point de voyage rapide dans le monde.
/// Permet au joueur de se teleporter entre les points decouverts.
/// </summary>
public class FastTravelPoint : MonoBehaviour
{
    #region Fields

    [Header("Identification")]
    [SerializeField] private string _pointId;
    [SerializeField] private string _pointName;
    [SerializeField] private RegionData _region;

    [Header("Etat")]
    [SerializeField] private bool _isUnlocked = false;
    [SerializeField] private bool _startsUnlocked = false;

    [Header("Spawn")]
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private float _spawnRadius = 1f;

    [Header("Cout")]
    [SerializeField] private int _travelCost = 0;
    [SerializeField] private bool _freeTravelWhenNearby = true;
    [SerializeField] private float _freeRadius = 50f;

    [Header("Visuel")]
    [SerializeField] private GameObject _unlockedVisual;
    [SerializeField] private GameObject _lockedVisual;
    [SerializeField] private ParticleSystem _activationEffect;

    [Header("Interaction")]
    [SerializeField] private float _interactionRadius = 3f;

    #endregion

    #region Events

    /// <summary>Declenche lors du deblocage.</summary>
    public event Action<FastTravelPoint> OnUnlocked;

    /// <summary>Declenche lors de l'utilisation.</summary>
    public event Action<FastTravelPoint> OnUsed;

    #endregion

    #region Properties

    /// <summary>ID du point.</summary>
    public string PointId => _pointId;

    /// <summary>Nom du point.</summary>
    public string PointName => _pointName;

    /// <summary>Region associee.</summary>
    public RegionData Region => _region;

    /// <summary>Est debloque?</summary>
    public bool IsUnlocked => _isUnlocked;

    /// <summary>Position de spawn.</summary>
    public Vector3 SpawnPosition => _spawnPoint != null
        ? _spawnPoint.position
        : transform.position;

    /// <summary>Rotation de spawn.</summary>
    public Quaternion SpawnRotation => _spawnPoint != null
        ? _spawnPoint.rotation
        : transform.rotation;

    /// <summary>Cout de voyage.</summary>
    public int TravelCost => _travelCost;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (_startsUnlocked && !_isUnlocked)
        {
            Unlock();
        }

        UpdateVisuals();
    }

    private void OnDrawGizmosSelected()
    {
        // Afficher le rayon d'interaction
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _interactionRadius);

        // Afficher le rayon gratuit
        if (_freeTravelWhenNearby)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, _freeRadius);
        }

        // Afficher le point de spawn
        if (_spawnPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_spawnPoint.position, _spawnRadius);
            Gizmos.DrawLine(transform.position, _spawnPoint.position);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Debloque le point de voyage.
    /// </summary>
    public void Unlock()
    {
        if (_isUnlocked) return;

        _isUnlocked = true;

        // Effet visuel
        if (_activationEffect != null)
        {
            _activationEffect.Play();
        }

        UpdateVisuals();

        OnUnlocked?.Invoke(this);
    }

    /// <summary>
    /// Verifie si le voyage est gratuit depuis une position.
    /// </summary>
    /// <param name="fromPosition">Position de depart.</param>
    /// <returns>True si gratuit.</returns>
    public bool IsFreeFromPosition(Vector3 fromPosition)
    {
        if (!_freeTravelWhenNearby) return false;

        float distance = Vector3.Distance(transform.position, fromPosition);
        return distance <= _freeRadius;
    }

    /// <summary>
    /// Obtient le cout de voyage depuis une position.
    /// </summary>
    /// <param name="fromPosition">Position de depart.</param>
    /// <returns>Cout.</returns>
    public int GetTravelCost(Vector3 fromPosition)
    {
        if (IsFreeFromPosition(fromPosition)) return 0;
        return _travelCost;
    }

    /// <summary>
    /// Teleporte une entite a ce point.
    /// </summary>
    /// <param name="target">Transform a teleporter.</param>
    /// <returns>True si teleporte.</returns>
    public bool TeleportTo(Transform target)
    {
        if (!_isUnlocked || target == null) return false;

        // Position avec variation aleatoire
        Vector3 offset = UnityEngine.Random.insideUnitSphere * _spawnRadius;
        offset.y = 0;

        target.position = SpawnPosition + offset;
        target.rotation = SpawnRotation;

        OnUsed?.Invoke(this);

        return true;
    }

    /// <summary>
    /// Verifie si une entite est dans le rayon d'interaction.
    /// </summary>
    /// <param name="position">Position a verifier.</param>
    /// <returns>True si a portee.</returns>
    public bool IsInInteractionRange(Vector3 position)
    {
        return Vector3.Distance(transform.position, position) <= _interactionRadius;
    }

    #endregion

    #region Private Methods

    private void UpdateVisuals()
    {
        if (_unlockedVisual != null)
        {
            _unlockedVisual.SetActive(_isUnlocked);
        }

        if (_lockedVisual != null)
        {
            _lockedVisual.SetActive(!_isUnlocked);
        }
    }

    #endregion
}
