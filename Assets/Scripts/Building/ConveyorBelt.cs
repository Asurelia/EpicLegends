using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tapis roulant pour le transport de ressources.
/// </summary>
public class ConveyorBelt : MonoBehaviour
{
    #region Fields

    [Header("Configuration")]
    [SerializeField] private float _speed = 2f;
    [SerializeField] private float _itemSpacing = 1f;
    [SerializeField] private int _maxItems = 10;

    [Header("Connexions")]
    [SerializeField] private ConveyorBelt _inputBelt;
    [SerializeField] private ConveyorBelt _outputBelt;
    [SerializeField] private StorageBuilding _inputStorage;
    [SerializeField] private StorageBuilding _outputStorage;

    [Header("Visuel")]
    [SerializeField] private Transform _startPoint;
    [SerializeField] private Transform _endPoint;
    [SerializeField] private GameObject _itemVisualPrefab;

    // Items sur le convoyeur
    private List<ConveyorItem> _items = new List<ConveyorItem>();
    private float _extractTimer = 0f;
    private float _extractInterval = 0.5f;

    #endregion

    #region Properties

    /// <summary>Vitesse du convoyeur.</summary>
    public float Speed => _speed;

    /// <summary>Nombre d'items sur le convoyeur.</summary>
    public int ItemCount => _items.Count;

    /// <summary>Convoyeur plein?</summary>
    public bool IsFull => _items.Count >= _maxItems;

    /// <summary>Convoyeur vide?</summary>
    public bool IsEmpty => _items.Count == 0;

    /// <summary>Longueur du convoyeur.</summary>
    public float Length
    {
        get
        {
            if (_startPoint == null || _endPoint == null)
                return 1f;
            return Vector3.Distance(_startPoint.position, _endPoint.position);
        }
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _items = new List<ConveyorItem>();

        if (_startPoint == null)
            _startPoint = transform;
        if (_endPoint == null)
        {
            var endGO = new GameObject("EndPoint");
            endGO.transform.SetParent(transform);
            endGO.transform.localPosition = Vector3.forward * 2f;
            _endPoint = endGO.transform;
        }
    }

    private void Update()
    {
        UpdateItems();
        TryExtractFromInput();
        TryDeliverToOutput();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Ajoute un item au debut du convoyeur.
    /// </summary>
    public bool AddItem(ResourceType type, int amount)
    {
        if (IsFull) return false;

        // Verifier l'espacement
        if (_items.Count > 0)
        {
            var lastItem = _items[_items.Count - 1];
            if (lastItem.progress < _itemSpacing / Length)
                return false;
        }

        var item = new ConveyorItem
        {
            resourceType = type,
            amount = amount,
            progress = 0f
        };

        // Creer le visuel
        if (_itemVisualPrefab != null)
        {
            item.visual = Instantiate(_itemVisualPrefab, _startPoint.position, Quaternion.identity, transform);
        }

        _items.Add(item);
        return true;
    }

    /// <summary>
    /// Retire l'item a la fin du convoyeur.
    /// </summary>
    public bool RemoveEndItem(out ResourceType type, out int amount)
    {
        type = ResourceType.Wood;
        amount = 0;

        if (_items.Count == 0) return false;

        // Trouver l'item a la fin
        int endIndex = -1;

        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].progress >= 1f)
            {
                endIndex = i;
                break;
            }
        }

        if (endIndex < 0) return false;

        var endItem = _items[endIndex];
        type = endItem.resourceType;
        amount = endItem.amount;

        // Detruire le visuel
        if (endItem.visual != null)
        {
            Destroy(endItem.visual);
        }

        _items.RemoveAt(endIndex);
        return true;
    }

    /// <summary>
    /// Connecte a un convoyeur d'entree.
    /// </summary>
    public void ConnectInput(ConveyorBelt belt)
    {
        _inputBelt = belt;
        if (belt != null)
        {
            belt._outputBelt = this;
        }
    }

    /// <summary>
    /// Connecte a un convoyeur de sortie.
    /// </summary>
    public void ConnectOutput(ConveyorBelt belt)
    {
        _outputBelt = belt;
        if (belt != null)
        {
            belt._inputBelt = this;
        }
    }

    /// <summary>
    /// Connecte a un stockage d'entree.
    /// </summary>
    public void ConnectInputStorage(StorageBuilding storage)
    {
        _inputStorage = storage;
    }

    /// <summary>
    /// Connecte a un stockage de sortie.
    /// </summary>
    public void ConnectOutputStorage(StorageBuilding storage)
    {
        _outputStorage = storage;
    }

    /// <summary>
    /// Vide le convoyeur.
    /// </summary>
    public void Clear()
    {
        foreach (var item in _items)
        {
            if (item.visual != null)
            {
                Destroy(item.visual);
            }
        }
        _items.Clear();
    }

    #endregion

    #region Private Methods

    private void UpdateItems()
    {
        float moveAmount = (_speed / Length) * Time.deltaTime;

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];

            // Verifier le blocage par l'item devant
            bool blocked = false;
            if (i > 0)
            {
                var itemAhead = _items[i - 1];
                float minDistance = _itemSpacing / Length;
                if (item.progress + moveAmount > itemAhead.progress - minDistance)
                {
                    blocked = true;
                }
            }

            // Bloquer a la fin si pas de sortie
            if (item.progress >= 1f)
            {
                blocked = true;
            }

            if (!blocked)
            {
                item.progress = Mathf.Min(1f, item.progress + moveAmount);
                _items[i] = item;
            }

            // Mettre a jour le visuel
            if (item.visual != null)
            {
                item.visual.transform.position = Vector3.Lerp(
                    _startPoint.position,
                    _endPoint.position,
                    item.progress
                );
            }
        }
    }

    private void TryExtractFromInput()
    {
        _extractTimer += Time.deltaTime;
        if (_extractTimer < _extractInterval) return;
        _extractTimer = 0f;

        if (IsFull) return;

        // Extraire du convoyeur d'entree
        if (_inputBelt != null)
        {
            if (_inputBelt.RemoveEndItem(out var type, out var amount))
            {
                AddItem(type, amount);
            }
        }
        // Ou du stockage d'entree
        else if (_inputStorage != null)
        {
            var resources = _inputStorage.GetAllResources();
            foreach (var kvp in resources)
            {
                if (kvp.Value > 0)
                {
                    if (_inputStorage.RemoveResource(kvp.Key, 1))
                    {
                        AddItem(kvp.Key, 1);
                        break;
                    }
                }
            }
        }
    }

    private void TryDeliverToOutput()
    {
        if (_items.Count == 0) return;

        // Trouver l'item a la fin
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].progress >= 1f)
            {
                var item = _items[i];

                bool delivered = false;

                // Livrer au convoyeur de sortie
                if (_outputBelt != null && !_outputBelt.IsFull)
                {
                    if (_outputBelt.AddItem(item.resourceType, item.amount))
                    {
                        delivered = true;
                    }
                }
                // Ou au stockage de sortie
                else if (_outputStorage != null)
                {
                    if (_outputStorage.AddResource(item.resourceType, item.amount))
                    {
                        delivered = true;
                    }
                }

                if (delivered)
                {
                    if (item.visual != null)
                    {
                        Destroy(item.visual);
                    }
                    _items.RemoveAt(i);
                    break;
                }
            }
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (_startPoint != null && _endPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(_startPoint.position, _endPoint.position);
            Gizmos.DrawSphere(_startPoint.position, 0.1f);
            Gizmos.DrawSphere(_endPoint.position, 0.1f);
        }
    }

    #endregion
}

/// <summary>
/// Item sur un convoyeur.
/// </summary>
public struct ConveyorItem
{
    public ResourceType resourceType;
    public int amount;
    public float progress; // 0 = debut, 1 = fin
    public GameObject visual;
}
