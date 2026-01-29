using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gere l'equipe de creatures du joueur.
/// Limite a 6 creatures actives, les autres sont stockees.
/// </summary>
public class CreatureParty : MonoBehaviour
{
    #region Constants

    public const int MAX_PARTY_SIZE = 6;
    public const int MAX_STORAGE_SIZE = 100;

    #endregion

    #region Fields

    [Header("Party")]
    [SerializeField] private List<CreatureInstance> _party = new List<CreatureInstance>();
    [SerializeField] private List<CreatureInstance> _storage = new List<CreatureInstance>();

    [Header("Active Creature")]
    [SerializeField] private int _activeCreatureIndex = 0;
    [SerializeField] private CreatureController _activeController;

    [Header("Settings")]
    [SerializeField] private Transform _creatureSpawnPoint;
    [SerializeField] private float _summonCooldown = 1f;

    // Etat
    private float _lastSummonTime;
    private bool _initialized;

    #endregion

    #region Events

    public event Action<CreatureInstance> OnCreatureAdded;
    public event Action<CreatureInstance> OnCreatureRemoved;
    public event Action<CreatureInstance> OnActiveCreatureChanged;
    public event Action<int> OnPartyReordered;

    #endregion

    #region Properties

    public int CreatureCount => _party.Count;
    public int StorageCount => _storage.Count;
    public int MaxPartySize => MAX_PARTY_SIZE;
    public int MaxStorageSize => MAX_STORAGE_SIZE;
    public bool IsPartyFull => _party.Count >= MAX_PARTY_SIZE;
    public bool IsStorageFull => _storage.Count >= MAX_STORAGE_SIZE;

    public CreatureInstance ActiveCreature =>
        (_activeCreatureIndex >= 0 && _activeCreatureIndex < _party.Count)
            ? _party[_activeCreatureIndex]
            : null;

    public CreatureController ActiveController => _activeController;

    public IReadOnlyList<CreatureInstance> Party => _party.AsReadOnly();
    public IReadOnlyList<CreatureInstance> Storage => _storage.AsReadOnly();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (_initialized) return;

        _party = new List<CreatureInstance>();
        _storage = new List<CreatureInstance>();
        _initialized = true;
    }

    #endregion

    #region Public Methods - Party Management

    /// <summary>
    /// Ajoute une creature a l'equipe ou au stockage.
    /// </summary>
    public bool AddCreature(CreatureInstance creature)
    {
        if (creature == null) return false;

        // Assurer l'initialisation
        Initialize();

        if (!IsPartyFull)
        {
            _party.Add(creature);
            OnCreatureAdded?.Invoke(creature);
            return true;
        }
        else if (!IsStorageFull)
        {
            _storage.Add(creature);
            OnCreatureAdded?.Invoke(creature);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Retire une creature de l'equipe.
    /// </summary>
    public bool RemoveCreature(CreatureInstance creature)
    {
        if (creature == null) return false;

        if (_party.Remove(creature))
        {
            OnCreatureRemoved?.Invoke(creature);

            // Ajuster l'index actif
            if (_activeCreatureIndex >= _party.Count)
            {
                _activeCreatureIndex = Mathf.Max(0, _party.Count - 1);
            }

            return true;
        }

        if (_storage.Remove(creature))
        {
            OnCreatureRemoved?.Invoke(creature);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Obtient une creature par index.
    /// </summary>
    public CreatureInstance GetCreature(int index)
    {
        if (index >= 0 && index < _party.Count)
        {
            return _party[index];
        }
        return null;
    }

    /// <summary>
    /// Echange deux creatures dans l'equipe.
    /// </summary>
    public bool SwapCreatures(int index1, int index2)
    {
        if (index1 < 0 || index1 >= _party.Count) return false;
        if (index2 < 0 || index2 >= _party.Count) return false;
        if (index1 == index2) return false;

        (_party[index1], _party[index2]) = (_party[index2], _party[index1]);
        OnPartyReordered?.Invoke(_activeCreatureIndex);
        return true;
    }

    /// <summary>
    /// Deplace une creature de l'equipe vers le stockage.
    /// </summary>
    public bool MoveToStorage(int partyIndex)
    {
        if (partyIndex < 0 || partyIndex >= _party.Count) return false;
        if (_party.Count <= 1) return false; // Garder au moins une creature
        if (IsStorageFull) return false;

        var creature = _party[partyIndex];
        _party.RemoveAt(partyIndex);
        _storage.Add(creature);

        // Ajuster l'index actif
        if (_activeCreatureIndex >= _party.Count)
        {
            _activeCreatureIndex = _party.Count - 1;
        }

        OnPartyReordered?.Invoke(_activeCreatureIndex);
        return true;
    }

    /// <summary>
    /// Deplace une creature du stockage vers l'equipe.
    /// </summary>
    public bool MoveToParty(int storageIndex)
    {
        if (storageIndex < 0 || storageIndex >= _storage.Count) return false;
        if (IsPartyFull) return false;

        var creature = _storage[storageIndex];
        _storage.RemoveAt(storageIndex);
        _party.Add(creature);

        OnPartyReordered?.Invoke(_activeCreatureIndex);
        return true;
    }

    #endregion

    #region Public Methods - Active Creature

    /// <summary>
    /// Change la creature active.
    /// </summary>
    public bool SetActiveCreature(int index)
    {
        if (index < 0 || index >= _party.Count) return false;
        if (_party[index].IsFainted) return false;

        _activeCreatureIndex = index;
        OnActiveCreatureChanged?.Invoke(ActiveCreature);

        // Mettre a jour le controleur si necessaire
        UpdateActiveController();

        return true;
    }

    /// <summary>
    /// Passe a la creature suivante disponible.
    /// </summary>
    public bool SwitchToNextCreature()
    {
        for (int i = 1; i < _party.Count; i++)
        {
            int index = (_activeCreatureIndex + i) % _party.Count;
            if (!_party[index].IsFainted)
            {
                return SetActiveCreature(index);
            }
        }
        return false;
    }

    /// <summary>
    /// Obtient la premiere creature non KO.
    /// </summary>
    public CreatureInstance GetFirstAvailableCreature()
    {
        foreach (var creature in _party)
        {
            if (!creature.IsFainted)
            {
                return creature;
            }
        }
        return null;
    }

    /// <summary>
    /// Verifie si toutes les creatures sont KO.
    /// </summary>
    public bool IsPartyWiped()
    {
        foreach (var creature in _party)
        {
            if (!creature.IsFainted)
            {
                return false;
            }
        }
        return true;
    }

    #endregion

    #region Public Methods - Summoning

    /// <summary>
    /// Invoque la creature active dans le monde.
    /// </summary>
    public CreatureController SummonActiveCreature(Vector3 position)
    {
        if (ActiveCreature == null) return null;
        if (ActiveCreature.IsFainted) return null;
        if (Time.time - _lastSummonTime < _summonCooldown) return null;

        // Rappeler la creature precedente
        if (_activeController != null)
        {
            RecallActiveCreature();
        }

        // Creer le nouveau controleur
        if (ActiveCreature.Data.prefab != null)
        {
            var go = Instantiate(ActiveCreature.Data.prefab, position, Quaternion.identity);
            _activeController = go.GetComponent<CreatureController>();

            if (_activeController == null)
            {
                _activeController = go.AddComponent<CreatureController>();
            }

            _activeController.Initialize(ActiveCreature, true);
            _activeController.OnCreatureFainted += OnActiveCreatureFainted;

            _lastSummonTime = Time.time;
            return _activeController;
        }

        return null;
    }

    /// <summary>
    /// Rappelle la creature active.
    /// </summary>
    public void RecallActiveCreature()
    {
        if (_activeController != null)
        {
            _activeController.OnCreatureFainted -= OnActiveCreatureFainted;
            Destroy(_activeController.gameObject);
            _activeController = null;
        }
    }

    #endregion

    #region Public Methods - Healing

    /// <summary>
    /// Soigne toutes les creatures de l'equipe.
    /// </summary>
    public void HealAllParty()
    {
        foreach (var creature in _party)
        {
            creature.FullHeal();
        }
    }

    /// <summary>
    /// Soigne une creature specifique.
    /// </summary>
    public void HealCreature(int index, float amount)
    {
        if (index >= 0 && index < _party.Count)
        {
            _party[index].Heal(amount);
        }
    }

    #endregion

    #region Private Methods

    private void UpdateActiveController()
    {
        if (_activeController != null && _activeController.Instance != ActiveCreature)
        {
            // Rappeler et re-invoquer
            Vector3 position = _activeController.transform.position;
            RecallActiveCreature();
            SummonActiveCreature(position);
        }
    }

    private void OnActiveCreatureFainted(CreatureController controller)
    {
        // Passer automatiquement a la creature suivante
        if (!SwitchToNextCreature())
        {
            // Toutes les creatures sont KO
            Debug.Log("Party wiped!");
        }
    }

    #endregion
}
