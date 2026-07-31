using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class AutoRunnerMove : MonoBehaviour
{
    [Header("Auto Run Settings")]
    [Tooltip("자동으로 오른쪽으로 달리는 속도")]
    public float moveSpeed = 7f;

    [Header("Jump Settings")]
    public float jumpForce = 11f;
    public float jumpDelay = 0.1f;

    private Rigidbody2D rb;
    private Animator animator;
    private int jumpCount = 0;
    private float lastJumpTime = 0f;
    private bool isGrounded = false;
    private float lastGroundedTime = 0f; // 마지막으로 땅을 딛고 서 있던 시간 (코요테 타임용)
    private Vector2 moveDirection;
    private bool canControl = true; // 조작(점프) 가능 상태 플래그

    private GaugeController gaugeController; // 물감 충전 여부에 따른 속도 감소용 참조

    [Header("집중 충전 속도 설정")]
    [Range(0f, 1f)]
    [Tooltip("집중 충전(R키 꾹 누름) 시 이동 속도 비율 (0.2면 80% 감소, 0이면 완전 정지, 기본값: 0.2)")]
    public float focusChargeSpeedMultiplier = 0.2f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        gaugeController = FindFirstObjectByType<GaugeController>();

        // 오토러너는 항상 오른쪽을 바라보며 시작
        moveDirection = Vector2.right;
    }

    void Update()
    {
        // [추가] 일시정지(Pause) 상태일 때는 키 입력을 완전히 차단하고, 잔여 속도를 동결하여 미끄러짐 방지
        if (PauseManager.IsPaused)
        {
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            if (animator != null)
            {
                animator.SetBool("IsWalking", false);
            }
            return;
        }

        bool jumpPressed = false;

        // 좌우 입력은 완전히 제거됨 - 항상 오른쪽으로 자동 이동
        moveDirection = Vector2.right;

        // 항상 걷는 애니메이션이 나오도록 유지
        if (animator != null)
        {
            animator.SetBool("IsWalking", true);
        }

        // 조작 가능한 상태일 때만 점프 입력 허용 (좌우 입력은 사용하지 않음)
        if (canControl)
        {
            KeyCode jumpKey = (KeyBindManager.Instance != null) ? KeyBindManager.Instance.JumpKey : KeyCode.Space;

            if (Input.GetKeyDown(jumpKey)) jumpPressed = true;

            // [신규] 지면 근접 판정 전처리 (상승 중이거나 점프 직후 0.15초 이내는 스킵하여 3단 점프 오작동 원천 방지)
            bool isAscending = rb != null && rb.linearVelocity.y > 0.01f;
            bool justJumped = (Time.time - lastJumpTime < 0.15f);

            if (!isAscending && !justJumped && CheckNearGround())
            {
                isGrounded = true;
                lastGroundedTime = Time.time;
                jumpCount = 0; // 1단 점프 가능 상태로 리셋
            }

            if (jumpPressed)
            {
                if (jumpCount < 2 && Time.time - lastJumpTime >= jumpDelay)
                {
                    // 낙하 중 점프 시 중력 속도에 의해 점프가 씹히는 현상을 방지하기 위해 Y축 속도만 0으로 초기화
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
                    // 점프 시 X축 속도는 유지하여 벽에서 점프 시 튕겨 나가게 함
                    rb.AddForce(new Vector2(0, jumpForce), ForceMode2D.Impulse);

                    // 점프 효과음 재생
                    if (SoundManager.Instance != null)
                    {
                        SoundManager.Instance.PlaySFX(SoundManager.SFXType.Jump, 0.8f);
                    }

                    // 공중에서 두 번째 점프를 가했을 때를 '더블점프'로 명확히 판정하여 신호 전송
                    if (jumpCount == 1)
                    {
                        Debug.Log("[AutoRunnerMove] 공중 더블점프(2단 점프) 작동!");
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
        }
    }

    void FixedUpdate()
    {
        // 조작 가능한 상태일 때만 자동 이동 속도 적용 (넉백 등의 물리 외력 보존을 위함)
        if (canControl)
        {
            float activeSpeed = moveSpeed;
            // R키 꾹 눌러 집중 충전 중인 경우 이동 속도 감소 비율 적용 (기본 80% 감속)
            if (gaugeController != null && gaugeController.IsFocusCharging)
            {
                activeSpeed = moveSpeed * focusChargeSpeedMultiplier;
            }
            rb.linearVelocity = new Vector2(moveDirection.x * activeSpeed, rb.linearVelocity.y);
        }

        // 땅을 벗어난 뒤 0.1초(코요테 타임) 동안은 공중 강제 판정(jumpCount=1)을 유예하여 기본점프를 보장
        if (!isGrounded && jumpCount == 0)
        {
            if (Time.time - lastGroundedTime >= 0.1f)
            {
                jumpCount = 1;
            }
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
                lastGroundedTime = Time.time; // 땅을 딛고 있는 동안 실시간 시간 갱신
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
    /// 외부에서 플레이어의 조작(점프) 가능 여부를 제어하는 함수
    /// </summary>
    /// <param name="value">true: 조작 가능, false: 조작 불가능</param>
    public void SetControl(bool value)
    {
        canControl = value;

        if (!canControl)
        {
            // 조작이 막히면 자동 이동도 함께 멈춤
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
            if (animator != null)
            {
                animator.SetFloat("VelocityX", 0f);
            }
        }
    }

    /// <summary>
    /// 플레이어 발바닥 기준 아래 방향으로 0.1m 이내에 지면(Ground)이 존재하는지 박스 투사로 정밀 검사합니다.
    /// </summary>
    private bool CheckNearGround()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) col = GetComponentInChildren<Collider2D>();
        if (col == null) return false;

        // 플레이어 콜라이더 하단 면으로부터 아래로 0.07m + 미세 작동 마진 0.01f 총 0.08f 상자 캐스트
        float checkDistance = 0.08f;
        Vector2 boxSize = new Vector2(col.bounds.size.x * 0.85f, 0.05f); // 발폭보다 미세하게 좁은 상자 크기
        Vector2 boxCenter = new Vector2(col.bounds.center.x, col.bounds.min.y - 0.01f);

        // Player 레이어는 감지에서 제외하여 자기 자신 충돌 방지
        int playerLayer = LayerMask.NameToLayer("Player");
        int layerMask = ~(1 << playerLayer);

        RaycastHit2D hit = Physics2D.BoxCast(boxCenter, boxSize, 0f, Vector2.down, checkDistance, layerMask);

        // 감지된 지형 콜라이더가 있고, 트리거 성격의 감지 영역이 아닐 경우
        if (hit.collider != null && !hit.collider.isTrigger)
        {
            // 접촉한 바닥면의 각도가 평평하거나 완만한 서 있을 수 있는 경사인지 확인
            if (hit.normal.y > 0.5f)
            {
                return true;
            }
        }
        return false;
    }
}