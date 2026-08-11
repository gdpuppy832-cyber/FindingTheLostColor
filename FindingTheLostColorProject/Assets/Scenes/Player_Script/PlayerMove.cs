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
    private float lastGroundedTime = 0f; // [추가] 마지막으로 땅을 딛고 서 있던 시간 (코요테 타임용)
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

    public bool IsDashing => isDashing; // [추가] 외부 스크립트에서 대쉬 상태 여부를 파악할 수 있는 Getter
    private GaugeController gaugeController; // [추가] 물감 충전 여부에 따른 속도 감소용 참조

    [Header("집중 충전 속도 설정 (신규)")]
    [Range(0f, 1f)]
    [Tooltip("집중 충전(R키 꾹 누름) 시 이동 속도 비율 (0.2면 80% 감소, 0이면 완전 정지, 기본값: 0.2)")]
    public float focusChargeSpeedMultiplier = 0.2f;

    [Header("대쉬 효과음 설정 (신규)")]
    [Tooltip("대쉬 발동 시 재생할 효과음 AudioClip (비워둘 시 무음)")]
    public AudioClip dashSFX;
    [Tooltip("대쉬 효과음 시작 오프셋 시간 (초, 앞부분 자를 구간, 기본값: 0.1초)")]
    public float dashSFXStartOffset = 0.1f;



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

    [Header("Dash Gauge HUD")]
    [Tooltip("플레이어 옆 아치형 대쉬 게이지 컨트롤러")]
    public DashGaugeController dashGaugeCtrl;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        gaugeController = FindFirstObjectByType<GaugeController>();

        // 대쉬 인디케이터 초기 상태를 밝은 색(사용 가능)으로 초기화
        if (dashIndicatorSR != null)
        {
            dashIndicatorSR.color = dashReadyColor;
        }
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

        float moveInput = 0f;
        bool jumpPressed = false;
        bool dashPressed = false;


        // 조작 가능한 상태일 때만 키보드 입력 허용
        if (canControl && !isDashing)
        {
            // KeyBindManager 동적 단축키 연동 적용
            KeyCode leftKey = (KeyBindManager.Instance != null) ? KeyBindManager.Instance.LeftKey : KeyCode.A;
            KeyCode rightKey = (KeyBindManager.Instance != null) ? KeyBindManager.Instance.RightKey : KeyCode.D;
            KeyCode jumpKey = (KeyBindManager.Instance != null) ? KeyBindManager.Instance.JumpKey : KeyCode.Space;
            KeyCode dashKey = (KeyBindManager.Instance != null) ? KeyBindManager.Instance.DashKey : KeyCode.LeftShift;

            if (Input.GetKey(leftKey)) moveInput -= 1f;
            if (Input.GetKey(rightKey)) moveInput += 1f;
            if (Input.GetKeyDown(jumpKey)) jumpPressed = true;
            if (Input.GetKeyDown(dashKey)) dashPressed = true;

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

            // 대쉬 시도 조건 (S_Monster 슬로우 장판의 대쉬 차단 여부 검사)
            PlayerTrailDebuff trailDebuff = GetComponent<PlayerTrailDebuff>();
            bool isDashBlockedByTrail = (trailDebuff != null && trailDebuff.DashMultiplier <= 0.005f);

            if (dashPressed && canDash && !isDashBlockedByTrail)
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
    /// 외부에서 플레이어의 조작 가능 여부를 제어하는 함수
    /// </summary>
    /// <param name="value">true: 조작 가능, false: 조작 불가능</param>
    public void SetControl(bool value)
    {
        canControl = value;

        if (!canControl)
        {
            moveDirection = Vector2.zero;

            // [컷씬 진입 시 속도 고정 버그 방탄 처리]
            // 키를 누르고 있던 와중에 조작을 잃을 경우, 기존 X축 속도가 고정되는 현상을 즉각 차단
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
    /// 좌우 시선 방향에 기초한 짧은 수평 대쉬 연산 코루틴
    /// </summary>
    private System.Collections.IEnumerator DashRoutine()
    {
        canDash = false;
        isDashing = true;

        // 대쉬 효과음 재생 (등록된 오디오 클립 및 시작 오프셋 사용)
        if (dashSFX != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX(dashSFX, dashSFXStartOffset);
        }

        // 아치형 대쉬 쿨타임 게이지 UI 구동
        if (dashGaugeCtrl != null)
        {
            dashGaugeCtrl.StartDashGauge(dashCooldown);
        }

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

        // 대쉬 중 실시간 슬로우 장판 효율 디버프 갱신
        PlayerTrailDebuff trailDebuff = GetComponent<PlayerTrailDebuff>();

        while (elapsed < dashDuration)
        {
            float currentDashMult = trailDebuff != null ? trailDebuff.DashMultiplier : 1.0f;

            // 대쉬 속도로 수평 방향으로 강하게 추진(슬로우 장판 배율 곱함)하며 수직 낙하는 0으로 굳힘
            rb.linearVelocity = new Vector2(dashDir * dashSpeed * currentDashMult, 0f);
            
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

    /// <summary>
    /// [추가] 플레이어 발바닥 기준 아래 방향으로 0.1m 이내에 지면(Ground)이 존재하는지 박스 투사로 정밀 검사합니다.
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