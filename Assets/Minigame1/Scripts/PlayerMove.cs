using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4f;

    private Rigidbody2D rb;
    private Vector2 input;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        input = Vector2.zero;

        if (Keyboard.current.aKey.isPressed)
            input.x = -1;

        if (Keyboard.current.dKey.isPressed)
            input.x = 1;

        if (Keyboard.current.wKey.isPressed)
            input.y = 1;

        if (Keyboard.current.sKey.isPressed)
            input.y = -1;
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = input.normalized * moveSpeed;
    }
}