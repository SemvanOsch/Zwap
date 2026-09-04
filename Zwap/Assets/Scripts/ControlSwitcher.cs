using UnityEngine;

public enum ControlType
{
    Keyboard,
    Tilt,
    Touch
}

public class ControlSwitcher : MonoBehaviour
{
    public static ControlSwitcher Instance;

    [Header("Switching")]
    [SerializeField] private float switchInterval = 10f;
    [SerializeField] private TouchControls touchControls;

    [Header("Inverse")]
    [Range(0f, 1f)]
    [SerializeField] private float inverseChance = 0.3f;
    [SerializeField] private ControlType[] invertibleControls = { ControlType.Touch };

    public ControlType CurrentControl { get; private set; } = ControlType.Keyboard;
    public ControlType NextControl { get; private set; }
    public bool IsInverted { get; private set; }

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
        NextControl = RollNextControl();

        bool canInvert = System.Array.IndexOf(invertibleControls, CurrentControl) >= 0;
        bool shouldInvert = canInvert && Random.value < inverseChance;

        if (shouldInvert != IsInverted)
        {
            IsInverted = shouldInvert;

            if (touchControls != null)
                touchControls.ToggleInverse();
        }

        Debug.Log("Current: " + CurrentControl + " | Next: " + NextControl + " | Inverted: " + IsInverted);

        OnControlChanged?.Invoke(CurrentControl);
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