using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Systeme d'aggro pour gerer les menaces.
/// Suit la menace de chaque cible et determine qui attaquer.
/// </summary>
public class AggroSystem : MonoBehaviour
{
    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private float _threatDecayRate = 5f;
    [SerializeField] private float _threatDecayDelay = 3f;
    [SerializeField] private float _maxThreat = 1000f;

    #endregion

    #region Private Fields

    private Dictionary<GameObject, float> _threatTable = new Dictionary<GameObject, float>();
    private Dictionary<GameObject, float> _lastThreatTime = new Dictionary<GameObject, float>();
    private GameObject _currentTarget;

    #endregion

    #region Events

    /// <summary>
    /// Declenche quand la cible change.
    /// </summary>
    public event Action<GameObject, GameObject> OnTargetChanged;

    /// <summary>
    /// Declenche quand une menace est ajoutee.
    /// </summary>
    public event Action<GameObject, float> OnThreatAdded;

    #endregion

    #region Properties

    /// <summary>
    /// Cible actuelle avec la plus haute menace.
    /// </summary>
    public GameObject CurrentTarget => _currentTarget;

    /// <summary>
    /// Nombre de cibles dans la table de menace.
    /// </summary>
    public int ThreatCount => _threatTable.Count;

    /// <summary>
    /// A-t-on des cibles?
    /// </summary>
    public bool HasTargets => _threatTable.Count > 0;

    #endregion

    #region Unity Callbacks

    private void Update()
    {
        DecayThreat();
        UpdateTarget();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Ajoute de la menace pour une cible.
    /// </summary>
    public void AddThreat(GameObject target, float amount)
    {
        if (target == null || amount <= 0f) return;

        if (!_threatTable.ContainsKey(target))
        {
            _threatTable[target] = 0f;
        }

        _threatTable[target] = Mathf.Min(_maxThreat, _threatTable[target] + amount);
        _lastThreatTime[target] = Time.time;

        OnThreatAdded?.Invoke(target, amount);
    }

    /// <summary>
    /// Retire de la menace d'une cible.
    /// </summary>
    public void RemoveThreat(GameObject target, float amount)
    {
        if (target == null || !_threatTable.ContainsKey(target)) return;

        _threatTable[target] = Mathf.Max(0f, _threatTable[target] - amount);

        if (_threatTable[target] <= 0f)
        {
            _threatTable.Remove(target);
            _lastThreatTime.Remove(target);
        }
    }

    /// <summary>
    /// Efface toute la menace d'une cible.
    /// </summary>
    public void ClearThreat(GameObject target)
    {
        if (target == null) return;

        _threatTable.Remove(target);
        _lastThreatTime.Remove(target);

        if (_currentTarget == target)
        {
            _currentTarget = null;
        }
    }

    /// <summary>
    /// Efface toute la table de menace.
    /// </summary>
    public void ClearAllThreat()
    {
        var oldTarget = _currentTarget;
        _threatTable.Clear();
        _lastThreatTime.Clear();
        _currentTarget = null;

        if (oldTarget != null)
        {
            OnTargetChanged?.Invoke(oldTarget, null);
        }
    }

    /// <summary>
    /// Obtient la menace d'une cible.
    /// </summary>
    public float GetThreat(GameObject target)
    {
        if (target == null || !_threatTable.ContainsKey(target)) return 0f;
        return _threatTable[target];
    }

    /// <summary>
    /// Obtient la cible avec la plus haute menace.
    /// </summary>
    public GameObject GetHighestThreatTarget()
    {
        GameObject highest = null;
        float highestThreat = 0f;

        foreach (var kvp in _threatTable)
        {
            if (kvp.Key == null) continue;
            if (kvp.Value > highestThreat)
            {
                highestThreat = kvp.Value;
                highest = kvp.Key;
            }
        }

        return highest;
    }

    /// <summary>
    /// Multiplie la menace d'une cible (taunt).
    /// </summary>
    public void MultiplyThreat(GameObject target, float multiplier)
    {
        if (target == null || !_threatTable.ContainsKey(target)) return;

        _threatTable[target] = Mathf.Min(_maxThreat, _threatTable[target] * multiplier);
        _lastThreatTime[target] = Time.time;
    }

    /// <summary>
    /// Force une cible specifique (taunt force).
    /// </summary>
    public void ForceTaunt(GameObject target, float duration)
    {
        if (target == null) return;

        // Mettre la menace au maximum
        _threatTable[target] = _maxThreat;
        _lastThreatTime[target] = Time.time + duration;

        var oldTarget = _currentTarget;
        _currentTarget = target;

        if (oldTarget != target)
        {
            OnTargetChanged?.Invoke(oldTarget, target);
        }
    }

    #endregion

    #region Private Methods

    private void DecayThreat()
    {
        if (_threatDecayRate <= 0f) return;

        List<GameObject> toRemove = new List<GameObject>();

        foreach (var kvp in _threatTable)
        {
            if (kvp.Key == null)
            {
                toRemove.Add(kvp.Key);
                continue;
            }

            // Verifier si le delai de decay est passe
            if (_lastThreatTime.ContainsKey(kvp.Key))
            {
                float lastTime = _lastThreatTime[kvp.Key];
                if (Time.time - lastTime < _threatDecayDelay) continue;
            }

            // Appliquer le decay
            float newThreat = kvp.Value - _threatDecayRate * Time.deltaTime;
            if (newThreat <= 0f)
            {
                toRemove.Add(kvp.Key);
            }
            else
            {
                _threatTable[kvp.Key] = newThreat;
            }
        }

        // Nettoyer les cibles mortes ou a 0
        foreach (var target in toRemove)
        {
            _threatTable.Remove(target);
            _lastThreatTime.Remove(target);
        }
    }

    private void UpdateTarget()
    {
        var newTarget = GetHighestThreatTarget();

        if (newTarget != _currentTarget)
        {
            var oldTarget = _currentTarget;
            _currentTarget = newTarget;
            OnTargetChanged?.Invoke(oldTarget, newTarget);
        }
    }

    #endregion
}
