using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>
/// Heron enemy (2D): sits still and kills the player instantly (via PlayerStart.ForceFatalHit)
/// when the player's hitbox is inside its kill circle. By default the WHOLE hitbox must be inside.
/// The kill area is shown in-game as a red dotted outline plus a red fill that gets stronger
/// the deeper the player moves in (100% = the moment the kill triggers).
/// Everything is tweakable from the Inspector.
/// </summary>
public class Heron : MonoBehaviour
{
    [Header("Kill Area")]
    [Tooltip("Radius of the kill circle (world units).")]
    [SerializeField, Min(0f)] private float killRadius = 2.5f;

    [Tooltip("Shifts the center of the kill circle relative to the heron (local space).")]
    [SerializeField] private Vector2 areaOffset = Vector2.zero;

    [Header("Kill Rules")]
    [Tooltip("ON: all corners of the player's hitbox must be inside the circle.\nOFF: any overlap with the circle kills.")]
    [SerializeField] private bool requireFullyInside = true;

    [Tooltip("Seconds between the player being caught and the kill actually happening.")]
    [SerializeField, Min(0f)] private float killDelay = 0f;

    [Header("Kill Sprites")]
    [Tooltip("Optional. Frames played in sequence at the heron's own position the moment the kill happens.")]
    [SerializeField] private Sprite[] killSprites;
    [SerializeField] private float killFrameLength = 0.08f;
    [SerializeField] private int killSpriteSortingOrder = 10;

    [Tooltip("Optional. The heron's own SpriteRenderer, so the kill animation copies its Flip X/Y and plays at the same spot as its idle animation. Leave empty to auto-grab the one on this GameObject.")]
    [SerializeField] private SpriteRenderer heronSpriteRenderer;

    [Header("Area Visual (in-game)")]
    [Tooltip("Show the kill area in the game: dotted outline + fill that gets redder the closer the player is to being caught.")]
    [SerializeField] private bool showAreaVisual = true;

    [Tooltip("Color of the dotted outline.")]
    [SerializeField] private Color outlineColor = Color.red;

    [Tooltip("Length of each dash ALONG the circle (world units).")]
    [SerializeField, Min(0.02f)] private float dashLength = 0.3f;

    [Tooltip("Gap between dashes (world units). The number of dashes is worked out from the circle size, dash length and gap.")]
    [SerializeField, Min(0.01f)] private float dashGap = 0.2f;

    [Tooltip("Extra rotation of each dash in degrees. 0 = long side follows the circle, 90 = dashes point outward.")]
    [SerializeField] private float dashAngleOffset = 0f;

    [Tooltip("Thickness of the dashes (world units).")]
    [FormerlySerializedAs("dotSize")]
    [SerializeField, Min(0.01f)] private float dashThickness = 0.08f;

    [Tooltip("Color of the fill. Its alpha is ignored, use the two alpha fields below.")]
    [SerializeField] private Color fillColor = Color.red;

    [Tooltip("Base fill opacity: the area is always at least this red, even when the player is far away.")]
    [SerializeField, Range(0f, 1f)] private float fillAlphaEmpty = 0.15f;

    [Tooltip("Fill opacity at the moment the kill triggers.")]
    [SerializeField, Range(0f, 1f)] private float fillAlphaFull = 0.6f;

    [Tooltip("Maps how far the player is in (0..1) to fill strength (0..1). Linear by default, curve it to make the fill kick in late or early.")]
    [SerializeField] private AnimationCurve fillCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Tooltip("ON: the fill also grows outward from the center as the player gets closer.")]
    [SerializeField] private bool growFromCenter = false;

    [Tooltip("How fast the fill follows the player. Higher = snappier, 0 = instant.")]
    [SerializeField, Min(0f)] private float fillSpeed = 12f;

    [Tooltip("Sorting layer of the visual. Make sure it draws above your background.")]
    [SerializeField] private string sortingLayerName = "Default";

    [Tooltip("Sorting order of the fill (dashes are +1). Keep it below the player's sprite.")]
    [SerializeField] private int sortingOrder = 5;

    [Tooltip("ON: the heron's own sprite is forced to draw above the area visual (fill + dashes). OFF: leaves its sorting order as set on the SpriteRenderer itself.")]
    [SerializeField] private bool keepHeronSpriteOnTop = true;

    [Header("Player")]
    [Tooltip("Optional. Leave empty to find the PlayerStart in the scene automatically.")]
    [SerializeField] private PlayerStart player;

    [Tooltip("Optional. Leave empty to use the collider on the player object (or its children).")]
    [SerializeField] private Collider2D playerColliderOverride;

    [Header("Feedback")]
    [Tooltip("Optional animator on the heron.")]
    [SerializeField] private Animator animator;

    [Tooltip("Animator trigger fired the moment the player is caught.")]
    [SerializeField] private string catchTrigger = "Attack";

    [Tooltip("Extra hook for sounds, particles, camera shake, etc. Fires when the kill happens.")]
    [SerializeField] private UnityEvent onKill;

    [SerializeField] private bool logKill = false;

    [Header("Debug")]
    [SerializeField] private bool drawGizmo = true;
    [SerializeField] private Color gizmoColor = Color.red;

    private Collider2D playerCollider;
    private bool triggered;

    // visual
    private static Sprite circleSprite;
    private Transform visualRoot;
    private SpriteRenderer fillRenderer;
    private float dangerSmoothed;

    private Vector2 AreaCenter => transform.TransformPoint(areaOffset);

    private void Start()
    {
        FindPlayer();
        if (heronSpriteRenderer == null)
            heronSpriteRenderer = GetComponent<SpriteRenderer>();

        // Same layer and above the fill/dashes, so the idle sprite isn't hidden behind them.
        if (keepHeronSpriteOnTop && heronSpriteRenderer != null)
        {
            if (!string.IsNullOrEmpty(sortingLayerName))
                heronSpriteRenderer.sortingLayerName = sortingLayerName;
            heronSpriteRenderer.sortingOrder = Mathf.Max(heronSpriteRenderer.sortingOrder, sortingOrder + 2);
        }

        if (showAreaVisual) BuildVisual();
    }

    private void OnEnable()
    {
        if (visualRoot != null) visualRoot.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (visualRoot != null) visualRoot.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (visualRoot != null) Destroy(visualRoot.gameObject);
    }

    private void Update()
    {
        if (playerCollider == null)
        {
            FindPlayer();
            if (playerCollider == null)
            {
                UpdateVisual(0f);
                return;
            }
        }

        Bounds b = playerCollider.bounds;

        UpdateVisual(triggered ? 1f : ComputeDanger(b));
        if (triggered) return;

        if (PlayerInArea(b))
        {
            triggered = true;

            if (animator && !string.IsNullOrEmpty(catchTrigger))
                animator.SetTrigger(catchTrigger);

            if (killDelay > 0f) Invoke(nameof(Kill), killDelay);
            else Kill();
        }
    }

    private void LateUpdate()
    {
        // World-space visual follows the heron (it may be scrolling down with the level)
        if (visualRoot != null)
        {
            Vector2 c = AreaCenter;
            visualRoot.position = new Vector3(c.x, c.y, transform.position.z);
        }
    }

    private void FindPlayer()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerStart>();

        if (playerColliderOverride != null)
            playerCollider = playerColliderOverride;
        else if (player != null)
            playerCollider = player.GetComponentInChildren<Collider2D>();
    }

    // ---------- Kill check ----------

    private bool PlayerInArea(Bounds b)
    {
        Vector2 center = AreaCenter;

        // Any overlap is enough
        if (!requireFullyInside)
            return ((Vector2)b.ClosestPoint(center) - center).sqrMagnitude <= killRadius * killRadius;

        // All four corners of the hitbox must be inside the circle
        return FarthestCornerDistance(b, center) <= killRadius;
    }

    private static float FarthestCornerDistance(Bounds b, Vector2 center)
    {
        Vector2 c = b.center;
        Vector2 e = b.extents;
        float far = 0f;

        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        {
            Vector2 corner = c + new Vector2(e.x * x, e.y * y);
            far = Mathf.Max(far, (corner - center).magnitude);
        }
        return far;
    }

    // 0 = player is clear of the area, 1 = the kill triggers.
    // It starts rising as the hitbox touches the circle and hits 1 exactly at the kill moment.
    private float ComputeDanger(Bounds b)
    {
        Vector2 center = AreaCenter;
        float halfDiag = ((Vector2)b.extents).magnitude;

        float metric = requireFullyInside
            ? FarthestCornerDistance(b, center)
            : ((Vector2)b.ClosestPoint(center) - center).magnitude;

        return Mathf.InverseLerp(killRadius + 2f * halfDiag, killRadius, metric);
    }

    private void Kill()
    {
        if (logKill) Debug.Log($"{name} killed the player!");

        PlayKillSprites();

        if (player != null)
            player.ForceFatalHit();

        onKill?.Invoke();
    }

    // ---------- Kill sprite animation ----------

    // Spawns a small object at the heron's own position (same spot as its idle sprite,
    // not the player/fish) and flips through killSprites, then cleans itself up.
    // Purely visual -- doesn't block or delay ForceFatalHit.
    private void PlayKillSprites()
    {
        if (killSprites == null || killSprites.Length == 0) return;

        Transform anchor = heronSpriteRenderer != null ? heronSpriteRenderer.transform : transform;

        var fx = new GameObject($"{name}_KillEffect");
        // Parented to the heron (world position kept) so it keeps moving with it --
        // e.g. if the heron/level is still scrolling down during the death sequence --
        // instead of being left behind at the spot it spawned.
        fx.transform.SetParent(anchor, worldPositionStays: true);
        fx.transform.position = anchor.position;
        fx.transform.rotation = anchor.rotation;

        // The area visual (circle + dashes) has done its job once the kill fires;
        // hide it immediately so it doesn't keep hovering there during the death animation.
        if (visualRoot != null)
            visualRoot.gameObject.SetActive(false);

        var sr = fx.AddComponent<SpriteRenderer>();
        if (!string.IsNullOrEmpty(sortingLayerName))
            sr.sortingLayerName = sortingLayerName;
        // Always above the area visual (fill = sortingOrder, dashes = sortingOrder+1),
        // even if killSpriteSortingOrder was left lower in the Inspector.
        sr.sortingOrder = Mathf.Max(killSpriteSortingOrder, sortingOrder + 2);

        // Covers both ways a heron might be mirrored: Flip X on its SpriteRenderer,
        // or a negative X scale on its transform (the same trick used for trees via
        // the Spawner). XOR'ing them means it's correct whichever one (or neither)
        // is actually in use, without needing to know which.
        bool heronMirrored = false;
        if (heronSpriteRenderer != null) heronMirrored ^= heronSpriteRenderer.flipX;
        heronMirrored ^= transform.lossyScale.x < 0f;

        sr.flipX = heronMirrored;
        if (heronSpriteRenderer != null)
            sr.flipY = heronSpriteRenderer.flipY;

        // Hide the heron's own idle sprite while the kill animation plays over the
        // same spot, so they don't show at once. It's re-shown if the heron is reused.
        if (heronSpriteRenderer != null)
            heronSpriteRenderer.enabled = false;

        StartCoroutine(AnimateKillSprites(sr, fx));
    }

    private IEnumerator AnimateKillSprites(SpriteRenderer sr, GameObject fx)
    {
        foreach (Sprite frame in killSprites)
        {
            if (frame == null) continue;
            sr.sprite = frame;
            yield return new WaitForSeconds(killFrameLength);
        }

        Destroy(fx);

        // Was hidden in PlayKillSprites() so it wouldn't show under the kill animation;
        // bring it back now the animation is done, so the heron doesn't stay invisible.
        if (heronSpriteRenderer != null)
            heronSpriteRenderer.enabled = true;
    }

    // ---------- Area visual ----------

    private void BuildVisual()
    {
        Sprite circle = GetCircleSprite();

        visualRoot = new GameObject($"{name}_AreaVisual").transform;

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(visualRoot, false);
        fillRenderer = fillGO.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = circle;
        ConfigureSorting(fillRenderer, 0);

        Sprite square = GetSquareSprite();
        float circumference = 2f * Mathf.PI * killRadius;
        int count = Mathf.Max(4, Mathf.RoundToInt(circumference / Mathf.Max(0.01f, dashLength + dashGap)));
        float step = circumference / count;                  // arc length per dash + gap
        float length = Mathf.Min(dashLength, step * 0.95f);  // never longer than its slot

        for (int i = 0; i < count; i++)
        {
            float a = i * Mathf.PI * 2f / count;

            var dash = new GameObject("Dash");
            dash.transform.SetParent(visualRoot, false);
            dash.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * killRadius;
            // long side follows the circle (tangent direction)
            dash.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f + dashAngleOffset);
            dash.transform.localScale = new Vector3(length, dashThickness, 1f);

            var sr = dash.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = outlineColor;
            ConfigureSorting(sr, 1);
        }

        UpdateVisual(0f);
    }

    private void ConfigureSorting(SpriteRenderer sr, int offset)
    {
        if (!string.IsNullOrEmpty(sortingLayerName))
            sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder + offset;
    }

    private void UpdateVisual(float target)
    {
        if (fillRenderer == null) return;

        dangerSmoothed = fillSpeed <= 0f
            ? target
            : Mathf.Lerp(dangerSmoothed, target, 1f - Mathf.Exp(-fillSpeed * Time.deltaTime));

        float strength = Mathf.Clamp01(fillCurve.Evaluate(dangerSmoothed));

        Color c = fillColor;
        c.a = Mathf.Lerp(fillAlphaEmpty, fillAlphaFull, strength);
        fillRenderer.color = c;

        float diameter = 2f * killRadius * (growFromCenter ? Mathf.Clamp01(dangerSmoothed) : 1f);
        fillRenderer.transform.localScale = new Vector3(diameter, diameter, 1f);
    }

    // Small generated white circle (1 world unit across), so no art assets are needed.
    private static Sprite GetCircleSprite()
    {
        if (circleSprite != null) return circleSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float r = size * 0.5f;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x + 0.5f - r;
            float dy = y + 0.5f - r;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
            pixels[y * size + x] = new Color32(255, 255, 255, a);
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
        circleSprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return circleSprite;
    }

    // Small generated white square (1 world unit across) used for the outline dashes.
    private static Sprite squareSprite;

    private static Sprite GetSquareSprite()
    {
        if (squareSprite != null) return squareSprite;

        const int size = 8;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        tex.Apply();

        squareSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
        squareSprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return squareSprite;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmo) return;
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(AreaCenter, killRadius);
    }
}