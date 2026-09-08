using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TouchControls : MonoBehaviour
{
    public PlayerStart playerStart;

    // Which arrows are currently held. The active movement vector is recomputed
    // from these every change, so a missed press/release (e.g. a finger held while
    // the panel is toggled off during a control switch) can never strand a value.
    private bool upHeld, downHeld, leftHeld, rightHeld;

    private bool subscribed;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        // Safety net in case ControlSwitcher.Instance wasn't set during OnEnable.
        TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (subscribed || ControlSwitcher.Instance == null)
            return;

        ControlSwitcher.Instance.OnControlChanged += HandleControlChanged;
        subscribed = true;
    }

    private void OnDisable()
    {
        // Panel hidden (e.g. switching to tilt) while an arrow may still be held:
        // clear our state and the player's input so nothing carries over.
        ClearHeld();

        if (subscribed && ControlSwitcher.Instance != null)
            ControlSwitcher.Instance.OnControlChanged -= HandleControlChanged;
        subscribed = false;
    }

    private void HandleControlChanged(ControlType newControl)
    {
        // This component lives on a different GameObject than the arrow panel that
        // gets toggled, so its OnDisable won't fire on a switch. Clearing here makes
        // sure a held arrow (whose release was swallowed when the panel hid) can't
        // leave a flag stuck true.
        ClearHeld();
    }

    private void ClearHeld()
    {
        upHeld = downHeld = leftHeld = rightHeld = false;
        if (playerStart != null)
            playerStart.ResetInput();
    }

    private void PushInput()
    {
        if (playerStart == null)
            return;

        Vector2 dir = Vector2.zero;
        if (upHeld)    dir += Vector2.up;
        if (downHeld)  dir += Vector2.down;
        if (leftHeld)  dir += Vector2.left;
        if (rightHeld) dir += Vector2.right;

        // Each panel's buttons are bound to the logically-correct handler (the
        // inverted panel's down-pointing arrow calls OnDownPress, etc.), so the
        // inversion is already encoded in the wiring — no negation needed here.
        playerStart.SetTouchInput(dir);
    }

    public void OnUpPress()      { upHeld = true;     PushInput(); }
    public void OnUpRelease()    { upHeld = false;    PushInput(); }

    public void OnDownPress()    { downHeld = true;   PushInput(); }
    public void OnDownRelease()  { downHeld = false;  PushInput(); }

    public void OnLeftPress()    { leftHeld = true;   PushInput(); }
    public void OnLeftRelease()  { leftHeld = false;  PushInput(); }

    public void OnRightPress()   { rightHeld = true;  PushInput(); }
    public void OnRightRelease() { rightHeld = false; PushInput(); }
}