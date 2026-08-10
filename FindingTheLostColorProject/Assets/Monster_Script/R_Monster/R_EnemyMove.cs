using UnityEngine;

[DefaultExecutionOrder(-100)]
public class R_EnemyMove : MonoBehaviour
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
    public GameObject currentAlertPrefab;
    bool isStateDelay = false;
    float stateDelayTimer = 0f;
    bool pendingChaseState = false;
    bool isChasing = false;
    public bool IsStateDelay => isStateDelay;
    public float attackStopDistance = 1.5f;

    [Tooltip("�� �Ÿ� �ȿ� ���� ���̶� ������ ���������� �������� �ʰ� �̵��� ����� (���/�� �������� ���, ���� ��忡���� ����)")]
    public float safeDropDistance = 3f;

    [Tooltip("��ȸ(����) ����� �� ������ �����ϴ� ����ĳ��Ʈ �Ÿ�")]
    public float wanderEdgeCheckDistance = 2f;
    [Header("���� ����")]
    public float jumpForce = 5f;
    public float climbableWallHeight = 1.2f;

    [Header("�� ���� ������ ����")]
    [Tooltip("isGrounded�� FixedUpdate������ ���ŵǱ� ������, Update() Ÿ�ְ̹� ��߳� �����δ� ���� ���ε��� " +
             "�� ������ ���� false�� ���� Jump()�� ���õǴ� ��찡 ����. ���������� �����ߴ� �������� " +
             "�� �ð�(��) ���̸� ������ ���� ���·� �����ؼ� ������ ������ �ʵ��� �� (�ڿ��� Ÿ��)")]
    public float groundedBufferDuration = 0.15f;
    float lastGroundedTime = -999f;


    Animator animator; // �ڽ� ������Ʈ�� �ִ� ��쵵 ����ؼ� GetComponentInChildren ���

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
    [Tooltip("���� ����� �� �ִϸ��̼� ��� �ӵ� ���� (�̵� �ӵ� ������ ���缭 1.5 ����)")]
    public float chaseAnimSpeedMultiplier = 1.5f;

    void Update()
    {
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

        // ���� ���� ���ο� ���� �ִϸ����� ��� �ӵ��� �ǽð����� ����
        if (animator != null)
        {
            animator.speed = isChasing ? chaseAnimSpeedMultiplier : 1f;
        }

        if (ignoreEdgeTimer > 0f)//���� ��ȯ ���� ��ȣ �ð�
            ignoreEdgeTimer -= Time.deltaTime;

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

        if (isStopped)//���� ������ ���� ����
        {
            if (animator != null) animator.SetBool("IsWalking", false);

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
            if (animator != null) animator.SetBool("IsWalking", false);
            prevposition = transform.position;
            return;
        }

        bool edgeAhead = (desiredDir < 0f && !groundedLeft) || (desiredDir > 0f && !groundedRight);
        bool suppressCheck = !isChasing && ignoreEdgeTimer > 0f && desiredDir == moveDir;

        if (edgeAhead && !suppressCheck)
        {
            if (animator != null) animator.SetBool("IsWalking", false);
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
            bool isSettledOnGround = isGrounded && Mathf.Abs(rigid.linearVelocity.y) < 0.05f;

            if (isChasing && isSettledOnGround && CanClimbWall(desiredDir))
            {
                Jump();
                return;
            }

            if (animator != null) animator.SetBool("IsWalking", false);

            // 배회 모드에서만 벽 충돌 시 0.5초 멈췄다가 반대 방향으로 전환
            if (!isChasing)
            {
                isStopped = true;
                stopTimer = 0f;
            }

            prevposition = transform.position;
            return;
        }

        if (animator != null) animator.SetBool("IsWalking", true);

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

        Debug.Log($"[R_JUMP] frame={Time.frameCount} pos={transform.position} isChasing={isChasing} velY_before={rigid.linearVelocity.y}");

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

        if (isGrounded)
            lastGroundedTime = Time.time;
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