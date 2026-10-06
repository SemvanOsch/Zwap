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

    [Header("Attack Border")]
    [Tooltip("Color of the border around the hit area while the swipe is lethal.")]
    [SerializeField] private Color attackBorderColor =
        new Color(1f, 0f, 0f, 1f);

    [Tooltip("Border thickness in world units.")]
    [SerializeField] private float attackBorderThickness = 0.1f;

    [Tooltip("Keep the translucent fill visible inside the border during the attack.")]
    [SerializeField] private bool keepFillDuringAttack = true;

    [Tooltip("How long the border and fill take to fade out after the attack ends. 0 = instant.")]
    [SerializeField] private float fadeOutDuration = 0.5f;

    [Header("Attack Animation")]
    [SerializeField] private SpriteRenderer attackSpriteRenderer;
    [SerializeField] private Sprite[] attackSprites;
    [SerializeField] private float attackFrameDuration = 0.08f;
    [SerializeField] private bool playAttackAnimation = true;

    private Sprite idleSprite;
    private Coroutine attackAnimationCoroutine;
    private Coroutine fadeCoroutine;

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

    // Border: top, bottom, left, right
    private SpriteRenderer borderTop;
    private SpriteRenderer borderBottom;
    private SpriteRenderer borderLeft;
    private SpriteRenderer borderRight;

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
        fadeCoroutine = null;
        attackAnimationCoroutine = null;

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
        CreateBorderRenderers();

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
        IsAttacking = false;

        if (warningIndicator != null)
            warningIndicator.SetActive(true);

        // Cancel any fade still in progress (e.g. via ForceAttackNow)
        // and make sure no border is left over.
        CancelFade();
        SetBorderActive(false);

        SetFillColor(warningFillColor);
        SetFillActive(true);

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

        CancelFade();

        // Fill stays as the translucent warning fill (or is hidden),
        // and only the border turns bright red.
        SetFillColor(warningFillColor);

        if (keepFillDuringAttack)
        {
            SetWarningFillProgress(1f);
            SetFillActive(true);
        }
        else
        {
            SetFillActive(false);
        }

        LayoutBorder();
        SetBorderColor(attackBorderColor);
        SetBorderActive(true);

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
        // The lethal window ends immediately; only the visuals fade.
        IsAttacking = false;

        CancelFade();

        if (fadeOutDuration > 0f)
        {
            fadeCoroutine = StartCoroutine(FadeOutVisuals());
        }
        else
        {
            SetFillActive(false);
            SetBorderActive(false);
        }

        if (logStateChanges)
            Debug.Log("[Bear] Swipe ended", this);

        OnAttackEnd?.Invoke();
    }

    private IEnumerator FadeOutVisuals()
    {
        float fillStartAlpha = warningFillColor.a;
        float borderStartAlpha = attackBorderColor.a;

        float t = 0f;

        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;

            float k = Mathf.Clamp01(t / fadeOutDuration);
            float remaining = 1f - k;

            Color fill = warningFillColor;
            fill.a = fillStartAlpha * remaining;
            SetFillColor(fill);

            Color border = attackBorderColor;
            border.a = borderStartAlpha * remaining;
            SetBorderColor(border);

            yield return null;
        }

        SetFillActive(false);
        SetBorderActive(false);

        // Restore full colors so the next warning/attack starts clean.
        SetFillColor(warningFillColor);
        SetBorderColor(attackBorderColor);

        fadeCoroutine = null;
    }

    private void CancelFade()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }
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
            GetSolidWhiteSprite();

        renderer.color =
            warningFillColor;

        fillObj.SetActive(false);

        return renderer;
    }

    private void CreateBorderRenderers()
    {
        if (swipeHitbox == null)
            return;

        borderTop = CreateBorderSide("AttackBorder Top");
        borderBottom = CreateBorderSide("AttackBorder Bottom");
        borderLeft = CreateBorderSide("AttackBorder Left");
        borderRight = CreateBorderSide("AttackBorder Right");
    }

    private SpriteRenderer CreateBorderSide(string objectName)
    {
        GameObject obj = new GameObject(objectName);

        obj.transform.SetParent(
            swipeHitbox.transform,
            worldPositionStays: false
        );

        obj.transform.localRotation = Quaternion.identity;

        SpriteRenderer renderer =
            obj.AddComponent<SpriteRenderer>();

        renderer.sprite = GetSolidWhiteSprite();
        renderer.color = attackBorderColor;

        // Draw above the translucent fill.
        renderer.sortingOrder = 1;

        obj.SetActive(false);

        return renderer;
    }

    /// <summary>
    /// Sizes and positions the four border strips just inside the edges
    /// of the swipe hitbox.
    /// </summary>
    private void LayoutBorder()
    {
        if (swipeHitbox == null ||
            borderTop == null ||
            borderBottom == null ||
            borderLeft == null ||
            borderRight == null)
            return;

        Vector3 lossy = swipeHitbox.transform.lossyScale;

        float scaleX =
            Mathf.Approximately(lossy.x, 0f) ? 1f : Mathf.Abs(lossy.x);

        float scaleY =
            Mathf.Approximately(lossy.y, 0f) ? 1f : Mathf.Abs(lossy.y);

        // Convert the world-unit thickness into the hitbox's local space.
        float tx = attackBorderThickness / scaleX;
        float ty = attackBorderThickness / scaleY;

        Vector2 size = swipeHitbox.size;
        Vector2 offset = swipeHitbox.offset;

        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;

        // Top
        borderTop.transform.localPosition =
            new Vector3(offset.x, offset.y + halfH - ty * 0.5f, 0f);
        borderTop.transform.localScale =
            new Vector3(size.x, ty, 1f);

        // Bottom
        borderBottom.transform.localPosition =
            new Vector3(offset.x, offset.y - halfH + ty * 0.5f, 0f);
        borderBottom.transform.localScale =
            new Vector3(size.x, ty, 1f);

        // Left
        borderLeft.transform.localPosition =
            new Vector3(offset.x - halfW + tx * 0.5f, offset.y, 0f);
        borderLeft.transform.localScale =
            new Vector3(tx, size.y, 1f);

        // Right
        borderRight.transform.localPosition =
            new Vector3(offset.x + halfW - tx * 0.5f, offset.y, 0f);
        borderRight.transform.localScale =
            new Vector3(tx, size.y, 1f);
    }

    // Shared by every bear's fill and border strips (their tint lives on the
    // SpriteRenderer, not the sprite), so it's built once instead of 6x per bear.
    private static Sprite solidWhiteSprite;

    private static Sprite GetSolidWhiteSprite()
    {
        if (solidWhiteSprite != null)
            return solidWhiteSprite;

        Texture2D tex =
            new Texture2D(1, 1);

        tex.SetPixel(
            0,
            0,
            Color.white
        );

        tex.Apply();

        solidWhiteSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit: 1f
        );

        // Same as Heron: keep it alive across scene loads (and play sessions, since
        // domain reload is disabled) so the cached reference never points at an unloaded asset.
        tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
        solidWhiteSprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return solidWhiteSprite;
    }

    private void SetFillActive(bool active)
    {
        if (warningFillRendererLeft != null)
            warningFillRendererLeft.gameObject.SetActive(active);

        if (warningFillRendererRight != null)
            warningFillRendererRight.gameObject.SetActive(active);
    }

    private void SetFillColor(Color color)
    {
        if (warningFillRendererLeft != null)
            warningFillRendererLeft.color = color;

        if (warningFillRendererRight != null)
            warningFillRendererRight.color = color;
    }

    private void SetBorderActive(bool active)
    {
        if (borderTop != null)
            borderTop.gameObject.SetActive(active);

        if (borderBottom != null)
            borderBottom.gameObject.SetActive(active);

        if (borderLeft != null)
            borderLeft.gameObject.SetActive(active);

        if (borderRight != null)
            borderRight.gameObject.SetActive(active);
    }

    private void SetBorderColor(Color color)
    {
        if (borderTop != null)
            borderTop.color = color;

        if (borderBottom != null)
            borderBottom.color = color;

        if (borderLeft != null)
            borderLeft.color = color;

        if (borderRight != null)
            borderRight.color = color;
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
        fadeCoroutine = null;
        attackAnimationCoroutine = null;
        StartCoroutine(ForceAttackNowRoutine());
    }

    private IEnumerator ForceAttackNowRoutine()
    {
        yield return StartCoroutine(RunAttackCycle());
        yield return StartCoroutine(AttackLoop());
    }
}