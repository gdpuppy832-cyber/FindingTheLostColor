using UnityEngine;

public class J_EnemyAttack : MonoBehaviour
{
    public float attackRange = 4f;        // y축 범위
    public float lineWidth = 0.3f;        // x축 범위
    public float telegraphTime = 0.7f;    // 점프 전 경고 시간
    public float jumpDuration = 0.6f;     // 점프(공중에 떠있는) 시간
    public float jumpHeight = 2f;         // 포물선 최고 높이
    public float landRadius = 1.5f;       // 착지 피해 범위
    public float postDelay = 0.5f;        // 착지 후 이동 불가 딜레이 (공격 실패 시 적용)
    public float hitPostDelay = 0.25f;    // 착지 후 이동 불가 딜레이 (공격 성공 시 적용, 조정 가능)
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
    Animator animator; // 자식 오브젝트에 있는 경우도 대비해서 GetComponentInChildren 사용

    // 이번 점프 공격 도중 플레이어와 실제로 충돌(ContactHit)했는지 여부 -> 후딜 결정에 사용
    bool hitPlayerThisJump = false;

    GameObject activeMissIndicator;

    [Header("공격 실패 표시 오브젝트")]
    [Tooltip("공격 실패(후딜레이) 동안 몬스터 머리 위에 띄울, 자체 애니메이션이 있는 프리팹 (비워두면 생성 안 함)")]
    public GameObject missIndicatorPrefab;

    [Tooltip("몬스터 기준 표시 오브젝트가 생성될 위치 오프셋")]
    public Vector3 missIndicatorOffset = new Vector3(0f, 1.5f, 0f);

    [Tooltip("공격 실패 후딜레이가 시작되고 나서, 표시 오브젝트가 실제로 나타나기까지 대기하는 시간(초)")]
    public float missIndicatorDelay = 0.2f;

    void Start()
    {
        enemyMove = GetComponent<J_EnemyMove>();
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

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

            // 점프 공격 도중이었다면, 실제로 플레이어와 충돌했다는 것을 기록해서 후딜 판정에 사용
            if (isAttacking)
            {
                hitPlayerThisJump = true;
            }
        }
    }

    void Update()
    {
        if (target == null)
        {
            return;
        }
        if (isAttacking || !canAttack) return;

        // 추적 시작/종료 시 J_EnemyMove가 잠시 멈춰있는 동안(isStateDelay)에는 공격을 시도하지 않음
        if (enemyMove != null && enemyMove.IsStateDelay) return;

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
        hitPlayerThisJump = false; // 새 점프 시작 시 충돌 기록 초기화

        if (animator != null)
        {
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsAttacking", true);
        }

        if (enemyMove != null)
            enemyMove.enabled = false;

        Vector2 attackStartPos = transform.position;

        // 낭떠러지가 아니어도 착지 지점이 너무 가까우면(제자리 수준) 안전장치로 취소
        if (Vector2.Distance(attackStartPos, landPos) < minJumpDistance)
        {
            isAttacking = false;
            canAttack = true;

            if (animator != null)
                animator.SetBool("IsAttacking", false);

            if (enemyMove != null)
                enemyMove.enabled = true;
            yield break;
        }
        yield return new WaitForSeconds(telegraphTime);
        Vector2 startPos = transform.position;

        // 점프 시작 직전, 날아가는 방향(착지 지점 쪽)을 바라보도록 스프라이트 반전
        if (landPos.x != startPos.x)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (landPos.x < startPos.x ? 1f : -1f);
            transform.localScale = scale;
        }

        // 포물선 점프 (장애물/천장 감지 없이 계획된 착지 지점까지 그대로 진행)
        float elapsed = 0f;

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / jumpDuration);
            Vector2 flatPos = Vector2.Lerp(startPos, landPos, t);
            float heightOffset = 4f * jumpHeight * t * (1f - t);

            Vector2 desiredPos = new Vector2(flatPos.x, flatPos.y + heightOffset);

            transform.position = new Vector3(desiredPos.x, desiredPos.y, transform.position.z);
            yield return null;
        }

        transform.position = landPos;

        // 착지 후에도 아주 짧게 대기하며 충돌 판정이 들어올 기회를 줌
        // (ContactHit의 onTriggerEnter/onTriggerStay는 물리 프레임에 반응하므로, 착지 직후 한 프레임 정도는 필요)
        yield return new WaitForFixedUpdate();

        // 점프 도중 실제로 플레이어와 충돌(TryContactDamage 발동)했는지에 따라 후딜 시간을 다르게 적용
        // 성공: hitPostDelay(기본 0.25초, 조정 가능) / 실패: postDelay(기본 0.5초)
        if (hitPlayerThisJump)
        {
            yield return new WaitForSeconds(hitPostDelay);
        }
        else
        {
            // 공격 실패 시에만 IsDelaying 애니메이션 파라미터를 켬
            if (animator != null) animator.SetBool("IsDelaying", true);

            // NormalMonster는 수정하지 않는 전제이므로, 정화 여부(IsPurified)는 참조만 해서 확인
            NormalMonster nm = GetComponent<NormalMonster>();
            if (nm == null) nm = GetComponentInParent<NormalMonster>();

            bool indicatorSpawned = false;
            float waitElapsed = 0f;

            // postDelay 동안 매 프레임 진행하면서,
            // 1) missIndicatorDelay가 지나면 그때 표시 오브젝트를 생성하고
            // 2) 표시 중에 몬스터가 정화되면 즉시 파괴함
            // (참고: 정화로 인해 이 스크립트 자체가 비활성화되면 이 루프도 함께 멈추는데,
            //  그 경우를 대비한 정리는 OnDisable에서 별도로 처리함)
            while (waitElapsed < postDelay)
            {
                waitElapsed += Time.deltaTime;

                if (!indicatorSpawned && waitElapsed >= missIndicatorDelay)
                {
                    activeMissIndicator = SpawnMissIndicator();
                    indicatorSpawned = true;
                }

                if (activeMissIndicator != null && nm != null && nm.IsPurified)
                {
                    Destroy(activeMissIndicator);
                    activeMissIndicator = null;
                }

                yield return null;
            }

            if (animator != null) animator.SetBool("IsDelaying", false);

            if (activeMissIndicator != null)
            {
                Destroy(activeMissIndicator);
                activeMissIndicator = null;
            }
        }

        if (animator != null) animator.SetBool("IsAttacking", false);

        isAttacking = false;

        // enemyMove를 IsAttacking을 끈 다음에 활성화해서,
        // 같은 프레임에 IsWalking이 급하게 바뀌어 트랜지션이 꼬이는 걸 방지
        if (enemyMove != null)
            enemyMove.enabled = true;

        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }
    // NormalMonster.Purify()가 이 컴포넌트를 강제로 비활성화시킬 때 Unity가 자동 호출.
    // 그 시점에 코루틴이 멈춰서 위 while 루프의 정리 로직이 실행되지 못하므로,
    // 여기서 확실하게 표시 오브젝트를 파괴함
    void OnDisable()
    {
        if (activeMissIndicator != null)
        {
            Destroy(activeMissIndicator);
            activeMissIndicator = null;
        }
    }

    // 공격 실패 시 몬스터 머리 위에 띄울 표시 오브젝트 생성 (자체 애니메이션은 프리팹의 Animator가 알아서 재생)
    GameObject SpawnMissIndicator()
    {
        if (missIndicatorPrefab == null) return null;

        GameObject indicator = Instantiate(missIndicatorPrefab, transform);
        indicator.transform.localPosition = missIndicatorOffset;
        indicator.transform.localRotation = Quaternion.identity;
        return indicator;
    }


    public float groundCheckRadius = 0.3f;


    [Tooltip("경로 중간 지점 아래로 이 거리 안에 땅이 있으면 낭떠러지로 취급하지 않고 점프를 계속 진행함 (계단/턱 아래로 착지 허용)")]
    public float safeDropDistance = 3f;

    // isCliff: 경로 중간에 (safeDropDistance 안에서도) 바닥을 전혀 못 찾거나,
    // 시작 지점보다 착지 지점이 safeDropDistance 이상 훨씬 낮아서 (진짜 낭떠러지로 추정되어) 취소된 경우 true
    Vector2 FindValidLandingSpot(Vector2 start, Vector2 desired, out bool isCliff)
    {
        isCliff = false;

        Collider2D selfCol = GetComponent<Collider2D>();
        float footOffset = selfCol != null ? selfCol.bounds.extents.y : 0.5f;

        float rayStartY = desired.y + 1f;
        Vector2 rayStart = new Vector2(desired.x, rayStartY);
        RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, 50f, groundLayer);
        Vector2 targetLandPos = hit.collider != null ? hit.point + Vector2.up * footOffset : desired;

        // 안전장치 1: 시작 지점 기준 착지 지점이 safeDropDistance보다 훨씬 더 아래(진짜 낭떠러지 낙차)면
        // 애초에 점프 자체를 취소함. 계단 정도의 낙차(safeDropDistance 이내)만 허용.
        float startGroundY = start.y - footOffset;
        float landGroundY = targetLandPos.y - footOffset;
        if (startGroundY - landGroundY > safeDropDistance)
        {
            isCliff = true;
            return start;
        }

        int steps = 10;
        Vector2 lastGroundPos = start;
        float lastGroundY = startGroundY; // 급격한 단차(중간에 툭 떨어지는 진짜 낭떠러지) 감지용

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 checkPos = Vector2.Lerp(start, targetLandPos, t);

            // 바로 아래(footOffset)만 검사하는 대신, safeDropDistance만큼 더 깊게 검사해서
            // 그 안에 땅이 있으면 낭떠러지로 취급하지 않고 점프를 계속 진행함
            RaycastHit2D dropHit = Physics2D.Raycast(checkPos, Vector2.down, footOffset + safeDropDistance, groundLayer);

            if (dropHit.collider == null)
            {
                // safeDropDistance 안에서도 땅을 전혀 못 찾음 -> 진짜 낭떠러지
                isCliff = true;
                return lastGroundPos;
            }

            // 안전장치 2: 직전 검사 지점의 땅보다 이번 지점의 땅이 safeDropDistance 이상 갑자기 낮아지면
            // (경로 중간에 급격한 단차/낭떠러지가 있다는 뜻이므로) 취소
            if (lastGroundY - dropHit.point.y > safeDropDistance)
            {
                isCliff = true;
                return lastGroundPos;
            }

            lastGroundPos = checkPos;
            lastGroundY = dropHit.point.y;
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