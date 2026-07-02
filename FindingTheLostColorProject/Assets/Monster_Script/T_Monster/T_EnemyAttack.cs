using UnityEngine;

public class T_EnemyAttack : MonoBehaviour
{
    // ===== 공통 =====
    public float detectRange = 8f;      // 이 범위 안에 들어오면 추격상태
    public Transform target;            // 비워두면 Player 태그로 자동 탐색
    public LayerMask targetLayer;
    public ContactRelay contactHitbox;
    public MonoBehaviour moveScript;    // 이 몬스터가 쓰는 이동 스크립트 (인스펙터 연결)


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
    public float jumpPostDelay = 3f;
    public float jumpCooldown = 2f;

    void Start()
    {
        bodyCollider = GetComponent<Collider2D>();

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
                if (IsJumpInRange()) StartCoroutine(JumpAttackRoutine());
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
        pb.homeParent = transform; // 재사용 시 되돌아갈 부모 (복제본은 이미 부모 없음)

        // 복제본이라 이미 부모가 없는 상태 — 위치/회전만 갱신
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

    System.Collections.IEnumerator JumpAttackRoutine()
    {
        Vector2 jumpColliderSize = bodyCollider != null ? bodyCollider.bounds.size * 0.95f : Vector2.one * 0.5f;

        isAttacking = true;
        canAttack = false;
        if (moveScript != null) moveScript.enabled = false;

        Vector2 startPos = transform.position;
        Vector2 desiredLandPos = target.position;
        Vector2 landPos = FindValidLandingSpot(startPos, desiredLandPos);

        if (Vector2.Distance(startPos, landPos) < minJumpDistance)
        {
            if (moveScript != null) moveScript.enabled = true;
            EndAttackCycle();
            yield return new WaitForSeconds(jumpCooldown);
            canAttack = true;
            yield break;
        }
        yield return new WaitForSeconds(jumpTelegraphTime);

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
                    desiredPos = hit.point - moveDir.normalized * 0.05f;
                    hitCeiling = true;
                }
            }

            lastValidPos = desiredPos;
            transform.position = new Vector3(desiredPos.x, desiredPos.y, transform.position.z);
            yield return null;

            if (hitCeiling) break;
        }

        if (hitCeiling)
            yield return StartCoroutine(FallToGround());
        else
            transform.position = landPos;

        yield return new WaitForSeconds(jumpPostDelay);
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

            Vector2 nextPos = (Vector2)transform.position + Vector2.down * fallSpeed * Time.deltaTime;
            Vector2 groundCheckPos = nextPos + Vector2.down * footOffset;
            bool foundGround = Physics2D.OverlapCircle(groundCheckPos, groundCheckRadius, groundLayer) != null;

            transform.position = new Vector3(nextPos.x, nextPos.y, transform.position.z);

            if (foundGround) yield break;
            yield return null;
        }
    }

    Vector2 FindValidLandingSpot(Vector2 start, Vector2 desired)
    {
        float footOffset = bodyCollider != null ? bodyCollider.bounds.extents.y : 0.5f;

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

            if (foundGround) lastGroundPos = checkPos;
            else return lastGroundPos;
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