using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Growing-circle background transition. A circle expands from the fish's position;
// inside the circle the NEW map is shown, outside it the OLD map stays, until the
// circle covers the whole screen and the swap is committed.
//
// Setup (see the component's fields):
//   Canvas
//   ├─ Background (RawImage)          = baseImage (the OLD map, scrolled by BackgroundScroller)
//   └─ RevealCircle (Image + Mask)    = circle   (Image uses a circle sprite, e.g. Unity's "Knob";
//      └─ NewBackground (RawImage)    = revealImage    Mask's "Show Mask Graphic" OFF)
//
// The circle grows via its RectTransform sizeDelta (NOT localScale) so the masked
// child map is clipped, never zoomed. revealImage is kept full-screen and in scroll
// sync with baseImage every frame.
public class BackgroundReveal : MonoBehaviour
{
    [Header("Images")]
    [Tooltip("The base/old background RawImage — the SAME one assigned to ControlBackground and scrolled by BackgroundScroller.")]
    [SerializeField] private RawImage baseImage;

    [Tooltip("The overlay RawImage that shows the new map, masked to the circle. Child of the RevealCircle object.")]
    [SerializeField] private RawImage revealImage;

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
    [Tooltip("The Canvas's RectTransform, used to convert the fish position to canvas space and to size the overlay.")]
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
    private Texture pending; // the texture the running reveal is transitioning to

    private void Awake()
    {
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
        // to size 0 makes its Mask clip the overlay to nothing, so it shows nothing.
        if (circle != null) circle.sizeDelta = Vector2.zero;
        if (revealImage != null) revealImage.gameObject.SetActive(false);
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

    // Kick off (or fast-forward then restart) a circle reveal to the given texture.
    public void Reveal(Texture tex)
    {
        if (baseImage == null || tex == null)
            return;

        // Missing any piece needed for the animation -> just swap instantly so the
        // game never gets stuck on the wrong background.
        if (revealImage == null || circle == null || canvasRect == null || fish == null)
        {
            baseImage.texture = tex;
            return;
        }

        // A reveal is already playing: commit it immediately so its target isn't lost,
        // then start the new one from the finished state.
        if (routine != null)
        {
            StopCoroutine(routine);
            Commit();
        }

        // Nothing to do if we're already showing this map.
        if (tex == baseImage.texture)
            return;

        routine = StartCoroutine(RevealRoutine(tex));
    }

    private IEnumerator RevealRoutine(Texture tex)
    {
        pending = tex;

        revealImage.texture = tex;
        revealImage.uvRect = baseImage.uvRect;
        // Fully opaque, untinted — otherwise the old map shows through and the new one
        // looks translucent during the wipe.
        revealImage.color = Color.white;

        // Match the overlay's rect EXACTLY to the base map, so the new map is shown at
        // the same scale and framing. Without this the new texture is stretched over a
        // different-sized rect and looks zoomed-in / low-detail inside the circle.
        RectTransform baseRT = baseImage.rectTransform;
        RectTransform overlayRT = revealImage.rectTransform;
        overlayRT.anchorMin = overlayRT.anchorMax = new Vector2(0.5f, 0.5f);
        overlayRT.pivot = new Vector2(0.5f, 0.5f);
        overlayRT.sizeDelta = baseRT.rect.size;

        // Freeze the circle's center on the fish (in canvas space) for the whole reveal.
        Vector2 center = FishCanvasPoint();
        circle.anchoredPosition = center;

        // Position the overlay so it overlaps the base map 1:1, compensating for the
        // circle it's parented to (overlay pos is relative to the circle's center).
        Vector2 baseCenter = (Vector2)canvasRect.InverseTransformPoint(baseRT.TransformPoint(baseRT.rect.center));
        overlayRT.anchoredPosition = baseCenter - center;

        float diameter = MaxRadiusToCorners(center) * 2f * radiusMargin;

        revealImage.gameObject.SetActive(true);
        circle.sizeDelta = Vector2.zero;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
            float s = ease.Evaluate(k) * diameter;
            circle.sizeDelta = new Vector2(s, s);

            // Stay in scroll sync with the base map so the reveal is seamless.
            revealImage.uvRect = baseImage.uvRect;
            yield return null;
        }

        Commit();
    }

    // Finish a reveal: the new map becomes the base, and the overlay is hidden so we're
    // back to the cheap single-image steady state.
    private void Commit()
    {
        if (pending != null)
            baseImage.texture = pending;

        // Collapse the circle (its Mask then shows nothing) but keep it active so this
        // script can start the next reveal's coroutine.
        if (circle != null)
            circle.sizeDelta = Vector2.zero;
        if (revealImage != null)
            revealImage.gameObject.SetActive(false);

        routine = null;
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
