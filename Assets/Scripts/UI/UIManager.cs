using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire central de l'interface utilisateur.
/// Gere l'enregistrement, l'affichage et la pile de panneaux.
/// </summary>
public class UIManager : MonoBehaviour
{
    #region Singleton

    private static UIManager _instance;
    public static UIManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<UIManager>();
            }
            return _instance;
        }
    }

    #endregion

    #region Private Fields

    private Dictionary<string, UIPanel> _registeredPanels = new Dictionary<string, UIPanel>();
    private Stack<UIPanel> _panelStack = new Stack<UIPanel>();

    #endregion

    #region Events

    /// <summary>
    /// Declenche quand un panneau est ouvert.
    /// </summary>
    public event Action<UIPanel> OnPanelOpened;

    /// <summary>
    /// Declenche quand un panneau est ferme.
    /// </summary>
    public event Action<UIPanel> OnPanelClosed;

    /// <summary>
    /// Declenche quand la pile UI change.
    /// </summary>
    public event Action OnUIStackChanged;

    #endregion

    #region Properties

    /// <summary>
    /// Nombre de panneaux dans la pile.
    /// </summary>
    public int PanelStackCount => _panelStack.Count;

    /// <summary>
    /// Panneau actuellement au sommet de la pile.
    /// </summary>
    public UIPanel CurrentPanel => _panelStack.Count > 0 ? _panelStack.Peek() : null;

    /// <summary>
    /// Indique si un UI est actuellement ouvert.
    /// </summary>
    public bool IsUIOpen => _panelStack.Count > 0;

    /// <summary>
    /// Indique si le jeu doit etre en pause.
    /// </summary>
    public bool ShouldPauseGame
    {
        get
        {
            foreach (var panel in _panelStack)
            {
                if (panel.PausesGame) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Indique si les inputs du jeu doivent etre bloques.
    /// </summary>
    public bool ShouldBlockGameInput
    {
        get
        {
            foreach (var panel in _panelStack)
            {
                if (panel.BlocksGameInput) return true;
            }
            return false;
        }
    }

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void Update()
    {
        // Gerer la fermeture avec Escape
        if (Input.GetKeyDown(KeyCode.Escape) && CurrentPanel != null && CurrentPanel.CanCloseWithEscape)
        {
            PopPanel();
        }
    }

    #endregion

    #region Panel Registration

    /// <summary>
    /// Enregistre un panneau avec un identifiant.
    /// </summary>
    public void RegisterPanel(string id, UIPanel panel)
    {
        if (string.IsNullOrEmpty(id) || panel == null)
        {
            Debug.LogWarning("[UIManager] ID ou panneau invalide");
            return;
        }

        if (_registeredPanels.ContainsKey(id))
        {
            Debug.LogWarning($"[UIManager] Panneau deja enregistre: {id}");
            _registeredPanels[id] = panel;
        }
        else
        {
            _registeredPanels.Add(id, panel);
        }
    }

    /// <summary>
    /// Desenregistre un panneau.
    /// </summary>
    public void UnregisterPanel(string id)
    {
        if (_registeredPanels.ContainsKey(id))
        {
            _registeredPanels.Remove(id);
        }
    }

    /// <summary>
    /// Verifie si un panneau est enregistre.
    /// </summary>
    public bool HasPanel(string id)
    {
        return _registeredPanels.ContainsKey(id);
    }

    /// <summary>
    /// Obtient un panneau par son ID.
    /// </summary>
    public T GetPanel<T>(string id) where T : UIPanel
    {
        if (_registeredPanels.TryGetValue(id, out var panel))
        {
            return panel as T;
        }
        return null;
    }

    /// <summary>
    /// Obtient un panneau par son ID (non-generique).
    /// </summary>
    public UIPanel GetPanel(string id)
    {
        _registeredPanels.TryGetValue(id, out var panel);
        return panel;
    }

    #endregion

    #region Panel Stack

    /// <summary>
    /// Ajoute un panneau a la pile et l'affiche.
    /// </summary>
    public void PushPanel(string id)
    {
        if (!_registeredPanels.TryGetValue(id, out var panel))
        {
            Debug.LogWarning($"[UIManager] Panneau non trouve: {id}");
            return;
        }

        PushPanel(panel);
    }

    /// <summary>
    /// Ajoute un panneau a la pile et l'affiche.
    /// </summary>
    public void PushPanel(UIPanel panel)
    {
        if (panel == null) return;

        // Desactiver le panneau actuel s'il existe
        if (CurrentPanel != null && CurrentPanel != panel)
        {
            // On ne cache pas, on desactive juste l'interaction
        }

        _panelStack.Push(panel);
        panel.Show();

        OnPanelOpened?.Invoke(panel);
        OnUIStackChanged?.Invoke();
    }

    /// <summary>
    /// Retire le panneau au sommet de la pile.
    /// </summary>
    public UIPanel PopPanel()
    {
        if (_panelStack.Count == 0) return null;

        var panel = _panelStack.Pop();
        panel.Hide();

        OnPanelClosed?.Invoke(panel);
        OnUIStackChanged?.Invoke();

        return panel;
    }

    /// <summary>
    /// Ferme tous les panneaux.
    /// </summary>
    public void PopAllPanels()
    {
        while (_panelStack.Count > 0)
        {
            var panel = _panelStack.Pop();
            panel.Hide();
            OnPanelClosed?.Invoke(panel);
        }

        OnUIStackChanged?.Invoke();
    }

    /// <summary>
    /// Ferme un panneau specifique s'il est dans la pile.
    /// </summary>
    public void ClosePanel(string id)
    {
        if (!_registeredPanels.TryGetValue(id, out var panel)) return;
        ClosePanel(panel);
    }

    /// <summary>
    /// Ferme un panneau specifique s'il est dans la pile.
    /// </summary>
    public void ClosePanel(UIPanel panel)
    {
        if (panel == null) return;

        // Reconstruire la pile sans ce panneau
        var tempStack = new Stack<UIPanel>();
        bool found = false;

        while (_panelStack.Count > 0)
        {
            var current = _panelStack.Pop();
            if (current == panel)
            {
                current.Hide();
                found = true;
                OnPanelClosed?.Invoke(current);
            }
            else
            {
                tempStack.Push(current);
            }
        }

        // Restaurer la pile
        while (tempStack.Count > 0)
        {
            _panelStack.Push(tempStack.Pop());
        }

        if (found)
        {
            OnUIStackChanged?.Invoke();
        }
    }

    #endregion

    #region Quick Access

    /// <summary>
    /// Affiche ou cache un panneau (toggle).
    /// </summary>
    public void TogglePanel(string id)
    {
        if (!_registeredPanels.TryGetValue(id, out var panel)) return;

        if (panel.IsVisible)
        {
            ClosePanel(panel);
        }
        else
        {
            PushPanel(panel);
        }
    }

    /// <summary>
    /// Affiche uniquement ce panneau (ferme les autres).
    /// </summary>
    public void ShowExclusive(string id)
    {
        PopAllPanels();
        PushPanel(id);
    }

    #endregion
}
