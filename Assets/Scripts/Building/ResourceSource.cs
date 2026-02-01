using System;
using UnityEngine;

/// <summary>
/// Source de ressources dans le monde (arbre, rocher, minerai).
/// </summary>
public class ResourceSource : MonoBehaviour
{
    #region Fields

    [Header("Configuration")]
    [SerializeField] private ResourceData _resourceData;
    [SerializeField] private int _maxResources = 10;
    [SerializeField] private bool _respawns = true;
    [SerializeField] private float _respawnTime = 60f;

    [Header("Visuel")]
    [SerializeField] private GameObject _fullVisual;
    [SerializeField] private GameObject _depletedVisual;
    [SerializeField] private float _shakeAmount = 0.1f;
    [SerializeField] private float _shakeDuration = 0.2f;

    // Etat
    private int _currentResources;
    private bool _isDepleted = false;
    private float _respawnTimer = 0f;
    private Vector3 _originalPosition;

    // Shake
    private float _shakeTimer = 0f;

    #endregion

    #region Events

    public event Action<ResourceType, int> OnResourceGathered;
    public event Action OnDepleted;
    public event Action OnRespawned;

    #endregion

    #region Properties

    /// <summary>Donnees de la ressource.</summary>
    public ResourceData ResourceData => _resourceData;

    /// <summary>Type de ressource.</summary>
    public ResourceType ResourceType => _resourceData != null ? _resourceData.resourceType : ResourceType.Wood;

    /// <summary>Ressources restantes.</summary>
    public int CurrentResources => _currentResources;

    /// <summary>Maximum de ressources.</summary>
    public int MaxResources => _maxResources;

    /// <summary>Pourcentage restant.</summary>
    public float ResourcePercent => _maxResources > 0 ? (float)_currentResources / _maxResources : 0f;

    /// <summary>Est epuise?</summary>
    public bool IsDepleted => _isDepleted;

    /// <summary>Peut respawn?</summary>
    public bool CanRespawn => _respawns;

    /// <summary>Temps avant respawn.</summary>
    public float RespawnTimeRemaining => _isDepleted ? Mathf.Max(0, _respawnTime - _respawnTimer) : 0f;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        _currentResources = _maxResources;
        _originalPosition = transform.position;
        UpdateVisual();

        // S'enregistrer auprès du manager
        if (ResourceNodeManager.Instance != null)
        {
            ResourceNodeManager.Instance.RegisterNode(this);
        }
    }

    private void OnDestroy()
    {
        // Se retirer du manager
        if (ResourceNodeManager.Instance != null)
        {
            ResourceNodeManager.Instance.UnregisterNode(this);
        }
    }

    private void Update()
    {
        // Timer de respawn
        if (_isDepleted && _respawns)
        {
            _respawnTimer += Time.deltaTime;
            if (_respawnTimer >= _respawnTime)
            {
                Respawn();
            }
        }

        // Animation de shake
        if (_shakeTimer > 0)
        {
            _shakeTimer -= Time.deltaTime;
            float intensity = _shakeTimer / _shakeDuration;
            transform.position = _originalPosition + UnityEngine.Random.insideUnitSphere * _shakeAmount * intensity;

            if (_shakeTimer <= 0)
            {
                transform.position = _originalPosition;
            }
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Tente de collecter des ressources.
    /// </summary>
    public bool TryGather(ToolType tool, out ResourceType type, out int amount)
    {
        type = ResourceType;
        amount = 0;

        if (_isDepleted) return false;
        if (_resourceData == null) return false;
        if (!_resourceData.CanGatherWith(tool)) return false;

        // Calculer la quantite
        amount = Mathf.Min(_resourceData.gatherAmount, _currentResources);
        if (amount <= 0) return false;

        // Retirer les ressources
        _currentResources -= amount;

        // Effets
        PlayGatherEffects();
        TriggerShake();

        OnResourceGathered?.Invoke(type, amount);

        // Verifier si epuise
        if (_currentResources <= 0)
        {
            Deplete();
        }

        return true;
    }

    /// <summary>
    /// Epuise la source.
    /// </summary>
    public void Deplete()
    {
        _isDepleted = true;
        _currentResources = 0;
        _respawnTimer = 0f;

        UpdateVisual();
        OnDepleted?.Invoke();
    }

    /// <summary>
    /// Fait respawn la source.
    /// </summary>
    public void Respawn()
    {
        _isDepleted = false;
        _currentResources = _maxResources;
        _respawnTimer = 0f;

        UpdateVisual();
        OnRespawned?.Invoke();
    }

    /// <summary>
    /// Force un respawn immediat.
    /// </summary>
    public void ForceRespawn()
    {
        Respawn();
    }

    /// <summary>
    /// Definit la quantite de ressources.
    /// </summary>
    public void SetResourceAmount(int amount)
    {
        _currentResources = Mathf.Clamp(amount, 0, _maxResources);

        if (_currentResources <= 0)
        {
            Deplete();
        }
        else
        {
            _isDepleted = false;
            UpdateVisual();
        }
    }

    #endregion

    #region Private Methods

    private void UpdateVisual()
    {
        if (_fullVisual != null)
            _fullVisual.SetActive(!_isDepleted);

        if (_depletedVisual != null)
            _depletedVisual.SetActive(_isDepleted);
    }

    private void PlayGatherEffects()
    {
        if (_resourceData == null) return;

        // Son
        if (_resourceData.gatherSound != null)
        {
            AudioSource.PlayClipAtPoint(_resourceData.gatherSound, transform.position);
        }

        // Particules
        if (_resourceData.gatherVFX != null)
        {
            Instantiate(_resourceData.gatherVFX, transform.position, Quaternion.identity);
        }
    }

    private void TriggerShake()
    {
        _shakeTimer = _shakeDuration;
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Configure la source.
    /// </summary>
    public void Configure(ResourceData data, int maxResources, bool respawns, float respawnTime)
    {
        _resourceData = data;
        _maxResources = maxResources;
        _currentResources = maxResources;
        _respawns = respawns;
        _respawnTime = respawnTime;
    }

    #endregion
}
