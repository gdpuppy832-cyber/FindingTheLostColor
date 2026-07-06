using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

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

        if (isStopped) // 절벽 끝에서 멈춘 상태
        {
            stopTimer += Time.deltaTime;
            if (stopTimer >= 0.5f)
            {
                isStopped = false;
                stopTimer = 0f;
                moveDir = -moveDir; // 반대 방향으로 전환
                ignoreEdgeTimer = 0.3f; // 전환 직후 짧게 재감지 무시

                if (!isChasing)
                {
                    // 배회 타이머도 반전된 방향에 맞게 재설정
                    timer = moveDir < 0f ? 0f : 3.5f;
                }
            }
            return; // 멈춰있는 동안은 이동/반전 로직 스킵
        }

        timer += Time.deltaTime;
        if (!isChasing && distance <= range)//추적 시작
        {
            isChasing = true;
        }
        else if (isChasing && distance > chaseRange)//추적 종료 (더 넓은 범위를 벗어나야 그만둠)
        {
            isChasing = false;
        }

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
        if (edgeAhead && ignoreEdgeTimer <= 0f)
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
    bool IsGroundAheadInDirection(float dirSign)
    {
        if (col == null) return true; // 안전장치: 콜라이더 없으면 그냥 이동 허용

        float halfWidth = col.bounds.extents.x;
        float oneThird = halfWidth * 2f / 3f;

        // 이동 방향 쪽 발끝 지점(기존 FixedUpdate의 leftPoint/rightPoint와 동일한 기준)
        Vector2 checkPoint = (Vector2)rigid.position + Vector2.right * oneThird * Mathf.Sign(dirSign);
        RaycastHit2D hit = Physics2D.Raycast(checkPoint, Vector2.down, 2, LayerMask.GetMask("Platform"));
        return hit.collider != null;
    }
}
