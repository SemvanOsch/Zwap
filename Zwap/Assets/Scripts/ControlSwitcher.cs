using UnityEngine;
using UnityEngine.UI;

public enum ControlType
{
    Tilt,
    Touch
}

[System.Serializable]
public class ControlPanel
{
    public ControlType type;
    public GameObject normalPanel;
    public GameObject invertedPanel;   // optional; leave null if this control can't invert
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

    [Header("Next Control Icon")]
    [SerializeField] private Image nextControlIcon;
    [SerializeField] private Sprite tiltSprite;
    [SerializeField] private Sprite touchSprite;
    [SerializeField] private Sprite invertedSprite;

    [Header("Inverse")]
    [Range(0f, 1f)]
    [SerializeField] private float inverseChance = 0.3f;
    [SerializeField] private ControlType[] invertibleControls = { ControlType.Touch };

    public ControlType CurrentControl { get; private set; } = ControlType.Touch;
    public ControlType NextControl { get; private set; }
    public bool IsInverted { get; private set; }
    public bool NextIsInverted { get; private set; }

    public event System.Action<ControlType> OnControlChanged;

    private float switchTimer;
    private bool shockSpawnedThisCycle;

    private void Awake()
    {
        // if (Instance != null && Instance != this)
        // {
        //     Debug.LogWarning("Duplicate ControlSwitcher — destroying " + gameObject.name, gameObject);
        //     Destroy(gameObject);
        //     return;
        // }

        Instance = this;

        NextControl = RollNextControl();
        NextIsInverted = RollInverted(NextControl);

        UpdateNextControlIcon();

        if (shockBangDelay > switchInterval)
            Debug.LogWarning("shockBangDelay is larger than switchInterval — the shock effect will spawn immediately every cycle. Lower shockBangDelay or raise switchInterval.");
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

        NextControl = RollNextControl();
        NextIsInverted = RollInverted(NextControl);

        UpdatePanels();
        UpdateNextControlIcon();

        Debug.Log("Current: " + CurrentControl + " (Inverted: " + IsInverted + ")" +
                   " | Next: " + NextControl + " (Inverted: " + NextIsInverted + ")");

        OnControlChanged?.Invoke(CurrentControl);
    }

    private bool RollInverted(ControlType type)
    {
        bool canInvert = System.Array.IndexOf(invertibleControls, type) >= 0;
        return canInvert && Random.value < inverseChance;
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

        nextControlIcon.sprite = NextIsInverted ? invertedSprite : GetSpriteFor(NextControl);
    }

    private Sprite GetSpriteFor(ControlType type)
    {
        switch (type)
        {
            case ControlType.Tilt:  return tiltSprite;
            case ControlType.Touch: return touchSprite;
            default: return null;
        }
    }

    private ControlType RollNextControl()
    {
        ControlType[] allTypes = (ControlType[])System.Enum.GetValues(typeof(ControlType));
        ControlType picked;

        do
        {
            picked = allTypes[Random.Range(0, allTypes.Length)];
        }
        while (picked == CurrentControl);

        return picked;
    }
}