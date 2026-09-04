using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerStart : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [SerializeField] private Animator _animator;

    private Vector2 moveInput;
    private Vector2 keyboardInput;
    private PlayerControls controls;
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        try
        {
            controls = new PlayerControls();
            Debug.Log("PlayerControls created successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to create PlayerControls: " + e);
        }
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
            case ControlType.Keyboard:
                return keyboardInput;

            case ControlType.Tilt:
                return GetTiltInput();

            case ControlType.Touch:
                return moveInput; // already reflects inversion, since TouchControls applies its own flip internally

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

        Vector3 move = new Vector3(activeInput.x, activeInput.y, 0f) * moveSpeed;

        rb.MovePosition(transform.position + move);
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
        }
    }
}