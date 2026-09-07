using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerStart : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Bounds")] [SerializeField] private Camera cam;
    [SerializeField] private float padding = 0.5f; // keeps the sprite fully on scree

    [SerializeField] private Animator _animator;
    
    [Header("Inverse")]
    [SerializeField] private TouchControls touchControls;
    // To inverse control do touchControls.ToggleInverse();
    // To set back to normal do touchControls.SetNormal();

    private Vector2 moveInput;
    private Vector2 keyboardInput;
    private PlayerControls controls;
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        controls = new PlayerControls();
        if (cam == null) cam = Camera.main;
    }

    private void OnEnable()
    {
        controls.Player.Move.performed += OnKeyboardMove;
        controls.Player.Move.canceled += OnKeyboardStop;
        controls.Player.Enable();

        if (Accelerometer.current != null)
            InputSystem.EnableDevice(Accelerometer.current);
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

    private void FixedUpdate()
    {
        // BELANGRIJK: eerst tiltInput maken
        Vector2 tiltInput = GetTiltInput();

        // Input combineren
        Vector2 combined = moveInput + keyboardInput + tiltInput;

        // Animatie
        bool isMoving = combined.magnitude > 0.01f;

        if (_animator != null)
            _animator.SetBool("IsMoving", isMoving);

        // Beweging
        Vector3 move = new Vector3(combined.x, combined.y, 0f) * moveSpeed;

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
    }

    public void AddInput(Vector2 dir)
    {
        moveInput += dir;
    }

    public void RemoveInput(Vector2 dir)
    {
        moveInput -= dir;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Collided with: " + other.gameObject.name);

        if (other.CompareTag("Entity"))
        {
            Debug.Log("Tag matched");

            // SceneManager.LoadScene("Home-Screen");
        }
    }
}