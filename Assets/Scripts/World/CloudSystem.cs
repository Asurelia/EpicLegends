using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Systeme de nuages proceduraux.
/// Gere la generation et le mouvement des nuages.
/// </summary>
public class CloudSystem : MonoBehaviour
{
    #region Serialized Fields

    [Header("Cloud Settings")]
    [SerializeField] private int _cloudCount = 30;
    [SerializeField] private float _cloudHeight = 150f;
    [SerializeField] private float _worldSize = 512f;
    [SerializeField] private float _cloudSpread = 1.2f;

    [Header("Cloud Appearance")]
    [SerializeField] private Vector2 _cloudSizeMin = new Vector2(20f, 10f);
    [SerializeField] private Vector2 _cloudSizeMax = new Vector2(60f, 30f);
    [SerializeField] private float _cloudOpacityMin = 0.3f;
    [SerializeField] private float _cloudOpacityMax = 0.8f;

    [Header("Movement")]
    [SerializeField] private Vector2 _windDirection = new Vector2(1f, 0.3f);
    [SerializeField] private float _windSpeed = 5f;
    [SerializeField] private float _windVariation = 0.2f;

    [Header("Performance")]
    [SerializeField] private bool _useBillboards = true;
    [SerializeField] private float _fadeDistance = 500f;
    [SerializeField] private int _cloudLayers = 2;

    [Header("Day/Night")]
    [SerializeField] private Color _dayColor = Color.white;
    [SerializeField] private Color _sunsetColor = new Color(1f, 0.8f, 0.6f);
    [SerializeField] private Color _nightColor = new Color(0.3f, 0.3f, 0.4f);

    #endregion

    #region Private Fields

    private Material _cloudMaterial;
    private List<CloudData> _clouds;
    private Transform _cameraTransform;
    private float _timeOfDay = 0.5f;

    #endregion

    #region Initialization

    /// <summary>
    /// Initialise le systeme de nuages.
    /// </summary>
    public void Initialize(float worldSize, float cloudHeight, Material cloudMaterial)
    {
        _worldSize = worldSize;
        _cloudHeight = cloudHeight;
        _cloudMaterial = cloudMaterial;

        _clouds = new List<CloudData>();

        // Log material info for debugging
        if (_cloudMaterial != null)
        {
            Debug.Log($"[CloudSystem] Using cloud material with shader: {_cloudMaterial.shader.name}");
            if (_cloudMaterial.shader.name.Contains("pink", System.StringComparison.OrdinalIgnoreCase) ||
                _cloudMaterial.shader.name.Contains("error", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[CloudSystem] Material shader appears invalid (pink shader detected). Clouds may render incorrectly.");
            }
        }
        else
        {
            Debug.LogWarning("[CloudSystem] No cloud material provided, will use URP fallback shader.");
        }

        GenerateClouds();

        Debug.Log($"[CloudSystem] Initialized with {_cloudCount} clouds");
    }

    private void GenerateClouds()
    {
        for (int layer = 0; layer < _cloudLayers; layer++)
        {
            float layerHeight = _cloudHeight + layer * 20f;
            int cloudsPerLayer = _cloudCount / _cloudLayers;

            for (int i = 0; i < cloudsPerLayer; i++)
            {
                CreateCloud(layer, layerHeight);
            }
        }
    }

    private void CreateCloud(int layer, float height)
    {
        // Random position within world bounds
        float spreadSize = _worldSize * _cloudSpread;
        float x = Random.Range(-spreadSize / 2f, spreadSize / 2f) + _worldSize / 2f;
        float z = Random.Range(-spreadSize / 2f, spreadSize / 2f) + _worldSize / 2f;
        float y = height + Random.Range(-10f, 10f);

        Vector3 position = new Vector3(x, y, z);

        // Random size
        float width = Random.Range(_cloudSizeMin.x, _cloudSizeMax.x);
        float heightSize = Random.Range(_cloudSizeMin.y, _cloudSizeMax.y);
        Vector3 scale = new Vector3(width, heightSize, width * 0.6f);

        // Random opacity
        float opacity = Random.Range(_cloudOpacityMin, _cloudOpacityMax);

        // Create cloud GameObject
        GameObject cloudObj;

        if (_useBillboards)
        {
            cloudObj = CreateBillboardCloud(position, scale, opacity);
        }
        else
        {
            cloudObj = CreateVolumetricCloud(position, scale, opacity);
        }

        cloudObj.transform.SetParent(transform);

        // Add to tracking list
        _clouds.Add(new CloudData
        {
            GameObject = cloudObj,
            BasePosition = position,
            Size = scale,
            Opacity = opacity,
            Layer = layer,
            WindOffset = Random.Range(-_windVariation, _windVariation),
            Phase = Random.Range(0f, Mathf.PI * 2f)
        });
    }

    private GameObject CreateBillboardCloud(Vector3 position, Vector3 scale, float opacity)
    {
        GameObject cloud = GameObject.CreatePrimitive(PrimitiveType.Quad);
        cloud.name = "Cloud";

        // Remove collider
        var collider = cloud.GetComponent<Collider>();
        if (collider != null) Destroy(collider); // CRITICAL FIX: Use Destroy() in runtime, not DestroyImmediate()

        cloud.transform.position = position;
        cloud.transform.localScale = scale;
        cloud.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // Apply material
        var renderer = cloud.GetComponent<Renderer>();
        if (_cloudMaterial != null)
        {
            renderer.material = new Material(_cloudMaterial);
            SetupTransparentMaterial(renderer.material, opacity);
        }
        else
        {
            // Fallback: Create URP transparent material
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null)
            {
                Debug.LogError("[CloudSystem] URP Lit shader not found! Clouds will be pink.");
                urpShader = Shader.Find("Standard");
            }

            if (urpShader != null)
            {
                renderer.material = new Material(urpShader);
                SetupTransparentMaterial(renderer.material, opacity);
            }
            else
            {
                Debug.LogError("[CloudSystem] No compatible shader found for clouds!");
            }
        }

        // Disable shadows
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return cloud;
    }

    private GameObject CreateVolumetricCloud(Vector3 position, Vector3 scale, float opacity)
    {
        // Create cloud from multiple layered quads
        GameObject cloudRoot = new GameObject("VolumetricCloud");
        cloudRoot.transform.position = position;

        int layers = 5;
        for (int i = 0; i < layers; i++)
        {
            GameObject layer = GameObject.CreatePrimitive(PrimitiveType.Quad);
            layer.name = $"CloudLayer_{i}";

            var collider = layer.GetComponent<Collider>();
            if (collider != null) Destroy(collider); // CRITICAL FIX: Use Destroy() in runtime, not DestroyImmediate()

            layer.transform.SetParent(cloudRoot.transform);
            layer.transform.localPosition = Vector3.up * (i - layers / 2) * 2f;
            layer.transform.localScale = scale * (1f - i * 0.1f);
            layer.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var renderer = layer.GetComponent<Renderer>();
            if (_cloudMaterial != null)
            {
                renderer.material = new Material(_cloudMaterial);
                float layerOpacity = opacity * (1f - (float)i / layers * 0.5f);
                SetupTransparentMaterial(renderer.material, layerOpacity);
            }
            else
            {
                // Fallback for volumetric clouds
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                if (urpShader != null)
                {
                    renderer.material = new Material(urpShader);
                    float layerOpacity = opacity * (1f - (float)i / layers * 0.5f);
                    SetupTransparentMaterial(renderer.material, layerOpacity);
                }
            }

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        return cloudRoot;
    }

    /// <summary>
    /// Configure un material pour la transparence URP.
    /// </summary>
    private void SetupTransparentMaterial(Material mat, float opacity)
    {
        if (mat == null) return;

        // Configure URP transparent mode
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1); // 0 = Opaque, 1 = Transparent
        }

        if (mat.HasProperty("_Blend"))
        {
            mat.SetFloat("_Blend", 0); // 0 = Alpha, 1 = Premultiply, 2 = Additive, 3 = Multiply
        }

        // Enable transparency keywords
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");

        // Set render queue for transparency
        mat.renderQueue = 3000; // Transparent queue

        // Set color with alpha
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, opacity));
        }
        else if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", new Color(1f, 1f, 1f, opacity));
        }
        else
        {
            mat.color = new Color(1f, 1f, 1f, opacity);
        }

        Debug.Log($"[CloudSystem] Configured material '{mat.shader.name}' for transparency (opacity: {opacity})");
    }

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        _cameraTransform = Camera.main?.transform;
    }

    private void Update()
    {
        UpdateCloudPositions();
        UpdateCloudColors();

        // CRITICAL FIX: Re-cache camera if it became null (e.g., scene change)
        if (_cameraTransform == null)
        {
            _cameraTransform = Camera.main?.transform;
        }

        if (_useBillboards && _cameraTransform != null)
        {
            UpdateBillboardOrientations();
        }
    }

    #endregion

    #region Cloud Updates

    private void UpdateCloudPositions()
    {
        float time = Time.time;

        foreach (var cloud in _clouds)
        {
            if (cloud.GameObject == null) continue;

            // Calculate wind movement
            float windX = _windDirection.x * (_windSpeed + cloud.WindOffset);
            float windZ = _windDirection.y * (_windSpeed + cloud.WindOffset);

            // Add some oscillation
            float oscillation = Mathf.Sin(time * 0.5f + cloud.Phase) * 2f;

            // New position
            Vector3 newPos = cloud.BasePosition;
            newPos.x += (time * windX) % (_worldSize * _cloudSpread);
            newPos.z += (time * windZ) % (_worldSize * _cloudSpread);
            newPos.y += oscillation;

            // Wrap around world
            float halfWorld = _worldSize / 2f;
            float spreadHalf = _worldSize * _cloudSpread / 2f;

            if (newPos.x > halfWorld + spreadHalf)
            {
                newPos.x -= _worldSize * _cloudSpread;
                cloud.BasePosition.x -= _worldSize * _cloudSpread;
            }
            if (newPos.z > halfWorld + spreadHalf)
            {
                newPos.z -= _worldSize * _cloudSpread;
                cloud.BasePosition.z -= _worldSize * _cloudSpread;
            }

            cloud.GameObject.transform.position = newPos;
        }
    }

    private void UpdateCloudColors()
    {
        Color currentColor = GetCurrentCloudColor();

        foreach (var cloud in _clouds)
        {
            if (cloud.GameObject == null) continue;

            var renderers = cloud.GameObject.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.material != null)
                {
                    Color c = currentColor;
                    c.a = cloud.Opacity;

                    // Set color using URP property names with fallback
                    if (renderer.material.HasProperty("_BaseColor"))
                        renderer.material.SetColor("_BaseColor", c);
                    else if (renderer.material.HasProperty("_Color"))
                        renderer.material.SetColor("_Color", c);
                    else
                        renderer.material.color = c;
                }
            }
        }
    }

    private Color GetCurrentCloudColor()
    {
        // 0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset, 1 = midnight
        if (_timeOfDay < 0.2f || _timeOfDay > 0.8f)
        {
            return _nightColor;
        }
        else if (_timeOfDay < 0.3f)
        {
            float t = (_timeOfDay - 0.2f) / 0.1f;
            return Color.Lerp(_nightColor, _sunsetColor, t);
        }
        else if (_timeOfDay < 0.4f)
        {
            float t = (_timeOfDay - 0.3f) / 0.1f;
            return Color.Lerp(_sunsetColor, _dayColor, t);
        }
        else if (_timeOfDay < 0.6f)
        {
            return _dayColor;
        }
        else if (_timeOfDay < 0.7f)
        {
            float t = (_timeOfDay - 0.6f) / 0.1f;
            return Color.Lerp(_dayColor, _sunsetColor, t);
        }
        else
        {
            float t = (_timeOfDay - 0.7f) / 0.1f;
            return Color.Lerp(_sunsetColor, _nightColor, t);
        }
    }

    private void UpdateBillboardOrientations()
    {
        if (_cameraTransform == null) return;

        foreach (var cloud in _clouds)
        {
            if (cloud.GameObject == null) continue;

            // Look at camera but keep horizontal
            Vector3 lookDir = _cameraTransform.position - cloud.GameObject.transform.position;
            lookDir.y = 0;

            if (lookDir != Vector3.zero)
            {
                cloud.GameObject.transform.rotation = Quaternion.LookRotation(lookDir) * Quaternion.Euler(90f, 0f, 0f);
            }
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Definit l'heure du jour (0-1).
    /// </summary>
    public void SetTimeOfDay(float time)
    {
        _timeOfDay = Mathf.Clamp01(time);
    }

    /// <summary>
    /// Definit la direction du vent.
    /// </summary>
    public void SetWindDirection(Vector2 direction)
    {
        _windDirection = direction.normalized;
    }

    /// <summary>
    /// Definit la vitesse du vent.
    /// </summary>
    public void SetWindSpeed(float speed)
    {
        _windSpeed = Mathf.Max(0f, speed);
    }

    /// <summary>
    /// Force le rafraichissement des nuages.
    /// </summary>
    public void RefreshClouds()
    {
        // Clear existing
        foreach (var cloud in _clouds)
        {
            if (cloud.GameObject != null)
            {
                Destroy(cloud.GameObject);
            }
        }
        _clouds.Clear();

        // Regenerate
        GenerateClouds();
    }

    /// <summary>
    /// Definit la densite des nuages.
    /// </summary>
    public void SetCloudDensity(float density)
    {
        int newCount = Mathf.RoundToInt(30 * density);
        if (newCount != _cloudCount)
        {
            _cloudCount = newCount;
            RefreshClouds();
        }
    }

    #endregion
}

/// <summary>
/// Donnees d'un nuage.
/// </summary>
public class CloudData
{
    public GameObject GameObject;
    public Vector3 BasePosition;
    public Vector3 Size;
    public float Opacity;
    public int Layer;
    public float WindOffset;
    public float Phase;
}
