using UnityEngine;

// Slowly spins the rock, but ONLY while the reverse (inverted) joystick control is the
// active dimension. Put this on the rock prefab only — nothing else should rotate.
//
// Timing is deliberately linked to the rock's skin change: the rock's colour/skin swaps
// via BackgroundReveal's growing circle (see RockWaterEffect), which only "commits" the
// new look when OnRevealProgress fires 1f. We flip the spin on/off at that same commit,
// so the rock starts/stops rotating exactly when it visually becomes (or leaves) the
// reverse-joystick dimension. The flip itself is instant — there's no easing of the spin
// speed, just the small reveal delay before it kicks in.
//
// When a rock is spawned while reverse-joystick is already active, it starts spinning
// straight away (no reveal is playing for it, and it already shows that skin).
public class RockRotation : MonoBehaviour
{
    [Tooltip("Spin speed in degrees per second while the reverse joystick is active. " +
             "Negative spins the other way.")]
    [SerializeField] private float rotationSpeed = 30f;

    private bool rotating;          // are we currently spinning?
    private bool pendingRotating;   // the target state, waiting on the reveal to commit
    private bool awaitingReveal;    // a control change is mid-reveal; apply on commit

    private bool subscribedToSwitcher;
    private bool subscribedToReveal;

    private void OnEnable()
    {
        TrySubscribeSwitcher();
        TrySubscribeReveal();
    }

    private void Start()
    {
        // Safety net in case the singletons weren't ready during OnEnable (script init
        // order), matching how RockWaterEffect subscribes.
        TrySubscribeSwitcher();
        TrySubscribeReveal();

        // A freshly spawned rock already shows the current dimension's skin, so apply the
        // matching spin state immediately — no reveal is running just for this spawn.
        if (ControlSwitcher.Instance != null)
            ApplyRotating(IsReverseJoystick());
        else
            ApplyRotating(false);
    }

    private void OnDisable()
    {
        if (subscribedToSwitcher && ControlSwitcher.Instance != null)
            ControlSwitcher.Instance.OnControlChanged -= HandleControlChanged;
        subscribedToSwitcher = false;

        if (subscribedToReveal && BackgroundReveal.Instance != null)
            BackgroundReveal.Instance.OnRevealProgress -= HandleRevealProgress;
        subscribedToReveal = false;
    }

    private void TrySubscribeSwitcher()
    {
        if (subscribedToSwitcher || ControlSwitcher.Instance == null) return;
        ControlSwitcher.Instance.OnControlChanged += HandleControlChanged;
        subscribedToSwitcher = true;
    }

    private void TrySubscribeReveal()
    {
        if (subscribedToReveal || BackgroundReveal.Instance == null) return;
        BackgroundReveal.Instance.OnRevealProgress += HandleRevealProgress;
        subscribedToReveal = true;
    }

    // "Reverse joycon" = the inverted variant of the on-screen joystick control.
    private bool IsReverseJoystick()
    {
        var s = ControlSwitcher.Instance;
        return s != null && s.CurrentControl == ControlType.Joystick && s.IsInverted;
    }

    private void HandleControlChanged(ControlType newControl)
    {
        bool desired = newControl == ControlType.Joystick &&
                       ControlSwitcher.Instance != null && ControlSwitcher.Instance.IsInverted;

        // Turning OFF is always instant: winding the spin down over the reveal and then
        // snapping upright looked goofy. Stop and snap upright right away instead.
        if (!desired)
        {
            awaitingReveal = false; // cancel any pending turn-on that hasn't landed yet
            ApplyRotating(false);
            return;
        }

        // Turning ON stays linked to the skin change. No reveal in the scene: switch
        // instantly, mirroring RockWaterEffect's fallback.
        if (BackgroundReveal.Instance == null)
        {
            ApplyRotating(true);
            return;
        }

        // Otherwise wait for the reveal to finish (OnRevealProgress == 1f) so the spin
        // starts in lockstep with the skin actually landing on the reverse-joystick dimension.
        pendingRotating = true;
        awaitingReveal = true;
    }

    private void HandleRevealProgress(float t)
    {
        if (!awaitingReveal) return;
        if (t >= 1f)
        {
            ApplyRotating(pendingRotating);
            awaitingReveal = false;
        }
    }

    private void ApplyRotating(bool value)
    {
        rotating = value;
        if (!rotating)
            transform.rotation = Quaternion.identity; // snap back upright instantly
    }

    private void Update()
    {
        if (rotating)
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }
}
