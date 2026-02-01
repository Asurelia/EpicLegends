using System;
using UnityEngine;

/// <summary>
/// Systeme de gestion de l'eau.
/// Gere les interactions avec l'eau, les effets et la physique.
/// </summary>
public class WaterSystem : MonoBehaviour
{
    #region Singleton

    public static WaterSystem Instance { get; private set; }

    #endregion

    #region Events

    public event Action<GameObject> OnObjectEnteredWater;
    public event Action<GameObject> OnObjectExitedWater;

    #endregion

    #region Serialized Fields

    [Header("Water Properties")]
    [SerializeField] private float _waterLevel;
    [SerializeField] private float _worldSize;
    [SerializeField] private float _buoyancyForce = 10f;
    [SerializeField] private float _waterDrag = 3f;

    [Header("Visual Effects")]
    [SerializeField] private Color _underwaterFogColor = new Color(0.2f, 0.4f, 0.6f);
    [SerializeField] private float _underwaterFogDensity = 0.1f;
    [SerializeField] private GameObject _splashEffectPrefab;
    [SerializeField] private GameObject _rippleEffectPrefab;

    [Header("Audio")]
    [SerializeField] private AudioClip _splashSound;
    [SerializeField] private AudioClip _underwaterAmbient;

    [Header("Wave Settings")]
    [SerializeField] private bool _enableWaves = true;
    [SerializeField] private float _waveHeight = 0.5f;
    [SerializeField] private float _waveSpeed = 1f;
    [SerializeField] private float _waveScale = 0.1f;

    #endregion

    #region Private Fields

    private Material _waterMaterial;
    private Color _originalFogColor;
    private float _originalFogDensity;
    private bool _isUnderwater;
    private Transform _playerTransform;

    #endregion

    #region Properties

    public float WaterLevel => _waterLevel;
    public bool IsUnderwater => _isUnderwater;

    #endregion

    #region Initialization

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Initialise le systeme d'eau.
    /// </summary>
    public void Initialize(float waterLevel, float worldSize)
    {
        _waterLevel = waterLevel;
        _worldSize = worldSize;

        _originalFogColor = RenderSettings.fogColor;
        _originalFogDensity = RenderSettings.fogDensity;

        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            _waterMaterial = renderer.material;
        }

        Debug.Log($"[WaterSystem] Initialized at height {_waterLevel}");
    }

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        // Find player
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
        }
    }

    private void Update()
    {
        UpdateWaves();
        CheckPlayerUnderwater();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Verifie si une position est dans l'eau.
    /// </summary>
    public bool IsPositionInWater(Vector3 position)
    {
        return position.y < GetWaterHeightAt(position);
    }

    /// <summary>
    /// Obtient la hauteur de l'eau a une position (incluant les vagues).
    /// </summary>
    public float GetWaterHeightAt(Vector3 position)
    {
        if (!_enableWaves)
        {
            return _waterLevel;
        }

        float waveOffset = Mathf.Sin(
            (position.x * _waveScale + Time.time * _waveSpeed) +
            Mathf.Cos(position.z * _waveScale * 0.7f + Time.time * _waveSpeed * 0.8f)
        ) * _waveHeight;

        return _waterLevel + waveOffset;
    }

    /// <summary>
    /// Calcule la force de flottabilite pour un objet.
    /// </summary>
    public Vector3 CalculateBuoyancy(Vector3 position, float objectRadius = 0.5f)
    {
        float waterHeight = GetWaterHeightAt(position);
        float submersion = Mathf.Clamp01((waterHeight - position.y) / (objectRadius * 2f));

        if (submersion <= 0)
        {
            return Vector3.zero;
        }

        return Vector3.up * _buoyancyForce * submersion;
    }

    /// <summary>
    /// Cree un effet de splash a une position.
    /// </summary>
    public void CreateSplash(Vector3 position, float scale = 1f)
    {
        if (_splashEffectPrefab != null)
        {
            Vector3 splashPos = new Vector3(position.x, _waterLevel, position.z);
            var splash = Instantiate(_splashEffectPrefab, splashPos, Quaternion.identity);
            splash.transform.localScale *= scale;
            Destroy(splash, 3f);
        }

        if (_splashSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(_splashSound.name, position);
        }
    }

    /// <summary>
    /// Cree un effet d'ondulation a une position.
    /// </summary>
    public void CreateRipple(Vector3 position, float scale = 1f)
    {
        if (_rippleEffectPrefab != null)
        {
            Vector3 ripplePos = new Vector3(position.x, _waterLevel + 0.01f, position.z);
            var ripple = Instantiate(_rippleEffectPrefab, ripplePos, Quaternion.Euler(90f, 0f, 0f));
            ripple.transform.localScale *= scale;
            Destroy(ripple, 5f);
        }
    }

    /// <summary>
    /// Applique la resistance de l'eau a un Rigidbody.
    /// </summary>
    public void ApplyWaterDrag(Rigidbody rb, float submersionRatio)
    {
        if (rb == null || submersionRatio <= 0) return;

        rb.linearDamping = Mathf.Lerp(rb.linearDamping, _waterDrag, submersionRatio);
        rb.angularDamping = Mathf.Lerp(rb.angularDamping, _waterDrag * 0.5f, submersionRatio);
    }

    #endregion

    #region Private Methods

    private void UpdateWaves()
    {
        if (!_enableWaves || _waterMaterial == null) return;

        // Update shader parameters if using custom water shader
        _waterMaterial.SetFloat("_WaveTime", Time.time * _waveSpeed);
        _waterMaterial.SetFloat("_WaveHeight", _waveHeight);
        _waterMaterial.SetFloat("_WaveScale", _waveScale);
    }

    private void CheckPlayerUnderwater()
    {
        if (_playerTransform == null) return;

        Vector3 headPosition = _playerTransform.position + Vector3.up * 1.5f;
        bool wasUnderwater = _isUnderwater;
        _isUnderwater = IsPositionInWater(headPosition);

        if (_isUnderwater != wasUnderwater)
        {
            if (_isUnderwater)
            {
                EnterUnderwater();
            }
            else
            {
                ExitUnderwater();
            }
        }
    }

    private void EnterUnderwater()
    {
        RenderSettings.fogColor = _underwaterFogColor;
        RenderSettings.fogDensity = _underwaterFogDensity;
        RenderSettings.fog = true;

        if (_underwaterAmbient != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(_underwaterAmbient.name);
        }

        Debug.Log("[WaterSystem] Player entered underwater");
    }

    private void ExitUnderwater()
    {
        RenderSettings.fogColor = _originalFogColor;
        RenderSettings.fogDensity = _originalFogDensity;

        CreateSplash(_playerTransform.position);

        Debug.Log("[WaterSystem] Player exited underwater");
    }

    #endregion

    #region Collision Detection

    private void OnTriggerEnter(Collider other)
    {
        if (other.attachedRigidbody != null)
        {
            CreateSplash(other.transform.position);
            OnObjectEnteredWater?.Invoke(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.attachedRigidbody != null)
        {
            OnObjectExitedWater?.Invoke(other.gameObject);
        }
    }

    #endregion
}
