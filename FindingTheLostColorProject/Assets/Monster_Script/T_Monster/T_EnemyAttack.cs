using UnityEngine;

public class T_EnemyAttack : MonoBehaviour
{
    // ===== 공통 =====
    public float detectRange = 8f;      // 이 범위 안에 들어오면 추격상태
    public Transform target;            // 비워두면 Player 태그로 자동 탐색
    public LayerMask targetLayer;
    public ContactHit contactHitbox;
    public MonoBehaviour moveScript;    // 이 몬스터가 쓰는 이동 스크립트 (인스펙터 연결)
    T_EnemyMove enemyMove;

    bool isAttacking = false;
    bool canAttack = true;

    enum AttackType { Melee, Ranged, Jump }
    AttackType? chosenAttack = null;    // 이번 사이클에 뽑힌 공격

    Collider2D bodyCollider;

    // ===== 근접 (기존 EnemyAttack) =====
    public float meleeWidth = 2f;
    public float meleeHeight = 1f;
    public float meleeTelegraphTime = 1f;
    public SpriteRenderer meleeTelegraphSprite;
    public float meleePostDelay = 0.5f;
    public float meleeCooldown = 2f;

    // ===== 원거리 (기존 R_EnemyAttack) =====
    public float rangedRange = 6f;
    public float rangedTelegraphTime = 1f;
    public float projectileSpeed = 4f;
    public float projectileLifetime = 3f;
    [Range(0f, 1f)] public float telegraphAnchorOffset = 0.5f;
    public SpriteRenderer rangedTelegraphSprite;
    public GameObject projectilePrefab;
    public GameObject childProjectile;
    GameObject childProjectileRuntime;
    public Transform muzzle;
    public float muzzleOrbitDistance = 2f;
    public float rangedPostDelay = 0.5f;
    public float rangedCooldown = 4f;

    // ===== 점프 (기존 J_EnemyAttack) =====
    public float jumpAttackRange = 2f;
    public float jumpLineWidth = 4f;
    public float jumpTelegraphTime = 2f;
    public float jumpDuration = 0.6f;
    public float jumpHeight = 2f;
    public float minJumpDistance = 1f;
    public LayerMask groundLayer;
    public LayerMask obstacleLayer;
    public float fallAcceleration = 20f;
    public float maxFallTime = 3f;
    public float groundCheckRadius = 0.3f;
    public float jumpPostDelay = 3f;              // 착지 후 이동 불가 딜레이 (공격 실패 시 적용)
    public float jumpHitPostDelay = 0.25f;        // 착지 후 이동 불가 딜레이 (공격 성공 시 적용, 조정 가능)
    public float jumpCooldown = 2f;
    public float safeDropDistance = 3f;

    // 점프 공격 도중 플레이어와 실제로 충돌(ContactHit)했는지 여부 -> 후딜 결정에 사용
    bool hitPlayerThisJump = false;
    void Start()
    {
        bodyCollider = GetComponent<Collider2D>();
        enemyMove = moveScript as T_EnemyMove;

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        if (meleeTelegraphSprite != null) meleeTelegraphSprite.enabled = false;
        if (rangedTelegraphSprite != null) rangedTelegraphSprite.enabled = false;

        if (childProjectile != null)
        {
            // 원본 템플릿을 그대로 복제해서 런타임 전용 인스턴스를 만듦
            childProjectileRuntime = Instantiate(
                childProjectile,
                childProjectile.transform.position,
                childProjectile.transform.rotation
            );
            childProjectileRuntime.transform.SetParent(null); // 복제본이니까 부모 분리해도 안전
            childProjectileRuntime.SetActive(false);

            childProjectile.SetActive(false); // 원본은 그냥 숨겨두기만 함
        }

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
        if (target == null) return;

        // 원거리 조준 갱신 (공격 중이 아닐 때만, 뭐가 뽑히든 상관없이 자연스럽게 유지)
        if (!isAttacking)
            UpdateMuzzlePosition();

        float dist = Vector2.Distance(transform.position, target.position);
        bool isChasing = dist <= detectRange;

        if (!isChasing)
        {
            chosenAttack = null; // 추격 풀리면 다음에 다시 새로 뽑도록 초기화
            return;
        }

        if (isAttacking || !canAttack) return;

        // 추적 시작/종료 대기 시간 동안은 공격 금지
        if (enemyMove != null && enemyMove.IsStateDelay) return;

        // 이번 사이클에 아직 공격을 안 뽑았으면 무작위로 하나 선택
        if (chosenAttack == null)
            chosenAttack = (AttackType)Random.Range(0, 3);

        switch (chosenAttack.Value)
        {
            case AttackType.Melee:
                if (IsMeleeInRange()) StartCoroutine(MeleeAttackRoutine());
                break;
            case AttackType.Ranged:
                if (IsRangedInRange()) StartCoroutine(RangedAttackRoutine());
                break;
            case AttackType.Jump:
                if (IsJumpInRange())
                {
                    Vector2 startPos = transform.position;
                    Vector2 desiredLandPos = target.position;
                    Vector2 landPos = FindValidLandingSpot(startPos, desiredLandPos, out bool isCliff);

                    // 착지 지점이 낭떠러지 때문에 잘린 경우, 점프 자체를 시도하지 않음 (텔레그래프도 안 뜨고 상태 변화 없음)
                    if (!isCliff)
                    {
                        StartCoroutine(JumpAttackRoutine(landPos));
                    }
                }
                break;
        }
    }

    // 몬스터 콜라이더 경계까지 거리 (방향 기준)
    float GetEdgeDistance(Vector2 dir)
    {
        if (bodyCollider == null) return 0f;
        Vector2 extents = bodyCollider.bounds.extents;
        float tx = extents.x / Mathf.Max(Mathf.Abs(dir.x), 0.0001f);
        float ty = extents.y / Mathf.Max(Mathf.Abs(dir.y), 0.0001f);
        return Mathf.Min(tx, ty);
    }

    void EndAttackCycle()
    {
        isAttacking = false;
        chosenAttack = null; // 다음 공격은 다시 무작위로 뽑음
    }

    // ================= 근접 =================
    bool IsMeleeInRange()
    {
        float dir = -Mathf.Sign(transform.localScale.x);
        if (dir == 0f) dir = 1f;
        float edgeOffset = bodyCollider != null ? bodyCollider.bounds.extents.x : 0f;
        Vector2 attackCenter = (Vector2)transform.position
            + new Vector2(dir * edgeOffset, 0f)
            + new Vector2(dir * meleeWidth / 2f, 0f);
        return Physics2D.OverlapBox(attackCenter, new Vector2(meleeWidth, meleeHeight), 0f, targetLayer) != null;
    }

    System.Collections.IEnumerator MeleeAttackRoutine()
    {
        isAttacking = true;
        canAttack = false;
        if (moveScript != null) moveScript.enabled = false;

        float dir = -Mathf.Sign(transform.localScale.x);
        if (dir == 0f) dir = 1f;
        float edgeOffset = bodyCollider != null ? bodyCollider.bounds.extents.x : 0f;
        Vector2 attackCenter = (Vector2)transform.position
            + new Vector2(dir * edgeOffset, 0f)
            + new Vector2(dir * meleeWidth / 2f, 0f);

        if (meleeTelegraphSprite != null)
        {
            meleeTelegraphSprite.enabled = true;
            meleeTelegraphSprite.transform.position = new Vector3(attackCenter.x, attackCenter.y, meleeTelegraphSprite.transform.position.z);

            float parentFlip = Mathf.Sign(transform.lossyScale.x);
            Vector3 ts = meleeTelegraphSprite.transform.localScale;
            meleeTelegraphSprite.transform.localScale = new Vector3(Mathf.Abs(ts.x) * parentFlip, ts.y, ts.z);

            meleeTelegraphSprite.size = new Vector2(meleeWidth, meleeHeight);
        }

        yield return new WaitForSeconds(meleeTelegraphTime);

        if (meleeTelegraphSprite != null) meleeTelegraphSprite.enabled = false;

        Collider2D hit = Physics2D.OverlapBox(attackCenter, new Vector2(meleeWidth, meleeHeight), 0f, targetLayer);
        if (hit != null)
        {
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
        if (moveScript != null) moveScript.enabled = true;

        EndAttackCycle();

        yield return new WaitForSeconds(meleeCooldown);
        canAttack = true;
    }

    // ================= 원거리 =================
    void UpdateMuzzlePosition()
    {
        if (muzzle == null || target == null) return;
        Vector2 dir = ((Vector2)target.position - (Vector2)transform.position).normalized;
        muzzle.position = (Vector2)transform.position + dir * GetEdgeDistance(dir);
    }

    bool IsRangedInRange()
    {
        return Vector2.Distance(transform.position, target.position) <= rangedRange;
    }

    System.Collections.IEnumerator RangedAttackRoutine()
    {
        isAttacking = true;
        canAttack = false;
        if (moveScript != null) moveScript.enabled = false;

        Vector2 fireDir = ((Vector2)target.position - (Vector2)transform.position).normalized;
        float angle = Mathf.Atan2(fireDir.y, fireDir.x) * Mathf.Rad2Deg;

        if (muzzle != null)
            muzzle.position = (Vector2)transform.position + fireDir * muzzleOrbitDistance;

        Vector2 startPoint = (Vector2)transform.position + fireDir * GetEdgeDistance(fireDir);

        if (rangedTelegraphSprite != null)
        {
            rangedTelegraphSprite.enabled = true;
            rangedTelegraphSprite.transform.position = startPoint + fireDir * (rangedRange * telegraphAnchorOffset);
            rangedTelegraphSprite.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            // 부모(T_Monster)가 좌우 반전되어 있으면 자식 스프라이트도 같이 뒤집히므로 보정
            float parentFlip = Mathf.Sign(transform.lossyScale.x);
            Vector3 ts = rangedTelegraphSprite.transform.localScale;
            rangedTelegraphSprite.transform.localScale = new Vector3(Mathf.Abs(ts.x) * parentFlip, ts.y, ts.z);

            rangedTelegraphSprite.size = new Vector2(rangedRange, rangedTelegraphSprite.size.y);
        }

        yield return new WaitForSeconds(rangedTelegraphTime);

        if (rangedTelegraphSprite != null) rangedTelegraphSprite.enabled = false;

        SpawnProjectile(fireDir, angle, startPoint);

        yield return new WaitForSeconds(rangedPostDelay);
        if (moveScript != null) moveScript.enabled = true;

        EndAttackCycle();

        yield return new WaitForSeconds(rangedCooldown);
        canAttack = true;
    }

    void SpawnProjectile(Vector2 fireDir, float angle, Vector2 spawnPos)
    {
        // 몬스터 원거리 발사 효과음 재생 (3D 입체 음향)
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFXAtPoint(SoundManager.SFXType.EnemyShoot, transform.position, 0.75f);
        }

        // 미리 복제해둔 런타임 투사체가 있으면 그걸 재사용 (파괴하지 않음)
        if (childProjectileRuntime != null)
        {
            FireChildProjectile(fireDir, angle, spawnPos);
            return;
        }

        GameObject proj;
        if (projectilePrefab != null)
        {
            proj = Instantiate(projectilePrefab, spawnPos, Quaternion.Euler(0f, 0f, angle));
        }
        else
        {
            proj = new GameObject("Projectile_Temp");
            proj.transform.position = spawnPos;
            proj.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            SpriteRenderer sr = proj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateTempCircleSprite();
            sr.color = Color.red;
            proj.transform.localScale = Vector3.one * 0.3f;

            CircleCollider2D col = proj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
        }

        Rigidbody2D rb = proj.GetComponent<Rigidbody2D>();
        if (rb == null) rb = proj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = fireDir * projectileSpeed;

        Projectile pb = proj.GetComponent<Projectile>();
        if (pb == null) pb = proj.AddComponent<Projectile>();
        pb.targetLayer = targetLayer;
        pb.lifetime = projectileLifetime;
    }

    // ===== 자식 투사체 재사용 로직 (파괴하지 않고 숨김/재표시) =====
    void FireChildProjectile(Vector2 fireDir, float angle, Vector2 spawnPos)
    {
        Projectile pb = childProjectileRuntime.GetComponent<Projectile>();
        if (pb == null) pb = childProjectileRuntime.AddComponent<Projectile>();
        pb.targetLayer = targetLayer;
        pb.lifetime = projectileLifetime;
        pb.reusable = true;
        pb.homeParent = transform; 

        childProjectileRuntime.transform.SetParent(null);

        childProjectileRuntime.transform.position = spawnPos;
        childProjectileRuntime.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        childProjectileRuntime.SetActive(true);

        Rigidbody2D rb = childProjectileRuntime.GetComponent<Rigidbody2D>();
        if (rb == null) rb = childProjectileRuntime.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = fireDir * projectileSpeed;

        pb.ResetForFire();
    }

    Sprite CreateTempCircleSprite()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                tex.SetPixel(x, y, d <= size / 2f ? Color.white : Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // ================= 점프 =================
    bool IsJumpInRange()
    {
        float h = Mathf.Abs(target.position.x - transform.position.x);
        float v = Mathf.Abs(target.position.y - transform.position.y);
        return h <= jumpLineWidth && v <= jumpAttackRange;
    }

    System.Collections.IEnumerator JumpAttackRoutine(Vector2 landPos)
    {
        Vector2 jumpColliderSize = bodyCollider != null ? bodyCollider.bounds.size * 0.95f : Vector2.one * 0.5f;

        isAttacking = true;
        canAttack = false;
        hitPlayerThisJump = false; // 새 점프 시작 시 충돌 기록 초기화
        if (moveScript != null) moveScript.enabled = false;

        Vector2 startPos = transform.position;

        // 낭떠러지가 아니어도 착지 지점이 너무 가까우면(제자리 수준) 안전장치로 취소
        if (Vector2.Distance(startPos, landPos) < minJumpDistance)
        {
            isAttacking = false;
            canAttack = true;
            if (moveScript != null) moveScript.enabled = true;
            EndAttackCycle();
            yield break;
        }

        yield return new WaitForSeconds(jumpTelegraphTime);

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
        // 성공: jumpHitPostDelay(기본 0.25초, 조정 가능) / 실패: jumpPostDelay(기본 3초)
        float actualPostDelay = hitPlayerThisJump ? jumpHitPostDelay : jumpPostDelay;
        yield return new WaitForSeconds(actualPostDelay);

        if (moveScript != null) moveScript.enabled = true;

        EndAttackCycle();

        yield return new WaitForSeconds(jumpCooldown);
        canAttack = true;
    }

    System.Collections.IEnumerator FallToGround()
    {
        float footOffset = bodyCollider != null ? bodyCollider.bounds.extents.y : 0.5f;

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
                transform.position = new Vector3(transform.position.x, groundHit.point.y + footOffset, transform.position.z);
                yield break;
            }

            transform.position += new Vector3(0f, -moveDist, 0f);
            yield return null;
        }
    }

    // isCliff: 경로 중간에 (safeDropDistance 안에서도) 바닥을 전혀 못 찾거나,
    // 시작 지점보다 착지 지점이 safeDropDistance 이상 훨씬 낮아서 (진짜 낭떠러지로 추정되어) 취소된 경우 true
    Vector2 FindValidLandingSpot(Vector2 start, Vector2 desired, out bool isCliff)
    {
        isCliff = false;

        float footOffset = bodyCollider != null ? bodyCollider.bounds.extents.y : 0.5f;

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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, rangedRange);
        Gizmos.DrawWireCube(transform.position, new Vector3(jumpLineWidth * 2f, jumpAttackRange * 2f, 0.1f));
    }


}