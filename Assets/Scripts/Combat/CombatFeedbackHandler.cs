using UnityEngine;

/// <summary>
/// Gere le feedback visuel et audio du combat.
/// Connecte le systeme de combat avec les effets de polish (shake, hitstop, VFX).
/// </summary>
public class CombatFeedbackHandler : MonoBehaviour
{
    #region Serialized Fields

    [Header("References")]
    [SerializeField] private CombatController _combatController;
    [SerializeField] private WeaponController _weaponController;
    // TODO: Re-enable when CameraController is updated for Cinemachine 3.x
    // CameraController temporarily disabled - using VisualEffectsManager fallback

    [Header("Hit Feedback")]
    [SerializeField] private float _lightHitShakeIntensity = 0.2f;
    [SerializeField] private float _lightHitShakeDuration = 0.1f;
    [SerializeField] private float _heavyHitShakeIntensity = 0.5f;
    [SerializeField] private float _heavyHitShakeDuration = 0.2f;
    [SerializeField] private float _criticalHitShakeIntensity = 0.8f;
    [SerializeField] private float _criticalHitShakeDuration = 0.25f;

    [Header("Hitstop")]
    [SerializeField] private bool _enableHitstop = true;
    [SerializeField] private float _lightHitstopDuration = 0.03f;
    [SerializeField] private float _heavyHitstopDuration = 0.06f;
    [SerializeField] private float _criticalHitstopDuration = 0.1f;
    [SerializeField] private float _hitstopTimeScale = 0.01f;

    [Header("Damage Received Feedback")]
    [SerializeField] private float _damageShakeIntensity = 0.3f;
    [SerializeField] private float _damageShakeDuration = 0.15f;
    [SerializeField] private Color _damageFlashColor = new Color(1f, 0.3f, 0.3f, 0.5f);
    [SerializeField] private float _damageFlashDuration = 0.1f;

    [Header("VFX Prefabs")]
    [SerializeField] private GameObject _lightHitVFX;
    [SerializeField] private GameObject _heavyHitVFX;
    [SerializeField] private GameObject _criticalHitVFX;
    [SerializeField] private GameObject _blockVFX;
    [SerializeField] private GameObject _parryVFX;

    [Header("Audio")]
    [SerializeField] private AudioClip[] _lightHitSounds;
    [SerializeField] private AudioClip[] _heavyHitSounds;
    [SerializeField] private AudioClip[] _criticalHitSounds;
    [SerializeField] private AudioClip _blockSound;
    [SerializeField] private AudioClip _parrySound;

    #endregion

    #region Private Fields

    private AudioSource _audioSource;
    private Hurtbox _hurtbox;
    private Renderer[] _renderers;
    private Color[] _originalColors;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        // Auto-detection
        if (_combatController == null)
            _combatController = GetComponent<CombatController>();
        if (_weaponController == null)
            _weaponController = GetComponent<WeaponController>();
        // TODO: Re-enable when CameraController is updated for Cinemachine 3.x
        // CameraController disabled - using VisualEffectsManager fallback only

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f;
        }

        _hurtbox = GetComponentInChildren<Hurtbox>();
        CacheRenderers();
    }

    private void OnEnable()
    {
        // S'abonner aux evenements de combat
        if (_combatController != null)
        {
            _combatController.OnAttackHit += HandleAttackHit;
        }

        if (_hurtbox != null)
        {
            _hurtbox.OnDamageReceived += HandleDamageReceived;
            _hurtbox.OnBlockSuccess += HandleBlockSuccess;
            _hurtbox.OnParrySuccess += HandleParrySuccess;
        }
    }

    private void OnDisable()
    {
        if (_combatController != null)
        {
            _combatController.OnAttackHit -= HandleAttackHit;
        }

        if (_hurtbox != null)
        {
            _hurtbox.OnDamageReceived -= HandleDamageReceived;
            _hurtbox.OnBlockSuccess -= HandleBlockSuccess;
            _hurtbox.OnParrySuccess -= HandleParrySuccess;
        }

        // MAJOR FIX: Stop all coroutines to prevent memory leaks
        StopAllCoroutines();
    }

    #endregion

    #region Event Handlers

    private void HandleAttackHit(DamageInfo damageInfo)
    {
        // Determiner le type de hit
        bool isCritical = damageInfo.isCritical;
        bool isHeavy = damageInfo.baseDamage > 20f || damageInfo.isGuardBreak;

        // Camera Shake
        float shakeIntensity;
        float shakeDuration;
        float hitstopDuration;
        GameObject vfxPrefab;
        AudioClip[] hitSounds;

        if (isCritical)
        {
            shakeIntensity = _criticalHitShakeIntensity;
            shakeDuration = _criticalHitShakeDuration;
            hitstopDuration = _criticalHitstopDuration;
            vfxPrefab = _criticalHitVFX;
            hitSounds = _criticalHitSounds;
        }
        else if (isHeavy)
        {
            shakeIntensity = _heavyHitShakeIntensity;
            shakeDuration = _heavyHitShakeDuration;
            hitstopDuration = _heavyHitstopDuration;
            vfxPrefab = _heavyHitVFX;
            hitSounds = _heavyHitSounds;
        }
        else
        {
            shakeIntensity = _lightHitShakeIntensity;
            shakeDuration = _lightHitShakeDuration;
            hitstopDuration = _lightHitstopDuration;
            vfxPrefab = _lightHitVFX;
            hitSounds = _lightHitSounds;
        }

        // Appliquer le camera shake
        if (VisualEffectsManager.Instance != null)
        {
            VisualEffectsManager.Instance.TriggerScreenShake(shakeIntensity, shakeDuration);
        }

        // Appliquer le hitstop
        if (_enableHitstop && VisualEffectsManager.Instance != null)
        {
            VisualEffectsManager.Instance.TriggerHitStop(hitstopDuration, _hitstopTimeScale);
        }

        // Spawn VFX
        if (vfxPrefab != null)
        {
            SpawnVFX(vfxPrefab, damageInfo.hitPoint, Quaternion.identity);
        }

        // Play sound
        PlayRandomSound(hitSounds);
    }

    private void HandleDamageReceived(DamageInfo damageInfo)
    {
        // Shake plus subtil quand on recoit des degats
        if (VisualEffectsManager.Instance != null)
        {
            VisualEffectsManager.Instance.TriggerScreenShake(_damageShakeIntensity, _damageShakeDuration);
        }

        // Flash de degats
        StartCoroutine(DamageFlashCoroutine());
    }

    private void HandleBlockSuccess(DamageInfo damageInfo)
    {
        // VFX et son de blocage
        if (_blockVFX != null)
        {
            SpawnVFX(_blockVFX, transform.position + Vector3.up, Quaternion.identity);
        }

        PlaySound(_blockSound);

        // Petit shake via VisualEffectsManager
        if (VisualEffectsManager.Instance != null)
        {
            VisualEffectsManager.Instance.TriggerScreenShake(0.15f, 0.1f);
        }
    }

    private void HandleParrySuccess(DamageInfo damageInfo)
    {
        // VFX et son de parade
        if (_parryVFX != null)
        {
            SpawnVFX(_parryVFX, transform.position + Vector3.up, Quaternion.identity);
        }

        PlaySound(_parrySound);

        // Hitstop pour parade parfaite
        if (VisualEffectsManager.Instance != null)
        {
            VisualEffectsManager.Instance.TriggerHitStop(0.15f, 0.02f);
        }

        // Shake plus prononce via VisualEffectsManager
        if (VisualEffectsManager.Instance != null)
        {
            VisualEffectsManager.Instance.TriggerScreenShake(0.4f, 0.15f);
        }
    }

    #endregion

    #region Private Methods

    private void CacheRenderers()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _originalColors = new Color[_renderers.Length];

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i].material.HasProperty("_Color"))
            {
                _originalColors[i] = _renderers[i].material.color;
            }
        }
    }

    private System.Collections.IEnumerator DamageFlashCoroutine()
    {
        // Appliquer la couleur de flash
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                var mat = _renderers[i].material;
                // URP uses _BaseColor, fallback to _Color for legacy
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", _damageFlashColor);
                else if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", _damageFlashColor);
            }
        }

        yield return new WaitForSeconds(_damageFlashDuration);

        // Restaurer les couleurs originales
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                var mat = _renderers[i].material;
                // URP uses _BaseColor, fallback to _Color for legacy
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", _originalColors[i]);
                else if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", _originalColors[i]);
            }
        }
    }

    private void SpawnVFX(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return;

        var vfx = Instantiate(prefab, position, rotation);

        // Auto-destruction si pas de particle system
        var ps = vfx.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            Destroy(vfx, ps.main.duration + ps.main.startLifetime.constantMax);
        }
        else
        {
            Destroy(vfx, 2f);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || _audioSource == null) return;
        _audioSource.PlayOneShot(clip);
    }

    private void PlayRandomSound(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;
        var clip = clips[Random.Range(0, clips.Length)];
        PlaySound(clip);
    }

    #endregion
}
