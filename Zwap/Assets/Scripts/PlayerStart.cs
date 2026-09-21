using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.SocialPlatforms.Impl;

[System.Serializable]
public class FishSkin
{
    public string skinName; // alleen voor overzicht in de Inspector, geen functionele rol
    public Sprite[] frames; // volgorde 1 t/m N

    [Tooltip("Aan: schommelt heen en weer (1..N..1..N...). Uit: loopt door en springt terug naar het begin (1..N, 1..N, ...).")]
    public bool pingPong;
}

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerStart : MonoBehaviour
{
    // Zet dit vanuit het (nog te bouwen) skin-selectiescherm, VOORDAT je deze scene
    // laadt: PlayerPrefs.SetInt(PlayerStart.SkinPrefsKey, index); PlayerPrefs.Save();
    public const string SkinPrefsKey = "SelectedSkin";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Range(0f, 1f)]
    [SerializeField] private float touchSensitivity = 0.5f; // 1 = full speed, lower = slower touch movement

    [Range(0f, 1f)]
    [SerializeField] private float joystickSensitivity = 0.5f; // 1 = full speed, lower = slower joystick movement

    [Header("Bounds")][SerializeField] private Camera cam;

    [Range(0f, 0.5f)]
    [SerializeField] private float paddingFractionX = 0.05f; // gap from the left/right rocks
    [Range(0f, 0.5f)]
    [SerializeField] private float paddingFractionY = 0.05f; // gap from the top/bottom edge

    [SerializeField] private float speedPerScore = 0.002f; // +0.2% move speed per point
    [SerializeField] private float maxSpeedMultiplier = 3f;

    [Header("Follow")]
    [SerializeField] private float followDeadzone = 0.1f; // finger this close to the fish = hold still (kills jitter)

    [Range(0f, 1f)]
    [SerializeField] private float followSensitivity = 0.5f; // 1 = full speed toward finger, lower = slower follow

    [Header("Slider")]
    [SerializeField] private SliderControls sliderControls; // the two on-screen sliders (bottom = X, right = Y)

    [Range(0f, 1f)]
    [SerializeField] private float sliderSensitivity = 0.5f; // 1 = full speed toward the slider point, lower = slower glide

    [Header("Slingshot")]
    [SerializeField] private float maxLaunchSpeed = 12f; // hard cap on launch speed (world units/sec) — the "max speed"

    [Range(0f, 1f)]
    [SerializeField] private float slingSensitivity = 0.5f; // slide sensitivity: 0.5 = neutral, higher reaches max speed with less pull

    [Range(0.05f, 1f)]
    [SerializeField] private float maxPullFraction = 0.35f; // a full-power pull, as a fraction of screen height (device-independent)

    [Range(0.02f, 0.5f)]
    [SerializeField] private float grabRadiusFraction = 0.12f; // a press must land this close to the fish (fraction of screen height) to grab it

    [SerializeField] private float slingDeceleration = 8f; // world units/sec^2 the fish sheds while coasting
    [SerializeField] private float slingStopThreshold = 0.05f; // below this speed the fish counts as stopped (idle animation)

    [Tooltip("When the finger is pinned against a screen edge (out of room to pull further), holding this many seconds adds full power on top of the current pull. Lets you escape a wall you can't drag away from.")]
    [SerializeField] private float slingEdgeChargeTime = 1f;

    [Range(0.01f, 0.15f)]
    [Tooltip("How close to a screen edge (as a fraction of screen height) the finger must be, while pulling into that edge, to count as pinned and start charging.")]
    [SerializeField] private float slingEdgeThresholdFraction = 0.04f;

    [Tooltip("Optional. A LineRenderer that draws the aim line while pulling. Points the launch way and grows with power.")]
    [SerializeField] private LineRenderer aimLine;
    [SerializeField] private float maxAimLineLength = 3f; // world-unit length of the aim line at full power

    [Header("Skins")]
    [SerializeField] private FishSkin[] skins;
    [SerializeField] private int fallbackSkinIndex = 0; // gebruikt zolang er nog geen selectiescherm is (of geen PlayerPrefs-waarde bestaat)
    [SerializeField] private SpriteRenderer playerSpriteRenderer; // toont zowel de skin-frames als de hit-window-blink hieronder

    [Header("Animatie snelheid")]
    [SerializeField] private float idleFrameLength = 0.15f; // seconden per frame in stilstand
    [SerializeField] private float movingFrameLength = 0.08f; // seconden per frame tijdens bewegen
    [SerializeField] private float speedTransitionDuration = 0.25f; // seconden om te blenden tussen idle- en moving-snelheid
    [Range(0f, 1f)]
    [SerializeField] private float speedTransitionExitTime = 0.7f; // wacht tot dit punt in de huidige cyclus voordat de blend start

    [Header("Hit Effect")]
    [SerializeField] private float hitEffectFrameLength = 0.08f; // seconds per frame, shared by all 3 animations below
    [SerializeField] private int hitEffectSortingOrder = 10; // keep higher than the player's sprite so it draws on top

    [Header("Hit Effect — 1st Hit")]
    [SerializeField] private Sprite[] firstHitSprites = new Sprite[3]; // Size = 3 in the Inspector
    [SerializeField] private AudioClip firstHitSound;

    [Header("Hit Effect — 2nd Hit (fatal)")]
    [SerializeField] private Sprite[] secondHitSprites = new Sprite[3];
    [SerializeField] private AudioClip secondHitSound;

    [Header("Hit Effect — Recovery")]
    [SerializeField] private Sprite[] recoverySprites = new Sprite[3];
    [SerializeField] private AudioClip recoverySound;

    [Header("Hit Window")]
    [SerializeField] private float hitWindowDuration = 3f; // seconds to land a fatal 2nd hit from a DIFFERENT object
    [SerializeField] private float blinkInterval = 0.12f; // seconds between color toggles while vulnerable
    [SerializeField] private Color blinkColor = Color.red;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource; // auto-added in Awake if left empty

    [Header("Game Over")]
    [SerializeField] private SceneField gameOverScene;

    private Vector2 moveInput;
    private Vector2 keyboardInput;
    private PlayerControls controls;
    private Rigidbody2D rb;
    private bool isGameOver = false;

    private bool isInHitWindow;        // true between a 1st hit and either a fatal 2nd hit or the recovery timeout
    private GameObject firstHitObject; // the object that caused the 1st hit, so a repeat trigger from it doesn't count as "another" hit
    private Coroutine hitWindowRoutine;
    private Color normalColor;

    private float multiplier = 1f;

    // --- Slingshot state ---
    private Vector2 slingVelocity;       // our own coast velocity (integrated via MovePosition, like every other mode)
    private bool slingAiming;            // true while the fish is grabbed and being pulled back
    private bool slingWasPressed;        // pointer state last FixedUpdate, to detect press/release edges
    private Vector2 slingAnchorScreen;   // screen point where the pull started
    private Vector2 slingCurrentScreen;  // latest finger screen point while pulling
    private float slingChargeN;          // extra normalized power banked by holding at an edge
    private float slingEffectivePullN;   // current power (drag + charge), 0..1, used on release and for the aim line

    // --- Skin/frame animation state ---
    private FishSkin currentSkin;
    private int frameIndex;
    private int frameStep = 1;      // +1 of -1, alleen relevant bij pingPong
    private int cycleLengthSteps;   // hoeveel frame-stappen 1 volledige cyclus telt
    private int stepsIntoCycle;
    private float frameTimer;
    private float currentFrameLength;
    private bool isMoving;          // vervangt de oude Animator-bool; drijft nu de sprite-snelheid aan
    private bool speedTransitionActive;
    private float speedTransitionTimer;
    private float speedTransitionFrom;
    private float speedTransitionTo;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        controls = new PlayerControls();
        if (cam == null) cam = Camera.main;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (playerSpriteRenderer != null)
            normalColor = playerSpriteRenderer.color;

        InitializeSkin();
    }

    // Kiest de skin op basis van PlayerPrefs (gezet door het skin-selectiescherm),
    // of fallbackSkinIndex als die nog niet bestaat, en zet de animatie op frame 1.
    private void InitializeSkin()
    {
        if (skins == null || skins.Length == 0) return;

        int skinIndex = PlayerPrefs.HasKey(SkinPrefsKey) ? PlayerPrefs.GetInt(SkinPrefsKey) : fallbackSkinIndex;
        skinIndex = Mathf.Clamp(skinIndex, 0, skins.Length - 1);
        currentSkin = skins[skinIndex];

        if (currentSkin.frames == null || currentSkin.frames.Length == 0) return;

        cycleLengthSteps = currentSkin.pingPong
            ? Mathf.Max(1, (currentSkin.frames.Length - 1) * 2)
            : currentSkin.frames.Length;

        frameIndex = 0;
        frameStep = 1;
        stepsIntoCycle = 0;
        frameTimer = 0f;
        currentFrameLength = idleFrameLength;

        if (playerSpriteRenderer != null)
            playerSpriteRenderer.sprite = currentSkin.frames[0];
    }

    private void Update()
    {
        UpdateSkinAnimation();
    }
    

    private void UpdateSkinAnimation()
    {
        if (currentSkin == null || currentSkin.frames == null || currentSkin.frames.Length == 0) return;

        float targetFrameLength = isMoving ? movingFrameLength : idleFrameLength;

        // Net als bij een Mecanim-transition: wacht tot 'exit time' bereikt is in de
        // huidige cyclus voordat de blend naar de nieuwe snelheid start.
        if (!speedTransitionActive && !Mathf.Approximately(currentFrameLength, targetFrameLength))
        {
            float cycleProgress = cycleLengthSteps > 0 ? (float)stepsIntoCycle / cycleLengthSteps : 1f;
            if (cycleProgress >= speedTransitionExitTime)
            {
                speedTransitionActive = true;
                speedTransitionTimer = 0f;
                speedTransitionFrom = currentFrameLength;
                speedTransitionTo = targetFrameLength;
            }
        }

        if (speedTransitionActive)
        {
            speedTransitionTimer += Time.deltaTime;
            float t = speedTransitionDuration > 0f ? Mathf.Clamp01(speedTransitionTimer / speedTransitionDuration) : 1f;
            currentFrameLength = Mathf.Lerp(speedTransitionFrom, speedTransitionTo, t);

            if (t >= 1f)
            {
                speedTransitionActive = false;
                currentFrameLength = speedTransitionTo;
            }
        }

        frameTimer += Time.deltaTime;
        if (frameTimer >= currentFrameLength)
        {
            frameTimer -= currentFrameLength;
            AdvanceFrame();
        }
    }

    private void AdvanceFrame()
    {
        Sprite[] frames = currentSkin.frames;
        int n = frames.Length;

        if (currentSkin.pingPong && n > 1)
        {
            frameIndex += frameStep;
            if (frameIndex >= n) { frameIndex = n - 2; frameStep = -1; }
            else if (frameIndex < 0) { frameIndex = 1; frameStep = 1; }
        }
        else
        {
            frameIndex = (frameIndex + 1) % n;
        }

        stepsIntoCycle = (stepsIntoCycle + 1) % Mathf.Max(1, cycleLengthSteps);

        if (playerSpriteRenderer != null)
            playerSpriteRenderer.sprite = frames[frameIndex];
    }

    private bool subscribed;

    private void OnEnable()
    {
        controls.Player.Move.performed += OnKeyboardMove;
        controls.Player.Move.canceled += OnKeyboardStop;
        controls.Player.Enable();

        if (Accelerometer.current != null)
            InputSystem.EnableDevice(Accelerometer.current);

        TrySubscribe();
    }

    private void Start()
    {
        // Safety net: if ControlSwitcher.Instance wasn't set yet during OnEnable
        // (script init order), subscribe now that every Awake has run.
        TrySubscribe();
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
        controls.Player.Move.performed -= OnKeyboardMove;
        controls.Player.Move.canceled -= OnKeyboardStop;
        controls.Player.Disable();

        if (Accelerometer.current != null)
            InputSystem.DisableDevice(Accelerometer.current);

        isMoving = false;

        if (subscribed && ControlSwitcher.Instance != null)
            ControlSwitcher.Instance.OnControlChanged -= HandleControlChanged;
        subscribed = false;
    }

    private void HandleControlChanged(ControlType newControl)
    {
        // clear every input source on switch, so nothing carries over
        keyboardInput = Vector2.zero;
        moveInput = Vector2.zero;
        rb.linearVelocity = Vector2.zero; // stop any coasting carried over from the previous mode

        // Drop any slingshot coast/aim so it can't bleed into the next mode.
        slingVelocity = Vector2.zero;
        slingAiming = false;
        slingWasPressed = false;
        if (aimLine != null) aimLine.enabled = false;

        // Entering Slider mode: park the handles on the fish's current position so
        // the absolute mapping doesn't yank it to wherever the sliders were left.
        if (newControl == ControlType.Slider && sliderControls != null
            && TryGetBounds(out Vector2 min, out Vector2 max))
        {
            Vector3 pos = transform.position;
            sliderControls.SetFromNormalized(new Vector2(
                Mathf.InverseLerp(min.x, max.x, pos.x),
                Mathf.InverseLerp(min.y, max.y, pos.y)));
        }
    }

    private void OnKeyboardMove(InputAction.CallbackContext ctx)
    {
        keyboardInput = ctx.ReadValue<Vector2>();
    }

    private void OnKeyboardStop(InputAction.CallbackContext ctx)
    {
        keyboardInput = Vector2.zero;
    }

    private Vector2 GetJoystickInput()
    {
        return Terresquall.VirtualJoystick.GetAxis();
    }

    private Vector2 GetTiltInput()
    {
        if (Accelerometer.current == null)
            return Vector2.zero;

        Vector3 accel = Accelerometer.current.acceleration.ReadValue();

        return new Vector2(accel.x, accel.y);
    }

    private Vector2 GetActiveInput()
    {
        if (ControlSwitcher.Instance == null)
        {
            Debug.LogWarning("ControlSwitcher.Instance is null — is ControlSwitcher in de scene?");
            return Vector2.zero;
        }

        ControlType current = ControlSwitcher.Instance.CurrentControl;

        switch (current)
        {
            case ControlType.Tilt:
                return GetTiltInput();

            case ControlType.Touch:
                return moveInput * touchSensitivity;

            case ControlType.Joystick:
                {
                    Vector2 joy = GetJoystickInput() * joystickSensitivity;
                    if (ControlSwitcher.Instance.IsInverted)
                        joy = -joy;
                    return joy;
                }

            default:
                return Vector2.zero;
        }
    }

    private void FixedUpdate()
    {
        if (ScoreManager.Instance != null)
            multiplier = Mathf.Min(1f + speedPerScore * ScoreManager.Instance.GetScore(), maxSpeedMultiplier);

        ControlType current = ControlSwitcher.Instance != null
            ? ControlSwitcher.Instance.CurrentControl
            : ControlType.Touch;

        Vector3 target;
        bool isMoving;

        if (current == ControlType.Follow)
        {
            target = GetFollowTarget(out isMoving);
        }
        else if (current == ControlType.Slider)
        {
            target = GetSliderTarget(out isMoving);
        }
        else if (current == ControlType.Slingshot)
        {
            target = GetSlingshotTarget(out isMoving);
        }
        else
        {
            Vector2 activeInput = GetActiveInput();
            isMoving = activeInput.magnitude > 0.01f;

            Vector3 move = new Vector3(activeInput.x, activeInput.y, 0f) * moveSpeed * multiplier;
            target = transform.position + move;
        }

        this.isMoving = isMoving; // drijft nu UpdateSkinAnimation() aan i.p.v. een Animator-bool

        if (TryGetBounds(out Vector2 min, out Vector2 max))
        {
            target.x = Mathf.Clamp(target.x, min.x, max.x);
            target.y = Mathf.Clamp(target.y, min.y, max.y);
        }

        rb.MovePosition(target);

        rb.linearVelocity = Vector2.zero;
    }

    private Vector3 GetFollowTarget(out bool isMoving)
    {
        isMoving = false;
        Vector3 pos = transform.position;

        if (cam == null)
            return pos;

        Vector2 screenPos;
        bool pressed;

        if (Touchscreen.current != null)
        {
            pressed = Touchscreen.current.primaryTouch.press.isPressed;
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
        }
        else if (Pointer.current != null)
        {
            pressed = Pointer.current.press.isPressed;
            screenPos = Pointer.current.position.ReadValue();
        }
        else
        {
            return pos;
        }

        if (!pressed)
            return pos;

        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        world.z = pos.z;

        if (Vector2.Distance(pos, world) <= followDeadzone)
            return pos;

        isMoving = true;

        float maxStep = moveSpeed * multiplier * followSensitivity;
        return Vector3.MoveTowards(pos, world, maxStep);
    }

    private bool TryGetBounds(out Vector2 min, out Vector2 max)
    {
        min = max = Vector2.zero;

        if (cam == null || !cam.orthographic)
            return false;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 c = cam.transform.position;

        float padX = halfW * paddingFractionX;
        float padY = halfW * paddingFractionY;

        min = new Vector2(c.x - halfW + padX, c.y - halfH + padY);
        max = new Vector2(c.x + halfW - padX, c.y + halfH - padY);
        return true;
    }

    private Vector3 GetSliderTarget(out bool isMoving)
    {
        isMoving = false;
        Vector3 pos = transform.position;

        if (sliderControls == null || !TryGetBounds(out Vector2 min, out Vector2 max))
            return pos;

        Vector2 v = sliderControls.Get01();
        Vector3 destination = new Vector3(
            Mathf.Lerp(min.x, max.x, v.x),
            Mathf.Lerp(min.y, max.y, v.y),
            pos.z);

        if (Vector2.Distance(pos, destination) <= 0.001f)
            return pos;

        isMoving = true;

        float maxStep = moveSpeed * multiplier * sliderSensitivity;
        return Vector3.MoveTowards(pos, destination, maxStep);
    }

    // Golf-game slingshot: hold on the fish, drag back, release. The fish launches
    // opposite the drag and coasts to a stop, sliding along a wall it hits instead of
    // bouncing. We keep our own velocity vector and move via MovePosition (like every
    // other mode), so it never fights the Rigidbody's gravity/body settings.
    private Vector3 GetSlingshotTarget(out bool isMoving)
    {
        Vector3 pos = transform.position;

        // Read the primary touch (or mouse in the editor), same source as Follow mode.
        bool pressed = false;
        Vector2 screenPos = Vector2.zero;
        if (Touchscreen.current != null)
        {
            pressed = Touchscreen.current.primaryTouch.press.isPressed;
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
        }
        else if (Pointer.current != null)
        {
            pressed = Pointer.current.press.isPressed;
            screenPos = Pointer.current.position.ReadValue();
        }

        float grabRadiusPx = grabRadiusFraction * Screen.height;
        float maxPullPx = Mathf.Max(1f, maxPullFraction * Screen.height);

        // Press began: only grab if it landed near the fish. Grabbing mid-coast is
        // allowed — it catches the fish so the player can immediately re-fling.
        if (pressed && !slingWasPressed && cam != null)
        {
            Vector2 fishScreen = cam.WorldToScreenPoint(pos);
            if (Vector2.Distance(screenPos, fishScreen) <= grabRadiusPx)
            {
                slingAiming = true;
                slingAnchorScreen = screenPos;
                slingCurrentScreen = screenPos;
                slingChargeN = 0f;          // fresh grab: no banked charge yet
                slingEffectivePullN = 0f;
                slingVelocity = Vector2.zero; // hold still while being aimed
            }
        }

        if (slingAiming && pressed)
        {
            slingCurrentScreen = screenPos; // fish stays put; we just track the pull

            // Bank extra power while the finger is jammed against a screen edge it's
            // pulling into (out of room to drag further). This is the only way to reach
            // full power when the fish is pinned to a wall, where the drag can't grow.
            if (IsPinnedAtEdge(slingEdgeThresholdFraction * Screen.height))
                slingChargeN += Time.fixedDeltaTime / Mathf.Max(0.01f, slingEdgeChargeTime);

            // Power shown/fired = drag distance plus any banked edge charge, capped at 1.
            Vector2 drag = slingAnchorScreen - slingCurrentScreen;
            slingEffectivePullN = Mathf.Clamp01(ComputePullN(drag, maxPullPx) + slingChargeN);
        }
        else if (slingAiming && !pressed)
        {
            // Release: launch opposite the pull. maxLaunchSpeed is the hard cap.
            Vector2 drag = slingAnchorScreen - slingCurrentScreen;
            slingVelocity = drag.sqrMagnitude > 0.0001f
                ? drag.normalized * (slingEffectivePullN * maxLaunchSpeed)
                : Vector2.zero;
            slingAiming = false;
        }

        slingWasPressed = pressed;

        // Draw (or hide) the aim line. Launch direction is opposite the drag; screen
        // axes map straight to world axes under an orthographic, axis-aligned camera.
        // Length reflects the effective power, so a growing line shows the edge charge.
        if (aimLine != null)
        {
            if (slingAiming)
            {
                Vector2 drag = slingAnchorScreen - slingCurrentScreen;
                Vector3 dir = drag.sqrMagnitude > 0.0001f ? (Vector3)drag.normalized : Vector3.zero;
                aimLine.positionCount = 2;
                aimLine.SetPosition(0, pos);
                aimLine.SetPosition(1, pos + dir * (slingEffectivePullN * maxAimLineLength));
                aimLine.enabled = true;
            }
            else
            {
                aimLine.enabled = false;
            }
        }

        // Coast: shed speed over time (held-still while aiming keeps velocity at zero).
        if (!slingAiming)
            slingVelocity = Vector2.MoveTowards(slingVelocity, Vector2.zero, slingDeceleration * Time.fixedDeltaTime);

        Vector3 target = pos + (Vector3)(slingVelocity * Time.fixedDeltaTime);

        // Wall glide: cancel only the velocity component pushing into the edge, keeping
        // the tangential component so the fish slides along the wall instead of bouncing.
        if (TryGetBounds(out Vector2 min, out Vector2 max))
        {
            if ((target.x <= min.x && slingVelocity.x < 0f) || (target.x >= max.x && slingVelocity.x > 0f))
                slingVelocity.x = 0f;
            if ((target.y <= min.y && slingVelocity.y < 0f) || (target.y >= max.y && slingVelocity.y > 0f))
                slingVelocity.y = 0f;
        }

        isMoving = slingVelocity.magnitude > slingStopThreshold;
        return target;
    }

    // Pull as a normalized 0..1 power. Sensitivity (0.5 = neutral) scales how quickly
    // a pull reaches full power; the result is clamped so it can never exceed 1.
    private float ComputePullN(Vector2 drag, float maxPullPx)
    {
        float sensitivityFactor = slingSensitivity * 2f;
        return Mathf.Clamp01((drag.magnitude / maxPullPx) * sensitivityFactor);
    }

    // True when the finger sits within edgePx of a screen border AND is pulling further
    // into that same border (so it's genuinely out of drag room, not just resting there).
    private bool IsPinnedAtEdge(float edgePx)
    {
        Vector2 c = slingCurrentScreen;
        Vector2 a = slingAnchorScreen;

        bool pinnedX = (c.x >= Screen.width - edgePx && c.x > a.x) || (c.x <= edgePx && c.x < a.x);
        bool pinnedY = (c.y >= Screen.height - edgePx && c.y > a.y) || (c.y <= edgePx && c.y < a.y);
        return pinnedX || pinnedY;
    }

    public void SetTouchInput(Vector2 dir)
    {
        moveInput = dir;
    }

    public void ResetInput()
    {
        moveInput = Vector2.zero;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isGameOver) return;

        if (!other.CompareTag("Entity")) return;

        if (!isInHitWindow)
        {
            isInHitWindow = true;
            firstHitObject = other.gameObject;

            PlayHitAudio(firstHitSound);
            StartCoroutine(PlayHitAnimation(firstHitSprites));

            hitWindowRoutine = StartCoroutine(HitWindowRoutine());
        }
        else if (other.gameObject != firstHitObject)
        {
            if (hitWindowRoutine != null) StopCoroutine(hitWindowRoutine);
            ResetBlink();
            isGameOver = true;
            HandleGameOver();
        }
    }

    private IEnumerator HitWindowRoutine()
    {
        float elapsed = 0f;
        bool toggled = false;

        while (elapsed < hitWindowDuration)
        {
            if (playerSpriteRenderer != null)
            {
                playerSpriteRenderer.color = toggled ? blinkColor : normalColor;
                toggled = !toggled;
            }

            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        ResetBlink();
        isInHitWindow = false;
        firstHitObject = null;
        hitWindowRoutine = null;

        PlayHitAudio(recoverySound);
        StartCoroutine(PlayHitAnimation(recoverySprites));
    }

    private void ResetBlink()
    {
        if (playerSpriteRenderer != null)
            playerSpriteRenderer.color = normalColor;
    }

    private void PlayHitAudio(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private IEnumerator PlayHitAnimation(Sprite[] sprites, System.Action onComplete = null)
    {
        if (sprites != null && sprites.Length > 0)
        {
            var fx = new GameObject("HitEffect");
            fx.transform.SetParent(transform, worldPositionStays: false);
            fx.transform.localPosition = Vector3.zero;

            var sr = fx.AddComponent<SpriteRenderer>();
            sr.sortingOrder = hitEffectSortingOrder;

            foreach (Sprite frame in sprites)
            {
                if (frame == null)
                    continue;

                sr.sprite = frame;
                yield return new WaitForSeconds(hitEffectFrameLength);
            }

            Destroy(fx);
        }

        onComplete?.Invoke();
    }
    
    public void ForceFatalHit()
    {
        if (isGameOver) return;

        if (hitWindowRoutine != null) StopCoroutine(hitWindowRoutine);
        ResetBlink();
        isGameOver = true;
        HandleGameOver();
    }


    private void HandleGameOver()
    {
        rb.linearVelocity = Vector2.zero;
        isMoving = false;

        PlayHitAudio(secondHitSound);

        StartCoroutine(PlayHitAnimation(secondHitSprites, FinishGameOver));

        enabled = false;
    }

    private void FinishGameOver()
    {
        int currentScore = ScoreManager.Instance.GetScore();

        if (currentScore > SaveManager.Instance.Data.highScore)
        {
            SaveManager.Instance.Data.highScore = currentScore;
            GameData.Instance.Highscore = currentScore;
        }

        SaveManager.Instance.Data.runs++;
        SaveManager.Instance.Save();

        SceneManager.LoadScene(gameOverScene);
    }
    
    
}