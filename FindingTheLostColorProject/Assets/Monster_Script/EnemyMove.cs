using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public class EnemyMove : MonoBehaviour
{
    public float speed = 1.5f;
    public Transform target;
    public float range;
    float timer = 0;
    Vector3 prevposition;
    Rigidbody2D rigid;
    bool isStopped = false; 
    float stopTimer = 0f;
    float ignoreEdgeTimer = 0f;
    Collider2D col;
    public float chaseRange; 
    bool isChasing = false;
    public float attackStopDistance = 1.5f;

    void Start()
    {
        prevposition = transform.position;
        rigid = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }


    void Update()
    {
        float distance = Vector3.Distance(transform.position, target.position);

        if (ignoreEdgeTimer > 0f)//방향 전환 직후 보호 시간
            ignoreEdgeTimer -= Time.deltaTime;

        if (isStopped)//절벽 끝에서 멈춘 상태
        {
            stopTimer += Time.deltaTime;
            if(stopTimer >= 0.5f)
            {
                isStopped = false;
                stopTimer = 0f;

                if (timer < 3.5f)
                    timer = 3.5f;
                else
                    timer = 0f;

                ignoreEdgeTimer = 2f; 
            }
            return; 
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

        if (isChasing)//추적 모드
        {
            float xDiff = target.position.x - transform.position.x;
            if (Mathf.Abs(xDiff) > attackStopDistance) 
            {
                float xDir = Mathf.Sign(xDiff);
                transform.Translate(speed * 1.5f * xDir * Time.deltaTime, 0f, 0f);
            }
        }
        else if (timer < 3)//배회상태
        {
            transform.Translate(new Vector2(-speed * Time.deltaTime, 0f));
        }
        else if (timer > 3.5 && timer < 6.5)
        {
            transform.Translate(new Vector2(speed * Time.deltaTime, 0f));
        }
        else if (timer > 7)
        {
            timer = 0;
        }

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

        bool isGrounded = leftHit.collider != null && rightHit.collider != null;

        if (!isGrounded && !isStopped && ignoreEdgeTimer <= 0f)
        {
            isStopped = true;
            stopTimer = 0f;
        }
    }
}
