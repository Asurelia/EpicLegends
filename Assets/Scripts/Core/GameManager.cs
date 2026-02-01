using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Central game manager singleton.
/// Handles game state, pausing, and scene transitions.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game State")]
    [SerializeField] private bool _isPaused = false;

    // Events
    public event Action OnGamePaused;
    public event Action OnGameResumed;
    public event Action OnGameOver;

    // Player reference
    private PlayerStats _playerStats;

    // Input System
    private InputAction _cancelAction;

    #region Unity Callbacks

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // DontDestroyOnLoad only works for root GameObjects
        if (Application.isPlaying)
        {
            // Detach from parent to make this a root object
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Start()
    {
        FindPlayer();
        SetupInputActions();
    }

    private void OnEnable()
    {
        _cancelAction?.Enable();
    }

    private void OnDisable()
    {
        _cancelAction?.Disable();
    }

    private void SetupInputActions()
    {
        // Create escape/cancel action for pause
        _cancelAction = new InputAction("Cancel", InputActionType.Button);
        _cancelAction.AddBinding("<Keyboard>/escape");
        _cancelAction.AddBinding("<Gamepad>/start");
        _cancelAction.performed += _ => TogglePause();
        _cancelAction.Enable();
    }

    #endregion

    #region Game State

    public void TogglePause()
    {
        if (_isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        _isPaused = true;
        Time.timeScale = 0f;
        OnGamePaused?.Invoke();
    }

    public void ResumeGame()
    {
        _isPaused = false;
        Time.timeScale = 1f;
        OnGameResumed?.Invoke();
    }

    public void GameOver()
    {
        OnGameOver?.Invoke();
        // Don't pause - let death animation play
    }

    #endregion

    #region Scene Management

    public void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    public void LoadScene(int buildIndex)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(buildIndex);
    }

    public void ReloadCurrentScene()
    {
        LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region Player Reference

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _playerStats = playerObj.GetComponent<PlayerStats>();
            if (_playerStats != null)
            {
                _playerStats.OnDeath += GameOver;
            }
        }
    }

    #endregion

    #region Properties

    public bool IsPaused => _isPaused;

    /// <summary>
    /// Reference au GameObject du joueur.
    /// </summary>
    public GameObject Player
    {
        get
        {
            if (_playerStats != null)
                return _playerStats.gameObject;

            // Recherche fallback
            return GameObject.FindGameObjectWithTag("Player");
        }
    }

    /// <summary>
    /// Reference aux stats du joueur.
    /// </summary>
    public PlayerStats PlayerStats => _playerStats;

    #endregion
}
