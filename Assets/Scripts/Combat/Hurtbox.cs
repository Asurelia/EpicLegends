using System;
using UnityEngine;

/// <summary>
/// Composant de hurtbox pour recevoir les degats.
/// Gere les differents etats (vulnerable, invincible, parade, blocage).
/// </summary>
public class Hurtbox : MonoBehaviour
{
    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private HurtboxState _state = HurtboxState.Vulnerable;
    [SerializeField] private float _blockDamageReduction = 0.7f;

    #endregion

    #region Events

    /// <summary>
    /// Declenche quand des degats sont recus.
    /// </summary>
    public event Action<DamageInfo> OnDamageReceived;

    /// <summary>
    /// Declenche quand une parade reussit.
    /// </summary>
    public event Action<DamageInfo> OnParrySuccess;

    /// <summary>
    /// Declenche quand un blocage reussit.
    /// </summary>
    public event Action<DamageInfo> OnBlockSuccess;

    /// <summary>
    /// Declenche quand la garde est brisee.
    /// </summary>
    public event Action OnGuardBreak;

    #endregion

    #region Properties

    /// <summary>
    /// Etat actuel de la hurtbox.
    /// </summary>
    public HurtboxState State => _state;

    /// <summary>
    /// Reduction des degats lors du blocage (0-1).
    /// </summary>
    public float BlockDamageReduction
    {
        get => _blockDamageReduction;
        set => _blockDamageReduction = Mathf.Clamp01(value);
    }

    /// <summary>
    /// Proprietaire de la hurtbox.
    /// </summary>
    public GameObject Owner { get; set; }

    #endregion

    #region Public Methods

    /// <summary>
    /// Change l'etat de la hurtbox.
    /// </summary>
    public void SetState(HurtboxState newState)
    {
        _state = newState;
    }

    /// <summary>
    /// Tente de recevoir des degats.
    /// Retourne true si les degats ont ete appliques.
    /// </summary>
    public bool TryReceiveDamage(DamageInfo damageInfo)
    {
        switch (_state)
        {
            case HurtboxState.Invincible:
                // Ignore completement les degats
                return false;

            case HurtboxState.Parrying:
                if (damageInfo.canBeParried)
                {
                    OnParrySuccess?.Invoke(damageInfo);
                    return false;
                }
                // L'attaque ne peut pas etre paree, traiter comme vulnerable
                break;

            case HurtboxState.Blocking:
                if (damageInfo.isGuardBreak)
                {
                    OnGuardBreak?.Invoke();
                    ApplyDamage(damageInfo);
                    return true;
                }
                if (damageInfo.canBeBlocked)
                {
                    // Reduire les degats
                    var reducedInfo = damageInfo;
                    reducedInfo.baseDamage *= (1f - _blockDamageReduction);
                    OnBlockSuccess?.Invoke(damageInfo);
                    ApplyDamage(reducedInfo);
                    return true;
                }
                break;

            case HurtboxState.SuperArmor:
                // Prend les degats mais ne stagger pas
                var noStaggerInfo = damageInfo;
                noStaggerInfo.staggerValue = 0f;
                ApplyDamage(noStaggerInfo);
                return true;

            case HurtboxState.Vulnerable:
            default:
                ApplyDamage(damageInfo);
                return true;
        }

        // Par defaut, appliquer les degats normalement
        ApplyDamage(damageInfo);
        return true;
    }

    /// <summary>
    /// Force la reception de degats (ignore l'etat).
    /// </summary>
    public void ForceDamage(DamageInfo damageInfo)
    {
        ApplyDamage(damageInfo);
    }

    #endregion

    #region Private Methods

    private void Awake()
    {
        if (Owner == null)
        {
            Owner = gameObject;
        }
    }

    private void ApplyDamage(DamageInfo damageInfo)
    {
        OnDamageReceived?.Invoke(damageInfo);
    }

    #endregion
}
