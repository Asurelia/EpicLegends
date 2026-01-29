using System;
using UnityEngine;

/// <summary>
/// Classe de base pour tous les panneaux UI.
/// </summary>
public class UIPanel : MonoBehaviour
{
    #region Events

    /// <summary>
    /// Declenche quand le panneau est affiche.
    /// </summary>
    public event Action OnPanelShown;

    /// <summary>
    /// Declenche quand le panneau est cache.
    /// </summary>
    public event Action OnPanelHidden;

    #endregion

    #region Properties

    /// <summary>
    /// Indique si le panneau est visible.
    /// </summary>
    public bool IsVisible => gameObject.activeSelf;

    /// <summary>
    /// Priorite de tri pour l'ordre d'affichage.
    /// </summary>
    public int SortPriority { get; set; }

    /// <summary>
    /// Le panneau peut-il etre ferme avec Escape.
    /// </summary>
    public bool CanCloseWithEscape { get; set; } = true;

    /// <summary>
    /// Le panneau bloque-t-il les inputs du jeu.
    /// </summary>
    public bool BlocksGameInput { get; set; } = true;

    /// <summary>
    /// Le panneau met-il le jeu en pause.
    /// </summary>
    public bool PausesGame { get; set; } = false;

    #endregion

    #region Public Methods

    /// <summary>
    /// Affiche le panneau.
    /// </summary>
    public virtual void Show()
    {
        gameObject.SetActive(true);
        OnShow();
        OnPanelShown?.Invoke();
    }

    /// <summary>
    /// Cache le panneau.
    /// </summary>
    public virtual void Hide()
    {
        OnHide();
        gameObject.SetActive(false);
        OnPanelHidden?.Invoke();
    }

    /// <summary>
    /// Bascule la visibilite du panneau.
    /// </summary>
    public virtual void Toggle()
    {
        if (IsVisible)
            Hide();
        else
            Show();
    }

    /// <summary>
    /// Ferme le panneau (equivalent a Hide).
    /// </summary>
    public virtual void Close()
    {
        Hide();
    }

    /// <summary>
    /// Rafraichit l'affichage du panneau.
    /// </summary>
    public virtual void Refresh()
    {
        // A surcharger dans les classes derivees
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// Appele quand le panneau devient visible.
    /// </summary>
    protected virtual void OnShow()
    {
        // A surcharger dans les classes derivees
    }

    /// <summary>
    /// Appele quand le panneau est cache.
    /// </summary>
    protected virtual void OnHide()
    {
        // A surcharger dans les classes derivees
    }

    #endregion
}
