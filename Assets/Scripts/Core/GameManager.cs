using System;
using UnityEngine;
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
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        FindPlayer();
    }

    private void Update()
    {
        // Toggle pause with Escape
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
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

    #endregion
}
