using UnityEngine;

public class H_MonsterAttack : MonoBehaviour
{
    [Header("기본 점프 공격 설정 (J_Monster 사양)")]
    public float attackRange = 4f;        // y축 범위
    public float lineWidth = 2.0f;        // x축 범위 (기존 0.3f에서 2.0f로 보정하여 도약 거리 연동 보장)
    public float telegraphTime = 0.7f;    // 점프 전 경고 시간
    public float jumpDuration = 0.6f;     // 점프(공중 체공) 시간
    public float jumpHeight = 2f;         // 포물선 최고 높이
    public float landRadius = 1.5f;       // 착지 피해 범위
    public float postDelay = 0.5f;        // 착지 후 이동 불가 딜레이
    public float attackCooldown = 2f;     // 쿨타임

    [Header("최초 위장 덮치기(Initial Pounce) 설정")]
    [Tooltip("최초 덮치기 점프 전 위협 경고 대기 시간 (초)")]
    public float initialTelegraphTime = 1.0f;

    [Tooltip("최초 덮치기 점프 시 도약 높이 (더 높은 포물선을 그려 멀리 가도록 유도)")]
    public float initialJumpHeight = 3.5f;

    [Tooltip("최초 덮치기 점프 시 공중 체공 시간 (더 빠르게 날아오도록 유도)")]
    public float initialJumpDuration = 0.5f;

    [Tooltip("최초 덮치기 공격 실패(착지) 시 기절 대기 시간 (초)")]
    public float stunDuration = 3.0f;

    [Header("기타 물리 및 필터링 설정")]
    public ContactHit contactHitbox;  
    public LayerMask groundLayer;
    public LayerMask obstacleLayer;
    Transform target;
    public LayerMask targetLayer;
    public float minJumpDistance = 1f;

    [Header("일반 근접 공격 설정 (최초 덮치기 이후, 일반 몬스터 사양)")]
    public float meleeWidth = 2f;
    public float meleeHeight = 1f;
    public float meleeTelegraphTime = 1f;
    public SpriteRenderer meleeTelegraphSprite;
    public float meleePostDelay = 0.5f;
    public float meleeCooldown = 2f;

    bool isAttacking = false;
    bool canAttack = true;

    H_MonsterMove enemyMove;
    private Rigidbody2D rigid;

    // 최초 덮치기 도중 플레이어 타격 성공 여부 플래그
    private bool initialPounceHitPlayer = false;

    void Start()
    {
        enemyMove = GetComponent<H_MonsterMove>();
        rigid = GetComponent<Rigidbody2D>();

        // 게임 시작 시 텔레그래프가 그대로 보이는 문제 방지 (일반 몬스터 EnemyAttack.cs와 동일하게 처음부터 꺼둠)
        if (meleeTelegraphSprite != null)
            meleeTelegraphSprite.enabled = false;

        // groundLayer가 Nothing(0)으로 풀려있으면 기본 Platform 레이어로 자동 할당하여 예외 차단
        if (groundLayer.value == 0)
        {
            groundLayer = LayerMask.GetMask("Platform");
            Debug.Log($"[H_MonsterAttack] {gameObject.name}의 groundLayer가 비어있어 기본 'Platform' 레이어로 자동 할당했습니다.");
        }

        // targetLayer가 Nothing(0)으로 풀려있으면 근접/점프 판정(OverlapBox 등)이 항상 실패하므로
        // 기본 Player 레이어로 자동 할당하여 "공격이 아예 안 나가는" 문제를 원천 차단
        if (targetLayer.value == 0)
        {
            targetLayer = LayerMask.GetMask("Player");
            Debug.LogWarning($"[H_MonsterAttack] {gameObject.name}의 targetLayer가 비어있어 기본 'Player' 레이어로 자동 할당했습니다. 인스펙터에서 직접 설정해두는 것을 권장합니다.");
        }

        // contactHitbox 슬롯이 비어있으면 자식 오브젝트에서 자동으로 찾아 연결 (인스펙터 실수 방지)
        if (contactHitbox == null)
        {
            contactHitbox = GetComponentInChildren<ContactHit>();
            if (contactHitbox != null)
            {
                Debug.Log($"[H_MonsterAttack] {gameObject.name}의 자식 오브젝트에서 ContactRelay({contactHitbox.gameObject.name})를 찾아 자동 매칭했습니다.");
            }
            else
            {
                Debug.LogWarning($"[H_MonsterAttack] {gameObject.name}에게서 피해를 줄 ContactRelay 자식 오브젝트를 찾을 수 없습니다! 콜라이더 피해가 들어가지 않을 수 있습니다.");
            }
        }

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
        // 몬스터가 완전히 잠복 모드(isAmbushed) 중일 때는 접촉 피해를 일절 주지 않음
        if (enemyMove != null && enemyMove.IsAmbushed) return;

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

            // ★ [요구사항] 최초 돌진 상태(isAttacking) 중 플레이어 타격에 성공하면 플래그 활성화
            if (isAttacking && enemyMove != null && !enemyMove.IsAmbushed)
            {
                initialPounceHitPlayer = true;
            }
        }
    }

    void Update()
    {
        if (target == null)
        {
            return;
        }

        // 잠복 상태 중이거나 공격 진행중/쿨타임 대기 상태라면 자동 공격 로직 진입 안함
        // (최초 덮치기는 H_MonsterMove에서 TriggerPounce를 호출해 수동 트리거함)
        if (enemyMove != null && enemyMove.IsAmbushed) return;
        if (isAttacking || !canAttack) return;

        // 최초 덮치기(점프) 이후부터는 일반 몬스터와 동일한 근접 공격 판정을 사용
        if (IsMeleeInRange())
        {
            StartCoroutine(MeleeAttackRoutine());
        }
    }

    // 몬스터가 바라보는 방향 기준 근접 판정 범위 안에 타겟이 있는지 확인 (EnemyAttack.cs와 동일한 방식)
    bool IsMeleeInRange()
    {
        float dir = -Mathf.Sign(transform.localScale.x);
        if (dir == 0f) dir = 1f;

        Collider2D selfCol = GetComponent<Collider2D>();
        float edgeOffset = selfCol != null ? selfCol.bounds.extents.x : 0f;

        Vector2 attackCenter = (Vector2)transform.position
            + new Vector2(dir * edgeOffset, 0f)
            + new Vector2(dir * meleeWidth / 2f, 0f);

        return Physics2D.OverlapBox(attackCenter, new Vector2(meleeWidth, meleeHeight), 0f, targetLayer) != null;
    }

    System.Collections.IEnumerator MeleeAttackRoutine()
    {
        isAttacking = true;
        canAttack = false;

        if (enemyMove != null)
            enemyMove.enabled = false;

        float dir = -Mathf.Sign(transform.localScale.x);
        if (dir == 0f) dir = 1f;

        Collider2D selfCol = GetComponent<Collider2D>();
        float edgeOffset = selfCol != null ? selfCol.bounds.extents.x : 0f;

        Vector2 attackCenter = (Vector2)transform.position
            + new Vector2(dir * edgeOffset, 0f)
            + new Vector2(dir * meleeWidth / 2f, 0f);

        if (meleeTelegraphSprite != null)
        {
            meleeTelegraphSprite.enabled = true;
            meleeTelegraphSprite.transform.position = new Vector3(attackCenter.x, attackCenter.y, meleeTelegraphSprite.transform.position.z);
            meleeTelegraphSprite.size = new Vector2(meleeWidth, meleeHeight);
        }

        yield return new WaitForSeconds(meleeTelegraphTime);

        if (meleeTelegraphSprite != null)
            meleeTelegraphSprite.enabled = false;

        Collider2D hit = Physics2D.OverlapBox(attackCenter, new Vector2(meleeWidth, meleeHeight), 0f, targetLayer);
        if (hit != null)
        {
            Debug.Log("근접 공격 적중: " + hit.name);
            PlayerHealth player = hit.GetComponent<PlayerHealth>();
            if (player == null) player = hit.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                float damage = 0.5f;
                NormalMonster nm = GetComponent<NormalMonster>();
                if (nm == null) nm = GetComponentInParent<NormalMonster>();
                if (nm != null) damage = nm.attackDamage;

                player.TakeDamage(damage);
            }
        }

        yield return new WaitForSeconds(meleePostDelay);

        if (enemyMove != null)
            enemyMove.enabled = true;

        isAttacking = false;

        yield return new WaitForSeconds(meleeCooldown);
        canAttack = true;
    }

    /// <summary>
    /// H_MonsterMove가 플레이어를 첫 포착해 숨겨진 상태에서 점프 덮치기 공격을 가할 때 호출됩니다.
    /// </summary>
    public void TriggerPounce()
    {
        if (isAttacking)
        {
            Debug.LogWarning($"[H_MonsterAttack] {gameObject.name}가 이미 공격 중이므로 덮치기 요청을 무시합니다.");
            return;
        }
        initialPounceHitPlayer = false; // 타격 플래그 리셋
        StartCoroutine(JumpAttackRoutine(true));
    }

    System.Collections.IEnumerator JumpAttackRoutine(bool isInitialPounce)
    {
        Debug.Log($"[H_MonsterAttack] JumpAttackRoutine 시작됨! (isInitialPounce: {isInitialPounce})");

        Collider2D selfColForJump = GetComponent<Collider2D>();
        Vector2 jumpColliderSize = selfColForJump != null ? selfColForJump.bounds.size * 0.95f : Vector2.one * 0.5f;

        isAttacking = true;
        canAttack = false;

        if (enemyMove != null)
            enemyMove.enabled = false;

        // 점프 시작/착지 지점 고정
        Vector2 startPos = transform.position;
        Vector2 desiredLandPos = target.position;
        Vector2 landPos = FindValidLandingSpot(startPos, desiredLandPos);

        float jumpDistance = Vector2.Distance(startPos, landPos);
        Debug.Log($"[H_MonsterAttack] 계산된 착지 위치: {landPos}, 현재 위치로부터의 거리: {jumpDistance}");

        // 덮치기 경로에 서 있을 지형이 마땅치 않아 점프가 취소되는 경우의 예외 복구 및 디버그 경고 출력
        if (jumpDistance < minJumpDistance)
        {
            Debug.LogWarning($"[H_MonsterAttack] 점프가 취소되었습니다! (도약 거리 {jumpDistance}가 최소 거리 {minJumpDistance}보다 작음). groundLayer가 정상적으로 매칭되었는지 확인하세요.");
            
            isAttacking = false;
            if (enemyMove != null)
            {
                enemyMove.enabled = true;
                if (isInitialPounce)
                {
                    enemyMove.CancelPounce();
                }
            }
            // 스팸 루프 방지를 위해 짧은 취소 대기 쿨다운 부여 후 해제
            yield return new WaitForSeconds(0.5f);
            canAttack = true;
            yield break;
        }
        
        // [핵심 요구사항] 최초 돌진일 경우 설정된 별도의 대기시간(initialTelegraphTime)을 사용합니다.
        float activeTelegraphTime = isInitialPounce ? initialTelegraphTime : telegraphTime;
        Debug.Log($"[H_MonsterAttack] 점프 전 위협 경고 대기 시간 시작 (대기 시간: {activeTelegraphTime}초)");
        yield return new WaitForSeconds(activeTelegraphTime);

        // ★ [핵심 요구사항] 위장상태에서 공중으로 몸을 던져 날아가는 순간, 모습을 바꿉니다 (WakeUp 호출)
        if (isInitialPounce && enemyMove != null)
        {
            enemyMove.WakeUp();
        }

        // [핵심 요구사항] 최초 돌진일 경우 설정된 돌진 사양(높이/공중 시간)을 사용합니다.
        float activeJumpHeight = isInitialPounce ? initialJumpHeight : jumpHeight;
        float activeJumpDuration = isInitialPounce ? initialJumpDuration : jumpDuration;

        Debug.Log($"[H_MonsterAttack] 포물선 공중 비행 시작! (목표 위치: {landPos}, 높이: {activeJumpHeight}, 시간: {activeJumpDuration}초)");

        // 포물선 점프 시작
        float elapsed = 0f;
        Vector2 lastValidPos = startPos;
        bool hitCeiling = false;

        while (elapsed < activeJumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / activeJumpDuration);
            Vector2 flatPos = Vector2.Lerp(startPos, landPos, t);
            float heightOffset = 4f * activeJumpHeight * t * (1f - t);

            Vector2 desiredPos = new Vector2(flatPos.x, flatPos.y + heightOffset);

            // 천장(장애물) 충돌 체크
            Vector2 moveDir = desiredPos - lastValidPos;
            float moveDist = moveDir.magnitude;
            if (moveDist > 0.0001f)
            {
                RaycastHit2D hit = Physics2D.BoxCast(lastValidPos, jumpColliderSize, 0f, moveDir.normalized, moveDist, obstacleLayer);
                if (hit.collider != null)
                {
                    Debug.Log("점프 중 천장 부딪힘: " + hit.collider.name);
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
            Debug.Log("[H_MonsterAttack] 천장 충돌로 인한 바닥 수직 낙하 시작");
            yield return StartCoroutine(FallToGround());
        }
        else
        {
            transform.position = landPos; // 정확히 착지 지점에 정렬 (J_EnemyAttack과 동일)
        }

        // [핵심 요구사항] 최초 돌진 완료 시 기절 시간 계산
        // 플레이어를 성공적으로 가격했다면 0.5초만 기절하고, 빗나갔다면(실패했다면) 풀 타임인 stunDuration(3초) 동안 기절합니다.
        if (isInitialPounce)
        {
            float activeStunDuration = initialPounceHitPlayer ? 0.5f : stunDuration;
            Debug.Log($"[H_MonsterAttack] 최초 덮치기 돌진 완료! (플레이어 피격 성공 여부: {initialPounceHitPlayer}) -> {activeStunDuration}초 동안 기절(Stun)합니다.");
            
            // 기절용 애니메이터 매개변수가 있을 시 호출 (Animator "Stun" Trigger)
            Animator anim = GetComponent<Animator>();
            if (anim == null) anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.SetTrigger("Stun");
            }

            yield return new WaitForSeconds(activeStunDuration);
            Debug.Log("[H_MonsterAttack] 기절 해제! 이제 정상 정찰 및 J_Monster 기본 점프 공격 패턴을 시작합니다.");
        }
        else
        {
            yield return new WaitForSeconds(postDelay);
        }

        // 기절/후속딜레이가 모두 풀리고 나서 비로소 움직임(Move) 스크립트 복구
        if (enemyMove != null)
            enemyMove.enabled = true;

        isAttacking = false;

        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }

    public float fallAcceleration = 20f;   // 낙하 가속도
    public float maxFallTime = 3f;         // 안전장치

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

            transform.position = new Vector3(nextPos.x, nextPos.y, transform.position.z);

            if (foundGround)
                yield break;

            yield return null;
        }
    }

    public float groundCheckRadius = 0.3f;  // 바닥 감지 반경

    Vector2 FindValidLandingSpot(Vector2 start, Vector2 desired)
    {
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
