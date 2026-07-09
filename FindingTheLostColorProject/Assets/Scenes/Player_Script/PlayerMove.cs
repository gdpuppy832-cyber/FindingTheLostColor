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

    [Header("Dash Settings")]
    [Tooltip("대쉬 속도 (기본값: 15.0)")]
    public float dashSpeed = 15f;
    [Tooltip("대쉬 지속 시간 (초, 기본값: 0.2초)")]
    public float dashDuration = 0.18f;
    [Tooltip("대쉬 쿨타임 (초, 기본값: 0.8초)")]
    public float dashCooldown = 0.8f;

    private bool isDashing = false;
    private bool canDash = true;

    [Header("Dash Visuals")]
    [Tooltip("대쉬 쿨타임 표시용 플레이어 발밑 원형 SpriteRenderer")]
    public SpriteRenderer dashIndicatorSR;
    [Tooltip("대쉬 가능(쿨타임 완료) 시의 원형 색상")]
    public Color dashReadyColor = Color.white;
    [Tooltip("대쉬 쿨타임 중(사용 불가) 시의 원형 색상")]
    public Color dashCooldownColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

    [Header("Dash Afterimage Settings")]
    [Tooltip("대쉬 중 잔상을 뿜어낼지 여부")]
    public bool spawnAfterimage = true;
    [Tooltip("잔상 스폰 간격 (초, 기본값: 0.04)")]
    public float afterimageInterval = 0.04f;
    [Tooltip("잔상 페이드아웃 속도 (기본값: 3.5)")]
    public float afterimageFadeSpeed = 3.5f;
    [Tooltip("잔상 고유 틴트 컬러")]
    public Color afterimageColor = new Color(0.3f, 0.75f, 1.0f, 0.55f);

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        // 대쉬 인디케이터 초기 상태를 밝은 색(사용 가능)으로 초기화
        if (dashIndicatorSR != null)
        {
            dashIndicatorSR.color = dashReadyColor;
        }
    }

    void Update()
    {
        float moveInput = 0f;
        bool jumpPressed = false;
        bool dashPressed = false;


        // 조작 가능한 상태일 때만 키보드 입력 허용
        if (canControl && !isDashing)
        {
#if ENABLE_INPUT_SYSTEM
            // New Input System 사용 시
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed) moveInput -= 1f;
                if (Keyboard.current.dKey.isPressed) moveInput += 1f;
                if (Keyboard.current.spaceKey.wasPressedThisFrame) jumpPressed = true;
                if (Keyboard.current.leftShiftKey.wasPressedThisFrame) dashPressed = true;
            }
#else
            // Legacy Input Manager 사용 시
            if (Input.GetKey(KeyCode.A)) moveInput -= 1f;
            if (Input.GetKey(KeyCode.D)) moveInput += 1f;
            if (Input.GetKeyDown(KeyCode.Space)) jumpPressed = true;
            if (Input.GetKeyDown(KeyCode.LeftShift)) dashPressed = true;
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

            // 대쉬 시도 조건
            if (dashPressed && canDash)
            {
                StartCoroutine(DashRoutine());
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
        // 대쉬 진행 중일 때는 일반 좌우 조작 물리 연산을 바이패스합니다.
        if (isDashing) return;

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

    /// <summary>
    /// 좌우 시선 방향에 기초한 짧은 수평 대쉬 연산 코루틴
    /// </summary>
    private System.Collections.IEnumerator DashRoutine()
    {
        canDash = false;
        isDashing = true;

        // 대쉬 사용 즉시 발밑 원형을 어두운 색상으로 변경
        if (dashIndicatorSR != null)
        {
            dashIndicatorSR.color = dashCooldownColor;
        }

        // 공중 대쉬 시 중력에 깎여 떨어지는 것을 차단하기 위해 중력 일시 해제
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0f;

        // 캐릭터가 최종적으로 바라본 방향 (localScale.x 의 부호 기반)
        float dashDir = Mathf.Sign(transform.localScale.x);

        // 플레이어 본체 스프라이트 렌더러 캐싱 (자식에 있는 경우 포함)
        SpriteRenderer playerSR = GetComponent<SpriteRenderer>();
        if (playerSR == null) playerSR = GetComponentInChildren<SpriteRenderer>();

        float elapsed = 0f;
        float lastAfterimageTime = -100f;

        while (elapsed < dashDuration)
        {
            // 대쉬 속도로 수평 방향으로 강하게 추진하며 수직 낙하는 0으로 굳힘
            rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0f);
            
            // 대쉬 중 정해진 시간 주기마다 뒤에 스프라이트 잔상 생성
            if (spawnAfterimage && playerSR != null && Time.time - lastAfterimageTime >= afterimageInterval)
            {
                lastAfterimageTime = Time.time;
                SpawnAfterimageObject(playerSR);
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 대쉬가 끝나면 중력 복구 및 제어권 해제
        rb.gravityScale = originalGravity;
        
        // 대쉬 직후 멈칫하지 않고 기존 물리 탄력을 부드럽게 이어받도록 수평 속도 리셋
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        
        isDashing = false;

        // 대쉬 쿨타임 대기
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;

        // 쿨타임 완주 즉시 발밑 원형을 밝은 색상으로 복원
        if (dashIndicatorSR != null)
        {
            dashIndicatorSR.color = dashReadyColor;
        }
    }

    /// <summary>
    /// 플레이어의 현재 시각적 상태(스프라이트, 스케일 등)를 복제하여 페이드아웃 잔상 오브젝트를 소환합니다.
    /// </summary>
    private void SpawnAfterimageObject(SpriteRenderer sourceSR)
    {
        // 1. 빈 게임오브젝트 생성 후 스프라이트 렌더러 및 제어 컴포넌트 추가
        GameObject afterimageObj = new GameObject("PlayerDashAfterimage");
        PlayerAfterimage afterimageScript = afterimageObj.AddComponent<PlayerAfterimage>();

        // 2. 현재 플레이어 본체의 시각 정보를 기반으로 껍데기 세팅
        afterimageScript.Setup(
            sourceSR.sprite,
            transform.position,
            transform.rotation,
            transform.localScale,
            afterimageColor,
            afterimageFadeSpeed,
            sourceSR.sortingOrder,
            sourceSR.sortingLayerName
        );
    }
}