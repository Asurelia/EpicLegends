using UnityEngine;

/// <summary>
/// ScriptableObject contenant les paramètres de la caméra.
/// Permet de modifier les paramètres sans toucher au code.
/// </summary>
[CreateAssetMenu(fileName = "CameraSettings", menuName = "EpicLegends/Camera/Camera Settings")]
public class CameraSettings : ScriptableObject
{
    [Header("Distance")]
    [Tooltip("Distance de la caméra au joueur en mode exploration")]
    [Range(2f, 20f)]
    public float explorationDistance = 8f;

    [Tooltip("Distance de la caméra au joueur en mode combat")]
    [Range(2f, 15f)]
    public float combatDistance = 5f;

    [Header("Hauteur")]
    [Tooltip("Hauteur de la caméra par rapport au joueur")]
    [Range(0f, 5f)]
    public float cameraHeight = 2f;

    [Tooltip("Offset vertical du point de visée")]
    [Range(0f, 3f)]
    public float lookAtHeight = 1.5f;

    [Header("Sensibilité")]
    [Tooltip("Sensibilité horizontale de la caméra")]
    [Range(0.1f, 10f)]
    public float horizontalSensitivity = 3f;

    [Tooltip("Sensibilité verticale de la caméra")]
    [Range(0.1f, 10f)]
    public float verticalSensitivity = 2f;

    [Tooltip("Inverser l'axe Y")]
    public bool invertY = false;

    [Header("Limites d'angle")]
    [Tooltip("Angle minimum de la caméra (regarder vers le haut)")]
    [Range(-90f, 0f)]
    public float minVerticalAngle = -30f;

    [Tooltip("Angle maximum de la caméra (regarder vers le bas)")]
    [Range(0f, 90f)]
    public float maxVerticalAngle = 60f;

    [Header("Lissage")]
    [Tooltip("Lissage du mouvement de la caméra")]
    [Range(0f, 1f)]
    public float damping = 0.1f;

    [Tooltip("Lissage de la rotation de la caméra")]
    [Range(0f, 1f)]
    public float rotationDamping = 0.05f;

    [Header("Collision")]
    [Tooltip("Activer la détection de collision")]
    public bool enableCollision = true;

    [Tooltip("Rayon de la sphère de collision")]
    [Range(0.1f, 1f)]
    public float collisionRadius = 0.3f;

    [Tooltip("Layers considérés comme obstacles")]
    public LayerMask collisionLayers = ~0;

    [Header("Lock-On")]
    [Tooltip("Distance maximale pour le lock-on")]
    [Range(5f, 50f)]
    public float lockOnMaxDistance = 20f;

    [Tooltip("Angle de détection du lock-on")]
    [Range(10f, 90f)]
    public float lockOnAngle = 45f;

    [Tooltip("Vitesse de rotation vers la cible")]
    [Range(1f, 20f)]
    public float lockOnRotationSpeed = 10f;

    [Header("Camera Shake")]
    [Tooltip("Intensité par défaut du shake")]
    [Range(0f, 5f)]
    public float defaultShakeIntensity = 1f;

    [Tooltip("Durée par défaut du shake")]
    [Range(0.05f, 2f)]
    public float defaultShakeDuration = 0.2f;
}
