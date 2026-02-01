using UnityEngine;

/// <summary>
/// Item physique dans le monde pouvant etre ramasse.
/// </summary>
public class WorldItem : MonoBehaviour
{
    #region Serialized Fields

    [Header("Item Data")]
    [SerializeField] private ItemData _item;
    [SerializeField] private int _quantity = 1;

    [Header("Pickup Settings")]
    [SerializeField] private float _pickupRadius = 1.5f;
    [SerializeField] private bool _autoPickup = true;
    [SerializeField] private float _autoPickupDelay = 0.5f;

    [Header("Visual")]
    [SerializeField] private float _bobSpeed = 2f;
    [SerializeField] private float _bobHeight = 0.2f;
    [SerializeField] private float _rotateSpeed = 90f;
    [SerializeField] private GameObject _glowEffect;

    [Header("Audio")]
    [SerializeField] private AudioClip _pickupSound;

    #endregion

    #region Private Fields

    private float _spawnTime;
    private Vector3 _basePosition;
    private bool _isPickedUp = false;
    private bool _isGrounded = false;

    #endregion

    #region Unity Callbacks

    private void Start()
    {
        _spawnTime = Time.time;
        _basePosition = transform.position;

        // Activer le glow selon la rarete
        if (_glowEffect != null && _item != null)
        {
            _glowEffect.SetActive(_item.rarity >= ItemRarity.Rare);
        }
    }

    private void Update()
    {
        if (_isPickedUp) return;

        // Animation de flottement et rotation
        if (_isGrounded)
        {
            AnimateItem();
        }

        // Auto-pickup si configure
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

            // Desactiver la physique une fois au sol
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }
        }

        // Pickup par le joueur
        if (other.CompareTag("Player"))
        {
            TryPickup(other.gameObject);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Initialise le WorldItem avec un item et une quantite.
    /// </summary>
    public void Initialize(ItemData item, int quantity = 1)
    {
        _item = item;
        _quantity = quantity;

        // Mettre a jour le visuel si necessaire
        UpdateVisual();
    }

    /// <summary>
    /// Tente de ramasser l'item.
    /// </summary>
    public bool TryPickup(GameObject picker)
    {
        if (_isPickedUp || _item == null) return false;

        // Trouver l'inventaire du joueur
        var inventory = picker.GetComponent<Inventory>();
        if (inventory == null)
        {
            inventory = picker.GetComponentInParent<Inventory>();
        }

        if (inventory == null)
        {
            Debug.LogWarning("[WorldItem] Pas d'inventaire trouve sur le picker");
            return false;
        }

        // Tenter d'ajouter a l'inventaire
        bool added = inventory.AddItem(_item, _quantity);

        if (added)
        {
            OnPickedUp();
            return true;
        }
        else
        {
            // Inventaire plein
            Debug.Log("[WorldItem] Inventaire plein!");
            // TODO: Afficher un message UI
            return false;
        }
    }

    #endregion

    #region Private Methods

    private void AnimateItem()
    {
        // Bob up and down
        float newY = _basePosition.y + Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

        // Rotate
        transform.Rotate(Vector3.up, _rotateSpeed * Time.deltaTime);
    }

    private void CheckAutoPickup()
    {
        // Chercher le joueur dans le rayon
        var player = GameManager.Instance?.Player;
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist <= _pickupRadius)
        {
            TryPickup(player);
        }
    }

    private void UpdateVisual()
    {
        if (_item == null) return;

        // Si l'item a un prefab visuel, l'utiliser
        // Sinon, colorer selon la rarete
        var renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            var mat = renderer.material;
            if (mat != null)
            {
                mat.color = GetRarityColor(_item.rarity);
            }
        }
    }

    private void OnPickedUp()
    {
        _isPickedUp = true;

        // Jouer le son
        if (_pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(_pickupSound, transform.position);
        }

        // Notification UI
        // TODO: UIManager.Instance.ShowItemPickup(_item, _quantity);

        Debug.Log($"[WorldItem] Picked up {_quantity}x {_item.displayName}");

        // Detruire l'objet
        Destroy(gameObject);
    }

    private Color GetRarityColor(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common => Color.white,
            ItemRarity.Uncommon => Color.green,
            ItemRarity.Rare => Color.blue,
            ItemRarity.Epic => new Color(0.6f, 0f, 0.8f),
            ItemRarity.Legendary => new Color(1f, 0.5f, 0f),
            ItemRarity.Mythic => Color.red,
            _ => Color.gray
        };
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
