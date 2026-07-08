using UnityEngine;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMove : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 7f;
    public float jumpForce = 11f;
    public float jumpDelay = 0.1f;

    private Rigidbody2D rb;
    private Animator animator;
    private int jumpCount = 0;
    private float lastJumpTime = 0f;
    private bool isGrounded = false;
    private Vector2 moveDirection;
    private bool canControl = true; // 조작 가능 상태 플래그

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        float moveInput = 0f;
        bool jumpPressed = false;


        // 조작 가능한 상태일 때만 키보드 입력 허용
        if (canControl)
        {
#if ENABLE_INPUT_SYSTEM
            // New Input System 사용 시
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed) moveInput -= 1f;
                if (Keyboard.current.dKey.isPressed) moveInput += 1f;
                if (Keyboard.current.spaceKey.wasPressedThisFrame) jumpPressed = true;
            }
#else
            // Legacy Input Manager 사용 시
            if (Input.GetKey(KeyCode.A)) moveInput -= 1f;
            if (Input.GetKey(KeyCode.D)) moveInput += 1f;
            if (Input.GetKeyDown(KeyCode.Space)) jumpPressed = true;
#endif

            if (jumpPressed)
            {
                if (jumpCount < 2 && Time.time - lastJumpTime >= jumpDelay)
                {
                    // 낙하 중 점프 시 중력 속도에 의해 점프가 씹히는 현상을 방지하기 위해 Y축 속도만 0으로 초기화
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                    // 점프 시 X축 속도는 유지하여 벽에서 점프 시 튕겨 나가게 함
                    rb.AddForce(new Vector2(0, jumpForce), ForceMode2D.Impulse);

                    // 공중에서 두 번째 점프를 가했을 때를 '더블점프'로 명확히 판정하여 신호 전송
                    if (jumpCount == 1)
                    {
                        Debug.Log("[PlayerMove] 공중 더블점프(2단 점프) 작동!");
                        if (animator != null)
                        {
                            animator.SetTrigger("DoubleJump");
                        }
                    }
                    else
                    {
                        animator.SetTrigger("OnJump");
                    }

                    jumpCount++;
                    lastJumpTime = Time.time;
                    isGrounded = false;
                }
            }

            // [추가] 키 입력 방향(moveInput)에 따라 캐릭터 좌우 스케일 반전 (뒤집기)
            if (moveInput > 0.01f)
            {
                // 오른쪽을 볼 때 (양수 스케일)
                transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
            }
            else if (moveInput < -0.01f)
            {
                // 왼쪽을 볼 때 (음수 스케일)
                transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
            }
        }

        moveDirection = new Vector2(moveInput, 0).normalized;
    }

    void FixedUpdate()
    {
        // 조작 가능한 상태일 때만 키 입력에 따른 좌우 이동 속도 적용 (넉백 등의 물리 외력 보존을 위함)
        if (canControl)
        {
            rb.linearVelocity = new Vector2(moveDirection.x * moveSpeed, rb.linearVelocity.y);
        }

        if (!isGrounded && jumpCount == 0)
        {
            jumpCount = 1;
        }

        animator.SetFloat("VelocityX", moveDirection.x * moveSpeed);
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

    /// <summary>
    /// 외부에서 플레이어의 조작 가능 여부를 제어하는 함수
    /// </summary>
    /// <param name="value">true: 조작 가능, false: 조작 불가능</param>
    public void SetControl(bool value)
    {
        canControl = value;

        if (!canControl)
        {
            moveDirection = Vector2.zero;
        }
    }
}