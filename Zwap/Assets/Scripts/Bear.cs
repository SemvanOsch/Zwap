
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A screen-wide obstacle that drifts downward and periodically telegraphs,
/// then executes, a full-width swipe attack that's instantly fatal on contact.
/// </summary>
public class Bear : MonoBehaviour
{
    [Header("Tree Placement")]
    [Tooltip("How far the tree is pushed DOWN from the bear's origin, in world units.")]
    [SerializeField] private float treeDropOffset = 0f;

    [Header("Tree")]
    [SerializeField] private GameObject treePrefab;

    [Tooltip("Optional. Where the tree gets parented/positioned.")]
    [SerializeField] private Transform treeAnchor;

    [SerializeField] private bool fitTreeToScreenWidth = true;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1f;

    [SerializeField] private bool scaleSpeedWithScore = false;
    [SerializeField] private float speedPerScore = 0.002f;
    [SerializeField] private float maxSpeedMultiplier = 3f;

    [Header("Attack Timing")]
    [SerializeField] private float attackInterval = 4f;
    [SerializeField] private float warningDuration = 1f;
    [SerializeField] private float swipeActiveDuration = 0.5f;

    [Header("Warning Indicator")]
    [SerializeField] private GameObject warningIndicator;

    [Header("Warning Fill")]
    [SerializeField] private Color warningFillColor =
        new Color(1f, 0f, 0f, 0.35f);

    [SerializeField] private bool logStateChanges = true;

    [Header("Attack Animation")]
    [SerializeField] private SpriteRenderer attackSpriteRenderer;
    [SerializeField] private Sprite[] attackSprites;
    [SerializeField] private float attackFrameDuration = 0.08f;
    [SerializeField] private bool playAttackAnimation = true;

    private Sprite idleSprite;
    private Coroutine attackAnimationCoroutine;

    [Header("Swipe Hitbox")]
    [SerializeField] private BoxCollider2D swipeHitbox;
    [SerializeField] private bool fitHitboxToScreenWidth = true;
    [SerializeField] private float manualPlayfieldWidth = 0f;
    [SerializeField] private Camera cam;

    [Header("Cleanup")]
    [SerializeField] private float destroyBelowScreenPadding = 1f;

    [Header("Events")]
    public UnityEvent OnWarningStart;
    public UnityEvent OnAttackStart;
    public UnityEvent OnAttackEnd;

    public bool IsWarning { get; private set; }
    public bool IsAttacking { get; private set; }

    private float warningFillFullScaleX;
    private SpriteRenderer warningFillRendererLeft;
    private SpriteRenderer warningFillRendererRight;

    private ObjectVariant treeVariant;
    private bool subscribedToSwitcherForTree;

    private void Awake()
    {
        if (cam == null)
            cam = Camera.main;

        SpawnTree();
    }

    private void OnEnable()
    {
        StartCoroutine(AttackLoop());
        TrySubscribeSwitcherForTree();
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        if (subscribedToSwitcherForTree &&
            ControlSwitcher.Instance != null)
        {
            ControlSwitcher.Instance.OnControlChanged -=
                HandleTreeControlChanged;
        }

        subscribedToSwitcherForTree = false;
    }

    private void TrySubscribeSwitcherForTree()
    {
        if (subscribedToSwitcherForTree ||
            ControlSwitcher.Instance == null)
            return;

        ControlSwitcher.Instance.OnControlChanged +=
            HandleTreeControlChanged;

        subscribedToSwitcherForTree = true;
    }

    private void HandleTreeControlChanged(ControlType newControl)
    {
        if (treeVariant == null)
            return;

        bool inverted =
            ControlSwitcher.Instance != null &&
            ControlSwitcher.Instance.IsInverted;

        treeVariant.ApplySkin(newControl, inverted);
    }

    private void SpawnTree()
    {
        if (treePrefab == null)
            return;

        Transform parent =
            treeAnchor != null ? treeAnchor : transform;

        GameObject treeInstance =
            Instantiate(treePrefab, parent);

        treeInstance.transform.localPosition =
            new Vector3(0f, -treeDropOffset, 0f);

        StripTreeInstance(treeInstance);

        treeVariant =
            treeInstance.GetComponentInChildren<ObjectVariant>();

        TrySubscribeSwitcherForTree();

        if (treeVariant != null &&
            ControlSwitcher.Instance != null)
        {
            treeVariant.ApplySkin(
                ControlSwitcher.Instance.CurrentControl,
                ControlSwitcher.Instance.IsInverted
            );
        }

        if (fitTreeToScreenWidth)
        {
            FitTreeSpriteToWidth(
                treeInstance,
                treeInstance.GetComponentInChildren<SpriteRenderer>()
            );
        }
    }

    private void StripTreeInstance(GameObject treeInstance)
    {
        foreach (Collider2D col in
                 treeInstance.GetComponentsInChildren<Collider2D>())
        {
            Destroy(col);
        }

        foreach (MonoBehaviour mb in
                 treeInstance.GetComponentsInChildren<MonoBehaviour>())
        {
            if (mb is ObjectVariant)
                continue;

            Destroy(mb);
        }

        if (!treeInstance.activeSelf)
            treeInstance.SetActive(true);
    }

    private void FitTreeSpriteToWidth(
        GameObject treeInstance,
        SpriteRenderer sr)
    {
        if (sr == null || sr.sprite == null)
            return;

        float targetWidth = GetTargetWidth();
        float spriteWidth = sr.sprite.bounds.size.x;

        if (targetWidth <= 0f || spriteWidth <= 0f)
            return;

        Transform parent = treeInstance.transform.parent;

        float parentScaleX =
            parent != null ? parent.lossyScale.x : 1f;

        if (Mathf.Approximately(parentScaleX, 0f))
            parentScaleX = 1f;

        Vector3 scale = treeInstance.transform.localScale;

        scale.x =
            targetWidth /
            (spriteWidth * parentScaleX);

        treeInstance.transform.localScale = scale;

        if (cam != null)
        {
            Vector3 pos = treeInstance.transform.position;
            pos.x = cam.transform.position.x;
            treeInstance.transform.position = pos;
        }
    }

    private void Start()
    {
        if (fitHitboxToScreenWidth)
            FitHitboxToScreenWidth();

        CreateWarningFillRenderer();

        if (warningIndicator != null)
            warningIndicator.SetActive(false);

        if (attackSpriteRenderer != null)
            idleSprite = attackSpriteRenderer.sprite;
    }

    private void Update()
    {
        MoveDown();
        DestroyIfBelowScreen();
    }

    private void MoveDown()
    {
        float multiplier = 1f;

        if (scaleSpeedWithScore &&
            ScoreManager.Instance != null)
        {
            multiplier = Mathf.Min(
                1f +
                speedPerScore *
                ScoreManager.Instance.GetScore(),
                maxSpeedMultiplier
            );
        }

        transform.position +=
            Vector3.down *
            (moveSpeed * multiplier * Time.deltaTime);
    }

    private void DestroyIfBelowScreen()
    {
        if (cam == null || !cam.orthographic)
            return;

        float bottomEdge =
            cam.transform.position.y -
            cam.orthographicSize -
            destroyBelowScreenPadding;

        if (transform.position.y < bottomEdge)
            Destroy(gameObject);
    }

    private IEnumerator AttackLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(
                Mathf.Max(0f, attackInterval)
            );

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

            SetWarningFillProgress(
                warningDuration > 0f
                    ? Mathf.Clamp01(t / warningDuration)
                    : 1f
            );

            yield return null;
        }

        SetWarningFillProgress(1f);

        BeginAttack();

        yield return new WaitForSeconds(
            Mathf.Max(0f, swipeActiveDuration)
        );

        EndAttack();
    }

    private void BeginWarning()
    {
        IsWarning = true;

        if (warningIndicator != null)
            warningIndicator.SetActive(true);

        if (warningFillRendererLeft != null)
            warningFillRendererLeft.gameObject.SetActive(true);

        if (warningFillRendererRight != null)
            warningFillRendererRight.gameObject.SetActive(true);

        SetWarningFillProgress(0f);

        if (logStateChanges)
        {
            Debug.Log(
                $"[Bear] Warning - swipe incoming in {warningDuration}s",
                this
            );
        }

        OnWarningStart?.Invoke();
    }

    private void BeginAttack()
    {
        IsWarning = false;
        IsAttacking = true;

        if (warningIndicator != null)
            warningIndicator.SetActive(false);

        if (warningFillRendererLeft != null)
            warningFillRendererLeft.gameObject.SetActive(false);

        if (warningFillRendererRight != null)
            warningFillRendererRight.gameObject.SetActive(false);

        if (playAttackAnimation &&
            attackSpriteRenderer != null &&
            attackSprites != null &&
            attackSprites.Length > 0)
        {
            if (attackAnimationCoroutine != null)
                StopCoroutine(attackAnimationCoroutine);

            attackAnimationCoroutine =
                StartCoroutine(PlayAttackAnimation());
        }

        if (logStateChanges)
            Debug.Log("[Bear] Swipe is now LETHAL", this);

        OnAttackStart?.Invoke();
    }

    private IEnumerator PlayAttackAnimation()
    {
        for (int i = 0; i < attackSprites.Length; i++)
        {
            if (attackSprites[i] != null)
                attackSpriteRenderer.sprite = attackSprites[i];

            yield return new WaitForSeconds(
                Mathf.Max(0.01f, attackFrameDuration)
            );
        }

        if (idleSprite != null)
            attackSpriteRenderer.sprite = idleSprite;

        attackAnimationCoroutine = null;
    }

    private void EndAttack()
    {
        IsAttacking = false;

        if (logStateChanges)
            Debug.Log("[Bear] Swipe ended", this);

        OnAttackEnd?.Invoke();
    }

    public void HandleSwipeHit(Collider2D other)
    {
        if (!IsAttacking)
            return;

        PlayerStart player =
            other.GetComponent<PlayerStart>();

        if (player == null)
            return;

        player.ForceFatalHit();
    }

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

        float scaleX =
            swipeHitbox.transform.lossyScale.x;

        if (Mathf.Approximately(scaleX, 0f))
            scaleX = 1f;

        Vector2 size = swipeHitbox.size;

        size.x = targetWidth / scaleX;

        swipeHitbox.size = size;
    }

    private float GetTargetWidth()
    {
        if (manualPlayfieldWidth > 0f)
            return manualPlayfieldWidth;

        if (cam == null || !cam.orthographic)
            return 0f;

        return cam.orthographicSize *
               cam.aspect *
               2f;
    }

    private void CreateWarningFillRenderer()
    {
        if (swipeHitbox == null)
            return;

        warningFillFullScaleX =
            swipeHitbox.size.x;

        warningFillRendererLeft =
            CreateWarningFillSide("WarningFill Left");

        warningFillRendererRight =
            CreateWarningFillSide("WarningFill Right");
    }

    private SpriteRenderer CreateWarningFillSide(
        string objectName)
    {
        GameObject fillObj =
            new GameObject(objectName);

        fillObj.transform.SetParent(
            swipeHitbox.transform,
            worldPositionStays: false
        );

        fillObj.transform.localRotation =
            Quaternion.identity;

        SpriteRenderer renderer =
            fillObj.AddComponent<SpriteRenderer>();

        renderer.sprite =
            CreateSolidWhiteSprite();

        renderer.color =
            warningFillColor;

        fillObj.SetActive(false);

        return renderer;
    }

    private static Sprite CreateSolidWhiteSprite()
    {
        Texture2D tex =
            new Texture2D(1, 1);

        tex.SetPixel(
            0,
            0,
            Color.white
        );

        tex.Apply();

        return Sprite.Create(
            tex,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit: 1f
        );
    }

    private void SetWarningFillProgress(float progress)
    {
        if (warningFillRendererLeft == null ||
            warningFillRendererRight == null)
            return;

        progress = Mathf.Clamp01(progress);

        // 0 = empty
        // 1 = completely filled
        float filledWidth =
            warningFillFullScaleX * progress;

        float halfWidth =
            filledWidth * 0.5f;

        float halfFullWidth =
            warningFillFullScaleX * 0.5f;

        // LEFT SIDE
        Transform left =
            warningFillRendererLeft.transform;

        left.localPosition =
            new Vector3(
                -halfFullWidth +
                halfWidth * 0.5f,
                swipeHitbox.offset.y,
                0f
            );

        left.localScale =
            new Vector3(
                halfWidth,
                swipeHitbox.size.y,
                1f
            );

        // RIGHT SIDE
        Transform right =
            warningFillRendererRight.transform;

        right.localPosition =
            new Vector3(
                halfFullWidth -
                halfWidth * 0.5f,
                swipeHitbox.offset.y,
                0f
            );

        right.localScale =
            new Vector3(
                halfWidth,
                swipeHitbox.size.y,
                1f
            );
    }

    public void SetMoveSpeed(float unitsPerSecond)
    {
        moveSpeed = unitsPerSecond;
    }

    public void SetAttackInterval(float seconds)
    {
        attackInterval = Mathf.Max(0f, seconds);
    }

    public void SetWarningDuration(float seconds)
    {
        warningDuration = Mathf.Max(0f, seconds);
    }

    public void SetSwipeActiveDuration(float seconds)
    {
        swipeActiveDuration = Mathf.Max(0f, seconds);
    }

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
