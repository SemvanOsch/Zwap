using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerStart : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    
    private Vector3 startPos;
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
        throw new NotImplementedException();
    }


    // Update is called once per frame
    void Update()
    {
        
    }

    private void FixedUpdate()
    {
        throw new NotImplementedException();
    }
}
