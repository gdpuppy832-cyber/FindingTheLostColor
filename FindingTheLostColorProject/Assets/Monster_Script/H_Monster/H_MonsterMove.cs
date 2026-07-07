using UnityEngine;

public class H_MonsterMove : MonoBehaviour
{
    [Header("이동 및 추격 설정 (J_Monster 사양)")]
    [Tooltip("기본 이동 속도")]
    public float speed = 1.5f;

    [Tooltip("플레이어 추적을 개시할 감지 반경 (J_Monster 사양)")]
    public float range = 4.0f;

    [Tooltip("플레이어 추적을 중단할 상실 반경 (J_Monster 사양)")]
    public float chaseRange = 7.0f;

    [Tooltip("공격 직전 멈춰설 대상과의 거리")]
    public float attackStopDistance = 1.5f;

    Transform target;
    float timer = 0;
    Vector3 prevposition;
    Rigidbody2D rigid;
    bool groundedLeft = true;
    bool groundedRight = true;
    bool isStopped = false;
    float stopTimer = 0f;
    float ignoreEdgeTimer = 0f;
    float moveDir = -1f; // 현재 이동 방향 (배회 모드 기준, 절벽에서 반전시킬 때 사용)
    private SpriteRenderer spriteRenderer;
    private Collider2D col;
    bool isChasing = false;

    [Header("잠복(Ambushed) 설정")]
    [Tooltip("플레이어가 이 거리 이내로 들어오면 잠복에서 깨어납니다.")]
    public float detectionRange = 5f;

    [Tooltip("애니메이터의 잠복 상태 변수명 (bool)")]
    public string ambushAnimBool = "isAmbushed";

    [Header("잠복 시 Y축 오프셋 설정")]
    [Tooltip("잠복(수풀) 상태일 때 적용할 Y축 오프셋 (수풀이 공중에 뜰 때 마이너스 값으로 조절)")]
    public float ambushYOffset = 0f;

    [Header("잠복 시 콜라이더 크기 조절")]
    [Tooltip("잠복(수풀) 상태일 때 사용할 콜라이더 크기 (Box 또는 Capsule Collider 2D용)")]
    public Vector2 ambushColliderSize = new Vector2(1f, 1f);

    [Tooltip("잠복(수풀) 상태일 때 사용할 콜라이더 오프셋")]
    public Vector2 ambushColliderOffset = new Vector2(0f, 0f);

    [Header("스프라이트 폴백 설정 (애니메이션이 없는 경우 대비)")]
    [Tooltip("잠복 상태일 때 보여줄 이미지 (모습 1)")]
    public Sprite ambushSprite;

    [Tooltip("잠복 해제 후 활동할 때 보여줄 이미지 (모습 2)")]
    public Sprite activeSprite;

    private Animator animator;
    private bool isAmbushed = true;
    private bool isPouncing = false; // 최초 덮치기 점프 실행 중 여부

    private Vector3 originalSpriteLocalPos;
    private Vector2 originalColliderOffset;
    private Vector2 originalColliderSize;
    private bool hasCachedOffsets = false;
    private bool hasCachedCollider = false;

    public bool IsAmbushed => isAmbushed;

    void Start()
    {
        prevposition = transform.position;
        rigid = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        // 최초 오프셋 저장
        if (spriteRenderer != null)
        {
            originalSpriteLocalPos = spriteRenderer.transform.localPosition;
            hasCachedOffsets = true;
        }

        // 콜라이더 타입별 크기 캐싱
        if (col != null)
        {
            originalColliderOffset = col.offset;
            if (col is BoxCollider2D boxCol)
            {
                originalColliderSize = boxCol.size;
                hasCachedCollider = true;
            }
            else if (col is CapsuleCollider2D capCol)
            {
                originalColliderSize = capCol.size;
                hasCachedCollider = true;
            }
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            target = player.transform;

        // 게임 시작 시 무조건 잠복 상태로 리셋
        GoToSleep();
    }

    void Update()
    {
        if (target == null) return;

        float distance = Vector3.Distance(transform.position, target.position);

        // 1. 플레이어 거리 기반 잠복(Ambush) 해제 & 최초 덮치기 점프 공격 트리거
        if (isAmbushed)
        {
            if (distance <= detectionRange && !isPouncing)
            {
                isPouncing = true;

                // H_MonsterAttack 스크립트가 있다면 최초 덮치기 점프를 즉시 요청
                H_MonsterAttack attackScript = GetComponent<H_MonsterAttack>();
                if (attackScript == null) attackScript = GetComponentInChildren<H_MonsterAttack>();

                if (attackScript != null)
                {
                    attackScript.TriggerPounce();
                    Debug.Log($"[H_MonsterMove] {gameObject.name} 플레이어 감지! 최초 돌진 덮치기를 개시합니다.");
                }
                else
                {
                    // 공격 스크립트가 없을 시 즉시 강제 기상
                    WakeUp();
                }
            }
        }
        // ★ [요구사항] 한 번 잠복에서 일어난 이후에는 플레이어가 멀어져도 다시 위장으로 잠들지 않습니다. (GoToSleep 호출 제거)

        // 잠복(기상 대기/최초 덮치기 도약 대기) 중일 때는 정찰 이동 및 좌우 회전 등의 자체 이동 로직 완전 차단
        if (isAmbushed)
        {
            return;
        }

        // --- 이하는 EnemyMove 계열과 동일한 절벽 감지/추적 복귀 로직 ---
        if (ignoreEdgeTimer > 0f)
            ignoreEdgeTimer -= Time.deltaTime;

        // 추적 시작/종료 판정은 isStopped 상태와 무관하게 항상 먼저 체크
        // (isStopped 블록 뒤에 두면, 절벽에서 멈춰있는 동안 추적 종료 조건이 검사되지 않아
        //  플레이어가 멀어져도 isChasing이 계속 true로 남는 문제가 있었음)
        if (!isChasing && distance <= range)
        {
            isChasing = true;
        }
        else if (isChasing && distance > chaseRange)
        {
            isChasing = false;
            timer = 0f; // 배회 모드로 깨끗하게 복귀하도록 타이머 리셋

            if (isStopped) stopTimer = 0f; // 절벽에서 대기 중이었다면 배회 반전 흐름으로 자연스럽게 이어지도록 리셋
        }

        if (isStopped)
        {
            // 추적 중이었다면: 매 프레임 플레이어 방향을 다시 계산해서,
            // 그 방향이 절벽이 아니면(반대쪽으로 갔거나 안전해지면) 즉시 대기 해제
            if (isChasing)
            {
                float xDiff = target.position.x - transform.position.x;
                if (Mathf.Abs(xDiff) > attackStopDistance)
                {
                    float wantDir = Mathf.Sign(xDiff);
                    bool wantDirIsEdge = (wantDir < 0f && !groundedLeft) || (wantDir > 0f && !groundedRight);
                    if (!wantDirIsEdge)
                    {
                        isStopped = false;
                        stopTimer = 0f;
                    }
                }
                return; // 절벽 방향을 계속 원할 때만 대기 유지
            }

            stopTimer += Time.deltaTime;
            if (stopTimer >= 0.5f)
            {
                isStopped = false;
                stopTimer = 0f;
                moveDir = -moveDir; // 반대 방향으로 전환 (배회 모드 전용)
                ignoreEdgeTimer = 1.3f; // 전환 직후 짧게 재감지 무시
                timer = moveDir < 0f ? 0f : 3.5f; // 배회 타이머도 반전된 방향에 맞게 재설정
            }
            return;
        }

        timer += Time.deltaTime;

        // 1. 원래 하고 싶은 이동 방향
        float desiredDir = 0f;
        if (isChasing)
        {
            float xDiff = target.position.x - transform.position.x;
            if (Mathf.Abs(xDiff) > attackStopDistance)
                desiredDir = Mathf.Sign(xDiff);
        }
        else if (timer < 3f)
        {
            desiredDir = -1f;
        }
        else if (timer > 3.5f && timer < 6.5f)
        {
            desiredDir = 1f;
        }
        else if (timer > 7f)
        {
            timer = 0f;
        }

        if (desiredDir == 0f)
        {
            prevposition = transform.position;
            return;
        }

        // 2. 이동하려는 방향 쪽에 땅이 없으면(절벽) 즉시 반전하지 않고 멈춤 상태로 전환
        bool edgeAhead = (desiredDir < 0f && !groundedLeft) || (desiredDir > 0f && !groundedRight);

        // 유예 시간(ignoreEdgeTimer)은 "배회 모드에서 방금 반전한 방향(moveDir)으로 계속 갈 때"만 적용.
        // 추적이 반대 방향(절벽 쪽)으로 끌어당기는 경우엔 방향이 다르므로 유예 시간과 무관하게
        // 항상 절벽을 감지해야 함 - 안 그러면 반전 직후 유예 시간 동안 추적이 절벽 방향으로 끼어들 때
        // 절벽 감지가 무시된 채 몇 걸음 걸어가버리는 문제가 있음
        bool suppressCheck = !isChasing && ignoreEdgeTimer > 0f && desiredDir == moveDir;

        if (edgeAhead && !suppressCheck)
        {
            isStopped = true;
            stopTimer = 0f;
            return;
        }

        float moveSpeed = isChasing ? speed * 1.5f : speed;
        transform.Translate(moveSpeed * desiredDir * Time.deltaTime, 0f, 0f);
        moveDir = desiredDir;

        float velocityX = transform.position.x - prevposition.x;
        if (velocityX != 0)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * -Mathf.Sign(velocityX);
            transform.localScale = scale;
        }
        prevposition = transform.position;
    }

    void FixedUpdate()
    {
        // 잠복 중에는 고지/낙하 체크 패스
        if (isAmbushed) return;

        float halfWidth = col.bounds.extents.x;
        float oneThird = halfWidth * 2f / 3f;

        Vector2 leftPoint = (Vector2)rigid.position + Vector2.left * oneThird;
        Vector2 rightPoint = (Vector2)rigid.position + Vector2.right * oneThird;

        RaycastHit2D leftHit = Physics2D.Raycast(leftPoint, Vector2.down, 2, LayerMask.GetMask("Platform"));
        RaycastHit2D rightHit = Physics2D.Raycast(rightPoint, Vector2.down, 2, LayerMask.GetMask("Platform"));

        groundedLeft = leftHit.collider != null;
        groundedRight = rightHit.collider != null;
    }

    /// <summary>
    /// 잠복에서 완전히 깨어납니다. (점프가 공중으로 발사되는 시점에 호출됨)
    /// </summary>
    public void WakeUp()
    {
        isAmbushed = false;
        isPouncing = false;

        // 1. 오프셋 및 콜라이더 복구 (원래 크기로 돌림)
        ApplyVisualOffset(false);
        ApplyColliderSettings(false);

        // 2. 애니메이터 활성화 (활동 상태)
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool(ambushAnimBool, false);
        }

        // 3. 스프라이트 이미지 대체 적용 (모습 2)
        if (activeSprite != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = activeSprite;
        }

        Debug.Log($"[H_MonsterMove] {gameObject.name} 최초 돌진 도약 시작! 위장을 영구 해제합니다 (무적 풀림).");
    }

    /// <summary>
    /// 다시 잠복 모드로 들어가 무적이 되고 웅크립니다. (최초 로딩 시에만 사용됨)
    /// </summary>
    public void GoToSleep()
    {
        isAmbushed = true;
        isChasing = false;
        isPouncing = false;

        // 1. 잠복 오프셋 및 콜라이더 설정 적용 (수풀에 맞는 콜라이더 크기 조정)
        ApplyVisualOffset(true);
        ApplyColliderSettings(true);

        // 2. 애니메이터 활성화 (잠복 상태)
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool(ambushAnimBool, true);
        }

        // 3. 스프라이트 이미지 대체 적용 (모습 1)
        if (ambushSprite != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = ambushSprite;
        }

        // 4. 움직임 정지
        if (rigid != null)
        {
            rigid.linearVelocity = Vector2.zero;
        }
    }

    /// <summary>
    /// 덮치기 점프가 최소 거리 판정 등의 사유로 시작 전 취소되었을 때 복구
    /// </summary>
    public void CancelPounce()
    {
        isPouncing = false;
        GoToSleep();
    }

    /// <summary>
    /// 잠복 상태에 따른 스프라이트/본체 Y 오프셋을 처리합니다.
    /// </summary>
    private void ApplyVisualOffset(bool apply)
    {
        if (!hasCachedOffsets) return;

        if (apply)
        {
            if (spriteRenderer != null)
            {
                if (spriteRenderer.transform != transform)
                {
                    // 스프라이트 렌더러가 자식 오브젝트인 경우 로컬 Y 위치만 조절
                    spriteRenderer.transform.localPosition = originalSpriteLocalPos + Vector3.up * ambushYOffset;
                }
                else
                {
                    // 본체에 직접 붙어있는 경우, 본체 콜라이더 오프셋만 조절
                    if (col != null)
                    {
                        col.offset = originalColliderOffset - new Vector2(0f, ambushYOffset);
                    }
                }
            }
        }
        else
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.transform.localPosition = originalSpriteLocalPos;
            }
            if (col != null)
            {
                col.offset = originalColliderOffset;
            }
        }
    }

    /// <summary>
    /// 잠복 상태에 맞추어 콜라이더 크기(Size)와 오프셋(Offset)을 다이내믹하게 스위칭합니다.
    /// </summary>
    private void ApplyColliderSettings(bool applyAmbush)
    {
        if (!hasCachedCollider || col == null) return;

        if (applyAmbush)
        {
            col.offset = ambushColliderOffset;
            if (col is BoxCollider2D boxCol)
            {
                boxCol.size = ambushColliderSize;
            }
            else if (col is CapsuleCollider2D capCol)
            {
                capCol.size = ambushColliderSize;
            }
        }
        else
        {
            col.offset = originalColliderOffset;
            if (col is BoxCollider2D boxCol)
            {
                boxCol.size = originalColliderSize;
            }
            else if (col is CapsuleCollider2D capCol)
            {
                capCol.size = originalColliderSize;
            }
        }
    }
}