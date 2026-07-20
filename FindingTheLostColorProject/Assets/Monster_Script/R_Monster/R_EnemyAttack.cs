using UnityEngine;

public class R_EnemyAttack : MonoBehaviour
{
    public float attackRange = 6f;       // 이 범위 안에 타겟이 들어오면 공격 시작
    public float telegraphTime = 1f;   // 경고 표시 시간
    public float projectileSpeed = 8f;   // 투사체 속도
    public float projectileLifetime = 3f;// 투사체 생존 시간 (충돌 안 해도 사라짐)
    public float attackCooldown = 2f;    // 공격 쿨타임
    [Range(0f, 1f)]
    public float telegraphAnchorOffset = 0.5f;

    public Transform target;
    public LayerMask targetLayer;

    public SpriteRenderer telegraphSprite; // 일직선 텔레그래프용 (가늘고 긴 사각형 스프라이트, 자식 오브젝트)
    public GameObject projectilePrefab;    // 비워두면 코드에서 임시 도형 생성
    GameObject projectileTemplate;

    bool isAttacking = false;
    bool canAttack = true;

    public Transform muzzle; 
    public float muzzleOrbitDistance = 1f; // 콜라이더가 없을 때 사용할 기본 거리 

    Collider2D bodyCollider;

    public float postDelay = 0.5f; // 공격 후 이동/공격 불가 딜레이
    R_EnemyMove enemyMove;

    public ContactHit contactHitbox; // 자식 오브젝트(ContactHitbox)의 ContactRelay 연결
    Animator animator; // 자식 오브젝트에 있는 경우도 대비해서 GetComponentInChildren 사용

    void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        if (telegraphSprite != null)
            telegraphSprite.enabled = false;
        enemyMove = GetComponent<R_EnemyMove>();
        bodyCollider = GetComponent<Collider2D>();

        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (projectilePrefab != null)
        {
            projectileTemplate = Instantiate(projectilePrefab, projectilePrefab.transform.position, projectilePrefab.transform.rotation);
            projectileTemplate.transform.SetParent(null);
            projectileTemplate.SetActive(false);
            projectilePrefab.SetActive(false); // 원본은 그냥 숨겨만 두고 다시는 건드리지 않음
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
        }
    }

    void Update()
    {
        if (!isAttacking)
            UpdateMuzzlePosition();

        if (isAttacking || !canAttack || target == null) return;

        // 추적 시작/종료 연출 중에는 공격 금지
        if (enemyMove != null && enemyMove.IsStateDelay) return;

        float distance = Vector2.Distance(transform.position, target.position);
        if (distance <= attackRange)
        {
            StartCoroutine(AttackRoutine());
        }
    }

    void UpdateMuzzlePosition()
    {
        if (muzzle == null || target == null) return;

        Vector2 dir = ((Vector2)target.position - (Vector2)transform.position).normalized;
        muzzle.position = (Vector2)transform.position + dir * GetEdgeDistance(dir);
    }

    // 몬스터 콜라이더의 경계까지 거리를 방향(dir) 기준으로 계산
    float GetEdgeDistance(Vector2 dir)
    {
        if (bodyCollider == null) return muzzleOrbitDistance;

        Vector2 extents = bodyCollider.bounds.extents; // 콜라이더 절반 크기 (x, y)

        float tx = extents.x / Mathf.Max(Mathf.Abs(dir.x), 0.0001f);
        float ty = extents.y / Mathf.Max(Mathf.Abs(dir.y), 0.0001f);

        return Mathf.Min(tx, ty); // 더 먼저 닿는 쪽(모서리)까지의 거리
    }

    System.Collections.IEnumerator AttackRoutine()
    {
        isAttacking = true;
        canAttack = false;

        if (animator != null)
        {
            animator.SetBool("IsWalking", false); // 공격 시작 시 걷기 상태를 확실히 꺼서 Walk 애니메이션과 충돌 방지
            animator.SetBool("IsAttacking", true);
        }

        if (enemyMove != null)
            enemyMove.enabled = false; // 텔레그래프 시작과 동시에 이동 정지

        // 발사 시점 타겟 방향 고정
        Vector2 fireDir = ((Vector2)target.position - (Vector2)transform.position).normalized;
        float angle = Mathf.Atan2(fireDir.y, fireDir.x) * Mathf.Rad2Deg;

        if (muzzle != null)
            muzzle.position = (Vector2)transform.position + fireDir * muzzleOrbitDistance;


        Vector2 startPoint = (Vector2)transform.position + fireDir * GetEdgeDistance(fireDir);

        if (telegraphSprite != null)
        {
            telegraphSprite.enabled = true;
            telegraphSprite.transform.position = startPoint + fireDir * (attackRange * telegraphAnchorOffset);
            telegraphSprite.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            telegraphSprite.size = new Vector2(attackRange, telegraphSprite.size.y);
        }

        yield return new WaitForSeconds(telegraphTime); // 1초 대기 (멈춰있는 상태)

        if (muzzle != null)
            muzzle.position = (Vector2)transform.position + fireDir * GetEdgeDistance(fireDir);

        if (telegraphSprite != null)
            telegraphSprite.enabled = false;

        // 발사 직전 현재 몬스터 위치에서 발사 위치를 다시 계산
        startPoint = (Vector2)transform.position + fireDir * GetEdgeDistance(fireDir);

        // 투사체 생성
        SpawnProjectile(fireDir, angle, startPoint);

        // 후딜레이: 이동도 공격도 불가
        yield return new WaitForSeconds(postDelay); // 0.5초 대기

        if (animator != null) animator.SetBool("IsAttacking", false);

        if (enemyMove != null)
            enemyMove.enabled = true; // 이동 재개

        isAttacking = false; // 이 시점부터 새 공격 판정은 가능하지만 canAttack은 아직 false

        // 쿨타임
        yield return new WaitForSeconds(attackCooldown); // 4초 대기
        canAttack = true;
    }

    void SpawnProjectile(Vector2 fireDir, float angle, Vector2 spawnPos)
    {
        // 몬스터 원거리 발사 효과음 재생 (3D 입체 음향)
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFXAtPoint(SoundManager.SFXType.EnemyShoot, transform.position, 0.75f);
        }
        GameObject proj;
        if (projectileTemplate != null)
        {
            proj = Instantiate(projectileTemplate, spawnPos, Quaternion.Euler(0f, 0f, angle));
            proj.SetActive(true); // 템플릿이 꺼져있어도 복제본은 반드시 켜서 생성
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

        // 왼쪽으로 발사할 때만 X축을 뒤집음 (오른쪽 발사는 원본 그대로 유지)
        if (fireDir.x < 0f)
        {
            Vector3 scale = proj.transform.localScale;
            scale.x = -Mathf.Abs(scale.x);
            proj.transform.localScale = scale;
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

    // 임시 원형 스프라이트 생성 (테스트용)
    Sprite CreateTempCircleSprite()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                tex.SetPixel(x, y, dist <= size / 2f ? Color.white : Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}