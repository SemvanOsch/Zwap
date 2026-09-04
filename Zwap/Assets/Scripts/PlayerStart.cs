using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerStart : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [SerializeField] private Animator _animator;

    private Vector3 startPos;
    private Vector2 moveInput;
    private Vector2 keyboardInput;
    private PlayerControls controls;
    private Rigidbody2D rb;

    public void AddInput(Vector2 dir)
    {
        moveInput += dir;
    }

    public void RemoveInput(Vector2 dir)
    {
        moveInput -= dir;
    }

    private void Awake()
    {
        startPos = transform.position;
        rb = GetComponent<Rigidbody2D>();
        controls = new PlayerControls();
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
        Vector2 combined = moveInput + keyboardInput;

        bool isMoving = combined.magnitude > 0.01f;
        _animator.SetBool("IsMoving", isMoving);

        Vector2 tiltInput = GetTiltInput();
        Vector2 combined = moveInput + keyboardInput + tiltInput;
        Vector3 move = new Vector3(combined.x, combined.y, 0f) * moveSpeed;
        rb.MovePosition(transform.position + move);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Collided with: " + other.gameObject.name);

        if (other.gameObject.CompareTag("Entity"))
        {
             Debug.Log("Tag matched");
            // SceneManager.LoadScene("Home-Screen");
        }
    }
}