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
    public ContactHit contactHitbox;
    public LayerMask groundLayer;
    public LayerMask obstacleLayer;
    Transform target;
    public LayerMask targetLayer;
    public float minJumpDistance = 1f;


    bool isAttacking = false;
    bool canAttack = true;

    J_EnemyMove enemyMove;

    void Start()
    {
        enemyMove = GetComponent<J_EnemyMove>();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            target = player.transform;
        int playerLayer = LayerMask.NameToLayer("Player");
        int monsterLayer = LayerMask.NameToLayer("Monster");
        if (playerLayer != -1 && monsterLayer != -1)
            Physics2D.IgnoreLayerCollision(playerLayer, monsterLayer, true);

        if (contactHitbox != null)
        {
            contactHitbox.gameObject.layer = 0; // Default 레이어로 변경하여 Player/Monster 레이어 무시 상태에서도 피격 감지 보장
            contactHitbox.onTriggerEnter += TryContactDamage;
            contactHitbox.onTriggerStay += TryContactDamage;
        }
    }

    void TryContactDamage(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerHealth player = other.GetComponent<PlayerHealth>();
        if (player == null) player = other.GetComponentInParent<PlayerHealth>();
        if (player != null)
        {
            float damage = 0.5f;
            NormalMonster nm = GetComponent<NormalMonster>();
            if (nm == null) nm = GetComponentInParent<NormalMonster>();
            if (nm != null) damage = nm.attackDamage;

            player.TakeDamage(damage);
        }
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
            Vector2 startPos = transform.position;
            Vector2 desiredLandPos = target.position;
            Vector2 landPos = FindValidLandingSpot(startPos, desiredLandPos, out bool isCliff);

            // 착지 지점이 낭떠러지 때문에 잘린 경우, 점프 자체를 시도하지 않음 (텔레그래프도 안 뜨고 상태 변화 없음)
            if (isCliff) return;

            StartCoroutine(JumpAttackRoutine(landPos));
        }
    }

    System.Collections.IEnumerator JumpAttackRoutine(Vector2 landPos)
    {
        Collider2D selfColForJump = GetComponent<Collider2D>();
        Vector2 jumpColliderSize = selfColForJump != null ? selfColForJump.bounds.size * 0.95f : Vector2.one * 0.5f;

        isAttacking = true;
        canAttack = false;

        if (enemyMove != null)
            enemyMove.enabled = false;

        Vector2 startPos = transform.position;

        // 낭떠러지가 아니어도 착지 지점이 너무 가까우면(제자리 수준) 안전장치로 취소
        if (Vector2.Distance(startPos, landPos) < minJumpDistance)
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
            transform.position = new Vector3(desiredPos.x, desiredPos.y, transform.position.z);
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
            transform.position = landPos;
        }

        yield return new WaitForSeconds(postDelay);

        if (enemyMove != null)
            enemyMove.enabled = true;

        isAttacking = false;

        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }

    public float fallAcceleration = 20f;
    public float maxFallTime = 3f;

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

            float moveDist = fallSpeed * Time.deltaTime;

            // 이동하기 전에 먼저 발밑 기준으로 이번 프레임에 이동할 거리(moveDist)만큼 아래에
            // 바닥이 있는지 레이캐스트로 미리 검사함.
            // (기존에는 먼저 이동한 뒤 검사해서, 낙하 속도가 붙으면 바닥을 뚫고 들어간 위치까지
            //  이동한 다음에야 멈춰서 "땅속에 들어갔다 나오는" 현상이 있었음)
            RaycastHit2D groundHit = Physics2D.Raycast((Vector2)transform.position, Vector2.down, footOffset + moveDist, groundLayer);
            if (groundHit.collider != null)
            {
                // 뚫고 들어가지 않도록, 바닥 표면에 발이 정확히 닿는 위치로 정렬하고 종료
                transform.position = new Vector3(transform.position.x, groundHit.point.y + footOffset, transform.position.z);
                yield break;
            }

            transform.position += new Vector3(0f, -moveDist, 0f);
            yield return null;
        }
    }

    public float groundCheckRadius = 0.3f;

    // isCliff: 경로 중간에 바닥이 끊겨서 착지 지점이 원래 목표(desired)보다 앞에서 잘렸는지 여부
    Vector2 FindValidLandingSpot(Vector2 start, Vector2 desired, out bool isCliff)
    {
        isCliff = false;

        Collider2D selfCol = GetComponent<Collider2D>();
        float footOffset = selfCol != null ? selfCol.bounds.extents.y : 0.5f;

        float rayStartY = desired.y + 1f;
        Vector2 rayStart = new Vector2(desired.x, rayStartY);
        RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, 50f, groundLayer);
        Vector2 targetLandPos = hit.collider != null ? hit.point + Vector2.up * footOffset : desired;

        int steps = 10;
        Vector2 lastGroundPos = start;
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 checkPos = Vector2.Lerp(start, targetLandPos, t);
            Vector2 groundCheckPos = checkPos + Vector2.down * footOffset;

            bool foundGround = Physics2D.OverlapCircle(groundCheckPos, groundCheckRadius, groundLayer) != null;

            if (foundGround)
            {
                lastGroundPos = checkPos;
            }
            else
            {
                isCliff = true;
                return lastGroundPos;
            }
        }

        return targetLandPos;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 center = transform.position;
        Vector3 size = new Vector3(lineWidth * 2f, attackRange * 2f, 0.1f);
        Gizmos.DrawWireCube(center, size);
    }
}