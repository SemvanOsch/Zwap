using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerStart : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    
    private Vector3 startPos;
    private Vector2 moveInput;
    private Vector2 keyboardInput;
    private PlayerControls controls;
    private Rigidbody2D rb;

    public void AddInput(Vector2 dir)    { moveInput += dir; }
    public void RemoveInput(Vector2 dir) { moveInput -= dir; }
    
    private void Awake()
    {
        startPos = transform.position;
        rb = GetComponent<Rigidbody2D>();
        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        controls.Player.Move.performed += OnKeyboardMove;
        controls.Player.Move.canceled  += OnKeyboardStop;
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Move.performed -= OnKeyboardMove;
        controls.Player.Move.canceled  -= OnKeyboardStop;
        controls.Player.Disable();
    }

    private void OnKeyboardMove(InputAction.CallbackContext ctx)
    {
        keyboardInput = ctx.ReadValue<Vector2>();
    }

    private void OnKeyboardStop(InputAction.CallbackContext ctx)
    {
        keyboardInput = Vector2.zero;
    }
    
    private void FixedUpdate()
    {
        Vector2 combined = moveInput + keyboardInput;
        Vector3 move = new Vector3(combined.x, combined.y, 0f) * moveSpeed;
        rb.MovePosition(transform.position + move);
    }
}

public class PlayerCollision : MonoBehaviour
{
    void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log("Collided with: " + collision.gameObject.name);

        if (collision.gameObject.CompareTag("Rock"))
        {
            Debug.Log("Tag matched, loading scene");
            SceneManager.LoadScene("Home-Screen");
        }
    }
}