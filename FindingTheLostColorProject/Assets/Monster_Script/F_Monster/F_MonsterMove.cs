using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public class F_EnemyMove : MonoBehaviour
{
    public float speed = 3f;
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
    [Tooltip("�������� ����ٰ� �ݴ� �������� ��ȯ�� ����, �� ���������� ���� �簨���� �����ϴ� �ð� (��). ���� ���� ���� ���ڸ����� �����ϴ� ���� ����")]
    public float edgeIgnoreDuration = 0.3f;
    public float chaseRange;
    public GameObject chaseStartPrefab;
    public GameObject chaseEndPrefab;

    GameObject currentAlert;

    bool isStateDelay = false;
    float stateDelayTimer;
    bool pendingChaseState = false;
    bool isChasing = false;
    public bool IsStateDelay => isStateDelay;
    public float attackStopDistance = 1.5f;

    [Tooltip("이 거리 안에 낮은 땅이라도 있으면 낭떠러지로 판정하지 않고 이동을 허용함 (계단/턱 내려가기 허용, 추적 모드에서만 적용)")]
    public float safeDropDistance = 3f;

    [Tooltip("배회(순찰) 모드일 때 절벽을 감지하는 레이캐스트 거리")]
    public float wanderEdgeCheckDistance = 2f;

    [Header("빠른 이동 절벽 선행 감지 설정")]
    [Tooltip("이동 방향 앞쪽으로 절벽을 미리 검사할 기본 거리. 값이 클수록 더 멀리 앞을 내다보고 미리 멈춤")]
    public float edgeLookAheadDistance = 0.8f;
    [Tooltip("앞쪽 검사 지점에서 아래로 땅이 있는지 확인하는 Raycast 거리")]
    public float edgeGroundCheckDistance = 3f;
    [Tooltip("앞쪽 절벽 검사에 사용할 지점 개수 (많을수록 촘촘하지만 비용 증가)")]
    public int edgeCheckPointCount = 3;

    // 이동 방향 앞쪽 다중 지점 검사 결과 (groundedLeft/Right와 별개로, edgeAhead 판정 전용으로만 사용)
    bool edgeSafeLeft = true;
    bool edgeSafeRight = true;

    [Tooltip("������ ���� �ٴڿ� �پ��ִ���(��¥ ����) �����ϴ� ���� ����ĳ��Ʈ �Ÿ�. " +
             "isGrounded(���� ������)�� �޸� ���� ���� ª�� ��Ƽ�, ������ ���߿� �� �ִ� ���ȿ��� " +
             "false�� �ǵ��� �� (��: B_EnemyAttack�� ���߿��� ������ Ʈ�������� �ʰ� ���� �뵵)")]
    public float trueGroundCheckDistance = 0.12f;
    bool isTrueGrounded = false;
    public bool IsTrueGrounded => isTrueGrounded;

    [Header("���� ����")]
    public float jumpForce = 5f;
    public float climbableWallHeight = 1.2f;
    Animator animator;

    [Header("HP Bar (�¿� ���� ����)")]
    [Tooltip("������ �ڽ����� �����ϴ� World Space Canvas HP��. �θ��� localScale.x ������ �����ϰ� �׻� ���������� ������")]
    public Transform hpBar;

    Vector3 initialScale;           // ���� �ڽ��� �ʱ� localScale (���� ���ذ�)
    Vector3 hpBarInitialLocalScale; // HP���� �ʱ� localScale (���� ���ذ�)

    void Start()
    {
        prevposition = transform.position;
        rigid = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            target = player.transform;

        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        initialScale = transform.localScale;
        if (hpBar != null) hpBarInitialLocalScale = hpBar.localScale;
    }

    void Update()
    {
        float distance = Vector3.Distance(transform.position, target.position);
        if (isStateDelay)
        {
            if (animator != null)
            {
                animator.SetBool("IsWalking", false);
            }
            UpdateAnimatorSpeed(false);

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

        if (!isChasing && distance <= range)
        {
            FaceTarget();

            isStateDelay = true;
            stateDelayTimer = 0f;
            pendingChaseState = true;

            ShowAlert(chaseStartPrefab);

            return;
        }
        else if (isChasing && distance > chaseRange)
        {
            FaceTarget();

            isStateDelay = true;
            stateDelayTimer = 0f;
            pendingChaseState = false;

            ShowAlert(chaseEndPrefab);

            if (isStopped)
                stopTimer = 0f;

            return;
        }

        if (isStopped) // ���� ������ ���� ����
        {
            if (animator != null)
            {
                animator.SetBool("IsWalking", false);
            }
            UpdateAnimatorSpeed(false);
            if (isChasing)
            {
                float xDiff = target.position.x - transform.position.x;
                if (Mathf.Abs(xDiff) > attackStopDistance)
                {
                    float wantDir = Mathf.Sign(xDiff);
                    // 정지 상태 해제 여부도 동일하게 앞쪽 다중 지점 검사 결과를 기준으로 판단
                    bool wantDirIsEdge = (wantDir < 0f && !edgeSafeLeft) || (wantDir > 0f && !edgeSafeRight);
                    if (!wantDirIsEdge)
                    {
                        isStopped = false;
                        stopTimer = 0f;
                    }
                }
                return;
            }

            stopTimer += Time.deltaTime;
            if (stopTimer >= 0.5f)
            {
                isStopped = false;
                stopTimer = 0f;
                moveDir = -moveDir;
                ignoreEdgeTimer = edgeIgnoreDuration;
                timer = moveDir < 0f ? 0f : 3.5f;
            }
            return;
        }

        timer += Time.deltaTime;

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
            }
            UpdateAnimatorSpeed(false);

            prevposition = transform.position;
            return;
        }

        bool edgeAhead = (desiredDir < 0f && !edgeSafeLeft) || (desiredDir > 0f && !edgeSafeRight);
        bool suppressCheck = !isChasing && ignoreEdgeTimer > 0f && desiredDir == moveDir;

        if (edgeAhead && !suppressCheck)
        {
            isStopped = true;
            stopTimer = 0f;
            return;
        }

        float moveSpeed = isChasing ? speed * 1.5f : speed;

        float rayDistance = 0.2f;

        RaycastHit2D wallHit = BoxCastIgnoreTrampoline(
      col.bounds.center,
      col.bounds.size * 0.9f,
      0f,
      Vector2.right * desiredDir,
      rayDistance,
      LayerMask.GetMask("Platform")
  );

        if (wallHit.collider != null)
        {
            if (animator != null)
            {
                animator.SetBool("IsWalking", false);
            }
            UpdateAnimatorSpeed(false);

            if (isChasing && isGrounded && CanClimbWall(desiredDir))
            {
                Jump();
                return;
            }
            // ��ȸ ��忡���� �� �浹 �� 0.5�� ����ٰ� �ݴ� �������� ��ȯ
            if (!isChasing)
            {
                isStopped = true;
                stopTimer = 0f;
            }

            prevposition = transform.position;
            return;
        }
        if (animator != null)
        {
            animator.SetBool("IsWalking", true);
        }
        UpdateAnimatorSpeed(isChasing);

        transform.Translate(moveSpeed * desiredDir * Time.deltaTime, 0f, 0f);
        moveDir = desiredDir;

        if (desiredDir != 0)
        {
            ApplyFacing(-Mathf.Sign(desiredDir));
        }

        prevposition = transform.position;
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

        // �߹� �������� climbableWallHeight��ŭ �������� ���� ��� �̾������� �˻�.
        // �� ���̿��� ���� ���ٸ�(=���� ���̶��) ���� �� �ִٰ� ����
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
        if (!isGrounded)
            return;

        rigid.linearVelocity =
            new Vector2(rigid.linearVelocity.x, jumpForce);
    }

    void UpdateAnimatorSpeed(bool wantFast)
    {
        if (animator == null) return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (wantFast && state.IsTag("Walk"))
        {
            animator.speed = 1.5f;
        }
        else
        {
            animator.speed = 1f;
        }
    }
    RaycastHit2D RaycastIgnoreTrampoline(Vector2 origin, Vector2 direction, float distance, int layerMask)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, distance, layerMask);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in hits)
        {
            if (h.collider != null && h.collider.GetComponent<Trampoline>() == null)
                return h;
        }
        return new RaycastHit2D();
    }

    RaycastHit2D BoxCastIgnoreTrampoline(Vector2 origin, Vector2 size, float angle, Vector2 direction, float distance, int layerMask)
    {
        RaycastHit2D[] hits = Physics2D.BoxCastAll(origin, size, angle, direction, distance, layerMask);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in hits)
        {
            if (h.collider != null && h.collider.GetComponent<Trampoline>() == null)
                return h;
        }
        return new RaycastHit2D();
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
        RaycastHit2D leftHit = RaycastIgnoreTrampoline(leftPoint, Vector2.down, checkDistance, LayerMask.GetMask("Platform"));
        RaycastHit2D rightHit = RaycastIgnoreTrampoline(rightPoint, Vector2.down, checkDistance, LayerMask.GetMask("Platform"));

        groundedLeft = leftHit.collider != null;
        groundedRight = rightHit.collider != null;
        isGrounded = groundedLeft || groundedRight;

        // ★ 절벽 감지 전용: 이동 방향 앞쪽에 여러 지점을 두고 각각 아래로 검사.
        //   groundedLeft/Right(제자리 좌우 검사)와는 별개이며, 벽 감지(BoxCast)와도 역할을 분리함.
        //   "앞쪽에 바닥이 있는가?"만 확인하는 용도.
        edgeSafeLeft = CheckEdgeAheadSafe(-1f, oneThird);
        edgeSafeRight = CheckEdgeAheadSafe(1f, oneThird);

        Vector2 feetCenter = new Vector2(col.bounds.center.x, col.bounds.min.y + 0.02f);
        RaycastHit2D trueGroundHit = RaycastIgnoreTrampoline(feetCenter, Vector2.down, trueGroundCheckDistance, LayerMask.GetMask("Platform"));
        isTrueGrounded = trueGroundHit.collider != null;
    }

    // dir 방향(왼쪽 -1 / 오른쪽 +1)으로 edgeCheckPointCount개의 지점을 두고,
    // 각 지점에서 아래로 edgeGroundCheckDistance만큼 땅이 있는지 확인.
    // 한 지점이라도 바닥이 없으면 그 방향은 "절벽 위험"으로 판단해 false 반환.
    bool CheckEdgeAheadSafe(float dir, float edgeOffsetX)
    {
        // 현재 실제 이동 속도(추격 시 1.5배)를 반영해, 한 FixedUpdate 동안 실제로 이동할
        // 거리보다 충분히 더 앞까지 검사 범위를 확장함 (빠른 몬스터가 절벽을 뛰어넘는 것을 방지)
        float currentMoveSpeed = isChasing ? speed * 1.5f : speed;
        float perFrameMoveDistance = currentMoveSpeed * Time.fixedDeltaTime;
        float dynamicLookAhead = Mathf.Max(edgeLookAheadDistance, perFrameMoveDistance * 2f);

        Vector2 basePos = (Vector2)rigid.position + Vector2.right * dir * edgeOffsetX;
        int pointCount = Mathf.Max(1, edgeCheckPointCount);

        for (int i = 1; i <= pointCount; i++)
        {
            float t = (float)i / pointCount;
            float aheadX = dynamicLookAhead * t;
            Vector2 checkPoint = basePos + Vector2.right * dir * aheadX;

            Debug.DrawRay(checkPoint, Vector2.down * edgeGroundCheckDistance, Color.yellow);

            RaycastHit2D hit = RaycastIgnoreTrampoline(checkPoint, Vector2.down, edgeGroundCheckDistance, LayerMask.GetMask("Platform"));
            if (hit.collider == null)
            {
                return false; // 이 지점 아래에 바닥이 없음 -> 절벽 위험
            }
        }

        return true;
    }
    private void FaceTarget()
    {
        if (target == null)
            return;

        float dir = Mathf.Sign(target.position.x - transform.position.x);

        if (dir != 0)
        {
            ApplyFacing(-dir);
        }
    }

    // ������ localScale.x�� ������ ��� �������� �� �Լ��� ���ؼ��� ó��.
    // ���Ͱ� �������� �� ����, �ڽ��� HP���� localScale.x�� �Բ� �ݴ�� �����ؼ�
    // �θ��� ������ ����Ŵ (�� ������ ������ �ƴ϶�, ������ ������ �߻��ϴ� �������� 1ȸ ó��)
    private void ApplyFacing(float sign)
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(initialScale.x) * sign;
        transform.localScale = scale;

        if (hpBar != null)
        {
            // �θ�(this)�� X�� ��ȣ�� �ʱⰪ ��� ������������ ���� HP�� ���� �������� ��ȣ�� �ݴ�� �ɾ���.
            // ��������� �θ�(����) ������ * �ڽ�(����) �������� �׻� �ʱ� ��ȣ(������)�� ������.
            Vector3 hpScale = hpBarInitialLocalScale;
            hpScale.x = hpBarInitialLocalScale.x * Mathf.Sign(scale.x) * Mathf.Sign(initialScale.x);
            hpBar.localScale = hpScale;
        }
    }
    // NormalMonster.Purify()�� �� ������Ʈ�� ������ ��Ȱ��ȭ��ų �� Unity�� �ڵ� ȣ��.
    // �� ������ Update() ����(isStateDelay ó��)�� ���缭 currentAlert�� �������� ���ϹǷ�,
    // ���⼭ Ȯ���ϰ� �ı���
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