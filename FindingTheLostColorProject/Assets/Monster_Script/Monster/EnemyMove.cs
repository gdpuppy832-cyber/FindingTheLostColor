using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using UnityEngine.UI;

public class EnemyMove : MonoBehaviour
{
    public float speed = 1.5f;
    Transform target;
    public float range;
    float timer = 0;
    Vector3 prevposition;
    Rigidbody2D rigid;
    Collider2D col;
    bool groundedLeft = true;
    bool groundedRight = true;
    bool isGrounded = false;
    bool isStopped = false;
    float stopTimer = 0f;
    float ignoreEdgeTimer = 0f;
    float moveDir = -1f;
    public float chaseRange;
    public GameObject chaseStartPrefab;
    public GameObject chaseEndPrefab;

    GameObject currentAlert;

    bool isStateDelay = false;
    float stateDelayTimer = 0f;
    bool pendingChaseState = false;
    bool isChasing = false;
    public bool IsStateDelay => isStateDelay;
    public bool IsGrounded => isGrounded; // 공중에 떠 있는 동안 EnemyAttack이 공격을 트리거하지 못하도록 외부에 공개
    public float attackStopDistance = 1.5f;

    [Tooltip("이 거리 안에 낮은 땅이라도 있으면 낭떠러지로 판정하지 않고 이동을 허용함 (계단/턱 내려가기 허용, 추적 모드에서만 적용)")]
    public float safeDropDistance = 3f;

    [Tooltip("배회(순찰) 모드일 때 절벽을 감지하는 레이캐스트 거리")]
    public float wanderEdgeCheckDistance = 2f;

    [Tooltip("실제로 발이 바닥에 붙어있는지(진짜 접지) 판정하는 전용 레이캐스트 거리. " +
             "isGrounded(절벽 감지용)와 달리 값을 아주 짧게 잡아서, 점프로 공중에 떠 있는 동안에는 " +
             "false가 되도록 함 (예: EnemyAttack이 공중에서 공격을 트리거하지 않게 막는 용도)")]
    public float trueGroundCheckDistance = 0.12f;
    bool isTrueGrounded = false;
    public bool IsTrueGrounded => isTrueGrounded;

    [Header("점프 설정")]
    public float jumpForce = 5f;
    public float climbableWallHeight = 1.2f;

    [Header("벽 점프 안정성 설정")]
    [Tooltip("isGrounded는 FixedUpdate에서만 갱신되기 때문에, Update() 타이밍과 어긋나 실제로는 접지 중인데도 " +
             "한 프레임 동안 false로 읽혀 Jump()가 무시되는 경우가 있음. 마지막으로 접지했던 시점부터 " +
             "이 시간(초) 안이면 여전히 접지 상태로 간주해서 점프가 씹히지 않도록 함 (코요테 타임)")]
    public float groundedBufferDuration = 0.15f;
    float lastGroundedTime = -999f; // FixedUpdate에서 isGrounded가 true였던 마지막 시각

    [Tooltip("추적 중 벽(1칸 벽 등)에 막혔을 때, 점프 조건(isGrounded/CanClimbWall)이 한 프레임 실패하더라도 " +
             "즉시 포기하지 않고 이 시간(초) 동안은 매 프레임 계속 점프를 재시도함")]
    public float wallJumpRetryDuration = 0.3f;
    float wallBlockStartTime = -1f; // 현재 방향의 벽에 처음 막힌 시각 (-1이면 막힌 적 없음/이미 벗어남)

    Animator animator; // 자식 오브젝트에 있는 Animator (스프라이트가 자식으로 분리된 구조 대비 GetComponentInChildren 사용)

    [Header("HP Bar (좌우 반전 방지)")]
    [Tooltip("몬스터의 자식으로 존재하는 World Space Canvas HP바. 부모의 localScale.x 반전과 무관하게 항상 정방향으로 유지됨")]
    public Transform hpBar;

    Vector3 initialScale;           // 몬스터 자신의 초기 localScale (방향 기준값)
    Vector3 hpBarInitialLocalScale; // HP바의 초기 localScale (보정 기준값)

    void Start()
    {
        prevposition = transform.position;
        rigid = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        initialScale = transform.localScale;
        if (hpBar != null) hpBarInitialLocalScale = hpBar.localScale;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
            target = player.transform;
    }

    void Update()
    {
        float distance = Vector3.Distance(transform.position, target.position);
        if (isStateDelay)
        {
            if (animator != null)
            {
                animator.SetBool("IsWalking", false);
                animator.speed = 1f;
            }

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

        if (ignoreEdgeTimer > 0f) // ���� ��ȯ ���� ��ȣ �ð� (�簨���� ���� ������ ����)
            ignoreEdgeTimer -= Time.deltaTime;

        // ���� ����/���� ������ isStopped ���¿� �����ϰ� �׻� ���� üũ
        // (�̰� isStopped ��� �ڿ� �θ�, �������� �����ִ� ���� ���� ���� ������ �ƿ� �˻���� �ʾ�
        //  �÷��̾ �־����� isChasing�� ��� true�� ���� ������ �־���)
        if (!isChasing && distance <= range)
        {
            isStateDelay = true;
            stateDelayTimer = 0f;
            pendingChaseState = true;

            ShowAlert(chaseStartPrefab);
            float lookDir = Mathf.Sign(target.position.x - transform.position.x);

            if (lookDir != 0)
            {
                ApplyFacing(-Mathf.Sign(lookDir));
            }


            return;
        }
        else if (isChasing && distance > chaseRange)
        {
            isStateDelay = true;
            stateDelayTimer = 0f;
            pendingChaseState = false;

            ShowAlert(chaseEndPrefab);

            if (isStopped)
                stopTimer = 0f;

            return;
        }

        if (isStopped) // 절벽 끝에서 멈춘 상태
        {
            if (animator != null)
            {
                animator.SetBool("IsWalking", false);
                animator.speed = 1f;
            }

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
                // xDiff가 attackStopDistance 이내면 어차피 desiredDir이 0이라 이동 안 하므로 그대로 대기
                return; // 절벽 방향을 계속 원할 때만 대기 유지
            }

            stopTimer += Time.deltaTime;
            if (stopTimer >= 0.5f)
            {
                isStopped = false;
                stopTimer = 0f;
                moveDir = -moveDir; // 반대 방향으로 전환 (배회 모드 전용)
                ignoreEdgeTimer = 0.3f; // 전환 직후 짧게 재감지 무시
                timer = moveDir < 0f ? 0f : 3.5f; // 배회 타이머도 반전된 방향에 맞게 재설정
            }
            return; // 멈춰있는 동안은 이동/반전 로직 스킵
        


        }

        timer += Time.deltaTime;

        // 1. ���� �ϰ� ���� �̵� ����
        float desiredDir = 0f;
        if (isChasing)//���� ���
        {
            float xDiff = target.position.x - transform.position.x;
            if (Mathf.Abs(xDiff) > attackStopDistance)
                desiredDir = Mathf.Sign(xDiff);
        }
        else if (timer < 3f)//��ȸ����
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
            {
                animator.SetBool("IsWalking", false);
                animator.speed = 1f;
            }
            prevposition = transform.position;
            return;
        }

        // 2. �̵��Ϸ��� ���� �ʿ� ���� ������(����) ��� �������� �ʰ� ���� ���·� ��ȯ
        bool edgeAhead = (desiredDir < 0f && !groundedLeft) || (desiredDir > 0f && !groundedRight);
        bool suppressCheck = !isChasing && ignoreEdgeTimer > 0f && desiredDir == moveDir;

        if (edgeAhead && !suppressCheck)
        {
            isStopped = true;
            stopTimer = 0f;
            return;
        }

        float moveSpeed = isChasing ? speed * 1.5f : speed;

        // �̵� ���� �տ� ���� �ִ��� �˻�
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
            if (isChasing && IsSettledOnGround())
            {
                if (wallBlockStartTime < 0f)
                    wallBlockStartTime = Time.time;

                if (CanClimbWall(desiredDir))
                {
                    Jump();
                    // 점프 시도가 성공적으로 들어갔으므로 이번 벽에 대한 재시도 상태 초기화
                    wallBlockStartTime = -1f;
                    return;
                }

                if (Time.time - wallBlockStartTime < wallJumpRetryDuration)
                {
                    if (animator != null)
                    {
                        animator.SetBool("IsWalking", false);
                        animator.speed = 1f;
                    }
                    prevposition = transform.position;
                    return;
                }
                // 재시도 시간을 넘겼다면 아래의 기존 처리(정지 유지)로 자연스럽게 넘어감
            }

            if (animator != null)
            {
                animator.SetBool("IsWalking", false);
                animator.speed = 1f;
            }

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
        else
        {
            wallBlockStartTime = -1f;
        }

        if (animator != null)
        {
            animator.SetBool("IsWalking", true);
            // 추적 중엔 이동 속도가 1.5배 빨라지므로, 애니메이션 재생 속도도 같은 비율로 빠르게
            animator.speed = isChasing ? 1.5f : 1f;
        }

        transform.Translate(moveSpeed * desiredDir * Time.deltaTime, 0f, 0f);
        moveDir = desiredDir;

        if (desiredDir != 0)
        {
            ApplyFacing(-Mathf.Sign(desiredDir));
        }

        prevposition = transform.position;
    }

    // 몬스터의 localScale.x를 뒤집는 모든 지점에서 이 함수를 통해서만 처리.
    // 몬스터가 뒤집히는 그 순간, 자식인 HP바의 localScale.x도 함께 반대로 보정해서
    // 부모의 반전을 상쇄시킴 (매 프레임 보정이 아니라, 반전이 실제로 발생하는 시점에만 1회 처리)
    private void ApplyFacing(float sign)
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(initialScale.x) * sign;
        transform.localScale = scale;

        if (hpBar != null)
        {
            // 부모(this)의 X축 부호가 초기값 대비 뒤집혔는지에 따라 HP바 로컬 스케일의 부호를 반대로 걸어줌.
            // 결과적으로 부모(월드) 스케일 * 자식(로컬) 스케일이 항상 초기 부호(정방향)로 유지됨.
            Vector3 hpScale = hpBarInitialLocalScale;
            hpScale.x = hpBarInitialLocalScale.x * Mathf.Sign(scale.x) * Mathf.Sign(initialScale.x);
            hpBar.localScale = hpScale;
        }
    }

    void FixedUpdate()
    {
        float halfWidth = col.bounds.extents.x;
        float oneThird = halfWidth * 2f / 3f;
        Vector2 leftPoint = (Vector2)rigid.position + Vector2.left * oneThird;
        Vector2 rightPoint = (Vector2)rigid.position + Vector2.right * oneThird;

        float checkDistance = isChasing ? safeDropDistance : wanderEdgeCheckDistance;

        Debug.DrawRay(leftPoint, Vector2.down * checkDistance, Color.red);
        Debug.DrawRay(rightPoint, Vector2.down * checkDistance, Color.blue);
        RaycastHit2D leftHit = Physics2D.Raycast(leftPoint, Vector2.down, checkDistance, LayerMask.GetMask("Platform"));
        RaycastHit2D rightHit = Physics2D.Raycast(rightPoint, Vector2.down, checkDistance, LayerMask.GetMask("Platform"));

        groundedLeft = leftHit.collider != null;
        groundedRight = rightHit.collider != null;
        isGrounded = groundedLeft || groundedRight;

        if (isGrounded)
            lastGroundedTime = Time.time;

        Vector2 centerPoint = (Vector2)rigid.position;
        RaycastHit2D trueGroundHit = Physics2D.Raycast(centerPoint, Vector2.down, col.bounds.extents.y + trueGroundCheckDistance, LayerMask.GetMask("Platform"));
        isTrueGrounded = trueGroundHit.collider != null;
    }

    private bool IsSettledOnGround()
    {
        return isTrueGrounded && Mathf.Abs(rigid.linearVelocity.y) < 0.05f;
    }
    private bool CanClimbWall(float dir)
    {
        Vector2 feetPos = new Vector2(transform.position.x, col.bounds.min.y + 0.05f) +
                           Vector2.right * dir *
                           (col.bounds.extents.x + 0.1f);

        RaycastHit2D lowHit = Physics2D.Raycast(
            feetPos,
            Vector2.right * dir,
            0.2f,
            LayerMask.GetMask("Platform"));

        if (lowHit.collider == null)
            return false;

        // 발밑 기준으로 climbableWallHeight만큼 위에서도 벽이 계속 이어지는지 검사.
        // 그 높이에서 벽이 없다면(=낮은 벽이라면) 오를 수 있다고 판정
        Vector2 upperPos = feetPos + Vector2.up * climbableWallHeight;

        RaycastHit2D upperHit = Physics2D.Raycast(
            upperPos,
            Vector2.right * dir,
            0.2f,
            LayerMask.GetMask("Platform"));

        return upperHit.collider == null;
    }
    private void Jump()
    {
        bool groundedRecently = isGrounded || (Time.time - lastGroundedTime <= groundedBufferDuration);
        if (!groundedRecently)
            return;

        rigid.linearVelocity =
            new Vector2(rigid.linearVelocity.x, jumpForce);
    }

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
            transform.position + Vector3.up * 2f,
            Quaternion.identity
        );

        currentAlert.transform.SetParent(transform);
    }
}
