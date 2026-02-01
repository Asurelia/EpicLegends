using UnityEngine;

/// <summary>
/// Caméra simple qui suit le joueur en third-person.
/// Alternative au CameraController Cinemachine quand celui-ci n'est pas configuré.
/// </summary>
public class SimpleFollowCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform _target;
    
    [Header("Position")]
    [SerializeField] private Vector3 _offset = new Vector3(0f, 8f, -10f);
    [SerializeField] private float _smoothSpeed = 5f;
    
    [Header("Rotation")]
    [SerializeField] private float _lookAtHeight = 1.5f;
    
    private void Start()
    {
        // Trouver le joueur si non assigné
        if (_target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _target = player.transform;
                Debug.Log("[SimpleFollowCamera] Player trouvé automatiquement");
            }
            else
            {
                Debug.LogWarning("[SimpleFollowCamera] Aucun Player trouvé! Assignez la cible manuellement.");
            }
        }
        
        // Position initiale
        if (_target != null)
        {
            transform.position = _target.position + _offset;
            LookAtTarget();
        }
    }
    
    private void LateUpdate()
    {
        if (_target == null) return;
        
        // Position désirée
        Vector3 desiredPosition = _target.position + _offset;
        
        // Interpolation smooth
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, _smoothSpeed * Time.deltaTime);
        transform.position = smoothedPosition;
        
        // Regarder le joueur
        LookAtTarget();
    }
    
    private void LookAtTarget()
    {
        if (_target == null) return;
        
        Vector3 lookAtPoint = _target.position + Vector3.up * _lookAtHeight;
        transform.LookAt(lookAtPoint);
    }
    
    /// <summary>
    /// Change la cible de la caméra.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        _target = newTarget;
    }
    
    /// <summary>
    /// Change l'offset de la caméra.
    /// </summary>
    public void SetOffset(Vector3 newOffset)
    {
        _offset = newOffset;
    }
}
