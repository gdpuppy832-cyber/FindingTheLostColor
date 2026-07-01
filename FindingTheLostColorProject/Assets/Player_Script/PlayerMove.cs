using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMoveT : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 10f;
    public float jumpDelay = 0.2f;

    private Rigidbody2D rb;
    private int jumpCount = 0;
    private float lastJumpTime = 0f;
    private bool isGrounded = false;
    private Vector2 moveDirection;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        float moveInput = 0f;
        if (Input.GetKey(KeyCode.A)) moveInput -= 1f;
        if (Input.GetKey(KeyCode.D)) moveInput += 1f;

        moveDirection = new Vector2(moveInput, 0).normalized;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (jumpCount < 2 && Time.time - lastJumpTime >= jumpDelay)
            {
                // 점프 시 X축 속도는 유지하여 벽에서 점프 시 튕겨 나가게 함
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                jumpCount++;
                lastJumpTime = Time.time;
                isGrounded = false;
            }
        }
    }

    void FixedUpdate()
    {
        // 벽 감지 로직 없이 부드러운 이동 속도 적용
        rb.linearVelocity = new Vector2(moveDirection.x * moveSpeed, rb.linearVelocity.y);

        if (!isGrounded && jumpCount == 0)
        {
            jumpCount = 1;
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                isGrounded = true;
                jumpCount = 0;
                return;
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        isGrounded = false;
    }
}