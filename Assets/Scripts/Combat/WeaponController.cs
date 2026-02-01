using System;
using UnityEngine;

/// <summary>
/// Controleur d'arme pour un personnage.
/// Gere l'equipement, les attaques et les combos specifiques a l'arme.
/// </summary>
public class WeaponController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private WeaponData _currentWeaponData;
    [SerializeField] private Transform _weaponSocket;
    [SerializeField] private float _attackerAttack = 0f;

    [Header("Components")]
    [SerializeField] private CombatController _combatController;
    [SerializeField] private Hitbox _hitbox;
    [SerializeField] private Animator _animator;

    #endregion

    #region Private Fields

    private GameObject _equippedWeaponInstance;
    private float _chargeTimer;
    private bool _isCharging;

    #endregion

    #region Events

    /// <summary>
    /// Declenche quand une arme est equipee.
    /// </summary>
    public event Action<WeaponData> OnWeaponEquipped;

    /// <summary>
    /// Declenche quand une arme est retiree.
    /// </summary>
    public event Action<WeaponData> OnWeaponUnequipped;

    /// <summary>
    /// Declenche quand une attaque chargee est complete.
    /// </summary>
    public event Action OnChargeComplete;

    #endregion

    #region Properties

    /// <summary>
    /// Donnees de l'arme actuelle.
    /// </summary>
    public WeaponData CurrentWeapon => _currentWeaponData;

    /// <summary>
    /// Une arme est-elle equipee?
    /// </summary>
    public bool HasWeaponEquipped => _currentWeaponData != null;

    /// <summary>
    /// Peut-on attaquer actuellement?
    /// </summary>
    public bool CanAttack => HasWeaponEquipped && (_combatController == null || _combatController.CanAttack());

    /// <summary>
    /// Est-ce que l'arme est en train de charger?
    /// </summary>
    public bool IsCharging => _isCharging;

    /// <summary>
    /// Temps de charge actuel.
    /// </summary>
    public float ChargeTime => _chargeTimer;

    /// <summary>
    /// Pourcentage de charge (0-1).
    /// </summary>
    public float ChargePercent
    {
        get
        {
            if (!HasWeaponEquipped || !_currentWeaponData.canCharge) return 0f;
            return Mathf.Clamp01((_chargeTimer - _currentWeaponData.minChargeTime) /
                                  (_currentWeaponData.maxChargeTime - _currentWeaponData.minChargeTime));
        }
    }

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        // Auto-detection des composants
        if (_combatController == null)
            _combatController = GetComponent<CombatController>();
        if (_hitbox == null)
            _hitbox = GetComponentInChildren<Hitbox>();
        if (_animator == null)
            _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        // Equiper l'arme initiale si definie
        if (_currentWeaponData != null)
        {
            EquipWeapon(_currentWeaponData);
        }
    }

    private void Update()
    {
        if (_isCharging)
        {
            UpdateCharging();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Equipe une nouvelle arme.
    /// </summary>
    public void EquipWeapon(WeaponData weaponData)
    {
        if (weaponData == null) return;

        // Retirer l'arme actuelle
        if (_currentWeaponData != null)
        {
            UnequipWeapon();
        }

        _currentWeaponData = weaponData;

        // Instancier le modele 3D
        if (weaponData.prefab != null && _weaponSocket != null)
        {
            _equippedWeaponInstance = Instantiate(
                weaponData.prefab,
                _weaponSocket.position,
                _weaponSocket.rotation,
                _weaponSocket
            );
        }

        // Configurer la hitbox selon l'arme
        if (_hitbox != null)
        {
            ConfigureHitboxForWeapon(weaponData);
        }

        OnWeaponEquipped?.Invoke(weaponData);
    }

    /// <summary>
    /// Retire l'arme actuelle.
    /// </summary>
    public void UnequipWeapon()
    {
        if (_currentWeaponData == null) return;

        var previousWeapon = _currentWeaponData;

        // Detruire l'instance visuelle
        if (_equippedWeaponInstance != null)
        {
            Destroy(_equippedWeaponInstance);
            _equippedWeaponInstance = null;
        }

        _currentWeaponData = null;
        OnWeaponUnequipped?.Invoke(previousWeapon);
    }

    /// <summary>
    /// Execute une attaque legere.
    /// </summary>
    public void LightAttack()
    {
        if (!CanAttack) return;

        if (_combatController != null)
        {
            _combatController.LightAttack();
        }
        else
        {
            // Attaque directe sans CombatController
            ExecuteDirectAttack(false);
        }
    }

    /// <summary>
    /// Execute une attaque lourde.
    /// </summary>
    public void HeavyAttack()
    {
        if (!CanAttack) return;

        if (_combatController != null)
        {
            _combatController.HeavyAttack();
        }
        else
        {
            ExecuteDirectAttack(true);
        }
    }

    /// <summary>
    /// Commence a charger une attaque.
    /// </summary>
    public void StartCharging()
    {
        if (!HasWeaponEquipped || !_currentWeaponData.canCharge) return;
        if (!CanAttack) return;

        _isCharging = true;
        _chargeTimer = 0f;

        if (_combatController != null)
        {
            _combatController.StartCharging();
        }
    }

    /// <summary>
    /// Relache l'attaque chargee.
    /// </summary>
    public void ReleaseCharge()
    {
        if (!_isCharging) return;

        _isCharging = false;

        // Calculer les degats selon la charge
        float damage = _currentWeaponData.GetChargedDamage(_chargeTimer, _attackerAttack);

        // Creer et appliquer l'attaque
        if (_hitbox != null)
        {
            var damageInfo = _currentWeaponData.CreateDamageInfo(gameObject, _attackerAttack);
            damageInfo.baseDamage = damage;
            _hitbox.Activate(damageInfo);
        }

        if (_combatController != null)
        {
            _combatController.ReleaseCharge();
        }

        _chargeTimer = 0f;
    }

    /// <summary>
    /// Annule la charge en cours.
    /// </summary>
    public void CancelCharge()
    {
        _isCharging = false;
        _chargeTimer = 0f;
    }

    /// <summary>
    /// Obtient les degats actuels de l'arme.
    /// </summary>
    public float GetCurrentDamage()
    {
        if (!HasWeaponEquipped) return 0f;
        return _currentWeaponData.GetDamageWithStats(_attackerAttack);
    }

    /// <summary>
    /// Obtient les degats d'une attaque chargee au niveau actuel.
    /// </summary>
    public float GetChargedDamage()
    {
        if (!HasWeaponEquipped) return 0f;
        return _currentWeaponData.GetChargedDamage(_chargeTimer, _attackerAttack);
    }

    /// <summary>
    /// Definit la stat d'attaque du porteur.
    /// </summary>
    public void SetAttackerAttack(float attack)
    {
        _attackerAttack = Mathf.Max(0f, attack);
    }

    #endregion

    #region Private Methods

    private void UpdateCharging()
    {
        _chargeTimer += Time.deltaTime;

        // Verifier si la charge est complete
        if (_chargeTimer >= _currentWeaponData.maxChargeTime)
        {
            _chargeTimer = _currentWeaponData.maxChargeTime;
            OnChargeComplete?.Invoke();

            // Jouer le son de charge complete
            if (_currentWeaponData.chargeCompleteSound != null)
            {
                AudioSource.PlayClipAtPoint(
                    _currentWeaponData.chargeCompleteSound,
                    transform.position
                );
            }
        }
    }

    private void ExecuteDirectAttack(bool isHeavy)
    {
        if (_hitbox == null) return;

        float damageMultiplier = isHeavy ? 1.5f : 1f;
        var damageInfo = _currentWeaponData.CreateDamageInfo(gameObject, _attackerAttack);
        damageInfo.baseDamage *= damageMultiplier;

        _hitbox.Activate(damageInfo);
    }

    private void ConfigureHitboxForWeapon(WeaponData weaponData)
    {
        if (weaponData == null || _hitbox == null) return;

        // Creer ou modifier les donnees de hitbox selon le type d'arme
        var hitboxData = ScriptableObject.CreateInstance<HitboxData>();

        // Configurer la taille selon le type d'arme et sa portee
        switch (weaponData.weaponType)
        {
            case WeaponType.Sword:
                hitboxData.shape = HitboxShape.Box;
                hitboxData.size = new Vector3(1f, 0.5f, weaponData.range);
                hitboxData.offset = new Vector3(0f, 0.5f, weaponData.range * 0.5f);
                break;

            case WeaponType.Greatsword:
                hitboxData.shape = HitboxShape.Box;
                hitboxData.size = new Vector3(1.5f, 1f, weaponData.range);
                hitboxData.offset = new Vector3(0f, 0.75f, weaponData.range * 0.5f);
                hitboxData.maxTargets = 8; // Plus de cibles pour armes lourdes
                break;

            case WeaponType.DualBlades:
                hitboxData.shape = HitboxShape.Sphere;
                hitboxData.radius = weaponData.range * 0.6f;
                hitboxData.offset = new Vector3(0f, 0.5f, weaponData.range * 0.4f);
                hitboxData.canRehit = true; // Multi-hit pour lames doubles
                hitboxData.rehitDelay = 0.2f;
                break;

            case WeaponType.Spear:
                hitboxData.shape = HitboxShape.Box;
                hitboxData.size = new Vector3(0.4f, 0.4f, weaponData.range);
                hitboxData.offset = new Vector3(0f, 0.5f, weaponData.range * 0.5f);
                hitboxData.maxTargets = 3; // Moins de cibles, mais plus de portee
                break;

            case WeaponType.Scythe:
                hitboxData.shape = HitboxShape.Sphere;
                hitboxData.radius = weaponData.range * 0.8f;
                hitboxData.offset = new Vector3(0f, 0.5f, weaponData.range * 0.3f);
                hitboxData.maxTargets = 6;
                break;

            default:
                hitboxData.shape = HitboxShape.Box;
                hitboxData.size = new Vector3(1f, 0.5f, weaponData.range);
                hitboxData.offset = new Vector3(0f, 0.5f, weaponData.range * 0.5f);
                break;
        }

        // Configurer les multiplicateurs selon les stats de l'arme
        hitboxData.damageMultiplier = 1f;
        hitboxData.knockbackMultiplier = weaponData.knockbackForce / 3f; // Normalise par rapport a la valeur par defaut
        hitboxData.staggerMultiplier = weaponData.staggerValue / 10f; // Normalise

        _hitbox.Data = hitboxData;
    }

    #endregion
}
