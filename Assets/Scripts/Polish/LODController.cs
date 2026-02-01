using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Controleur de LOD (Level of Detail) dynamique.
/// Gere les niveaux de detail des objets en fonction de la distance a la camera.
/// </summary>
public class LODController : MonoBehaviour
{
    #region Singleton

    private static LODController _instance;
    public static LODController Instance
    {
        get => _instance;
        private set => _instance = value;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _managedObjects = new Dictionary<int, LODManagedObject>();
    }

    #endregion

    #region Events

    /// <summary>Declenche quand un objet change de LOD.</summary>
    public event Action<GameObject, int, int> OnLODChanged; // obj, oldLOD, newLOD

    #endregion

    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private float _updateInterval = 0.2f;
    [SerializeField] private int _objectsPerFrame = 30;
    [SerializeField] private float _lodBias = 1f;

    [Header("Distance Thresholds")]
    [SerializeField] private float _lod0Distance = 20f;
    [SerializeField] private float _lod1Distance = 40f;
    [SerializeField] private float _lod2Distance = 80f;
    [SerializeField] private float _lod3Distance = 150f;
    [SerializeField] private float _cullDistance = 250f;

    [Header("Transition")]
    [SerializeField] private bool _smoothTransition = false;
    [SerializeField] private float _transitionSpeed = 2f;

    [Header("Shadow LOD")]
    [SerializeField] private bool _shadowLOD = true;
    [SerializeField] private float _shadowCullDistance = 100f;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;

    #endregion

    #region Private Fields

    private Dictionary<int, LODManagedObject> _managedObjects;
    private Camera _mainCamera;
    private float _updateTimer;
    private int _currentIndex;

    // Stats
    private int[] _lodCounts = new int[5];

    #endregion

    #region Properties

    /// <summary>Biais de LOD (plus bas = plus agressif).</summary>
    public float LODBias
    {
        get => _lodBias;
        set => _lodBias = Mathf.Clamp(value, 0.25f, 2f);
    }

    /// <summary>Nombre d'objets par niveau de LOD.</summary>
    public int[] LODCounts => _lodCounts;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        _updateTimer += Time.deltaTime;
        if (_updateTimer >= _updateInterval)
        {
            _updateTimer = 0f;
            UpdateLODs();
        }
    }

    #endregion

    #region Public Methods - Registration

    /// <summary>
    /// Enregistre un objet pour la gestion LOD automatique.
    /// </summary>
    public void RegisterObject(GameObject obj, LODLevelConfig[] levels)
    {
        if (obj == null) return;

        int id = obj.GetInstanceID();
        if (_managedObjects.ContainsKey(id)) return;

        var managed = new LODManagedObject
        {
            gameObject = obj,
            transform = obj.transform,
            levels = levels,
            currentLevel = 0,
            renderers = obj.GetComponentsInChildren<Renderer>()
        };

        _managedObjects[id] = managed;

        // Appliquer le LOD initial
        ApplyLODLevel(managed, 0);
    }

    /// <summary>
    /// Enregistre un objet avec les renderers LOD standard.
    /// </summary>
    public void RegisterWithAutoDetect(GameObject obj)
    {
        if (obj == null) return;

        var lodGroup = obj.GetComponent<LODGroup>();
        if (lodGroup != null)
        {
            // L'objet a deja un LODGroup, pas besoin de le gerer
            return;
        }

        // Creer une configuration simple basee sur les renderers enfants
        var renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        var levels = new LODLevelConfig[]
        {
            new LODLevelConfig { renderers = renderers, shadowMode = ShadowCastingMode.On },
            new LODLevelConfig { renderers = renderers, shadowMode = ShadowCastingMode.Off },
            new LODLevelConfig { renderers = null, shadowMode = ShadowCastingMode.Off } // Cull
        };

        RegisterObject(obj, levels);
    }

    /// <summary>
    /// Retire un objet de la gestion LOD.
    /// </summary>
    public void UnregisterObject(GameObject obj)
    {
        if (obj == null) return;

        int id = obj.GetInstanceID();
        if (_managedObjects.ContainsKey(id))
        {
            // Restaurer a LOD0 avant de retirer
            ApplyLODLevel(_managedObjects[id], 0);
            _managedObjects.Remove(id);
        }
    }

    #endregion

    #region Public Methods - Configuration

    /// <summary>
    /// Definit les distances de LOD.
    /// </summary>
    public void SetLODDistances(float lod0, float lod1, float lod2, float lod3, float cull)
    {
        _lod0Distance = Mathf.Max(5f, lod0);
        _lod1Distance = Mathf.Max(_lod0Distance + 5f, lod1);
        _lod2Distance = Mathf.Max(_lod1Distance + 5f, lod2);
        _lod3Distance = Mathf.Max(_lod2Distance + 5f, lod3);
        _cullDistance = Mathf.Max(_lod3Distance + 10f, cull);
    }

    /// <summary>
    /// Applique un profil de qualite.
    /// </summary>
    public void ApplyQualityProfile(LODQualityProfile profile)
    {
        switch (profile)
        {
            case LODQualityProfile.Low:
                _lodBias = 0.5f;
                SetLODDistances(10f, 20f, 40f, 80f, 150f);
                break;

            case LODQualityProfile.Medium:
                _lodBias = 0.75f;
                SetLODDistances(20f, 40f, 80f, 150f, 250f);
                break;

            case LODQualityProfile.High:
                _lodBias = 1f;
                SetLODDistances(30f, 60f, 120f, 200f, 350f);
                break;

            case LODQualityProfile.Ultra:
                _lodBias = 1.5f;
                SetLODDistances(50f, 100f, 200f, 350f, 500f);
                break;
        }
    }

    /// <summary>
    /// Force la mise a jour de tous les LODs.
    /// </summary>
    public void ForceUpdateAll()
    {
        if (_mainCamera == null) return;

        Vector3 cameraPos = _mainCamera.transform.position;

        foreach (var kvp in _managedObjects)
        {
            UpdateObjectLOD(kvp.Value, cameraPos);
        }
    }

    #endregion

    #region Public Methods - Queries

    /// <summary>
    /// Obtient le niveau LOD actuel d'un objet.
    /// </summary>
    public int GetCurrentLODLevel(GameObject obj)
    {
        if (obj == null) return -1;

        int id = obj.GetInstanceID();
        if (_managedObjects.TryGetValue(id, out var managed))
        {
            return managed.currentLevel;
        }
        return -1;
    }

    /// <summary>
    /// Calcule le niveau LOD pour une distance.
    /// </summary>
    public int CalculateLODLevel(float distance)
    {
        float adjustedDistance = distance / _lodBias;

        if (adjustedDistance < _lod0Distance) return 0;
        if (adjustedDistance < _lod1Distance) return 1;
        if (adjustedDistance < _lod2Distance) return 2;
        if (adjustedDistance < _lod3Distance) return 3;
        if (adjustedDistance < _cullDistance) return 4;
        return 5; // Cull
    }

    #endregion

    #region Private Methods

    private void UpdateLODs()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        Vector3 cameraPos = _mainCamera.transform.position;

        // Reset stats
        Array.Clear(_lodCounts, 0, _lodCounts.Length);

        // Traiter un lot d'objets
        var keys = new List<int>(_managedObjects.Keys);
        int count = Mathf.Min(_objectsPerFrame, keys.Count);

        for (int i = 0; i < count; i++)
        {
            _currentIndex = (_currentIndex + 1) % keys.Count;
            int id = keys[_currentIndex];

            if (!_managedObjects.TryGetValue(id, out var managed)) continue;
            if (managed.gameObject == null)
            {
                _managedObjects.Remove(id);
                continue;
            }

            UpdateObjectLOD(managed, cameraPos);
        }

        // Compter tous les objets pour les stats
        foreach (var kvp in _managedObjects)
        {
            int level = Mathf.Clamp(kvp.Value.currentLevel, 0, _lodCounts.Length - 1);
            _lodCounts[level]++;
        }
    }

    private void UpdateObjectLOD(LODManagedObject managed, Vector3 cameraPos)
    {
        float distance = Vector3.Distance(managed.transform.position, cameraPos);
        int newLevel = CalculateLODLevel(distance);

        if (newLevel != managed.currentLevel)
        {
            int oldLevel = managed.currentLevel;
            ApplyLODLevel(managed, newLevel);
            OnLODChanged?.Invoke(managed.gameObject, oldLevel, newLevel);
        }

        // Shadow LOD
        if (_shadowLOD)
        {
            UpdateShadowLOD(managed, distance);
        }
    }

    private void ApplyLODLevel(LODManagedObject managed, int level)
    {
        managed.currentLevel = level;

        if (managed.levels == null || managed.levels.Length == 0)
        {
            // Gestion simple si pas de niveaux definis
            ApplySimpleLOD(managed, level);
            return;
        }

        // Desactiver tous les niveaux
        foreach (var lodLevel in managed.levels)
        {
            if (lodLevel.renderers != null)
            {
                foreach (var renderer in lodLevel.renderers)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = false;
                    }
                }
            }
        }

        // Activer le niveau voulu
        if (level < managed.levels.Length && managed.levels[level].renderers != null)
        {
            foreach (var renderer in managed.levels[level].renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                    renderer.shadowCastingMode = managed.levels[level].shadowMode;
                }
            }
        }
    }

    private void ApplySimpleLOD(LODManagedObject managed, int level)
    {
        if (managed.renderers == null) return;

        bool visible = level < 5; // 5 = cull
        var shadowMode = level < 2 ? ShadowCastingMode.On : ShadowCastingMode.Off;

        foreach (var renderer in managed.renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
                if (visible)
                {
                    renderer.shadowCastingMode = shadowMode;
                }
            }
        }
    }

    private void UpdateShadowLOD(LODManagedObject managed, float distance)
    {
        if (managed.renderers == null) return;

        bool castShadows = distance < _shadowCullDistance;

        foreach (var renderer in managed.renderers)
        {
            if (renderer != null && renderer.enabled)
            {
                renderer.shadowCastingMode = castShadows
                    ? ShadowCastingMode.On
                    : ShadowCastingMode.Off;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!_debugMode) return;

        Vector3 pos = transform.position;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(pos, _lod0Distance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pos, _lod1Distance);

        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(pos, _lod2Distance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pos, _lod3Distance);

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(pos, _cullDistance);
    }

    #endregion
}

/// <summary>
/// Objet gere par le LODController.
/// </summary>
public class LODManagedObject
{
    public GameObject gameObject;
    public Transform transform;
    public LODLevelConfig[] levels;
    public Renderer[] renderers;
    public int currentLevel;
}

/// <summary>
/// Configuration d'un niveau LOD.
/// </summary>
[Serializable]
public class LODLevelConfig
{
    public Renderer[] renderers;
    public ShadowCastingMode shadowMode;
    public Material overrideMaterial;
}

/// <summary>
/// Profils de qualite LOD.
/// </summary>
public enum LODQualityProfile
{
    Low,
    Medium,
    High,
    Ultra
}
