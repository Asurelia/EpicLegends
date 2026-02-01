using UnityEngine;

/// <summary>
/// Marqueur d'entree de grotte.
/// Gere la transition vers les donjons/grottes procedurales.
/// </summary>
public class CaveEntranceMarker : MonoBehaviour
{
    #region Serialized Fields

    [Header("Cave Settings")]
    [SerializeField] private string _caveId;
    [SerializeField] private int _caveSeed;
    [SerializeField] private CaveType _caveType = CaveType.Natural;
    [SerializeField] private int _difficultyLevel = 1;

    [Header("Visual")]
    [SerializeField] private GameObject _entranceVisualPrefab;
    [SerializeField] private ParticleSystem _ambientParticles;
    [SerializeField] private Light _entranceLight;

    [Header("Interaction")]
    [SerializeField] private float _interactionRadius = 3f;
    [SerializeField] private string _interactionPrompt = "Entrer dans la grotte";

    #endregion

    #region Private Fields

    private Vector3 _entrancePosition;
    private bool _isDiscovered;
    private bool _isCleared;

    #endregion

    #region Properties

    public string CaveId => _caveId;
    public int CaveSeed => _caveSeed;
    public CaveType Type => _caveType;
    public int DifficultyLevel => _difficultyLevel;
    public bool IsDiscovered => _isDiscovered;
    public bool IsCleared => _isCleared;
    public Vector3 EntrancePosition => _entrancePosition;

    #endregion

    #region Initialization

    /// <summary>
    /// Initialise le marqueur de grotte.
    /// </summary>
    public void Initialize(Vector3 position, int seed)
    {
        _entrancePosition = position;
        _caveSeed = seed;
        _caveId = $"Cave_{seed}";

        transform.position = position;

        // Determine cave type based on seed
        System.Random rng = new System.Random(seed);
        _caveType = (CaveType)rng.Next(0, System.Enum.GetValues(typeof(CaveType)).Length);
        _difficultyLevel = rng.Next(1, 6);

        SetupVisuals();

        Debug.Log($"[CaveEntranceMarker] Cave {_caveId} initialized at {position}, Type: {_caveType}, Difficulty: {_difficultyLevel}");
    }

    private void SetupVisuals()
    {
        // Create entrance visual if prefab available
        if (_entranceVisualPrefab != null)
        {
            Instantiate(_entranceVisualPrefab, transform.position, Quaternion.identity, transform);
        }

        // Setup ambient particles
        if (_ambientParticles != null)
        {
            var emission = _ambientParticles.emission;
            emission.rateOverTime = _caveType == CaveType.Haunted ? 20f : 5f;
        }

        // Setup entrance light
        if (_entranceLight != null)
        {
            _entranceLight.color = GetCaveLightColor();
            _entranceLight.intensity = 0.5f;
        }
    }

    private Color GetCaveLightColor()
    {
        switch (_caveType)
        {
            case CaveType.Crystal:
                return new Color(0.5f, 0.8f, 1f);
            case CaveType.Lava:
                return new Color(1f, 0.4f, 0.2f);
            case CaveType.Haunted:
                return new Color(0.6f, 0.2f, 0.8f);
            case CaveType.Ice:
                return new Color(0.7f, 0.9f, 1f);
            default:
                return new Color(0.8f, 0.7f, 0.5f);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Marque la grotte comme decouverte.
    /// </summary>
    public void Discover()
    {
        if (_isDiscovered) return;

        _isDiscovered = true;

        // Notify quest/achievement systems
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnSecretFound();
        }

        Debug.Log($"[CaveEntranceMarker] Cave {_caveId} discovered!");
    }

    /// <summary>
    /// Marque la grotte comme nettoyee.
    /// </summary>
    public void MarkCleared()
    {
        _isCleared = true;

        // Visual feedback
        if (_entranceLight != null)
        {
            _entranceLight.color = Color.green;
        }

        Debug.Log($"[CaveEntranceMarker] Cave {_caveId} cleared!");
    }

    /// <summary>
    /// Entre dans la grotte.
    /// </summary>
    public void EnterCave()
    {
        Discover();

        // Generate or load the cave dungeon
        if (ProceduralDungeonManager.Instance != null)
        {
            // Use cave seed for procedural generation
            ProceduralDungeonManager.Instance.GenerateDungeon(null, _caveSeed);
        }

        Debug.Log($"[CaveEntranceMarker] Entering cave {_caveId}");
    }

    /// <summary>
    /// Verifie si le joueur peut interagir avec l'entree.
    /// </summary>
    public bool CanInteract(Vector3 playerPosition)
    {
        return Vector3.Distance(playerPosition, _entrancePosition) <= _interactionRadius;
    }

    /// <summary>
    /// Obtient le prompt d'interaction.
    /// </summary>
    public string GetInteractionPrompt()
    {
        string typeStr = GetCaveTypeName();
        return $"{_interactionPrompt} ({typeStr} - Niveau {_difficultyLevel})";
    }

    private string GetCaveTypeName()
    {
        switch (_caveType)
        {
            case CaveType.Natural: return "Grotte naturelle";
            case CaveType.Crystal: return "Grotte de cristal";
            case CaveType.Lava: return "Grotte volcanique";
            case CaveType.Ice: return "Grotte de glace";
            case CaveType.Haunted: return "Grotte hantee";
            case CaveType.Ancient: return "Ruines anciennes";
            default: return "Grotte";
        }
    }

    #endregion

    #region Unity Lifecycle

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Discover();
            Debug.Log($"[CaveEntranceMarker] {GetInteractionPrompt()}");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("[CaveEntranceMarker] Player left cave entrance area");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactionRadius);

        // Draw cave type indicator
        Gizmos.color = GetCaveLightColor();
        Gizmos.DrawSphere(transform.position + Vector3.up * 2f, 0.5f);
    }

    #endregion
}

/// <summary>
/// Types de grottes.
/// </summary>
public enum CaveType
{
    Natural,
    Crystal,
    Lava,
    Ice,
    Haunted,
    Ancient
}
