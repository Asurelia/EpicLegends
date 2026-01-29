using UnityEngine;

/// <summary>
/// Splitter pour diviser le flux de ressources.
/// </summary>
public class ItemSplitter : MonoBehaviour
{
    #region Fields

    [Header("Configuration")]
    [SerializeField] private SplitMode _splitMode = SplitMode.RoundRobin;
    [SerializeField] private float _processInterval = 0.5f;

    [Header("Entree")]
    [SerializeField] private ConveyorBelt _inputBelt;
    [SerializeField] private StorageBuilding _inputStorage;

    [Header("Sorties")]
    [SerializeField] private ConveyorBelt _outputBeltA;
    [SerializeField] private ConveyorBelt _outputBeltB;
    [SerializeField] private ConveyorBelt _outputBeltC;

    [Header("Filtrage")]
    [SerializeField] private bool _useFilters = false;
    [SerializeField] private ResourceType[] _filterOutputA;
    [SerializeField] private ResourceType[] _filterOutputB;
    [SerializeField] private ResourceType[] _filterOutputC;

    // Etat
    private int _currentOutput = 0;
    private float _processTimer = 0f;

    #endregion

    #region Properties

    /// <summary>Mode de split.</summary>
    public SplitMode SplitMode => _splitMode;

    /// <summary>Utilise des filtres?</summary>
    public bool UseFilters => _useFilters;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        _processTimer += Time.deltaTime;

        if (_processTimer >= _processInterval)
        {
            _processTimer = 0f;
            ProcessItem();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Configure le mode de split.
    /// </summary>
    public void SetSplitMode(SplitMode mode)
    {
        _splitMode = mode;
    }

    /// <summary>
    /// Configure les filtres.
    /// </summary>
    public void SetFilters(ResourceType[] filterA, ResourceType[] filterB, ResourceType[] filterC)
    {
        _useFilters = true;
        _filterOutputA = filterA;
        _filterOutputB = filterB;
        _filterOutputC = filterC;
    }

    /// <summary>
    /// Desactive les filtres.
    /// </summary>
    public void ClearFilters()
    {
        _useFilters = false;
        _filterOutputA = null;
        _filterOutputB = null;
        _filterOutputC = null;
    }

    /// <summary>
    /// Connecte l'entree.
    /// </summary>
    public void ConnectInput(ConveyorBelt belt)
    {
        _inputBelt = belt;
    }

    /// <summary>
    /// Connecte les sorties.
    /// </summary>
    public void ConnectOutputs(ConveyorBelt beltA, ConveyorBelt beltB, ConveyorBelt beltC = null)
    {
        _outputBeltA = beltA;
        _outputBeltB = beltB;
        _outputBeltC = beltC;
    }

    #endregion

    #region Private Methods

    private void ProcessItem()
    {
        ResourceType type;
        int amount;

        // Extraire de l'entree
        if (_inputBelt != null)
        {
            if (!_inputBelt.RemoveEndItem(out type, out amount))
                return;
        }
        else if (_inputStorage != null)
        {
            var resources = _inputStorage.GetAllResources();
            bool found = false;

            foreach (var kvp in resources)
            {
                if (kvp.Value > 0 && _inputStorage.RemoveResource(kvp.Key, 1))
                {
                    type = kvp.Key;
                    amount = 1;
                    found = true;
                    break;
                }
            }

            if (!found) return;
            type = ResourceType.Wood; // Fallback
            amount = 1;
        }
        else
        {
            return;
        }

        // Determiner la sortie
        ConveyorBelt targetOutput = GetTargetOutput(type);

        if (targetOutput != null && !targetOutput.IsFull)
        {
            targetOutput.AddItem(type, amount);
        }
        else
        {
            // Pas de sortie disponible, essayer les autres
            if (TryAnyOutput(type, amount))
            {
                return;
            }

            // Remettre dans l'entree si possible
            if (_inputStorage != null)
            {
                _inputStorage.AddResource(type, amount);
            }
        }
    }

    private ConveyorBelt GetTargetOutput(ResourceType type)
    {
        if (_useFilters)
        {
            return GetFilteredOutput(type);
        }

        switch (_splitMode)
        {
            case SplitMode.RoundRobin:
                return GetRoundRobinOutput();

            case SplitMode.Priority:
                return GetPriorityOutput();

            case SplitMode.Random:
                return GetRandomOutput();

            case SplitMode.Overflow:
                return GetOverflowOutput();

            default:
                return _outputBeltA;
        }
    }

    private ConveyorBelt GetFilteredOutput(ResourceType type)
    {
        // Verifier filtre A
        if (_filterOutputA != null && _outputBeltA != null)
        {
            foreach (var filterType in _filterOutputA)
            {
                if (filterType == type && !_outputBeltA.IsFull)
                    return _outputBeltA;
            }
        }

        // Verifier filtre B
        if (_filterOutputB != null && _outputBeltB != null)
        {
            foreach (var filterType in _filterOutputB)
            {
                if (filterType == type && !_outputBeltB.IsFull)
                    return _outputBeltB;
            }
        }

        // Verifier filtre C
        if (_filterOutputC != null && _outputBeltC != null)
        {
            foreach (var filterType in _filterOutputC)
            {
                if (filterType == type && !_outputBeltC.IsFull)
                    return _outputBeltC;
            }
        }

        // Aucun filtre correspondant, utiliser la premiere sortie disponible
        if (_outputBeltA != null && !_outputBeltA.IsFull) return _outputBeltA;
        if (_outputBeltB != null && !_outputBeltB.IsFull) return _outputBeltB;
        if (_outputBeltC != null && !_outputBeltC.IsFull) return _outputBeltC;

        return null;
    }

    private ConveyorBelt GetRoundRobinOutput()
    {
        ConveyorBelt[] outputs = { _outputBeltA, _outputBeltB, _outputBeltC };

        for (int i = 0; i < 3; i++)
        {
            int index = (_currentOutput + i) % 3;
            var belt = outputs[index];

            if (belt != null && !belt.IsFull)
            {
                _currentOutput = (index + 1) % 3;
                return belt;
            }
        }

        return null;
    }

    private ConveyorBelt GetPriorityOutput()
    {
        if (_outputBeltA != null && !_outputBeltA.IsFull) return _outputBeltA;
        if (_outputBeltB != null && !_outputBeltB.IsFull) return _outputBeltB;
        if (_outputBeltC != null && !_outputBeltC.IsFull) return _outputBeltC;
        return null;
    }

    private ConveyorBelt GetRandomOutput()
    {
        ConveyorBelt[] available = new ConveyorBelt[3];
        int count = 0;

        if (_outputBeltA != null && !_outputBeltA.IsFull) available[count++] = _outputBeltA;
        if (_outputBeltB != null && !_outputBeltB.IsFull) available[count++] = _outputBeltB;
        if (_outputBeltC != null && !_outputBeltC.IsFull) available[count++] = _outputBeltC;

        if (count == 0) return null;
        return available[Random.Range(0, count)];
    }

    private ConveyorBelt GetOverflowOutput()
    {
        // Remplir A d'abord, puis B si A est plein, puis C
        if (_outputBeltA != null)
        {
            if (!_outputBeltA.IsFull) return _outputBeltA;
        }
        if (_outputBeltB != null)
        {
            if (!_outputBeltB.IsFull) return _outputBeltB;
        }
        if (_outputBeltC != null)
        {
            if (!_outputBeltC.IsFull) return _outputBeltC;
        }
        return null;
    }

    private bool TryAnyOutput(ResourceType type, int amount)
    {
        if (_outputBeltA != null && !_outputBeltA.IsFull)
        {
            _outputBeltA.AddItem(type, amount);
            return true;
        }
        if (_outputBeltB != null && !_outputBeltB.IsFull)
        {
            _outputBeltB.AddItem(type, amount);
            return true;
        }
        if (_outputBeltC != null && !_outputBeltC.IsFull)
        {
            _outputBeltC.AddItem(type, amount);
            return true;
        }
        return false;
    }

    #endregion
}

/// <summary>
/// Modes de split.
/// </summary>
public enum SplitMode
{
    /// <summary>Alterne entre les sorties.</summary>
    RoundRobin,

    /// <summary>Remplit dans l'ordre de priorite.</summary>
    Priority,

    /// <summary>Choix aleatoire.</summary>
    Random,

    /// <summary>Overflow: remplit A, puis B si plein, puis C.</summary>
    Overflow
}
