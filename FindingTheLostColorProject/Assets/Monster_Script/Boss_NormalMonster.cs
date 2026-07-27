using UnityEngine;


[RequireComponent(typeof(NormalMonster))]
public class Boss_NormalMonster : MonoBehaviour
{
    [Header("Flee Settings")]
    [Tooltip("정화된 직후 움직이기 시작하기까지 멈춰있는 시간 (초)")]
    public float pauseDuration = 0.5f;

    [Tooltip("직진 이동 속도")]
    public float moveSpeed = 4f;

    public enum FleeDirection { Right, Left, Auto }
    [Tooltip("도망칠 방향. Auto면 정화되는 순간 플레이어 반대쪽으로 자동 결정")]
    public FleeDirection direction = FleeDirection.Auto;

    [Tooltip("화면 밖으로 나갔다고 판정할 여유 마진 (뷰포트 기준 비율, 0.1 = 화면 경계에서 10% 더 벗어나야 사라짐)")]
    public float offScreenMargin = 0.1f;

    [Header("Ground Detection (벽은 무시하되 바닥은 유지하기 위함)")]
    [Tooltip("바닥으로 인식할 레이어")]
    public LayerMask groundLayer;
    [Tooltip("바닥에 닿아있다고 판정할 여유 거리")]
    public float groundCheckDistance = 0.1f;
    [Tooltip("공중에 떠 있을 때 떨어지는 가속도")]
    public float fallAcceleration = 20f;

    NormalMonster normalMonster;
    Rigidbody2D rb;
    Collider2D selfCol;
    Camera cam;
    Animator animator; // 자식 오브젝트에 있는 경우도 대비해서 GetComponentInChildren 사용

    float verticalVelocity = 0f;

    float moveDir = 1f;
    bool isFleeing = false;
    bool wasPurified = false;

    void Awake()
    {
        normalMonster = GetComponent<NormalMonster>();
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = GetComponentInParent<Rigidbody2D>();
        if (rb == null) rb = GetComponentInChildren<Rigidbody2D>();
        cam = Camera.main;

        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        selfCol = GetComponent<Collider2D>();
        if (selfCol == null) selfCol = GetComponentInChildren<Collider2D>();
    }

    void Update()
    {
        // 정화되는 순간(false -> true로 바뀌는 프레임)을 감지
        if (!wasPurified && normalMonster.IsPurified)
        {
            wasPurified = true;
            StartCoroutine(FleeRoutine());
        }

        if (isFleeing && IsOffScreen())
        {
            Destroy(gameObject);
        }
    }

    void FixedUpdate()
    {
        if (!isFleeing || rb == null) return;

        float footOffset = selfCol != null ? selfCol.bounds.extents.y : 0.5f;

        // 레이가 벽 콜라이더 "내부"에서 시작할 경우(몬스터가 벽을 통과 중일 때) 즉시 그 자리를
        // 바닥으로 오인해서 위로 튕겨 올라가는 문제를 막기 위해, 이 두 레이캐스트 동안만
        // "콜라이더 내부에서 시작하는 레이는 무시"하도록 설정함
        bool prevQueriesStartInColliders = Physics2D.queriesStartInColliders;
        Physics2D.queriesStartInColliders = false;

        // 바로 아래에 바닥이 있는지 직접 검사 (물리 충돌이 아니라 레이캐스트로만 판정 -> 벽은 영향 없음)
        RaycastHit2D groundHit = Physics2D.Raycast(rb.position, Vector2.down, footOffset + groundCheckDistance, groundLayer);

        float deltaY;
        if (groundHit.collider != null)
        {
            // 바닥에 닿아있으면 그 표면에 맞춰 고정하고 낙하 속도 초기화
            verticalVelocity = 0f;
            float targetY = groundHit.point.y + footOffset;
            deltaY = targetY - rb.position.y;
        }
        else
        {
            // 바닥이 없으면 계속 낙하 (뚫고 들어가지 않도록 이동 전에 미리 거리 검사)
            verticalVelocity -= fallAcceleration * Time.fixedDeltaTime;
            float moveDist = -verticalVelocity * Time.fixedDeltaTime;

            RaycastHit2D preCheck = Physics2D.Raycast(rb.position, Vector2.down, footOffset + moveDist, groundLayer);
            if (preCheck.collider != null)
            {
                verticalVelocity = 0f;
                deltaY = (preCheck.point.y + footOffset) - rb.position.y;
            }
            else
            {
                deltaY = -moveDist;
            }
        }

        // 원래 설정으로 복구 (다른 스크립트의 레이캐스트 동작에 영향 주지 않도록)
        Physics2D.queriesStartInColliders = prevQueriesStartInColliders;

        Vector2 newPos = rb.position + new Vector2(moveDir * moveSpeed * Time.fixedDeltaTime, deltaY);
        rb.MovePosition(newPos);

        // 도망치는 방향에 맞춰 스프라이트 좌우 반전
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * -moveDir;
        transform.localScale = scale;
    }

    System.Collections.IEnumerator FleeRoutine()
                {
                    // 도망칠 방향 결정
                    if (direction == FleeDirection.Right)
                    {
                        moveDir = 1f;
                    }
                    else if (direction == FleeDirection.Left)
                    {
                        moveDir = -1f;
                    }
                    else
                    {
                        // Auto: 플레이어 반대쪽으로 도망
                        PlayerHealth player = FindFirstObjectByType<PlayerHealth>();
                        if (player != null)
                        {
                            moveDir = transform.position.x >= player.transform.position.x ? 1f : -1f;
                        }
                        else
                        {
                            float scaleSign = Mathf.Sign(transform.localScale.x);
                            moveDir = scaleSign != 0f ? scaleSign : 1f;
                        }
                    }

                    yield return new WaitForSeconds(pauseDuration);

                    // Kinematic으로 전환하면 물리 충돌(밀림)을 전혀 받지 않게 되어 벽을 그대로 통과함.
                    // 바닥은 아래에서 별도의 레이캐스트로 직접 감지해서 착지 상태를 유지시킴 (FixedUpdate 참고)
                    if (rb != null)
                    {
                        rb.bodyType = RigidbodyType2D.Kinematic;
                        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                    }

                    isFleeing = true;

                    if (animator != null)
                        animator.SetBool("IsWalking", true);
                }

                bool IsOffScreen()
                {
                    if (cam == null) cam = Camera.main;
                    if (cam == null) return false;

                    Vector3 viewportPos = cam.WorldToViewportPoint(transform.position);
                    return viewportPos.x < -offScreenMargin || viewportPos.x > 1f + offScreenMargin;
                }
            }
        
    