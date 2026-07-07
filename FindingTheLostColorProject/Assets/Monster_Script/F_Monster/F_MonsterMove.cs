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
    bool isStopped = false;
    float stopTimer = 0f;
    float ignoreEdgeTimer = 0f;
    float moveDir = -1f;
    public float chaseRange;
    bool isChasing = false;
    public float attackStopDistance = 1.5f;

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

        if (ignoreEdgeTimer > 0f) // 방향 전환 직후 보호 시간 (재감지로 인한 재진동 방지)
            ignoreEdgeTimer -= Time.deltaTime;

        // 추적 시작/종료 판정은 isStopped 상태와 무관하게 항상 먼저 체크
        // (이걸 isStopped 블록 뒤에 두면, 절벽에서 멈춰있는 동안 추적 종료 조건이 아예 검사되지 않아
        //  플레이어가 멀어져도 isChasing이 계속 true로 남는 문제가 있었음)
        if (!isChasing && distance <= range)//추적 시작
        {
            isChasing = true;
        }
        else if (isChasing && distance > chaseRange)//추적 종료 (더 넓은 범위를 벗어나야 그만둠)
        {
            isChasing = false;
            timer = 0f; // 배회 모드로 깨끗하게 복귀하도록 타이머 리셋 (추적 중 쌓인 timer 값 무시)

            // 절벽에서 대기 중이었다면, 배회 모드의 "멈춤->반전" 흐름으로 자연스럽게 이어지도록
            // 대기 타이머를 리셋해서 0.5초 후 정상적으로 반전되게 함
            if (isStopped) stopTimer = 0f;
        }

        if (isStopped) // 절벽 끝에서 멈춘 상태
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

        // 1. 원래 하고 싶은 이동 방향
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
        transform.Translate(moveSpeed * desiredDir * Time.deltaTime, 0f, 0f);
        moveDir = desiredDir;

        float velocityX = transform.position.x - prevposition.x;
        if (velocityX != 0)//이미지 반전
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * -Mathf.Sign(velocityX);
            transform.localScale = scale;
        }
        prevposition = transform.position;
    }
    void FixedUpdate()
    {
        //절벽 감지
        float halfWidth = col.bounds.extents.x;
        float oneThird = halfWidth * 2f / 3f;
        Vector2 leftPoint = (Vector2)rigid.position + Vector2.left * oneThird;
        Vector2 rightPoint = (Vector2)rigid.position + Vector2.right * oneThird;
        Debug.DrawRay(leftPoint, Vector2.down * 2, Color.red);
        Debug.DrawRay(rightPoint, Vector2.down * 2, Color.blue);
        RaycastHit2D leftHit = Physics2D.Raycast(leftPoint, Vector2.down, 2, LayerMask.GetMask("Platform"));
        RaycastHit2D rightHit = Physics2D.Raycast(rightPoint, Vector2.down, 2, LayerMask.GetMask("Platform"));

        groundedLeft = leftHit.collider != null;
        groundedRight = rightHit.collider != null;
    }
}