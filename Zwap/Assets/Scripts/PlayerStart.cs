using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerStart : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Range(0f, 1f)]
    [SerializeField] private float touchSensitivity = 0.5f; // 1 = full speed, lower = slower touch movement

    [Header("Bounds")] [SerializeField] private Camera cam;
    [SerializeField] private float padding = 0.5f; // keeps the sprite fully on screen

    [SerializeField] private Animator _animator;
    
    [SerializeField] private float speedPerScore = 0.002f; // +0.2% move speed per point
    [SerializeField] private float maxSpeedMultiplier = 3f;

    [Header("Game Over")]
    [SerializeField] private SceneField gameOverScene;

    private Vector2 moveInput;
    private Vector2 keyboardInput;
    private PlayerControls controls;
    private Rigidbody2D rb;
    private bool isGameOver = false;
    
    private float multiplier = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        controls = new PlayerControls();
        if (cam == null) cam = Camera.main;
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
        Vector2 activeInput = GetActiveInput();

        bool isMoving = activeInput.magnitude > 0.01f;

        if (_animator != null)
            _animator.SetBool("IsMoving", isMoving);
        
        if (ScoreManager.Instance != null)
            multiplier = Mathf.Min(1f + speedPerScore * ScoreManager.Instance.GetScore(), maxSpeedMultiplier);

        Vector3 move = new Vector3(activeInput.x, activeInput.y, 0f) * moveSpeed * multiplier;

        Vector3 target = transform.position + move;

        if (cam != null && cam.orthographic)
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 c = cam.transform.position;

            target.x = Mathf.Clamp(target.x, c.x - halfW + padding, c.x + halfW - padding);
            target.y = Mathf.Clamp(target.y, c.y - halfH + padding, c.y + halfH - padding);
        }

        rb.MovePosition(target);

        // Position is fully driven by MovePosition, so never let the dynamic body
        // build up momentum — otherwise it coasts when input stops.
        rb.linearVelocity = Vector2.zero;
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

        Debug.Log("Collided with: " + other.gameObject.name);

        if (other.CompareTag("Entity"))
        {
            Debug.Log("Tag matched");
            isGameOver = true;
            HandleGameOver();
        }
    }

    private void HandleGameOver()
    {
        rb.linearVelocity = Vector2.zero;
        enabled = false; // stops FixedUpdate from running again on this component

        if (_animator != null)
            _animator.SetBool("IsMoving", false);

        SceneManager.LoadScene(gameOverScene);
    }
}