using UnityEngine;

public class S_MonsterMove : MonoBehaviour
{
    [Header("Move")]
    [Tooltip("현재 위치 기준 좌우 이동 거리")]
    public float patrolRange = 3f;

    [Tooltip("이동 속도")]
    public float moveSpeed = 2f;

    [Tooltip("시작 시 오른쪽으로 먼저 이동할지 여부")]
    public bool startMovingRight = true;

    [Header("자국 생성 설정")]
    [Tooltip("바닥에 남길 자국(Trail) 프리팹 (S_MonsterTrail 컴포넌트가 있어야 함)")]
    public GameObject trailPrefab;

    [Tooltip("자국이 남겨지는 시간 주기 (초, 기본값: 0.3초마다 하나씩)")]
    public float spawnInterval = 0.3f;

    [Header("컴포넌트 및 외형 설정")]
    [Tooltip("이동 방향에 따라 SpriteRenderer의 FlipX를 제어할지 여부")]
    public bool useSpriteFlip = true;

    [Tooltip("스프라이트 기본 에셋이 왼쪽을 바라보고 있어서 문워크를 하는 경우 체크를 켜 줍니다.")]
    public bool invertSpriteDirection = false;

    [Header("충돌 필터 설정")]
    [Tooltip("몬스터가 충돌할 플랫폼/바닥 레이어들 (이 레이어들을 제외한 플레이어, 아이템 등 모든 레이어는 무조건 관통합니다)")]
    public LayerMask platformLayers;

    [Header("플레이어 접촉 피해 설정")]
    [Tooltip("플레이어 접촉 판정 박스 크기 배율 (실제 콜라이더 크기에 곱해서 사용, 기본값 1 = 콜라이더와 동일한 크기)")]
    public float playerContactBoxScale = 1f;
    private float lastAttackTime = 0f;

    [Header("방향 전환 설정")]
    [Tooltip("방향을 바꾸기 전 멈추는 시간 (초)")]
    public float turnPauseDuration = 0.5f;

    private float startX;
    private bool movingRight;
    private float spawnTimer = 0f;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private NormalMonster normalMonster; // 정화 상태 연동용
    private bool isPausedForTurn = false; // 방향 전환 전 정지 중인지 여부
    private Animator animator; // 걷기 애니메이션 제어용

    [Header("낭떠러지 감지 설정")]
    [Tooltip("발밑에 땅이 있는지 검사하는 레이 길이")]
    public float groundCheckDistance = 2f;

    private Collider2D selfColCached; // 매 프레임 GetComponent 호출 방지용 캐시


    void Start()
    {
        startX = transform.position.x;

        movingRight = startMovingRight;
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        normalMonster = GetComponent<NormalMonster>();
        if (normalMonster == null) normalMonster = GetComponentInParent<NormalMonster>();

        // 플랫폼 레이어 자동 기본값 할당 (비어있을 시 Ground/Platform 레이어 검색)
        if (platformLayers.value == 0)
        {
            int ground = LayerMask.NameToLayer("Ground");
            int platform = LayerMask.NameToLayer("Platform");
            int def = LayerMask.NameToLayer("Default");

            int mask = 0;
            if (ground != -1) mask |= (1 << ground);
            if (platform != -1) mask |= (1 << platform);
            if (mask == 0 && def != -1) mask |= (1 << def);

            platformLayers = mask;
        }

        

        selfColCached = GetComponent<Collider2D>();
        if (selfColCached == null) selfColCached = GetComponentInChildren<Collider2D>();

        // 초기 방향 스프라이트 셋업
        UpdateSpriteDirection();

        int playerLayer = LayerMask.NameToLayer("Player");
        int monsterLayer = gameObject.layer;

        if (playerLayer != -1)
        {
            Physics2D.IgnoreLayerCollision(monsterLayer, playerLayer, true);
        }
    }

    void Update()
    {
        // 고양이 몬스터가 정화 완료(Saved)된 상태라면 더 이상 이동하거나 자국을 남기지 않음
        if (normalMonster != null && normalMonster.IsPurified) return;

        // 1. 자국 생성 주기 타이머 계산
        spawnTimer += Time.deltaTime;
        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            SpawnTrail();
        }
    }

    void FixedUpdate()
    {
        // 정화 완료 시 물리 이동 정지
        if (normalMonster != null && normalMonster.IsPurified)
        {
            if (animator != null)
                animator.SetBool("IsWalking", false);
            return;
        }

        UpdateMovement();
        CheckPlayerContactDamage();
    }

   
    // excludeLayers 설정으로 인해 물리 충돌/트리거 이벤트 자체가 발생하지 않으므로,
    // 별도의 오버랩 검사로 플레이어와의 접촉을 직접 확인해서 피해를 줌
    private void CheckPlayerContactDamage()
    {
        if (normalMonster != null && normalMonster.IsPurified) return;

        // 자기 자신 오브젝트에 콜라이더가 없으면 자식 오브젝트에서도 찾아봄
        // (스프라이트/콜라이더가 자식에 있는 프리팹 구조 대비)
        Collider2D selfCol = GetComponent<Collider2D>();
        if (selfCol == null) selfCol = GetComponentInChildren<Collider2D>();
        if (selfCol == null)
        {
            Debug.LogWarning(gameObject.name + ": Collider2D를 찾지 못해 플레이어 접촉 판정을 할 수 없습니다.");
            return;
        }

        // 실제 콜라이더의 월드 기준 크기와 중심을 그대로 사용 (배율만 적용)
        Vector2 boxSize = selfCol.bounds.size * playerContactBoxScale;
        Vector2 boxCenter = selfCol.bounds.center;

        // ContactFilter로 트리거 콜라이더까지 명시적으로 포함시켜, 전역 queriesHitTriggers 설정과 무관하게 항상 감지
        ContactFilter2D filter = new ContactFilter2D();
        filter.NoFilter();
        filter.useTriggers = true;

        Collider2D[] results = new Collider2D[8];
        int count = Physics2D.OverlapBox(boxCenter, boxSize, 0f, filter, results);

        Collider2D hit = null;
        for (int i = 0; i < count; i++)
        {
            if (results[i] != null && results[i].CompareTag("Player"))
            {
                hit = results[i];
                break;
            }
        }

        if (hit == null) return;

        float damage = normalMonster != null ? normalMonster.attackDamage : 0.5f;
        float cooldown = normalMonster != null ? normalMonster.attackCooldown : 1f;

        if (Time.time - lastAttackTime < cooldown) return;

        PlayerHealth player = hit.GetComponent<PlayerHealth>();
        if (player == null) player = hit.GetComponentInParent<PlayerHealth>();

        if (player != null)
        {
            player.TakeDamage(damage);
            lastAttackTime = Time.time;
        }
    }


    private void UpdateMovement()
    {
        // 방향 전환 때문에 멈춰있는 동안에는 이동을 시도하지 않음
        if (isPausedForTurn)
        {
            if (animator != null)
                animator.SetBool("IsWalking", false);

            return;
        }

        float currentX = transform.position.x;
        float deltaX = currentX - startX;
        float moveDir = movingRight ? 1f : -1f;


        

        if (movingRight && deltaX >= patrolRange)
        {
            if (animator != null)
                animator.SetBool("IsWalking", false);

            StartCoroutine(PauseThenTurn(false));
            return;
        }

        if (!movingRight && deltaX <= -patrolRange)
        {
            if (animator != null)
                animator.SetBool("IsWalking", false);

            StartCoroutine(PauseThenTurn(true));
            return;
        }

        if (animator != null)
            animator.SetBool("IsWalking", true);

        transform.position += Vector3.right * moveDir * moveSpeed * Time.deltaTime;
    }

    /// <summary>
    /// 방향 전환 전 turnPauseDuration만큼 제자리에 멈춘 뒤, 방향을 실제로 전환합니다.
    /// </summary>
    private System.Collections.IEnumerator PauseThenTurn(bool nextMovingRight)
    {
        isPausedForTurn = true;



        yield return new WaitForSeconds(turnPauseDuration);

        movingRight = nextMovingRight;
        UpdateSpriteDirection();
        isPausedForTurn = false;
    }

    [Header("자국 소멸 설정")]
    [Tooltip("자국이 옅어지기 시작하는 시점 (생성 후 경과 시간, 초)")]
    public float trailFadeStartTime = 3f;

    [Tooltip("자국이 완전히 사라지는 시점 (생성 후 경과 시간, 초 - trailFadeStartTime보다 커야 함)")]
    public float trailFullyGoneTime = 4f;

    /// <summary>
    /// 몬스터가 기어가는 길바닥에 자국을 생성합니다.
    /// </summary>
    private void SpawnTrail()
    {
        GameObject trailObj;

        // 몬스터 발 밑 쪽에 소환하기 위한 위치 보정
        Vector3 spawnPosition = transform.position;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            spawnPosition.y = col.bounds.min.y; // 캐릭터 발바닥 높이에 자국 고정
        }

        if (trailPrefab != null)
        {
            trailObj = Instantiate(trailPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            // 폴백: 인스펙터에 프리팹을 지정하지 않았을 때 자동으로 생성되는 디버그용 임시 자국 오브젝트
            trailObj = new GameObject("S_MonsterTrail_Debug");
            trailObj.transform.position = spawnPosition;

            // 시각적인 식별을 위한 반투명 자주색 스프라이트 셋업
            SpriteRenderer sr = trailObj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateDebugTrailSprite();
            sr.color = new Color(0.45f, 0.15f, 0.55f, 0.6f); // 반투명한 보라색
            sr.sortingOrder = -1; // 캐릭터 뒤에 그려지도록 설정

            // 플레이어 감지를 위한 트리거 콜라이더 부착
            BoxCollider2D boxCol = trailObj.AddComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector2(0.8f, 0.3f);
        }

        // 생성된 자국(프리팹이든 디버그용이든)을 3초부터 서서히 옅어지다가 4초에 완전히 사라지게 함
        StartCoroutine(FadeAndDestroyTrail(trailObj));
    }

    private System.Collections.IEnumerator FadeAndDestroyTrail(GameObject trailObj)
    {
        if (trailObj == null) yield break;

        SpriteRenderer sr = trailObj.GetComponent<SpriteRenderer>();
        if (sr == null) sr = trailObj.GetComponentInChildren<SpriteRenderer>();

        // 페이드 시작 전까지 대기
        yield return new WaitForSeconds(trailFadeStartTime);

        if (sr == null)
        {
            // 페이드시킬 스프라이트가 없으면 그냥 종료 시점에 파괴
            yield return new WaitForSeconds(Mathf.Max(0f, trailFullyGoneTime - trailFadeStartTime));
            if (trailObj != null) Destroy(trailObj);
            yield break;
        }

        Color startColor = sr.color;
        float fadeDuration = Mathf.Max(0.01f, trailFullyGoneTime - trailFadeStartTime);
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            if (trailObj == null) yield break; // 페이드 도중 다른 이유로 먼저 파괴된 경우 안전 종료

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0f, t);
            sr.color = c;
            yield return null;
        }

        if (trailObj != null) Destroy(trailObj);
    }

    /// <summary>
    /// 이동 방향에 맞춰 스프라이트 좌우 방향을 갱신합니다.
    /// </summary>
    private void UpdateSpriteDirection()
    {
        bool faceRight = movingRight;
        if (invertSpriteDirection)
        {
            faceRight = !movingRight;
        }

        if (useSpriteFlip && spriteRenderer != null)
        {
            spriteRenderer.flipX = !faceRight;
        }
        else
        {
            // localScale을 직접 뒤집는 방식 지원 
            Vector3 scale = transform.localScale;
            scale.x = faceRight ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
            transform.localScale = scale;
        }
    }

    /// <summary>
    /// 자국용 임시 텍스처를 런타임에 빌드합니다. (테스트용)
    /// </summary>
    private Sprite CreateDebugTrailSprite()
    {
        int width = 32;
        int height = 12;
        Texture2D tex = new Texture2D(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 중간은 칠하고 바깥쪽은 투명하게 페이드되는 타원 형태의 자국 생성
                float dx = (x - width / 2f) / (width / 2f);
                float dy = (y - height / 2f) / (height / 2f);

                if (dx * dx + dy * dy <= 1f)
                {
                    tex.SetPixel(x, y, Color.white);
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;

        Vector3 center = transform.position;

        if (Application.isPlaying)
        {
            center.x = startX;
        }

        Vector3 left = center + Vector3.left * patrolRange;
        Vector3 right = center + Vector3.right * patrolRange;

        Gizmos.DrawLine(left, right);
        Gizmos.DrawWireSphere(left, 0.2f);
        Gizmos.DrawWireSphere(right, 0.2f);
    }
}