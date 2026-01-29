using System;
using UnityEngine;
using Cinemachine;

/// <summary>
/// Contrôleur de caméra third-person utilisant Cinemachine.
/// Gère le suivi du joueur, les collisions, le lock-on et le camera shake.
/// </summary>
public class CameraController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Références")]
    [SerializeField] private CinemachineFreeLook _freeLookCamera;
    [SerializeField] private CinemachineVirtualCamera _lockOnCamera;
    [SerializeField] private Transform _playerTransform;
    [SerializeField] private CameraSettings _settings;

    [Header("Lock-On")]
    [SerializeField] private LayerMask _lockOnTargetLayers;

    #endregion

    #region Private Fields

    private CinemachineImpulseSource _impulseSource;
    private Transform _currentLockTarget;
    private bool _isLockedOn;
    private CameraMode _currentMode = CameraMode.Exploration;
    private float _currentYaw;
    private float _currentPitch;

    #endregion

    #region Events

    /// <summary>
    /// Déclenché quand le mode de caméra change.
    /// </summary>
    public event Action<CameraMode> OnCameraModeChanged;

    /// <summary>
    /// Déclenché quand une cible est verrouillée.
    /// </summary>
    public event Action<Transform> OnTargetLocked;

    /// <summary>
    /// Déclenché quand le lock-on est désactivé.
    /// </summary>
    public event Action OnTargetUnlocked;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        // Créer les paramètres par défaut si non assignés
        if (_settings == null)
        {
            _settings = ScriptableObject.CreateInstance<CameraSettings>();
        }

        // Obtenir ou créer l'impulse source
        _impulseSource = GetComponent<CinemachineImpulseSource>();
        if (_impulseSource == null)
        {
            _impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
        }

        // Trouver le joueur si non assigné
        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerTransform = player.transform;
            }
        }

        SetupCameras();
    }

    private void Start()
    {
        ApplySettings();
    }

    private void LateUpdate()
    {
        if (_isLockedOn && _currentLockTarget != null)
        {
            UpdateLockOnRotation();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Change le mode de caméra.
    /// </summary>
    public void SetCameraMode(CameraMode mode)
    {
        if (_currentMode == mode) return;

        _currentMode = mode;
        ApplyCameraMode(mode);
        OnCameraModeChanged?.Invoke(mode);
    }

    /// <summary>
    /// Active le lock-on sur la cible la plus proche.
    /// </summary>
    /// <returns>True si une cible a été trouvée</returns>
    public bool TryLockOn()
    {
        if (_isLockedOn)
        {
            // Désactiver le lock-on
            UnlockTarget();
            return false;
        }

        // Chercher la cible la plus proche
        Transform target = FindBestLockOnTarget();
        if (target != null)
        {
            LockOnTarget(target);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Change la cible de lock-on.
    /// </summary>
    /// <param name="direction">-1 pour gauche, 1 pour droite</param>
    public void SwitchLockOnTarget(int direction)
    {
        if (!_isLockedOn) return;

        Transform newTarget = FindNextLockOnTarget(direction);
        if (newTarget != null && newTarget != _currentLockTarget)
        {
            LockOnTarget(newTarget);
        }
    }

    /// <summary>
    /// Déclenche un effet de camera shake.
    /// </summary>
    public void TriggerShake(float intensity = -1f, float duration = -1f)
    {
        if (_impulseSource == null) return;

        float shakeIntensity = intensity > 0 ? intensity : _settings.defaultShakeIntensity;
        float shakeDuration = duration > 0 ? duration : _settings.defaultShakeDuration;

        _impulseSource.m_ImpulseDefinition.m_TimeEnvelope.m_SustainTime = shakeDuration;
        _impulseSource.GenerateImpulse(shakeIntensity);
    }

    /// <summary>
    /// Applique les paramètres de sensibilité.
    /// </summary>
    public void UpdateSensitivity(float horizontal, float vertical)
    {
        Settings.horizontalSensitivity = Mathf.Clamp(horizontal, 0.1f, 10f);
        Settings.verticalSensitivity = Mathf.Clamp(vertical, 0.1f, 10f);
        ApplySettings();
    }

    /// <summary>
    /// Inverse l'axe Y.
    /// </summary>
    public void SetInvertY(bool invert)
    {
        Settings.invertY = invert;
        ApplySettings();
    }

    #endregion

    #region Private Methods

    private void SetupCameras()
    {
        // Setup FreeLook camera si elle existe
        if (_freeLookCamera != null && _playerTransform != null)
        {
            _freeLookCamera.Follow = _playerTransform;
            _freeLookCamera.LookAt = _playerTransform;
        }

        // Setup Lock-On camera si elle existe
        if (_lockOnCamera != null && _playerTransform != null)
        {
            _lockOnCamera.Follow = _playerTransform;
            _lockOnCamera.Priority = 0; // Désactivée par défaut
        }
    }

    private void ApplySettings()
    {
        if (_freeLookCamera == null) return;

        // Appliquer les paramètres de sensibilité
        _freeLookCamera.m_XAxis.m_MaxSpeed = _settings.horizontalSensitivity * 100f;
        _freeLookCamera.m_YAxis.m_MaxSpeed = _settings.verticalSensitivity;

        if (_settings.invertY)
        {
            _freeLookCamera.m_YAxis.m_InvertInput = true;
        }

        // Appliquer les distances selon le mode
        ApplyCameraMode(_currentMode);
    }

    private void ApplyCameraMode(CameraMode mode)
    {
        if (_freeLookCamera == null) return;

        float distance = mode == CameraMode.Combat
            ? _settings.combatDistance
            : _settings.explorationDistance;

        // Appliquer la distance aux trois rigs
        for (int i = 0; i < 3; i++)
        {
            var orbit = _freeLookCamera.m_Orbits[i];
            float heightMultiplier = i == 0 ? 1.5f : (i == 1 ? 1f : 0.5f);
            orbit.m_Height = _settings.cameraHeight * heightMultiplier;
            orbit.m_Radius = distance * (i == 1 ? 1f : 0.8f);
            _freeLookCamera.m_Orbits[i] = orbit;
        }
    }

    private Transform FindBestLockOnTarget()
    {
        if (_playerTransform == null) return null;

        Collider[] colliders = Physics.OverlapSphere(
            _playerTransform.position,
            _settings.lockOnMaxDistance,
            _lockOnTargetLayers
        );

        Transform bestTarget = null;
        float bestScore = float.MaxValue;

        foreach (var collider in colliders)
        {
            if (collider.transform == _playerTransform) continue;

            // Vérifier si la cible est dans l'angle de détection
            Vector3 directionToTarget = collider.transform.position - _playerTransform.position;
            float angle = Vector3.Angle(Camera.main.transform.forward, directionToTarget);

            if (angle > _settings.lockOnAngle) continue;

            // Le score est basé sur l'angle et la distance
            float distance = directionToTarget.magnitude;
            float score = angle + (distance * 0.5f);

            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = collider.transform;
            }
        }

        return bestTarget;
    }

    private Transform FindNextLockOnTarget(int direction)
    {
        if (_playerTransform == null || _currentLockTarget == null) return null;

        Collider[] colliders = Physics.OverlapSphere(
            _playerTransform.position,
            _settings.lockOnMaxDistance,
            _lockOnTargetLayers
        );

        Transform bestTarget = null;
        float bestAngle = direction > 0 ? float.MaxValue : float.MinValue;

        Vector3 currentDir = (_currentLockTarget.position - _playerTransform.position).normalized;
        Vector3 right = Camera.main.transform.right;

        foreach (var collider in colliders)
        {
            if (collider.transform == _playerTransform) continue;
            if (collider.transform == _currentLockTarget) continue;

            Vector3 targetDir = (collider.transform.position - _playerTransform.position).normalized;
            float signedAngle = Vector3.SignedAngle(currentDir, targetDir, Vector3.up);

            if (direction > 0)
            {
                // Chercher à droite (angle positif le plus petit)
                if (signedAngle > 0 && signedAngle < bestAngle)
                {
                    bestAngle = signedAngle;
                    bestTarget = collider.transform;
                }
            }
            else
            {
                // Chercher à gauche (angle négatif le plus grand)
                if (signedAngle < 0 && signedAngle > bestAngle)
                {
                    bestAngle = signedAngle;
                    bestTarget = collider.transform;
                }
            }
        }

        return bestTarget;
    }

    private void LockOnTarget(Transform target)
    {
        _currentLockTarget = target;
        _isLockedOn = true;

        // Activer la caméra de lock-on
        if (_lockOnCamera != null)
        {
            _lockOnCamera.LookAt = target;
            _lockOnCamera.Priority = 20;
        }

        // Désactiver le FreeLook
        if (_freeLookCamera != null)
        {
            _freeLookCamera.Priority = 0;
        }

        SetCameraMode(CameraMode.Combat);
        OnTargetLocked?.Invoke(target);
    }

    private void UnlockTarget()
    {
        _currentLockTarget = null;
        _isLockedOn = false;

        // Désactiver la caméra de lock-on
        if (_lockOnCamera != null)
        {
            _lockOnCamera.Priority = 0;
        }

        // Réactiver le FreeLook
        if (_freeLookCamera != null)
        {
            _freeLookCamera.Priority = 10;
        }

        SetCameraMode(CameraMode.Exploration);
        OnTargetUnlocked?.Invoke();
    }

    private void UpdateLockOnRotation()
    {
        if (_currentLockTarget == null || _playerTransform == null) return;

        // Vérifier si la cible est toujours valide
        float distance = Vector3.Distance(_playerTransform.position, _currentLockTarget.position);
        if (distance > _settings.lockOnMaxDistance * 1.5f)
        {
            UnlockTarget();
            return;
        }

        // Vérifier si la cible est toujours active
        if (!_currentLockTarget.gameObject.activeInHierarchy)
        {
            UnlockTarget();
        }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Mode de caméra actuel.
    /// </summary>
    public CameraMode CurrentMode => _currentMode;

    /// <summary>
    /// True si une cible est verrouillée.
    /// </summary>
    public bool IsLockedOn => _isLockedOn;

    /// <summary>
    /// Cible actuelle du lock-on.
    /// </summary>
    public Transform CurrentLockTarget => _currentLockTarget;

    /// <summary>
    /// Paramètres de la caméra.
    /// </summary>
    public CameraSettings Settings
    {
        get
        {
            if (_settings == null)
            {
                _settings = ScriptableObject.CreateInstance<CameraSettings>();
            }
            return _settings;
        }
    }

    #endregion
}

/// <summary>
/// Modes de caméra disponibles.
/// </summary>
public enum CameraMode
{
    Exploration,
    Combat,
    Cinematic
}
