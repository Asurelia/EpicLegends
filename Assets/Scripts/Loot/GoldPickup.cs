using UnityEngine;

/// <summary>
/// Pickup d'or dans le monde.
/// </summary>
public class GoldPickup : MonoBehaviour
{
    #region Serialized Fields

    [Header("Gold Settings")]
    [SerializeField] private int _amount = 1;

    [Header("Pickup Settings")]
    [SerializeField] private float _pickupRadius = 2f;
    [SerializeField] private bool _autoPickup = true;
    [SerializeField] private float _autoPickupDelay = 0.3f;
    [SerializeField] private float _magnetSpeed = 10f;

    [Header("Visual")]
    [SerializeField] private float _bobSpeed = 3f;
    [SerializeField] private float _bobHeight = 0.15f;
    [SerializeField] private float _rotateSpeed = 180f;
    [SerializeField] private ParticleSystem _sparkles;

    [Header("Audio")]
    [SerializeField] private AudioClip _pickupSound;

    #endregion

    #region Private Fields

    private float _spawnTime;
    private Vector3 _basePosition;
    private bool _isPickedUp = false;
    private bool _isGrounded = false;
    private bool _isMagneting = false;
    private Transform _magnetTarget;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        _spawnTime = Time.time;
        _basePosition = transform.position;
    }

    private void Update()
    {
        if (_isPickedUp) return;

        // Magnet vers le joueur
        if (_isMagneting && _magnetTarget != null)
        {
            MoveTowardsTarget();
            return;
        }

        // Animation
        if (_isGrounded)
        {
            AnimateGold();
        }

        // Auto-pickup
        if (_autoPickup && Time.time - _spawnTime > _autoPickupDelay)
        {
            CheckAutoPickup();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isPickedUp) return;

        // Detecter le sol
        if (!_isGrounded && !other.CompareTag("Player"))
        {
            _isGrounded = true;
            _basePosition = transform.position;

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }
        }

        // Pickup par le joueur
        if (other.CompareTag("Player"))
        {
            Pickup(other.gameObject);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Initialise la quantite d'or.
    /// </summary>
    public void Initialize(int amount)
    {
        _amount = amount;

        // Ajuster la taille selon la quantite
        float scale = Mathf.Clamp(0.2f + (amount / 100f) * 0.3f, 0.2f, 0.5f);
        transform.localScale = Vector3.one * scale;
    }

    #endregion

    #region Private Methods

    private void AnimateGold()
    {
        // Bob
        float newY = _basePosition.y + Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

        // Rotate
        transform.Rotate(Vector3.up, _rotateSpeed * Time.deltaTime);
    }

    private void CheckAutoPickup()
    {
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.transform.position);

        if (dist <= _pickupRadius)
        {
            // Commencer le magnet
            _isMagneting = true;
            _magnetTarget = player.transform;
            _isGrounded = false;
        }
    }

    private void MoveTowardsTarget()
    {
        if (_magnetTarget == null)
        {
            _isMagneting = false;
            return;
        }

        Vector3 direction = (_magnetTarget.position - transform.position).normalized;
        transform.position += direction * _magnetSpeed * Time.deltaTime;

        // Si assez proche, pickup
        if (Vector3.Distance(transform.position, _magnetTarget.position) < 0.5f)
        {
            Pickup(_magnetTarget.gameObject);
        }
    }

    private void Pickup(GameObject picker)
    {
        if (_isPickedUp) return;
        _isPickedUp = true;

        // Ajouter l'or au joueur
        var playerStats = picker.GetComponent<PlayerStats>();
        if (playerStats == null)
        {
            playerStats = picker.GetComponentInParent<PlayerStats>();
        }

        if (playerStats != null)
        {
            playerStats.AddGold(_amount);
        }

        // Son
        if (_pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(_pickupSound, transform.position);
        }

        // Notification UI
        // TODO: UIManager.Instance.ShowGoldPickup(_amount);

        Debug.Log($"[GoldPickup] Picked up {_amount} gold");

        Destroy(gameObject);
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _pickupRadius);
    }

    #endregion
}
