using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
public class B_EnemyMove : MonoBehaviour
{
    public float speed = 0.75f;
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
    public float attackStopDistance = 1.5f;

    [Tooltip("이 거리 안에 낮은 땅이라도 있으면 낭떠러지로 판정하지 않고 이동을 허용함 (계단/턱 내려가기 허용, 추적 모드에서만 적용)")]
    public float safeDropDistance = 3f;

    [Tooltip("배회(순찰) 모드일 때 절벽을 감지하는 레이캐스트 거리")]
    public float wanderEdgeCheckDistance = 2f;

    [Tooltip("실제로 발이 바닥에 붙어있는지(진짜 접지) 판정하는 전용 레이캐스트 거리. " +
             "isGrounded(절벽 감지용)와 달리 값을 아주 짧게 잡아서, 점프로 공중에 떠 있는 동안에는 " +
             "false가 되도록 함 (예: B_EnemyAttack이 공중에서 공격을 트리거하지 않게 막는 용도)")]
    public float trueGroundCheckDistance = 0.12f;
    bool isTrueGrounded = false;
    public bool IsTrueGrounded => isTrueGrounded;

    [Header("점프 설정")]
    public float jumpForce = 5f;
    public float climbableWallHeight = 1.2f;

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
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            target = player.transform;



        initialScale = transform.localScale;
        if (hpBar != null) hpBarInitialLocalScale = hpBar.localScale;
    }
    void Update()
    {
        float distance = Vector3.Distance(transform.position, target.position);
        if (isStateDelay)
        {
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
        if (ignoreEdgeTimer > 0f)//방향 전환 직후 보호 시간
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

        if (isStopped)//절벽 끝에서 멈춘 상태
        {
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
                return;
            }

            stopTimer += Time.deltaTime;
            if (stopTimer >= 0.5f)
            {
                isStopped = false;
                stopTimer = 0f;
                moveDir = -moveDir;
                ignoreEdgeTimer = 0.3f;
                timer = moveDir < 0f ? 0f : 3.5f;
            }
            return;
        }

        timer += Time.deltaTime;

        float desiredDir = 0f;
        if (isChasing)//추적 모드
        {
            float xDiff = target.position.x - transform.position.x;
            if (Mathf.Abs(xDiff) > attackStopDistance)
                desiredDir = Mathf.Sign(xDiff);
        }
        else if (timer < 3f)//배회상태
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

        bool edgeAhead = (desiredDir < 0f && !groundedLeft) || (desiredDir > 0f && !groundedRight);
        bool suppressCheck = !isChasing && ignoreEdgeTimer > 0f && desiredDir == moveDir;

        if (edgeAhead && !suppressCheck)
        {
            isStopped = true;
            stopTimer = 0f;
            return;
        }

        float moveSpeed = isChasing ? speed * 1.5f : speed;

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
            if (isChasing && isGrounded && CanClimbWall(desiredDir))
            {
                Jump();
                return;
            }
            // 배회 모드에서만 벽 충돌 시 0.5초 멈췄다가 반대 방향으로 전환
            if (!isChasing)
            {
                isStopped = true;
                stopTimer = 0f;
            }

            prevposition = transform.position;
            return;
        }

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
        // 기존에는 frontPos가 몸통 중앙(transform.position) 높이에서 시작해서,
        // 몸통 중앙보다 낮은 벽은 lowHit이 아예 아무것도 맞히지 못해 항상 false(오르기 불가)로
        // 판정되는 문제가 있었음. 감지 기준점을 발밑(콜라이더 하단)으로 낮춰서
        // 어떤 높이의 벽이든 lowHit이 정상적으로 감지되도록 함
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
        if (!isGrounded)
            return;

        rigid.linearVelocity =
            new Vector2(rigid.linearVelocity.x, jumpForce);
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

        Vector2 centerPoint = (Vector2)rigid.position;
        RaycastHit2D trueGroundHit = Physics2D.Raycast(centerPoint, Vector2.down, col.bounds.extents.y + trueGroundCheckDistance, LayerMask.GetMask("Platform"));
        isTrueGrounded = trueGroundHit.collider != null;
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
            transform.position + Vector3.up * 2f,
            Quaternion.identity
        );

        currentAlert.transform.SetParent(transform);
    }
}