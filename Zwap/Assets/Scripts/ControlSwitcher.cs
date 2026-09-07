using UnityEngine;
using UnityEngine.UI;

public enum ControlType
{
    Tilt,
    Touch
}

public class ControlSwitcher : MonoBehaviour
{
    public static ControlSwitcher Instance;

    [Header("Switching")]
    [SerializeField] private float switchInterval = 10f;
    [SerializeField] private TouchControls touchControls;

    [Header("Touch UI")]
    [SerializeField] private GameObject touchUIPanel;

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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        NextControl = RollNextControl();
        NextIsInverted = RollInverted(NextControl);

        UpdateNextControlIcon();
    }

    private void Start()
    {
        UpdateTouchUIVisibility();
    }

    private void Update()
    {
        switchTimer += Time.deltaTime;

        if (switchTimer >= switchInterval)
        {
            SwitchControl();
            switchTimer = 0f;
        }
    }

    private void SwitchControl()
    {
        CurrentControl = NextControl;

        if (NextIsInverted != IsInverted)
        {
            IsInverted = NextIsInverted;

            if (touchControls != null)
                touchControls.ToggleInverse();
        }

        NextControl = RollNextControl();
        NextIsInverted = RollInverted(NextControl);

        UpdateTouchUIVisibility();
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

    private void UpdateTouchUIVisibility()
    {
        if (touchUIPanel != null)
            touchUIPanel.SetActive(CurrentControl == ControlType.Touch);
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