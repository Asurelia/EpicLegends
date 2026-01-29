using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Composant de hitbox pour les attaques.
/// Detecte les collisions avec les hurtboxes ennemies.
/// </summary>
public class Hitbox : MonoBehaviour
{
    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private HitboxData _data;
    [SerializeField] private LayerMask _targetLayers;

    #endregion

    #region Private Fields

    private bool _isActive;
    private HashSet<GameObject> _hitTargets = new HashSet<GameObject>();
    private DamageInfo _currentDamageInfo;

    #endregion

    #region Events

    /// <summary>
    /// Declenche quand une cible est touchee.
    /// </summary>
    public event Action<GameObject, DamageInfo> OnHit;

    #endregion

    #region Properties

    /// <summary>
    /// La hitbox est-elle active?
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Donnees de configuration.
    /// </summary>
    public HitboxData Data
    {
        get => _data;
        set => _data = value;
    }

    /// <summary>
    /// Proprietaire de la hitbox.
    /// </summary>
    public GameObject Owner { get; set; }

    #endregion

    #region Public Methods

    /// <summary>
    /// Active la hitbox pour detecter les collisions.
    /// </summary>
    public void Activate()
    {
        Activate(new DamageInfo(10f, DamageType.Physical, Owner));
    }

    /// <summary>
    /// Active la hitbox avec des informations de degats specifiques.
    /// </summary>
    public void Activate(DamageInfo damageInfo)
    {
        _hitTargets.Clear();
        _currentDamageInfo = damageInfo;
        _isActive = true;
    }

    /// <summary>
    /// Desactive la hitbox.
    /// </summary>
    public void Deactivate()
    {
        _isActive = false;
    }

    /// <summary>
    /// Enregistre une cible comme touchee.
    /// </summary>
    public void RegisterHit(GameObject target)
    {
        if (target != null)
        {
            _hitTargets.Add(target);
        }
    }

    /// <summary>
    /// Verifie si une cible a deja ete touchee.
    /// </summary>
    public bool HasHit(GameObject target)
    {
        return target != null && _hitTargets.Contains(target);
    }

    /// <summary>
    /// Efface la liste des cibles touchees.
    /// </summary>
    public void ClearHitTargets()
    {
        _hitTargets.Clear();
    }

    /// <summary>
    /// Effectue une detection manuelle des collisions.
    /// </summary>
    public void CheckCollisions()
    {
        if (!_isActive || _data == null) return;

        Collider[] hits;
        Vector3 worldPosition = transform.TransformPoint(_data.offset);

        switch (_data.shape)
        {
            case HitboxShape.Sphere:
                hits = Physics.OverlapSphere(worldPosition, _data.radius, _targetLayers);
                break;
            case HitboxShape.Box:
            default:
                hits = Physics.OverlapBox(worldPosition, _data.size * 0.5f, transform.rotation, _targetLayers);
                break;
        }

        int hitCount = 0;
        foreach (var hit in hits)
        {
            if (hitCount >= _data.maxTargets) break;

            var target = hit.gameObject;
            if (target == Owner) continue; // Ne pas se toucher soi-meme
            if (HasHit(target) && !_data.canRehit) continue;

            var hurtbox = hit.GetComponent<Hurtbox>();
            if (hurtbox != null)
            {
                ProcessHit(target, hurtbox);
                hitCount++;
            }
        }
    }

    #endregion

    #region Private Methods

    private void FixedUpdate()
    {
        if (_isActive)
        {
            CheckCollisions();
        }
    }

    private void ProcessHit(GameObject target, Hurtbox hurtbox)
    {
        if (HasHit(target) && !_data.canRehit) return;

        // Preparer les degats avec les modificateurs de la hitbox
        var damageInfo = _currentDamageInfo;
        if (_data != null)
        {
            damageInfo.baseDamage *= _data.damageMultiplier;
            damageInfo.knockbackForce *= _data.knockbackMultiplier;
            damageInfo.staggerValue *= _data.staggerMultiplier;
        }

        // Calculer la direction du knockback
        damageInfo.knockbackDirection = (target.transform.position - transform.position).normalized;
        damageInfo.hitPoint = target.transform.position;

        // Envoyer les degats
        if (hurtbox.TryReceiveDamage(damageInfo))
        {
            RegisterHit(target);
            OnHit?.Invoke(target, damageInfo);

            // Effets visuels et sonores
            if (_data != null)
            {
                if (_data.hitEffectPrefab != null)
                {
                    Instantiate(_data.hitEffectPrefab, damageInfo.hitPoint, Quaternion.identity);
                }
                if (_data.hitSound != null)
                {
                    AudioSource.PlayClipAtPoint(_data.hitSound, damageInfo.hitPoint);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_data == null) return;

        Gizmos.color = _isActive ? Color.red : Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;

        switch (_data.shape)
        {
            case HitboxShape.Sphere:
                Gizmos.DrawWireSphere(_data.offset, _data.radius);
                break;
            case HitboxShape.Box:
            default:
                Gizmos.DrawWireCube(_data.offset, _data.size);
                break;
        }
    }

    #endregion
}
