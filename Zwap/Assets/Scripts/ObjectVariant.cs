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

        [Tooltip("One or more sprite options for this control. Each spawned object randomly picks one of these (by index) and uses it for its whole life.")]
        public Sprite[] normalSprites;

        [Tooltip("Optional inverted-variant options, matched by the same random index. Leave empty to reuse the normal sprite when inverted.")]
        public Sprite[] invertedSprites;
    }

    [Tooltip("One entry per control. Each entry can hold several sprite options; a random one is chosen per spawned object. invertedSprites are optional and only used when that control is in its inverted variant.")]
    [SerializeField] private SkinEntry[] skins;

    [Tooltip("Point in the reveal (0..1) at which the sprite snaps to the new skin, so it changes under the growing circle. 0.5 = the circle's midpoint.")]
    [Range(0f, 1f)]
    [SerializeField] private float swapAtRevealProgress = 0.5f;

    [Header("Hitbox")]
    [Tooltip("Auto-resize this object's Collider2D to match whichever sprite is showing, so slightly-different-sized skins all get a correct hitbox. Turn off to keep the collider fixed as authored in the prefab.")]
    [SerializeField] private bool autoFitCollider = true;

    [Tooltip("Shrinks the auto-fitted hitbox relative to the sprite. 1 = exact sprite bounds; 0.85 leaves the hitbox slightly inside the rock, which usually feels fairer.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float hitboxScale = 1f;

    private SpriteRenderer sr;
    private Collider2D fitCollider; // resized to match the current sprite when autoFitCollider is on
    private Sprite defaultSprite;   // fallback when a control has no entry
    private bool subscribedToSwitcher;
    private bool subscribedToReveal;

    // Which sprite option this spawned instance uses, chosen once in Awake and reused for
    // every dimension so the object keeps a consistent identity while it changes skin.
    // Clamped per-entry in GetSpriteFor, so entries with fewer options still work.
    private int variantIndex;

    // Pending swap state while a reveal is in progress.
    private bool waitingForReveal;
    private Sprite pendingSprite;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        fitCollider = GetComponent<Collider2D>();
        defaultSprite = sr.sprite; // whatever the prefab ships with

        // Roll a single random index against the largest option set, then clamp it down
        // per entry. This way an entry offering 3 sprites gets an even 0/1/2 spread, while
        // an entry with only 1 always resolves to that one.
        int maxOptions = 0;
        if (skins != null)
        {
            foreach (var entry in skins)
            {
                if (entry?.normalSprites != null && entry.normalSprites.Length > maxOptions)
                    maxOptions = entry.normalSprites.Length;
            }
        }
        variantIndex = maxOptions > 0 ? Random.Range(0, maxOptions) : 0;
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
            SetSprite(target);
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
            SetSprite(pendingSprite);
            waitingForReveal = false;
        }
    }

    public  void ApplySkin(ControlType type, bool inverted)
    {
        Sprite target = GetSpriteFor(type, inverted);
        if (target != null)
            SetSprite(target);
    }

    // Central sprite assignment: also re-fits the collider so the hitbox tracks the
    // sprite that's actually on screen.
    private void SetSprite(Sprite sprite)
    {
        sr.sprite = sprite;
        FitColliderToSprite(sprite);
    }

    // Resizes the Collider2D to the sprite's local bounds (scaled by hitboxScale). Runs in
    // the object's local space, so the transform's own scale still applies on top — the
    // capsule also rotates with the object, so RockRotation's spin stays correct.
    private void FitColliderToSprite(Sprite sprite)
    {
        if (!autoFitCollider || fitCollider == null || sprite == null) return;

        Vector2 size = (Vector2)sprite.bounds.size * hitboxScale;
        Vector2 offset = sprite.bounds.center; // handles sprites whose pivot isn't centered

        switch (fitCollider)
        {
            case CapsuleCollider2D capsule:
                capsule.size = size;
                capsule.offset = offset;
                break;
            case BoxCollider2D box:
                box.size = size;
                box.offset = offset;
                break;
            case CircleCollider2D circle:
                circle.radius = Mathf.Max(size.x, size.y) * 0.5f;
                circle.offset = offset;
                break;
        }
    }

    private Sprite GetSpriteFor(ControlType type, bool inverted)
    {
        if (skins == null) return defaultSprite;

        foreach (var entry in skins)
        {
            if (entry == null || entry.type != type)
                continue;

            // Fall back to the normal sprite when this control has no inverted one.
            if (inverted && entry.invertedSprites != null && entry.invertedSprites.Length > 0)
                return entry.invertedSprites[Mathf.Min(variantIndex, entry.invertedSprites.Length - 1)];

            if (entry.normalSprites != null && entry.normalSprites.Length > 0)
                return entry.normalSprites[Mathf.Min(variantIndex, entry.normalSprites.Length - 1)];

            return defaultSprite;
        }

        // No entry for this control at all: keep the prefab's default sprite.
        return defaultSprite;
    }
}
