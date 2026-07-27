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
    [Tooltip("절벽에서 멈췄다가 반대 방향으로 전환한 직후, 그 방향으로의 절벽 재감지를 무시하는 시간 (초). 방향 반전 직후 제자리에서 진동하는 것을 방지")]
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
    [Header("점프 설정")]
    public float jumpForce = 5f;
    public float climbableWallHeight = 1.2f;
    Animator animator;

    void Start()
    {
        prevposition = transform.position;
        rigid = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

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

        if (ignoreEdgeTimer > 0f) // 방향 전환 직후 보호 시간 (재감지로 인한 재진동 방지)
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

        if (isStopped) // 절벽 끝에서 멈춘 상태
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
                ignoreEdgeTimer = edgeIgnoreDuration;
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
            if (animator != null)
            {
                animator.SetBool("IsWalking", false);
            }
            UpdateAnimatorSpeed(false);

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
            // 배회 모드에서만 벽 충돌 시 0.5초 멈췄다가 반대 방향으로 전환
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
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * -Mathf.Sign(desiredDir);
            transform.localScale = scale;
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
}