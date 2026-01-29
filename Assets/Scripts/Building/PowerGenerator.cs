using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generateur d'energie pour alimenter les batiments.
/// </summary>
public class PowerGenerator : MonoBehaviour
{
    #region Fields

    [Header("Configuration")]
    [SerializeField] private float _powerOutput = 100f;
    [SerializeField] private float _range = 15f;
    [SerializeField] private GeneratorType _generatorType = GeneratorType.Manual;

    [Header("Consommation de Fuel")]
    [SerializeField] private bool _requiresFuel = false;
    [SerializeField] private ResourceType _fuelType = ResourceType.Coal;
    [SerializeField] private float _fuelConsumptionRate = 1f;
    [SerializeField] private float _currentFuel = 0f;
    [SerializeField] private float _maxFuel = 100f;

    [Header("References")]
    [SerializeField] private StorageBuilding _fuelStorage;

    // Etat
    private bool _isActive = true;
    private bool _isPowered = true;
    private List<IPowerConsumer> _connectedConsumers = new List<IPowerConsumer>();
    private float _currentLoad = 0f;

    #endregion

    #region Events

    public event Action<bool> OnPowerStateChanged;
    public event Action<float> OnLoadChanged;
    public event Action<float> OnFuelChanged;

    #endregion

    #region Properties

    /// <summary>Production d'energie.</summary>
    public float PowerOutput => _powerOutput;

    /// <summary>Portee du reseau.</summary>
    public float Range => _range;

    /// <summary>Type de generateur.</summary>
    public GeneratorType GeneratorType => _generatorType;

    /// <summary>Generateur actif?</summary>
    public bool IsActive => _isActive && _isPowered;

    /// <summary>Charge actuelle (energie utilisee).</summary>
    public float CurrentLoad => _currentLoad;

    /// <summary>Energie disponible.</summary>
    public float AvailablePower => _isPowered ? Mathf.Max(0, _powerOutput - _currentLoad) : 0f;

    /// <summary>Pourcentage de charge.</summary>
    public float LoadPercentage => _powerOutput > 0 ? _currentLoad / _powerOutput : 0f;

    /// <summary>Niveau de fuel actuel.</summary>
    public float CurrentFuel => _currentFuel;

    /// <summary>Fuel maximum.</summary>
    public float MaxFuel => _maxFuel;

    /// <summary>Pourcentage de fuel.</summary>
    public float FuelPercentage => _maxFuel > 0 ? _currentFuel / _maxFuel : 0f;

    /// <summary>Nombre de consommateurs connectes.</summary>
    public int ConsumerCount => _connectedConsumers.Count;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _connectedConsumers = new List<IPowerConsumer>();
    }

    private void Update()
    {
        if (_isActive)
        {
            UpdateFuelConsumption();
            UpdatePowerDistribution();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _range);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Active/desactive le generateur.
    /// </summary>
    public void SetActive(bool active)
    {
        _isActive = active;

        if (!active)
        {
            // Couper l'energie aux consommateurs
            foreach (var consumer in _connectedConsumers)
            {
                consumer.SetPowerState(false);
            }
        }
    }

    /// <summary>
    /// Ajoute du fuel.
    /// </summary>
    public bool AddFuel(float amount)
    {
        if (!_requiresFuel) return false;
        if (amount <= 0) return false;

        float canAdd = Mathf.Min(amount, _maxFuel - _currentFuel);
        _currentFuel += canAdd;

        OnFuelChanged?.Invoke(_currentFuel);

        return canAdd > 0;
    }

    /// <summary>
    /// Connecte un consommateur d'energie.
    /// </summary>
    public bool ConnectConsumer(IPowerConsumer consumer)
    {
        if (consumer == null) return false;
        if (_connectedConsumers.Contains(consumer)) return false;

        // Verifier la portee
        var consumerMono = consumer as MonoBehaviour;
        if (consumerMono != null)
        {
            float dist = Vector3.Distance(transform.position, consumerMono.transform.position);
            if (dist > _range) return false;
        }

        _connectedConsumers.Add(consumer);
        UpdatePowerDistribution();

        return true;
    }

    /// <summary>
    /// Deconnecte un consommateur.
    /// </summary>
    public void DisconnectConsumer(IPowerConsumer consumer)
    {
        if (_connectedConsumers.Remove(consumer))
        {
            consumer.SetPowerState(false);
            UpdatePowerDistribution();
        }
    }

    /// <summary>
    /// Recherche les consommateurs a portee.
    /// </summary>
    public void ScanForConsumers()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, _range);

        foreach (var collider in colliders)
        {
            var consumer = collider.GetComponent<IPowerConsumer>();
            if (consumer != null && !_connectedConsumers.Contains(consumer))
            {
                ConnectConsumer(consumer);
            }
        }
    }

    #endregion

    #region Private Methods

    private void UpdateFuelConsumption()
    {
        if (!_requiresFuel) return;

        if (_currentFuel > 0)
        {
            _currentFuel -= _fuelConsumptionRate * Time.deltaTime;
            _currentFuel = Mathf.Max(0, _currentFuel);

            OnFuelChanged?.Invoke(_currentFuel);

            if (_currentFuel <= 0)
            {
                TryRefuel();
            }

            _isPowered = _currentFuel > 0;
        }
        else
        {
            TryRefuel();
            _isPowered = _currentFuel > 0;
        }

        bool wasOn = _isPowered;
        if (wasOn != _isPowered)
        {
            OnPowerStateChanged?.Invoke(_isPowered);
        }
    }

    private void TryRefuel()
    {
        if (_fuelStorage == null) return;

        int needed = Mathf.CeilToInt(_maxFuel - _currentFuel);
        int available = _fuelStorage.GetResourceCount(_fuelType);
        int toTake = Mathf.Min(needed, available);

        if (toTake > 0)
        {
            _fuelStorage.RemoveResource(_fuelType, toTake);
            _currentFuel += toTake;
            OnFuelChanged?.Invoke(_currentFuel);
        }
    }

    private void UpdatePowerDistribution()
    {
        float totalDemand = 0f;

        // Calculer la demande totale
        foreach (var consumer in _connectedConsumers)
        {
            totalDemand += consumer.PowerRequired;
        }

        _currentLoad = totalDemand;
        OnLoadChanged?.Invoke(_currentLoad);

        // Distribuer l'energie
        bool hasEnoughPower = totalDemand <= _powerOutput && _isPowered;

        foreach (var consumer in _connectedConsumers)
        {
            consumer.SetPowerState(hasEnoughPower);
        }
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Configure le generateur.
    /// </summary>
    public void Configure(float powerOutput, float range, GeneratorType type)
    {
        _powerOutput = powerOutput;
        _range = range;
        _generatorType = type;
    }

    #endregion
}

/// <summary>
/// Types de generateurs.
/// </summary>
public enum GeneratorType
{
    /// <summary>Generateur manuel (manivelle).</summary>
    Manual,

    /// <summary>Generateur a combustible.</summary>
    Fuel,

    /// <summary>Panneau solaire.</summary>
    Solar,

    /// <summary>Eolienne.</summary>
    Wind,

    /// <summary>Generateur geothermique.</summary>
    Geothermal,

    /// <summary>Reacteur avance.</summary>
    Reactor
}

/// <summary>
/// Interface pour les consommateurs d'energie.
/// </summary>
public interface IPowerConsumer
{
    /// <summary>Energie requise.</summary>
    float PowerRequired { get; }

    /// <summary>Met a jour l'etat d'alimentation.</summary>
    void SetPowerState(bool hasPower);
}
