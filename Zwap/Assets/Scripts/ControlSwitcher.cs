using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum ControlType
{
    Tilt,
    Touch,
    Follow,
    Joystick,  // on-screen Terresquall Virtual Joystick. Appended (not inserted) so
               // existing serialized ControlType values in scenes keep their meaning.
    Slider,    // two on-screen sliders (bottom = X, right = Y) set an absolute
               // position; the fish glides toward it. Appended for the same reason.
    Slingshot  // hold on the fish, drag back and release: the fish launches the
               // opposite way and coasts. No on-screen panel. Appended for the same reason.
}

public enum ControlDifficulty
{
    Easy,
    Medium,
    Hard
}

[System.Serializable]
public class ControlPanel
{
    public ControlType type;
    public GameObject normalPanel;
    public GameObject invertedPanel;   // optional; leave null if this control can't invert
}

// One entry per (ControlType, inverted) combination that actually exists, tagging
// it with a difficulty. A control's inverted variant is treated as a fully
// separate, independently-tagged option - not a random coin-flip applied after
// the fact - so e.g. "Touch" can be Easy while "Touch (Inverted)" is Hard.
// Kept in sync automatically (see SyncControlDifficultyEntries): every
// ControlType always gets a normal entry, and additionally gets an inverted
// entry only if it's listed in invertibleControls below.
[System.Serializable]
public class ControlDifficultyEntry
{
    public ControlType type;
    public bool inverted;
    public ControlDifficulty difficulty;
}

public class ControlSwitcher : MonoBehaviour
{
    public static ControlSwitcher Instance;

    [Header("Switching")]
    [SerializeField] private float switchInterval = 10f;

    [Header("Shock Effect")]
    [SerializeField] private GameObject shockEffectPrefab;

    // Time from the start of the shock animation to the power-10 "bang" frame:
    // 2 frames (0.16s) + pause (1.3s) + 3 frames (0.24s) + pause (1.3s)
    // + 3 frames before the power-10 frame (0.24s) = 3.24s.
    // Tweak this by ear if you retime the animation/audio.
    [SerializeField] private float shockBangDelay = 3.24f;

    [Header("Control Panels")]
    [SerializeField] private ControlPanel[] controlPanels;

    [Header("Difficulty")]
    [Tooltip("Which difficulty each control (and each control's Inverted variant, " +
             "if it has one) belongs to. Stays in sync automatically: add a new " +
             "ControlType, or add one to Invertible Controls below, and the matching " +
             "entry appears here tagged Easy by default, ready for you to re-tag it.")]
    [SerializeField] private ControlDifficultyEntry[] controlDifficulties;

    [Tooltip("The forced sequence difficulties are picked in. E.g. Easy, Medium, Hard " +
             "means: 1st control is rolled from the Easy pool, 2nd from Medium, 3rd " +
             "from Hard, 4th wraps back around to Easy, and so on.")]
    [SerializeField] private ControlDifficulty[] difficultyOrder =
    {
        ControlDifficulty.Easy,
        ControlDifficulty.Medium,
        ControlDifficulty.Hard
    };

    [Header("Next Control Icon")]
    [SerializeField] private Image nextControlIcon;
    [SerializeField] private Sprite tiltSprite;
    [SerializeField] private Sprite touchSprite;
    [SerializeField] private Sprite followSprite;
    [SerializeField] private Sprite joystickSprite;
    [SerializeField] private Sprite sliderSprite;
    [SerializeField] private Sprite slingshotSprite;
    [SerializeField] private Sprite invertedSprite;
    [Tooltip("Optional. Unique icon for the inverted joystick. Falls back to Inverted Sprite if left empty.")]
    [SerializeField] private Sprite joystickInvertedSprite;

    [Header("Inverse")]
    [Tooltip("Which controls even have an Inverted variant available to be picked. " +
             "This no longer rolls a chance at runtime - it just determines whether " +
             "an \"Inverted\" row for that control exists above in Control Difficulties, " +
             "so you can tag it into whichever difficulty tier you want.")]
    [SerializeField] private ControlType[] invertibleControls = { ControlType.Touch };

    public ControlType CurrentControl { get; private set; } = ControlType.Touch;
    public ControlType NextControl { get; private set; }
    public bool IsInverted { get; private set; }
    public bool NextIsInverted { get; private set; }

    public event System.Action<ControlType> OnControlChanged;

    private float switchTimer;
    private bool shockSpawnedThisCycle;
    private int difficultyOrderIndex; // how far along difficultyOrder we currently are

    private void Awake()
    {
        // if (Instance != null && Instance != this)
        // {
        //     Debug.LogWarning("Duplicate ControlSwitcher — destroying " + gameObject.name, gameObject);
        //     Destroy(gameObject);
        //     return;
        // }

        Instance = this;

        SyncControlDifficultyEntries(); // safety net in case OnValidate never ran (e.g. instantiated at runtime)

        ControlDifficultyEntry initialEntry = RollNextEntry();
        NextControl = initialEntry.type;
        NextIsInverted = initialEntry.inverted;

        UpdateNextControlIcon();

        if (shockBangDelay > switchInterval)
            Debug.LogWarning("shockBangDelay is larger than switchInterval — the shock effect will spawn immediately every cycle. Lower shockBangDelay or raise switchInterval.");
    }

#if UNITY_EDITOR
    // Keeps controlDifficulties lined up with ControlType + invertibleControls
    // while you edit in the Inspector.
    private void OnValidate()
    {
        SyncControlDifficultyEntries();
    }
#endif

    // Rebuilds controlDifficulties so it has exactly one normal entry per
    // ControlType, plus one inverted entry per ControlType listed in
    // invertibleControls - preserving any difficulty already assigned, and
    // defaulting new entries to Easy. Entries for a type that's no longer in
    // invertibleControls are dropped automatically (they're simply not
    // re-added when the list is rebuilt).
    private void SyncControlDifficultyEntries()
    {
        ControlType[] allTypes = (ControlType[])System.Enum.GetValues(typeof(ControlType));
        List<ControlDifficultyEntry> updated = new List<ControlDifficultyEntry>(allTypes.Length * 2);

        foreach (ControlType type in allTypes)
        {
            updated.Add(FindOrCreateEntry(type, inverted: false));

            if (invertibleControls != null && System.Array.IndexOf(invertibleControls, type) >= 0)
                updated.Add(FindOrCreateEntry(type, inverted: true));
        }

        controlDifficulties = updated.ToArray();
    }

    private ControlDifficultyEntry FindOrCreateEntry(ControlType type, bool inverted)
    {
        ControlDifficultyEntry existing = null;
        if (controlDifficulties != null)
            existing = System.Array.Find(controlDifficulties, e => e != null && e.type == type && e.inverted == inverted);

        return existing ?? new ControlDifficultyEntry { type = type, inverted = inverted, difficulty = ControlDifficulty.Easy };
    }

    private void Start()
    {
        UpdatePanels();
    }

    private void Update()
    {
        switchTimer += Time.deltaTime;

        // Spawn the shock effect early enough that its bang frame lands exactly
        // when the switch happens below.
        if (!shockSpawnedThisCycle && switchTimer >= switchInterval - shockBangDelay)
        {
            SpawnShockEffect();
            shockSpawnedThisCycle = true;
        }

        if (switchTimer >= switchInterval)
        {
            SwitchControl();
            switchTimer = 0f;
            shockSpawnedThisCycle = false;
        }
    }

    private void SpawnShockEffect()
    {
        if (shockEffectPrefab == null)
            return;

        // Parented to this transform (the player), so it tracks the player's
        // position automatically for the whole lifetime of the effect.
        GameObject fx = Instantiate(shockEffectPrefab, transform.position, Quaternion.identity, transform);

        // Calls a parameterless "Play" method on whatever script sits on the
        // prefab's root, without needing to know its exact class name here.
        // If your prefab's script method is named differently, change "Play"
        // below to match (or tell me the script name and I'll wire it directly
        // with GetComponent<YourScript>().YourMethod() instead, which is a bit
        // faster and gives a clearer error if it's missing).
        fx.SendMessage("Play", SendMessageOptions.DontRequireReceiver);
    }

    private void SwitchControl()
    {
        CurrentControl = NextControl;
        IsInverted = NextIsInverted;

        ControlDifficultyEntry nextEntry = RollNextEntry();
        NextControl = nextEntry.type;
        NextIsInverted = nextEntry.inverted;

        UpdatePanels();
        UpdateNextControlIcon();

        Debug.Log("Current: " + CurrentControl + " (Inverted: " + IsInverted + ")" +
                   " | Next: " + NextControl + " (Inverted: " + NextIsInverted + ")");

        OnControlChanged?.Invoke(CurrentControl);
    }

    // Single source of truth for which control panel is visible. Computed purely
    // from state, so exactly one panel (the current control's, in its normal or
    // inverted variant) is active and everything else is off.
    private void UpdatePanels()
    {
        if (controlPanels == null)
            return;

        foreach (var panel in controlPanels)
        {
            if (panel == null)
                continue;

            bool isCurrent = panel.type == CurrentControl;
            // Fall back to the normal panel when this control has no inverted variant.
            bool showInverted = isCurrent && IsInverted && panel.invertedPanel != null;

            if (panel.normalPanel != null)
                panel.normalPanel.SetActive(isCurrent && !showInverted);
            if (panel.invertedPanel != null)
                panel.invertedPanel.SetActive(showInverted);
        }
    }

    private void UpdateNextControlIcon()
    {
        if (nextControlIcon == null)
            return;

        if (NextIsInverted)
        {
            // Use the dedicated inverted-joystick icon when available; otherwise fall
            // back to the shared inverted sprite (used by every other inverted control).
            nextControlIcon.sprite = (NextControl == ControlType.Joystick && joystickInvertedSprite != null)
                ? joystickInvertedSprite
                : invertedSprite;
        }
        else
        {
            nextControlIcon.sprite = GetSpriteFor(NextControl);
        }
    }

    private Sprite GetSpriteFor(ControlType type)
    {
        switch (type)
        {
            case ControlType.Tilt:     return tiltSprite;
            case ControlType.Touch:    return touchSprite;
            case ControlType.Follow:   return followSprite;
            case ControlType.Joystick: return joystickSprite;
            case ControlType.Slider:   return sliderSprite;
            case ControlType.Slingshot: return slingshotSprite;
            default: return null;
        }
    }

    // Picks the next (control, inverted) entry from whichever difficulty tier is
    // due next in difficultyOrder, rather than from every possibility equally.
    private ControlDifficultyEntry RollNextEntry()
    {
        ControlDifficulty tier = GetNextDifficultyTier();
        ControlDifficultyEntry[] pool = GetEntriesInDifficulty(tier);

        if (pool == null || pool.Length == 0)
        {
            Debug.LogWarning($"No controls are tagged with difficulty '{tier}' - falling back to picking from every control (normal + inverted) this time.");
            pool = controlDifficulties;
        }

        ControlDifficultyEntry picked;
        do
        {
            picked = pool[Random.Range(0, pool.Length)];
        }
        // Avoid repeating the exact same variant back-to-back (unless the pool
        // genuinely only has one option) - e.g. Touch -> Touch is skipped, but
        // Touch -> Touch (Inverted) is allowed since it's a different variant.
        while (pool.Length > 1 && picked.type == CurrentControl && picked.inverted == IsInverted);

        return picked;
    }

    // Walks difficultyOrder one step and wraps back to the start once it runs out,
    // so Easy, Medium, Hard, Easy, Medium, Hard, ... repeats forever.
    private ControlDifficulty GetNextDifficultyTier()
    {
        if (difficultyOrder == null || difficultyOrder.Length == 0)
            return ControlDifficulty.Easy; // sensible default if the array is left empty

        ControlDifficulty tier = difficultyOrder[difficultyOrderIndex % difficultyOrder.Length];
        difficultyOrderIndex++;
        return tier;
    }

    // All entries currently tagged with the given difficulty.
    private ControlDifficultyEntry[] GetEntriesInDifficulty(ControlDifficulty difficulty)
    {
        if (controlDifficulties == null)
            return null;

        List<ControlDifficultyEntry> matches = new List<ControlDifficultyEntry>();
        foreach (ControlDifficultyEntry entry in controlDifficulties)
        {
            if (entry != null && entry.difficulty == difficulty)
                matches.Add(entry);
        }

        return matches.ToArray();
    }

    // ----------------------------------------------------------------------
    // Public API - call these from any other script to change the difficulty
    // order, re-tag controls, or change which controls can invert, at runtime
    // (e.g. a difficulty-ramp manager that toughens things up over time).
    // ----------------------------------------------------------------------

    /// <summary>
    /// Replaces the forced difficulty sequence entirely, e.g.
    /// SetDifficultyOrder(new[] { ControlDifficulty.Medium, ControlDifficulty.Hard, ControlDifficulty.Hard }).
    /// By default this also restarts the sequence from its first entry
    /// (resetIndex: true) - pass false if you'd rather the new order pick up
    /// wherever the old one left off.
    /// </summary>
    public void SetDifficultyOrder(ControlDifficulty[] newOrder, bool resetIndex = true)
    {
        difficultyOrder = (newOrder != null && newOrder.Length > 0)
            ? (ControlDifficulty[])newOrder.Clone()
            : new[] { ControlDifficulty.Easy }; // never leave it empty - RollNextEntry needs at least one tier

        if (resetIndex)
            difficultyOrderIndex = 0;
    }

    /// <summary>Read-only copy of the current difficulty order, if you need to inspect it elsewhere.</summary>
    public ControlDifficulty[] GetDifficultyOrder() => (ControlDifficulty[])difficultyOrder.Clone();

    /// <summary>
    /// Re-tags a single control (or its Inverted variant) to a new difficulty, e.g.
    /// SetControlDifficulty(ControlType.Slingshot, ControlDifficulty.Hard)
    /// or SetControlDifficulty(ControlType.Touch, ControlDifficulty.Hard, inverted: true)
    /// to move "Touch (Inverted)" specifically into Hard.
    /// </summary>
    public void SetControlDifficulty(ControlType type, ControlDifficulty newDifficulty, bool inverted = false)
    {
        SyncControlDifficultyEntries(); // guarantees the entry exists first, if it's allowed to

        ControlDifficultyEntry entry = System.Array.Find(controlDifficulties, e => e.type == type && e.inverted == inverted);

        if (entry != null)
            entry.difficulty = newDifficulty;
        else if (inverted)
            Debug.LogWarning($"{type} has no Inverted entry because it isn't in invertibleControls - call SetInvertible({type}, true) first.");
    }

    /// <summary>
    /// Re-tags several controls/variants in one call, e.g.
    /// SetControlDifficulties((ControlType.Slider, ControlDifficulty.Hard, false), (ControlType.Touch, ControlDifficulty.Hard, true))
    /// </summary>
    public void SetControlDifficulties(params (ControlType type, ControlDifficulty difficulty, bool inverted)[] assignments)
    {
        if (assignments == null) return;

        foreach (var (type, difficulty, inverted) in assignments)
            SetControlDifficulty(type, difficulty, inverted);
    }

    /// <summary>Reads back which difficulty a control (or its Inverted variant) is currently tagged with.</summary>
    public ControlDifficulty GetControlDifficulty(ControlType type, bool inverted = false)
    {
        SyncControlDifficultyEntries();

        ControlDifficultyEntry entry = System.Array.Find(controlDifficulties, e => e.type == type && e.inverted == inverted);
        return entry != null ? entry.difficulty : ControlDifficulty.Easy;
    }

    /// <summary>
    /// Turns a control's Inverted variant on or off entirely. Turning it on adds
    /// an "Inverted" entry (tagged Easy until you re-tag it) for that control to
    /// controlDifficulties; turning it off removes that entry so it can never be
    /// rolled. This replaces the old inverseChance dice-roll - now whether (and
    /// how often) a control shows up inverted is fully controlled by whichever
    /// difficulty its Inverted entry is tagged with and how often that tier comes
    /// up in difficultyOrder.
    /// </summary>
    public void SetInvertible(ControlType type, bool canInvert)
    {
        List<ControlType> list = invertibleControls != null
            ? new List<ControlType>(invertibleControls)
            : new List<ControlType>();

        bool alreadyIn = list.Contains(type);

        if (canInvert && !alreadyIn)
            list.Add(type);
        else if (!canInvert && alreadyIn)
            list.Remove(type);

        invertibleControls = list.ToArray();
        SyncControlDifficultyEntries(); // adds/removes the Inverted entry to match
    }
}