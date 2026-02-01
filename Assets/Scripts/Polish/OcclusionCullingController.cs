using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controleur d'occlusion culling dynamique.
/// Complete le systeme de culling integre de Unity avec un culling manuel par distance et frustum.
/// </summary>
public class OcclusionCullingController : MonoBehaviour
{
    #region Singleton

    private static OcclusionCullingController _instance;
    public static OcclusionCullingController Instance
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

        _culledObjects = new Dictionary<int, CullableObject>();
        _visibleObjects = new HashSet<int>();
        _occluders = new List<OccluderData>();
    }

    #endregion

    #region Events

    /// <summary>Declenche quand un objet est cull.</summary>
    public event Action<GameObject> OnObjectCulled;

    /// <summary>Declenche quand un objet devient visible.</summary>
    public event Action<GameObject> OnObjectBecameVisible;

    #endregion

    #region Serialized Fields

    [Header("Configuration")]
    [SerializeField] private float _maxCullDistance = 200f;
    [SerializeField] private float _updateInterval = 0.1f;
    [SerializeField] private int _objectsPerFrame = 50;
    [SerializeField] private bool _useDistanceCulling = true;
    [SerializeField] private bool _useFrustumCulling = true;

    [Header("Distance Tiers")]
    [SerializeField] private float _nearDistance = 50f;
    [SerializeField] private float _mediumDistance = 100f;
    [SerializeField] private float _farDistance = 150f;

    [Header("Categories")]
    [SerializeField] private CullingCategory[] _categories;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;

    #endregion

    #region Private Fields

    private Dictionary<int, CullableObject> _culledObjects;
    private HashSet<int> _visibleObjects;
    private List<OccluderData> _occluders;
    private Camera _mainCamera;
    private Plane[] _frustumPlanes;
    private float _updateTimer;
    private int _currentIndex;

    // Stats
    private int _visibleCount;
    private int _culledCount;

    #endregion

    #region Properties

    /// <summary>Nombre d'objets visibles.</summary>
    public int VisibleCount => _visibleCount;

    /// <summary>Nombre d'objets culles.</summary>
    public int CulledCount => _culledCount;

    /// <summary>Distance maximale de culling.</summary>
    public float MaxCullDistance => _maxCullDistance;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Debug.LogWarning("[OcclusionCullingController] Main camera not found!");
        }
    }

    private void Update()
    {
        _updateTimer += Time.deltaTime;
        if (_updateTimer >= _updateInterval)
        {
            _updateTimer = 0f;
            UpdateCulling();
        }
    }

    private void OnDestroy()
    {
        _culledObjects?.Clear();
        _visibleObjects?.Clear();
    }

    #endregion

    #region Public Methods - Registration

    /// <summary>
    /// Enregistre un objet pour le culling.
    /// </summary>
    public void RegisterObject(GameObject obj, CullingPriority priority = CullingPriority.Medium)
    {
        if (obj == null) return;

        int id = obj.GetInstanceID();
        if (_culledObjects.ContainsKey(id)) return;

        var cullable = new CullableObject
        {
            gameObject = obj,
            transform = obj.transform,
            renderers = obj.GetComponentsInChildren<Renderer>(),
            priority = priority,
            bounds = CalculateBounds(obj),
            isVisible = true
        };

        _culledObjects[id] = cullable;
        _visibleObjects.Add(id);
    }

    /// <summary>
    /// Retire un objet du culling.
    /// </summary>
    public void UnregisterObject(GameObject obj)
    {
        if (obj == null) return;

        int id = obj.GetInstanceID();
        if (_culledObjects.ContainsKey(id))
        {
            // S'assurer que l'objet est visible avant de le retirer
            SetObjectVisibility(_culledObjects[id], true);
            _culledObjects.Remove(id);
        }
        _visibleObjects.Remove(id);
    }

    /// <summary>
    /// Enregistre un occluder (objet qui bloque la vue).
    /// </summary>
    public void RegisterOccluder(GameObject obj, float occlusionRadius)
    {
        if (obj == null) return;

        _occluders.Add(new OccluderData
        {
            transform = obj.transform,
            radius = occlusionRadius
        });
    }

    #endregion

    #region Public Methods - Configuration

    /// <summary>
    /// Configure la distance de culling.
    /// </summary>
    public void SetMaxCullDistance(float distance)
    {
        _maxCullDistance = Mathf.Max(10f, distance);
    }

    /// <summary>
    /// Active/desactive le culling par distance.
    /// </summary>
    public void SetDistanceCulling(bool enabled)
    {
        _useDistanceCulling = enabled;
    }

    /// <summary>
    /// Active/desactive le culling par frustum.
    /// </summary>
    public void SetFrustumCulling(bool enabled)
    {
        _useFrustumCulling = enabled;
    }

    /// <summary>
    /// Force la mise a jour du culling.
    /// </summary>
    public void ForceUpdate()
    {
        UpdateAllObjects();
    }

    #endregion

    #region Public Methods - Queries

    /// <summary>
    /// Verifie si un objet est visible.
    /// </summary>
    public bool IsObjectVisible(GameObject obj)
    {
        if (obj == null) return false;
        return _visibleObjects.Contains(obj.GetInstanceID());
    }

    /// <summary>
    /// Obtient les objets visibles dans un rayon.
    /// </summary>
    public List<GameObject> GetVisibleObjectsInRadius(Vector3 center, float radius)
    {
        var result = new List<GameObject>();
        float sqrRadius = radius * radius;

        foreach (var kvp in _culledObjects)
        {
            if (!kvp.Value.isVisible) continue;

            float sqrDist = (kvp.Value.transform.position - center).sqrMagnitude;
            if (sqrDist <= sqrRadius)
            {
                result.Add(kvp.Value.gameObject);
            }
        }

        return result;
    }

    #endregion

    #region Private Methods

    private void UpdateCulling()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        // Mettre a jour les plans du frustum
        _frustumPlanes = GeometryUtility.CalculateFrustumPlanes(_mainCamera);

        Vector3 cameraPos = _mainCamera.transform.position;

        // Traiter un lot d'objets par frame
        var keys = new List<int>(_culledObjects.Keys);
        int count = Mathf.Min(_objectsPerFrame, keys.Count);

        _visibleCount = 0;
        _culledCount = 0;

        for (int i = 0; i < count; i++)
        {
            _currentIndex = (_currentIndex + 1) % keys.Count;
            int id = keys[_currentIndex];

            if (!_culledObjects.TryGetValue(id, out var cullable)) continue;
            if (cullable.gameObject == null)
            {
                _culledObjects.Remove(id);
                _visibleObjects.Remove(id);
                continue;
            }

            bool shouldBeVisible = ShouldBeVisible(cullable, cameraPos);

            if (shouldBeVisible != cullable.isVisible)
            {
                SetObjectVisibility(cullable, shouldBeVisible);
            }

            if (cullable.isVisible) _visibleCount++;
            else _culledCount++;
        }
    }

    private void UpdateAllObjects()
    {
        if (_mainCamera == null) return;

        _frustumPlanes = GeometryUtility.CalculateFrustumPlanes(_mainCamera);
        Vector3 cameraPos = _mainCamera.transform.position;

        foreach (var kvp in _culledObjects)
        {
            var cullable = kvp.Value;
            if (cullable.gameObject == null) continue;

            bool shouldBeVisible = ShouldBeVisible(cullable, cameraPos);
            if (shouldBeVisible != cullable.isVisible)
            {
                SetObjectVisibility(cullable, shouldBeVisible);
            }
        }
    }

    private bool ShouldBeVisible(CullableObject cullable, Vector3 cameraPos)
    {
        if (cullable.transform == null) return false;

        Vector3 objPos = cullable.transform.position;

        // Distance culling
        if (_useDistanceCulling)
        {
            float sqrDist = (objPos - cameraPos).sqrMagnitude;
            float maxDist = GetCullDistanceForPriority(cullable.priority);

            if (sqrDist > maxDist * maxDist)
            {
                return false;
            }
        }

        // Frustum culling
        if (_useFrustumCulling)
        {
            // Recalculer les bounds si necessaire
            if (cullable.bounds.size == Vector3.zero)
            {
                cullable.bounds = CalculateBounds(cullable.gameObject);
            }

            Bounds worldBounds = cullable.bounds;
            worldBounds.center = objPos;

            if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, worldBounds))
            {
                return false;
            }
        }

        return true;
    }

    private void SetObjectVisibility(CullableObject cullable, bool visible)
    {
        cullable.isVisible = visible;

        if (cullable.renderers != null)
        {
            foreach (var renderer in cullable.renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }

        int id = cullable.gameObject.GetInstanceID();
        if (visible)
        {
            _visibleObjects.Add(id);
            OnObjectBecameVisible?.Invoke(cullable.gameObject);
        }
        else
        {
            _visibleObjects.Remove(id);
            OnObjectCulled?.Invoke(cullable.gameObject);
        }
    }

    private float GetCullDistanceForPriority(CullingPriority priority)
    {
        switch (priority)
        {
            case CullingPriority.Critical:
                return _maxCullDistance * 2f; // Jamais cull, distance tres elevee
            case CullingPriority.High:
                return _maxCullDistance;
            case CullingPriority.Medium:
                return _mediumDistance;
            case CullingPriority.Low:
                return _nearDistance;
            case CullingPriority.VeryLow:
                return _nearDistance * 0.5f;
            default:
                return _mediumDistance;
        }
    }

    private Bounds CalculateBounds(GameObject obj)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(obj.transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        // Convertir en bounds locales
        return new Bounds(Vector3.zero, bounds.size);
    }

    private void OnDrawGizmosSelected()
    {
        if (!_debugMode) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _nearDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _mediumDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _maxCullDistance);
    }

    #endregion
}

/// <summary>
/// Donnees d'un objet cullable.
/// </summary>
public class CullableObject
{
    public GameObject gameObject;
    public Transform transform;
    public Renderer[] renderers;
    public CullingPriority priority;
    public Bounds bounds;
    public bool isVisible;
}

/// <summary>
/// Donnees d'un occluder.
/// </summary>
public class OccluderData
{
    public Transform transform;
    public float radius;
}

/// <summary>
/// Priorite de culling.
/// </summary>
public enum CullingPriority
{
    /// <summary>Jamais cull (joueur, boss, etc.).</summary>
    Critical,

    /// <summary>Distance maximale.</summary>
    High,

    /// <summary>Distance moyenne.</summary>
    Medium,

    /// <summary>Distance courte.</summary>
    Low,

    /// <summary>Tres courte distance (details).</summary>
    VeryLow
}

/// <summary>
/// Categorie de culling avec settings personnalises.
/// </summary>
[Serializable]
public class CullingCategory
{
    public string categoryName;
    public CullingPriority priority;
    public float customDistance;
    public bool useShadowCulling;
    public float shadowCullDistance;
}
