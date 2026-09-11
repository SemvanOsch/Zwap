using UnityEngine;
using UnityEngine.UI;

// Swaps the scrolling background to match whichever control is currently active.
// Lives on its own so BackgroundScroller stays focused on scrolling; this component
// only decides *which* texture is shown. It subscribes to ControlSwitcher exactly
// like PlayerStart/TouchControls do (TrySubscribe in OnEnable/Start, unsubscribe in
// OnDisable) so it survives script init-order differences.
public class ControlBackground : MonoBehaviour
{
    [System.Serializable]
    public class BackgroundEntry
    {
        public ControlType type;
        public Texture normalBackground;
        public Texture invertedBackground; // optional; leave null if this control can't invert
    }

    [Tooltip("The RawImage BackgroundScroller scrolls. Its texture is what we swap.")]
    [SerializeField] private RawImage img;

    [Tooltip("One entry per control. invertedBackground is optional and only used when that control is in its inverted variant.")]
    [SerializeField] private BackgroundEntry[] backgrounds;

    [Tooltip("Optional. If assigned, control switches play the growing-circle reveal instead of an instant swap. The very first background (at startup) is always instant.")]
    [SerializeField] private BackgroundReveal reveal;

    private bool subscribed;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        // Safety net in case ControlSwitcher.Instance wasn't set during OnEnable.
        TrySubscribe();

        // OnControlChanged only fires on a *switch*, never for the starting control,
        // so apply the current control's background once at startup — instantly, since
        // there's no "previous" map to reveal from.
        if (ControlSwitcher.Instance != null)
            ApplyFor(ControlSwitcher.Instance.CurrentControl, ControlSwitcher.Instance.IsInverted, animate: false);
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
        if (subscribed && ControlSwitcher.Instance != null)
            ControlSwitcher.Instance.OnControlChanged -= HandleControlChanged;
        subscribed = false;
    }

    private void HandleControlChanged(ControlType newControl)
    {
        // The event only carries the ControlType, so read the inverted flag straight
        // from the switcher (it's already updated by the time this fires).
        bool inverted = ControlSwitcher.Instance != null && ControlSwitcher.Instance.IsInverted;
        ApplyFor(newControl, inverted, animate: true);
    }

    private void ApplyFor(ControlType type, bool inverted, bool animate)
    {
        Texture tex = GetTextureFor(type, inverted);
        if (tex != null)
            ApplyBackground(tex, animate);
    }

    private Texture GetTextureFor(ControlType type, bool inverted)
    {
        if (backgrounds == null)
            return null;

        foreach (var entry in backgrounds)
        {
            if (entry == null || entry.type != type)
                continue;

            // Fall back to the normal background when this control has no inverted one.
            if (inverted && entry.invertedBackground != null)
                return entry.invertedBackground;

            return entry.normalBackground;
        }

        return null;
    }

    // Single choke point for changing the visible background. With a BackgroundReveal
    // assigned, a switch plays the growing-circle transition; otherwise (or for the
    // startup application) it's an instant texture swap.
    private void ApplyBackground(Texture tex, bool animate)
    {
        if (animate && reveal != null)
        {
            reveal.Reveal(tex);
            return;
        }

        if (img != null)
            img.texture = tex;
    }
}
