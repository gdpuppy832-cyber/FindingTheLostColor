using UnityEngine;

public class J_EnemyAttack : MonoBehaviour
{
    public float attackRange = 4f;        // y축 범위
    public float lineWidth = 0.3f;        // x축 범위
    public float telegraphTime = 0.7f;    // 점프 전 경고 시간
    public float jumpDuration = 0.6f;     // 점프(공중에 떠있는) 시간
    public float jumpHeight = 2f;         // 포물선 최고 높이
    public float landRadius = 1.5f;       // 착지 피해 범위
    public float postDelay = 0.5f;        // 착지 후 이동 불가 딜레이
    public float attackCooldown = 2f;     // 쿨타임
    public ContactRelay contactHitbox;
    public LayerMask groundLayer;
    public LayerMask obstacleLayer;
    Transform target;
    public LayerMask targetLayer;


    bool isAttacking = false;
    bool canAttack = true;

    J_EnemyMove enemyMove;
    Rigidbody2D rb;
    void Start()
    {
        enemyMove = GetComponent<J_EnemyMove>();
        rb = GetComponent<Rigidbody2D>();


        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            target = player.transform;
        int playerLayer = LayerMask.NameToLayer("Player");
        int monsterLayer = LayerMask.NameToLayer("Monster");
        if (playerLayer != -1 && monsterLayer != -1)
            Physics2D.IgnoreLayerCollision(playerLayer, monsterLayer, true);

        if (contactHitbox != null)
        {
            contactHitbox.onTriggerEnter += TryContactDamage;
            contactHitbox.onTriggerStay += TryContactDamage;
        }
    }

    void TryContactDamage(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        Debug.Log("플레이어에게 접촉 피해를 입혔다: ");
    }

    void Update()
    {
        if (target == null)
        {
            Debug.Log("타겟이 null입니다. Player 태그를 확인하세요.");
            return;
        }
        if (isAttacking || !canAttack) return;

        float horizontalDist = Mathf.Abs(target.position.x - transform.position.x);
        float verticalDist = Mathf.Abs(target.position.y - transform.position.y);

        if (horizontalDist <= lineWidth && verticalDist <= attackRange)
        {
            
            StartCoroutine(JumpAttackRoutine());
        }
    }

    System.Collections.IEnumerator JumpAttackRoutine()
    {
        Collider2D selfColForJump = GetComponent<Collider2D>();
        Vector2 jumpColliderSize = selfColForJump != null ? selfColForJump.bounds.size * 0.95f : Vector2.one * 0.5f;

        float originalGravityScale = 0f;
        if (rb != null)
        {
            originalGravityScale = rb.gravityScale;
            rb.gravityScale = 0f;      // 점프/낙하 도중엔 물리 중력이 위치를 방해하지 않도록 끔
            rb.linearVelocity = Vector2.zero;
        }

        isAttacking = true;
        canAttack = false;

        if (enemyMove != null)
            enemyMove.enabled = false;

        // 점프 시작/도착 지점 고정 (텔레그래프 시점 기준)
        Vector2 startPos = transform.position;
        Vector2 desiredLandPos = target.position;
        Vector2 landPos = FindValidLandingSpot(startPos, desiredLandPos);
        

        if (Vector2.Distance(startPos, landPos) < 0.1f)
        {
            
            isAttacking = false;
            canAttack = true;
            if (enemyMove != null)
                enemyMove.enabled = true;
            yield break;
        }
        yield return new WaitForSeconds(telegraphTime);



        // 포물선 점프
        float elapsed = 0f;
        Vector2 lastValidPos = startPos;
        bool hitCeiling = false;

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / jumpDuration);
            Vector2 flatPos = Vector2.Lerp(startPos, landPos, t);
            float heightOffset = 4f * jumpHeight * t * (1f - t);


            Vector2 desiredPos = new Vector2(flatPos.x, flatPos.y + heightOffset);

            // 천장(장애물) 충돌 체크: 직전 위치 -> 목표 위치 사이에 장애물이 있으면 그 지점 앞에서 멈춤
            Vector2 moveDir = desiredPos - lastValidPos;
            float moveDist = moveDir.magnitude;
            if (moveDist > 0.0001f)
            {
                RaycastHit2D hit = Physics2D.BoxCast(lastValidPos, jumpColliderSize, 0f, moveDir.normalized, moveDist, obstacleLayer);
                if (hit.collider != null)
                {
                    Debug.Log("점프 중 천장(장애물)에 부딪힘: " + hit.collider.name);
                    desiredPos = hit.point - moveDir.normalized * 0.05f;
                    hitCeiling = true;
                }
            }

            lastValidPos = desiredPos;
            if (rb != null) rb.position = desiredPos;
            else transform.position = new Vector3(desiredPos.x, desiredPos.y, transform.position.z);
            yield return null;

            if (hitCeiling)   
                break;
        }

        if (hitCeiling)
        {
            yield return StartCoroutine(FallToGround());
        }
        else
        {
            if (rb != null) rb.position = landPos;
            else transform.position = landPos; // 정확히 착지 지점에 정렬
        }

        // 착지 완료 → 중력/속도를 원래대로 복구
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = originalGravityScale;
        }

        yield return new WaitForSeconds(postDelay);

        if (enemyMove != null)
            enemyMove.enabled = true;

        isAttacking = false;

        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }
    public float fallAcceleration = 20f;   // 낙하 가속도(중력 느낌)
    public float maxFallTime = 3f;         // 바닥을 못 찾았을 때 무한 낙하 방지용 안전장치

    System.Collections.IEnumerator FallToGround()
    {
        Collider2D selfCol = GetComponent<Collider2D>();
        float footOffset = selfCol != null ? selfCol.bounds.extents.y : 0.5f;

        float fallSpeed = 0f;
        float fallElapsed = 0f;

        while (fallElapsed < maxFallTime)
        {
            fallElapsed += Time.deltaTime;
            fallSpeed += fallAcceleration * Time.deltaTime;

            Vector2 nextPos = (Vector2)transform.position + Vector2.down * fallSpeed * Time.deltaTime;
            Vector2 groundCheckPos = nextPos + Vector2.down * footOffset;

            bool foundGround = Physics2D.OverlapCircle(groundCheckPos, groundCheckRadius, groundLayer) != null;

            if (rb != null) rb.position = nextPos;
            else transform.position = new Vector3(nextPos.x, nextPos.y, transform.position.z);

            if (foundGround)
                yield break;

            yield return null;
        }
    }

    public float groundCheckRadius = 0.3f;  // 바닥 감지 반경

    Vector2 FindValidLandingSpot(Vector2 start, Vector2 desired)
    {
        // 콜라이더 하단(발밑)까지의 거리를 자동 계산 (피벗이 중앙이어도 정확히 발밑을 검사)
        Collider2D selfCol = GetComponent<Collider2D>();
        float footOffset = selfCol != null ? selfCol.bounds.extents.y : 0.5f;

        int steps = 10; // 경로를 몇 단계로 나눠 검사할지
        for (int i = steps; i >= 0; i--)
        {
            float t = (float)i / steps;
            Vector2 checkPos = Vector2.Lerp(start, desired, t);
            Vector2 groundCheckPos = checkPos + Vector2.down * footOffset;

            bool foundGround = Physics2D.OverlapCircle(groundCheckPos, groundCheckRadius, groundLayer) != null;
            

            if (foundGround)
                return checkPos;
        }

        return start;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 center = transform.position;
        Vector3 size = new Vector3(lineWidth * 2f, attackRange * 2f, 0.1f);
        Gizmos.DrawWireCube(center, size);
    }
}