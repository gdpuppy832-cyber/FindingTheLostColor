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

    [Tooltip("이 거리 안에 낮은 땅이라도 있으면 낭떠러지로 판정하지 않고 이동을 허용함 (계단/턱 내려가기 허용, 추적 모드에서만 적용)")]
    public float safeDropDistance = 3f;
    [Header("점프 설정")]
    public float jumpForce = 5f;
    public float climbableWallHeight = 1.2f;

    [Header("잠복 → 덮치기 연출 타이밍")]
    [Tooltip("풀숲의 InPlayer 파라미터 발동 후, IsHunting 파라미터가 발동되기까지 대기 시간 (초)")]
    public float inPlayerToHuntingDelay = 0.75f;

    [Tooltip("풀숲의 IsHunting 파라미터 발동 후, 몬스터 본체가 나오며 점프 공격 시퀀스가 시작되기까지 대기 시간 (초)")]
    public float huntingToPounceDelay = 0.5f;

    Transform target;
    float timer = 0;
    Vector3 prevposition;
    Rigidbody2D rigid;
    bool groundedLeft = true;
    bool groundedRight = true;
    bool isGrounded = false;
    bool isStopped = false;
    float stopTimer = 0f;
    float ignoreEdgeTimer = 0f;
    float moveDir = -1f; // 현재 이동 방향 (배회 모드 기준, 절벽에서 반전시킬 때 사용)
    private SpriteRenderer spriteRenderer;
    private Collider2D col;
    bool isChasing = false;
    public GameObject chaseStartPrefab;
    public GameObject chaseEndPrefab;

    GameObject currentAlert;

    bool isStateDelay = false;
    public bool IsStateDelay => isStateDelay;
    float stateDelayTimer = 0f;
    bool pendingChaseState = false;

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

    [Header("풀숲 오브젝트 설정")]
    [Tooltip("잠복 상태일 때 몬스터 대신 보여줄 풀숲 오브젝트 프리팹 (BushHuntIndicator 컴포넌트 포함)")]
    public GameObject ambushBushPrefab;

    GameObject activeBushInstance; // 현재 씬에 소환되어 있는 풀숲 오브젝트
    BushHuntIndicator activeBushIndicator; // 위 오브젝트의 애니메이션 제어용 참조

    private Animator animator;
    private bool isAmbushed = true;
    private bool isPouncing = false; // 최초 덮치기 점프 실행 중 여부
    private H_MonsterAttack attackScript;

    public BushHuntIndicator bushHuntIndicator;

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

        // 덮치기 발동 범위(lineWidth/attackRange)를 읽어오기 위해 미리 참조 확보
        attackScript = GetComponent<H_MonsterAttack>();
        if (attackScript == null) attackScript = GetComponentInChildren<H_MonsterAttack>();

        // 게임 시작 시 무조건 잠복 상태로 리셋
        GoToSleep();
    }


    void Update()
    {
        if (target == null) return;

        float distance = Vector3.Distance(transform.position, target.position);
        if (isStateDelay)
        {
            if (animator != null)
                animator.SetBool("IsWalking", false);

            stateDelayTimer += Time.deltaTime;

            if (stateDelayTimer >= 0.5f)
            {
                isStateDelay = false;
                stateDelayTimer = 0f;

                if (currentAlert != null)
                {
                    Destroy(currentAlert);
                    currentAlert = null;
                }

                isChasing = pendingChaseState;

                if (!isChasing)
                    timer = 0f;
            }

            return;
        }

        // 1. 플레이어 거리 기반 잠복(Ambush) 해제 & 최초 덮치기 점프 공격 트리거
        // (H_MonsterAttack의 lineWidth/attackRange 사각형 범위를 그대로 사용 - J_EnemyAttack과 동일한 판정 방식)
        if (isAmbushed && !isPouncing)
        {
            bool inRange;
            if (attackScript != null)
            {
                float horizontalDist = Mathf.Abs(target.position.x - transform.position.x);
                float verticalDist = Mathf.Abs(target.position.y - transform.position.y);
                inRange = horizontalDist <= attackScript.lineWidth && verticalDist <= attackScript.attackRange;
            }
            else
            {
                // H_MonsterAttack을 못 찾았을 때를 대비한 안전장치용 폴백 (기존 원형 판정)
                inRange = distance <= detectionRange;
            }

            if (inRange)
            {
                isPouncing = true; // 재진입(코루틴 중복 실행) 방지를 위해 즉시 설정

                Vector2 detectedPos = target.position; // ★ 최초 감지 시점의 플레이어 좌표를 고정 캡처
                StartCoroutine(PounceSequenceRoutine(detectedPos));
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
            FaceTarget();

            isStateDelay = true;
            stateDelayTimer = 0f;
            pendingChaseState = true;

            ShowAlert(chaseStartPrefab);

            if (animator != null)
                animator.SetBool("IsWalking", false);

            return;
        }
        else if (isChasing && distance > chaseRange)
        {
            FaceTarget();

            isStateDelay = true;
            stateDelayTimer = 0f;
            pendingChaseState = false;

            ShowAlert(chaseEndPrefab);

            if (animator != null)
                animator.SetBool("IsWalking", false);

            if (isStopped)
                stopTimer = 0f;

            return;
        }

        if (isStopped)
        {
            if (animator != null)
                animator.SetBool("IsWalking", false);

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
                ignoreEdgeTimer = 0f; // 전환 직후 짧게 재감지 무시
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
            if (animator != null)
                animator.SetBool("IsWalking", false);

            prevposition = transform.position;
            return;
        }

        // 2. 이동하려는 방향 쪽에 땅이 없으면(절벽) 즉시 반전하지 않고 멈춤 상태로 전환
        bool edgeAhead = (desiredDir < 0f && !groundedLeft) || (desiredDir > 0f && !groundedRight);

        bool suppressCheck = !isChasing && ignoreEdgeTimer > 0f && desiredDir == moveDir;

        if (edgeAhead && !suppressCheck)
        {
            isStopped = true;
            stopTimer = 0f;
            return;
        }

        float moveSpeed = isChasing ? speed * 1.5f : speed;

        // 이동 방향 앞에 벽이 있는지 검사
        float rayDistance = 0.2f;

        RaycastHit2D wallHit = Physics2D.BoxCast(
            col.bounds.center,
            col.bounds.size * 0.9f,
            0f,
            Vector2.right * desiredDir,
            rayDistance,
            LayerMask.GetMask("Platform")
        );

        if (wallHit.collider != null)
        {
            if (isChasing &&
                isGrounded &&
                CanClimbWall(desiredDir))
            {
                Jump();
                return;
            }

            if (animator != null)
                animator.SetBool("IsWalking", false);

            // 배회 모드에서만 벽 충돌 시 0.5초 멈췄다가 반대 방향으로 전환
            // (추적 모드에서는 낭떠러지 처리와 마찬가지로 절벽/벽 회피를 강제로 걸지 않음 - 플레이어를 계속 쫓아가려는 의도 유지)
            if (!isChasing)
            {
                isStopped = true;
                stopTimer = 0f;
            }

            prevposition = transform.position;
            return;
        }

        if (animator != null)
            animator.SetBool("IsWalking", true);

        transform.Translate(moveSpeed * desiredDir * Time.deltaTime, 0f, 0f);
        moveDir = desiredDir;

        if (desiredDir != 0)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * -Mathf.Sign(desiredDir);
            transform.localScale = scale;
        }

        prevposition = transform.position;
    }
    private bool CanClimbWall(float dir)
    {
        Vector2 frontPos = (Vector2)transform.position +
                           Vector2.right * dir *
                           (col.bounds.extents.x + 0.1f);

        RaycastHit2D lowHit = Physics2D.Raycast(
            frontPos,
            Vector2.right * dir,
            0.2f,
            LayerMask.GetMask("Platform"));

        if (lowHit.collider == null)
            return false;

        Vector2 upperPos = frontPos + Vector2.up * climbableWallHeight;

        RaycastHit2D upperHit = Physics2D.Raycast(
            upperPos,
            Vector2.right * dir,
            0.2f,
            LayerMask.GetMask("Platform"));

        return upperHit.collider == null;
    }

    private void Jump()
    {
        if (!isGrounded)
            return;

        rigid.linearVelocity =
            new Vector2(rigid.linearVelocity.x, jumpForce);
    }
    void FixedUpdate()
    {
        // 잠복 중에는 고지/낙하 체크 패스
        if (isAmbushed) return;

        // 절벽 감지: 배회 모드에서는 기존처럼 짧은 거리(2)로 엄격하게 감지,
        // 추적 모드일 때만 safeDropDistance만큼 더 멀리 검사해서 낮은 턱/계단을 내려갈 수 있게 함
        float halfWidth = col.bounds.extents.x;
        float oneThird = halfWidth * 2f / 3f;

        Vector2 leftPoint = (Vector2)rigid.position + Vector2.left * oneThird;
        Vector2 rightPoint = (Vector2)rigid.position + Vector2.right * oneThird;

        float checkDistance = isChasing ? safeDropDistance : 2f;

        Debug.DrawRay(leftPoint, Vector2.down * checkDistance, Color.red);
        Debug.DrawRay(rightPoint, Vector2.down * checkDistance, Color.blue);

        RaycastHit2D leftHit = Physics2D.Raycast(leftPoint, Vector2.down, checkDistance, LayerMask.GetMask("Platform"));
        RaycastHit2D rightHit = Physics2D.Raycast(rightPoint, Vector2.down, checkDistance, LayerMask.GetMask("Platform"));

        groundedLeft = leftHit.collider != null;
        groundedRight = rightHit.collider != null;
        isGrounded = groundedLeft || groundedRight;
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

        // 3. 몬스터 본체를 다시 보여주고, 풀숲 오브젝트는 제거
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }
        HideBush();
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

        // 3. 몬스터 본체를 숨기고, 대신 풀숲 오브젝트를 보여줌
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }
        ShowBush();

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

    // 풀숲 오브젝트를 소환하거나(없을 경우) 다시 보이게 함
    private void ShowBush()
    {
        if (ambushBushPrefab == null) return;

        if (activeBushInstance == null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * ambushYOffset;
            activeBushInstance = Instantiate(ambushBushPrefab, spawnPos, Quaternion.identity, transform);
            activeBushIndicator = activeBushInstance.GetComponent<BushHuntIndicator>();
            if (activeBushIndicator == null) activeBushIndicator = activeBushInstance.GetComponentInChildren<BushHuntIndicator>();
        }

        // 원본 프리팹이 비활성 상태로 저장되어 있거나, 이전에 꺼진 채로 재사용되는 경우를 모두 대비해
        // Instantiate 직후든 재사용이든 상관없이 항상 명시적으로 켜줌
        activeBushInstance.SetActive(true);

        // 다시 잠복 상태로 돌아온 것이므로(예: CancelPounce), 사냥 긴장 애니메이션은 꺼둠
        if (activeBushIndicator != null)
            activeBushIndicator.SetHunting(false);
    }

    // 풀숲 오브젝트를 파괴/제거
    private void HideBush()
    {
        if (activeBushInstance != null)
        {
            Destroy(activeBushInstance);
            activeBushInstance = null;
            activeBushIndicator = null;
        }
    }

    /// <summary>
    /// 잠복 상태에 따른 스프라이트/본체 Y 오프셋을 처리합니다.
    /// (풀숲이 별도 오브젝트로 분리되었으므로, 여기서는 콜라이더 오프셋만 다룹니다)
    /// </summary>
    private void ApplyVisualOffset(bool apply)
    {
        if (!hasCachedOffsets) return;

        if (apply)
        {
            if (col != null)
            {
                col.offset = originalColliderOffset - new Vector2(0f, ambushYOffset);
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
    private void FaceTarget()
    {
        if (target == null)
            return;

        float dir = Mathf.Sign(target.position.x - transform.position.x);

        if (dir != 0)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * -dir;
            transform.localScale = scale;
        }
    }
    // NormalMonster.Purify()가 이 컴포넌트를 강제로 비활성화시킬 때 Unity가 자동 호출.
    // 그 시점에 Update() 루프(isStateDelay 처리)가 멈춰서 currentAlert가 정리되지 못하므로,
    // 여기서 확실하게 파괴함
    void OnDisable()
    {
        if (currentAlert != null)
        {
            Destroy(currentAlert);
            currentAlert = null;
        }
    }
    private void ShowAlert(GameObject prefab)
    {
        if (prefab == null)
            return;

        if (currentAlert != null)
            Destroy(currentAlert);

        currentAlert = Instantiate(
            prefab,
            transform.position + Vector3.up * 1.25f,
            Quaternion.identity
        );

        currentAlert.transform.SetParent(transform);
    }
    System.Collections.IEnumerator PounceSequenceRoutine(Vector2 detectedPos)
    {
        if (activeBushIndicator != null)
        {
            activeBushIndicator.SetInPlayer();
        }

        yield return new WaitForSeconds(inPlayerToHuntingDelay);

        if (activeBushIndicator != null)
        {
            activeBushIndicator.SetHunting(true);
        }

        yield return new WaitForSeconds(huntingToPounceDelay);

        if (attackScript != null)
        {
            attackScript.TriggerPounce(detectedPos); // ★ 감지 시점 좌표 전달
        }
        else
        {
            WakeUp();
        }
    }
}