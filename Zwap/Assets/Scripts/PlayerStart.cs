using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.SocialPlatforms.Impl;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerStart : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Range(0f, 1f)]
    [SerializeField] private float touchSensitivity = 0.5f; // 1 = full speed, lower = slower touch movement

    [Header("Bounds")] [SerializeField] private Camera cam;

    // Padding is expressed as a FRACTION of the visible half-width, not a fixed
    // number of world units. The rock border is a UI background scaled by a
    // CanvasScaler set to "Scale With Screen Size / Match = Width", so it always
    // sits at a constant fraction of the screen width. Deriving the clamp from
    // halfW the same way keeps the player aligned with the rocks on every aspect
    // ratio. Both axes use halfW because the CanvasScaler matches width (so the
    // background's on-screen height scales with width too, not with orthographicSize).
    [Range(0f, 0.5f)]
    [SerializeField] private float paddingFractionX = 0.05f; // gap from the left/right rocks
    [Range(0f, 0.5f)]
    [SerializeField] private float paddingFractionY = 0.05f; // gap from the top/bottom edge

    [SerializeField] private Animator _animator;
    
    [SerializeField] private float speedPerScore = 0.002f; // +0.2% move speed per point
    [SerializeField] private float maxSpeedMultiplier = 3f;

    [Header("Follow")]
    [SerializeField] private float followDeadzone = 0.1f; // finger this close to the fish = hold still (kills jitter)

    [Range(0f, 1f)]
    [SerializeField] private float followSensitivity = 0.5f; // 1 = full speed toward finger, lower = slower follow

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
    [SerializeField] private SpriteRenderer playerSpriteRenderer; // the fish's own sprite, for the vulnerability blink

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

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        controls = new PlayerControls();
        if (cam == null) cam = Camera.main;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (playerSpriteRenderer != null)
            normalColor = playerSpriteRenderer.color;
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

        if (_animator != null)
            _animator.SetBool("IsMoving", false);

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
    }

    private void OnKeyboardMove(InputAction.CallbackContext ctx)
    {
        keyboardInput = ctx.ReadValue<Vector2>();
    }

    private void OnKeyboardStop(InputAction.CallbackContext ctx)
    {
        keyboardInput = Vector2.zero;
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
            Debug.LogWarning("ControlSwitcher.Instance is null — is ControlSwitcher in the scene?");
            return Vector2.zero;
        }

        ControlType current = ControlSwitcher.Instance.CurrentControl;

        switch (current)
        {
            case ControlType.Tilt:
                return GetTiltInput();

            case ControlType.Touch:
                // already reflects inversion, since TouchControls applies its own flip internally
                return moveInput * touchSensitivity;

            default:
                return Vector2.zero;
        }
    }

    private void FixedUpdate()
    {
        // Update the score-based speed ramp first so every control mode uses it.
        if (ScoreManager.Instance != null)
            multiplier = Mathf.Min(1f + speedPerScore * ScoreManager.Instance.GetScore(), maxSpeedMultiplier);

        ControlType current = ControlSwitcher.Instance != null
            ? ControlSwitcher.Instance.CurrentControl
            : ControlType.Touch;

        Vector3 target;
        bool isMoving;

        if (current == ControlType.Follow)
        {
            // Follow mode drives an absolute destination (the finger), not a
            // per-step direction, so it computes its own target.
            target = GetFollowTarget(out isMoving);
        }
        else
        {
            Vector2 activeInput = GetActiveInput();
            isMoving = activeInput.magnitude > 0.01f;

            Vector3 move = new Vector3(activeInput.x, activeInput.y, 0f) * moveSpeed * multiplier;
            target = transform.position + move;
        }

        if (_animator != null)
            _animator.SetBool("IsMoving", isMoving);

        if (cam != null && cam.orthographic)
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 c = cam.transform.position;

            // Scale the padding with the visible width so it tracks the width-matched
            // rock border across screen sizes (see the paddingFraction fields above).
            float padX = halfW * paddingFractionX;
            float padY = halfW * paddingFractionY;

            target.x = Mathf.Clamp(target.x, c.x - halfW + padX, c.x + halfW - padX);
            target.y = Mathf.Clamp(target.y, c.y - halfH + padY, c.y + halfH - padY);
        }

        rb.MovePosition(target);

        // Position is fully driven by MovePosition, so never let the dynamic body
        // build up momentum — otherwise it coasts when input stops.
        rb.linearVelocity = Vector2.zero;
    }

    // Where the fish should move to this step in Follow mode. It only moves while
    // a finger is actually pressed (a mouse press stands in for a finger in the
    // Editor); with nothing pressed it stays exactly where it is.
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
        else if (Pointer.current != null) // mouse fallback so it's testable in the Editor
        {
            pressed = Pointer.current.press.isPressed;
            screenPos = Pointer.current.position.ReadValue();
        }
        else
        {
            return pos; // no touchscreen or pointer present
        }

        // Finger up -> don't move at all.
        if (!pressed)
            return pos;

        // Screen pixels -> world position. Depth along the view doesn't matter for
        // an orthographic 2D camera, but keep the fish's own z so it stays on plane.
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        world.z = pos.z;

        // Deadzone: if the finger is basically on top of the fish, hold still so it
        // doesn't jitter back and forth across the touch point.
        if (Vector2.Distance(pos, world) <= followDeadzone)
            return pos;

        isMoving = true;

        // Step toward the finger, capped so the fish glides instead of teleporting
        // onto it. followSensitivity scales Follow speed on its own; multiplier keeps
        // the score-based speed ramp applying here like everywhere else.
        float maxStep = moveSpeed * multiplier * followSensitivity;
        return Vector3.MoveTowards(pos, world, maxStep);
    }

    // Touch controls push the fully-resolved direction here (recomputed from the
    // set of currently-held arrows), so a lost press/release can't leave a stuck
    // residual the way the old += / -= accumulator could.
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
        if (isGameOver) return; // guard against multiple triggers in the same frame

        if (!other.CompareTag("Entity")) return;

        if (!isInHitWindow)
        {
            // 1st hit: not fatal, opens a hitWindowDuration-second window during
            // which a hit from a DIFFERENT object is fatal.
            isInHitWindow = true;
            firstHitObject = other.gameObject;

            PlayHitAudio(firstHitSound);
            StartCoroutine(PlayHitAnimation(firstHitSprites));

            hitWindowRoutine = StartCoroutine(HitWindowRoutine());
        }
        else if (other.gameObject != firstHitObject)
        {
            // 2nd hit within the window, from a different object -> fatal.
            if (hitWindowRoutine != null) StopCoroutine(hitWindowRoutine);
            ResetBlink();
            isGameOver = true;
            HandleGameOver();
        }
        // else: repeat trigger from the same object that caused the 1st hit — ignored.
    }

    // Blinks the player toward blinkColor for hitWindowDuration seconds. If nothing
    // fatal interrupts it (see OnTriggerEnter2D above), the window times out here
    // and the player recovers back to normal with its own little animation + sound.
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

    // Spawns a small child object at the player's position, flips through the given
    // 3 sprites at hitEffectFrameLength seconds each, then cleans itself up. Shared
    // by the 1st hit, the fatal 2nd hit, and the recovery-back-to-normal animation.
    private IEnumerator PlayHitAnimation(Sprite[] sprites, System.Action onComplete = null)
    {
        if (sprites != null && sprites.Length > 0)
        {
            var fx = new GameObject("HitEffect");
            fx.transform.SetParent(transform, worldPositionStays: false);
            fx.transform.localPosition = Vector3.zero;

            var sr = fx.AddComponent<SpriteRenderer>();
            sr.sortingOrder = hitEffectSortingOrder; // draw on top of the player sprite

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

    private void HandleGameOver()
    {
        rb.linearVelocity = Vector2.zero;

        if (_animator != null)
            _animator.SetBool("IsMoving", false);

        PlayHitAudio(secondHitSound);

        // Play the fatal-hit flash first, then finish the game-over flow once it's
        // done, so the player actually sees the animation instead of the scene
        // cutting it off.
        StartCoroutine(PlayHitAnimation(secondHitSprites, FinishGameOver));

        // Stop input/movement processing while the hit effect and scene transition
        // play out. This does NOT stop coroutines already running — disabling a
        // component only pauses its Update/FixedUpdate.
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