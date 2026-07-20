using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
public class ColoringBridge : MonoBehaviour
{
    [Header("채색 및 정화 설정")]
    [Tooltip("최대 체력 (가득 채워야 다리가 활성화됨)")]
    public float maxHealth = 3f;

    [Tooltip("현재 체력")]
    public float currentHealth = 0f;

    [Header("색상 변경 설정")]
    [Tooltip("채색 전 (색이 빠지고 투명한 상태)의 색상")]
    public Color startColor = new Color(1f, 1f, 1f, 0.2f);

    [Tooltip("채색 완료 (선명하고 단단한 상태)의 색상")]
    public Color targetColor = Color.white;

    [Header("Sprite Settings (신규)")]
    [Tooltip("기본 미완성 상태의 스프라이트")]
    public Sprite defaultSprite;
    [Tooltip("정화 완료(완성) 상태의 스프라이트")]
    public Sprite purifiedSprite;
    [Tooltip("스프라이트를 변경할 대상 SpriteRenderer (비워두면 본인 컴포넌트 자동 캐싱)")]
    public SpriteRenderer targetSpriteRenderer;

    [Header("밟을 때 눌림 연출 설정")]
    [Tooltip("플레이어가 다리를 밟았을 때 내려갈 거리 (Y축 기준, 예: 0.2f)")]
    public float stepDownDistance = 0.2f;
    [Tooltip("다리가 복귀하거나 내려앉는 이동 보간 속도 (기본값: 10.0f)")]
    public float stepLerpSpeed = 10f;

    [Header("인접 다리 블록 연동 설정 (전이 기믹)")]
    [Tooltip("인접해서 에너지를 공유할 다리 블록(타일)들")]
    public ColoringBridge[] adjacentBridges;

    [Header("HIT! 텍스트 폰트")]
    [Tooltip("회복할 때 팝업되는 HIT! 텍스트 폰트 (비워두면 플레이어 폰트 자동 상속)")]
    public Font hitTextFont;

    private SpriteRenderer[] allSpriteRenderers;
    private Collider2D bridgeCollider;
    private bool isPurified = false;

    private Vector3 originalPosition;      // 다리 최초 정렬 위치
    private bool isPlayerStepping = false; // 현재 플레이어가 다리 위를 밟고 있는지 여부

    public bool IsPurified => isPurified;

    private int originalLayer;

    void Start()
    {
        allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        bridgeCollider = GetComponent<Collider2D>();
        originalPosition = transform.position; // 최초 고정 원점 위치 백업
        originalLayer = gameObject.layer;

        // 미완성 상태일 때는 몬스터가 딛지 못하도록 레이어 강제 임시 격리 (Platform 레이어인 경우 예방)
        int platformLayer = LayerMask.NameToLayer("Platform");
        if (gameObject.layer == platformLayer)
        {
            SetLayerRecursively(gameObject, LayerMask.NameToLayer("Default"));
        }

        if (targetSpriteRenderer == null)
        {
            targetSpriteRenderer = GetComponent<SpriteRenderer>();
            if (targetSpriteRenderer == null)
            {
                targetSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        // [자동 쌍방향 연동] 내가 가리키는 상대방들의 인접 목록에도 자동으로 나 자신을 등록시킵니다.
        if (adjacentBridges != null)
        {
            foreach (var adj in adjacentBridges)
            {
                if (adj != null)
                {
                    adj.RegisterAdjacentBridge(this); // 상대방 타일에 나를 역으로 자동 링크
                }
            }
        }

        // 시작 시 색상 초기화 및 충돌 판정 비활성화 (IsTrigger = true 상태로 통과하게 함)
        UpdateVisualColor();
        UpdateSprite(); // 초기 스프라이트 셋업
        UpdateMinimapIcon(); // 미니맵 아이콘 초기 색상 셋업
        SetSolidState(false);
    }

    /// <summary>
    /// 게임오브젝트와 모든 자식 오브젝트의 레이어를 재귀적으로 변경하는 헬퍼 메서드
    /// </summary>
    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    void Update()
    {
        // 정화된 단단한 상태에서 플레이어가 밟고 있다면 Y축으로 내려앉고, 발을 떼면 원점 복구
        if (isPurified)
        {
            // 물리 충돌 이벤트 대신 오버랩 박스로 부드럽고 떨림 없는 밟기 감지
            isPlayerStepping = CheckPlayerOnTop();
        }
        else
        {
            isPlayerStepping = false;
        }

        Vector3 targetPos = originalPosition;
        if (isPurified && isPlayerStepping)
        {
            targetPos.y = originalPosition.y - stepDownDistance;
        }

        // 콜라이더와 다리 오브젝트 통째로 부드럽게 보간 이동 (실제 충돌판정도 실시간 이동)
        if (Vector3.Distance(transform.position, targetPos) > 0.001f)
        {
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * stepLerpSpeed);
        }
        else
        {
            transform.position = targetPos;
        }
    }

    /// <summary>
    /// 플레이어의 직접적인 붓질(Heal)에 의한 치료 수신 및 파동 전이 개시
    /// </summary>
    public void Heal(float amount)
    {
        if (isPurified) return;

        // 플레이어 직접 타격이므로 sender를 null로 지정하여 전이 연산 시작
        ApplyHealLogic(amount, null);
    }

    /// <summary>
    /// 내부 정화 누적 및 역방향을 제외한 인접 타일로의 시간차 딜레이 전이 연산 (파동 전이)
    /// </summary>
    private void ApplyHealLogic(float amount, ColoringBridge sender)
    {
        if (isPurified) return;

        int oldIntHealth = Mathf.FloorToInt(currentHealth);
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        int newIntHealth = Mathf.FloorToInt(currentHealth);

        // 1 회복할 때마다 HIT! 텍스트 생성
        if (newIntHealth > oldIntHealth)
        {
            for (int i = oldIntHealth + 1; i <= newIntHealth; i++)
            {
                SpawnHitText();
            }
        }

        UpdateVisualColor();

        // [시간차 채색 전이] 나에게 신호를 준 sender 방향을 제외한 다른 인접 타일로 시간차 전파
        if (adjacentBridges != null && adjacentBridges.Length > 0)
        {
            foreach (var adj in adjacentBridges)
            {
                // 역류 필터링: 나에게 전달해 준 타일이 아니고, 아직 정화되지 않은 경우에만 전사
                if (adj != null && adj != sender && !adj.IsPurified)
                {
                    // 0.08초의 간격을 두고 옆으로 채색 에너지를 번지게 코루틴 실행
                    StartCoroutine(SpreadHealDelayRoutine(adj, amount, 0.08f));
                }
            }
        }

        // 100% 채색 시 정화 완료 처리
        if (currentHealth >= maxHealth)
        {
            PurifyWithSender(sender);
        }
    }

    private IEnumerator SpreadHealDelayRoutine(ColoringBridge target, float amount, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (target != null && !target.IsPurified)
        {
            // 에너지를 수신한 타일은 나(this)를 sender로 지정하여 다음 인접 타일로 퍼뜨림
            target.ApplyHealLogic(amount, this);
        }
    }

    private void UpdateVisualColor()
    {
        float ratio = currentHealth / maxHealth;
        Color currentColor = Color.Lerp(startColor, targetColor, ratio);

        if (allSpriteRenderers != null)
        {
            foreach (var sr in allSpriteRenderers)
            {
                if (sr != null)
                {
                    sr.color = currentColor;
                }
            }
        }
    }

    /// <summary>
    /// 물리적 단단함 제어 (정화 전에는 IsTrigger=true로 뚫리게 하고, 정화 후에는 IsTrigger=false로 딛고 설 수 있게 함)
    /// </summary>
    private void SetSolidState(bool isSolid)
    {
        if (bridgeCollider != null)
        {
            bridgeCollider.isTrigger = !isSolid;
        }
    }

    /// <summary>
    /// 자신을 정화하고, 신호를 보내준 sender를 제외한 방향으로 0.15초 도미노 완성 전파
    /// </summary>
    private void PurifyWithSender(ColoringBridge sender)
    {
        if (isPurified) return;

        Purify();

        // [도미노 연쇄 완성 전이] 나를 정화해 준 방향(sender)을 제외하고, 0.15초 텀으로 옆으로 연쇄 완성
        if (adjacentBridges != null && adjacentBridges.Length > 0)
        {
            foreach (var adj in adjacentBridges)
            {
                if (adj != null && adj != sender && !adj.IsPurified)
                {
                    StartCoroutine(SpreadPurifyDelayRoutine(adj, 0.15f));
                }
            }
        }
    }

    private void Purify()
    {
        isPurified = true;
        currentHealth = maxHealth;

        // 정화 완료 효과음 재생 (3D 입체 음향)
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFXAtPoint(SoundManager.SFXType.EnemyRecover, transform.position, 0.95f);
        }

        UpdateVisualColor();
        UpdateSprite(); // 완성 시 스프라이트 이미지 교체
        UpdateMinimapIcon(); // [신규] 미니맵 아이콘 완성 색상(빛나는 하늘색)으로 변환
        SetSolidState(true); // 이제 단단해져서 건널 수 있는 다리가 됨

        // 레이어를 "Platform"으로 승격시켜 몬스터들의 바닥/벽 감지 레이캐스트에 걸리도록 함
        int platformLayer = LayerMask.NameToLayer("Platform");
        if (platformLayer != -1)
        {
            SetLayerRecursively(gameObject, platformLayer);
        }
    }

    /// <summary>
    /// [신규] 색칠 다리의 완성/미완성 상태에 따라 미니맵 아이콘의 색상을 다르게 스왑해 주는 함수
    /// (미완성: 어두운 회색 / 완성: 빛나는 하늘색)
    /// </summary>
    private void UpdateMinimapIcon()
    {
        int minimapLayer = LayerMask.NameToLayer("MinimapIcon");
        SpriteRenderer[] childSRs = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in childSRs)
        {
            if (sr.gameObject.layer == minimapLayer || sr.gameObject.name.ToLower().Contains("minimap"))
            {
                // 정화 완료 시 빛나는 하늘색 (Cyan), 미완성 시 어두운 회색
                sr.color = isPurified ? new Color(0.2f, 0.9f, 1f, 1f) : new Color(0.35f, 0.35f, 0.35f, 0.7f);
            }
        }
    }

    private IEnumerator SpreadPurifyDelayRoutine(ColoringBridge target, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (target != null && !target.IsPurified)
        {
            // 다음 타일도 나(this)를 sender로 등록하여 그 다음 방향으로만 도미노 완성 전파
            target.PurifyWithSender(this);
        }
    }

    /// <summary>
    /// 외부 정화 트리거 (체인용 호환 메소드 유지)
    /// </summary>
    public void PurifyFromChain()
    {
        if (isPurified) return;
        PurifyWithSender(null);
    }

    /// <summary>
    /// [추가] 인접한 다른 다리 블록에 의해 역방향으로 인접 참조를 추가받을 때 자동 확장하는 헬퍼
    /// </summary>
    public void RegisterAdjacentBridge(ColoringBridge bridge)
    {
        if (bridge == null || bridge == this) return;

        if (adjacentBridges == null)
        {
            adjacentBridges = new ColoringBridge[] { bridge };
            return;
        }

        // 이미 등록되어 있다면 스킵
        foreach (var adj in adjacentBridges)
        {
            if (adj == bridge) return;
        }

        // 동적으로 배열 확장 및 나 자신 역참조 연결
        List<ColoringBridge> list = new List<ColoringBridge>(adjacentBridges);
        list.Add(bridge);
        adjacentBridges = list.ToArray();
    }

    private bool CheckPlayerOnTop()
    {
        if (bridgeCollider == null) return false;

        // 다리 콜라이더의 바운드를 기준으로 상단 표면 위에 얇은 감지 박스 생성
        Bounds bounds = bridgeCollider.bounds;
        float checkWidth = bounds.size.x * 0.95f; 
        float checkHeight = 0.15f;                 
        Vector2 checkCenter = new Vector2(bounds.center.x, bounds.max.y + (checkHeight / 2f));

        // OverlapBoxAll로 해당 영역에 있는 모든 물체 감색 (Trigger가 아닌 실제 Player 감지)
        Collider2D[] hitColliders = Physics2D.OverlapBoxAll(checkCenter, new Vector2(checkWidth, checkHeight), 0f);
        foreach (var hit in hitColliders)
        {
            if (hit != null && hit.CompareTag("Player") && !hit.isTrigger)
            {
                return true; // 플레이어가 다리 상단에 서 있는 상태 확인됨
            }
        }
        return false;
    }

    private void UpdateSprite()
    {
        if (targetSpriteRenderer == null) return;

        if (isPurified)
        {
            if (purifiedSprite != null)
            {
                targetSpriteRenderer.sprite = purifiedSprite;
            }
        }
        else
        {
            if (defaultSprite != null)
            {
                targetSpriteRenderer.sprite = defaultSprite;
            }
        }
    }

    private void SpawnHitText()
    {
        GameObject hitTextObj = new GameObject("HitText_Popup");
        Vector3 spawnOffset = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.6f, 1.2f), 0f);
        hitTextObj.transform.position = transform.position + spawnOffset;

        TextMesh textMesh = hitTextObj.AddComponent<TextMesh>();
        textMesh.text = "HIT!";
        textMesh.fontSize = 36;
        textMesh.characterSize = 0.16f;
        textMesh.color = new Color(1f, 0.7f, 0f);
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;

        Font appliedFont = hitTextFont;
        if (appliedFont == null)
        {
            PlayerInteraction playerInt = FindFirstObjectByType<PlayerInteraction>();
            if (playerInt != null && playerInt.customFont != null)
            {
                appliedFont = playerInt.customFont;
            }
        }

        if (appliedFont != null)
        {
            textMesh.font = appliedFont;
        }

        MeshRenderer meshRenderer = hitTextObj.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingLayerName = "UI";
            meshRenderer.sortingOrder = 150;
            if (appliedFont != null)
            {
                meshRenderer.material = appliedFont.material;
            }
        }

        FloatingText floatingScript = hitTextObj.AddComponent<FloatingText>();
        if (floatingScript != null)
        {
            floatingScript.Setup(new Color(1f, 0.7f, 0f), 0.8f);
        }
    }
}
