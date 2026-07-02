using UnityEngine;

public class S_MonsterMove : MonoBehaviour
{
    [Header("이동 설정")]
    [Tooltip("좌우 순찰 범위 (시작 지점 기준 X축 이동 반경)")]
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

    [Header("충돌 필터 설정")]
    [Tooltip("몬스터가 충돌할 플랫폼/바닥 레이어들 (이 레이어들을 제외한 플레이어, 아이템 등 모든 레이어는 무조건 관통합니다)")]
    public LayerMask platformLayers;

    private float startX;
    private bool movingRight;
    private float spawnTimer = 0f;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private NormalMonster normalMonster; // 정화 상태 연동용

    void Start()
    {
        startX = transform.position.x;
        movingRight = startMovingRight;

        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        
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

        // 플랫폼을 제외한 모든 레이어를 물리 충돌에서 제외(Exclude)시킴으로써 관통 이동 구현
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        foreach (var col in colliders)
        {
            col.excludeLayers = ~platformLayers;
        }

        // 초기 방향 스프라이트 셋업
        UpdateSpriteDirection();
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
        if (normalMonster != null && normalMonster.IsPurified) return;

        UpdateMovement();
    }

    /// <summary>
    /// 순찰 범위 내에서 좌우 이동을 처리합니다.
    /// </summary>
    private void UpdateMovement()
    {
        float currentX = transform.position.x;
        float deltaX = currentX - startX;

        // 방향 전환 감지
        if (movingRight && deltaX >= patrolRange)
        {
            movingRight = false;
            UpdateSpriteDirection();
        }
        else if (!movingRight && deltaX <= -patrolRange)
        {
            movingRight = true;
            UpdateSpriteDirection();
        }

        float moveDir = movingRight ? 1f : -1f;

        // 물리 엔진(Rigidbody2D)이 달려있으면 속도를 제어하고, 없으면 transform을 직접 이동
        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
        {
            rb.linearVelocity = new Vector2(moveDir * moveSpeed, rb.linearVelocity.y);
        }
        else
        {
            transform.Translate(Vector2.right * moveDir * moveSpeed * Time.fixedDeltaTime);
        }
    }

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

            // 자국 관리 스크립트 강제 추가
            trailObj.AddComponent<S_MonsterTrail>();
        }
    }

    /// <summary>
    /// 이동 방향에 맞춰 스프라이트 좌우 방향을 갱신합니다.
    /// </summary>
    private void UpdateSpriteDirection()
    {
        if (useSpriteFlip && spriteRenderer != null)
        {
            // 우측 이동 시 flipX = false(정방향), 좌측 이동 시 flipX = true(반전)
            spriteRenderer.flipX = !movingRight;
        }
        else
        {
            // localScale을 직접 뒤집는 전통적인 방식 지원
            Vector3 scale = transform.localScale;
            scale.x = movingRight ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
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
        // 씬 뷰에서 좌우 순찰 범위를 시각적으로 확인하기 위한 기즈모 선 드로잉
        Gizmos.color = Color.magenta;
        Vector3 start = transform.position;
        if (Application.isPlaying)
        {
            start.x = startX;
        }
        Vector3 left = start + Vector3.left * patrolRange;
        Vector3 right = start + Vector3.right * patrolRange;

        Gizmos.DrawLine(left, right);
        Gizmos.DrawWireSphere(left, 0.2f);
        Gizmos.DrawWireSphere(right, 0.2f);
    }
}
