using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Swaps every "map" layer (the scrolling background AND the scrolling rock side
// borders) to match whichever control is currently active. On a switch it plays the
// growing-circle reveal via BackgroundReveal, so all layers change together, clipped to
// one circle. At startup it applies the current map instantly (no "previous" map to
// reveal from). Subscribes to ControlSwitcher exactly like PlayerStart/TouchControls do
// (TrySubscribe in OnEnable/Start, unsubscribe in OnDisable) so it survives script
// init-order differences.
public class ControlBackground : MonoBehaviour
{
    [System.Serializable]
    public class BackgroundEntry
    {
        public ControlType type;
        public Texture normalBackground;
        public Texture invertedBackground; // optional; leave null if this control can't invert
    }

    [System.Serializable]
    public class RevealLayer
    {
        [Tooltip("Label only, for clarity in the Inspector (e.g. Background, RockLeft, RockRight).")]
        public string name;

        [Tooltip("The base/old RawImage shown normally and scrolled by BackgroundScroller.")]
        public RawImage baseImage;

        [Tooltip("The overlay RawImage under the RevealCircle mask that shows the upcoming texture for THIS layer during the wipe. Leave empty and this layer just swaps instantly.")]
        public RawImage overlay;

        [Tooltip("Per-control textures for THIS layer. invertedBackground is optional and only used when that control is in its inverted variant.")]
        public BackgroundEntry[] perControl;
    }

    [Tooltip("One entry per map layer: background, rock-left, rock-right, etc.")]
    [SerializeField] private RevealLayer[] layers;

    [Tooltip("Optional. If assigned, control switches play the growing-circle reveal instead of an instant swap. The first application at startup is always instant.")]
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

        // Overlays should show nothing until a reveal runs.
        if (layers != null)
        {
            foreach (var layer in layers)
            {
                if (layer != null && layer.overlay != null)
                    layer.overlay.gameObject.SetActive(false);
            }
        }

        // OnControlChanged only fires on a *switch*, never for the starting control, so
        // apply the current control's map once at startup — instantly, since there's no
        // "previous" map to reveal from.
        if (ControlSwitcher.Instance != null)
            Apply(ControlSwitcher.Instance.CurrentControl, ControlSwitcher.Instance.IsInverted, animate: false);
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
        Apply(newControl, inverted, animate: true);
    }

    private void Apply(ControlType type, bool inverted, bool animate)
    {
        if (layers == null)
            return;

        // Animated path: hand every layer to the reveal so they wipe together.
        if (animate && reveal != null)
        {
            var requests = new List<BackgroundReveal.RevealRequest>();

            foreach (var layer in layers)
            {
                if (layer == null || layer.baseImage == null)
                    continue;

                Texture tex = GetTextureFor(layer.perControl, type, inverted);
                if (tex == null)
                    continue;

                requests.Add(new BackgroundReveal.RevealRequest(layer.baseImage, layer.overlay, tex));
            }

            if (requests.Count > 0)
                reveal.Reveal(requests.ToArray());

            return;
        }

        // Instant path (startup, or no reveal assigned).
        foreach (var layer in layers)
        {
            if (layer == null || layer.baseImage == null)
                continue;

            Texture tex = GetTextureFor(layer.perControl, type, inverted);
            if (tex != null)
                layer.baseImage.texture = tex;
        }
    }

    private Texture GetTextureFor(BackgroundEntry[] entries, ControlType type, bool inverted)
    {
        if (entries == null)
            return null;

        foreach (var entry in entries)
        {
            if (entry == null || entry.type != type)
                continue;

            // Fall back to the normal texture when this control has no inverted one.
            if (inverted && entry.invertedBackground != null)
                return entry.invertedBackground;

            return entry.normalBackground;
        }

        return null;
    }
}
