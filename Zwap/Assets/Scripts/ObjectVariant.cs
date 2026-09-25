using UnityEngine;

// Swaps a falling object's sprite ("skin") to match whichever control is currently
// active, mirroring how ControlBackground swaps the background texture and how
// RockWaterEffect swaps the swirl color. Lives on the collidable prefabs (Rock,
// Tree Stump). Each prefab carries its own skin set, so different objects can look
// different in every control dimension.
//
// Timing follows RockWaterEffect: the skin swaps "under" the growing-circle reveal
// (at its midpoint) so it changes in sync with the background. If there's no
// BackgroundReveal in the scene, it swaps instantly instead.
[RequireComponent(typeof(SpriteRenderer))]
public class ObjectVariant : MonoBehaviour
{
    [System.Serializable]
    public class SkinEntry
    {
        public ControlType type;
        public Sprite normalSprite;
        public Sprite invertedSprite; // optional; leave null to reuse normalSprite when inverted
    }

    [Tooltip("One entry per control. invertedSprite is optional and only used when that control is in its inverted variant; leave it empty to reuse the normal sprite.")]
    [SerializeField] private SkinEntry[] skins;

    [Tooltip("Point in the reveal (0..1) at which the sprite snaps to the new skin, so it changes under the growing circle. 0.5 = the circle's midpoint.")]
    [Range(0f, 1f)]
    [SerializeField] private float swapAtRevealProgress = 0.5f;

    private SpriteRenderer sr;
    private Sprite defaultSprite;   // fallback when a control has no entry
    private bool subscribedToSwitcher;
    private bool subscribedToReveal;

    // Pending swap state while a reveal is in progress.
    private bool waitingForReveal;
    private Sprite pendingSprite;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        defaultSprite = sr.sprite; // whatever the prefab ships with
    }

    private void OnEnable()
    {
        TrySubscribeSwitcher();
        TrySubscribeReveal();
    }

    private void Start()
    {
        // Safety net: if ControlSwitcher/BackgroundReveal weren't set during OnEnable
        // (script init order), subscribe now that every Awake has run.
        TrySubscribeSwitcher();
        TrySubscribeReveal();

        // OnControlChanged only fires on a *switch*, never for the control that's
        // already active when this object spawns — so apply the current skin instantly
        // at spawn, otherwise a freshly spawned object shows the wrong skin until the
        // next switch.
        if (ControlSwitcher.Instance != null)
            ApplySkin(ControlSwitcher.Instance.CurrentControl, ControlSwitcher.Instance.IsInverted);
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

    private void HandleControlChanged(ControlType newControl)
    {
        Debug.Log($"[ObjectVariant] {name} got control change: {newControl}", this);
        // The event only carries the ControlType; read IsInverted straight from the
        // switcher (already updated by the time this fires), exactly like the others.
        bool inverted = ControlSwitcher.Instance != null && ControlSwitcher.Instance.IsInverted;
        Sprite target = GetSpriteFor(newControl, inverted);
        if (target == null) return; // no entry for this control: keep whatever we have

        if (BackgroundReveal.Instance == null)
        {
            // No reveal in the scene: swap immediately.
            sr.sprite = target;
            return;
        }

        // Defer the swap to the reveal's midpoint so it changes under the circle.
        pendingSprite = target;
        waitingForReveal = true;
    }

    private void HandleRevealProgress(float t)
    {
        if (!waitingForReveal) return;
        if (t >= swapAtRevealProgress)
        {
            sr.sprite = pendingSprite;
            waitingForReveal = false;
        }
    }

    public  void ApplySkin(ControlType type, bool inverted)
    {
        Sprite target = GetSpriteFor(type, inverted);
        if (target != null)
            sr.sprite = target;
    }

    private Sprite GetSpriteFor(ControlType type, bool inverted)
    {
        if (skins == null) return defaultSprite;

        foreach (var entry in skins)
        {
            if (entry == null || entry.type != type)
                continue;

            // Fall back to the normal sprite when this control has no inverted one.
            if (inverted && entry.invertedSprite != null)
                return entry.invertedSprite;

            return entry.normalSprite != null ? entry.normalSprite : defaultSprite;
        }

        // No entry for this control at all: keep the prefab's default sprite.
        return defaultSprite;
    }
}
