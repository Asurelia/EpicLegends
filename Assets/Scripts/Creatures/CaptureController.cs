using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Controleur pour gerer les captures de creatures.
/// Attache au joueur pour permettre les captures.
/// </summary>
public class CaptureController : MonoBehaviour
{
    #region Fields

    [Header("Settings")]
    [SerializeField] private float _captureRange = 15f;
    [SerializeField] private float _throwSpeed = 20f;
    [SerializeField] private float _oscillationDuration = 0.5f;
    [SerializeField] private LayerMask _creatureLayer;

    [Header("References")]
    [SerializeField] private Transform _throwOrigin;
    [SerializeField] private CreatureParty _creatureParty;

    // Etat
    private CaptureItemData _equippedItem;
    private bool _isCapturing;
    private CreatureController _currentTarget;

    #endregion

    #region Events

    public event Action<CreatureController> OnCaptureStarted;
    public event Action<CreatureController, bool, int> OnCaptureEnded; // target, success, oscillations
    public event Action<CreatureController> OnCaptureOscillation;
    public event Action OnNoItemEquipped;
    public event Action OnTargetOutOfRange;
    public event Action OnPartyFull;

    #endregion

    #region Properties

    public float CaptureRange => _captureRange;
    public bool IsCapturing => _isCapturing;
    public CaptureItemData EquippedItem => _equippedItem;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (_throwOrigin == null)
        {
            _throwOrigin = transform;
        }
    }

    private void OnDestroy()
    {
        // MAJOR FIX: Stop all coroutines to prevent memory leaks
        StopAllCoroutines();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Equipe un item de capture.
    /// </summary>
    public void EquipCaptureItem(CaptureItemData item)
    {
        _equippedItem = item;
    }

    /// <summary>
    /// Tente de capturer une creature.
    /// </summary>
    public bool TryCapture(CreatureController target)
    {
        if (_isCapturing) return false;
        if (target == null) return false;
        if (target.IsOwnedByPlayer) return false;

        // Verifier l'item equipe
        if (_equippedItem == null)
        {
            OnNoItemEquipped?.Invoke();
            return false;
        }

        // Verifier la portee
        float distance = Vector3.Distance(transform.position, target.transform.position);
        if (distance > _captureRange)
        {
            OnTargetOutOfRange?.Invoke();
            return false;
        }

        // Verifier l'espace dans l'equipe
        if (_creatureParty != null && _creatureParty.IsPartyFull && _creatureParty.IsStorageFull)
        {
            OnPartyFull?.Invoke();
            return false;
        }

        // Lancer la capture
        StartCoroutine(CaptureSequence(target));
        return true;
    }

    /// <summary>
    /// Capture la creature la plus proche dans la portee.
    /// </summary>
    public bool TryCaptureNearest()
    {
        var target = FindNearestCreature();
        if (target == null) return false;
        return TryCapture(target);
    }

    /// <summary>
    /// Trouve la creature la plus proche.
    /// </summary>
    public CreatureController FindNearestCreature()
    {
        var colliders = Physics.OverlapSphere(transform.position, _captureRange, _creatureLayer);

        CreatureController nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (var collider in colliders)
        {
            var creature = collider.GetComponent<CreatureController>();
            if (creature == null) continue;
            if (creature.IsOwnedByPlayer) continue;
            if (creature.IsFainted) continue;

            float distance = Vector3.Distance(transform.position, creature.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = creature;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Annule la capture en cours.
    /// </summary>
    public void CancelCapture()
    {
        if (_isCapturing)
        {
            StopAllCoroutines();
            _isCapturing = false;
        }
    }

    /// <summary>
    /// Definit la reference a la party.
    /// </summary>
    public void SetCreatureParty(CreatureParty party)
    {
        _creatureParty = party;
    }

    #endregion

    #region Private Methods

    private IEnumerator CaptureSequence(CreatureController target)
    {
        _isCapturing = true;
        _currentTarget = target;

        OnCaptureStarted?.Invoke(target);

        // Lancer l'item de capture (animation)
        yield return StartCoroutine(ThrowCaptureItem(target));

        // Effectuer le calcul de capture avec oscillations
        var result = CaptureCalculator.AttemptCaptureWithOscillations(
            target.Instance,
            _equippedItem,
            1f // TODO: utiliser le niveau joueur
        );

        // Animation des oscillations
        for (int i = 0; i < result.oscillations; i++)
        {
            OnCaptureOscillation?.Invoke(target);
            yield return new WaitForSeconds(_oscillationDuration);
        }

        // Resultat
        if (result.success)
        {
            // Capture reussie
            OnCaptureSuccess(target);
        }
        else
        {
            // Capture ratee
            OnCaptureFailed(target);
        }

        OnCaptureEnded?.Invoke(target, result.success, result.oscillations);

        _isCapturing = false;
        _currentTarget = null;
    }

    private IEnumerator ThrowCaptureItem(CreatureController target)
    {
        if (_equippedItem?.throwPrefab == null)
        {
            yield break;
        }

        // Creer l'objet lance
        var throwObject = Instantiate(
            _equippedItem.throwPrefab,
            _throwOrigin.position,
            Quaternion.identity
        );

        // Jouer le son
        if (_equippedItem.throwSound != null)
        {
            AudioSource.PlayClipAtPoint(_equippedItem.throwSound, _throwOrigin.position);
        }

        // Animer vers la cible
        Vector3 startPos = _throwOrigin.position;
        Vector3 endPos = target.transform.position + Vector3.up;
        float distance = Vector3.Distance(startPos, endPos);
        float duration = distance / _throwSpeed;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Trajectoire en arc
            Vector3 pos = Vector3.Lerp(startPos, endPos, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * 2f; // Arc parabolique

            throwObject.transform.position = pos;
            throwObject.transform.LookAt(endPos);

            yield return null;
        }

        // Detruire l'objet lance
        Destroy(throwObject);
    }

    private void OnCaptureSuccess(CreatureController target)
    {
        // Effet visuel
        if (_equippedItem?.successVFX != null)
        {
            Instantiate(_equippedItem.successVFX, target.transform.position, Quaternion.identity);
        }

        // Son
        if (_equippedItem?.successSound != null)
        {
            AudioSource.PlayClipAtPoint(_equippedItem.successSound, target.transform.position);
        }

        // Ajouter a l'equipe
        if (_creatureParty != null)
        {
            _creatureParty.AddCreature(target.Instance);
        }

        // Calculer l'XP bonus pour affaiblissement
        float weakeningBonus = CaptureCalculator.CalculateWeakeningBonus(target.Instance);
        int bonusXP = Mathf.RoundToInt(target.Data.defeatExperience * weakeningBonus);

        // Donner l'XP bonus au joueur
        var playerStats = GetComponent<PlayerStats>();
        if (playerStats != null && bonusXP > 0)
        {
            playerStats.AddExperience(bonusXP);
        }

        // Achievement integration
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnCreatureCaptured();
        }

        // Consommer l'item de capture de l'inventaire
        ConsumeEquippedItem();

        // Detruire la creature sauvage
        Destroy(target.gameObject);

        Debug.Log($"Capture reussie! {target.Instance.Nickname} ajoute a l'equipe. Bonus XP: {bonusXP}");
    }

    private void OnCaptureFailed(CreatureController target)
    {
        // Effet visuel
        if (_equippedItem?.failVFX != null)
        {
            Instantiate(_equippedItem.failVFX, target.transform.position, Quaternion.identity);
        }

        // Son
        if (_equippedItem?.failSound != null)
        {
            AudioSource.PlayClipAtPoint(_equippedItem.failSound, target.transform.position);
        }

        // Consommer l'item de capture meme en cas d'echec
        ConsumeEquippedItem();

        Debug.Log($"Capture ratee! {target.Instance.Nickname} s'est echappe.");
    }

    /// <summary>
    /// Consomme l'item de capture equipe.
    /// </summary>
    private void ConsumeEquippedItem()
    {
        if (_equippedItem == null) return;

        var inventory = GetComponent<Inventory>();
        if (inventory != null)
        {
            // Trouver l'item dans l'inventaire par son nom
            var items = inventory.GetAllItems();
            foreach (var item in items)
            {
                if (item?.Data != null && item.Data.displayName == _equippedItem.itemName)
                {
                    inventory.RemoveItem(item.Data, 1);

                    // Si plus d'item, desequiper
                    int remaining = inventory.GetItemCount(item.Data.itemId);
                    if (remaining <= 0)
                    {
                        _equippedItem = null;
                    }
                    break;
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Dessiner la portee de capture
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _captureRange);
    }

    #endregion
}
