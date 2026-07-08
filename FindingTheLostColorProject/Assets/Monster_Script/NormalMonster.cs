using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody2D))]
public class NormalMonster : MonoBehaviour
{
    [Header("Monster Health Settings")]
    [Tooltip("최대 체력 (정화를 완료하기 위한 타겟 수치, 기본값: 5)")]
    public float maxHealth = 5f;

    [Tooltip("현재 체력 (0으로 스폰되어 maxHealth까지 채워야 정화됨)")]
    public float currentHealth = 0f;

    [Header("Attack Settings")]
    [Tooltip("플레이어와 부딪혔을 때 입히는 피해량 (기본값: 0.5)")]
    public float attackDamage = 0.5f;

    [Tooltip("피해를 입히는 주기/쿨타임 (초, 기본값: 1.0초)")]
    public float attackCooldown = 1f;

    [Header("Purification Animation")]
    [Tooltip("정화 완료 시 재생할 Animator Trigger 이름 (기본값: Purified)")]
    public string purifiedTriggerName = "Purified";

    [Header("HIT! 폰트 설정 (TextMeshPro 전용)")]
    [Tooltip("체력 회복 시 뜨는 HIT! 텍스트의 TMPro 폰트 에셋 (드래그 앤 드롭 가능)")]
    public TMP_FontAsset hitTextFont;

    [Header("Color Fade Settings (물감 칠해지는 색상 연출)")]
    [Tooltip("시작 시 (체력이 0일 때) 몬스터의 색상 (어둡거나 색이 빠진 톤)")]
    public Color startColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [Tooltip("정화 완료 시 (체력이 100%일 때) 몬스터의 색상 (원본 풀 컬러)")]
    public Color targetColor = Color.white;

    private SpriteRenderer[] allSpriteRenderers;
    private Rigidbody2D rb;
    private Animator animator;
    private bool isPurified = false;
    private float lastAttackTime = 0f;

    public bool IsPurified => isPurified;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = GetComponentInParent<Rigidbody2D>();
        if (rb == null) rb = GetComponentInChildren<Rigidbody2D>();

        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        // 요구사항: 스폰될 때 현재 체력을 0으로 시작
        currentHealth = 0f;

        // 비주얼 색상 초기화 (어두운 무채색 톤)
        UpdateVisualColor();
    }

    /// <summary>
    /// 플레이어의 붓질 등을 통해 체력(물감)을 채우는 함수
    /// </summary>
    /// <param name="amount">회복할 양</param>
    public void Heal(float amount)
    {
        if (isPurified) return;

        // H_Monster인 경우 잠복(Ambush) 중이면 정화(피해) 무시 (무적 상태)
        H_MonsterMove hMove = GetComponent<H_MonsterMove>();
        if (hMove == null) hMove = GetComponentInParent<H_MonsterMove>();
        if (hMove != null && hMove.IsAmbushed) return;

        // 1만큼 채워지는 구간 감지
        int oldIntHealth = Mathf.FloorToInt(currentHealth);

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        int newIntHealth = Mathf.FloorToInt(currentHealth);

        // 체력의 정수 부분이 올라갈 때마다 (1 회복할 때마다) HIT! 텍스트 생성
        if (newIntHealth > oldIntHealth)
        {
            for (int i = oldIntHealth + 1; i <= newIntHealth; i++)
            {
                SpawnHitText();
            }
        }

        // 체력 상태에 따라 스프라이트 색상을 부드럽게 칠함 (Lerp)
        UpdateVisualColor();

        // 정화 완료 판정
        if (currentHealth >= maxHealth)
        {
            Purify();
        }
    }

    /// <summary>
    /// 체력 상태에 맞추어 몬스터 및 자식/부모 오브젝트 전체의 색을 실시간으로 채워주는 연출 함수
    /// (총알이나 공격 이펙트, 경고 영역 등은 채색 대상에서 안전하게 제외합니다.)
    /// </summary>
    private void UpdateVisualColor()
    {
        float ratio = currentHealth / maxHealth;
        Color currentColor = Color.Lerp(startColor, targetColor, ratio);

        // 1. 본체 SpriteRenderer 색상 변경
        SpriteRenderer srSelf = GetComponent<SpriteRenderer>();
        if (srSelf != null) srSelf.color = currentColor;

        // 제외할 공격 관련 키워드 (총알, 이펙트, 경고 이미지 등)
        string[] ignoreKeywords = { "telegraph", "projectile", "bullet", "shot", "warn", "indicator", "attack", "effect", "danger", "marker" };

        // 2. 모든 자식 SpriteRenderer 색상 변경 (단, 제외 대상은 변경하지 않음)
        SpriteRenderer[] childSRs = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in childSRs)
        {
            if (sr == srSelf) continue;

            string fullPath = GetGameObjectPath(sr.gameObject).ToLower();
            bool shouldIgnore = false;
            foreach (var keyword in ignoreKeywords)
            {
                if (fullPath.Contains(keyword))
                {
                    shouldIgnore = true;
                    break;
                }
            }

            if (!shouldIgnore)
            {
                sr.color = currentColor;
            }
        }

        // 3. 부모 SpriteRenderer 색상 변경 (단, 제외 대상은 변경하지 않음)
        SpriteRenderer[] parentSRs = GetComponentsInParent<SpriteRenderer>();
        foreach (var sr in parentSRs)
        {
            if (sr == srSelf) continue;

            string fullPath = GetGameObjectPath(sr.gameObject).ToLower();
            bool shouldIgnore = false;
            foreach (var keyword in ignoreKeywords)
            {
                if (fullPath.Contains(keyword))
                {
                    shouldIgnore = true;
                    break;
                }
            }

            if (!shouldIgnore)
            {
                sr.color = currentColor;
            }
        }
    }

    /// <summary>
    /// 게임 오브젝트의 전체 계층 구조 경로(Path)를 반환하는 헬퍼 함수
    /// </summary>
    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform current = obj.transform;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }
        return path;
    }

    /// <summary>
    /// 1 회복할 때마다 몬스터 주변에 TextMeshPro를 이용한 HIT! 문양을 띄워주는 연출 함수
    /// </summary>
    private void SpawnHitText()
    {
        // 1. 새로운 게임 오브젝트 생성
        GameObject hitTextObj = new GameObject("HitText_Popup");
        
        // 몬스터 머리 위 주변 랜덤 위치에 띄움
        Vector3 spawnOffset = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.6f, 1.2f), 0f);
        hitTextObj.transform.position = transform.position + spawnOffset;
        
        // 2. TextMeshPro 추가 및 세팅
        TextMeshPro tmp = hitTextObj.AddComponent<TextMeshPro>();
        tmp.text = "HIT!";
        tmp.fontSize = 4.5f; // TextMeshPro용 폰트 사이즈
        tmp.color = new Color(1f, 0.7f, 0f); // 선명한 주황/노란색 계열
        tmp.alignment = TextAlignmentOptions.Center;

        // 폰트 설정 (TextMeshPro 폰트 에셋 지정)
        if (hitTextFont != null)
        {
            tmp.font = hitTextFont;
        }
        
        // 3. 렌더 순서 설정 (UI 레이어로 설정하여 화면 최상단 출력)
        MeshRenderer meshRenderer = hitTextObj.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingLayerName = "UI";
            meshRenderer.sortingOrder = 150;
        }

        // 4. 서서히 위로 떠오르며 사라지는 제어용 FloatingText 컴포넌트 장착
        FloatingText floatingScript = hitTextObj.AddComponent<FloatingText>();
        floatingScript.Setup(new Color(1f, 0.7f, 0f), 0.8f);
    }

    /// <summary>
    /// 체력이 가득 차서 고양이가 정화되었을 때의 처리
    /// </summary>
    private void Purify()
    {
        isPurified = true;
        currentHealth = maxHealth;
        UpdateVisualColor(); // 완전히 원래 색으로 변경

        Debug.Log($"[NormalMonster] {gameObject.name} 정화 완료!");

        // 정화 카운트 매니저가 존재하면 카운트 증가 알림 (즉시 UI에 반영)
        if (PurificationManager.Instance != null)
        {
            PurificationManager.Instance.OnCatPurified(this);
        }

        // 1. 정화 애니메이션 트리거 재생
        if (animator != null)
        {
            animator.SetTrigger(purifiedTriggerName);
        }

        // 2. 플레이어 콜라이더와의 충돌만 무시 (바닥 플랫폼과는 계속 충돌하여 아래로 수직 착지 가능)
        PlayerHealth player = FindFirstObjectByType<PlayerHealth>();
        if (player != null)
        {
            Collider2D playerCol = player.GetComponent<Collider2D>();
            Collider2D[] monsterCols = GetComponentsInChildren<Collider2D>(true);
            foreach (var mCol in monsterCols)
            {
                if (playerCol != null && mCol != null)
                {
                    Physics2D.IgnoreCollision(mCol, playerCol, true);
                }
            }
        }

        // 3. 물리 움직임 멈춤 및 중력 활성화 (수직 낙하 착지)
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic; // 물리 중력을 강제로 다시 켬!
            rb.gravityScale = 1.5f;                // 바닥에 빠르게 안착하도록 가중치 부여
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            rb.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        }

        // 4. 본체와 자식/부모에 붙어있는 모든 움직임/AI 스크립트 비활성화 및 코루틴 정지
        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script != this)
            {
                script.StopAllCoroutines();
                script.enabled = false;
            }
        }

        MonoBehaviour[] parentScripts = GetComponentsInParent<MonoBehaviour>();
        foreach (var script in parentScripts)
        {
            if (script != this && !(script is NormalMonster))
            {
                script.StopAllCoroutines();
                script.enabled = false;
            }
        }

        MonoBehaviour[] childScripts = GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var script in childScripts)
        {
            if (script != this && !(script is NormalMonster))
            {
                script.StopAllCoroutines();
                script.enabled = false;
            }
        }

        // 5. 공격 범위 경고/범위 이펙트 등 활성화된 피드백 자식 오브젝트들을 모두 검색하여 소멸/비활성화 처리
        string[] feedbackKeywords = { "warn", "indicator", "range", "area", "attack", "red", "effect", "danger", "marker", "telegraph" };
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        foreach (var child in allChildren)
        {
            if (child == transform) continue; // 자기 자신 제외
            
            string lowerName = child.name.ToLower();
            foreach (var keyword in feedbackKeywords)
            {
                if (lowerName.Contains(keyword))
                {
                    child.gameObject.SetActive(false);
                    break;
                }
            }
        }

        // 6. 각 공격 스크립트(F_EnemyAttack, EnemyAttack, R_EnemyAttack)의 telegraphSprite 컴포넌트 및 게임오브젝트를 강제로 직접 비활성화
        var fAttack = GetComponent<F_EnemyAttack>();
        if (fAttack == null) fAttack = GetComponentInChildren<F_EnemyAttack>();
        if (fAttack == null) fAttack = GetComponentInParent<F_EnemyAttack>();
        if (fAttack != null && fAttack.telegraphSprite != null)
        {
            fAttack.telegraphSprite.enabled = false;
            fAttack.telegraphSprite.gameObject.SetActive(false);
        }

        var normalAttack = GetComponent<EnemyAttack>();
        if (normalAttack == null) normalAttack = GetComponentInChildren<EnemyAttack>();
        if (normalAttack == null) normalAttack = GetComponentInParent<EnemyAttack>();
        if (normalAttack != null && normalAttack.telegraphSprite != null)
        {
            normalAttack.telegraphSprite.enabled = false;
            normalAttack.telegraphSprite.gameObject.SetActive(false);
        }

        var rAttack = GetComponent<R_EnemyAttack>();
        if (rAttack == null) rAttack = GetComponentInChildren<R_EnemyAttack>();
        if (rAttack == null) rAttack = GetComponentInParent<R_EnemyAttack>();
        if (rAttack != null && rAttack.telegraphSprite != null)
        {
            rAttack.telegraphSprite.enabled = false;
            rAttack.telegraphSprite.gameObject.SetActive(false);
        }
    }

    #region 플레이어 피격 데미지 판정 (친구 커밋 복구 대행)
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (isPurified) return;
        // 보스는 BossAttack의 별도 contactHitbox로만 접촉 피해를 관리하므로,
        // 여기서 중복으로 데미지를 주지 않도록 제외 (0.5 데미지가 섞여 들어오는 원인이었음)
        if (GetComponent<BossAttack>() != null) return;

        PlayerHealth player = collision.gameObject.GetComponent<PlayerHealth>();
        if (player == null) player = collision.gameObject.GetComponentInParent<PlayerHealth>();

        if (player != null && Time.time - lastAttackTime >= attackCooldown)
        {
            player.TakeDamage(attackDamage);
            lastAttackTime = Time.time;
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (isPurified) return;
        if (GetComponent<BossAttack>() != null) return;

        PlayerHealth player = collision.GetComponent<PlayerHealth>();
        if (player == null) player = collision.GetComponentInParent<PlayerHealth>();

        if (player != null && Time.time - lastAttackTime >= attackCooldown)
        {
            player.TakeDamage(attackDamage);
            lastAttackTime = Time.time;
        }
    }
    #endregion
}

/// <summary>
/// TextMeshPro를 이용해 위로 둥실 떠오르며 사라지는 텍스트 컴포넌트
/// </summary>
public class FloatingText : MonoBehaviour
{
    private float duration = 0.8f;
    private float speed = 1.2f;
    private TextMeshPro textMesh;
    private Color baseColor;
    private float elapsedTime = 0f;

    public void Setup(Color color, float dur)
    {
        textMesh = GetComponent<TextMeshPro>();
        baseColor = color;
        duration = dur;
    }

    void Update()
    {
        elapsedTime += Time.deltaTime;
        float t = elapsedTime / duration;

        // 천천히 위로 이동
        transform.Translate(Vector3.up * speed * Time.deltaTime);

        if (textMesh != null)
        {
            // 천천히 투명해지도록 보간
            textMesh.color = Color.Lerp(baseColor, new Color(baseColor.r, baseColor.g, baseColor.b, 0f), t);
        }

        // 수명 만료 시 파괴
        if (elapsedTime >= duration)
        {
            Destroy(gameObject);
        }
    }
    private float lastAttackTime = 0f;

    private void OnTriggerStay2D(Collider2D other)
    {
        NormalMonster normal = GetComponent<NormalMonster>();
        if (normal == null || normal.IsPurified) return;

        if (!other.CompareTag("Player")) return;

        PlayerHealth player = other.GetComponent<PlayerHealth>();
        if (player == null)
            player = other.GetComponentInParent<PlayerHealth>();

        if (player != null && Time.time - lastAttackTime >= normal.attackCooldown)
        {
            player.TakeDamage(normal.attackDamage);
            lastAttackTime = Time.time;
        }
    }
}
