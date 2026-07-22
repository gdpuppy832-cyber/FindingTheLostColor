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
    [Header("점프 설정")]
    public float jumpForce = 5f;
    public float climbableWallHeight = 1.2f;

    void Start()
    {
        prevposition = transform.position;
        rigid = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            target = player.transform;
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