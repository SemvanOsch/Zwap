using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Growing-circle map transition. A circle expands from the fish's position; inside the
// circle the NEW map is shown, outside it the OLD map stays, until the circle covers
// the whole screen and the swap is committed.
//
// This reveals any number of LAYERS at once (background + rock side borders + ...),
// all clipped to the same circle so they change in perfect sync. Each layer is a pair
// of RawImages: a base/old image shown normally, and an overlay copy parented under the
// mask. The caller (ControlBackground) supplies these pairs plus the target texture per
// layer via RevealRequest.
//
// Setup:
//   Canvas
//   ├─ Background   (RawImage)          = a layer's base image (scrolled)
//   ├─ RockLeft     (RawImage)          = a layer's base image (scrolled)
//   ├─ RockRight    (RawImage)          = a layer's base image (scrolled)
//   └─ RevealCircle (Image + Mask)      = circle (Image uses a circle sprite; Mask's
//      ├─ NewBackground (RawImage)         "Show Mask Graphic" OFF). Each overlay is a
//      ├─ NewRockLeft   (RawImage)         DIRECT child of the circle so it's clipped.
//      └─ NewRockRight  (RawImage)
//
// The circle grows via its RectTransform sizeDelta (NOT localScale) so the masked
// overlays are clipped, never zoomed. Each overlay is matched to its own base image's
// rect and kept in scroll sync every frame.
public class BackgroundReveal : MonoBehaviour
{
    public static BackgroundReveal Instance;

    // One layer to reveal: its base/old image, the masked overlay copy, and the texture
    // the overlay is transitioning to. Built at runtime by ControlBackground.
    public struct RevealRequest
    {
        public RawImage baseImage;
        public RawImage overlay;
        public Texture newTexture;

        public RevealRequest(RawImage baseImage, RawImage overlay, Texture newTexture)
        {
            this.baseImage = baseImage;
            this.overlay = overlay;
            this.newTexture = newTexture;
        }
    }

    [Header("Circle")]
    [Tooltip("RectTransform of the masked circle (Image + Mask). Its pivot and anchor must both be centered. Grown via sizeDelta.")]
    [SerializeField] private RectTransform circle;

    [Tooltip("Generate a crisp high-res circle sprite at startup and assign it to the circle's Image, instead of using the low-res built-in 'Knob' sprite. Leave on to avoid pixelated edges.")]
    [SerializeField] private bool generateCircleSprite = true;

    [Tooltip("Optional. The Image on the RevealCircle whose sprite gets replaced. Falls back to the Image on the circle object if left empty.")]
    [SerializeField] private Image circleImage;

    [Tooltip("Resolution of the generated circle sprite. Higher = crisper when scaled to fill the screen.")]
    [SerializeField] private int circleResolution = 512;

    [Header("References")]
    [Tooltip("The Canvas's RectTransform, used to convert the fish position to canvas space and to place the overlays.")]
    [SerializeField] private RectTransform canvasRect;

    [Tooltip("The fish/player transform — the circle grows from here.")]
    [SerializeField] private Transform fish;

    [Tooltip("Camera used to project the fish to screen space. Falls back to Camera.main if left empty.")]
    [SerializeField] private Camera cam;

    [Header("Timing")]
    [Tooltip("How long the circle takes to cover the screen, in seconds.")]
    [SerializeField] private float duration = 0.6f;

    [Tooltip("Eases the circle's growth (x = time 0..1, y = progress 0..1).")]
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Extra margin on the final radius so the circle fully clears the farthest corner.")]
    [SerializeField] private float radiusMargin = 1.05f;

    private Coroutine routine;
    private RevealRequest[] active; // the layers of the reveal currently in progress

    // Fires every frame during a reveal with the same eased 0..1 fraction that drives
    // the circle's size, and once more with exactly 1f on commit. Other systems (e.g.
    // a rock's water-swirl color, falling-object skins) subscribe to this to stay in
    // lockstep with the circle's visual growth instead of switching instantly.
    public event System.Action<float> OnRevealProgress;

    private void Awake()
    {
        Instance = this;

        // Swap the low-res built-in circle for a crisp generated one so the edge stays
        // smooth when the circle is scaled up to cover the screen.
        if (generateCircleSprite)
        {
            if (circleImage == null && circle != null)
                circleImage = circle.GetComponent<Image>();

            if (circleImage != null)
                circleImage.sprite = BuildCircleSprite(circleResolution);
        }

        // Start hidden — no reveal in progress. The circle GameObject stays ACTIVE
        // (this script lives on it and must be able to run coroutines); collapsing it
        // to size 0 makes its Mask clip the overlays to nothing, so it shows nothing.
        if (circle != null) circle.sizeDelta = Vector2.zero;
    }

    // Kick off (or fast-forward then restart) a circle reveal for the given layers.
    public void Reveal(RevealRequest[] requests)
    {
        if (requests == null || requests.Length == 0)
            return;

        // Missing any piece needed for the animation -> just swap instantly so the
        // game never gets stuck on the wrong map.
        if (circle == null || canvasRect == null || fish == null)
        {
            InstantApply(requests);
            return;
        }

        // A reveal is already playing: commit it immediately so its targets aren't lost,
        // then start the new one from the finished state.
        if (routine != null)
        {
            StopCoroutine(routine);
            Commit();
        }

        // Nothing to do if every layer already shows its target.
        bool anyChange = false;
        foreach (var r in requests)
        {
            if (r.baseImage != null && r.newTexture != null && r.baseImage.texture != r.newTexture)
            {
                anyChange = true;
                break;
            }
        }
        if (!anyChange)
            return;

        routine = StartCoroutine(RevealRoutine(requests));
    }

    // Immediately set every layer's base texture, no animation (used at startup and as
    // the fallback when the reveal can't run).
    public void InstantApply(RevealRequest[] requests)
    {
        if (requests == null)
            return;

        foreach (var r in requests)
        {
            if (r.baseImage != null && r.newTexture != null)
                r.baseImage.texture = r.newTexture;
        }
    }

    private IEnumerator RevealRoutine(RevealRequest[] requests)
    {
        active = requests;

        // Freeze the circle's center on the fish (in canvas space) for the whole reveal.
        Vector2 center = FishCanvasPoint();
        circle.anchoredPosition = center;

        // Prepare each overlay: show its new texture, and match its rect EXACTLY to its
        // own base image so the new map lines up 1:1 (same scale, framing and position).
        foreach (var r in requests)
        {
            if (r.overlay == null || r.baseImage == null)
                continue;

            r.overlay.texture = r.newTexture;
            r.overlay.color = Color.white; // opaque; otherwise the old map shows through
            r.overlay.uvRect = r.baseImage.uvRect;

            MatchRect(r.overlay.rectTransform, r.baseImage.rectTransform);

            r.overlay.gameObject.SetActive(true);
        }

        float diameter = MaxRadiusToCorners(center) * 2f * radiusMargin;
        circle.sizeDelta = Vector2.zero;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
            float eased = ease.Evaluate(k);
            circle.sizeDelta = new Vector2(eased * diameter, eased * diameter);
            OnRevealProgress?.Invoke(eased);

            // Keep every overlay in scroll sync with its base so the reveal is seamless.
            foreach (var r in requests)
            {
                if (r.overlay != null && r.baseImage != null)
                    r.overlay.uvRect = r.baseImage.uvRect;
            }
            yield return null;
        }

        Commit();
    }

    // Finish a reveal: each new map becomes its base, and the overlays are hidden so
    // we're back to the cheap single-image steady state.
    private void Commit()
    {
        if (active != null)
        {
            foreach (var r in active)
            {
                if (r.baseImage != null && r.newTexture != null)
                    r.baseImage.texture = r.newTexture;
                if (r.overlay != null)
                    r.overlay.gameObject.SetActive(false);
            }
        }
        active = null;

        OnRevealProgress?.Invoke(1f); // guarantee listeners land exactly on the final value

        // Collapse the circle (its Mask then shows nothing) but keep it active so this
        // script can start the next reveal's coroutine.
        if (circle != null)
            circle.sizeDelta = Vector2.zero;

        routine = null;
    }

    // Builds a white, anti-aliased filled circle as a Sprite. Used as the mask shape;
    // being high-res keeps the reveal edge round and clean at any on-screen size.
    private Sprite BuildCircleSprite(int size)
    {
        size = Mathf.Max(8, size);

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float r = size * 0.5f;
        const float edge = 1.5f; // anti-alias width in pixels
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r;
                float dy = y + 0.5f - r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                // 1 inside the circle, fading to 0 across `edge` pixels at the rim.
                float a = Mathf.Clamp01((r - dist) / edge);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }

    private static readonly Vector3[] cornerBuf = new Vector3[4];

    // Places `overlay` over the exact on-screen rectangle occupied by `baseRT`, robust
    // to the base image's anchors, pivot, and any scale on it or its parents. Copies the
    // base's world corners and compensates for the overlay parent's own scale — so an
    // overlay parented under the circle still lands 1:1 on top of its base image.
    private void MatchRect(RectTransform overlay, RectTransform baseRT)
    {
        baseRT.GetWorldCorners(cornerBuf); // 0=bottom-left, 1=top-left, 2=top-right, 3=bottom-right
        Vector3 worldCenter = (cornerBuf[0] + cornerBuf[2]) * 0.5f;
        float worldWidth = Vector3.Distance(cornerBuf[0], cornerBuf[3]);
        float worldHeight = Vector3.Distance(cornerBuf[0], cornerBuf[1]);

        overlay.anchorMin = overlay.anchorMax = new Vector2(0.5f, 0.5f);
        overlay.pivot = new Vector2(0.5f, 0.5f);
        overlay.localScale = Vector3.one;

        Vector3 parentScale = overlay.parent != null ? overlay.parent.lossyScale : Vector3.one;
        float sx = Mathf.Approximately(parentScale.x, 0f) ? 1f : parentScale.x;
        float sy = Mathf.Approximately(parentScale.y, 0f) ? 1f : parentScale.y;
        overlay.sizeDelta = new Vector2(worldWidth / sx, worldHeight / sy);

        overlay.position = worldCenter;
    }

    // Fish world position -> point in canvasRect's local space.
    private Vector2 FishCanvasPoint()
    {
        Camera projectionCam = cam != null ? cam : Camera.main;

        Vector2 screen = projectionCam != null
            ? (Vector2)projectionCam.WorldToScreenPoint(fish.position)
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // For a Screen Space - Overlay canvas the UI camera must be null; for
        // Screen Space - Camera / World Space it's the canvas's own camera.
        Canvas canvas = canvasRect.GetComponentInParent<Canvas>();
        Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera
            : null;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCam, out Vector2 local);
        return local;
    }

    // Largest distance from the circle's center to any canvas corner — how far the
    // circle must reach to cover the whole screen.
    private float MaxRadiusToCorners(Vector2 center)
    {
        Rect r = canvasRect.rect;
        float max = 0f;
        max = Mathf.Max(max, Vector2.Distance(center, new Vector2(r.xMin, r.yMin)));
        max = Mathf.Max(max, Vector2.Distance(center, new Vector2(r.xMin, r.yMax)));
        max = Mathf.Max(max, Vector2.Distance(center, new Vector2(r.xMax, r.yMin)));
        max = Mathf.Max(max, Vector2.Distance(center, new Vector2(r.xMax, r.yMax)));
        return max;
    }
}
