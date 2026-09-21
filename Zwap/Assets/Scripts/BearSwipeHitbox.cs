using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Sits on a child GameObject holding the swipe's trigger collider, and simply
/// forwards trigger hits back to the owning Bear. Kept as its own tiny script
/// (rather than putting the collider directly on the Bear) so the hitbox can be
/// sized/positioned independently of the bear's own sprite/animation transform,
/// and so multiple overlapping colliders on the bear don't get confused about
/// which one fired.
/// </summary>
public class BearSwipeHitbox : MonoBehaviour
{
    [SerializeField] private Bear owner;

    private void Reset()
    {
        // Auto-fills when you add this component in the Editor, if it's a
        // child of the Bear - saves a manual drag most of the time.
        owner = GetComponentInParent<Bear>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null)
            owner.HandleSwipeHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Also forward ongoing overlaps, not just the initial enter. This matters
        // because we deliberately never enable/disable this collider (see Bear's
        // comments on isAttackLethalNow) - if the player is already standing
        // inside the hitbox's area the moment the swipe turns lethal, Unity will
        // NOT fire OnTriggerEnter2D for them (it only fires on the transition
        // into overlap, not on a state flag changing) - Stay covers that case.
        if (owner != null)
            owner.HandleSwipeHit(other);
    }
}

/// <summary>
/// A screen-wide obstacle that drifts downward and periodically telegraphs,
/// then executes, a full-width swipe attack that's instantly fatal on contact.
/// Spawn it like any other SpawnableItem in Spawner.cs - it manages its own
/// movement, attack timing, and cleanup once it scrolls off-screen.
/// </summary>
[RequireComponent(typeof(Collider2D))] // the bear's OWN body collider, if you want the fish to be able to sit on/bump the treestump; unrelated to the swipe hitbox below
public class Bear : MonoBehaviour
{
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

    [Tooltip("Log warning/attack state changes to the Console - handy for tuning timings before you have a final animation/indicator in place.")]
    [SerializeField] private bool logStateChanges = true;

    [Header("Attack Animation (optional)")]
    [Tooltip("Optional. If assigned, this trigger fires on the Animator the instant the swipe becomes lethal.")]
    [SerializeField] private Animator animator;
    [SerializeField] private string attackTriggerName = "Attack";

    [Header("Swipe Hitbox")]
    [Tooltip("The full-width trigger collider (on a child object with a BearSwipeHitbox component) that becomes lethal during the attack.")]
    [SerializeField] private BoxCollider2D swipeHitbox;

    [Tooltip("Automatically stretches swipeHitbox to exactly the camera's visible width on Start. Turn off if you'd rather size it by hand.")]
    [SerializeField] private bool fitHitboxToScreenWidth = true;

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

    private void Awake()
    {
        if (cam == null)
            cam = Camera.main;
    }

    private void Start()
    {
        if (fitHitboxToScreenWidth)
            FitHitboxToScreenWidth();

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
        yield return new WaitForSeconds(Mathf.Max(0f, warningDuration));

        BeginAttack();
        yield return new WaitForSeconds(Mathf.Max(0f, swipeActiveDuration));

        EndAttack();
    }

    private void BeginWarning()
    {
        IsWarning = true;

        if (warningIndicator != null)
            warningIndicator.SetActive(true);

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

    // Stretches swipeHitbox's width to exactly match the camera's visible width
    // and centers it on the camera's X, so it's genuinely edge-to-edge on any
    // screen size/aspect ratio. Leaves the hitbox's Y position and height alone -
    // position that manually to match wherever your swipe animation reads as
    // dangerous.
    public void FitHitboxToScreenWidth()
    {
        if (swipeHitbox == null || cam == null || !cam.orthographic)
            return;

        float halfW = cam.orthographicSize * cam.aspect;

        Vector3 pos = swipeHitbox.transform.position;
        pos.x = cam.transform.position.x;
        swipeHitbox.transform.position = pos;

        float scaleX = swipeHitbox.transform.lossyScale.x;
        if (Mathf.Approximately(scaleX, 0f)) scaleX = 1f; // guard against a zeroed-out scale

        Vector2 size = swipeHitbox.size;
        size.x = (halfW * 2f) / scaleX;
        swipeHitbox.size = size;
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