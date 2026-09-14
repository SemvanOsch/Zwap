using UnityEngine;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    public bool IsPaused { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject pauseMenuUI; // the panel that shows when paused (Resume/Quit buttons etc.)

    [Header("Pause Button Icon")]
    [SerializeField] private Image pauseButtonImage; // the Image component on the pause button itself
    [SerializeField] private Sprite pauseSprite;      // shown while the game is running (tap to pause)
    [SerializeField] private Sprite resumeSprite;     // shown while paused (tap to resume)

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        // Make sure the pause button renders on top from the moment the scene
        // loads, not just after the first Pause() call.
        transform.SetAsLastSibling();
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

        Time.timeScale = 0f;        // freezes movement, spawning, timers
        AudioListener.pause = true; // freezes all AudioSources (music + SFX)

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(true);

        transform.SetAsLastSibling();
        UpdateButtonSprite();
    }

    public void Resume()
    {
        IsPaused = false;

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (pauseMenuUI != null)
            pauseMenuUI.SetActive(false);

        UpdateButtonSprite();
    }

    private void UpdateButtonSprite()
    {
        if (pauseButtonImage == null) return;

        pauseButtonImage.sprite = IsPaused ? resumeSprite : pauseSprite;
    }
}