using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A screen-wide obstacle that drifts downward and periodically telegraphs,
/// then executes, a full-width swipe attack that's instantly fatal on contact.
/// Spawn it like any other SpawnableItem in Spawner.cs - it manages its own
/// movement, attack timing, and cleanup once it scrolls off-screen.
/// </summary>
public class Bear : MonoBehaviour
{
    [Header("Tree")]
    [Tooltip("The tree/log prefab. Only its sprite is copied onto a plain SpriteRenderer under this Bear - its own movement script and collider are NOT included, so it can't double-move or add an extra hit area beyond the swipe.")]
    [SerializeField] private GameObject treePrefab;

    [Tooltip("Optional. Where the tree gets parented/positioned. Leave empty to just parent it directly under this Bear's own transform at local (0,0,0).")]
    [SerializeField] private Transform treeAnchor;

    [Tooltip("Stretches the copied tree sprite horizontally so it exactly spans the same width as Swipe Hitbox (respecting Manual Playfield Width below if set). Only affects this copy under the Bear - the original Tree Prefab elsewhere is untouched.")]
    [SerializeField] private bool fitTreeToScreenWidth = true;

    [Header("Movement")]
    [Tooltip("How fast the bear drifts down the screen, in world units/sec.")]
    [SerializeField] private float moveSpeed = 1f;

    [Tooltip("If enabled, move speed increases with score, same as the player and spawner do elsewhere.")]
    [SerializeField] private bool scaleSpeedWithScore = false;
    [SerializeField] private float speedPerScore = 0.002f;
    [SerializeField] private float maxSpeedMultiplier = 3f;

    [Header("Attack Timing")]
    [Tooltip("Seconds between the end of one attack and the start of the next warning. Change this any time from the Inspector or via SetAttackInterval().")]
    [SerializeField] private float attackInterval = 4f;

    [Tooltip("How long the warning shows before the swipe actually becomes lethal.")]
    [SerializeField] private float warningDuration = 1f;

    [Tooltip("How long the swipe stays lethal once it triggers.")]
    [SerializeField] private float swipeActiveDuration = 0.5f;

    [Header("Warning Indicator")]
    [Tooltip("Optional. Shown below the bear (or wherever you place it) for warningDuration before each swipe. Leave empty to skip the visual warning.")]
    [SerializeField] private GameObject warningIndicator;

    [Header("Warning Fill (progress-bar style)")]
    [Tooltip("Auto-generated to exactly match Swipe Hitbox's size and position - grows from empty to the hitbox's full width as the warning counts down, then vanishes the instant the swipe goes lethal.")]
    [SerializeField] private Color warningFillColor = new Color(1f, 0f, 0f, 0.35f);

    [Tooltip("Log warning/attack state changes to the Console - handy for tuning timings before you have a final animation/indicator in place.")]
    [SerializeField] private bool logStateChanges = true;

    [Header("Attack Animation (optional)")]
    [Tooltip("Optional. If assigned, this trigger fires on the Animator the instant the swipe becomes lethal.")]
    [SerializeField] private Animator animator;
    [SerializeField] private string attackTriggerName = "Attack";

    [Header("Swipe Hitbox")]
    [Tooltip("The full-width trigger collider (on a child object with a BearSwipeHitbox component) that becomes lethal during the attack.")]
    [SerializeField] private BoxCollider2D swipeHitbox;

    [Tooltip("Automatically stretches swipeHitbox on Start. Turn off to size it entirely by hand.")]
    [SerializeField] private bool fitHitboxToScreenWidth = true;

    [Tooltip("If set above 0, this exact width (in world units) is used instead of the camera's full width - use this when the camera shows more than your actual playing field (e.g. margins/black bars on the sides). Leave at 0 to just use the camera's width automatically.")]
    [SerializeField] private float manualPlayfieldWidth = 0f;

    [SerializeField] private Camera cam; // defaults to Camera.main if left empty

    [Header("Cleanup")]
    [Tooltip("Extra world units below the visible screen before the bear destroys itself.")]
    [SerializeField] private float destroyBelowScreenPadding = 1f;

    [Header("Events")]
    [Tooltip("Fires the instant the warning appears (attack telegraph begins).")]
    public UnityEvent OnWarningStart;
    [Tooltip("Fires the instant the warning disappears and the swipe becomes lethal.")]
    public UnityEvent OnAttackStart;
    [Tooltip("Fires when the swipe stops being lethal and the bear goes back to idle.")]
    public UnityEvent OnAttackEnd;

    /// <summary>True for the warningDuration window before a swipe.</summary>
    public bool IsWarning { get; private set; }

    /// <summary>True for the swipeActiveDuration window - contact with the player during this window is fatal.</summary>
    public bool IsAttacking { get; private set; }

    private float warningFillFullScaleX; // the localScale.x that reads as "100% filled, matching swipeHitbox's full width"
    private SpriteRenderer warningFillRenderer; // built automatically in Start() - not a manual field

    private void Awake()
    {
        if (cam == null)
            cam = Camera.main;

        SpawnTree();
    }

    // Briefly instantiates treePrefab (off to the side, unparented) so its own
    // script gets a chance to resolve/apply whichever biome sprite it should
    // show, then copies JUST that sprite onto a plain SpriteRenderer parented
    // under the Bear, and destroys the temporary instance. This deliberately
    // does NOT keep the tree's own movement script (which would double up with
    // the Bear's own MoveDown() and make it fall faster) or its own collider
    // (only the swipe hitbox should ever be able to hurt the player here) -
    // just the visual.
    private void SpawnTree()
    {
        if (treePrefab == null)
            return;

        GameObject tempInstance = Instantiate(treePrefab);
        SpriteRenderer sourceRenderer = tempInstance.GetComponentInChildren<SpriteRenderer>();

        if (sourceRenderer == null)
        {
            Debug.LogWarning("[Bear] Tree Prefab has no SpriteRenderer to copy a sprite from.", this);
            Destroy(tempInstance);
            return;
        }

        Transform parent = treeAnchor != null ? treeAnchor : transform;
        GameObject treeVisual = new GameObject("Tree (sprite only)");
        treeVisual.transform.SetParent(parent, worldPositionStays: false);
        treeVisual.transform.localPosition = Vector3.zero;

        SpriteRenderer sr = treeVisual.AddComponent<SpriteRenderer>();
        sr.sprite = sourceRenderer.sprite;
        sr.sortingLayerID = sourceRenderer.sortingLayerID;
        sr.sortingOrder = sourceRenderer.sortingOrder;
        sr.flipX = sourceRenderer.flipX;
        sr.flipY = sourceRenderer.flipY;
        sr.color = sourceRenderer.color;

        if (fitTreeToScreenWidth)
            FitTreeSpriteToWidth(treeVisual, sr);

        Destroy(tempInstance);
    }

    // Stretches the copied tree sprite's horizontal scale so it spans
    // GetTargetWidth() (the same width the swipe hitbox uses), and centers it
    // on the camera's X - same idea as FitHitboxToScreenWidth, just applied to
    // this sprite-only copy instead of a collider.
    private void FitTreeSpriteToWidth(GameObject treeVisual, SpriteRenderer sr)
    {
        if (sr.sprite == null)
            return;

        float targetWidth = GetTargetWidth();
        float spriteWidth = sr.sprite.bounds.size.x;
        if (targetWidth <= 0f || spriteWidth <= 0f)
            return;

        Vector3 scale = treeVisual.transform.localScale;
        scale.x = targetWidth / spriteWidth;
        treeVisual.transform.localScale = scale;

        if (cam != null)
        {
            Vector3 pos = treeVisual.transform.position;
            pos.x = cam.transform.position.x;
            treeVisual.transform.position = pos;
        }
    }

    private void Start()
    {
        if (fitHitboxToScreenWidth)
            FitHitboxToScreenWidth();

        CreateWarningFillRenderer();

        if (warningIndicator != null)
            warningIndicator.SetActive(false);
    }

    private void OnEnable()
    {
        StartCoroutine(AttackLoop());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private void Update()
    {
        MoveDown();
        DestroyIfBelowScreen();
    }

    private void MoveDown()
    {
        float multiplier = 1f;

        if (scaleSpeedWithScore && ScoreManager.Instance != null)
            multiplier = Mathf.Min(1f + speedPerScore * ScoreManager.Instance.GetScore(), maxSpeedMultiplier);

        transform.position += Vector3.down * (moveSpeed * multiplier * Time.deltaTime);
    }

    private void DestroyIfBelowScreen()
    {
        if (cam == null || !cam.orthographic)
            return;

        float bottomEdge = cam.transform.position.y - cam.orthographicSize - destroyBelowScreenPadding;

        if (transform.position.y < bottomEdge)
            Destroy(gameObject);
    }

    // Repeats forever while the bear is enabled: wait -> warn -> swipe -> repeat.
    private IEnumerator AttackLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, attackInterval));
            yield return StartCoroutine(RunAttackCycle());
        }
    }

    private IEnumerator RunAttackCycle()
    {
        BeginWarning();

        float t = 0f;
        while (t < warningDuration)
        {
            t += Time.deltaTime;
            SetWarningFillProgress(warningDuration > 0f ? Mathf.Clamp01(t / warningDuration) : 1f);
            yield return null;
        }
        SetWarningFillProgress(1f);

        BeginAttack();
        yield return new WaitForSeconds(Mathf.Max(0f, swipeActiveDuration));

        EndAttack();
    }

    private void BeginWarning()
    {
        IsWarning = true;

        if (warningIndicator != null)
            warningIndicator.SetActive(true);

        if (warningFillRenderer != null)
        {
            warningFillRenderer.gameObject.SetActive(true);
            SetWarningFillProgress(0f);
        }

        if (logStateChanges)
            Debug.Log($"[Bear] Warning - swipe incoming in {warningDuration}s", this);

        OnWarningStart?.Invoke();
    }

    private void BeginAttack()
    {
        IsWarning = false;
        IsAttacking = true;

        if (warningIndicator != null)
            warningIndicator.SetActive(false);

        if (warningFillRenderer != null)
            warningFillRenderer.gameObject.SetActive(false);

        if (animator != null && !string.IsNullOrEmpty(attackTriggerName))
            animator.SetTrigger(attackTriggerName);

        if (logStateChanges)
            Debug.Log("[Bear] Swipe is now LETHAL", this);

        OnAttackStart?.Invoke();
    }

    private void EndAttack()
    {
        IsAttacking = false;

        if (logStateChanges)
            Debug.Log("[Bear] Swipe ended", this);

        OnAttackEnd?.Invoke();
    }

    /// <summary>
    /// Called by BearSwipeHitbox whenever anything overlaps it. Only actually
    /// hurts the player while IsAttacking is true - outside the attack window
    /// this is a no-op, so the hitbox can safely stay enabled at all times
    /// (see BearSwipeHitbox's OnTriggerStay2D comment for why that matters).
    /// </summary>
    public void HandleSwipeHit(Collider2D other)
    {
        if (!IsAttacking)
            return;

        PlayerStart player = other.GetComponent<PlayerStart>();
        if (player == null)
            return;

        player.ForceFatalHit(); // requires the small PlayerStart addition - see chat explanation
    }

    // Stretches swipeHitbox's width to either manualPlayfieldWidth (if set above
    // 0) or the camera's full visible width otherwise, and centers it on the
    // camera's X. Leaves the hitbox's Y position and height alone - position
    // that manually to match wherever your swipe animation reads as dangerous.
    public void FitHitboxToScreenWidth()
    {
        if (swipeHitbox == null)
            return;

        float targetWidth = GetTargetWidth();
        if (targetWidth <= 0f)
            return;

        if (cam != null)
        {
            Vector3 pos = swipeHitbox.transform.position;
            pos.x = cam.transform.position.x;
            swipeHitbox.transform.position = pos;
        }

        float scaleX = swipeHitbox.transform.lossyScale.x;
        if (Mathf.Approximately(scaleX, 0f)) scaleX = 1f; // guard against a zeroed-out scale

        Vector2 size = swipeHitbox.size;
        size.x = targetWidth / scaleX;
        swipeHitbox.size = size;
    }

    // manualPlayfieldWidth wins whenever it's set above 0; otherwise falls back
    // to reading the camera's own orthographic width.
    private float GetTargetWidth()
    {
        if (manualPlayfieldWidth > 0f)
            return manualPlayfieldWidth;

        if (cam == null || !cam.orthographic)
            return 0f;

        return cam.orthographicSize * cam.aspect * 2f;
    }

    // Creates a child of swipeHitbox - matching its exact size and position -
    // that visually fills as the warning counts down. Built from a single
    // generated white pixel so no sprite asset is needed; the hitbox and the
    // fill are guaranteed to line up because the fill is quite literally sized
    // from the hitbox's own BoxCollider2D.size/offset, not computed separately.
    private void CreateWarningFillRenderer()
    {
        if (swipeHitbox == null)
            return;

        GameObject fillObj = new GameObject("WarningFill (auto-generated)");
        fillObj.transform.SetParent(swipeHitbox.transform, worldPositionStays: false);
        fillObj.transform.localPosition = swipeHitbox.offset;
        fillObj.transform.localRotation = Quaternion.identity;

        warningFillRenderer = fillObj.AddComponent<SpriteRenderer>();
        warningFillRenderer.sprite = CreateSolidWhiteSprite();
        warningFillRenderer.color = warningFillColor;

        // The generated sprite is exactly 1x1 world unit at scale 1, so scale
        // directly equals the collider's own size in local units - height is
        // fixed, width starts at 0 and grows toward warningFillFullScaleX.
        warningFillFullScaleX = swipeHitbox.size.x;
        fillObj.transform.localScale = new Vector3(0f, swipeHitbox.size.y, 1f);

        fillObj.SetActive(false);
    }

    private static Sprite CreateSolidWhiteSprite()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), pixelsPerUnit: 1f);
    }

    // Scales warningFillRenderer horizontally so 0 = invisible/empty and
    // warningFillFullScaleX = exactly as wide as swipeHitbox. Grows outward
    // from the center, matching how the hitbox itself is centered.
    private void SetWarningFillProgress(float progress)
    {
        if (warningFillRenderer == null)
            return;

        Vector3 scale = warningFillRenderer.transform.localScale;
        scale.x = warningFillFullScaleX * Mathf.Clamp01(progress);
        warningFillRenderer.transform.localScale = scale;
    }

    // ------------------------------------------------------------------
    // Public API - tune this bear at runtime, e.g. from a difficulty
    // system that makes bears attack faster/longer the further you get.
    // ------------------------------------------------------------------

    public void SetMoveSpeed(float unitsPerSecond) => moveSpeed = unitsPerSecond;
    public void SetAttackInterval(float seconds) => attackInterval = Mathf.Max(0f, seconds);
    public void SetWarningDuration(float seconds) => warningDuration = Mathf.Max(0f, seconds);
    public void SetSwipeActiveDuration(float seconds) => swipeActiveDuration = Mathf.Max(0f, seconds);

    /// <summary>Skips whatever's left of the current wait and starts a warning->swipe cycle immediately.</summary>
    public void ForceAttackNow()
    {
        StopAllCoroutines();
        StartCoroutine(ForceAttackNowRoutine());
    }

    private IEnumerator ForceAttackNowRoutine()
    {
        yield return StartCoroutine(RunAttackCycle());
        yield return StartCoroutine(AttackLoop());
    }
}