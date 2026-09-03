using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerStart : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    
    private Vector3 startPos;
    private Vector2 moveInput;
    private PlayerControls controls;
    private Rigidbody2D rb;
    
    // Loads values when object is created
    private void Awake()
    {
        startPos = transform.position;
        rb = GetComponent<Rigidbody2D>();
        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx =>  moveInput = Vector2.zero;
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Move.performed -= ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled -= ctx =>  moveInput = Vector2.zero;
        controls.Player.Disable();
    }
    
    
    // Update once every frame on set timer
    private void FixedUpdate()
    {
        Vector3 move = new Vector3(moveInput.x, moveInput.y, 0f) * moveSpeed;
        rb.MovePosition(transform.position + move);
    }
}
