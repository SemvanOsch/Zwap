using System.Collections;
using UnityEngine;

/// <summary>
/// Shows a one-time help popup the first time the player enters a given control/area.
///
/// How it works:
///   - Each "area" is a pre-made panel you design in the Editor, paired with a unique key.
///   - The first time ShowHelp(key) is called for that key, the panel appears and the
///     game freezes (same freeze as PauseManager: Time.timeScale = 0 + AudioListener.pause).
///   - Each panel has a full-screen Button whose OnClick is wired to CloseHelp(). Tapping it
///     closes the popup, unfreezes the game, and records the key in PlayerPrefs so it never
///     shows again — including on later boots, because PlayerPrefs is stored on disk.
///     (Closing via the Button/EventSystem, rather than polling the pointer and deactivating
///     the panel underneath it, is what keeps the UI input system from getting stuck.)
///
/// It auto-triggers a popup for each ControlType the first time that control becomes active
/// (via ControlSwitcher), and you can also call ShowHelp("YourKey") from anywhere for any
/// other one-off area.
/// </summary>
public class HelpManager : MonoBehaviour
{
    public static HelpManager Instance { get; private set; }

    [System.Serializable]
    public class HelpArea
    {
        [Tooltip("Unique id for this area. For control popups this must match the ControlType " +
                 "name exactly (Tilt, Touch, Follow, Joystick, Slider, Slingshot).")]
        public string key;

        [Tooltip("The pre-made panel to show for this area. It should sit inactive in the scene " +
                 "and cover the screen with your transparent background + explanation text.")]
        public GameObject panel;
    }

    [Header("Areas")]
    [Tooltip("One entry per help popup. Design each panel yourself in the Editor.")]
    [SerializeField] private HelpArea[] areas;

    [Header("Behaviour")]
    [Tooltip("Prefix used for the PlayerPrefs flag that records a popup has been seen.")]
    [SerializeField] private string prefsKeyPrefix = "helpSeen_";

    [Tooltip("Also show help for the control that is already active when the scene starts.")]
    [SerializeField] private bool showHelpForStartingControl = true;

    [Tooltip("Appended to the control name to form the key when that control is inverted. " +
             "E.g. with 'Inverse', an inverted Touch control looks for the area key 'TouchInverse'.")]
    [SerializeField] private string inverseSuffix = "Inverse";

    [Tooltip("Seconds to wait after a control becomes active before its popup appears, so the " +
             "new control is visible first. Real (unscaled) time. 0 = show immediately.")]
    [SerializeField] private float showDelay = 0.5f;

    [Header("Debug")]
    [Tooltip("TESTING ONLY: when on, popups ignore the 'already seen' flag and never write it, " +
             "so every area shows again on each run. Turn this OFF for real saving.")]
    [SerializeField] private bool debugIgnoreSaved = false;

    // The area currently on screen, or null when nothing is showing.
    private HelpArea currentArea;

    // The pending delayed-show, so a newer request can cancel an older one.
    private Coroutine pendingShow;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Make sure every panel starts hidden regardless of how it was left in the Editor.
        if (areas != null)
        {
            foreach (var area in areas)
            {
                if (area != null && area.panel != null)
                    area.panel.SetActive(false);
            }
        }
    }

    private void Start()
    {
        // Subscribe here (not in OnEnable): by Start, every object's Awake has run, so
        // ControlSwitcher.Instance is guaranteed set. Subscribing in OnEnable raced
        // ControlSwitcher's Awake and could silently miss the hookup depending on load order,
        // which is why the popup showed on some scene entries but not others.
        if (ControlSwitcher.Instance != null)
            ControlSwitcher.Instance.OnControlChanged += HandleControlChanged;

        // ControlSwitcher does not fire OnControlChanged for the very first control (it is
        // set up silently in Awake/Start), so trigger its help here.
        if (showHelpForStartingControl && ControlSwitcher.Instance != null)
            RequestShow(BuildKey(ControlSwitcher.Instance.CurrentControl, ControlSwitcher.Instance.IsInverted));
    }

    private void OnDisable()
    {
        if (ControlSwitcher.Instance != null)
            ControlSwitcher.Instance.OnControlChanged -= HandleControlChanged;

        pendingShow = null; // coroutines are stopped automatically when the object is disabled
    }

    private void HandleControlChanged(ControlType type)
    {
        // ControlSwitcher sets CurrentControl/IsInverted before firing this, so IsInverted
        // here reflects the control that just became active.
        bool inverted = ControlSwitcher.Instance != null && ControlSwitcher.Instance.IsInverted;
        RequestShow(BuildKey(type, inverted));
    }

    // Shows the popup after showDelay, so the new control is on screen first. A newer request
    // cancels an older pending one.
    private void RequestShow(string key)
    {
        if (pendingShow != null)
            StopCoroutine(pendingShow);

        if (showDelay <= 0f)
        {
            ShowHelp(key);
            return;
        }

        pendingShow = StartCoroutine(ShowAfterDelay(key));
    }

    private IEnumerator ShowAfterDelay(string key)
    {
        yield return new WaitForSecondsRealtime(showDelay);
        pendingShow = null;
        ShowHelp(key);
    }

    // Builds the area key for a control: its name, plus the inverse suffix when inverted
    // (e.g. "Touch" or "TouchInverse"). Controls that never invert always yield the plain name.
    private string BuildKey(ControlType type, bool inverted)
    {
        return inverted ? type + inverseSuffix : type.ToString();
    }

    /// <summary>
    /// Shows the help popup for this area the first time only. Does nothing if the area was
    /// already seen, has no panel, or another popup is currently open.
    /// </summary>
    public void ShowHelp(string key)
    {
        if (string.IsNullOrEmpty(key))
            return;

        // Don't stack popups; if one is already up, ignore new requests.
        if (currentArea != null)
            return;

        // Already seen in a previous run (or earlier this run)? Skip.
        // (Skipped while debugIgnoreSaved is on, so popups always show during testing.)
        if (!debugIgnoreSaved && PlayerPrefs.GetInt(prefsKeyPrefix + key, 0) == 1)
            return;

        HelpArea area = FindArea(key);
        if (area == null || area.panel == null)
            return;

        currentArea = area;
        area.panel.SetActive(true);

        // Freeze exactly like PauseManager (harmless if something already froze the game).
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    /// <summary>
    /// Closes the popup that is currently showing, unfreezes the game, and marks the area
    /// as seen so it never shows again. Safe to call from a Button OnClick too.
    /// </summary>
    public void CloseHelp()
    {
        if (currentArea == null)
            return;

        // Persist "seen" so it stays closed across boots.
        // (Skipped while debugIgnoreSaved is on, so nothing gets recorded during testing.)
        if (!debugIgnoreSaved)
        {
            PlayerPrefs.SetInt(prefsKeyPrefix + currentArea.key, 1);
            PlayerPrefs.Save();
        }

        if (currentArea.panel != null)
            currentArea.panel.SetActive(false);

        currentArea = null;

        // Resume the game — unless the real pause menu is currently open, in which case
        // leave it frozen so we don't accidentally un-pause a genuine pause.
        bool pauseMenuOpen = PauseManager.Instance != null && PauseManager.Instance.IsPaused;
        if (!pauseMenuOpen)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }

    private HelpArea FindArea(string key)
    {
        if (areas == null)
            return null;

        foreach (var area in areas)
        {
            if (area != null && area.key == key)
                return area;
        }

        return null;
    }
}
