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

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)
                input.x -= 1f;

            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed)
                input.x += 1f;

            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)
                input.y += 1f;

            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)
                input.y -= 1f;
        }

        var gp = Gamepad.current;
        if (input == Vector2.zero && gp != null)
        {
            input = gp.leftStick.ReadValue();
            if (input == Vector2.zero)
            {
                input = gp.dpad.ReadValue();
            }
        }
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = input.normalized * moveSpeed;
    }
}
