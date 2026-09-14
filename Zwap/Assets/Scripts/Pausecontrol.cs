using UnityEngine;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    public bool IsPaused { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject pauseMenuUI; // the panel that shows when paused (Resume/Quit buttons etc.)

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // Hook this up to the pause button's OnClick() in the Inspector.
    public void TogglePause()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        IsPaused = true;

        Time.timeScale = 0f;      // freezes movement, spawning, timers
        AudioListener.pause = true; // freezes all AudioSources (music + SFX)

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(true);
    }

    public void Resume()
    {
        IsPaused = false;

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);
    }
}